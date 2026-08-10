using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;

namespace LSPOrderTracking.App_Code
{
    public static class LspRepository
    {
        public static DataTable GetUsers()
        {
            return FillTable("SELECT UserID, LoginID, FullName, RoleName, MachineCode, IsActive FROM Users ORDER BY LoginID");
        }

        public static void AddUser(string loginId, string password, string fullName, string roleName, string machineCode, string createdBy)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(
                "INSERT INTO Users (LoginID, UserPassword, FullName, RoleName, MachineCode, IsActive, CreatedOn) VALUES (?, ?, ?, ?, ?, True, ?)",
                connection))
            {
                command.Parameters.AddWithValue("@p1", loginId);
                command.Parameters.AddWithValue("@p2", password);
                command.Parameters.AddWithValue("@p3", fullName);
                command.Parameters.AddWithValue("@p4", roleName);
                command.Parameters.AddWithValue("@p5", machineCode);
                command.Parameters.AddWithValue("@p6", DateTime.Now);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        public static DataTable GetUserHierarchy()
        {
            var table = new DataTable();
            table.Columns.Add("Role");
            table.Columns.Add("ReportsTo");
            table.Rows.Add("Management", "Admin");
            table.Rows.Add("Order Entry", "Management");
            table.Rows.Add("Lot Making", "Management");
            table.Rows.Add("Optimisation User", "Management");
            table.Rows.Add("Production User", "Management");
            table.Rows.Add("Packing User", "Production User");
            table.Rows.Add("QC User", "Production User");
            table.Rows.Add("Dispatch User", "Management");
            table.Rows.Add("Machine Wise User", "Production User");
            return table;
        }

        public static Dictionary<string, string> GetDashboardSummary()
        {
            var values = new Dictionary<string, string>();
            values["InProduction"] = Convert.ToString(ExecuteScalar("SELECT COUNT(*) FROM Lots WHERE LotStatus='In Production'"));
            values["Packed"] = Convert.ToString(ExecuteScalar("SELECT COUNT(*) FROM PackingEntries WHERE PackingStatus='Packed'"));
            values["QcPending"] = Convert.ToString(ExecuteScalar("SELECT COUNT(*) FROM QCEntries WHERE QCStatus='QC Requested'"));
            values["DispatchReady"] = Convert.ToString(ExecuteScalar("SELECT COUNT(*) FROM DispatchEntries"));
            values["MonthlySale"] = FormatCurrencyValue(ExecuteScalar(
                "SELECT Nz(SUM(POValueWithGST),0) FROM Orders WHERE YEAR(POReceivedDate)=YEAR(Date()) AND MONTH(POReceivedDate)=MONTH(Date())"));
            return values;
        }

        public static DataTable GetRecentLots()
        {
            return FillTable(
                "SELECT TOP 10 L.LotNumber AS Lot, O.PONumber AS [PO No], O.ClientName AS Client, L.LotStatus AS Status, O.DeliveryCity AS City " +
                "FROM Lots L INNER JOIN Orders O ON L.OrderID = O.OrderID ORDER BY L.LotID DESC");
        }

        public static DataTable GetOrdersForGrid()
        {
            return FillTable(
                "SELECT TOP 20 L.LotNumber AS Lot, O.PONumber AS [PO No], O.POReceivedDate AS [PO Date], O.OrderLoggedIn AS [Order Logged In], O.POValueWithGST AS [PO Value], O.OrderStatus AS [Order Status], O.DeliveryCity AS City " +
                "FROM Lots L INNER JOIN Orders O ON L.OrderID = O.OrderID ORDER BY O.OrderID DESC");
        }

        public static void CreateOrderAndLot(OrderLotInput input, string createdBy)
        {
            using (var connection = DbHelper.CreateConnection())
            {
                connection.Open();
                var transaction = connection.BeginTransaction();

                try
                {
                    int orderId;
                    using (var orderCommand = new OleDbCommand(
                        "INSERT INTO Orders (PONumber, POReceivedDate, POValueWithGST, ClientName, DeliveryCity, OrderStatus, OrderLoggedIn, CreatedBy, CreatedOn) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
                        connection, transaction))
                    {
                        orderCommand.Parameters.AddWithValue("@p1", input.PONumber);
                        orderCommand.Parameters.AddWithValue("@p2", input.POReceivedDate);
                        orderCommand.Parameters.AddWithValue("@p3", input.POValueWithGst);
                        orderCommand.Parameters.AddWithValue("@p4", input.ClientName);
                        orderCommand.Parameters.AddWithValue("@p5", input.DeliveryCity);
                        orderCommand.Parameters.AddWithValue("@p6", input.OrderStatus);
                        orderCommand.Parameters.AddWithValue("@p7", input.OrderLoggedIn);
                        orderCommand.Parameters.AddWithValue("@p8", createdBy);
                        orderCommand.Parameters.AddWithValue("@p9", DateTime.Now);
                        orderCommand.ExecuteNonQuery();
                    }

                    using (var idCommand = new OleDbCommand("SELECT @@IDENTITY", connection, transaction))
                    {
                        orderId = Convert.ToInt32(idCommand.ExecuteScalar());
                    }

                    foreach (var line in input.Lines.Where(line => !string.IsNullOrWhiteSpace(line.SKUCode) && line.Quantity > 0))
                    {
                        InsertOrderItem(connection, transaction, orderId, line);
                    }

                    using (var lotCommand = new OleDbCommand(
                        "INSERT INTO Lots (LotNumber, OrderID, JITDate, MachineCode, LotStatus) VALUES (?, ?, ?, ?, ?)",
                        connection, transaction))
                    {
                        lotCommand.Parameters.AddWithValue("@p1", input.LotNumber);
                        lotCommand.Parameters.AddWithValue("@p2", orderId);
                        lotCommand.Parameters.AddWithValue("@p3", input.JitDate);
                        lotCommand.Parameters.AddWithValue("@p4", input.MachineCode);
                        lotCommand.Parameters.AddWithValue("@p5", input.LotStatus);
                        lotCommand.ExecuteNonQuery();
                    }

                    using (var reportCommand = new OleDbCommand(
                        "INSERT INTO ManagementReportCache (LotNumber, PONumber, POReceivedDate, POValueWithGST, Item1, Qty1, Item2, Qty2, Item3, Qty3, Item4, Qty4, Item5, Qty5, OrderStatus, DeliveryCity) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                        connection, transaction))
                    {
                        reportCommand.Parameters.AddWithValue("@p1", input.LotNumber);
                        reportCommand.Parameters.AddWithValue("@p2", input.PONumber);
                        reportCommand.Parameters.AddWithValue("@p3", input.POReceivedDate);
                        reportCommand.Parameters.AddWithValue("@p4", input.POValueWithGst);
                        reportCommand.Parameters.AddWithValue("@p5", GetLineDescription(input, 0));
                        reportCommand.Parameters.AddWithValue("@p6", GetLineQuantity(input, 0));
                        reportCommand.Parameters.AddWithValue("@p7", GetLineDescription(input, 1));
                        reportCommand.Parameters.AddWithValue("@p8", GetLineQuantity(input, 1));
                        reportCommand.Parameters.AddWithValue("@p9", GetLineDescription(input, 2));
                        reportCommand.Parameters.AddWithValue("@p10", GetLineQuantity(input, 2));
                        reportCommand.Parameters.AddWithValue("@p11", GetLineDescription(input, 3));
                        reportCommand.Parameters.AddWithValue("@p12", GetLineQuantity(input, 3));
                        reportCommand.Parameters.AddWithValue("@p13", GetLineDescription(input, 4));
                        reportCommand.Parameters.AddWithValue("@p14", GetLineQuantity(input, 4));
                        reportCommand.Parameters.AddWithValue("@p15", input.LotStatus);
                        reportCommand.Parameters.AddWithValue("@p16", input.DeliveryCity);
                        reportCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static DataTable GetLotsForLookup()
        {
            return FillTable("SELECT LotID, LotNumber FROM Lots ORDER BY LotNumber");
        }

        public static DataTable GetSkuLookup()
        {
            return FillTable("SELECT SKUCode, SKUDescription, SKURate FROM ProductMaster WHERE IsActive=True ORDER BY SKUCode");
        }

        public static SkuInfo GetSkuInfo(string skuCode)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand("SELECT TOP 1 SKUCode, SKUDescription, SKURate FROM ProductMaster WHERE SKUCode=? AND IsActive=True", connection))
            {
                command.Parameters.AddWithValue("@p1", skuCode);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (reader != null && reader.Read())
                    {
                        return new SkuInfo
                        {
                            SKUCode = Convert.ToString(reader["SKUCode"]),
                            Description = Convert.ToString(reader["SKUDescription"]),
                            Rate = Convert.ToDecimal(reader["SKURate"])
                        };
                    }
                }
            }

            return new SkuInfo();
        }

        public static void AddProduction(int lotId, int producedQty, int rejectedQty, string shopfloorStatus, string enteredBy)
        {
            SaveLotStatus(
                "INSERT INTO ProductionEntries (LotID, ProducedQty, RejectedQty, ShopfloorStatus, EntryDate, EnteredBy) VALUES (?, ?, ?, ?, ?, ?)",
                "UPDATE Lots SET LotStatus=? WHERE LotID=?",
                lotId,
                shopfloorStatus,
                producedQty,
                rejectedQty,
                enteredBy);
        }

        public static void AddPacking(int lotId, int boxQty, int packedQty, string packingStatus, string enteredBy)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(
                "INSERT INTO PackingEntries (LotID, BoxQty, PackedQty, PackingStatus, EntryDate, EnteredBy) VALUES (?, ?, ?, ?, ?, ?)",
                connection))
            {
                command.Parameters.AddWithValue("@p1", lotId);
                command.Parameters.AddWithValue("@p2", boxQty);
                command.Parameters.AddWithValue("@p3", packedQty);
                command.Parameters.AddWithValue("@p4", packingStatus);
                command.Parameters.AddWithValue("@p5", DateTime.Now);
                command.Parameters.AddWithValue("@p6", enteredBy);
                connection.Open();
                command.ExecuteNonQuery();
            }

            UpdateLotStatus(lotId, packingStatus);
        }

        public static void AddQc(int lotId, string qcStatus, string remark, string enteredBy)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(
                "INSERT INTO QCEntries (LotID, QCStatus, Remark, EntryDate, EnteredBy) VALUES (?, ?, ?, ?, ?)",
                connection))
            {
                command.Parameters.AddWithValue("@p1", lotId);
                command.Parameters.AddWithValue("@p2", qcStatus);
                command.Parameters.AddWithValue("@p3", remark);
                command.Parameters.AddWithValue("@p4", DateTime.Now);
                command.Parameters.AddWithValue("@p5", enteredBy);
                connection.Open();
                command.ExecuteNonQuery();
            }

            UpdateLotStatus(lotId, qcStatus);
        }

        public static void AddDispatch(int lotId, DateTime dispatchDate, string transportInvoice, decimal transportCharge, string dispatchStatus, string enteredBy)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(
                "INSERT INTO DispatchEntries (LotID, DispatchDate, TransportInvoice, TransportCharge, DispatchStatus, EnteredBy) VALUES (?, ?, ?, ?, ?, ?)",
                connection))
            {
                command.Parameters.AddWithValue("@p1", lotId);
                command.Parameters.AddWithValue("@p2", dispatchDate);
                command.Parameters.AddWithValue("@p3", transportInvoice);
                command.Parameters.AddWithValue("@p4", transportCharge);
                command.Parameters.AddWithValue("@p5", dispatchStatus);
                command.Parameters.AddWithValue("@p6", enteredBy);
                connection.Open();
                command.ExecuteNonQuery();
            }

            UpdateLotStatus(lotId, dispatchStatus);
        }

        public static DataTable GetProductionEntries()
        {
            return FillTable(
                "SELECT TOP 20 L.LotNumber AS Lot, P.ProducedQty, P.RejectedQty, P.ShopfloorStatus AS Status, P.EntryDate " +
                "FROM ProductionEntries P INNER JOIN Lots L ON P.LotID=L.LotID ORDER BY P.ProductionID DESC");
        }

        public static DataTable GetPackingEntries()
        {
            return FillTable(
                "SELECT TOP 20 L.LotNumber AS Lot, P.BoxQty, P.PackedQty, P.PackingStatus AS Status, P.EntryDate " +
                "FROM PackingEntries P INNER JOIN Lots L ON P.LotID=L.LotID ORDER BY P.PackingID DESC");
        }

        public static DataTable GetQcEntries()
        {
            return FillTable(
                "SELECT TOP 20 L.LotNumber AS Lot, Q.QCStatus AS Status, Q.Remark, Q.EntryDate " +
                "FROM QCEntries Q INNER JOIN Lots L ON Q.LotID=L.LotID ORDER BY Q.QCID DESC");
        }

        public static DataTable GetDispatchEntries()
        {
            return FillTable(
                "SELECT TOP 20 L.LotNumber AS Lot, D.DispatchDate, D.TransportInvoice, D.TransportCharge, D.DispatchStatus AS Status " +
                "FROM DispatchEntries D INNER JOIN Lots L ON D.LotID=L.LotID ORDER BY D.DispatchID DESC");
        }

        public static DataTable GetManagementReport(string poNumber, string lotNumber, string city, string status)
        {
            var query =
                "SELECT LotNumber AS Lot, PONumber AS [PO No], POReceivedDate AS [PO Received Date], POValueWithGST AS [PO Value With GST], " +
                "Item1 AS [1st PO Item], Qty1 AS [Qty 1], Item2 AS [2nd PO Item], Qty2 AS [Qty 2], Item3 AS [3rd PO Item], Qty3 AS [Qty 3], " +
                "Item4 AS [4th PO Item], Qty4 AS [Qty 4], Item5 AS [5th PO Item], Qty5 AS [Qty 5], OrderStatus AS [Order Status], DeliveryCity AS City " +
                "FROM ManagementReportCache WHERE 1=1";

            var parameters = new List<OleDbParameter>();

            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                query += " AND PONumber LIKE ?";
                parameters.Add(new OleDbParameter("@p", "%" + poNumber + "%"));
            }
            if (!string.IsNullOrWhiteSpace(lotNumber))
            {
                query += " AND LotNumber LIKE ?";
                parameters.Add(new OleDbParameter("@p", "%" + lotNumber + "%"));
            }
            if (!string.IsNullOrWhiteSpace(city))
            {
                query += " AND DeliveryCity LIKE ?";
                parameters.Add(new OleDbParameter("@p", "%" + city + "%"));
            }
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All Status", StringComparison.OrdinalIgnoreCase))
            {
                query += " AND OrderStatus = ?";
                parameters.Add(new OleDbParameter("@p", status));
            }

            query += " ORDER BY ReportID DESC";
            return FillTable(query, parameters.ToArray());
        }

        private static void SaveLotStatus(string insertSql, string updateSql, int lotId, string lotStatus, int producedQty, int rejectedQty, string enteredBy)
        {
            using (var connection = DbHelper.CreateConnection())
            {
                connection.Open();
                using (var insert = new OleDbCommand(insertSql, connection))
                {
                    insert.Parameters.AddWithValue("@p1", lotId);
                    insert.Parameters.AddWithValue("@p2", producedQty);
                    insert.Parameters.AddWithValue("@p3", rejectedQty);
                    insert.Parameters.AddWithValue("@p4", lotStatus);
                    insert.Parameters.AddWithValue("@p5", DateTime.Now);
                    insert.Parameters.AddWithValue("@p6", enteredBy);
                    insert.ExecuteNonQuery();
                }

                using (var update = new OleDbCommand(updateSql, connection))
                {
                    update.Parameters.AddWithValue("@p1", lotStatus);
                    update.Parameters.AddWithValue("@p2", lotId);
                    update.ExecuteNonQuery();
                }
            }
        }

        private static void UpdateLotStatus(int lotId, string status)
        {
            using (var connection = DbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = new OleDbCommand("UPDATE Lots SET LotStatus=? WHERE LotID=?", connection))
                {
                    command.Parameters.AddWithValue("@p1", status);
                    command.Parameters.AddWithValue("@p2", lotId);
                    command.ExecuteNonQuery();
                }

                using (var reportCommand = new OleDbCommand(
                    "UPDATE ManagementReportCache SET OrderStatus=? WHERE LotNumber=(SELECT LotNumber FROM Lots WHERE LotID=?)",
                    connection))
                {
                    reportCommand.Parameters.AddWithValue("@p1", status);
                    reportCommand.Parameters.AddWithValue("@p2", lotId);
                    reportCommand.ExecuteNonQuery();
                }
            }
        }

        private static void InsertOrderItem(OleDbConnection connection, OleDbTransaction transaction, int orderId, OrderLineInput line)
        {
            if (string.IsNullOrWhiteSpace(line.Description) || line.Quantity <= 0)
            {
                return;
            }

            using (var command = new OleDbCommand(
                "INSERT INTO OrderItems (OrderID, ItemSequence, ItemName, ItemQty, SKUCode, ItemRate, ItemAmount) VALUES (?, ?, ?, ?, ?, ?, ?)",
                connection, transaction))
            {
                command.Parameters.AddWithValue("@p1", orderId);
                command.Parameters.AddWithValue("@p2", line.Sequence);
                command.Parameters.AddWithValue("@p3", line.Description);
                command.Parameters.AddWithValue("@p4", line.Quantity);
                command.Parameters.AddWithValue("@p5", line.SKUCode);
                command.Parameters.AddWithValue("@p6", line.Rate);
                command.Parameters.AddWithValue("@p7", line.Amount);
                command.ExecuteNonQuery();
            }
        }

        private static string GetLineDescription(OrderLotInput input, int index)
        {
            return input.Lines.Count > index ? input.Lines[index].Description : string.Empty;
        }

        private static int GetLineQuantity(OrderLotInput input, int index)
        {
            return input.Lines.Count > index ? input.Lines[index].Quantity : 0;
        }

        private static DataTable FillTable(string query, params OleDbParameter[] parameters)
        {
            var table = new DataTable();
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(query, connection))
            using (var adapter = new OleDbDataAdapter(command))
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                connection.Open();
                adapter.Fill(table);
            }
            return table;
        }

        private static object ExecuteScalar(string query)
        {
            using (var connection = DbHelper.CreateConnection())
            using (var command = new OleDbCommand(query, connection))
            {
                connection.Open();
                return command.ExecuteScalar();
            }
        }

        private static string FormatCurrencyValue(object value)
        {
            decimal amount;
            decimal.TryParse(Convert.ToString(value), out amount);
            return amount.ToString("0.##");
        }
    }

    public sealed class OrderLotInput
    {
        public string PONumber { get; set; }
        public DateTime POReceivedDate { get; set; }
        public DateTime OrderLoggedIn { get; set; }
        public decimal POValueWithGst { get; set; }
        public string ClientName { get; set; }
        public string DeliveryCity { get; set; }
        public string OrderStatus { get; set; }
        public string LotNumber { get; set; }
        public string MachineCode { get; set; }
        public DateTime JitDate { get; set; }
        public string LotStatus { get; set; }
        public List<OrderLineInput> Lines { get; set; }
    }

    public sealed class OrderLineInput
    {
        public int Sequence { get; set; }
        public string SKUCode { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
    }

    public sealed class SkuInfo
    {
        public string SKUCode { get; set; }
        public string Description { get; set; }
        public decimal Rate { get; set; }
    }
}
