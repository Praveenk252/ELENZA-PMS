using System.Collections.Generic;
using System.Data;

namespace LSPOrderTracking.App_Code
{
    public static class SampleData
    {
        public static DataTable DashboardStatus()
        {
            var table = new DataTable();
            table.Columns.Add("Metric");
            table.Columns.Add("Value");

            table.Rows.Add("Orders In Production", "117");
            table.Rows.Add("Packed", "44");
            table.Rows.Add("QC Pending", "13");
            table.Rows.Add("Dispatch Ready", "21");
            table.Rows.Add("Monthly Sale", "18.40 Lakh");

            return table;
        }

        public static DataTable TodayOrders()
        {
            var table = new DataTable();
            table.Columns.Add("Lot");
            table.Columns.Add("PO No");
            table.Columns.Add("Client");
            table.Columns.Add("Status");
            table.Columns.Add("City");

            table.Rows.Add("Lot 186", "PO-1354-260602757", "Livspace", "In Production", "Mumbai");
            table.Rows.Add("Lot 187", "PO-1354-260602749", "Livspace", "Packed", "Mumbai");
            table.Rows.Add("Lot 188", "PO-1354-260600137", "Livspace", "QC Pending", "Bangalore");
            table.Rows.Add("Lot 189", "PO-1354-260602208", "Livspace", "Dispatch Ready", "Chennai");

            return table;
        }

        public static DataTable ManagementReport()
        {
            var table = new DataTable();
            table.Columns.Add("Lot");
            table.Columns.Add("PO No");
            table.Columns.Add("PO Date");
            table.Columns.Add("PO Value With GST");
            table.Columns.Add("1st PO Item");
            table.Columns.Add("Qty 1");
            table.Columns.Add("2nd PO Item");
            table.Columns.Add("Qty 2");
            table.Columns.Add("Order Status");
            table.Columns.Add("City");

            table.Rows.Add("Lot 186", "PO-1354-260602757", "Jun 02, 2026", "9777.48", "Duron Bedside Table", "2", "Ohio Bedside Table", "2", "In Production", "Mumbai");
            table.Rows.Add("Lot 187", "PO-1354-260602749", "Jun 02, 2026", "4363.64", "Ohio Bedside Table", "1", "Ohio Bedside Table", "1", "Packed", "Mumbai");
            table.Rows.Add("Lot 188", "PO-1354-260600137", "Jun 01, 2026", "15883.98", "Sarah Upholstered Bed Frame, King- Fully Hydraulic(Carcass)", "1", "", "", "QC Pending", "Bangalore");

            return table;
        }

        public static List<string> Roles()
        {
            return new List<string>
            {
                "Admin",
                "Management",
                "Order Entry",
                "Lot Making",
                "Optimisation User",
                "Production User",
                "Packing User",
                "QC User",
                "Dispatch User",
                "Machine Wise User"
            };
        }
    }
}
