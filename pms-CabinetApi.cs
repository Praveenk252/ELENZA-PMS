using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace CabinetStore
{
    public class CabinetApi : IHttpHandler
    {
        private const string NeutralRegistrationConflictMessage = "These registration details cannot be used. Please review your details or sign in.";
        private const string MailFromAddress = "praveenk252@gmail.com";
        private const string MailAppPassword = "xerb yurz zkxe eoys";
        private const string NewUserNotificationBcc = "praveenk25286@gmail.com";

        private string DbPath
        {
            get
            {
                var candidates = new[]
                {
                    HttpContext.Current.Server.MapPath("~/App_Data/Cabinets.accdb"),
                    HttpContext.Current.Server.MapPath("/pms/App_Data/Cabinets.accdb"),
                    @"h:\root\home\elenzapms-001\www\App_Data\Cabinets.accdb",
                    @"h:\root\home\elenzapms-001\www\pms\App_Data\Cabinets.accdb"
                };
                foreach (var path in candidates)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return path;
                }
                return candidates[candidates.Length - 1];
            }
        }
        private static string S(object value)
        {
            return value == null || value == DBNull.Value ? "" : value.ToString();
        }

        private static object Scalar(OleDbConnection conn, string sql, params object[] values)
        {
            using (var cmd = new OleDbCommand(sql, conn))
            {
                foreach (var value in values)
                    cmd.Parameters.AddWithValue("?", value ?? "");
                return cmd.ExecuteScalar();
            }
        }

        private static void SendMail(string to, string subject, string body, string bcc)
        {
            SendMail(to, subject, body, null, bcc, false);
        }

        private static void SendMail(string to, string subject, string body, string htmlBody, string bcc, bool isHtml)
        {
            using (var smtp = new System.Net.Mail.SmtpClient())
            {
                smtp.Host = "smtp.gmail.com";
                smtp.Port = 587;
                smtp.EnableSsl = true;
                smtp.Credentials = new System.Net.NetworkCredential(MailFromAddress, MailAppPassword);

                using (var msg = new System.Net.Mail.MailMessage())
                {
                    msg.From = new System.Net.Mail.MailAddress(MailFromAddress);
                    msg.To.Add(to);
                    if (!string.IsNullOrEmpty(bcc))
                        msg.Bcc.Add(bcc);
                    msg.Subject = subject;
                    if (isHtml && !string.IsNullOrEmpty(htmlBody))
                    {
                        msg.Body = body;
                        msg.IsBodyHtml = false;
                        msg.AlternateViews.Add(
                            System.Net.Mail.AlternateView.CreateAlternateViewFromString(body, Encoding.UTF8, "text/plain")
                        );
                        msg.AlternateViews.Add(
                            System.Net.Mail.AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html")
                        );
                    }
                    else
                    {
                        msg.Body = body;
                        msg.IsBodyHtml = false;
                    }
                    smtp.Send(msg);
                }
            }
        }

        private static string ResolveAction(HttpContext context)
        {
            var action = (context.Request["action"] ?? "").ToLower();
            if (!string.IsNullOrEmpty(action)) return action;

            if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    context.Request.InputStream.Position = 0;
                    var json = new System.IO.StreamReader(context.Request.InputStream).ReadToEnd();
                    context.Request.InputStream.Position = 0;
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var obj = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        if (obj != null && obj.ContainsKey("action") && obj["action"] != null)
                            return obj["action"].ToString().ToLower();
                    }
                }
                catch
                {
                }
            }

            return "";
        }

        private Dictionary<string, int> GetCabinetBounds(OleDbConnection conn, int cabId)
        {
            using (var cmd = new OleDbCommand("SELECT TOP 1 WidthMin, WidthMax, DepthMin, DepthMax, HeightMin, HeightMax, StepSize FROM [CabinetOptions] WHERE CabinetID = ?", conn))
            {
                cmd.Parameters.AddWithValue("?", cabId);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        return new Dictionary<string, int>
                        {
                            { "WidthMin", rdr["WidthMin"] != DBNull.Value ? Convert.ToInt32(rdr["WidthMin"]) : 600 },
                            { "WidthMax", rdr["WidthMax"] != DBNull.Value ? Convert.ToInt32(rdr["WidthMax"]) : 1200 },
                            { "DepthMin", rdr["DepthMin"] != DBNull.Value ? Convert.ToInt32(rdr["DepthMin"]) : 560 },
                            { "DepthMax", rdr["DepthMax"] != DBNull.Value ? Convert.ToInt32(rdr["DepthMax"]) : 560 },
                            { "HeightMin", rdr["HeightMin"] != DBNull.Value ? Convert.ToInt32(rdr["HeightMin"]) : 700 },
                            { "HeightMax", rdr["HeightMax"] != DBNull.Value ? Convert.ToInt32(rdr["HeightMax"]) : 700 },
                            { "StepSize", rdr["StepSize"] != DBNull.Value ? Convert.ToInt32(rdr["StepSize"]) : 50 }
                        };
                    }
                }
            }

            return new Dictionary<string, int>
            {
                { "WidthMin", 600 }, { "WidthMax", 1200 },
                { "DepthMin", 560 }, { "DepthMax", 560 },
                { "HeightMin", 700 }, { "HeightMax", 700 },
                { "StepSize", 50 }
            };
        }

        private string ValidateCabinetDimensions(Dictionary<string, int> bounds, double W, double D, double H)
        {
            if (W < bounds["WidthMin"] || W > bounds["WidthMax"])
                return "Width must be between " + bounds["WidthMin"] + " mm and " + bounds["WidthMax"] + " mm.";

            if (D < bounds["DepthMin"] || D > bounds["DepthMax"])
                return "Depth must be between " + bounds["DepthMin"] + " mm and " + bounds["DepthMax"] + " mm.";

            if (H < bounds["HeightMin"] || H > bounds["HeightMax"])
                return "Height must be between " + bounds["HeightMin"] + " mm and " + bounds["HeightMax"] + " mm.";

            int step = bounds["StepSize"] <= 0 ? 50 : bounds["StepSize"];
            if (((int)Math.Round(W) - bounds["WidthMin"]) % step != 0)
                return "Width must follow the configured step size of " + step + " mm.";
            if (((int)Math.Round(D) - bounds["DepthMin"]) % step != 0)
                return "Depth must follow the configured step size of " + step + " mm.";
            if (((int)Math.Round(H) - bounds["HeightMin"]) % step != 0)
                return "Height must follow the configured step size of " + step + " mm.";

            return "";
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");

            try
            {
                string action = ResolveAction(context);
                string method = context.Request.HttpMethod.ToUpper();

                switch (action)
                {
                    case "categories": HandleCategories(context); break;
                    case "cabinets": HandleCabinets(context); break;
                    case "cabinet_detail": HandleCabinetDetail(context); break;
                    case "cabinet_images": HandleCabinetImages(context); break;
                    case "board_images": HandleBoardImages(context); break;
                    case "laminate_images": HandleLaminateImages(context); break;
                    case "hardware_images": HandleHardwareImages(context); break;
                    case "price": HandlePrice(context); break;
                    case "register": HandleRegister(context); break;
                    case "login": HandleLogin(context); break;
                    case "cart_save": HandleCartSave(context); break;
                    case "cart_load": HandleCartLoad(context); break;
                    case "cart_price": HandleCartPrice(context); break;
                    case "place_order": HandlePlaceOrder(context); break;
                    case "order_detail": HandleOrderDetail(context); break;
                    case "my_orders": HandleMyOrders(context); break;
                    case "quotation_html": HandleQuotationHTML(context); break;
                    case "invoice_html": HandleInvoiceHTML(context); break;
                    case "offers_active": HandleOffersActive(context); break;
                    case "boq_html": HandleBoqHtml(context); break;
                    case "boq_excel": HandleBoqExcel(context); break;
                    case "quote_excel": HandleQuoteExcel(context); break;
                    case "hardware_list": HandleHardwareList(context); break;
                    case "board_options": HandleBoardOptions(context); break;
                    case "laminate_options": HandleLaminateOptions(context); break;
                    case "send_otp": HandleSendOTP(context); break;
                    case "verify_otp": HandleVerifyOTP(context); break;
                    case "prod_orders": HandleProdOrders(context); break;
                    case "prod_order_detail": HandleProdOrderDetail(context); break;
                    case "prod_update_status": HandleProdUpdateStatus(context); break;
                    case "prod_drill_programs": HandleProdDrillPrograms(context); break;
                    case "get_profile": HandleGetProfile(context); break;
                    case "save_profile": HandleSaveProfile(context); break;
                    case "pending_tax_list": HandlePendingTaxList(context); break;
                    case "approve_tax": HandleApproveTax(context); break;
                    case "download_template": HandleDownloadTemplate(context); break;
                    case "parse_upload": HandleParseUploadExcel(context); break;
                    default: JsonError(context, "Unknown action: " + action); break;
                }
            }
            catch (Exception ex)
            {
                JsonError(context, ex.Message);
            }
        }

        bool IHttpHandler.IsReusable { get { return true; } }

        // --- Categories ---
        private void HandleCategories(HttpContext ctx)
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Categories] ORDER BY SortOrder", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "CategoryID", "CategoryName", "Description", "ImageURL", "SortOrder"));
            }
            JsonOK(ctx, list);
        }

        // --- Cabinets ---
        private void HandleCabinets(HttpContext ctx)
        {
            int catId = ParseInt(ctx.Request["catID"]);
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Cabinets] WHERE CategoryID = ? ORDER BY SortOrder", conn);
                cmd.Parameters.AddWithValue("?", catId);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "CabinetID", "CategoryID", "ModelName", "ModelCode", "Description", "ImageURL", "SortOrder"));
            }
            JsonOK(ctx, list);
        }

        // --- Cabinet Detail ---
        private void HandleCabinetDetail(HttpContext ctx)
        {
            int cabId = ParseInt(ctx.Request["cabID"]);
            var result = new Dictionary<string, object>();

            using (var conn = GetConn())
            {
                conn.Open();

                // Cabinet info
                using (var cmd = new OleDbCommand("SELECT * FROM [Cabinets] WHERE CabinetID = ?", conn))
                {
                    cmd.Parameters.AddWithValue("?", cabId);
                    using (var rdr = cmd.ExecuteReader())
                        if (rdr.Read())
                            result["cabinet"] = ReadDict(rdr, "CabinetID", "CategoryID", "ModelName", "ModelCode", "Description", "ImageURL");
                }

                // Cabinet images
                var imgList = new List<Dictionary<string, object>>();
                using (var cmd = new OleDbCommand("SELECT * FROM [CabinetImages] WHERE CabinetID = ? ORDER BY SortOrder, ImageID", conn))
                {
                    cmd.Parameters.AddWithValue("?", cabId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var img = new Dictionary<string, object>();
                            img["ImageID"] = Convert.ToInt32(rdr["ImageID"]);
                            img["ImageURL"] = rdr["ImageURL"].ToString();
                            img["AltText"] = rdr["AltText"] != DBNull.Value ? rdr["AltText"].ToString() : "";
                            imgList.Add(img);
                        }
                    }
                }
                result["images"] = imgList;

                // Options with hardcoded defaults
                var optsList = new List<Dictionary<string, object>>();
                using (var cmd = new OleDbCommand("SELECT * FROM [CabinetOptions] WHERE CabinetID = ?", conn))
                {
                    cmd.Parameters.AddWithValue("?", cabId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            optsList.Add(ReadDict(rdr, "OptionID", "WidthMin", "WidthMax", "DepthMin", "DepthMax", "HeightMin", "HeightMax", "StepSize"));
                    }
                }
                if (optsList.Count == 0)
                {
                    optsList.Add(new Dictionary<string, object> {
                        { "WidthMin", 600 }, { "WidthMax", 1200 }, { "StepSize", 50 },
                        { "DepthMin", 560 }, { "DepthMax", 560 },
                        { "HeightMin", 700 }, { "HeightMax", 700 }
                    });
                }
                result["options"] = optsList;

                // Materials (stable explicit list to avoid host-specific lookup issues)
                result["materials"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "MaterialID", 1 }, { "Name", "MR Ply" } },
                    new Dictionary<string, object> { { "MaterialID", 3 }, { "Name", "BWP Ply" } }
                };

                // Thicknesses (hardcoded to avoid complex queries)
                result["defaultThicknessID"] = 2;
                result["thicknesses"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "ThicknessID", 2 }, { "ThicknessValue", 18 } }
                };

                // Colours (stable explicit list to avoid host-specific lookup issues)
                result["colours"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "ColourID", 11 }, { "ColourName", "Off White" }, { "HexCode", "#F8F6F0" } },
                    new Dictionary<string, object> { { "ColourID", 10 }, { "ColourName", "Fabric" }, { "HexCode", "#E8E0D8" } }
                };

                // Hardware (return empty — handles not required)
                result["hardware"] = new List<Dictionary<string, object>>();
            }

            JsonOK(ctx, result);
        }

        // --- Cabinet Images ---
        private void HandleCabinetImages(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            ctx.Request.InputStream.Position = 0;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int cabId = ParseInt(GetPostParam(data, "cabID"));

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT * FROM [CabinetImages] WHERE CabinetID = ? ORDER BY SortOrder, ImageID", conn))
                {
                    cmd.Parameters.AddWithValue("?", cabId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var img = new Dictionary<string, object>();
                            img["ImageID"] = Convert.ToInt32(rdr["ImageID"]);
                            img["ImageURL"] = rdr["ImageURL"].ToString();
                            img["AltText"] = rdr["AltText"] != DBNull.Value ? rdr["AltText"].ToString() : "";
                            list.Add(img);
                        }
                    }
                }
            }

            JsonOK(ctx, new Dictionary<string, object> { { "images", list }, { "count", list.Count } });
        }

        private void HandleBoardImages(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            ctx.Request.InputStream.Position = 0;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int matId = ParseInt(GetPostParam(data, "materialID"));

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT * FROM [BoardImages] WHERE MaterialID = ? ORDER BY SortOrder, ImageID", conn))
                {
                    cmd.Parameters.AddWithValue("?", matId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var img = new Dictionary<string, object>();
                            img["ImageID"] = Convert.ToInt32(rdr["ImageID"]);
                            img["ImageURL"] = rdr["ImageURL"].ToString();
                            img["AltText"] = rdr["AltText"] != DBNull.Value ? rdr["AltText"].ToString() : "";
                            list.Add(img);
                        }
                    }
                }
            }

            JsonOK(ctx, new Dictionary<string, object> { { "images", list }, { "count", list.Count } });
        }

        private void HandleLaminateImages(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            ctx.Request.InputStream.Position = 0;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int lamId = ParseInt(GetPostParam(data, "laminateID"));

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT * FROM [LaminateImages] WHERE LaminateID = ? ORDER BY SortOrder, ImageID", conn))
                {
                    cmd.Parameters.AddWithValue("?", lamId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var img = new Dictionary<string, object>();
                            img["ImageID"] = Convert.ToInt32(rdr["ImageID"]);
                            img["ImageURL"] = rdr["ImageURL"].ToString();
                            img["AltText"] = rdr["AltText"] != DBNull.Value ? rdr["AltText"].ToString() : "";
                            list.Add(img);
                        }
                    }
                }
            }

            JsonOK(ctx, new Dictionary<string, object> { { "images", list }, { "count", list.Count } });
        }

        private void HandleHardwareImages(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            ctx.Request.InputStream.Position = 0;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int hwId = ParseInt(GetPostParam(data, "hardwareID"));

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT * FROM [HardwareImages] WHERE HardwareID = ? ORDER BY SortOrder, ImageID", conn))
                {
                    cmd.Parameters.AddWithValue("?", hwId);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            var img = new Dictionary<string, object>();
                            img["ImageID"] = Convert.ToInt32(rdr["ImageID"]);
                            img["ImageURL"] = rdr["ImageURL"].ToString();
                            img["AltText"] = rdr["AltText"] != DBNull.Value ? rdr["AltText"].ToString() : "";
                            list.Add(img);
                        }
                    }
                }
            }

            JsonOK(ctx, new Dictionary<string, object> { { "images", list }, { "count", list.Count } });
        }

        // --- Price (BOQ calculation) ---
        private void HandlePrice(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            ctx.Request.InputStream.Position = 0;
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);

            int cabId = ParseInt(GetPostParam(data, "cabID"));
            double W = ParseDouble(GetPostParam(data, "W"));
            double D = ParseDouble(GetPostParam(data, "D"));
            double H = ParseDouble(GetPostParam(data, "H"));
            int matId = ParseInt(GetPostParam(data, "materialID"));
            int thickId = ParseInt(GetPostParam(data, "thicknessID"));
            int colId = ParseInt(GetPostParam(data, "colourID"));

            var result = new Dictionary<string, object>();
            var panels = new List<Dictionary<string, object>>();
            double subtotal = 0;

            using (var conn = GetConn())
            {
                conn.Open();
                var bounds = GetCabinetBounds(conn, cabId);
                var dimensionError = ValidateCabinetDimensions(bounds, W, D, H);
                if (!string.IsNullOrEmpty(dimensionError))
                {
                    JsonError(ctx, dimensionError);
                    return;
                }

                // Get panel definitions with formulas
                var cmd = new OleDbCommand(@"SELECT pd.*, bt.ThicknessValue FROM [PanelDefinitions] pd 
                    LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID = bt.ThicknessID
                    WHERE pd.CabinetID = ? ORDER BY pd.SortOrder", conn);
                cmd.Parameters.AddWithValue("?", cabId);

                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int panelDefId = Convert.ToInt32(rdr["PanelDefID"]);
                        string panelName = rdr["PanelName"].ToString();
                        double thickness = rdr["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(rdr["ThicknessValue"]) : 18;
                        string faceDim1 = (rdr["FaceDim1"] ?? "Width").ToString();
                        string faceDim2 = (rdr["FaceDim2"] ?? "Height").ToString();

                        // Get formulas for this panel
                        var fCmd = new OleDbCommand("SELECT * FROM [PanelFormulas] WHERE PanelDefID = ?", conn);
                        fCmd.Parameters.AddWithValue("?", panelDefId);
                        double pW = 0, pD = 0, pH = 0;

                        using (var fRdr = fCmd.ExecuteReader())
                        {
                            while (fRdr.Read())
                            {
                                string dimType = fRdr["DimensionType"].ToString();
                                string expr = fRdr["Expression"].ToString();
                                double val = FormulaEngine.Evaluate(expr, W, D, H, thickness);

                                if (dimType == "Width") pW = val;
                                else if (dimType == "Depth") pD = val;
                                else if (dimType == "Height") pH = val;
                            }
                        }

                        double dim1 = 0, dim2 = 0;
                        if (faceDim1 == "Width") dim1 = pW;
                        else if (faceDim1 == "Depth") dim1 = pD;
                        else if (faceDim1 == "Height") dim1 = pH;

                        if (faceDim2 == "Width") dim2 = pW;
                        else if (faceDim2 == "Depth") dim2 = pD;
                        else if (faceDim2 == "Height") dim2 = pH;

                        double sft = FormulaEngine.ComputeSFT(dim1, dim2);

                        // Get pricing (use panel's own DefaultThicknessID)
                        int panelThickId = rdr["DefaultThicknessID"] != DBNull.Value ? Convert.ToInt32(rdr["DefaultThicknessID"]) : thickId;
                        double pricePerSFT = GetPricePerSFT(conn, matId, panelThickId, colId);
                        double panelTotal = Math.Round(sft * pricePerSFT, 2);

                        var panel = new Dictionary<string, object>();
                        panel["name"] = panelName;
                        panel["width"] = pW;
                        panel["depth"] = pD;
                        panel["height"] = pH;
                        panel["dim1"] = dim1;
                        panel["dim2"] = dim2;
                        panel["faceDim1"] = faceDim1;
                        panel["faceDim2"] = faceDim2;
                        panel["sft"] = sft;
                        panel["thickness"] = thickness;
                        panel["pricePerSFT"] = pricePerSFT;
                        panel["total"] = panelTotal;
                        panels.Add(panel);
                        subtotal += panelTotal;
                    }
                }

                // Hardware — auto-included from CabinetHardwareMap where Quantity>0
                var hwList = new List<Dictionary<string, object>>();
                double hwTotal = 0;
                var hwCmd = new OleDbCommand(@"SELECT h.*, ch.Quantity FROM [HardwareItems] h 
                    INNER JOIN [CabinetHardwareMap] ch ON h.HardwareID = ch.HardwareID 
                    WHERE ch.CabinetID = ? AND ch.Quantity > 0", conn);
                hwCmd.Parameters.AddWithValue("?", cabId);
                using (var hwRdr = hwCmd.ExecuteReader())
                {
                    while (hwRdr.Read())
                    {
                        string name = hwRdr["HardwareName"].ToString();
                        double unitPrice = Convert.ToDouble(hwRdr["UnitPrice"]);
                        int qty = Convert.ToInt32(hwRdr["Quantity"]);
                        double lineTotal = unitPrice * qty;

                        var hw = new Dictionary<string, object>();
                        hw["name"] = name;
                        hw["unitPrice"] = unitPrice;
                        hw["qty"] = qty;
                        hw["total"] = lineTotal;
                        hwList.Add(hw);
                        hwTotal += lineTotal;
                    }
                }

                subtotal += hwTotal;
                result["panels"] = panels;
                result["hardware"] = hwList;
                result["subtotal"] = Math.Round(subtotal, 2);
                result["count"] = panels.Count;
            }

            JsonOK(ctx, result);
        }

        // --- Register ---
        private void HandleRegister(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
            string user = data.ContainsKey("username") ? data["username"] : "";
            string pass = data.ContainsKey("password") ? data["password"] : "";

            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT COUNT(*) FROM [Users] WHERE Username = ?", conn);
                cmd.Parameters.AddWithValue("?", user);
                int exists = (int)cmd.ExecuteScalar();
                if (exists > 0)
                {
                    JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                    return;
                }

                cmd = new OleDbCommand("INSERT INTO [Users] (Username, [Password]) VALUES (?,?)", conn);
                cmd.Parameters.AddWithValue("?", user);
                cmd.Parameters.AddWithValue("?", pass);
                cmd.ExecuteNonQuery();

                cmd = new OleDbCommand("SELECT @@IDENTITY", conn);
                int uid = Convert.ToInt32(cmd.ExecuteScalar());
                JsonOK(ctx, new { success = true, userID = uid, username = user });
            }
        }

        // --- Send OTP ---
        private void HandleSendOTP(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
            string mobile = data.ContainsKey("mobile") ? data["mobile"] : "";
            string email = data.ContainsKey("email") ? data["email"] : "";

            if (string.IsNullOrEmpty(mobile) || mobile.Length < 10 || string.IsNullOrEmpty(email))
            {
                JsonOK(ctx, new { success = false, message = "Invalid input" });
                return;
            }

            using (var conn = GetConn())
            {
                conn.Open();

                var duplicateMobile = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM [Users] WHERE Mobile = ?", mobile));
                if (duplicateMobile > 0)
                {
                    JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                    return;
                }

                var duplicateEmail = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM [Users] WHERE Email = ?", email));
                if (duplicateEmail > 0)
                {
                    JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                    return;
                }

                using (var up = new OleDbCommand("UPDATE [OTPVerifications] SET IsUsed = 1 WHERE Mobile = ? AND IsUsed = 0", conn))
                {
                    up.Parameters.AddWithValue("?", mobile);
                    up.ExecuteNonQuery();
                }

                Random rnd = new Random();
                string otp = rnd.Next(100000, 999999).ToString();
                using (var ins = new OleDbCommand("INSERT INTO [OTPVerifications] (Mobile, OTP, CreatedAt) VALUES (?,?,NOW())", conn))
                {
                    ins.Parameters.AddWithValue("?", mobile);
                    ins.Parameters.AddWithValue("?", otp);
                    ins.ExecuteNonQuery();
                }

                try
                {
                    SendMail(
                        email,
                        "Elenza - OTP for Registration",
                        "Your OTP for registration is: " + otp + "\n\nThis OTP is valid for 10 minutes.\n\nRegards,\nElenza",
                        null
                    );
                }
                catch
                {
                    JsonOK(ctx, new { success = false, message = "Failed to send OTP" });
                    return;
                }

                JsonOK(ctx, new { success = true, message = "OTP sent to your email" });
            }
        }

        // --- Verify OTP ---
        private void HandleVerifyOTP(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
            string mobile = data.ContainsKey("mobile") ? data["mobile"] : "";
            string otp = data.ContainsKey("otp") ? data["otp"] : "";
            string username = data.ContainsKey("username") ? data["username"] : "";
            string password = data.ContainsKey("password") ? data["password"] : "";
            string businessName = data.ContainsKey("businessName") ? data["businessName"] : "";
            string address = data.ContainsKey("address") ? data["address"] : "";
            string taxType = data.ContainsKey("taxType") ? data["taxType"] : "";
            string taxNumber = data.ContainsKey("taxNumber") ? data["taxNumber"] : "";
            string email = data.ContainsKey("email") ? data["email"] : "";

            if (string.IsNullOrEmpty(mobile) || string.IsNullOrEmpty(otp))
            {
                JsonOK(ctx, new { success = false, message = "Invalid input" });
                return;
            }

            using (var conn = GetConn())
            {
                conn.Open();

                int otpId = 0;
                using (var cmd = new OleDbCommand("SELECT TOP 1 OTPID FROM [OTPVerifications] WHERE Mobile = ? AND OTP = ? AND IsUsed = 0 ORDER BY OTPID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("?", mobile);
                    cmd.Parameters.AddWithValue("?", otp);
                    object r = cmd.ExecuteScalar();
                    if (r == null)
                    {
                        JsonOK(ctx, new { success = false, message = "Verification failed" });
                        return;
                    }
                    otpId = Convert.ToInt32(r);
                }

                if (string.IsNullOrEmpty(password))
                {
                    JsonOK(ctx, new { success = false, message = "Verification failed" });
                    return;
                }

                string finalUsername = string.IsNullOrEmpty(username) ? mobile : username;

                var duplicateUsername = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM [Users] WHERE Username = ?", finalUsername));
                if (duplicateUsername > 0)
                {
                    JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                    return;
                }

                var duplicateMobile = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM [Users] WHERE Mobile = ?", mobile));
                if (duplicateMobile > 0)
                {
                    JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                    return;
                }

                if (!string.IsNullOrEmpty(email))
                {
                    var duplicateEmail = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM [Users] WHERE Email = ?", email));
                    if (duplicateEmail > 0)
                    {
                        JsonOK(ctx, new { success = false, message = NeutralRegistrationConflictMessage });
                        return;
                    }
                }

                using (var ins = new OleDbCommand("INSERT INTO [Users] (Username, [Password], Role, Mobile, Email, BusinessName, Address, TaxType, TaxNumber) VALUES (?,?,'customer',?,?,?,?,?,?)", conn))
                {
                    ins.Parameters.AddWithValue("?", finalUsername);
                    ins.Parameters.AddWithValue("?", password);
                    ins.Parameters.AddWithValue("?", mobile);
                    ins.Parameters.AddWithValue("?", email ?? "");
                    ins.Parameters.AddWithValue("?", businessName ?? "");
                    ins.Parameters.AddWithValue("?", address ?? "");
                    ins.Parameters.AddWithValue("?", taxType ?? "GST");
                    ins.Parameters.AddWithValue("?", taxNumber ?? "");
                    ins.ExecuteNonQuery();
                }

                int uid;
                using (var cmd = new OleDbCommand("SELECT @@IDENTITY", conn))
                {
                    uid = Convert.ToInt32(cmd.ExecuteScalar());
                }

                using (var up = new OleDbCommand("UPDATE [OTPVerifications] SET IsUsed = 1, VerifiedAt = NOW() WHERE OTPID = ?", conn))
                {
                    up.Parameters.AddWithValue("?", otpId);
                    up.ExecuteNonQuery();
                }

                if (!string.IsNullOrEmpty(email))
                {
                    try
                    {
                        var lines = new List<string>();
                        lines.Add("Welcome to Elenza.");
                        lines.Add("");
                        lines.Add("Your registration has been completed successfully.");
                        lines.Add("");
                        lines.Add("Account details");
                        lines.Add("Username: " + finalUsername);
                        lines.Add("Business Name: " + (string.IsNullOrEmpty(businessName) ? "-" : businessName));
                        lines.Add("Mobile: " + mobile);
                        lines.Add("Email: " + email);
                        lines.Add("Tax Type: " + (string.IsNullOrEmpty(taxType) ? "-" : taxType));
                        lines.Add("Tax Number: " + (string.IsNullOrEmpty(taxNumber) ? "-" : taxNumber));
                        lines.Add("");
                        lines.Add("What happens next");
                        lines.Add("- Sign in using your registered username and password.");
                        lines.Add("- Complete your profile details if any fields are pending.");
                        lines.Add("- Browse cabinets, configure sizes and finishes, and proceed to checkout.");
                        lines.Add("");
                        lines.Add("Consent acknowledgement");
                        lines.Add("During registration, you confirmed consent for the collection and use of your business, contact, address, and tax information for account creation, quotation support, order processing, service communication, and delivery coordination.");
                        lines.Add("");
                        lines.Add("If any registered detail needs correction, please contact the Elenza team before placing your order.");
                        lines.Add("");
                        lines.Add("Regards,");
                        lines.Add("Elenza");

                        string safeBusinessName = HttpUtility.HtmlEncode(string.IsNullOrEmpty(businessName) ? "Customer" : businessName);
                        string safeUsername = HttpUtility.HtmlEncode(finalUsername);
                        string safeMobile = HttpUtility.HtmlEncode(mobile);
                        string safeEmail = HttpUtility.HtmlEncode(email);
                        string safeTaxType = HttpUtility.HtmlEncode(string.IsNullOrEmpty(taxType) ? "-" : taxType);
                        string safeTaxNumber = HttpUtility.HtmlEncode(string.IsNullOrEmpty(taxNumber) ? "-" : taxNumber);

                        string html = ""
                            + "<html><body style=\"margin:0;padding:0;background:#edf4ff;font-family:Segoe UI,Arial,sans-serif;color:#10213e;\">"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:linear-gradient(180deg,#f7fbff 0%,#edf4ff 100%);padding:34px 0;\">"
                            + "<tr><td align=\"center\">"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" align=\"center\" style=\"width:100%;max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d6e5ff;border-radius:22px;overflow:hidden;box-shadow:0 24px 54px rgba(25,78,180,0.12);\">"
                            + "<tr><td style=\"padding:0;background:#ffffff;\">"
                            + "<div style=\"padding:30px 34px 26px;background:radial-gradient(circle at top right,rgba(147,197,253,0.34),transparent 26%),linear-gradient(135deg,#163a7a 0%,#2156c6 52%,#3b82f6 100%);color:#ffffff;\">"
                            + "<div style=\"display:inline-block;padding:8px 14px;border:1px solid rgba(255,255,255,0.22);border-radius:999px;background:rgba(255,255,255,0.10);font-size:11px;letter-spacing:0.18em;text-transform:uppercase;font-weight:700;\">Elenza</div>"
                            + "<div style=\"margin-top:18px;font-size:34px;line-height:1.15;font-weight:700;letter-spacing:-0.02em;\">Welcome aboard</div>"
                            + "<div style=\"margin-top:10px;max-width:500px;font-size:15px;line-height:1.7;opacity:0.95;\">Your account is active and ready for cabinet configuration, quotation review, and order processing with Elenza.</div>"
                            + "<div style=\"margin-top:22px;padding:16px 18px;border-radius:16px;background:rgba(255,255,255,0.12);border:1px solid rgba(255,255,255,0.14);\">"
                            + "<div style=\"font-size:12px;letter-spacing:0.14em;text-transform:uppercase;font-weight:700;opacity:0.85;\">Account Ready</div>"
                            + "<div style=\"margin-top:6px;font-size:14px;line-height:1.7;\">You can now sign in, complete your profile, and begin selecting cabinet modules, materials, and finishes.</div>"
                            + "</div>"
                            + "</div>"
                            + "</td></tr>"
                            + "<tr><td style=\"padding:32px 34px 18px;\">"
                            + "<div style=\"font-size:20px;font-weight:700;color:#163a7a;\">Hello " + safeBusinessName + ",</div>"
                            + "<div style=\"margin-top:12px;font-size:14px;line-height:1.85;color:#4a6184;\">Thank you for registering with Elenza. We have successfully created your account and recorded your registration consent for business processing purposes. Below is a summary of the information linked to this registration.</div>"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:24px;border-collapse:separate;border-spacing:0;background:linear-gradient(180deg,#fbfdff 0%,#f4f8ff 100%);border:1px solid #dce8ff;border-radius:18px;overflow:hidden;\">"
                            + "<tr><td colspan=\"2\" style=\"padding:16px 18px;font-size:12px;font-weight:700;letter-spacing:0.16em;text-transform:uppercase;color:#5f7394;border-bottom:1px solid #dce8ff;background:#f8fbff;\">Registered Details</td></tr>"
                            + "<tr><td style=\"padding:13px 18px;font-size:12px;color:#6a7f9e;width:190px;text-transform:uppercase;letter-spacing:0.12em;\">Username</td><td style=\"padding:13px 18px;font-size:15px;font-weight:700;color:#10213e;\">" + safeUsername + "</td></tr>"
                            + "<tr><td style=\"padding:13px 18px;font-size:12px;color:#6a7f9e;border-top:1px solid #e8f0ff;text-transform:uppercase;letter-spacing:0.12em;\">Mobile</td><td style=\"padding:13px 18px;font-size:15px;font-weight:600;color:#10213e;border-top:1px solid #e8f0ff;\">" + safeMobile + "</td></tr>"
                            + "<tr><td style=\"padding:13px 18px;font-size:12px;color:#6a7f9e;border-top:1px solid #e8f0ff;text-transform:uppercase;letter-spacing:0.12em;\">Email</td><td style=\"padding:13px 18px;font-size:15px;font-weight:600;color:#10213e;border-top:1px solid #e8f0ff;\">" + safeEmail + "</td></tr>"
                            + "<tr><td style=\"padding:13px 18px;font-size:12px;color:#6a7f9e;border-top:1px solid #e8f0ff;text-transform:uppercase;letter-spacing:0.12em;\">Tax Type</td><td style=\"padding:13px 18px;font-size:15px;font-weight:600;color:#10213e;border-top:1px solid #e8f0ff;\">" + safeTaxType + "</td></tr>"
                            + "<tr><td style=\"padding:13px 18px;font-size:12px;color:#6a7f9e;border-top:1px solid #e8f0ff;text-transform:uppercase;letter-spacing:0.12em;\">Tax Number</td><td style=\"padding:13px 18px;font-size:15px;font-weight:600;color:#10213e;border-top:1px solid #e8f0ff;\">" + safeTaxNumber + "</td></tr>"
                            + "</table>"
                            + "<div style=\"margin-top:22px;padding:20px 20px 18px;border-radius:18px;background:linear-gradient(180deg,#f8fbff 0%,#eef5ff 100%);border:1px solid #dbeafe;box-shadow:0 8px 20px rgba(37,99,235,0.06);\">"
                            + "<div style=\"font-size:13px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;color:#1d4ed8;\">Explore Our Product Range</div>"
                            + "<div style=\"margin-top:10px;font-size:13px;line-height:1.85;color:#536b8f;\">On the Elenza website, you can explore and order a complete modular interior selection tailored for your project needs.</div>"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:14px;table-layout:fixed;\">"
                            + "<tr>"
                            + "<td style=\"width:50%;padding:0 8px 10px 0;vertical-align:stretch;\" valign=\"top\"><a href=\"http://elenzapms-001-site1.jtempurl.com/pms/ecb/public/index.html#cabinets\" style=\"display:block;min-height:112px;padding:14px 14px 12px;border-radius:14px;background:#86e3ce;color:#123b39;text-decoration:none;box-sizing:border-box;\"><div style=\"font-size:13px;font-weight:800;color:#123b39;\">Cabinets</div><div style=\"margin-top:5px;font-size:12px;line-height:1.7;color:#255a56;\">Custom cabinet modules with configurable dimensions, materials, and finishes.</div></a></td>"
                            + "<td style=\"width:50%;padding:0 0 10px 8px;vertical-align:stretch;\" valign=\"top\"><a href=\"http://elenzapms-001-site1.jtempurl.com/pms/ecb/public/index.html#hardware\" style=\"display:block;min-height:112px;padding:14px 14px 12px;border-radius:14px;background:#d0e6a5;color:#394b1f;text-decoration:none;box-sizing:border-box;\"><div style=\"font-size:13px;font-weight:800;color:#394b1f;\">Hardware</div><div style=\"margin-top:5px;font-size:12px;line-height:1.7;color:#556d2b;\">Essential fittings and accessories required for cabinet completion.</div></a></td>"
                            + "</tr>"
                            + "<tr>"
                            + "<td style=\"width:50%;padding:0 8px 0 0;vertical-align:stretch;\" valign=\"top\"><a href=\"http://elenzapms-001-site1.jtempurl.com/pms/ecb/public/index.html#boards\" style=\"display:block;min-height:112px;padding:14px 14px 12px;border-radius:14px;background:#ffdd94;color:#5b4312;text-decoration:none;box-sizing:border-box;\"><div style=\"font-size:13px;font-weight:800;color:#5b4312;\">Boards</div><div style=\"margin-top:5px;font-size:12px;line-height:1.7;color:#7a5a1f;\">Board options for different build requirements and structural preferences.</div></a></td>"
                            + "<td style=\"width:50%;padding:0 0 0 8px;vertical-align:stretch;\" valign=\"top\"><a href=\"http://elenzapms-001-site1.jtempurl.com/pms/ecb/public/index.html#laminates\" style=\"display:block;min-height:112px;padding:14px 14px 12px;border-radius:14px;background:#fa897b;color:#5a1f22;text-decoration:none;box-sizing:border-box;\"><div style=\"font-size:13px;font-weight:800;color:#5a1f22;\">Laminate / Acrylic</div><div style=\"margin-top:5px;font-size:12px;line-height:1.7;color:#7a3135;\">Surface finish options to match visual style, durability, and project theme.</div></a></td>"
                            + "</tr>"
                            + "</table>"
                            + "</div>"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:22px;\">"
                            + "<tr>"
                            + "<td valign=\"top\">"
                            + "<div style=\"height:100%;padding:20px 20px 18px;border-radius:18px;background:linear-gradient(180deg,#f5f9ff 0%,#ebf3ff 100%);border:1px solid #d8e7ff;\">"
                            + "<div style=\"text-align:center;font-size:24px;line-height:1.25;font-weight:800;letter-spacing:0.04em;text-transform:uppercase;color:#1b1b1b;\">4 Steps Project<br>Process</div>"
                            + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:18px;\">"
                            + "<tr><td style=\"padding:0 0 12px 0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"width:86px;vertical-align:top;padding-right:12px;\"><div style=\"width:78px;height:78px;border-radius:999px;background:#dbeafe;border:1.5px solid #9ec5fe;text-align:center;\"><div style=\"margin-top:18px;font-size:28px;line-height:1;font-weight:800;color:#2156c6;\">1</div><div style=\"margin-top:6px;font-size:11px;letter-spacing:0.08em;text-transform:uppercase;color:#4b6ea9;font-weight:700;\">Login</div></div></td><td style=\"vertical-align:top;\"><div style=\"padding:16px 18px;border:1.5px solid #cfe0ff;border-radius:14px;background:#ffffff;box-shadow:0 6px 16px rgba(37,99,235,0.08);\"><div style=\"font-size:13px;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;color:#163a7a;\">01. Sign In</div><div style=\"margin-top:7px;font-size:12px;line-height:1.7;color:#4b5563;\">Use your registered username and password to access your Elenza account securely.</div></div></td></tr></table></td></tr>"
                            + "<tr><td style=\"padding:0 0 10px 37px;\"><div style=\"width:2px;height:26px;background:#9ec5fe;\"></div></td></tr>"
                            + "<tr><td style=\"padding:0 0 12px 0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"vertical-align:top;\"><div style=\"padding:16px 18px;border:1.5px solid #cfe0ff;border-radius:14px;background:#ffffff;box-shadow:0 6px 16px rgba(37,99,235,0.08);\"><div style=\"font-size:13px;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;color:#163a7a;\">02. Complete Profile</div><div style=\"margin-top:7px;font-size:12px;line-height:1.7;color:#4b5563;\">Fill in pending business, contact, and delivery information before checkout.</div></div></td><td style=\"width:86px;vertical-align:top;padding-left:12px;\"><div style=\"width:78px;height:78px;border-radius:999px;background:#dbeafe;border:1.5px solid #9ec5fe;text-align:center;\"><div style=\"margin-top:18px;font-size:28px;line-height:1;font-weight:800;color:#2156c6;\">2</div><div style=\"margin-top:6px;font-size:11px;letter-spacing:0.08em;text-transform:uppercase;color:#4b6ea9;font-weight:700;\">Profile</div></div></td></tr></table></td></tr>"
                            + "<tr><td style=\"padding:0 0 10px 37px;\"><div style=\"width:2px;height:26px;background:#9ec5fe;\"></div></td></tr>"
                            + "<tr><td style=\"padding:0 0 12px 0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"width:86px;vertical-align:top;padding-right:12px;\"><div style=\"width:78px;height:78px;border-radius:999px;background:#dbeafe;border:1.5px solid #9ec5fe;text-align:center;\"><div style=\"margin-top:18px;font-size:28px;line-height:1;font-weight:800;color:#2156c6;\">3</div><div style=\"margin-top:6px;font-size:11px;letter-spacing:0.08em;text-transform:uppercase;color:#4b6ea9;font-weight:700;\">Design</div></div></td><td style=\"vertical-align:top;\"><div style=\"padding:16px 18px;border:1.5px solid #cfe0ff;border-radius:14px;background:#ffffff;box-shadow:0 6px 16px rgba(37,99,235,0.08);\"><div style=\"font-size:13px;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;color:#163a7a;\">03. Configure Cabinets</div><div style=\"margin-top:7px;font-size:12px;line-height:1.7;color:#4b5563;\">Choose cabinet sizes, materials, colours, and required options for your project.</div></div></td></tr></table></td></tr>"
                            + "<tr><td style=\"padding:0 0 10px 37px;\"><div style=\"width:2px;height:26px;background:#9ec5fe;\"></div></td></tr>"
                            + "<tr><td style=\"padding:0;\"><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tr><td style=\"vertical-align:top;\"><div style=\"padding:16px 18px;border:1.5px solid #cfe0ff;border-radius:14px;background:#ffffff;box-shadow:0 6px 16px rgba(37,99,235,0.08);\"><div style=\"font-size:13px;font-weight:800;letter-spacing:0.08em;text-transform:uppercase;color:#163a7a;\">04. Review & Order</div><div style=\"margin-top:7px;font-size:12px;line-height:1.7;color:#4b5563;\">Check pricing, confirm selections, and proceed confidently with your order.</div></div></td><td style=\"width:86px;vertical-align:top;padding-left:12px;\"><div style=\"width:78px;height:78px;border-radius:999px;background:#dbeafe;border:1.5px solid #9ec5fe;text-align:center;\"><div style=\"margin-top:18px;font-size:28px;line-height:1;font-weight:800;color:#2156c6;\">4</div><div style=\"margin-top:6px;font-size:11px;letter-spacing:0.08em;text-transform:uppercase;color:#4b6ea9;font-weight:700;\">Order</div></div></td></tr></table></td></tr>"
                            + "</table>"
                            + "</div>"
                            + "</td>"
                            + "</tr>"
                            + "<tr>"
                            + "<td valign=\"top\" style=\"padding-top:16px;\">"
                            + "<div style=\"height:100%;padding:20px 20px 18px;border-radius:18px;background:linear-gradient(180deg,#fbfdff 0%,#f6faff 100%);border:1px solid #dde9ff;\">"
                            + "<div style=\"font-size:13px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;color:#1d4ed8;\">Consent Acknowledgement</div>"
                            + "<div style=\"margin-top:12px;font-size:13px;line-height:1.85;color:#536b8f;\">During registration, you confirmed consent for the collection and use of your business, contact, address, and tax information for account creation, quotation support, order processing, service communication, and delivery coordination.</div>"
                            + "<div style=\"margin-top:10px;font-size:13px;line-height:1.85;color:#536b8f;\">If any registered information needs correction, please contact the Elenza team before placing your order.</div>"
                            + "</div>"
                            + "</td>"
                            + "</tr>"
                            + "</table>"
                            + "<div style=\"margin-top:24px;padding:18px 20px;border-radius:16px;background:#0f172a;color:#dbeafe;\">"
                            + "<div style=\"font-size:12px;font-weight:700;letter-spacing:0.14em;text-transform:uppercase;opacity:0.82;\">Support Note</div>"
                            + "<div style=\"margin-top:8px;font-size:13px;line-height:1.8;\">Keep this email for reference. If you need help with login, profile details, or quotation flow, our team can assist you before order placement.</div>"
                            + "</div>"
                            + "<div style=\"margin-top:26px;padding-top:18px;border-top:1px solid #e5efff;font-size:13px;line-height:1.8;color:#5f7394;\">Regards,<br><strong style=\"font-size:15px;color:#163a7a;\">Elenza</strong></div>"
                            + "</td></tr>"
                            + "</table>"
                            + "</td></tr></table>"
                            + "</body></html>";

                        SendMail(
                            email,
                            "Elenza - Registration Successful",
                            string.Join("\n", lines.ToArray()),
                            html,
                            NewUserNotificationBcc,
                            true
                        );
                    }
                    catch
                    {
                    }
                }

                JsonOK(ctx, new { success = true, userID = uid, username = finalUsername, role = "customer" });
            }
        }

        // --- Login ---
        private void HandleLogin(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(json);
            string user = data.ContainsKey("username") ? data["username"] : "";
            string pass = data.ContainsKey("password") ? data["password"] : "";

            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Users] WHERE Username = ? AND [Password] = ?", conn);
                cmd.Parameters.AddWithValue("?", user);
                cmd.Parameters.AddWithValue("?", pass);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        int uid = Convert.ToInt32(r["UserID"]);
                        string role = "customer";
                        try
                        {
                            var roleOrdinal = r.GetOrdinal("Role");
                            if (roleOrdinal >= 0 && r["Role"] != DBNull.Value)
                                role = r["Role"].ToString();
                        }
                        catch
                        {
                        }
                        string token = Guid.NewGuid().ToString("N").Substring(0, 16);
                        JsonOK(ctx, new { success = true, token = token, userID = uid, username = user, role = role });
                    }
                    else
                    {
                        JsonOK(ctx, new { success = false, message = "Invalid username or password" });
                    }
                }
            }
        }

        // --- Cart Save ---
        private void HandleCartSave(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string token = data.ContainsKey("token") ? Convert.ToString(data["token"]) : "";
            int userId = ParseInt(data.ContainsKey("userID") ? Convert.ToString(data["userID"]) : "0");
            var items = data.ContainsKey("items") ? (System.Collections.ArrayList)data["items"] : new System.Collections.ArrayList();

            using (var conn = GetConn())
            {
                conn.Open();

                // Clear existing cart for user
                var cmd = new OleDbCommand("DELETE FROM [CartItems] WHERE UserID = ?", conn);
                cmd.Parameters.AddWithValue("?", userId);
                cmd.ExecuteNonQuery();

                // Insert items
                string sessionId = Guid.NewGuid().ToString("N");
                foreach (Dictionary<string, object> item in items)
                {
                    cmd = new OleDbCommand(@"INSERT INTO [CartItems] (UserID, SessionID, CabinetID, Width, Depth, Height, MaterialID, ThicknessID, ColourID, Quantity) 
                        VALUES (?,?,?,?,?,?,?,?,?,?)", conn);
                    cmd.Parameters.AddWithValue("?", userId);
                    cmd.Parameters.AddWithValue("?", sessionId);
                    cmd.Parameters.AddWithValue("?", ParseInt(Convert.ToString(item["cabinetID"])));
                    cmd.Parameters.AddWithValue("?", ParseDouble(Convert.ToString(item["width"])));
                    cmd.Parameters.AddWithValue("?", ParseDouble(Convert.ToString(item["depth"])));
                    cmd.Parameters.AddWithValue("?", ParseDouble(Convert.ToString(item["height"])));
                    cmd.Parameters.AddWithValue("?", ParseInt(Convert.ToString(item["materialID"])));
                    cmd.Parameters.AddWithValue("?", ParseInt(Convert.ToString(item["thicknessID"])));
                    cmd.Parameters.AddWithValue("?", ParseInt(Convert.ToString(item["colourID"])));
                    cmd.Parameters.AddWithValue("?", ParseInt(Convert.ToString(item["quantity"])));
                    cmd.ExecuteNonQuery();
                }
            }
            JsonOK(ctx, new { success = true });
        }

        // --- Cart Load ---
        private void HandleCartLoad(HttpContext ctx)
        {
            int userId = ParseInt(ctx.Request["userID"]);
            var list = new List<Dictionary<string, object>>();

            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT ci.*,c.ModelName,c.ModelCode FROM [CartItems] ci LEFT JOIN [Cabinets] c ON ci.CabinetID=c.CabinetID WHERE ci.UserID = ? ORDER BY ci.CartItemID", conn);
                cmd.Parameters.AddWithValue("?", userId);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "CartItemID", "CabinetID", "Width", "Depth", "Height", "MaterialID", "ThicknessID", "ColourID", "Quantity", "ModelName", "ModelCode"));
            }
            JsonOK(ctx, list);
        }

        // --- Cart Price ---
        private void HandleCartPrice(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var itemsRaw = data.ContainsKey("items") ? (System.Collections.ArrayList)data["items"] : new System.Collections.ArrayList();

            double subtotal = 0;
            var pricedItems = new List<Dictionary<string, object>>();

            using (var conn = GetConn())
            {
                conn.Open();
                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    string type = raw.ContainsKey("type") ? Convert.ToString(raw["type"]) : "cabinet";
                    int qty = ParseInt(Convert.ToString(raw["quantity"]));

                    if (type == "cabinet")
                    {
                        var item = new CartItem
                        {
                            CabinetID = ParseInt(Convert.ToString(raw["cabinetID"])),
                            Width = ParseDouble(Convert.ToString(raw["width"])),
                            Depth = ParseDouble(Convert.ToString(raw["depth"])),
                            Height = ParseDouble(Convert.ToString(raw["height"])),
                            MaterialID = ParseInt(Convert.ToString(raw["materialID"])),
                            ThicknessID = ParseInt(Convert.ToString(raw["thicknessID"])),
                            ColourID = ParseInt(Convert.ToString(raw["colourID"])),
                            Quantity = qty
                        };
                        var priceResult = ComputeItemPrice(conn, item);
                        pricedItems.Add(priceResult);
                        subtotal += Convert.ToDouble(priceResult["lineTotal"]);
                    }
                    else
                    {
                        double unitPrice = ParseDouble(Convert.ToString(raw["unitPrice"]));
                        double lineTotal = Math.Round(unitPrice * qty, 2);
                        subtotal += lineTotal;
                        var pi = new Dictionary<string, object>();
                        pi["type"] = type;
                        pi["productID"] = raw.ContainsKey("productID") ? ((raw["productID"] ?? "").ToString()) : "";
                        pi["name"] = raw.ContainsKey("name") ? ((raw["name"] ?? "").ToString()) : "";
                        pi["quantity"] = qty;
                        pi["unitPrice"] = unitPrice;
                        pi["lineTotal"] = lineTotal;
                        pricedItems.Add(pi);
                    }
                }
            }

            double gstRate = 18;
            double gstAmount = Math.Round(subtotal * gstRate / 100, 2);
            double grandTotal = Math.Round(subtotal + gstAmount, 2);
            if (grandTotal < 0) grandTotal = 0;

            var result = new Dictionary<string, object>();
            result["items"] = pricedItems;
            result["subtotal"] = Math.Round(subtotal, 2);
            result["gstRate"] = gstRate;
            result["gstAmount"] = gstAmount;
            result["offers"] = new List<Dictionary<string, object>>();
            result["totalDiscount"] = 0;
            result["grandTotal"] = grandTotal;

            JsonOK(ctx, result);
        }

        private Dictionary<string, object> ComputeItemPrice(OleDbConnection conn, CartItem item)
        {
            double subtotal = 0;
            var panelList = new List<Dictionary<string, object>>();

            var cmd = new OleDbCommand(@"SELECT pd.*, bt.ThicknessValue FROM [PanelDefinitions] pd 
                LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID = bt.ThicknessID
                WHERE pd.CabinetID = ? ORDER BY pd.SortOrder", conn);
            cmd.Parameters.AddWithValue("?", item.CabinetID);

            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    int panelDefId = Convert.ToInt32(rdr["PanelDefID"]);
                    string panelName = rdr["PanelName"].ToString();
                    double thickness = rdr["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(rdr["ThicknessValue"]) : 18;
                    string faceDim1 = (rdr["FaceDim1"] ?? "Width").ToString();
                    string faceDim2 = (rdr["FaceDim2"] ?? "Height").ToString();

                    var fCmd = new OleDbCommand("SELECT * FROM [PanelFormulas] WHERE PanelDefID = ?", conn);
                    fCmd.Parameters.AddWithValue("?", panelDefId);
                    double pW = 0, pD = 0, pH = 0;
                    using (var fRdr = fCmd.ExecuteReader())
                    {
                        while (fRdr.Read())
                        {
                            string dimType = fRdr["DimensionType"].ToString();
                            string expr = fRdr["Expression"].ToString();
                            double val = FormulaEngine.Evaluate(expr, item.Width, item.Depth, item.Height, thickness);
                            if (dimType == "Width") pW = val;
                            else if (dimType == "Depth") pD = val;
                            else if (dimType == "Height") pH = val;
                        }
                    }

                    double dim1 = faceDim1 == "Width" ? pW : faceDim1 == "Depth" ? pD : pH;
                    double dim2 = faceDim2 == "Width" ? pW : faceDim2 == "Depth" ? pD : pH;
                    double sft = FormulaEngine.ComputeSFT(dim1, dim2);
                    double pricePerSFT = GetPricePerSFT(conn, item.MaterialID, rdr["DefaultThicknessID"] != DBNull.Value ? Convert.ToInt32(rdr["DefaultThicknessID"]) : item.ThicknessID, item.ColourID);
                    double panelTotal = Math.Round(sft * pricePerSFT, 2);

                    panelList.Add(new Dictionary<string, object>
                    {
                        { "name", panelName }, { "width", pW }, { "depth", pD }, { "height", pH },
                        { "sft", sft }, { "pricePerSFT", pricePerSFT }, { "total", panelTotal }
                    });
                    subtotal += panelTotal;
                }
            }

            // Hardware cost rolled into unit price (no detail lines)
            double hwTotal = 0;
            var hwCmd2 = new OleDbCommand("SELECT SUM(h.UnitPrice * ch.Quantity) FROM [HardwareItems] h INNER JOIN [CabinetHardwareMap] ch ON h.HardwareID=ch.HardwareID WHERE ch.CabinetID=? AND ch.Quantity>0", conn);
            hwCmd2.Parameters.AddWithValue("?", item.CabinetID);
            object hwVal = hwCmd2.ExecuteScalar();
            if (hwVal != null) hwTotal = Convert.ToDouble(hwVal);

            subtotal += hwTotal;
            subtotal = Math.Round(subtotal, 2);
            double lineTotal = subtotal * item.Quantity;
            item.UnitPrice = subtotal;
            string materialName = "";
            string colourName = "";
            double thicknessValue = 0;

            using (var mc = new OleDbCommand("SELECT Name FROM [Materials] WHERE MaterialID = ?", conn))
            {
                mc.Parameters.AddWithValue("?", item.MaterialID);
                var mv = mc.ExecuteScalar();
                if (mv != null && mv != DBNull.Value) materialName = mv.ToString();
            }

            using (var cc = new OleDbCommand("SELECT ColourName FROM [Colours] WHERE ColourID = ?", conn))
            {
                cc.Parameters.AddWithValue("?", item.ColourID);
                var cv = cc.ExecuteScalar();
                if (cv != null && cv != DBNull.Value) colourName = cv.ToString();
            }

            using (var tc = new OleDbCommand("SELECT ThicknessValue FROM [BoardThickness] WHERE ThicknessID = ?", conn))
            {
                tc.Parameters.AddWithValue("?", item.ThicknessID);
                var tv = tc.ExecuteScalar();
                if (tv != null && tv != DBNull.Value) thicknessValue = Convert.ToDouble(tv);
            }

            return new Dictionary<string, object>
            {
                { "cabinetID", item.CabinetID },
                { "width", item.Width },
                { "depth", item.Depth },
                { "height", item.Height },
                { "materialID", item.MaterialID },
                { "materialName", materialName },
                { "thicknessID", item.ThicknessID },
                { "thicknessValue", thicknessValue },
                { "colourID", item.ColourID },
                { "colourName", colourName },
                { "quantity", item.Quantity },
                { "panels", panelList },
                { "unitPrice", item.UnitPrice },
                { "lineTotal", Math.Round(lineTotal, 2) }
            };
        }

        // --- Place Order ---
        private void HandlePlaceOrder(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int userId = ParseInt(data.ContainsKey("userID") ? Convert.ToString(data["userID"]) : "0");
            var itemsRaw = data.ContainsKey("items") ? (System.Collections.ArrayList)data["items"] : new System.Collections.ArrayList();
            var payment = data.ContainsKey("payment") ? (Dictionary<string, object>)data["payment"] : new Dictionary<string, object>();

            double subtotal = 0, grandTotal = 0;
            var orderItems = new List<Dictionary<string, object>>();
            string orderNo = "CBL-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);
            string transId = "TXN-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string customerEmail = "";
            string customerName = "";
            string customerMobile = "";
            string customerAddress = "";
            string customerTaxType = "";
            string customerTaxNumber = "";
            string customerUsername = "";
            int savedOrderId = 0;
            string payMethod = "Credit Card";
            string payStatus = "Success";
            string orderStatus = "Paid";

            using (var conn = GetConn())
            {
                conn.Open();

                using (var userCmd = new OleDbCommand("SELECT Username, BusinessName, Mobile, Email, Address, TaxType, TaxNumber FROM [Users] WHERE UserID = ?", conn))
                {
                    userCmd.Parameters.AddWithValue("?", userId);
                    using (var userRdr = userCmd.ExecuteReader())
                    {
                        if (userRdr.Read())
                        {
                            customerUsername = userRdr["Username"] != DBNull.Value ? userRdr["Username"].ToString() : "";
                            customerName = userRdr["BusinessName"] != DBNull.Value ? userRdr["BusinessName"].ToString() : "";
                            customerMobile = userRdr["Mobile"] != DBNull.Value ? userRdr["Mobile"].ToString() : "";
                            customerEmail = userRdr["Email"] != DBNull.Value ? userRdr["Email"].ToString() : "";
                            customerAddress = userRdr["Address"] != DBNull.Value ? userRdr["Address"].ToString() : "";
                            customerTaxType = userRdr["TaxType"] != DBNull.Value ? userRdr["TaxType"].ToString() : "";
                            customerTaxNumber = userRdr["TaxNumber"] != DBNull.Value ? userRdr["TaxNumber"].ToString() : "";
                        }
                    }
                }

                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    string type = raw.ContainsKey("type") ? ((raw["type"] ?? "cabinet").ToString()) : "cabinet";
                    int qty = ParseInt(Convert.ToString(raw["quantity"]));

                    if (type == "cabinet")
                    {
                        var item = new CartItem
                        {
                            CabinetID = ParseInt(Convert.ToString(raw["cabinetID"])),
                            Width = ParseDouble(Convert.ToString(raw["width"])),
                            Depth = ParseDouble(Convert.ToString(raw["depth"])),
                            Height = ParseDouble(Convert.ToString(raw["height"])),
                            MaterialID = ParseInt(Convert.ToString(raw["materialID"])),
                            ThicknessID = ParseInt(Convert.ToString(raw["thicknessID"])),
                            ColourID = ParseInt(Convert.ToString(raw["colourID"])),
                            Quantity = qty
                        };
                        var priceResult = ComputeItemPrice(conn, item);
                        double lineTotal = Convert.ToDouble(priceResult["lineTotal"]);
                        item.UnitPrice = Convert.ToDouble(priceResult["unitPrice"]);
                        subtotal += lineTotal;
                        var oi = new Dictionary<string, object>();
                        oi["type"] = "cabinet";
                        oi["cabinetID"] = item.CabinetID;
                        oi["width"] = item.Width; oi["depth"] = item.Depth; oi["height"] = item.Height;
                        oi["materialID"] = item.MaterialID; oi["thicknessID"] = item.ThicknessID; oi["colourID"] = item.ColourID;
                        oi["quantity"] = qty; oi["unitPrice"] = item.UnitPrice; oi["lineTotal"] = lineTotal;
                        oi["modelName"] = raw.ContainsKey("modelName") ? ((raw["modelName"] ?? "Cabinet").ToString()) : (raw.ContainsKey("name") ? ((raw["name"] ?? "Cabinet").ToString()) : "Cabinet");
                        orderItems.Add(oi);
                    }
                    else
                    {
                        double unitPrice = ParseDouble(Convert.ToString(raw["unitPrice"]));
                        double lineTotal = Math.Round(unitPrice * qty, 2);
                        subtotal += lineTotal;
                        var oi = new Dictionary<string, object>();
                        oi["type"] = type;
                        oi["productID"] = raw.ContainsKey("productID") ? ((raw["productID"] ?? "").ToString()) : "";
                        oi["modelName"] = raw.ContainsKey("name") ? ((raw["name"] ?? "Item").ToString()) : "Item";
                        oi["quantity"] = qty; oi["unitPrice"] = unitPrice; oi["lineTotal"] = lineTotal;
                        orderItems.Add(oi);
                    }
                }

                double gstRate = 18;
                double gstAmount = Math.Round(subtotal * gstRate / 100, 2);
                grandTotal = Math.Round(subtotal + gstAmount, 2);
                if (grandTotal < 0) grandTotal = 0;

                payMethod = payment.ContainsKey("method") ? Convert.ToString(payment["method"]) : "Credit Card";
                payStatus = payMethod == "COD" ? "Pending" : "Success";
                orderStatus = payMethod == "COD" ? "COD" : "Paid";

                // Create order
                var cmd = new OleDbCommand(@"INSERT INTO [Orders] (UserID, OrderNo, OrderDate, Status, Subtotal, DiscountTotal, GrandTotal, PaymentRef) 
                    VALUES (?,?,NOW(),?,?,?,?,?)", conn);
                cmd.Parameters.AddWithValue("?", userId);
                cmd.Parameters.AddWithValue("?", orderNo);
                cmd.Parameters.AddWithValue("?", orderStatus);
                cmd.Parameters.AddWithValue("?", subtotal);
                cmd.Parameters.AddWithValue("?", 0);
                cmd.Parameters.AddWithValue("?", grandTotal);
                cmd.Parameters.AddWithValue("?", transId);
                cmd.ExecuteNonQuery();

                cmd = new OleDbCommand("SELECT @@IDENTITY", conn);
                int orderId = Convert.ToInt32(cmd.ExecuteScalar());
                savedOrderId = orderId;

                // Insert order items
                foreach (var oi in orderItems)
                {
                    string type = oi.ContainsKey("type") ? ((oi["type"] ?? "cabinet").ToString()) : "cabinet";
                    string configJson;
                    int cabId = 0;
                    if (type == "cabinet")
                    {
                        var config = new Dictionary<string, object>
                        {
                            { "type", "cabinet" },
                            { "W", oi["width"] }, { "D", oi["depth"] }, { "H", oi["height"] },
                            { "MaterialID", oi["materialID"] }, { "ThicknessID", oi["thicknessID"] },
                            { "ColourID", oi["colourID"] }
                        };
                        configJson = new JavaScriptSerializer().Serialize(config);
                        cabId = Convert.ToInt32(oi["cabinetID"]);
                    }
                    else
                    {
                        var config = new Dictionary<string, object>
                        {
                            { "type", type },
                            { "name", oi.ContainsKey("modelName") ? ((oi["modelName"] ?? "Item").ToString()) : "Item" },
                            { "productID", oi.ContainsKey("productID") ? ((oi["productID"] ?? "").ToString()) : "" }
                        };
                        configJson = new JavaScriptSerializer().Serialize(config);
                    }

                    cmd = new OleDbCommand("INSERT INTO [OrderItems] (OrderID, CabinetID, ConfigJSON, Quantity, UnitPrice, LineTotal) VALUES (?,?,?,?,?,?)", conn);
                    cmd.Parameters.AddWithValue("?", orderId);
                    cmd.Parameters.AddWithValue("?", cabId);
                    cmd.Parameters.AddWithValue("?", configJson);
                    cmd.Parameters.AddWithValue("?", oi["quantity"]);
                    cmd.Parameters.AddWithValue("?", oi["unitPrice"]);
                    cmd.Parameters.AddWithValue("?", oi["lineTotal"]);
                    cmd.ExecuteNonQuery();
                }

                // Payment record
                cmd = new OleDbCommand("INSERT INTO [OrderPayments] (OrderID, Amount, PaymentMethod, TransactionID, Status, PaidAt) VALUES (?,?,?,?,?,NOW())", conn);
                cmd.Parameters.AddWithValue("?", orderId);
                cmd.Parameters.AddWithValue("?", grandTotal);
                cmd.Parameters.AddWithValue("?", payMethod);
                cmd.Parameters.AddWithValue("?", transId);
                cmd.Parameters.AddWithValue("?", payStatus);
                cmd.ExecuteNonQuery();

                // Clear user's cart
                cmd = new OleDbCommand("DELETE FROM [CartItems] WHERE UserID = ?", conn);
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("?", userId);
                cmd.ExecuteNonQuery();
            }

            try
            {
                var safeName = HttpUtility.HtmlEncode(string.IsNullOrEmpty(customerName) ? (string.IsNullOrEmpty(customerUsername) ? "Customer" : customerUsername) : customerName);
                var safeOrderNo = HttpUtility.HtmlEncode(orderNo);
                var safePayMethod = HttpUtility.HtmlEncode(payMethod);
                var safeAddress = HttpUtility.HtmlEncode(customerAddress ?? "");
                var safeTaxType = HttpUtility.HtmlEncode(customerTaxType ?? "");
                var safeTaxNumber = HttpUtility.HtmlEncode(customerTaxNumber ?? "");
                var safeMobile = HttpUtility.HtmlEncode(customerMobile ?? "");
                var safeEmail = HttpUtility.HtmlEncode(customerEmail ?? "");

                var itemRows = new StringBuilder();
                var adminItemRows = new StringBuilder();
                foreach (var oi in orderItems)
                {
                    var modelName = HttpUtility.HtmlEncode(oi.ContainsKey("modelName") ? Convert.ToString(oi["modelName"]) : "Item");
                    var quantity = oi.ContainsKey("quantity") ? Convert.ToString(oi["quantity"]) : "1";
                    var unitPrice = oi.ContainsKey("unitPrice") ? Convert.ToDouble(oi["unitPrice"]).ToString("N2") : "0.00";
                    var lineTotal = oi.ContainsKey("lineTotal") ? Convert.ToDouble(oi["lineTotal"]).ToString("N2") : "0.00";
                    var spec = "";
                    if ((oi.ContainsKey("type") ? Convert.ToString(oi["type"]) : "cabinet") == "cabinet")
                    {
                        spec = string.Format("{0} x {1} x {2} mm",
                            oi.ContainsKey("width") ? Convert.ToString(oi["width"]) : "0",
                            oi.ContainsKey("depth") ? Convert.ToString(oi["depth"]) : "0",
                            oi.ContainsKey("height") ? Convert.ToString(oi["height"]) : "0");
                    }
                    else
                    {
                        spec = HttpUtility.HtmlEncode(oi.ContainsKey("type") ? Convert.ToString(oi["type"]) : "item");
                    }

                    itemRows.Append("<tr>")
                        .Append("<td style=\"padding:12px 14px;border-bottom:1px solid #e6eefc;font-size:13px;color:#163a7a;font-weight:700;\">").Append(modelName).Append("</td>")
                        .Append("<td style=\"padding:12px 14px;border-bottom:1px solid #e6eefc;font-size:12px;color:#5a6f90;\">").Append(spec).Append("</td>")
                        .Append("<td style=\"padding:12px 14px;border-bottom:1px solid #e6eefc;font-size:12px;color:#1f2937;text-align:center;\">").Append(quantity).Append("</td>")
                        .Append("<td style=\"padding:12px 14px;border-bottom:1px solid #e6eefc;font-size:12px;color:#1f2937;text-align:right;\">Rs. ").Append(unitPrice).Append("</td>")
                        .Append("<td style=\"padding:12px 14px;border-bottom:1px solid #e6eefc;font-size:12px;color:#1f2937;text-align:right;\">Rs. ").Append(lineTotal).Append("</td>")
                        .Append("</tr>");

                    adminItemRows.Append(modelName).Append(" | ").Append(spec).Append(" | Qty: ").Append(quantity).Append(" | Unit: Rs. ").Append(unitPrice).Append(" | Total: Rs. ").Append(lineTotal).Append("\n");
                }

                var customerBody = new StringBuilder();
                customerBody.AppendLine("Thank you for your order with Elenza.");
                customerBody.AppendLine();
                customerBody.AppendLine("Order No: " + orderNo);
                customerBody.AppendLine("Payment Method: " + payMethod);
                customerBody.AppendLine("Payment Status: " + payStatus);
                customerBody.AppendLine("Grand Total: Rs. " + grandTotal.ToString("N2"));
                customerBody.AppendLine();
                customerBody.AppendLine("We have received your order and our team will review it shortly.");
                customerBody.AppendLine();
                customerBody.AppendLine("Regards,");
                customerBody.AppendLine("Elenza");

                var customerHtml =
                    "<html><body style=\"margin:0;padding:0;background:#eef5ff;font-family:Segoe UI,Arial,sans-serif;color:#13233f;\">"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"padding:28px 0;background:linear-gradient(180deg,#f7fbff 0%,#eef5ff 100%);\"><tr><td align=\"center\">"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;max-width:700px;margin:0 auto;background:#ffffff;border:1px solid #d7e6ff;border-radius:22px;overflow:hidden;box-shadow:0 22px 50px rgba(18,76,173,0.10);\">"
                    + "<tr><td style=\"padding:28px 30px;background:linear-gradient(135deg,#0f4cbd 0%,#3b82f6 100%);color:#ffffff;\">"
                    + "<div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;font-weight:700;opacity:0.88;\">Order Confirmed</div>"
                    + "<div style=\"margin-top:12px;font-size:30px;line-height:1.15;font-weight:800;\">Thank you for your order</div>"
                    + "<div style=\"margin-top:10px;font-size:15px;line-height:1.8;color:#dbeafe;max-width:520px;\">Your Elenza order has been received successfully. Our team will review the specifications and move it into the next stage.</div>"
                    + "</td></tr>"
                    + "<tr><td style=\"padding:28px 30px 24px;\">"
                    + "<div style=\"font-size:20px;font-weight:700;color:#163a7a;\">Hello " + safeName + ",</div>"
                    + "<div style=\"margin-top:10px;font-size:14px;line-height:1.85;color:#536b8f;\">We appreciate your order. Below is a quick summary for your records.</div>"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:22px;border-collapse:separate;border-spacing:0;background:#f9fbff;border:1px solid #dde9ff;border-radius:18px;overflow:hidden;\">"
                    + "<tr><td style=\"padding:14px 16px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;width:180px;\">Order Number</td><td style=\"padding:14px 16px;font-size:15px;font-weight:700;color:#13233f;\">" + safeOrderNo + "</td></tr>"
                    + "<tr><td style=\"padding:14px 16px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Payment</td><td style=\"padding:14px 16px;font-size:14px;color:#13233f;border-top:1px solid #e6eefc;\">" + safePayMethod + " | " + HttpUtility.HtmlEncode(payStatus) + "</td></tr>"
                    + "<tr><td style=\"padding:14px 16px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Contact</td><td style=\"padding:14px 16px;font-size:14px;color:#13233f;border-top:1px solid #e6eefc;\">" + safeMobile + " | " + safeEmail + "</td></tr>"
                    + "<tr><td style=\"padding:14px 16px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Total</td><td style=\"padding:14px 16px;font-size:16px;font-weight:800;color:#0f4cbd;border-top:1px solid #e6eefc;\">Rs. " + grandTotal.ToString("N2") + "</td></tr>"
                    + "</table>"
                    + "<div style=\"margin-top:22px;padding:18px;border-radius:18px;background:linear-gradient(180deg,#f8fbff 0%,#eef5ff 100%);border:1px solid #dbeafe;\">"
                    + "<div style=\"font-size:13px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;color:#1d4ed8;\">Order Items</div>"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"margin-top:14px;border-collapse:collapse;\">"
                    + "<tr><th style=\"padding:12px 14px;background:#eaf2ff;color:#214a93;font-size:11px;text-transform:uppercase;letter-spacing:0.12em;text-align:left;\">Item</th><th style=\"padding:12px 14px;background:#eaf2ff;color:#214a93;font-size:11px;text-transform:uppercase;letter-spacing:0.12em;text-align:left;\">Specification</th><th style=\"padding:12px 14px;background:#eaf2ff;color:#214a93;font-size:11px;text-transform:uppercase;letter-spacing:0.12em;text-align:center;\">Qty</th><th style=\"padding:12px 14px;background:#eaf2ff;color:#214a93;font-size:11px;text-transform:uppercase;letter-spacing:0.12em;text-align:right;\">Unit</th><th style=\"padding:12px 14px;background:#eaf2ff;color:#214a93;font-size:11px;text-transform:uppercase;letter-spacing:0.12em;text-align:right;\">Total</th></tr>"
                    + itemRows.ToString()
                    + "</table></div>"
                    + "<div style=\"margin-top:18px;padding:18px;border-radius:16px;background:#0f172a;color:#dbeafe;\">"
                    + "<div style=\"font-size:12px;letter-spacing:0.14em;text-transform:uppercase;font-weight:700;opacity:0.85;\">What happens next</div>"
                    + "<div style=\"margin-top:8px;font-size:13px;line-height:1.8;\">Our team will review your order, validate production details, and move it into the next stage. You can track progress from your dashboard.</div>"
                    + "</div>"
                    + "<div style=\"margin-top:20px;font-size:13px;line-height:1.8;color:#536b8f;\">Delivery Address:<br><strong style=\"color:#13233f;\">" + safeAddress + "</strong></div>"
                    + "<div style=\"margin-top:8px;font-size:13px;line-height:1.8;color:#536b8f;\">Tax Details: <strong style=\"color:#13233f;\">" + safeTaxType + " " + safeTaxNumber + "</strong></div>"
                    + "<div style=\"margin-top:24px;padding-top:18px;border-top:1px solid #e5efff;font-size:13px;line-height:1.8;color:#5f7394;\">Regards,<br><strong style=\"font-size:15px;color:#163a7a;\">Elenza</strong></div>"
                    + "</td></tr></table></td></tr></table></body></html>";

                if (!string.IsNullOrEmpty(customerEmail))
                {
                    SendMail(
                        customerEmail,
                        "Thank you for your order | " + orderNo,
                        customerBody.ToString(),
                        customerHtml,
                        null,
                        true
                    );
                }

                var adminBody = new StringBuilder();
                adminBody.AppendLine("New order received.");
                adminBody.AppendLine();
                adminBody.AppendLine("Order No: " + orderNo);
                adminBody.AppendLine("Order ID: " + savedOrderId);
                adminBody.AppendLine("Customer: " + (string.IsNullOrEmpty(customerName) ? customerUsername : customerName));
                adminBody.AppendLine("Username: " + customerUsername);
                adminBody.AppendLine("Mobile: " + customerMobile);
                adminBody.AppendLine("Email: " + customerEmail);
                adminBody.AppendLine("Address: " + customerAddress);
                adminBody.AppendLine("Tax: " + customerTaxType + " " + customerTaxNumber);
                adminBody.AppendLine("Payment: " + payMethod + " | " + payStatus + " | " + transId);
                adminBody.AppendLine("Grand Total: Rs. " + grandTotal.ToString("N2"));
                adminBody.AppendLine();
                adminBody.AppendLine("Items:");
                adminBody.Append(adminItemRows.ToString());

                var adminHtml =
                    "<html><body style=\"margin:0;padding:0;background:#f5f8ff;font-family:Segoe UI,Arial,sans-serif;color:#172b4d;\">"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"padding:24px 0;\"><tr><td align=\"center\">"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;max-width:720px;margin:0 auto;background:#ffffff;border:1px solid #dae6ff;border-radius:20px;overflow:hidden;\">"
                    + "<tr><td style=\"padding:24px 28px;background:linear-gradient(135deg,#163a7a 0%,#2563eb 100%);color:#ffffff;\">"
                    + "<div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;font-weight:700;opacity:0.88;\">New Order Alert</div>"
                    + "<div style=\"margin-top:10px;font-size:28px;font-weight:800;line-height:1.2;\">A new order has been received</div>"
                    + "<div style=\"margin-top:10px;font-size:14px;line-height:1.8;color:#dbeafe;\">Order " + safeOrderNo + " has been placed and is ready for team review.</div>"
                    + "</td></tr>"
                    + "<tr><td style=\"padding:24px 28px;\">"
                    + "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;border-spacing:0;background:#f9fbff;border:1px solid #e3edff;border-radius:16px;overflow:hidden;\">"
                    + "<tr><td style=\"padding:12px 14px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;width:170px;\">Customer</td><td style=\"padding:12px 14px;font-size:14px;font-weight:700;color:#172b4d;\">" + HttpUtility.HtmlEncode(string.IsNullOrEmpty(customerName) ? customerUsername : customerName) + "</td></tr>"
                    + "<tr><td style=\"padding:12px 14px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Contact</td><td style=\"padding:12px 14px;font-size:14px;color:#172b4d;border-top:1px solid #e6eefc;\">" + safeMobile + " | " + safeEmail + "</td></tr>"
                    + "<tr><td style=\"padding:12px 14px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Payment</td><td style=\"padding:12px 14px;font-size:14px;color:#172b4d;border-top:1px solid #e6eefc;\">" + safePayMethod + " | " + HttpUtility.HtmlEncode(payStatus) + " | " + HttpUtility.HtmlEncode(transId) + "</td></tr>"
                    + "<tr><td style=\"padding:12px 14px;font-size:12px;color:#6780a7;text-transform:uppercase;letter-spacing:0.12em;border-top:1px solid #e6eefc;\">Grand Total</td><td style=\"padding:12px 14px;font-size:15px;font-weight:800;color:#0f4cbd;border-top:1px solid #e6eefc;\">Rs. " + grandTotal.ToString("N2") + "</td></tr>"
                    + "</table>"
                    + "<div style=\"margin-top:18px;font-size:13px;font-weight:700;letter-spacing:0.12em;text-transform:uppercase;color:#214a93;\">Ordered Items</div>"
                    + "<div style=\"margin-top:10px;padding:16px;border:1px solid #e3edff;border-radius:14px;background:#fbfdff;font-size:13px;line-height:1.8;color:#425977;white-space:pre-line;\">" + HttpUtility.HtmlEncode(adminItemRows.ToString()) + "</div>"
                    + "<div style=\"margin-top:16px;font-size:13px;line-height:1.8;color:#536b8f;\">Address: <strong style=\"color:#172b4d;\">" + safeAddress + "</strong></div>"
                    + "<div style=\"margin-top:6px;font-size:13px;line-height:1.8;color:#536b8f;\">Tax: <strong style=\"color:#172b4d;\">" + safeTaxType + " " + safeTaxNumber + "</strong></div>"
                    + "</td></tr></table></td></tr></table></body></html>";

                SendMail(
                    "praveenk25286@gmail.com",
                    "New order received | " + orderNo,
                    adminBody.ToString(),
                    adminHtml,
                    null,
                    true
                );
            }
            catch
            {
            }

            JsonOK(ctx, new Dictionary<string, object>
            {
                { "success", true },
                { "orderID", orderNo },
                { "orderNo", orderNo },
                { "paymentRef", transId },
                { "grandTotal", grandTotal }
            });
        }

        // --- Order Detail ---
        private void HandleOrderDetail(HttpContext ctx)
        {
            int orderId = ParseInt(ctx.Request["orderID"]);
            var result = new Dictionary<string, object>();

            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT o.*, u.Username FROM [Orders] o LEFT JOIN [Users] u ON o.UserID = u.UserID WHERE o.OrderID = ?", conn);
                cmd.Parameters.AddWithValue("?", orderId);
                using (var rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        result["orderID"] = rdr["OrderID"];
                        result["orderNo"] = rdr["OrderNo"].ToString();
                        result["orderDate"] = Convert.ToDateTime(rdr["OrderDate"]).ToString("dd-MMM-yyyy");
                        result["status"] = rdr["Status"].ToString();
                        result["customer"] = (rdr["Username"] ?? "").ToString();
                        result["subtotal"] = Convert.ToDouble(rdr["Subtotal"]);
                        result["discount"] = Convert.ToDouble(rdr["DiscountTotal"]);
                        result["grandTotal"] = Convert.ToDouble(rdr["GrandTotal"]);
                        result["paymentRef"] = (rdr["PaymentRef"] ?? "").ToString();
                    }
                }

                var items = new List<Dictionary<string, object>>();
                cmd = new OleDbCommand("SELECT oi.*, c.ModelName, c.ModelCode FROM [OrderItems] oi LEFT JOIN [Cabinets] c ON oi.CabinetID = c.CabinetID WHERE oi.OrderID = ?", conn);
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("?", orderId);
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        var item = new Dictionary<string, object>();
                        item["modelName"] = rdr["ModelName"].ToString();
                        item["modelCode"] = (rdr["ModelCode"] ?? "").ToString();
                        item["quantity"] = rdr["Quantity"];
                        item["unitPrice"] = Convert.ToDouble(rdr["UnitPrice"]);
                        item["lineTotal"] = Convert.ToDouble(rdr["LineTotal"]);
                        try { item["config"] = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(rdr["ConfigJSON"].ToString()); }
                        catch { item["config"] = "{}"; }
                        items.Add(item);
                    }
                }
                result["items"] = items;
            }

            JsonOK(ctx, result);
        }

        // --- My Orders ---
        private void HandleMyOrders(HttpContext ctx)
        {
            int userId = ParseInt(ctx.Request["userID"]);
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Orders] WHERE UserID = ? ORDER BY OrderDate DESC", conn);
                cmd.Parameters.AddWithValue("?", userId);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "OrderID", "OrderNo", "OrderDate", "Status", "Subtotal", "DiscountTotal", "GrandTotal"));
            }
            JsonOK(ctx, list);
        }

        // --- Quotation HTML ---
        private void HandleQuotationHTML(HttpContext ctx)
        {
            int orderId = ParseInt(ctx.Request["orderID"]);
            ctx.Response.ContentType = "text/html";
            ctx.Response.Write(PdfHelper.GenerateQuotationHTML(DbPath, orderId));
        }

        // --- Invoice HTML ---
        private void HandleInvoiceHTML(HttpContext ctx)
        {
            int orderId = ParseInt(ctx.Request["orderID"]);
            ctx.Response.ContentType = "text/html";
            ctx.Response.Write(PdfHelper.GenerateInvoiceHTML(DbPath, orderId));
        }

        // --- Offers Active ---
        private void HandleOffersActive(HttpContext ctx)
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Offers] WHERE IsActive = TRUE AND (StartDate IS NULL OR StartDate <= NOW()) AND (EndDate IS NULL OR EndDate >= NOW())", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "OfferID", "OfferName", "OfferType", "DiscountType", "DiscountValue", "MinQty", "MinCartValue", "ComboIDs", "StartDate", "EndDate"));
            }
            JsonOK(ctx, list);
        }

        private void HandleHardwareList(HttpContext ctx)
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT HardwareID, HardwareName AS Name, HardwareName AS ItemName, '' AS Brand, UnitPrice, UOM AS Unit FROM [HardwareItems] ORDER BY HardwareName", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(ReadDict(rdr, "HardwareID", "Name", "ItemName", "Brand", "UnitPrice", "Unit"));
            }
            JsonOK(ctx, list);
        }

        private void HandleBoardOptions(HttpContext ctx)
        {
            var materials = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [Materials] WHERE Name IN ('MR Ply','BWP Ply','MDF','HDF') ORDER BY Name", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        materials.Add(ReadDict(rdr, "MaterialID", "Name"));
            }

            var thicknesses = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT * FROM [BoardThickness] ORDER BY ThicknessValue", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        thicknesses.Add(ReadDict(rdr, "ThicknessID", "ThicknessValue"));
            }

            var pricing = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                var cmd = new OleDbCommand("SELECT cp.MaterialID, cp.ThicknessID, cp.Total AS PricePerSFT, m.Name AS MaterialName, bt.ThicknessValue FROM ([CorePricing] cp INNER JOIN [Materials] m ON cp.MaterialID=m.MaterialID) INNER JOIN [BoardThickness] bt ON cp.ThicknessID=bt.ThicknessID WHERE m.Name IN ('MR Ply','BWP Ply','MDF','HDF') ORDER BY m.Name, bt.ThicknessValue", conn);
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        pricing.Add(ReadDict(rdr, "MaterialID", "ThicknessID", "PricePerSFT", "MaterialName", "ThicknessValue"));
            }

            var result = new Dictionary<string, object>();
            result["materials"] = materials;
            result["thicknesses"] = thicknesses;
            result["pricing"] = pricing;
            JsonOK(ctx, result);
        }

        private void HandleLaminateOptions(HttpContext ctx)
        {
            var items = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT LaminateID, Name, Brand, PricePerSFT FROM [Laminates] ORDER BY Name", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        items.Add(new Dictionary<string, object> {
                            { "id", "lam_" + r["LaminateID"].ToString() },
                            { "name", S(r["Name"]) },
                            { "brand", S(r["Brand"]) },
                            { "pricePerSFT", Convert.ToDouble(r["PricePerSFT"]) }
                        });
                var thicknesses = new List<double>();
                using (var tcmd = new OleDbCommand("SELECT ThicknessValue FROM [LaminateThicknesses] ORDER BY ThicknessValue", conn))
                using (var tr = tcmd.ExecuteReader())
                    while (tr.Read())
                        thicknesses.Add(Convert.ToDouble(tr["ThicknessValue"]));
                JsonOK(ctx, new Dictionary<string, object> { { "items", items }, { "thicknesses", thicknesses } });
            }
        }

        // --- Production Manager ---
        private void HandleProdOrders(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string search = data != null && data.ContainsKey("search") ? ((data["search"] ?? "").ToString()) : "";
            string statusFilter = data != null && data.ContainsKey("status") ? ((data["status"] ?? "").ToString()) : "";

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();

                var conditions = new List<string>();
                var parms = new List<OleDbParameter>();
                if (!string.IsNullOrEmpty(search)) { conditions.Add("o.OrderNo LIKE ?"); parms.Add(new OleDbParameter("?", "%" + search + "%")); }

                string sql = "SELECT o.OrderID, o.OrderNo, o.OrderDate, o.Subtotal, o.GrandTotal, o.Status, u.Username, op.Status AS ProdStatus";
                sql += " FROM ([Orders] o LEFT JOIN [Users] u ON o.UserID=u.UserID)";
                sql += " LEFT JOIN [OrderProduction] op ON o.OrderID=op.OrderID";
                if (conditions.Count > 0) sql += " WHERE " + string.Join(" AND ", conditions);

                if (!string.IsNullOrEmpty(statusFilter))
                {
                    if (conditions.Count == 0) sql += " WHERE op.Status=?";
                    else sql += " AND op.Status=?";
                    parms.Add(new OleDbParameter("?", statusFilter));
                }

                sql += " ORDER BY o.OrderDate DESC";

                using (var cmd = new OleDbCommand(sql, conn))
                {
                    cmd.Parameters.AddRange(parms.ToArray());
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            var d = new Dictionary<string, object>();
                            d["orderID"] = Convert.ToInt32(r["OrderID"]);
                            d["orderNo"] = (r["OrderNo"] ?? "").ToString();
                            d["orderDate"] = Convert.ToDateTime(r["OrderDate"]).ToString("yyyy-MM-dd");
                            d["username"] = r["Username"] != DBNull.Value ? r["Username"].ToString() : "Guest";
                            d["subtotal"] = Convert.ToDouble(r["Subtotal"]);
                            d["grandTotal"] = Convert.ToDouble(r["GrandTotal"]);
                            d["status"] = (r["Status"] ?? "").ToString();
                            d["prodStatus"] = r["ProdStatus"] != DBNull.Value ? r["ProdStatus"].ToString() : "";
                            list.Add(d);
                        }
                }
            }
            JsonOK(ctx, list);
        }

        private void HandleProdOrderDetail(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int orderId = data != null && data.ContainsKey("orderID") ? Convert.ToInt32(data["orderID"]) : 0;
            var result = new Dictionary<string, object>();

            using (var conn = GetConn())
            {
                conn.Open();

                using (var cmd = new OleDbCommand("SELECT o.*, u.Username FROM [Orders] o LEFT JOIN [Users] u ON o.UserID=u.UserID WHERE o.OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            result["orderID"] = orderId;
                            result["orderNo"] = (r["OrderNo"] ?? "").ToString();
                            result["orderDate"] = Convert.ToDateTime(r["OrderDate"]).ToString("yyyy-MM-dd");
                            result["username"] = r["Username"] != DBNull.Value ? r["Username"].ToString() : "Guest";
                            result["subtotal"] = Convert.ToDouble(r["Subtotal"]);
                            result["grandTotal"] = Convert.ToDouble(r["GrandTotal"]);
                            result["status"] = (r["Status"] ?? "").ToString();
                        }
                }

                using (var cmd = new OleDbCommand("SELECT TOP 1 u.BusinessName, u.Mobile, u.Email, u.Address, u.TaxType, u.TaxNumber, u.ContactPerson, u.ContactPhone, u.City, u.State, u.Pincode, u.DealerType, u.Website FROM [Orders] o LEFT JOIN [Users] u ON o.UserID=u.UserID WHERE o.OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            result["businessName"] = r["BusinessName"] != DBNull.Value ? r["BusinessName"].ToString() : "";
                            result["mobile"] = r["Mobile"] != DBNull.Value ? r["Mobile"].ToString() : "";
                            result["email"] = r["Email"] != DBNull.Value ? r["Email"].ToString() : "";
                            result["address"] = r["Address"] != DBNull.Value ? r["Address"].ToString() : "";
                            result["taxType"] = r["TaxType"] != DBNull.Value ? r["TaxType"].ToString() : "";
                            result["taxNumber"] = r["TaxNumber"] != DBNull.Value ? r["TaxNumber"].ToString() : "";
                            result["contactPerson"] = r["ContactPerson"] != DBNull.Value ? r["ContactPerson"].ToString() : "";
                            result["contactPhone"] = r["ContactPhone"] != DBNull.Value ? r["ContactPhone"].ToString() : "";
                            result["city"] = r["City"] != DBNull.Value ? r["City"].ToString() : "";
                            result["state"] = r["State"] != DBNull.Value ? r["State"].ToString() : "";
                            result["pincode"] = r["Pincode"] != DBNull.Value ? r["Pincode"].ToString() : "";
                            result["dealerType"] = r["DealerType"] != DBNull.Value ? r["DealerType"].ToString() : "";
                            result["website"] = r["Website"] != DBNull.Value ? r["Website"].ToString() : "";
                        }
                }

                using (var cmd = new OleDbCommand("SELECT TOP 1 PaymentMethod, TransactionID, Status, PaidAt, Amount FROM [OrderPayments] WHERE OrderID=? ORDER BY PaidAt DESC", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            result["paymentMethod"] = r["PaymentMethod"] != DBNull.Value ? r["PaymentMethod"].ToString() : "";
                            result["transactionID"] = r["TransactionID"] != DBNull.Value ? r["TransactionID"].ToString() : "";
                            result["paymentStatus"] = r["Status"] != DBNull.Value ? r["Status"].ToString() : "";
                            result["paidAt"] = r["PaidAt"] != DBNull.Value ? Convert.ToDateTime(r["PaidAt"]).ToString("yyyy-MM-dd HH:mm") : "";
                            result["paymentAmount"] = r["Amount"] != DBNull.Value ? Convert.ToDouble(r["Amount"]) : 0;
                        }
                }

                var items = new List<Dictionary<string, object>>();
                using (var cmd = new OleDbCommand("SELECT oi.*, c.ModelName FROM [OrderItems] oi LEFT JOIN [Cabinets] c ON oi.CabinetID=c.CabinetID WHERE oi.OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            var item = new Dictionary<string, object>();
                            item["orderItemID"] = Convert.ToInt32(r["OrderItemID"]);
                            item["cabinetID"] = r["CabinetID"] != DBNull.Value ? Convert.ToInt32(r["CabinetID"]) : 0;
                            item["modelName"] = r["ModelName"] != DBNull.Value ? r["ModelName"].ToString() : "Item";
                            item["quantity"] = Convert.ToInt32(r["Quantity"]);
                            item["unitPrice"] = Convert.ToDouble(r["UnitPrice"]);
                            item["lineTotal"] = Convert.ToDouble(r["LineTotal"]);
                            item["itemType"] = "cabinet";

                            Dictionary<string, object> cfg = null;
                            try { cfg = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(r["ConfigJSON"].ToString()); }
                            catch { cfg = new Dictionary<string, object>(); }

                            if (cfg != null)
                            {
                                item["itemType"] = cfg.ContainsKey("type") ? Convert.ToString(cfg["type"]) : "cabinet";
                                item["width"] = cfg.ContainsKey("W") ? ParseDouble(Convert.ToString(cfg["W"])) : 0;
                                item["depth"] = cfg.ContainsKey("D") ? ParseDouble(Convert.ToString(cfg["D"])) : 0;
                                item["height"] = cfg.ContainsKey("H") ? ParseDouble(Convert.ToString(cfg["H"])) : 0;
                                item["materialID"] = cfg.ContainsKey("MaterialID") ? ParseInt(Convert.ToString(cfg["MaterialID"])) : 0;
                                item["thicknessID"] = cfg.ContainsKey("ThicknessID") ? ParseInt(Convert.ToString(cfg["ThicknessID"])) : 0;
                                item["colourID"] = cfg.ContainsKey("ColourID") ? ParseInt(Convert.ToString(cfg["ColourID"])) : 0;
                                if (cfg.ContainsKey("name")) item["name"] = Convert.ToString(cfg["name"]);
                                if (cfg.ContainsKey("productID")) item["productID"] = Convert.ToString(cfg["productID"]);
                            }

                            var itemType = item["itemType"] != null ? item["itemType"].ToString() : "cabinet";
                            if (itemType == "cabinet")
                            {
                                var materialId = item.ContainsKey("materialID") ? Convert.ToInt32(item["materialID"]) : 0;
                                var colourId = item.ContainsKey("colourID") ? Convert.ToInt32(item["colourID"]) : 0;
                                var thicknessId = item.ContainsKey("thicknessID") ? Convert.ToInt32(item["thicknessID"]) : 0;
                                var materialName = Scalar(conn, "SELECT Name FROM [Materials] WHERE MaterialID=?", materialId);
                                var colourName = Scalar(conn, "SELECT ColourName FROM [Colours] WHERE ColourID=?", colourId);
                                var thicknessValue = Scalar(conn, "SELECT ThicknessValue FROM [BoardThickness] WHERE ThicknessID=?", thicknessId);
                                item["materialName"] = materialName != null ? materialName.ToString() : "";
                                item["colourName"] = colourName != null ? colourName.ToString() : "";
                                item["thicknessValue"] = thicknessValue != null ? thicknessValue.ToString() : "";
                            }
                            else
                            {
                                item["modelName"] = item.ContainsKey("name") ? Convert.ToString(item["name"]) : item["modelName"];
                            }

                            var panels = new List<Dictionary<string, object>>();
                            int cabId = r["CabinetID"] != DBNull.Value ? Convert.ToInt32(r["CabinetID"]) : 0;
                            if (cabId > 0)
                            {
                                using (var pc = new OleDbCommand("SELECT pd.*, bt.ThicknessValue FROM [PanelDefinitions] pd LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID=bt.ThicknessID WHERE pd.CabinetID=? ORDER BY pd.SortOrder", conn))
                                {
                                    pc.Parameters.AddWithValue("?", cabId);
                                    using (var pr = pc.ExecuteReader())
                                        while (pr.Read())
                                        {
                                            var pnl = new Dictionary<string, object>();
                                            pnl["panelName"] = (pr["PanelName"] ?? "").ToString();
                                            pnl["thickness"] = pr["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(pr["ThicknessValue"]) : 18;

                                            var drills = new List<Dictionary<string, object>>();
                                            using (var dc = new OleDbCommand("SELECT * FROM [DrillingPrograms] WHERE CabinetID=? AND PanelName=? ORDER BY ProgramID", conn))
                                            {
                                                dc.Parameters.AddWithValue("?", cabId);
                                                dc.Parameters.AddWithValue("?", (pr["PanelName"] ?? "").ToString());
                                                using (var dr = dc.ExecuteReader())
                                                    while (dr.Read())
                                                    {
                                                        drills.Add(new Dictionary<string, object> {
                                                            { "programID", Convert.ToInt32(dr["ProgramID"]) },
                                                            { "programFile", (dr["ProgramFile"] ?? "").ToString() },
                                                            { "fileType", (dr["FileType"] ?? "").ToString() },
                                                            { "description", dr["Description"] != DBNull.Value ? dr["Description"].ToString() : "" }
                                                        });
                                                    }
                                            }
                                            pnl["drillPrograms"] = drills;
                                            panels.Add(pnl);
                                        }
                                }
                            }
                            item["panels"] = panels;
                            items.Add(item);
                        }
                }
                result["items"] = items;

                var statusLog = new List<Dictionary<string, object>>();
                using (var cmd = new OleDbCommand("SELECT pl.*, u.Username FROM [ProductionLog] pl LEFT JOIN [Users] u ON pl.ChangedBy=u.UserID WHERE pl.ProductionID IN (SELECT ProductionID FROM [OrderProduction] WHERE OrderID=?) ORDER BY pl.ChangedAt", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            statusLog.Add(new Dictionary<string, object> {
                                { "status", (r["Status"] ?? "").ToString() },
                                { "changedBy", r["Username"] != DBNull.Value ? r["Username"].ToString() : "" },
                                { "changedAt", Convert.ToDateTime(r["ChangedAt"]).ToString("yyyy-MM-dd HH:mm") },
                                { "comment", r["Comment"] != DBNull.Value ? r["Comment"].ToString() : "" }
                            });
                }
                result["statusLog"] = statusLog;

                using (var cmd = new OleDbCommand("SELECT TOP 1 Status, Notes FROM [OrderProduction] WHERE OrderID=? ORDER BY ProductionID DESC", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                        {
                            result["prodStatus"] = (r["Status"] ?? "").ToString();
                            result["prodNotes"] = r["Notes"] != DBNull.Value ? r["Notes"].ToString() : "";
                        }
                        else result["prodStatus"] = "Received";
                }
            }
            JsonOK(ctx, result);
        }

        private void HandleProdUpdateStatus(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int orderId = data != null && data.ContainsKey("orderID") ? Convert.ToInt32(data["orderID"]) : 0;
            string newStatus = data != null && data.ContainsKey("status") ? ((data["status"] ?? "").ToString()) : "";
            string notes = data != null && data.ContainsKey("notes") ? ((data["notes"] ?? "").ToString()) : "";
            int userId = data != null && data.ContainsKey("userID") ? Convert.ToInt32(data["userID"]) : 0;

            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT ProductionID FROM [OrderProduction] WHERE OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    object existing = cmd.ExecuteScalar();
                    if (existing != null)
                    {
                        using (var up = new OleDbCommand("UPDATE [OrderProduction] SET Status=?, Notes=?, UpdatedBy=?, UpdatedAt=NOW() WHERE ProductionID=?", conn))
                        {
                            up.Parameters.AddWithValue("?", newStatus);
                            up.Parameters.AddWithValue("?", notes);
                            up.Parameters.AddWithValue("?", userId);
                            up.Parameters.AddWithValue("?", Convert.ToInt32(existing));
                            up.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (var ins = new OleDbCommand("INSERT INTO [OrderProduction] (OrderID, Status, Notes, UpdatedBy, UpdatedAt) VALUES (?,?,?,?,NOW())", conn))
                        {
                            ins.Parameters.AddWithValue("?", orderId);
                            ins.Parameters.AddWithValue("?", newStatus);
                            ins.Parameters.AddWithValue("?", notes);
                            ins.Parameters.AddWithValue("?", userId);
                            ins.ExecuteNonQuery();
                        }
                    }
                }

                using (var cmd = new OleDbCommand("INSERT INTO [ProductionLog] (ProductionID, Status, ChangedBy, ChangedAt, Comment) SELECT ProductionID, ?, ?, NOW(), ? FROM [OrderProduction] WHERE OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", newStatus);
                    cmd.Parameters.AddWithValue("?", userId);
                    cmd.Parameters.AddWithValue("?", notes);
                    cmd.Parameters.AddWithValue("?", orderId);
                    cmd.ExecuteNonQuery();
                }
            }
            JsonOK(ctx, new { success = true, status = newStatus });
        }

        private void HandleProdDrillPrograms(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int cabId = data != null && data.ContainsKey("cabinetID") ? Convert.ToInt32(data["cabinetID"]) : 0;
            string panelName = data != null && data.ContainsKey("panelName") ? ((data["panelName"] ?? "").ToString()) : "";

            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT * FROM [DrillingPrograms] WHERE CabinetID=? AND PanelName=? ORDER BY ProgramID", conn))
                {
                    cmd.Parameters.AddWithValue("?", cabId);
                    cmd.Parameters.AddWithValue("?", panelName);
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            list.Add(new Dictionary<string, object> {
                                { "programID", Convert.ToInt32(r["ProgramID"]) },
                                { "cabinetID", Convert.ToInt32(r["CabinetID"]) },
                                { "panelName", (r["PanelName"] ?? "").ToString() },
                                { "programFile", (r["ProgramFile"] ?? "").ToString() },
                                { "fileType", (r["FileType"] ?? "").ToString() },
                                { "description", r["Description"] != DBNull.Value ? r["Description"].ToString() : "" }
                            });
                }
            }
            JsonOK(ctx, list);
        }

        // --- BOQ HTML ---
        private void HandleBoqHtml(HttpContext ctx)
        {
            var itemsRaw = new System.Collections.ArrayList();
            string username = "Guest";
            int userID = 0;
            int orderId = ParseInt(ctx.Request["orderID"]);

            string cabID = ctx.Request["cabID"];
            if (orderId > 0)
            {
                itemsRaw = BuildOrderItemsRaw(orderId, out username);
            }
            else if (!string.IsNullOrEmpty(cabID))
            {
                var item = new Dictionary<string, object>();
                item["cabinetID"] = cabID;
                item["width"] = ctx.Request["W"] ?? "0";
                item["depth"] = ctx.Request["D"] ?? "0";
                item["height"] = ctx.Request["H"] ?? "0";
                item["materialID"] = ctx.Request["materialID"] ?? "1";
                item["thicknessID"] = ctx.Request["thicknessID"] ?? "2";
                item["colourID"] = ctx.Request["colourID"] ?? "1";
                item["modelName"] = ctx.Request["modelName"] ?? "Cabinet";
                item["quantity"] = ctx.Request["qty"] ?? "1";
                itemsRaw.Add(item);
                if (!string.IsNullOrEmpty(ctx.Request["username"])) username = ctx.Request["username"];
                if (!string.IsNullOrEmpty(ctx.Request["userID"])) userID = int.Parse(ctx.Request["userID"]);
            }
            else
            {
                string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                if (data != null)
                {
                    if (data.ContainsKey("orderID"))
                        orderId = ParseInt((data["orderID"] ?? "0").ToString());
                    if (data.ContainsKey("items"))
                        itemsRaw = (System.Collections.ArrayList)data["items"];
                    if (data.ContainsKey("username"))
                        username = (data["username"] ?? "Guest").ToString();
                    if (data.ContainsKey("userID"))
                        userID = int.Parse((data["userID"] ?? "0").ToString());
                }
                if (orderId > 0)
                    itemsRaw = BuildOrderItemsRaw(orderId, out username);
            }

            string quoteNo = "QTE-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);

            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'><title>BOQ - Cabinet Store</title><style>");
            sb.Append("@page{margin:12mm} body{font-family:'Segoe UI',Arial,sans-serif;color:#333;font-size:12px}");
            sb.Append(".hdr{text-align:center;border-bottom:3px solid #A54F6B;padding-bottom:12px;margin-bottom:20px}");
            sb.Append(".hdr h1{margin:0;color:#A54F6B;font-size:24px} .hdr p{margin:3px 0 0;color:#777;font-size:13px}");
            sb.Append(".hdr .qinfo{font-size:13px;color:#555;margin-top:6px}");
            sb.Append("h2{background:#f5f5f5;padding:8px 12px;font-size:16px;margin:20px 0 8px;border-left:4px solid #A54F6B}");
            sb.Append(".cab-name{font-size:14px;font-weight:bold;margin:12px 0 4px;color:#555}");
            sb.Append(".cab-dims{font-size:12px;color:#888;margin:0 0 8px}");
            sb.Append("table{width:100%;border-collapse:collapse;margin-bottom:6px}");
            sb.Append("th{background:#A54F6B;color:#fff;padding:5px 6px;text-align:left;font-size:11px}");
            sb.Append("td{padding:4px 6px;border-bottom:1px solid #eee;font-size:11px}");
            sb.Append("tr:nth-child(even) td{background:#f9f9f9}");
            sb.Append(".hw td{background:#f0f7ee;font-style:italic;color:#555}");
            sb.Append(".sub{text-align:right;font-weight:bold;padding:4px 8px;font-size:12px}");
            sb.Append(".gst{text-align:right;padding:3px 8px;font-size:12px;color:#666}");
            sb.Append(".gt{text-align:right;font-size:16px;font-weight:bold;color:#A54F6B;border-top:2px solid #333;padding-top:8px;margin-top:8px}");
            sb.Append(".ftr{text-align:center;color:#999;font-size:11px;margin-top:30px;border-top:1px solid #ddd;padding-top:10px}");
            sb.Append("@media print{.np{display:none}} .np{text-align:center;margin-bottom:15px}");
            sb.Append(".np button{background:#A54F6B;color:#fff;border:none;padding:8px 20px;font-size:14px;cursor:pointer;border-radius:4px;margin:0 5px}");
            sb.Append("</style></head><body>");
            sb.Append("<div class='np'><button onclick='window.print()'>Print / Save as PDF</button></div>");
            sb.Append("<div class='hdr'><h1>CABINET STORE</h1><p>Detailed Bill of Quantities</p>");
            sb.Append("<div class='qinfo'>Quote #: ").Append(XmlSafe(quoteNo)).Append(" | Customer: ").Append(XmlSafe(username)).Append(" | Date: ").Append(DateTime.Now.ToString("dd-MMM-yyyy")).Append("</div>");
            sb.Append("</div>");

            double grandTotal = 0;
            int cabIndex = 0;

            using (var conn = GetConn())
            {
                conn.Open();
                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    string itemType = (raw.ContainsKey("type") ? Convert.ToString(raw["type"]) : null) ?? "cabinet";
                    int qty = int.Parse(raw.ContainsKey("quantity") ? (raw["quantity"] ?? "1").ToString() : "1");
                    if (qty < 1) qty = 1;

                    if (itemType != "cabinet")
                    {
                        cabIndex++;
                        string itemName = (raw.ContainsKey("name") ? Convert.ToString(raw["name"]) : null) ?? "Item";
                        double ncUnitPrice = double.Parse(raw.ContainsKey("unitPrice") ? (raw["unitPrice"] ?? "0").ToString() : "0");
                        double ncLineTotal = Math.Round(ncUnitPrice * qty, 2);
                        grandTotal += ncLineTotal;
                        string typeLabel = itemType == "hardware" ? "HW" : itemType == "board" ? "BRD" : "LAM";
                        sb.Append("<div class='cab-name'>").Append(cabIndex).Append(". [").Append(typeLabel).Append("] ").Append(XmlSafe(itemName)).Append("</div>");
                        sb.Append("<div class='cab-dims'>Loose item · ").Append(typeLabel).Append("</div>");
                        sb.Append("<table><thead><tr><th>#</th><th>Item</th><th>Unit Price</th><th>Qty</th><th>Amount</th></tr></thead><tbody>");
                        sb.Append("<tr><td>1</td><td>").Append(XmlSafe(itemName)).Append("</td><td>₹ ").Append(ncUnitPrice.ToString("N2")).Append("</td><td>").Append(qty).Append("</td><td>₹ ").Append(ncLineTotal.ToString("N2")).Append("</td></tr>");
                        sb.Append("</tbody></table>");
                        sb.Append("<div class='sub'>Unit Price: ₹ ").Append(ncUnitPrice.ToString("N2")).Append(" | Qty: ").Append(qty).Append(" | Line Total: ₹ ").Append(ncLineTotal.ToString("N2")).Append("</div>");
                        continue;
                    }

                    int cabId = int.Parse((raw["cabinetID"] ?? "0").ToString());
                    double W = double.Parse((raw["width"] ?? "0").ToString());
                    double D = double.Parse((raw["depth"] ?? "0").ToString());
                    double H = double.Parse((raw["height"] ?? "0").ToString());
                    int matId = int.Parse((raw["materialID"] ?? "1").ToString());
                    int thickId = int.Parse((raw["thicknessID"] ?? "2").ToString());
                    int colId = int.Parse((raw["colourID"] ?? "1").ToString());
                    string modelName = (raw["modelName"] ?? "Cabinet").ToString();
                    if (qty < 1) qty = 1;

                    string catName = "", matName = "", colName = "", colHex = "", thickVal = "";
                    using (var sc = new OleDbCommand("SELECT cg.CategoryName FROM (Cabinets c LEFT JOIN Categories cg ON c.CategoryID=cg.CategoryID) WHERE c.CabinetID=?", conn))
                    { sc.Parameters.AddWithValue("?", cabId); var v = sc.ExecuteScalar(); if (v != null) catName = v.ToString(); }
                    using (var sc = new OleDbCommand("SELECT Name FROM Materials WHERE MaterialID=?", conn))
                    { sc.Parameters.AddWithValue("?", matId); var v = sc.ExecuteScalar(); matName = v != null ? v.ToString() : ""; }
                    using (var sc = new OleDbCommand("SELECT ColourName, HexCode FROM Colours WHERE ColourID=?", conn))
                    { sc.Parameters.AddWithValue("?", colId); using (var sr = sc.ExecuteReader()) { if (sr.Read()) { colName = (sr["ColourName"] ?? "").ToString(); colHex = (sr["HexCode"] ?? "").ToString(); } } }
                    using (var sc = new OleDbCommand("SELECT ThicknessValue FROM BoardThickness WHERE ThicknessID=?", conn))
                    { sc.Parameters.AddWithValue("?", thickId); var v = sc.ExecuteScalar(); thickVal = v != null ? v.ToString() + "mm" : ""; }

                    cabIndex++;
                    sb.Append("<div class='cab-name'>").Append(cabIndex).Append(". ").Append(modelName).Append("</div>");
                    sb.Append("<div class='cab-dims'>").Append(W).Append(" x ").Append(D).Append(" x ").Append(H).Append("mm | ");
                    sb.Append(matName).Append(" | ").Append(colName).Append(" | ").Append(thickVal).Append(" | Qty: ").Append(qty).Append("</div>");

                    double unitPrice = 0;
                    sb.Append("<table><thead><tr><th>#</th><th>Component</th><th>W (mm)</th><th>H (mm)</th><th>Thk</th><th>Colour</th><th>SFT</th><th>Rs/SFT</th><th>Amount</th></tr></thead><tbody>");
                    int pidx = 0;
                    using (var cmd = new OleDbCommand("SELECT pd.*,bt.ThicknessValue FROM [PanelDefinitions] pd LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID=bt.ThicknessID WHERE pd.CabinetID=? ORDER BY pd.SortOrder", conn))
                    {
                        cmd.Parameters.AddWithValue("?", cabId);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                            {
                                int pdId = Convert.ToInt32(r["PanelDefID"]);
                                string pName = r["PanelName"].ToString();
                                double thick = r["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(r["ThicknessValue"]) : 18;
                                string fd1 = (r["FaceDim1"] ?? "Width").ToString();
                                string fd2 = (r["FaceDim2"] ?? "Height").ToString();
                                double pW = 0, pD = 0, pH = 0;
                                using (var fc = new OleDbCommand("SELECT * FROM [PanelFormulas] WHERE PanelDefID=?", conn))
                                {
                                    fc.Parameters.AddWithValue("?", pdId);
                                    using (var fr = fc.ExecuteReader())
                                        while (fr.Read())
                                        {
                                            string dt = fr["DimensionType"].ToString();
                                            double v = FormulaEngine.Evaluate(fr["Expression"].ToString(), W, D, H, thick);
                                            if (dt == "Width") pW = v; else if (dt == "Depth") pD = v; else if (dt == "Height") pH = v;
                                        }
                                }
                                double dim1 = fd1 == "Width" ? pW : (fd1 == "Depth" ? pD : pH);
                                double dim2 = fd2 == "Width" ? pW : (fd2 == "Depth" ? pD : pH);
                                double sft = FormulaEngine.ComputeSFT(dim1, dim2);
                                int panelThickId = r["DefaultThicknessID"] != DBNull.Value ? Convert.ToInt32(r["DefaultThicknessID"]) : thickId;
                                double ppsft = GetPricePerSFT(conn, matId, panelThickId, colId);
                                double ptot = Math.Round(sft * ppsft, 2);
                                unitPrice += ptot;
                                pidx++;
                                sb.Append("<tr><td>").Append(pidx).Append("</td><td>").Append(pName);
                                sb.Append("</td><td>").Append(Math.Round(dim1, 2));
                                sb.Append("</td><td>").Append(Math.Round(dim2, 2));
                                sb.Append("</td><td>").Append(thick).Append("mm");
                                sb.Append("</td><td>").Append(colName).Append(colHex != "" ? " (" + colHex + ")" : "");
                                sb.Append("</td><td>").Append(Math.Round(sft, 4));
                                sb.Append("</td><td>₹ ").Append(ppsft.ToString("N2"));
                                sb.Append("</td><td>₹ ").Append(ptot.ToString("N2")).Append("</td></tr>");
                            }
                    }
                    double hwTotal = 0;
                    int hwIdx = 0;
                    using (var hc = new OleDbCommand("SELECT h.*,ch.Quantity FROM [HardwareItems] h INNER JOIN [CabinetHardwareMap] ch ON h.HardwareID=ch.HardwareID WHERE ch.CabinetID=? AND ch.Quantity>0 ORDER BY ch.HardwareID", conn))
                    {
                        hc.Parameters.AddWithValue("?", cabId);
                        using (var hr = hc.ExecuteReader())
                            while (hr.Read())
                            {
                                string hwName = hr["HardwareName"].ToString();
                                double hwUp = Convert.ToDouble(hr["UnitPrice"]);
                                int hwQty = Convert.ToInt32(hr["Quantity"]);
                                double hwLt = Math.Round(hwUp * hwQty, 2);
                                hwTotal += hwLt;
                                hwIdx++;
                                sb.Append("<tr class='hw'><td>").Append(pidx + hwIdx).Append("</td><td>");
                                sb.Append("[HW] ").Append(hwName).Append("</td><td>Hardware</td><td>-</td><td>-</td><td>-</td><td>-</td><td>₹ ").Append(hwUp.ToString("N2"));
                                sb.Append("</td><td>₹ ").Append(hwLt.ToString("N2")).Append("</td></tr>");
                            }
                    }
                    sb.Append("</tbody></table>");

                    unitPrice += hwTotal;
                    unitPrice = Math.Round(unitPrice, 2);
                    double lineTotal = Math.Round(unitPrice * qty, 2);
                    grandTotal += lineTotal;
                    sb.Append("<div class='sub'>Unit Price: ₹ ").Append(unitPrice.ToString("N2"));
                    sb.Append(" | Qty: ").Append(qty).Append(" | Line Total: ₹ ").Append(lineTotal.ToString("N2")).Append("</div>");
                }
            }

            double gstRate = 18;
            double gstAmount = Math.Round(grandTotal * gstRate / 100, 2);
            double totalWithGST = Math.Round(grandTotal + gstAmount, 2);

            sb.Append("<div class='sub'>Subtotal: ₹ ").Append(Math.Round(grandTotal, 2).ToString("N2")).Append("</div>");
            sb.Append("<div class='gst'>GST @ ").Append(gstRate).Append("%: ₹ ").Append(gstAmount.ToString("N2")).Append("</div>");
            sb.Append("<div class='gt'>Grand Total (incl. GST): ₹ ").Append(totalWithGST.ToString("N2")).Append("</div>");
            sb.Append("<div class='ftr'><p>Computer-generated BOQ. Prices in Indian Rupees (₹). All dimensions in mm.</p>");
            sb.Append("<p>GST @ ").Append(gstRate).Append("% applicable as per government regulations.</p></div>");
            sb.Append("</body></html>");

            ctx.Response.ContentType = "text/html";
            ctx.Response.Write(sb.ToString());
        }

        // --- BOQ Excel ---
        private void HandleBoqExcel(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int orderId = data != null && data.ContainsKey("orderID") ? ParseInt((data["orderID"] ?? "0").ToString()) : 0;
            var itemsRaw = data != null && data.ContainsKey("items") ? (System.Collections.ArrayList)data["items"] : new System.Collections.ArrayList();
            string username = "Guest";
            if (data != null && data.ContainsKey("username"))
                username = (data["username"] ?? "Guest").ToString();
            if (orderId > 0)
                itemsRaw = BuildOrderItemsRaw(orderId, out username);

            string quoteNo = "QTE-" + DateTime.Now.ToString("yyyyMMdd") + "-" + new Random().Next(1000, 9999);
            string sheet1Xml = BuildBoqSheetXml(itemsRaw, quoteNo, username);
            string sheet2Xml = BuildSummarySheetXml(itemsRaw, quoteNo, username);
            byte[] xlsxBytes = BuildXlsx(sheet1Xml, sheet2Xml);
            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=BOQ_" + quoteNo + ".xlsx");
            ctx.Response.BinaryWrite(xlsxBytes);
        }

        private System.Collections.ArrayList BuildOrderItemsRaw(int orderId, out string username)
        {
            username = "Production";
            var itemsRaw = new System.Collections.ArrayList();

            using (var conn = GetConn())
            {
                conn.Open();

                using (var cmd = new OleDbCommand("SELECT TOP 1 u.Username, u.BusinessName FROM [Orders] o LEFT JOIN [Users] u ON o.UserID=u.UserID WHERE o.OrderID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            var businessName = r["BusinessName"] != DBNull.Value ? r["BusinessName"].ToString() : "";
                            var userName = r["Username"] != DBNull.Value ? r["Username"].ToString() : "";
                            username = !string.IsNullOrEmpty(businessName) ? businessName : (!string.IsNullOrEmpty(userName) ? userName : "Production");
                        }
                    }
                }

                using (var cmd = new OleDbCommand("SELECT oi.*, c.ModelName FROM [OrderItems] oi LEFT JOIN [Cabinets] c ON oi.CabinetID=c.CabinetID WHERE oi.OrderID=? ORDER BY oi.OrderItemID", conn))
                {
                    cmd.Parameters.AddWithValue("?", orderId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var item = new Dictionary<string, object>();
                            Dictionary<string, object> config = null;
                            try { config = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(r["ConfigJSON"].ToString()); }
                            catch { config = new Dictionary<string, object>(); }

                            var itemType = config.ContainsKey("type") ? Convert.ToString(config["type"]) : "cabinet";
                            item["type"] = string.IsNullOrEmpty(itemType) ? "cabinet" : itemType;
                            item["quantity"] = r["Quantity"] != DBNull.Value ? Convert.ToInt32(r["Quantity"]) : 1;
                            item["unitPrice"] = r["UnitPrice"] != DBNull.Value ? Convert.ToDouble(r["UnitPrice"]) : 0;
                            item["lineTotal"] = r["LineTotal"] != DBNull.Value ? Convert.ToDouble(r["LineTotal"]) : 0;

                            if (Convert.ToString(item["type"]) == "cabinet")
                            {
                                item["cabinetID"] = r["CabinetID"] != DBNull.Value ? Convert.ToInt32(r["CabinetID"]) : 0;
                                item["modelName"] = r["ModelName"] != DBNull.Value ? r["ModelName"].ToString() : "Cabinet";
                                item["width"] = config.ContainsKey("W") ? config["W"] : 0;
                                item["depth"] = config.ContainsKey("D") ? config["D"] : 0;
                                item["height"] = config.ContainsKey("H") ? config["H"] : 0;
                                item["materialID"] = config.ContainsKey("MaterialID") ? config["MaterialID"] : 1;
                                item["thicknessID"] = config.ContainsKey("ThicknessID") ? config["ThicknessID"] : 2;
                                item["colourID"] = config.ContainsKey("ColourID") ? config["ColourID"] : 1;
                            }
                            else
                            {
                                item["name"] = config.ContainsKey("name") ? Convert.ToString(config["name"]) : (r["ModelName"] != DBNull.Value ? r["ModelName"].ToString() : "Item");
                                item["productID"] = config.ContainsKey("productID") ? Convert.ToString(config["productID"]) : "";
                            }

                            itemsRaw.Add(item);
                        }
                    }
                }
            }

            return itemsRaw;
        }

        private string BuildBoqSheetXml(System.Collections.ArrayList itemsRaw, string quoteNo, string username)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            // Title row
            sb.Append("<row><c t=\"inlineStr\" s=\"2\"><is><t>").Append(XmlSafe("CABINET STORE - DETAILED BOQ")).Append("</t></is></c></row>");
            sb.Append("<row><c t=\"inlineStr\"><is><t>Quote #: ").Append(XmlSafe(quoteNo)).Append(" | Customer: ").Append(XmlSafe(username)).Append(" | Date: ").Append(DateTime.Now.ToString("dd-MMM-yyyy")).Append("</t></is></c></row>");
            sb.Append("<row></row>");

            double grandTotal = 0;
            int cabIndex = 0;

            using (var conn = GetConn())
            {
                conn.Open();
                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    string itemType = (raw.ContainsKey("type") ? Convert.ToString(raw["type"]) : null) ?? "cabinet";
                    int qty = int.Parse(raw.ContainsKey("quantity") ? (raw["quantity"] ?? "1").ToString() : "1");
                    if (qty < 1) qty = 1;

                    if (itemType != "cabinet")
                    {
                        cabIndex++;
                        string itemName = (raw.ContainsKey("name") ? Convert.ToString(raw["name"]) : null) ?? "Item";
                        double ncUnitPrice = double.Parse(raw.ContainsKey("unitPrice") ? (raw["unitPrice"] ?? "0").ToString() : "0");
                        double ncLineTotal = Math.Round(ncUnitPrice * qty, 2);
                        grandTotal += ncLineTotal;
                        string typeLabel = itemType == "hardware" ? "HW" : itemType == "board" ? "BRD" : "LAM";
                        sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>").Append(cabIndex).Append(". [").Append(typeLabel).Append("] ").Append(XmlSafe(itemName)).Append("</t></is></c></row>");
                        sb.Append("<row><c t=\"inlineStr\"><is><t>Loose item · ").Append(typeLabel).Append("</t></is></c></row>");
                        sb.Append("<row><c t=\"inlineStr\" s=\"7\"><is><t>#</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Item</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Unit Price</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Qty</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Amount</t></is></c></row>");
                        sb.Append("<row><c t=\"n\" s=\"6\"><v>1</v></c><c t=\"inlineStr\" s=\"6\"><is><t>").Append(XmlSafe(itemName)).Append("</t></is></c><c t=\"n\" s=\"6\"><v>").Append(Math.Round(ncUnitPrice, 2)).Append("</v></c><c t=\"n\" s=\"6\"><v>").Append(qty).Append("</v></c><c t=\"n\" s=\"6\"><v>").Append(ncLineTotal).Append("</v></c></row>");
                        sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Unit Price</t></is></c><c t=\"n\" s=\"4\"><v>").Append(ncUnitPrice).Append("</v></c></row>");
                        if (qty > 1)
                            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Qty: ").Append(qty).Append("</t></is></c><c t=\"n\" s=\"4\"><v>").Append(ncLineTotal).Append("</v></c></row>");
                        continue;
                    }

                    int cabId = int.Parse((raw["cabinetID"] ?? "0").ToString());
                    double W = double.Parse((raw["width"] ?? "0").ToString());
                    double D = double.Parse((raw["depth"] ?? "0").ToString());
                    double H = double.Parse((raw["height"] ?? "0").ToString());
                    int matId = int.Parse((raw["materialID"] ?? "1").ToString());
                    int thickId = int.Parse((raw["thicknessID"] ?? "2").ToString());
                    int colId = int.Parse((raw["colourID"] ?? "1").ToString());
                    string modelName = (raw["modelName"] ?? "Cabinet").ToString();
                    if (qty < 1) qty = 1;
                    string matName = "", colName = "", colHex = "", thickVal = "";
                    using (var sc = new OleDbCommand("SELECT Name FROM Materials WHERE MaterialID=?", conn))
                    { sc.Parameters.AddWithValue("?", matId); var v = sc.ExecuteScalar(); matName = v != null ? v.ToString() : ""; }
                    using (var sc = new OleDbCommand("SELECT ColourName, HexCode FROM Colours WHERE ColourID=?", conn))
                    { sc.Parameters.AddWithValue("?", colId); using (var sr = sc.ExecuteReader()) { if (sr.Read()) { colName = (sr["ColourName"] ?? "").ToString(); colHex = (sr["HexCode"] ?? "").ToString(); } } }
                    using (var sc = new OleDbCommand("SELECT ThicknessValue FROM BoardThickness WHERE ThicknessID=?", conn))
                    { sc.Parameters.AddWithValue("?", thickId); var v = sc.ExecuteScalar(); thickVal = v != null ? v.ToString() + "mm" : ""; }

                    cabIndex++;
                    sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>").Append(cabIndex).Append(". ").Append(XmlSafe(modelName)).Append("</t></is></c></row>");
                    sb.Append("<row><c t=\"inlineStr\"><is><t>").Append(W).Append(" x ").Append(D).Append(" x ").Append(H).Append("mm | ").Append(XmlSafe(matName)).Append(" | ").Append(XmlSafe(colName)).Append(" | ").Append(thickVal).Append(" | Qty: ").Append(qty).Append("</t></is></c></row>");

                    sb.Append("<row><c t=\"inlineStr\" s=\"7\"><is><t>#</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Component</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>W (mm)</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>H (mm)</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Thk</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Colour</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>SFT</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Rs/SFT</t></is></c><c t=\"inlineStr\" s=\"7\"><is><t>Amount</t></is></c></row>");

                    double unitPrice = 0;
                    int pidx = 0;
                    using (var cmd = new OleDbCommand("SELECT pd.*,bt.ThicknessValue FROM [PanelDefinitions] pd LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID=bt.ThicknessID WHERE pd.CabinetID=? ORDER BY pd.SortOrder", conn))
                    {
                        cmd.Parameters.AddWithValue("?", cabId);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                            {
                                int pdId = Convert.ToInt32(r["PanelDefID"]);
                                string pName = r["PanelName"].ToString();
                                double thick = r["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(r["ThicknessValue"]) : 18;
                                string fd1 = (r["FaceDim1"] ?? "Width").ToString();
                                string fd2 = (r["FaceDim2"] ?? "Height").ToString();
                                double pW = 0, pD = 0, pH = 0;
                                using (var fc = new OleDbCommand("SELECT * FROM [PanelFormulas] WHERE PanelDefID=?", conn))
                                {
                                    fc.Parameters.AddWithValue("?", pdId);
                                    using (var fr = fc.ExecuteReader())
                                        while (fr.Read())
                                        {
                                            string dt = fr["DimensionType"].ToString();
                                            double v = FormulaEngine.Evaluate(fr["Expression"].ToString(), W, D, H, thick);
                                            if (dt == "Width") pW = v; else if (dt == "Depth") pD = v; else if (dt == "Height") pH = v;
                                        }
                                }
                                double dim1 = fd1 == "Width" ? pW : (fd1 == "Depth" ? pD : pH);
                                double dim2 = fd2 == "Width" ? pW : (fd2 == "Depth" ? pD : pH);
                                double sft = FormulaEngine.ComputeSFT(dim1, dim2);
                                int panelThickId = r["DefaultThicknessID"] != DBNull.Value ? Convert.ToInt32(r["DefaultThicknessID"]) : thickId;
                                double ppsft = GetPricePerSFT(conn, matId, panelThickId, colId);
                                double ptot = Math.Round(sft * ppsft, 2);
                                unitPrice += ptot;
                                pidx++;
                                string st = (pidx % 2 == 0) ? "5" : "6";
                                sb.Append("<row><c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(pidx).Append("</v></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(XmlSafe(pName)).Append("</t></is></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(Math.Round(dim1, 2)).Append("</v></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(Math.Round(dim2, 2)).Append("</v></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(thick).Append("mm</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(XmlSafe(colName + (colHex != "" ? " (" + colHex + ")" : ""))).Append("</t></is></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(Math.Round(sft, 4)).Append("</v></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(ppsft).Append("</v></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(ptot).Append("</v></c></row>");
                            }
                    }

                    double hwTotal = 0;
                    int hwIdx = 0;
                    using (var hc = new OleDbCommand("SELECT h.*,ch.Quantity FROM [HardwareItems] h INNER JOIN [CabinetHardwareMap] ch ON h.HardwareID=ch.HardwareID WHERE ch.CabinetID=? AND ch.Quantity>0 ORDER BY ch.HardwareID", conn))
                    {
                        hc.Parameters.AddWithValue("?", cabId);
                        using (var hr = hc.ExecuteReader())
                            while (hr.Read())
                            {
                                string hwName = hr["HardwareName"].ToString();
                                double hwUp = Convert.ToDouble(hr["UnitPrice"]);
                                int hwQty = Convert.ToInt32(hr["Quantity"]);
                                double hwLt = Math.Round(hwUp * hwQty, 2);
                                hwTotal += hwLt;
                                hwIdx++;
                                string hwSt = (hwIdx % 2 == 0) ? "5" : "6";
                                sb.Append("<row><c t=\"n\" s=\"").Append(hwSt).Append("\"><v>").Append(pidx + hwIdx).Append("</v></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>[HW] ").Append(XmlSafe(hwName)).Append("</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>Hardware</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>-</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>-</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>-</t></is></c>");
                                sb.Append("<c t=\"inlineStr\" s=\"").Append(hwSt).Append("\"><is><t>-</t></is></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(hwSt).Append("\"><v>").Append(hwUp).Append("</v></c>");
                                sb.Append("<c t=\"n\" s=\"").Append(hwSt).Append("\"><v>").Append(hwLt).Append("</v></c></row>");
                            }
                    }
                    unitPrice += hwTotal;
                    unitPrice = Math.Round(unitPrice, 2);
                    double lineTotal = Math.Round(unitPrice * qty, 2);
                    grandTotal += lineTotal;
                    sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Unit Price</t></is></c><c t=\"n\" s=\"4\"><v>").Append(unitPrice).Append("</v></c></row>");
                    if (qty > 1)
                        sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Qty: ").Append(qty).Append("</t></is></c><c t=\"n\" s=\"4\"><v>").Append(lineTotal).Append("</v></c></row>");
                }
            }

            double gstRate = 18;
            double gstAmount = Math.Round(grandTotal * gstRate / 100, 2);
            double totalWithGST = Math.Round(grandTotal + gstAmount, 2);

            sb.Append("<row></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Subtotal</t></is></c><c t=\"n\" s=\"4\"><v>").Append(Math.Round(grandTotal, 2)).Append("</v></c></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>GST @ ").Append(gstRate).Append("%</t></is></c><c t=\"n\" s=\"4\"><v>").Append(gstAmount).Append("</v></c></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Grand Total (incl. GST)</t></is></c><c t=\"n\" s=\"4\"><v>").Append(totalWithGST).Append("</v></c></row>");

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private string BuildSummarySheetXml(System.Collections.ArrayList itemsRaw, string quoteNo, string username)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sb.Append("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews>");
            sb.Append("<mergeCells count=\"4\">");
            sb.Append("<mergeCell ref=\"A1:G1\"/>");
            sb.Append("<mergeCell ref=\"A2:G2\"/>");
            sb.Append("<mergeCell ref=\"A3:G3\"/>");
            sb.Append("<mergeCell ref=\"A4:G4\"/>");
            sb.Append("</mergeCells>");
            sb.Append("<sheetData>");

            sb.Append("<row><c t=\"inlineStr\" s=\"2\"><is><t>").Append(XmlSafe("CABINET STORE - QUOTATION SUMMARY")).Append("</t></is></c></row>");
            sb.Append("<row><c t=\"inlineStr\"><is><t>Quote #: ").Append(XmlSafe(quoteNo)).Append(" | Date: ").Append(DateTime.Now.ToString("dd-MMM-yyyy")).Append("</t></is></c></row>");
            sb.Append("<row><c t=\"inlineStr\"><is><t>Customer: ").Append(XmlSafe(username)).Append("</t></is></c></row>");
            sb.Append("<row><c t=\"inlineStr\"><is><t>This is a computer-generated quotation. Prices in Indian Rupees (₹). All dimensions in mm.</t></is></c></row>");
            sb.Append("<row></row>");

            sb.Append("<row>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>#</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Cabinet</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Dimensions (mm)</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Material</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Colour</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Qty</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Unit Price (₹)</t></is></c>");
            sb.Append("<c t=\"inlineStr\" s=\"7\"><is><t>Line Total (₹)</t></is></c>");
            sb.Append("</row>");

            double grandTotal = 0;
            int idx = 0;

            using (var conn = GetConn())
            {
                conn.Open();
                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    string itemType = (raw.ContainsKey("type") ? Convert.ToString(raw["type"]) : null) ?? "cabinet";
                    int qty = int.Parse(raw.ContainsKey("quantity") ? (raw["quantity"] ?? "1").ToString() : "1");
                    if (qty < 1) qty = 1;

                    if (itemType != "cabinet")
                    {
                        idx++;
                        string itemName = (raw.ContainsKey("name") ? Convert.ToString(raw["name"]) : null) ?? "Item";
                        double ncUnitPrice = double.Parse(raw.ContainsKey("unitPrice") ? (raw["unitPrice"] ?? "0").ToString() : "0");
                        double ncLineTotal = Math.Round(ncUnitPrice * qty, 2);
                        grandTotal += ncLineTotal;
                        string typeLabel = itemType == "hardware" ? "HW" : itemType == "board" ? "BRD" : "LAM";
                        string ncSt = (idx % 2 == 0) ? "5" : "6";
                        sb.Append("<row>");
                        sb.Append("<c t=\"n\" s=\"").Append(ncSt).Append("\"><v>").Append(idx).Append("</v></c>");
                        sb.Append("<c t=\"inlineStr\" s=\"").Append(ncSt).Append("\"><is><t>[").Append(typeLabel).Append("] ").Append(XmlSafe(itemName)).Append("</t></is></c>");
                        sb.Append("<c t=\"inlineStr\" s=\"").Append(ncSt).Append("\"><is><t>Loose</t></is></c>");
                        sb.Append("<c t=\"inlineStr\" s=\"").Append(ncSt).Append("\"><is><t>-</t></is></c>");
                        sb.Append("<c t=\"inlineStr\" s=\"").Append(ncSt).Append("\"><is><t>-</t></is></c>");
                        sb.Append("<c t=\"n\" s=\"").Append(ncSt).Append("\"><v>").Append(qty).Append("</v></c>");
                        sb.Append("<c t=\"n\" s=\"").Append(ncSt).Append("\"><v>").Append(Math.Round(ncUnitPrice, 2)).Append("</v></c>");
                        sb.Append("<c t=\"n\" s=\"").Append(ncSt).Append("\"><v>").Append(ncLineTotal).Append("</v></c>");
                        sb.Append("</row>");
                        continue;
                    }

                    int cabId = int.Parse((raw["cabinetID"] ?? "0").ToString());
                    double W = double.Parse((raw["width"] ?? "0").ToString());
                    double D = double.Parse((raw["depth"] ?? "0").ToString());
                    double H = double.Parse((raw["height"] ?? "0").ToString());
                    int matId = int.Parse((raw["materialID"] ?? "1").ToString());
                    int thickId = int.Parse((raw["thicknessID"] ?? "2").ToString());
                    int colId = int.Parse((raw["colourID"] ?? "1").ToString());
                    string modelName = (raw["modelName"] ?? "Cabinet").ToString();
                    if (qty < 1) qty = 1;

                    string matName = "", colName = "";
                    using (var sc = new OleDbCommand("SELECT Name FROM Materials WHERE MaterialID=?", conn))
                    { sc.Parameters.AddWithValue("?", matId); var v = sc.ExecuteScalar(); matName = v != null ? v.ToString() : ""; }
                    using (var sc = new OleDbCommand("SELECT ColourName FROM Colours WHERE ColourID=?", conn))
                    { sc.Parameters.AddWithValue("?", colId); var v = sc.ExecuteScalar(); colName = v != null ? v.ToString() : ""; }

                    double unitPrice = 0;
                    using (var cmd = new OleDbCommand("SELECT pd.*,bt.ThicknessValue FROM [PanelDefinitions] pd LEFT JOIN [BoardThickness] bt ON pd.DefaultThicknessID=bt.ThicknessID WHERE pd.CabinetID=? ORDER BY pd.SortOrder", conn))
                    {
                        cmd.Parameters.AddWithValue("?", cabId);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                            {
                                int pdId = Convert.ToInt32(r["PanelDefID"]);
                                double thick = r["ThicknessValue"] != DBNull.Value ? Convert.ToDouble(r["ThicknessValue"]) : 18;
                                string fd1 = (r["FaceDim1"] ?? "Width").ToString();
                                string fd2 = (r["FaceDim2"] ?? "Height").ToString();
                                double pW = 0, pD = 0, pH = 0;
                                using (var fc = new OleDbCommand("SELECT * FROM [PanelFormulas] WHERE PanelDefID=?", conn))
                                {
                                    fc.Parameters.AddWithValue("?", pdId);
                                    using (var fr = fc.ExecuteReader())
                                        while (fr.Read())
                                        {
                                            string dt = fr["DimensionType"].ToString();
                                            double v = FormulaEngine.Evaluate(fr["Expression"].ToString(), W, D, H, thick);
                                            if (dt == "Width") pW = v; else if (dt == "Depth") pD = v; else if (dt == "Height") pH = v;
                                        }
                                }
                                double dim1 = fd1 == "Width" ? pW : (fd1 == "Depth" ? pD : pH);
                                double dim2 = fd2 == "Width" ? pW : (fd2 == "Depth" ? pD : pH);
                                double sft = FormulaEngine.ComputeSFT(dim1, dim2);
                                int panelThickId = r["DefaultThicknessID"] != DBNull.Value ? Convert.ToInt32(r["DefaultThicknessID"]) : thickId;
                                unitPrice += Math.Round(sft * GetPricePerSFT(conn, matId, panelThickId, colId), 2);
                            }
                    }

                    double hwTotal = 0;
                    using (var hc = new OleDbCommand("SELECT SUM(h.UnitPrice * ch.Quantity) FROM [HardwareItems] h INNER JOIN [CabinetHardwareMap] ch ON h.HardwareID=ch.HardwareID WHERE ch.CabinetID=? AND ch.Quantity>0", conn))
                    {
                        hc.Parameters.AddWithValue("?", cabId);
                        object v = hc.ExecuteScalar();
                        if (v != null) hwTotal = Convert.ToDouble(v);
                    }
                    unitPrice += hwTotal;
                    unitPrice = Math.Round(unitPrice, 2);
                    double lineTotal = Math.Round(unitPrice * qty, 2);
                    grandTotal += lineTotal;
                    idx++;

                    string st = (idx % 2 == 0) ? "5" : "6";
                    sb.Append("<row>");
                    sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(idx).Append("</v></c>");
                    sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(XmlSafe(modelName)).Append("</t></is></c>");
                    sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(W).Append(" x ").Append(D).Append(" x ").Append(H).Append("</t></is></c>");
                    sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(XmlSafe(matName)).Append("</t></is></c>");
                    sb.Append("<c t=\"inlineStr\" s=\"").Append(st).Append("\"><is><t>").Append(XmlSafe(colName)).Append("</t></is></c>");
                    sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(qty).Append("</v></c>");
                    sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(unitPrice).Append("</v></c>");
                    sb.Append("<c t=\"n\" s=\"").Append(st).Append("\"><v>").Append(lineTotal).Append("</v></c>");
                    sb.Append("</row>");
                }
            }

            double gstRate = 18;
            double gstAmount = Math.Round(grandTotal * gstRate / 100, 2);
            double totalWithGST = Math.Round(grandTotal + gstAmount, 2);

            sb.Append("<row></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Subtotal</t></is></c><c t=\"n\" s=\"4\"><v>").Append(Math.Round(grandTotal, 2)).Append("</v></c></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>GST @ ").Append(gstRate).Append("%</t></is></c><c t=\"n\" s=\"4\"><v>").Append(gstAmount).Append("</v></c></row>");
            sb.Append("<row><c t=\"inlineStr\" s=\"1\"><is><t>Grand Total (incl. GST)</t></is></c><c t=\"n\" s=\"4\"><v>").Append(totalWithGST).Append("</v></c></row>");

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static byte[] BuildXlsx(string sheet1Xml, string sheet2Xml)
        {
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var ctEntry = archive.CreateEntry("[Content_Types].xml");
                    using (var w = new StreamWriter(ctEntry.Open(), Encoding.UTF8))
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");

                    var relsEntry = archive.CreateEntry("_rels/.rels");
                    using (var w = new StreamWriter(relsEntry.Open(), Encoding.UTF8))
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

                    var wbEntry = archive.CreateEntry("xl/workbook.xml");
                    using (var w = new StreamWriter(wbEntry.Open(), Encoding.UTF8))
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"BOQ\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Summary\" sheetId=\"2\" r:id=\"rId3\"/></sheets></workbook>");

                    var wbRelsEntry = archive.CreateEntry("xl/_rels/workbook.xml.rels");
                    using (var w = new StreamWriter(wbRelsEntry.Open(), Encoding.UTF8))
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/></Relationships>");

                    var sheet1Entry = archive.CreateEntry("xl/worksheets/sheet1.xml");
                    using (var w = new StreamWriter(sheet1Entry.Open(), Encoding.UTF8))
                        w.Write(sheet1Xml);

                    if (!string.IsNullOrEmpty(sheet2Xml))
                    {
                        var sheet2Entry = archive.CreateEntry("xl/worksheets/sheet2.xml");
                        using (var w = new StreamWriter(sheet2Entry.Open(), Encoding.UTF8))
                            w.Write(sheet2Xml);
                    }

                    var stylesEntry = archive.CreateEntry("xl/styles.xml");
                    using (var w = new StreamWriter(stylesEntry.Open(), Encoding.UTF8))
                    {
                        var st = new StringBuilder();
                        st.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                        st.Append("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                        st.Append("<fonts count=\"4\">");
                        st.Append("<font><sz val=\"11\"/><name val=\"Calibri\"/></font>");
                        st.Append("<font><b/><sz val=\"11\"/><color rgb=\"FFA54F6B\"/><name val=\"Calibri\"/></font>");
                        st.Append("<font><b/><sz val=\"14\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>");
                        st.Append("<font><b/><sz val=\"11\"/><color rgb=\"FF333333\"/><name val=\"Calibri\"/></font>");
                        st.Append("</fonts>");
                        st.Append("<fills count=\"6\">");
                        st.Append("<fill><patternFill patternType=\"none\"/></fill>");
                        st.Append("<fill><patternFill patternType=\"gray125\"/></fill>");
                        st.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF3A3A2E\"/></patternFill></fill>");
                        st.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF2D9E6\"/></patternFill></fill>");
                        st.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8F0E0\"/></patternFill></fill>");
                        st.Append("<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFA54F6B\"/></patternFill></fill>");
                        st.Append("</fills>");
                        st.Append("<borders count=\"2\">");
                        st.Append("<border><left/><right/><top/><bottom/></border>");
                        st.Append("<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom></border>");
                        st.Append("</borders>");
                        st.Append("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                        st.Append("<cellXfs count=\"8\">");
                        st.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"2\" fillId=\"2\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" applyBorder=\"1\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"1\" applyFont=\"1\" applyBorder=\"1\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
                        st.Append("<xf numFmtId=\"0\" fontId=\"2\" fillId=\"5\" borderId=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\"/>");
                        st.Append("</cellXfs>");
                        st.Append("</styleSheet>");
                        w.Write(st.ToString());
                    }
                }

                return ms.ToArray();
            }
        }

        private static string XmlSafe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }

        private void HandleGetProfile(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int uid = data != null && data.ContainsKey("userID") ? Convert.ToInt32(data["userID"]) : 0;

            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT UserID, Username, BusinessName, Mobile, Email, Address, TaxType, TaxNumber, TaxNumberPending, ContactPerson, ContactPhone, City, State, Pincode, DealerType, Website FROM [Users] WHERE UserID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", uid);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) { JsonError(ctx, "User not found"); return; }
                        var d = new Dictionary<string, object>();
                        d["userID"] = Convert.ToInt32(r["UserID"]);
                        d["username"] = (r["Username"] ?? "").ToString();
                        d["businessName"] = (r["BusinessName"] ?? "").ToString();
                        d["mobile"] = (r["Mobile"] ?? "").ToString();
                        d["email"] = (r["Email"] ?? "").ToString();
                        d["address"] = (r["Address"] ?? "").ToString();
                        d["taxType"] = (r["TaxType"] ?? "").ToString();
                        d["taxNumber"] = (r["TaxNumber"] ?? "").ToString();
                        d["taxNumberPending"] = r["TaxNumberPending"] != DBNull.Value ? r["TaxNumberPending"].ToString() : "";
                        d["contactPerson"] = r["ContactPerson"] != DBNull.Value ? r["ContactPerson"].ToString() : "";
                        d["contactPhone"] = r["ContactPhone"] != DBNull.Value ? r["ContactPhone"].ToString() : "";
                        d["city"] = r["City"] != DBNull.Value ? r["City"].ToString() : "";
                        d["state"] = r["State"] != DBNull.Value ? r["State"].ToString() : "";
                        d["pincode"] = r["Pincode"] != DBNull.Value ? r["Pincode"].ToString() : "";
                        d["dealerType"] = r["DealerType"] != DBNull.Value ? r["DealerType"].ToString() : "";
                        d["website"] = r["Website"] != DBNull.Value ? r["Website"].ToString() : "";
                        JsonOK(ctx, d);
                    }
                }
            }
        }

        private void HandleSaveProfile(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int uid = data != null && data.ContainsKey("userID") ? Convert.ToInt32(data["userID"]) : 0;

            if (uid <= 0) { JsonError(ctx, "Invalid user"); return; }

            string businessName = data != null && data.ContainsKey("businessName") ? ((data["businessName"] ?? "").ToString()) : "";
            string mobile = data != null && data.ContainsKey("mobile") ? ((data["mobile"] ?? "").ToString()) : "";
            string email = data != null && data.ContainsKey("email") ? ((data["email"] ?? "").ToString()) : "";
            string address = data != null && data.ContainsKey("address") ? ((data["address"] ?? "").ToString()) : "";
            string contactPerson = data != null && data.ContainsKey("contactPerson") ? ((data["contactPerson"] ?? "").ToString()) : "";
            string contactPhone = data != null && data.ContainsKey("contactPhone") ? ((data["contactPhone"] ?? "").ToString()) : "";
            string city = data != null && data.ContainsKey("city") ? ((data["city"] ?? "").ToString()) : "";
            string state = data != null && data.ContainsKey("state") ? ((data["state"] ?? "").ToString()) : "";
            string pincode = data != null && data.ContainsKey("pincode") ? ((data["pincode"] ?? "").ToString()) : "";
            string dealerType = data != null && data.ContainsKey("dealerType") ? ((data["dealerType"] ?? "").ToString()) : "";
            string website = data != null && data.ContainsKey("website") ? ((data["website"] ?? "").ToString()) : "";
            string taxType = data != null && data.ContainsKey("taxType") ? ((data["taxType"] ?? "").ToString()) : "";
            string taxNumber = data != null && data.ContainsKey("taxNumber") ? ((data["taxNumber"] ?? "").ToString()) : "";

            string taxPending = "";
            using (var conn = GetConn())
            {
                conn.Open();

                if (!string.IsNullOrEmpty(taxNumber))
                {
                    string currentTax = "";
                    using (var chk = new OleDbCommand("SELECT TaxNumber FROM [Users] WHERE UserID=?", conn))
                    {
                        chk.Parameters.AddWithValue("?", uid);
                        object val = chk.ExecuteScalar();
                        if (val != null) currentTax = val.ToString();
                    }
                    if (!string.Equals(taxNumber, currentTax, StringComparison.OrdinalIgnoreCase))
                    {
                        taxPending = taxNumber;
                    }
                }

                using (var cmd = new OleDbCommand("UPDATE [Users] SET BusinessName=?, Mobile=?, Email=?, Address=?, ContactPerson=?, ContactPhone=?, City=?, State=?, Pincode=?, DealerType=?, Website=?, TaxType=?, TaxNumberPending=? WHERE UserID=?", conn))
                {
                    cmd.Parameters.AddWithValue("?", businessName ?? "");
                    cmd.Parameters.AddWithValue("?", mobile ?? "");
                    cmd.Parameters.AddWithValue("?", email ?? "");
                    cmd.Parameters.AddWithValue("?", address ?? "");
                    cmd.Parameters.AddWithValue("?", contactPerson ?? "");
                    cmd.Parameters.AddWithValue("?", contactPhone ?? "");
                    cmd.Parameters.AddWithValue("?", city ?? "");
                    cmd.Parameters.AddWithValue("?", state ?? "");
                    cmd.Parameters.AddWithValue("?", pincode ?? "");
                    cmd.Parameters.AddWithValue("?", dealerType ?? "");
                    cmd.Parameters.AddWithValue("?", website ?? "");
                    cmd.Parameters.AddWithValue("?", taxType ?? "");
                    cmd.Parameters.AddWithValue("?", taxPending);
                    cmd.Parameters.AddWithValue("?", uid);
                    cmd.ExecuteNonQuery();
                }
            }
            JsonOK(ctx, new { success = true, taxPending = !string.IsNullOrEmpty(taxPending) });
        }

        private void HandlePendingTaxList(HttpContext ctx)
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("SELECT UserID, Username, BusinessName, TaxNumber, TaxNumberPending FROM [Users] WHERE TaxNumberPending IS NOT NULL AND TaxNumberPending<>''", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        list.Add(new Dictionary<string, object> {
                            { "userID", Convert.ToInt32(r["UserID"]) },
                            { "username", (r["Username"] ?? "").ToString() },
                            { "businessName", (r["BusinessName"] ?? "").ToString() },
                            { "taxNumber", (r["TaxNumber"] ?? "").ToString() },
                            { "taxNumberPending", (r["TaxNumberPending"] ?? "").ToString() }
                        });
                    }
            }
            JsonOK(ctx, list);
        }

        private void HandleApproveTax(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            int uid = data != null && data.ContainsKey("userID") ? Convert.ToInt32(data["userID"]) : 0;
            if (uid <= 0) { JsonError(ctx, "Invalid user"); return; }

            using (var conn = GetConn())
            {
                conn.Open();
                using (var cmd = new OleDbCommand("UPDATE [Users] SET TaxNumber=TaxNumberPending, TaxNumberPending=NULL WHERE UserID=? AND TaxNumberPending IS NOT NULL AND TaxNumberPending<>''", conn))
                {
                    cmd.Parameters.AddWithValue("?", uid);
                    int n = cmd.ExecuteNonQuery();
                    if (n > 0) JsonOK(ctx, new { success = true });
                    else JsonError(ctx, "No pending tax number to approve");
                }
            }
        }

        private void HandleDownloadTemplate(HttpContext ctx)
        {
            byte[] xlsxBytes = BuildTemplateXlsxFromDB();
            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ctx.Response.AddHeader("Content-Disposition", "attachment; filename=Requirements_Template.xlsx");
            ctx.Response.BinaryWrite(xlsxBytes);
        }

        private string BuildSheetXmlWithData(string sheetName, string title, string[] headers, List<string[]> exampleRow, List<string[]> dataRows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            sb.Append("<row r=\"1\"><c r=\"A1\" t=\"inlineStr\" s=\"2\"><is><t>").Append(title).Append("</t></is></c></row>");

            int r = 3;
            sb.Append("<row r=\"").Append(r).Append("\">");
            for (int c = 0; c < headers.Length; c++)
            {
                string col = GetColLetter(c);
                sb.Append("<c r=\"").Append(col).Append(r).Append("\" t=\"inlineStr\" s=\"7\"><is><t>").Append(headers[c]).Append("</t></is></c>");
            }
            sb.Append("</row>");
            r++;

            if (exampleRow != null)
            {
                foreach (var row in exampleRow)
                {
                    sb.Append("<row r=\"").Append(r).Append("\">");
                    for (int c = 0; c < row.Length; c++)
                    {
                        string col = GetColLetter(c);
                        sb.Append("<c r=\"").Append(col).Append(r).Append("\" t=\"inlineStr\" s=\"4\"><is><t>").Append(row[c] ?? "").Append("</t></is></c>");
                    }
                    sb.Append("</row>");
                    r++;
                }
            }

            r++;

            if (dataRows != null)
            {
                foreach (var row in dataRows)
                {
                    sb.Append("<row r=\"").Append(r).Append("\">");
                    for (int c = 0; c < row.Length; c++)
                    {
                        string col = GetColLetter(c);
                        sb.Append("<c r=\"").Append(col).Append(r).Append("\" t=\"inlineStr\" s=\"6\"><is><t>").Append(row[c] ?? "").Append("</t></is></c>");
                    }
                    sb.Append("</row>");
                    r++;
                }
            }

            for (int i = 0; i < 10; i++)
                sb.Append("<row></row>");

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string GetColLetter(int col)
        {
            if (col < 26) return ((char)('A' + col)).ToString();
            return ((char)('A' + col / 26 - 1)).ToString() + ((char)('A' + col % 26)).ToString();
        }

        private byte[] BuildTemplateXlsxFromDB()
        {
            var cabinets = new List<string[]>();
            var hardware = new List<string[]>();
            var boards = new List<string[]>();
            var laminates = new List<string[]>();

            using (var conn = GetConn())
            {
                conn.Open();

                using (var cmd = new OleDbCommand("SELECT ModelCode, Name, Width, Depth, Height FROM [Cabinets] ORDER BY ModelCode", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        cabinets.Add(new string[] {
                            (r["ModelCode"] ?? "").ToString(),
                            (r["Name"] ?? "").ToString(),
                            (r["Width"] ?? "").ToString(),
                            (r["Depth"] ?? "").ToString(),
                            (r["Height"] ?? "").ToString()
                        });

                using (var cmd = new OleDbCommand("SELECT HardwareName, UnitPrice, UOM FROM [HardwareItems] ORDER BY HardwareName", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        hardware.Add(new string[] {
                            (r["HardwareName"] ?? "").ToString(),
                            (r["UnitPrice"] ?? "").ToString(),
                            (r["UOM"] ?? "").ToString()
                        });

                using (var cmd = new OleDbCommand("SELECT m.Name, bt.ThicknessValue FROM [CorePricing] cp INNER JOIN [Materials] m ON cp.MaterialID=m.MaterialID INNER JOIN [BoardThickness] bt ON cp.ThicknessID=bt.ThicknessID WHERE m.Name IN ('MR Ply','BWP Ply','MDF','HDF') ORDER BY m.Name, bt.ThicknessValue", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        boards.Add(new string[] {
                            (r["Name"] ?? "").ToString(),
                            (r["ThicknessValue"] ?? "").ToString()
                        });

                using (var cmd = new OleDbCommand("SELECT Name, Brand FROM [Laminates] ORDER BY Name", conn))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        laminates.Add(new string[] {
                            (r["Name"] ?? "").ToString(),
                            (r["Brand"] ?? "").ToString()
                        });
            }

            string[] cabHeaders = { "ModelCode", "Width(mm)", "Depth(mm)", "Height(mm)", "Material", "Thickness(mm)", "Colour", "Qty" };
            string[] hwHeaders = { "ItemName", "Qty" };
            string[] brdHeaders = { "Material", "Thickness(mm)", "Qty" };
            string[] lamHeaders = { "ProductName", "Qty" };

            var cabExample = new List<string[]> { new string[] { "BC-01", "800", "600", "720", "BWP Ply", "18", "White", "1" } };
            var hwExample = new List<string[]> { new string[] { "Hinge - Soft Close", "10" } };
            var brdExample = new List<string[]> { new string[] { "MR Ply", "18", "5" } };
            var lamExample = new List<string[]> { new string[] { "Arctic White", "3" } };

            var sheets = new string[4];
            sheets[0] = BuildSheetXmlWithData("Cabinets", "CABINETS - Available Items (copy ModelCode from below)", cabHeaders, cabExample, cabinets);
            sheets[1] = BuildSheetXmlWithData("Hardware", "HARDWARE - Available Items (copy ItemName from below)", hwHeaders, hwExample, hardware);
            sheets[2] = BuildSheetXmlWithData("Boards", "BOARDS - Available Materials & Thicknesses", brdHeaders, brdExample, boards);
            sheets[3] = BuildSheetXmlWithData("Laminates", "LAMINATES - Available Products (copy ProductName from below)", lamHeaders, lamExample, laminates);

            string[] sheetNames = { "Cabinets", "Hardware", "Boards", "Laminates" };
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var sbCT = new StringBuilder();
                    sbCT.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                    sbCT.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
                    sbCT.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
                    sbCT.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
                    sbCT.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
                    for (int i = 0; i < 4; i++)
                        sbCT.Append("<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                    sbCT.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                    sbCT.Append("</Types>");
                    using (var w = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open(), Encoding.UTF8))
                        w.Write(sbCT.ToString());

                    using (var w = new StreamWriter(archive.CreateEntry("_rels/.rels").Open(), Encoding.UTF8))
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

                    var wbSb = new StringBuilder();
                    wbSb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                    wbSb.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                    for (int i = 0; i < 4; i++)
                        wbSb.Append("<sheet name=\"").Append(sheetNames[i]).Append("\" sheetId=\"").Append(i + 1).Append("\" r:id=\"rId").Append(i == 0 ? "1" : (i + 2).ToString()).Append("\"/>");
                    wbSb.Append("</sheets></workbook>");
                    using (var w = new StreamWriter(archive.CreateEntry("xl/workbook.xml").Open(), Encoding.UTF8))
                        w.Write(wbSb.ToString());

                    var relsSb = new StringBuilder();
                    relsSb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                    relsSb.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                    relsSb.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>");
                    relsSb.Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
                    relsSb.Append("<Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/>");
                    relsSb.Append("<Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/>");
                    relsSb.Append("<Relationship Id=\"rId5\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet4.xml\"/>");
                    relsSb.Append("</Relationships>");
                    using (var w = new StreamWriter(archive.CreateEntry("xl/_rels/workbook.xml.rels").Open(), Encoding.UTF8))
                        w.Write(relsSb.ToString());

                    for (int i = 0; i < 4; i++)
                        using (var w = new StreamWriter(archive.CreateEntry("xl/worksheets/sheet" + (i + 1) + ".xml").Open(), Encoding.UTF8))
                            w.Write(sheets[i]);

                    using (var w = new StreamWriter(archive.CreateEntry("xl/styles.xml").Open(), Encoding.UTF8))
                    {
                        w.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                        w.Write("<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                        w.Write("<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"14\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font><font><i/><sz val=\"10\"/><color rgb=\"FF888888\"/><name val=\"Calibri\"/></font></fonts>");
                        w.Write("<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF3A3A2E\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8F0E0\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF5F5F5\"/></patternFill></fill></fills>");
                        w.Write("<borders count=\"2\"><border><left/><right/><top/><bottom/></border><border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom></border></borders>");
                        w.Write("<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>");
                        w.Write("<cellXfs count=\"8\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"4\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"1\" applyFill=\"1\" applyBorder=\"1\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/></cellXfs>");
                        w.Write("</styleSheet>");
                    }
                }
                return ms.ToArray();
            }
        }

        private void HandleParseUploadExcel(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string fileBase64 = data != null && data.ContainsKey("file") ? (data["file"] != null ? data["file"].ToString() : "") : "";

            if (string.IsNullOrEmpty(fileBase64))
            {
                JsonError(ctx, "No file data received");
                return;
            }

            string tempFile = "";
            try
            {
                byte[] fileBytes = Convert.FromBase64String(fileBase64);
                tempFile = Path.GetTempFileName() + ".xlsx";
                System.IO.File.WriteAllBytes(tempFile, fileBytes);

                if (!System.IO.File.Exists(tempFile))
                {
                    JsonError(ctx, "Could not save uploaded file");
                    return;
                }

                string excelConn = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + tempFile + ";Extended Properties=\"Excel 12.0;HDR=YES;IMEX=1\"";
                var result = new Dictionary<string, object>();
                var allItems = new List<Dictionary<string, object>>();

                using (var econn = new OleDbConnection(excelConn))
                {
                    econn.Open();

                    // Get sheet names
                    var sheets = new List<string>();
                    var schemaTable = econn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });
                    if (schemaTable != null)
                    {
                        foreach (System.Data.DataRow row in schemaTable.Rows)
                        {
                            string sheetName = (row["TABLE_NAME"] != null ? row["TABLE_NAME"].ToString() : "").TrimEnd('$');
                            if (!string.IsNullOrEmpty(sheetName) && !sheetName.EndsWith("_"))
                                sheets.Add(sheetName);
                        }
                    }

                    // Parse Cabinets sheet
                    if (sheets.Exists(s => s.IndexOf("Cabinets", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string sheet = sheets.Find(s => s.IndexOf("Cabinets", StringComparison.OrdinalIgnoreCase) >= 0);
                        try
                        {
                            using (var cmd = new OleDbCommand("SELECT * FROM [" + sheet + "$]", econn))
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    string modelCode = (r["ModelCode"] ?? "").ToString().Trim();
                                    string wStr = (r["Width(mm)"] ?? "").ToString().Trim();
                                    string dStr = (r["Depth(mm)"] ?? "").ToString().Trim();
                                    string hStr = (r["Height(mm)"] ?? "").ToString().Trim();
                                    string matName = (r["Material"] ?? "").ToString().Trim();
                                    string thickStr = (r["Thickness(mm)"] ?? "").ToString().Trim();
                                    string colourName = (r["Colour"] ?? "").ToString().Trim();
                                    string qtyStr = (r["Qty"] ?? "").ToString().Trim();

                                    if (string.IsNullOrEmpty(modelCode) && string.IsNullOrEmpty(matName)) continue;

                                    var item = new Dictionary<string, object>();
                                    item["sheet"] = "Cabinets";
                                    var errors = new List<string>();
                                    double W = 0, D = 0, H = 0;
                                    int qty = 1;
                                    int cabID = 0, matID = 0, thickID = 0, colID = 0;

                                    if (string.IsNullOrEmpty(modelCode)) errors.Add("Row " + r + ": ModelCode is required");
                                    else
                                    {
                                        using (var cc = new OleDbCommand("SELECT CabinetID FROM [Cabinets] WHERE ModelCode=?", GetConn()))
                                        {
                                            cc.Parameters.AddWithValue("?", modelCode);
                                            var v = cc.ExecuteScalar();
                                            if (v == null) errors.Add("Row " + r + ": ModelCode '" + modelCode + "' not found in database");
                                            else cabID = Convert.ToInt32(v);
                                        }
                                    }

                                    if (!double.TryParse(wStr, out W) || W <= 0) errors.Add("Row " + r + ": Width '" + wStr + "' is invalid — must be a positive number in mm");
                                    if (!double.TryParse(dStr, out D) || D <= 0) errors.Add("Row " + r + ": Depth '" + dStr + "' is invalid — must be a positive number in mm");
                                    if (!double.TryParse(hStr, out H) || H <= 0) errors.Add("Row " + r + ": Height '" + hStr + "' is invalid — must be a positive number in mm");
                                    if (!string.IsNullOrEmpty(qtyStr)) int.TryParse(qtyStr, out qty);
                                    if (qty < 1) qty = 1;

                                    if (string.IsNullOrEmpty(matName)) errors.Add("Row " + r + ": Material is required");
                                    else
                                    {
                                        using (var mc = new OleDbCommand("SELECT MaterialID FROM [Materials] WHERE Name=?", GetConn()))
                                        {
                                            mc.Parameters.AddWithValue("?", matName);
                                            var v = mc.ExecuteScalar();
                                            if (v == null) errors.Add("Row " + r + ": Material '" + matName + "' not found — available: MR Ply, BWP Ply, MDF, HDF, Stainless Steel, Solid Wood");
                                            else matID = Convert.ToInt32(v);
                                        }
                                    }

                                    if (string.IsNullOrEmpty(thickStr)) errors.Add("Row " + r + ": Thickness is required");
                                    else
                                    {
                                        double tv;
                                        if (double.TryParse(thickStr, out tv))
                                        {
                                            using (var tc = new OleDbCommand("SELECT ThicknessID FROM [BoardThickness] WHERE ThicknessValue=?", GetConn()))
                                            {
                                                tc.Parameters.AddWithValue("?", tv);
                                                var v = tc.ExecuteScalar();
                                                if (v == null) errors.Add("Row " + r + ": Thickness '" + thickStr + "'mm not found — available: 4, 6, 12, 16, 18, 25, 32");
                                                else thickID = Convert.ToInt32(v);
                                            }
                                        }
                                        else errors.Add("Row " + r + ": Thickness '" + thickStr + "' is not a valid number");
                                    }

                                    if (string.IsNullOrEmpty(colourName)) errors.Add("Row " + r + ": Colour is required");
                                    else
                                    {
                                        using (var colc = new OleDbCommand("SELECT ColourID FROM [Colours] WHERE ColourName=?", GetConn()))
                                        {
                                            colc.Parameters.AddWithValue("?", colourName);
                                            var v = colc.ExecuteScalar();
                                            if (v == null) errors.Add("Row " + r + ": Colour '" + colourName + "' not found in database");
                                            else colID = Convert.ToInt32(v);
                                        }
                                    }

                                    item["rowData"] = new Dictionary<string, object> {
                                        { "modelCode", modelCode }, { "cabinetID", cabID },
                                        { "width", W }, { "depth", D }, { "height", H },
                                        { "materialID", matID }, { "material", matName },
                                        { "thicknessID", thickID }, { "thickness", thickStr },
                                        { "colourID", colID }, { "colour", colourName },
                                        { "quantity", qty }
                                    };
                                    item["valid"] = errors.Count == 0;
                                    item["errors"] = errors;
                                    allItems.Add(item);
                                }
                            }
                        }
                        catch (Exception ex) { allItems.Add(new Dictionary<string, object> { { "sheet", "Cabinets" }, { "valid", false }, { "errors", new List<string> { "Error reading sheet: " + ex.Message } }, { "rowData", new Dictionary<string, object>() } }); }
                    }

                    // Parse Hardware sheet
                    if (sheets.Exists(s => s.IndexOf("Hardware", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string sheet = sheets.Find(s => s.IndexOf("Hardware", StringComparison.OrdinalIgnoreCase) >= 0);
                        try
                        {
                            using (var cmd = new OleDbCommand("SELECT * FROM [" + sheet + "$]", econn))
                            using (var r = cmd.ExecuteReader())
                            {
                                int rowNum = 2;
                                while (r.Read())
                                {
                                    rowNum++;
                                    string itemName = (r["ItemName"] ?? "").ToString().Trim();
                                    string qtyStr = (r["Qty"] ?? "").ToString().Trim();
                                    if (string.IsNullOrEmpty(itemName)) continue;

                                    var item = new Dictionary<string, object>();
                                    item["sheet"] = "Hardware";
                                    var errors = new List<string>();
                                    int qty = 1, hwID = 0;
                                    if (!string.IsNullOrEmpty(qtyStr)) int.TryParse(qtyStr, out qty);
                                    if (qty < 1) qty = 1;

                                    if (string.IsNullOrEmpty(itemName)) errors.Add("Row " + rowNum + ": ItemName is required");
                                    else
                                    {
                                        using (var hc = new OleDbCommand("SELECT HardwareID FROM [HardwareItems] WHERE HardwareName=?", GetConn()))
                                        {
                                            hc.Parameters.AddWithValue("?", itemName);
                                            var v = hc.ExecuteScalar();
                                            if (v == null) errors.Add("Row " + rowNum + ": Hardware '" + itemName + "' not found — check the Reference section for available items");
                                            else hwID = Convert.ToInt32(v);
                                        }
                                    }

                                    item["rowData"] = new Dictionary<string, object> {
                                        { "productID", hwID }, { "name", itemName }, { "quantity", qty }
                                    };
                                    item["valid"] = errors.Count == 0;
                                    item["errors"] = errors;
                                    allItems.Add(item);
                                }
                            }
                        }
                        catch (Exception ex) { allItems.Add(new Dictionary<string, object> { { "sheet", "Hardware" }, { "valid", false }, { "errors", new List<string> { "Error reading sheet: " + ex.Message } }, { "rowData", new Dictionary<string, object>() } }); }
                    }

                    // Parse Boards sheet
                    if (sheets.Exists(s => s.IndexOf("Boards", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string sheet = sheets.Find(s => s.IndexOf("Boards", StringComparison.OrdinalIgnoreCase) >= 0);
                        try
                        {
                            using (var cmd = new OleDbCommand("SELECT * FROM [" + sheet + "$]", econn))
                            using (var r = cmd.ExecuteReader())
                            {
                                int rowNum = 2;
                                while (r.Read())
                                {
                                    rowNum++;
                                    string matName = (r["Material"] ?? "").ToString().Trim();
                                    string thickStr = (r["Thickness(mm)"] ?? "").ToString().Trim();
                                    string qtyStr = (r["Qty"] ?? "").ToString().Trim();
                                    if (string.IsNullOrEmpty(matName)) continue;

                                    var item = new Dictionary<string, object>();
                                    item["sheet"] = "Boards";
                                    var errors = new List<string>();
                                    int qty = 1, matID = 0, thickID = 0;
                                    if (!string.IsNullOrEmpty(qtyStr)) int.TryParse(qtyStr, out qty);
                                    if (qty < 1) qty = 1;

                                    using (var mc = new OleDbCommand("SELECT MaterialID FROM [Materials] WHERE Name=?", GetConn()))
                                    {
                                        mc.Parameters.AddWithValue("?", matName);
                                        var v = mc.ExecuteScalar();
                                        if (v == null) errors.Add("Row " + rowNum + ": Material '" + matName + "' not found — available: MR Ply, BWP Ply, MDF, HDF");
                                        else matID = Convert.ToInt32(v);
                                    }

                                    if (string.IsNullOrEmpty(thickStr)) errors.Add("Row " + rowNum + ": Thickness is required");
                                    else
                                    {
                                        double tv;
                                        if (double.TryParse(thickStr, out tv))
                                        {
                                            using (var tc = new OleDbCommand("SELECT ThicknessID FROM [BoardThickness] WHERE ThicknessValue=?", GetConn()))
                                            {
                                                tc.Parameters.AddWithValue("?", tv);
                                                var v = tc.ExecuteScalar();
                                                if (v == null) errors.Add("Row " + rowNum + ": Thickness '" + thickStr + "'mm not found — available: 4, 6, 12, 16, 18, 25, 32");
                                                else thickID = Convert.ToInt32(v);
                                            }
                                        }
                                        else errors.Add("Row " + rowNum + ": Thickness '" + thickStr + "' is not a valid number");
                                    }

                                    item["rowData"] = new Dictionary<string, object> {
                                        { "materialID", matID }, { "material", matName },
                                        { "thicknessID", thickID }, { "thickness", thickStr },
                                        { "quantity", qty }
                                    };
                                    item["valid"] = errors.Count == 0;
                                    item["errors"] = errors;
                                    allItems.Add(item);
                                }
                            }
                        }
                        catch (Exception ex) { allItems.Add(new Dictionary<string, object> { { "sheet", "Boards" }, { "valid", false }, { "errors", new List<string> { "Error reading sheet: " + ex.Message } }, { "rowData", new Dictionary<string, object>() } }); }
                    }

                    // Parse Laminates sheet
                    if (sheets.Exists(s => s.IndexOf("Laminates", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        string sheet = sheets.Find(s => s.IndexOf("Laminates", StringComparison.OrdinalIgnoreCase) >= 0);
                        try
                        {
                            using (var cmd = new OleDbCommand("SELECT * FROM [" + sheet + "$]", econn))
                            using (var r = cmd.ExecuteReader())
                            {
                                int rowNum = 2;
                                while (r.Read())
                                {
                                    rowNum++;
                                    string prodName = (r["ProductName"] ?? "").ToString().Trim();
                                    string qtyStr = (r["Qty"] ?? "").ToString().Trim();
                                    if (string.IsNullOrEmpty(prodName)) continue;

                                    var item = new Dictionary<string, object>();
                                    item["sheet"] = "Laminates";
                                    var errors = new List<string>();
                                    int qty = 1, lamID = 0;
                                    if (!string.IsNullOrEmpty(qtyStr)) int.TryParse(qtyStr, out qty);
                                    if (qty < 1) qty = 1;

                                    using (var lc = new OleDbCommand("SELECT LaminateID FROM [Laminates] WHERE Name=?", GetConn()))
                                    {
                                        lc.Parameters.AddWithValue("?", prodName);
                                        var v = lc.ExecuteScalar();
                                        if (v == null) errors.Add("Row " + rowNum + ": Laminates '" + prodName + "' not found — check the Reference section for available products");
                                        else lamID = Convert.ToInt32(v);
                                    }

                                    item["rowData"] = new Dictionary<string, object> {
                                        { "laminateID", lamID }, { "name", prodName }, { "quantity", qty }
                                    };
                                    item["valid"] = errors.Count == 0;
                                    item["errors"] = errors;
                                    allItems.Add(item);
                                }
                            }
                        }
                        catch (Exception ex) { allItems.Add(new Dictionary<string, object> { { "sheet", "Laminates" }, { "valid", false }, { "errors", new List<string> { "Error reading sheet: " + ex.Message } }, { "rowData", new Dictionary<string, object>() } }); }
                    }
                }

                result["items"] = allItems;
                result["success"] = true;
                JsonOK(ctx, result);
            }
            catch (Exception ex)
            {
                JsonError(ctx, "Error processing file: " + ex.Message);
            }
            finally
            {
                try { if (!string.IsNullOrEmpty(tempFile) && System.IO.File.Exists(tempFile)) System.IO.File.Delete(tempFile); }
                catch { }
            }
        }

        private void HandleQuoteExcel(HttpContext ctx)
        {
            string json = new System.IO.StreamReader(ctx.Request.InputStream).ReadToEnd();
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            var itemsRaw = data != null && data.ContainsKey("items") ? (System.Collections.ArrayList)data["items"] : new System.Collections.ArrayList();
            int userID = data != null && data.ContainsKey("userID") ? Convert.ToInt32(data["userID"]) : 0;
            string username = "Guest";

            string businessName = "", address = "", email = "", mobile = "", contactPerson = "", contactPhone = "", city = "", state = "", pincode = "", taxNumber = "", taxType = "";
            using (var conn = GetConn())
            {
                conn.Open();
                if (userID > 0)
                {
                    using (var uc = new OleDbCommand("SELECT * FROM [Users] WHERE UserID=?", conn))
                    {
                        uc.Parameters.AddWithValue("?", userID);
                        using (var ur = uc.ExecuteReader())
                        {
                            if (ur.Read())
                            {
                                username = (ur["Username"] ?? "").ToString();
                                businessName = (ur["BusinessName"] ?? "").ToString();
                                address = (ur["Address"] ?? "").ToString();
                                email = (ur["Email"] ?? "").ToString();
                                mobile = (ur["Mobile"] ?? "").ToString();
                                contactPerson = ur["ContactPerson"] != DBNull.Value ? ur["ContactPerson"].ToString() : "";
                                contactPhone = ur["ContactPhone"] != DBNull.Value ? ur["ContactPhone"].ToString() : "";
                                city = ur["City"] != DBNull.Value ? ur["City"].ToString() : "";
                                state = ur["State"] != DBNull.Value ? ur["State"].ToString() : "";
                                pincode = ur["Pincode"] != DBNull.Value ? ur["Pincode"].ToString() : "";
                                taxNumber = (ur["TaxNumber"] ?? "").ToString();
                                taxType = (ur["TaxType"] ?? "").ToString();
                            }
                        }
                    }
                }

                var terms = new List<string>();
                using (var tc = new OleDbCommand("SELECT TermText FROM [TermsMaster] WHERE UserID=? OR (UserID=0 AND IsDefault=1) ORDER BY UserID DESC, TermID", conn))
                {
                    tc.Parameters.AddWithValue("?", userID);
                    using (var tr = tc.ExecuteReader())
                        while (tr.Read())
                            terms.Add((tr["TermText"] ?? "").ToString());
                }
                if (terms.Count == 0) terms.Add("Terms and conditions apply.");

                string quoteNo = "Q" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Quotation - Elenza</title><style>");
                sb.Append("@page{margin:15mm 12mm}");
                sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#2d2d2d;font-size:12px;line-height:1.5;margin:0;padding:20px}");
                sb.Append(".header{display:flex;align-items:center;gap:20px;border-bottom:3px solid #A54F6B;padding-bottom:16px;margin-bottom:20px}");
                sb.Append(".logo{font-size:28px;font-weight:700;color:#A54F6B;font-family:Georgia,serif}");
                sb.Append(".logo small{font-size:13px;font-weight:400;color:#888;display:block}");
                sb.Append(".qref{text-align:right;margin-left:auto}");
                sb.Append(".qref h2{margin:0;font-size:18px;color:#A54F6B}");
                sb.Append(".qref p{margin:2px 0;font-size:11px;color:#888}");
                sb.Append(".dealer-box{border:1px solid #ddd;border-radius:8px;padding:14px 16px;margin-bottom:18px;background:#faf8f6}");
                sb.Append(".dealer-box h3{margin:0 0 6px;font-size:14px;color:#A54F6B}");
                sb.Append(".dealer-box p{margin:2px 0;font-size:12px;color:#555}");
                sb.Append("table{width:100%;border-collapse:collapse;margin:14px 0}");
                sb.Append("th{background:#A54F6B;color:#fff;padding:8px 10px;text-align:left;font-size:11px;text-transform:uppercase;letter-spacing:0.08em}");
                sb.Append("td{padding:7px 10px;border-bottom:1px solid #e0ddd8;font-size:12px}");
                sb.Append("tr:nth-child(even) td{background:#f8f6f4}");
                sb.Append("tr:last-child td{border-bottom:2px solid #A54F6B}");
                sb.Append(".num{text-align:right}");
                sb.Append(".summary{width:320px;margin-left:auto;margin-top:8px}");
                sb.Append(".summary-row{display:flex;justify-content:space-between;padding:4px 0;font-size:12px}");
                sb.Append(".summary-row.total{font-size:15px;font-weight:700;color:#A54F6B;border-top:2px solid #A54F6B;padding-top:8px;margin-top:4px}");
                sb.Append(".terms{margin-top:24px;padding-top:14px;border-top:1px solid #ddd}");
                sb.Append(".terms h4{margin:0 0 8px;font-size:13px;color:#A54F6B}");
                sb.Append(".terms ul{margin:0;padding-left:18px}");
                sb.Append(".terms li{margin:3px 0;font-size:11px;color:#666}");
                sb.Append(".footer{text-align:center;margin-top:28px;font-size:10px;color:#aaa;border-top:1px solid #eee;padding-top:12px}");
                sb.Append(".no-print{text-align:center;margin-bottom:12px}");
                sb.Append(".no-print button{background:#A54F6B;color:#fff;border:none;padding:10px 24px;font-size:14px;cursor:pointer;border-radius:6px;margin:0 6px}");
                sb.Append("@media print{.no-print{display:none}}");
                sb.Append("</style></head><body>");
                sb.Append("<div class='no-print'><button onclick='window.print()'>Print / Save as PDF</button><button onclick=\"window.location.href='data:text/html,'+encodeURIComponent(document.body.innerHTML)\">Save as HTML</button></div>");
                sb.Append("<div class='header'>");
                sb.Append("<div class='logo'>ELENZA<small>Modular Furniture Solutions</small></div>");
                sb.Append("<div class='qref'><h2>QUOTATION</h2><p># ").Append(XmlSafe(quoteNo)).Append("</p><p>Date: ").Append(DateTime.Now.ToString("dd-MMM-yyyy")).Append("</p></div>");
                sb.Append("</div>");
                sb.Append("<div class='dealer-box'><h3>").Append(XmlSafe(string.IsNullOrEmpty(businessName) ? username : businessName)).Append("</h3>");
                sb.Append("<p>").Append(XmlSafe(address)).Append("</p>");
                if (!string.IsNullOrEmpty(city) || !string.IsNullOrEmpty(state))
                    sb.Append("<p>").Append(XmlSafe(city)).Append(!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(state) ? ", " : "").Append(XmlSafe(state)).Append(" - ").Append(XmlSafe(pincode)).Append("</p>");
                if (!string.IsNullOrEmpty(contactPerson))
                    sb.Append("<p>Attn: ").Append(XmlSafe(contactPerson)).Append(!string.IsNullOrEmpty(contactPhone) ? " | " + XmlSafe(contactPhone) : "").Append("</p>");
                sb.Append("<p>Email: ").Append(XmlSafe(email)).Append(" | Mobile: ").Append(XmlSafe(mobile)).Append("</p>");
                if (!string.IsNullOrEmpty(taxNumber))
                    sb.Append("<p>").Append(XmlSafe(taxType)).Append(": ").Append(XmlSafe(taxNumber)).Append("</p>");
                sb.Append("</div>");
                sb.Append("<table><thead><tr><th style='width:32px'>#</th><th>Item Description</th><th style='width:70px'>Qty</th><th style='width:90px' class='num'>Unit Price</th><th style='width:100px' class='num'>Amount</th></tr></thead><tbody>");

                double grandTotal = 0;
                int idx = 0;
                foreach (Dictionary<string, object> raw in itemsRaw)
                {
                    idx++;
                    string itemType = raw.ContainsKey("type") ? ((raw["type"] ?? "cabinet").ToString()) : "cabinet";
                    int qty = raw.ContainsKey("quantity") ? Convert.ToInt32(raw["quantity"]) : 1;
                    if (qty < 1) qty = 1;

                    if (itemType != "cabinet")
                    {
                        string itemName = raw.ContainsKey("name") ? ((raw["name"] ?? "Item").ToString()) : "Item";
                        double ncUnitPrice = raw.ContainsKey("unitPrice") ? Convert.ToDouble(raw["unitPrice"]) : 0;
                        double ncLineTotal = Math.Round(ncUnitPrice * qty, 2);
                        grandTotal += ncLineTotal;
                        string typeLabel = itemType == "hardware" ? "Hardware" : itemType == "board" ? "Board" : "Laminate";
                        sb.Append("<tr><td>").Append(idx).Append("</td><td><strong>[").Append(typeLabel).Append("]</strong> ").Append(XmlSafe(itemName)).Append("</td><td class='num'>").Append(qty).Append("</td><td class='num'>₹ ").Append(ncUnitPrice.ToString("N2")).Append("</td><td class='num'>₹ ").Append(ncLineTotal.ToString("N2")).Append("</td></tr>");
                        continue;
                    }

                    int cabId = raw.ContainsKey("cabinetID") ? Convert.ToInt32(raw["cabinetID"]) : 0;
                    double W = raw.ContainsKey("width") ? Convert.ToDouble(raw["width"]) : 0;
                    double D = raw.ContainsKey("depth") ? Convert.ToDouble(raw["depth"]) : 0;
                    double H = raw.ContainsKey("height") ? Convert.ToDouble(raw["height"]) : 0;
                    int matId = raw.ContainsKey("materialID") ? Convert.ToInt32(raw["materialID"]) : 1;
                    int thickId = raw.ContainsKey("thicknessID") ? Convert.ToInt32(raw["thicknessID"]) : 2;
                    int colId = raw.ContainsKey("colourID") ? Convert.ToInt32(raw["colourID"]) : 1;
                    string modelName = raw.ContainsKey("modelName") ? ((raw["modelName"] ?? "Cabinet").ToString()) : "Cabinet";

                    string matName = "", colName = "", thickVal = "";
                    using (var sc = new OleDbCommand("SELECT Name FROM Materials WHERE MaterialID=?", conn))
                    { sc.Parameters.AddWithValue("?", matId); var v = sc.ExecuteScalar(); matName = v != null ? v.ToString() : ""; }
                    using (var sc = new OleDbCommand("SELECT ColourName FROM Colours WHERE ColourID=?", conn))
                    { sc.Parameters.AddWithValue("?", colId); var v = sc.ExecuteScalar(); colName = v != null ? v.ToString() : ""; }
                    using (var sc = new OleDbCommand("SELECT ThicknessValue FROM BoardThickness WHERE ThicknessID=?", conn))
                    { sc.Parameters.AddWithValue("?", thickId); var v = sc.ExecuteScalar(); thickVal = v != null ? v.ToString() + "mm" : ""; }

                    double unitPrice = 0;
                using (var pc = new OleDbCommand("SELECT TOP 1 Total FROM CorePricing WHERE MaterialID=? AND ThicknessID=? AND ColourID=?", conn))
                {
                    pc.Parameters.AddWithValue("?", matId);
                        pc.Parameters.AddWithValue("?", thickId);
                        pc.Parameters.AddWithValue("?", colId);
                        var pv = pc.ExecuteScalar();
                        if (pv != null) unitPrice = Convert.ToDouble(pv);
                    }

                    double lineTotal = Math.Round(unitPrice * qty, 2);
                    grandTotal += lineTotal;
                    sb.Append("<tr><td>").Append(idx).Append("</td><td><strong>").Append(XmlSafe(modelName)).Append("</strong><br><span style='font-size:11px;color:#888'>").Append(W.ToString("0")).Append("x").Append(D.ToString("0")).Append("x").Append(H.ToString("0")).Append(" mm | ").Append(XmlSafe(matName)).Append(" | ").Append(XmlSafe(colName)).Append(" | ").Append(thickVal).Append("</span></td><td class='num'>").Append(qty).Append("</td><td class='num'>₹ ").Append(unitPrice.ToString("N2")).Append("</td><td class='num'>₹ ").Append(lineTotal.ToString("N2")).Append("</td></tr>");
                }

                sb.Append("</tbody></table>");
                double gstRate = 18;
                double gstAmount = Math.Round(grandTotal * gstRate / 100, 2);
                double totalWithGst = grandTotal + gstAmount;
                sb.Append("<div class='summary'>");
                sb.Append("<div class='summary-row'><span>Subtotal</span><span>₹ ").Append(grandTotal.ToString("N2")).Append("</span></div>");
                sb.Append("<div class='summary-row'>GST @ ").Append(gstRate).Append("%<span>₹ ").Append(gstAmount.ToString("N2")).Append("</span></div>");
                sb.Append("<div class='summary-row total'><span>Grand Total</span><span>₹ ").Append(totalWithGst.ToString("N2")).Append("</span></div>");
                sb.Append("</div>");
                sb.Append("<div class='terms'><h4>Terms & Conditions</h4><ul>");
                foreach (var t in terms)
                    sb.Append("<li>").Append(XmlSafe(t)).Append("</li>");
                sb.Append("</ul></div>");
                sb.Append("<div class='terms' style='margin-top:16px'><h4>Production Process</h4><ul>");
                sb.Append("<li><strong>Order Received</strong> — Order is acknowledged and logged into production queue.</li>");
                sb.Append("<li><strong>In Production</strong> — Manufacturing initiated. Panels cut, edged, and drilled as per design.</li>");
                sb.Append("<li><strong>Quality Check (QC)</strong> — Each panel inspected for dimensions, finish, and hardware alignment.</li>");
                sb.Append("<li><strong>Ready for Dispatch</strong> — Order is packed and ready for shipment.</li>");
                sb.Append("<li><strong>Shipped</strong> — Dispatched to the delivery address.</li>");
                sb.Append("<li><strong>Delivered</strong> — Order delivered and installation completed.</li>");
                sb.Append("</ul></div>");
                sb.Append("<div class='footer'>ELENZA · Modular Furniture Solutions · Email: praveenk252@gmail.com · Phone: +91-XXXXXXXXXX<br>This is a computer-generated quotation. Signature not required.</div>");
                sb.Append("</body></html>");

                byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                ctx.Response.ContentType = "application/vnd.ms-excel";
                ctx.Response.AddHeader("Content-Disposition", "attachment; filename=Quotation_" + quoteNo + ".xls");
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
        }

        // --- Helpers ---
        private OleDbConnection GetConn()
        {
            return new OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + DbPath + ";");
        }

        private double GetPricePerSFT(OleDbConnection conn, int matId, int thickId, int colId)
        {
            var cmd = new OleDbCommand("SELECT TOP 1 Total FROM [CorePricing] WHERE MaterialID = ? AND ThicknessID = ? AND ColourID = ?", conn);
            cmd.Parameters.AddWithValue("?", matId);
            cmd.Parameters.AddWithValue("?", thickId);
            cmd.Parameters.AddWithValue("?", colId);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToDouble(result) : 0;
        }

        private Dictionary<string, object> ReadDict(OleDbDataReader rdr, params string[] cols)
        {
            var dict = new Dictionary<string, object>();
            foreach (var col in cols)
            {
                int idx = rdr.GetOrdinal(col);
                dict[col] = rdr.IsDBNull(idx) ? null : rdr.GetValue(idx);
            }
            return dict;
        }

        private string GetPostParam(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
                return data[key].ToString();
            return null;
        }

        private int ParseInt(string val)
        {
            int result;
            int.TryParse(val, out result);
            return result;
        }

        private double ParseDouble(string val)
        {
            double result;
            double.TryParse(val, out result);
            return result;
        }

        private void JsonOK(HttpContext ctx, object data)
        {
            ctx.Response.Write(new JavaScriptSerializer().Serialize(data));
        }

        private void JsonError(HttpContext ctx, string msg)
        {
            ctx.Response.Write(new JavaScriptSerializer().Serialize(new { success = false, message = msg }));
        }

        private void HandlePopulateHwImages(HttpContext ctx)
        {
            var map = new string[][] {
                new[]{"1","images/hardware/ebco-165-hinge.jpg","Hinge-165"},
                new[]{"1","images/hardware/ebco-soft-close-hinge.jpg","SC-Hinge"},
                new[]{"1","images/hardware/hettich-sensys-hinge.jpg","Sensys-Hinge"},
                new[]{"1","images/hardware/hettich-sensys-165-hinge.jpg","Sensys-165"},
                new[]{"1","images/hardware/hettich-intermat-hinge.jpg","Intermat-Hinge"},
                new[]{"1","images/hardware/hettich-veosys-hinge.jpg","Veosys-Hinge"},
                new[]{"1","images/hardware/hettich-onsys-black-hinge.jpg","Onsys-Black"},
                new[]{"6","images/hardware/ebco-ss-hinge.jpg","SS-Hinge"},
                new[]{"6","images/hardware/hettich-ka4532-zinc.jpg","KA4532"},
                new[]{"6","images/hardware/hettich-ka4732-black.jpg","KA4732"},
                new[]{"2","images/hardware/ebco-shutter-handle.jpg","Shutter-Handle"},
                new[]{"2","images/hardware/hettich-edge-profile.jpg","Edge-Profile"},
                new[]{"7","images/hardware/hettich-gola-profile-l.jpg","Gola-L"},
                new[]{"8","images/hardware/hettich-gola-profile-c.jpg","Gola-C"},
                new[]{"8","images/hardware/ebco-gola-c-l.jpg","Gola-CL"},
                new[]{"7","images/hardware/ebco-pan-hanger.jpg","Pan-Hanger"},
                new[]{"3","images/hardware/ebco-tc-regular.jpg","TC-Regular"},
                new[]{"3","images/hardware/ebco-tc-softclose.jpg","TC-SoftClose"},
                new[]{"3","images/hardware/ebco-slim-tandem.jpg","Slim-Tandem"},
                new[]{"9","images/hardware/ebco-hi-slide-50.jpg","Hi-Slide-50"},
                new[]{"9","images/hardware/hettich-quadro-v6.jpg","Quadro-V6"},
                new[]{"9","images/hardware/hettich-innotech-50kg.jpg","Innotech-50kg"},
                new[]{"9","images/hardware/hettich-avantech-slim.jpg","Avantech-Slim"},
                new[]{"4","images/hardware/hettich-shelf-pin.jpg","Shelf-Pin"},
                new[]{"4","images/hardware/hettich-glass-shelf.jpg","Glass-Shelf"},
                new[]{"4","images/hardware/ebco-pin-shelf.jpg","Pin-Shelf"},
                new[]{"4","images/hardware/ebco-cabinet-shelf.jpg","Cabinet-Shelf"},
                new[]{"10","images/hardware/ebco-plastic-wicker.jpg","Plastic-Wicker"},
                new[]{"10","images/hardware/ebco-thali-basket.jpg","Thali-Basket"},
                new[]{"10","images/hardware/hettich-ctm-basket.jpg","CTM-Basket"},
                new[]{"10","images/hardware/hettich-wooden-wicker.jpg","Wooden-Wicker"},
                new[]{"10","images/hardware/hettich-spice-pullout.jpg","Spice-Pullout"},
                new[]{"11","images/hardware/hettich-cargo-cutlery.jpg","Cargo-Cutlery"},
                new[]{"11","images/hardware/hettich-cargo-dish-drainer.jpg","Dish-Drainer"},
                new[]{"11","images/hardware/hettich-cargo-iq300.jpg","Cargo-IQ300"},
                new[]{"11","images/hardware/hettich-orgatray-anthracite.jpg","OrgaTray-Anth"},
                new[]{"11","images/hardware/hettich-orgatray-silver.jpg","OrgaTray-Silver"},
                new[]{"15","images/hardware/hettich-magic-corner.jpg","Magic-Corner"},
                new[]{"15","images/hardware/hettich-duo-pantry.jpg","Duo-Pantry"},
                new[]{"15","images/hardware/ebco-corner-pullout.jpg","Corner-Pullout"},
                new[]{"15","images/hardware/ebco-blind-corner-3d.jpg","Blind-Corner"},
                new[]{"15","images/hardware/hettich-sensys-blind-corner.jpg","Sensys-Blind"}
            };
            var results = new Dictionary<string, object>();
            using (var conn = GetConn())
            {
                conn.Open();
                try { new OleDbCommand("DELETE FROM [HardwareImages]", conn).ExecuteNonQuery(); } catch { }
                int hwCount = 0;
                foreach (var row in map)
                {
                    var cmd = new OleDbCommand("INSERT INTO [HardwareImages] (HardwareID, ImageURL, AltText, SortOrder) VALUES (?, ?, ?, ?)", conn);
                    cmd.Parameters.AddWithValue("?", int.Parse(row[0]));
                    cmd.Parameters.AddWithValue("?", row[1]);
                    cmd.Parameters.AddWithValue("?", row[2]);
                    cmd.Parameters.AddWithValue("?", hwCount);
                    cmd.ExecuteNonQuery();
                    hwCount++;
                }
                results["hardwareImagesInserted"] = hwCount;
            }
            results["success"] = true;
            JsonOK(ctx, results);
        }
    }
}
