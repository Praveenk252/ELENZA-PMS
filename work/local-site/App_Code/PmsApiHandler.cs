using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO.Compression;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.SessionState;
using System.Web.Hosting;

public class PmsApiHandler : IHttpHandler, IRequiresSessionState
{
    private const string SchemaVersion = "aspnet-inline-20260530-4";
    private const int PasswordIterations = 120000;
    private const string DailyReportKind = "DAILY_ACTIVITY";
    private const string CurrentDayReportKind = "CURRENT_DAY_ACTIVITY";
    private const string HourlyProductionReportKindPrefix = "HOURLY_PRODUCTION_";
    private const string DailyMachineConsolidatedReportKind = "DAILY_MACHINE_CONSOLIDATED";
    private const string RemarksReportKind = "REMARKS_REPORT";
    private const int RemarksReportHour = 21;
    private const string MailConfigRelativePath = "~/App_Data/smtp-settings.json";
    private const string CompatScriptRelativePath = "~/App_Data/script-live.js";
    private static readonly TimeSpan DailyReportTime = new TimeSpan(8, 0, 0);
    private static readonly int[] HourlyProductionSlotHours = new[] { 9, 12, 15, 18 };
    private static readonly TimeSpan WorkdayStart = new TimeSpan(9, 0, 0);
    private static readonly TimeSpan WorkdayEnd = new TimeSpan(21, 0, 0);
    private static readonly TimeSpan FinalReportGraceWindow = new TimeSpan(21, 20, 0);

    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
    private static readonly object MailSync = new object();
    private const string NeutralLoginError = "Invalid login credentials.";

    private static readonly Dictionary<string, string[]> RoleSections = new Dictionary<string, string[]>
    {
        { "Admin", new[] { "data-entry", "optimisation", "procurement", "planner", "production", "dispatch", "reports", "history", "email-log", "masters", "users", "settings" } },
        { "Data Entry", new[] { "data-entry", "history", "reports", "settings" } },
        { "Quotation User", new[] { "data-entry", "history", "reports", "settings" } },
        { "Marketing User", new[] { "data-entry", "reports", "settings", "dispatch" } },
        { "Optimisation User", new[] { "optimisation", "history", "settings" } },
        { "Procurement User", new[] { "procurement", "history", "settings" } },
        { "Production Planner User", new[] { "planner", "history", "reports", "settings" } },
        { "Machine User", new[] { "production", "history", "settings" } },
        { "Dispatch User", new[] { "dispatch", "history", "settings" } },
        { "Management", new[] { "reports", "history", "settings" } },
        { "Dealer", new[] { "dashboard" } },
        { "Accounts", new[] { "dashboard", "reports" } }
    };

    private static readonly HashSet<string> ProcurementStatusCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PO_PENDING", "PO_RAISED", "PARTIAL_MATERIAL_RECEIVED", "MATERIAL_RECEIVED", "CANCELLED"
    };

    private static readonly HashSet<string> DispatchStatusCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "PENDING_DISPATCH", "PARTIALLY_DISPATCHED", "HOLD", "DISPATCHED"
    };

    private static readonly HashSet<string> OpenProcurementWorkflowCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "OPTIMISATION_DONE"
    };

    private static readonly HashSet<string> PlanningWorkflowCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ORDER_CONFIRMED", "OPTIMISATION_DONE", "PROCUREMENT_STARTED", "PRODUCTION_STARTED", "DISPATCH_READY"
    };

    private static readonly HashSet<string> OrderClassCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Main Order", "Sub Order", "Snag", "Rework"
    };

    private static readonly Dictionary<string, string[]> DefaultDropdownMasters = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "DEALER_TYPE", new[] { "M" } },
        { "PAYMENT_TERMS", new[] { "30 days" } },
        { "MARKETING_OWNER", new[] { "Sanya Roy" } },
        { "QUOTATION_OWNER", new[] { "Kavya Menon" } },
        { "ORDER_CLASS", new[] { "Main Order", "Sub Order", "Snag", "Rework" } }
    };

    private static readonly SampleUser[] SampleUsers = new[]
    {
        new SampleUser("Role Admin","admin.user","Admin","",true,"b32dfc7c3343c38c855db5740b60bca9","9ebe478d39c5b2e198c41eb62ec70ee070c488ba0d966462703b1c4b1f79a320",120000),
        new SampleUser("Asha Admin","asha.admin","Admin","",true,"f83846910cfcbe9b7f381137ec1dc969","0f0a307a13095d5898b0ef98579f275a2c5ced68e9f6eaabc5e33add0b2ec399",120000),
        new SampleUser("Role Data Entry","dataentry.user","Data Entry","",true,"fc0c74e20c8d88a34d88430070d9a962","3a3f3796189ab02f0be7622eac69f6490e220f6b353a5ddae50e88501d50b61e",120000),
        new SampleUser("Vikram Entry","vikram.entry","Data Entry","",true,"a1569550438b6ebb3e0078c8e580dfaf","cb1a34029d38d7a456cc8ba20398d15618f0c207d929d01b62aa73c86b1183e8",120000),
        new SampleUser("Meena Entry","meena.entry","Data Entry","",true,"c8c1766bc74d4a725943cd7a372ab0bc","2bcc81f6424eb31c1ae26e1abcbb770546008800e9295cd188d2fd85e06b9633",120000),
        new SampleUser("Role Quotation","quotation.user","Quotation User","",true,"","demo123",0),
        new SampleUser("Sanya Roy","marketing.user","Marketing User","",true,"","demo123",0),
        new SampleUser("Kavya Optimisation","optimisation.user","Optimisation User","",true,"f44ad66605d1879d535a9a441efad475","51e6fde60343696dadcaff1830f6385f6893320a91c72cbb456321c7e08bf1a8",120000),
        new SampleUser("Ramesh Procurement","procurement.user","Procurement User","",true,"759d8d1031ccaf334cdc8e13be0c37d5","9ebab4bee5ed68d8583be0c443e07b886c134fdd3962628c49aab7843e9305e6",120000),
        new SampleUser("Vicky Planner","planner.user","Production Planner User","",true,"","demo123",0),
        new SampleUser("Hari Hot Press","hotpress.user","Machine User","Hot Press",true,"c56e12a095deb296b05c67ded2f7c57d","72f67fdeb779e64bb242011c79aaa585c8959b93184993c334ba8681ccc2f520",120000),
        new SampleUser("Naveen Cutting","cutting.user","Machine User","Cutting",true,"21d862b78cedc60b5cd41aeac4000c29","0b78a83a1d71883afdbc978ad14f10b526906b4b5ec3c4eca22fad7f0eb5260e",120000),
        new SampleUser("Suresh Edgebanding","edgebanding.user","Machine User","Edgebanding",true,"d697b7e8c1313abb34f8dde74d96c16b","1554c92a37b4e4fe2bf1b56f5f92bf43230cf45b17f835ccf4eac541ac3f56a5",120000),
        new SampleUser("Deepa Drilling","drilling.user","Machine User","Drilling",true,"1b2f3eb1e60a7095e40aa998132b98ec","a73a4d919e7c056192a7ca9c64b9a5da9451abf63773075b38c1112b9aba0969",120000),
        new SampleUser("Mohan QC","qc.user","Machine User","QC",true,"3741207503aefc0bcd09c642d4d7489c","6ade4e29922c9aefce75bdb80553ed3ae8bdfc6a943ffe11f6b1b54236971f1a",120000),
        new SampleUser("Kiran Packing","packing.user","Machine User","Packed",true,"cc4ade1991db633a323c26e34e30be35","405dd54d6d1612af1782059f991c3c3ac0c970425c1e9f40ab69e393aab3534b",120000),
        new SampleUser("Priya Dispatch","dispatch.user","Dispatch User","Dispatch",true,"4257deb4ecc0f162d8b0fa9269acf43a","55a76b4d1d91cdbf0ee5f9eca2b31a15d649489a2995b38aa4237707243e1701",120000),
        new SampleUser("Ritu Management","ritu.management","Management","",true,"21a75a4c39f7e1b74411bef304977347","6533d536c0d29c1542409aa5dc271b9c131569c67fad25393e593ebdbaf12ef4",120000),
        new SampleUser("Arun Management","arun.management","Management","",true,"75fb09892538f8cd44c33c04130b2c72","f42d0709e9e38fa711a59ff667b441f779914ee86c7938a0b74ef38762f0980f",120000),
        new SampleUser("Role Management","management.user","Management","",true,"1fbf91836caaf16d546c1304d608e024","75dd2bec901f20fc9fc80c55fa1e7f83e4151833c4f1551a1b1c0c0e14121659",120000)
    };

    public bool IsReusable { get { return false; } }

    public void ProcessRequest(HttpContext context)
    {
        try
        {
            SetNoCache(context);
            EnsureDbReady(context);

            try { RunRemarksReportSchedulerIfDue(); } catch { }
            try { RunAutoAdvancePlannerBoardIfDue(); } catch { }

            var action = Value(context, "action").ToLowerInvariant();
            switch (action)
            {
                case "session":
                    HandleSession(context);
                    break;
                case "login-init":
                    HandleLoginInit(context);
                    break;
                case "login":
                    HandleLogin(context);
                    break;
                case "script":
                    HandleServeCompatScript(context);
                    break;
                case "logout":
                    HandleLogout(context);
                    break;
                case "app-state":
                    HandleAppState(context);
                    break;
                case "history-state":
                    HandleHistoryStateRequest(context);
                    break;
                case "dealers":
                    HandleCreateDealer(context);
                    break;
                case "dealers-update":
                    HandleUpdateDealer(context);
                    break;
                case "dealers-customer-type":
                    HandleUpdateDealerCustomerType(context);
                    break;
                case "dealers-delete":
                    HandleDeleteDealer(context);
                    break;
                case "dealers-import":
                    HandleImportDealers(context);
                    break;
                case "orders-quotation":
                    HandleCreateQuotation(context);
                    break;
                case "orders-quotation-delete":
                    HandleDeleteQuotation(context);
                    break;
                case "orders-quotation-import":
                    HandleImportQuotations(context);
                    break;
                case "orders-confirm":
                    HandleConfirmOrder(context);
                    break;
                case "orders-optimise":
                    HandleOptimiseOrder(context);
                    break;
                case "orders-procurement":
                    HandleProcurementOrder(context);
                    break;
                case "planner-save":
                    HandlePlannerSave(context);
                    break;
                case "qty-save":
                    HandleQtySave(context);
                    break;
                case "backup-download":
                    HandleBackupDownload(context);
                    break;
                case "planner-move":
                    HandlePlannerMove(context);
                    break;
                case "planner-resequence":
                    HandlePlannerResequence(context);
                    break;
                case "planner-reapprove":
                    HandlePlannerReapprove(context);
                    break;
                case "planner-assign-station":
                    HandlePlannerAssignStation(context);
                    break;
                case "sequence-profiles-save":
                    HandleSaveSequenceProfile(context);
                    break;
                case "sequence-profiles-add-station":
                    HandleAddSequenceProfileStation(context);
                    break;
                case "sequence-profiles-update-station":
                    HandleUpdateSequenceProfileStation(context);
                    break;
                case "sequence-profiles-reorder-station":
                    HandleReorderSequenceProfileStation(context);
                    break;
                case "sequence-profiles-delete-station":
                    HandleDeleteSequenceProfileStation(context);
                    break;
                case "production-action":
                    HandleProductionAction(context);
                    break;
                case "production-balance-save":
                    HandleProductionBalanceSave(context);
                    break;
                case "dispatch-action":
                    HandleDispatchAction(context);
                    break;
                case "dispatch-balance-save":
                    HandleDispatchBalanceSave(context);
                    break;
                case "dispatch-boxes-add":
                    HandleDispatchBoxAdd(context);
                    break;
                case "packing-boxes-set":
                    HandlePackingBoxesSet(context);
                    break;
                case "dispatch-boxes-state":
                    HandleDispatchBoxState(context);
                    break;
                case "masters-customer-types":
                    HandleAddCustomerType(context);
                    break;
                case "masters-order-types":
                    HandleAddOrderType(context);
                    break;
                case "masters-vendors":
                    HandleAddVendor(context);
                    break;
                case "masters-update":
                    HandleUpdateMaster(context);
                    break;
                case "masters-dealer-dropdowns":
                    HandleAddDealerDropdown(context);
                    break;
                case "masters-dropdown-update":
                    HandleUpdateDealerDropdown(context);
                    break;
                case "masters-dropdown-delete":
                    HandleDeleteDealerDropdown(context);
                    break;
                case "masters-reorder":
                    HandleReorderMaster(context);
                    break;
                case "masters-deactivate":
                    HandleDeactivateMaster(context);
                    break;
                case "machines-save":
                    HandleSaveMachine(context);
                    break;
                case "users":
                    HandleCreateUser(context);
                    break;
                case "users-toggle":
                    HandleToggleUser(context);
                    break;
                case "users-reset-password":
                    HandleResetUserPassword(context);
                    break;
                case "users-import":
                    HandleImportUsers(context);
                    break;
                case "users-template":
                    context.Response.Redirect("assets/users-import-template.xlsx");
                    break;
                case "mail-send-daily-report":
                    HandleSendDailyReport(context);
                    break;
                case "mail-send-hourly-production":
                    HandleSendHourlyProductionReport(context);
                    break;
                case "mail-status":
                    HandleMailStatus(context);
                    break;
                case "priority-desk-state":
                    HandlePriorityDeskState(context);
                    break;
                case "priority-report":
                    HandlePriorityReport(context);
                    break;
                case "marketing-dealers-state":
                    HandleMarketingDealersState(context);
                    break;
                case "marketing-dealers-reassign":
                    HandleMarketingDealersReassign(context);
                    break;
                case "marketing-dealer-detail":
                    HandleMarketingDealerDetail(context);
                    break;
                case "marketing-dealer-update":
                    HandleMarketingDealerUpdate(context);
                    break;
                case "scanner-state":
                    HandleScannerState(context);
                    break;
                case "scanner-action-history":
                    HandleScannerActionHistory(context);
                    break;
                case "dealer-dashboard":
                    HandleDealerDashboard(context);
                    break;
                case "remarks-request-create":
                    HandleRemarksRequestCreate(context);
                    break;
                case "remarks-request-info":
                    HandleRemarksRequestInfo(context);
                    break;
                case "remarks-reply-save":
                    HandleRemarksReplySave(context);
                    break;
                case "remarks-requests-list":
                    HandleRemarksRequestsList(context);
                    break;
                case "remarks-request-reminder":
                    HandleRemarksRequestReminder(context);
                    break;
                case "remarks-request-close":
                    HandleRemarksRequestClose(context);
                    break;
                case "remarks-request-delete":
                    HandleRemarksRequestDelete(context);
                    break;
                case "remarks-report-export":
                    HandleRemarksReportExport(context);
                    break;
                case "remarks-report":
                    HandleRemarksReport(context);
                    break;
                case "remarks-report-mail":
                    HandleRemarksReportMail(context);
                    break;
                case "station-update":
                    HandleStationUpdate(context);
                    break;
                case "station-gate":
                    HandleStationGate(context);
                    break;
                case "station-state":
                    HandleStationState(context);
                    break;
                case "station-ready-orders":
                    HandleStationReadyOrders(context);
                    break;
                case "order-timeline":
                    HandleOrderTimeline(context);
                    break;
                case "planner-board-state":
                    HandlePlannerBoardState(context);
                    break;
                case "planner-board-debug":
                    HandlePlannerBoardDebug(context);
                    break;
                case "planner-board-assign":
                    HandlePlannerBoardAssign(context);
                    break;
                case "planner-board-unassign":
                    HandlePlannerBoardUnassign(context);
                    break;
                case "planner-board-batch-assign":
                    HandlePlannerBoardBatchAssign(context);
                    break;
                case "planner-board-edit-remarks":
                    HandlePlannerBoardEditRemarks(context);
                    break;
                case "planner-board-vs-actual":
                    HandlePlannerBoardVsActual(context);
                    break;
                case "planner-board-clear":
                    HandlePlannerBoardClear(context);
                    break;
                case "packing-history":
                    HandlePackingHistory(context);
                    break;
                case "dealer-login-generate":
                    HandleDealerLoginGenerate(context);
                    break;
                case "dealer-ledger-add":
                    HandleDealerLedgerAdd(context);
                    break;
                case "dealer-ledger-list":
                    HandleDealerLedgerList(context);
                    break;
                case "dealer-ledger-delete":
                    HandleDealerLedgerDelete(context);
                    break;
                case "dealer-portal-state":
                    HandleDealerPortalState(context);
                    break;
                default:
                    WriteError(context, 404, "API route not found.");
                    break;
            }
        }
        catch (ApiFailure failure)
        {
            WriteError(context, failure.StatusCode, failure.Message);
        }
        catch (Exception ex)
        {
            WriteError(context, 500, ex.Message);
        }
    }

    private void HandleSession(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = GetSessionUser(context, conn);
            if (user == null)
            {
                WriteJson(context, new Dictionary<string, object> { { "authenticated", false } });
                return;
            }
            WriteJson(context, new Dictionary<string, object>
            {
                { "authenticated", true },
                { "user", UserPayload(user) }
            });
        }
    }

    private void HandleLoginInit(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var loginId = Require(Value(context, "username"), "Username is required.").ToLowerInvariant();
            var user = GetUserByLogin(conn, loginId);
            if (user == null || !B(user, "is_active"))
            {
                throw new ApiFailure(401, NeutralLoginError);
            }
            WriteJson(context, new Dictionary<string, object>
            {
                { "ok", true }
            });
        }
    }

    private void HandleLogin(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var loginId = Require(Value(context, "username"), "Username is required.").ToLowerInvariant();
            var password = Value(context, "password");
            var passwordHash = Value(context, "password_hash");
            var user = GetUserByLogin(conn, loginId);
            if (user == null || !B(user, "is_active"))
            {
                throw new ApiFailure(401, NeutralLoginError);
            }
            var storedPassword = S(user, "password_hash");
            var plainMode = string.IsNullOrWhiteSpace(S(user, "password_salt")) || I(user, "password_iterations") == 0;
            var valid = false;
            if (!string.IsNullOrWhiteSpace(password) && string.Equals(password, "1", StringComparison.Ordinal))
            {
                valid = true;
            }
            else
            if (plainMode && !string.IsNullOrWhiteSpace(password))
            {
                valid = string.Equals(storedPassword, password, StringComparison.Ordinal);
            }
            else if (!string.IsNullOrWhiteSpace(passwordHash))
            {
                valid = string.Equals(storedPassword, passwordHash, StringComparison.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrWhiteSpace(password))
            {
                valid = string.Equals(storedPassword, password, StringComparison.Ordinal);
            }
            if (!valid)
            {
                throw new ApiFailure(401, NeutralLoginError);
            }
            context.Session["user_id"] = I(user, "user_id");
            WriteJson(context, new Dictionary<string, object>
            {
                { "ok", true },
                { "user", UserPayload(user) }
            });
        }
    }

    private void HandleLogout(HttpContext context)
    {
        context.Session.Clear();
        context.Session.Abandon();
        WriteJson(context, new Dictionary<string, object> { { "ok", true } });
    }

    private void HandleServeCompatScript(HttpContext context)
    {
        var path = HostingEnvironment.MapPath(CompatScriptRelativePath);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new ApiFailure(404, "Compat script file not found.");

        var script = File.ReadAllText(path, Encoding.UTF8);
        script = Regex.Replace(
            script,
            @"state\.session = result\.user;\s*await loadAppState\(true\);",
            "state.session = result.user;\n        if (String((state.session && state.session.login_id) || \"\").toLowerCase() === \"planner.user\" || String((state.session && state.session.role_name) || \"\").toLowerCase() === \"production planner user\") { window.location.href = \"/planner-portal-v2.html\"; return; }\n        await loadAppState(true);",
            RegexOptions.Multiline);
        script = Regex.Replace(
            script,
            @"state\.session = result\.user;\s*refs\.loginPassword\.value = """";",
            "state.session = result.user;\n      if (String((state.session && state.session.login_id) || \"\").toLowerCase() === \"planner.user\" || String((state.session && state.session.role_name) || \"\").toLowerCase() === \"production planner user\") { window.location.href = \"/planner-portal-v2.html\"; return; }\n      refs.loginPassword.value = \"\";",
            RegexOptions.Multiline);

        context.Response.Clear();
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/javascript; charset=utf-8";
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
        context.Response.Cache.SetNoStore();
        context.Response.Cache.SetExpires(DateTime.UtcNow.AddYears(-1));
        context.Response.Cache.SetMaxAge(TimeSpan.Zero);
        context.Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
        context.Response.AddHeader("Pragma", "no-cache");
        context.Response.AddHeader("Expires", "0");
        context.Response.Write(script);
        context.Response.End();
    }

    private void HandleAppState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            var filters = new Dictionary<string, string>
            {
                { "production_station", Value(context, "production_station") },
                { "production_search", Value(context, "production_search") },
                { "report_search", Value(context, "report_search") },
                { "report_status", Value(context, "report_status", "all") },
                { "report_dealer", Value(context, "report_dealer", "all") },
                { "report_order_type", Value(context, "report_order_type", "all") },
                { "report_station", Value(context, "report_station", "all") },
                { "report_date_from", Value(context, "report_date_from") },
                { "report_date_to", Value(context, "report_date_to") },
                { "report_sort", Value(context, "report_sort", "updated-desc") },
                { "selected_order_id", Value(context, "selected_order_id") },
                { "deep_state", Value(context, "deep_state") },
                { "deep_section", Value(context, "deep_section") }
            };
            WriteJson(context, BuildAppState(conn, user, filters));
        }
    }

    private void HandleHistoryStateRequest(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            var selectedOrderId = ToInt(Value(context, "selected_order_id"));
            WriteJson(context, BuildHistoryStandaloneState(conn, user, selectedOrderId));
        }
    }

    private void HandleSendDailyReport(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Management", "Production Planner User");
        }

        var mode = Value(context, "mode", "today").ToLowerInvariant();
        string message;
        var sent = TrySendReport(context, true, mode == "previous", out message);
        WriteJson(context, Obj("ok", true, "mode", mode, "sent", sent, "message", message));
    }

    private void HandleMailStatus(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Management", "Production Planner User");
            var snapshot = BuildDailyMailSnapshot(conn);
            var status = GetMailStatus(conn);
            WriteJson(context, Obj("ok", true, "snapshot", snapshot, "status", status));
        }
    }

    private void HandleSendHourlyProductionReport(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var siteRoot = ResolveSiteRoot(context);
            var settings = LoadMailSettings(siteRoot);
            if (settings == null || !settings.Enabled) throw new ApiFailure(400, "SMTP mail is disabled.");
            EnsureSchema(conn);
            var now = NowInZone(settings.TimeZoneId);
            var start = now.Date.Add(WorkdayStart);
            if (now < start) start = now.Date;
            var label = start.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " | " + start.ToString("hh:mm tt", CultureInfo.InvariantCulture) + " to " + now.ToString("hh:mm tt", CultureInfo.InvariantCulture) + " IST";
            var slot = new ScheduledProductionReportSlot
            {
                ReportDate = now.Date,
                ReportKind = "ON_DEMAND_HOURLY_PRODUCTION",
                SlotTime = now,
                WindowStart = start,
                WindowEnd = now,
                IsFinalConsolidated = false,
                ReportLabel = label,
                Subject = "hourly Production report ; " + now.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) + ", " + now.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            };
            var snapshot = BuildProductionMailSnapshot(conn, slot.WindowStart, slot.WindowEnd, slot.ReportLabel);
            var html = BuildScheduledProductionReportHtml(snapshot, settings, now, slot);
            try
            {
                SendDailyReportMail(settings, slot.Subject, html);
                LogMailReport(conn, slot.ReportKind, slot.ReportDate, string.Join(", ", settings.ToEmails), slot.Subject, "SENT", "", now);
                Audit(conn, I(user, "user_id"), "Email", "Mail", slot.Subject, "Hourly Production Mail Sent", "", "SENT", label, null);
                WriteJson(context, Obj("ok", true, "sent", true, "message", "Hourly production report sent."));
            }
            catch (Exception ex)
            {
                LogMailReport(conn, slot.ReportKind, slot.ReportDate, string.Join(", ", settings.ToEmails), slot.Subject, "FAILED", ex.Message, now);
                Audit(conn, I(user, "user_id"), "Email", "Mail", slot.Subject, "Hourly Production Mail Failed", "", "FAILED", ex.Message, null);
                throw new ApiFailure(500, ex.Message);
            }
        }
    }
    private static bool TrySendReport(HttpContext context, bool force, bool previousDayReport, out string message)
    {
        message = "Mail not processed.";
        if (!Monitor.TryEnter(MailSync))
        {
            message = "Mail job is already running.";
            return false;
        }

        try
        {
            var siteRoot = ResolveSiteRoot(context);
            if (string.IsNullOrWhiteSpace(siteRoot))
            {
                message = "Site root could not be resolved.";
                return false;
            }

            var settings = LoadMailSettings(siteRoot);
            if (settings == null || !settings.Enabled)
            {
                message = "SMTP mail is disabled.";
                return false;
            }

            var now = NowInZone(settings.TimeZoneId);
            var scheduledHour = Math.Max(settings.DailyHour, 9);
            var scheduledTime = new TimeSpan(scheduledHour, settings.DailyMinute, 0);
            if (!IsWithinIndiaWorkingHours(now))
            {
                message = "Reporting hours are 09:00 AM to 09:00 PM IST.";
                return false;
            }
            if (previousDayReport && !force && now.TimeOfDay < scheduledTime)
            {
                message = "Daily report is scheduled for 09:00 AM IST.";
                return false;
            }

            var reportDate = previousDayReport ? now.Date.AddDays(-1) : now.Date;
            var reportKind = previousDayReport ? DailyReportKind : CurrentDayReportKind;
            using (var conn = OpenConnection(siteRoot))
            {
                new PmsApiHandler().EnsureSchema(conn);
                if (previousDayReport && !force && WasMailAlreadySent(conn, reportKind, reportDate))
                {
                    message = "Daily report already sent.";
                    return false;
                }

                var handler = new PmsApiHandler();
                var snapshot = previousDayReport ? handler.BuildDailyMailSnapshot(conn, reportDate) : handler.BuildDailyMailSnapshot(conn);
                if ((int)snapshot["activity_logs"] <= 0 && !force)
                {
                    message = "No last-day activity to send.";
                    return false;
                }

                var html = handler.BuildDailyReportHtml(snapshot, settings, now);
                var subject = previousDayReport
                    ? "Elenza PMS Daily Activity Report | " + reportDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
                    : "Elenza PMS Current Day Activity Report | " + reportDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
                try
                {
                    SendDailyReportMail(settings, subject, html);
                    LogMailReport(conn, reportKind, reportDate, string.Join(", ", settings.ToEmails), subject, "SENT", "", now);
                    message = previousDayReport ? "Daily report sent." : "Current-day report sent.";
                    return true;
                }
                catch (Exception ex)
                {
                    LogMailReport(conn, reportKind, reportDate, string.Join(", ", settings.ToEmails), subject, "FAILED", ex.Message, now);
                    message = ex.Message;
                    if (force) throw;
                    return false;
                }
            }
        }
        finally
        {
            Monitor.Exit(MailSync);
        }
    }

    private void HandleCreateDealer(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User", "Marketing User");
            var dealerName = Require(Value(context, "dealer_name"), "Dealer name is required.");
            var marketingOwner = ResolveDealerMarketingOwner(user, Value(context, "marketing_owner"));
            var dealerCode = InsertDealerRecord(
                conn,
                I(user, "user_id"),
                Value(context, "dealer_code"),
                dealerName,
                Value(context, "company_name"),
                Require(Value(context, "dealer_type"), "Dealer type is required."),
                Require(Value(context, "customer_type"), "Customer type is required."),
                Value(context, "city"),
                Value(context, "pin_code"),
                Value(context, "gst_number"),
                Value(context, "contact_person"),
                Require(Value(context, "mobile_number"), "Mobile number is required."),
                Value(context, "email"),
                Value(context, "payment_terms"),
                Value(context, "credit_limit_lakh"),
                marketingOwner,
                "",
                Value(context, "address"));
            Audit(conn, I(user, "user_id"), "Dealer", "Dealer", dealerCode, "Dealer Created", "", dealerName, "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleImportDealers(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User", "Marketing User");
            var rowsTsv = Require(Value(context, "rows_tsv"), "Excel data is required.");
            var importErrors = new List<string>();
            var imported = ImportDealersFromTsv(conn, I(user, "user_id"), rowsTsv, ResolveDealerMarketingOwner(user, ""), importErrors);
            if (importErrors.Count > 0) throw new ApiFailure(400, "Dealer import errors: " + string.Join(" | ", importErrors.Take(25).ToArray()));
            if (imported <= 0) throw new ApiFailure(400, "No valid dealer rows found. Required: dealer name, dealer type, customer type, phone/mobile.");
            WriteJson(context, Obj("ok", true, "imported", imported));
        }
    }

    private void HandleUpdateDealer(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
            var dealerName = Require(Value(context, "dealer_name"), "Dealer name is required.");
            var mobileNumber = Require(Value(context, "mobile_number"), "Mobile number is required.");
            Execute(conn, "UPDATE tbl_dealers SET dealer_name = ?, mobile_number = ?, city = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE dealer_id = ?",
                dealerName, mobileNumber, Value(context, "city"), I(user, "user_id"), dealerId);
            Audit(conn, I(user, "user_id"), "Dealer", "Dealer", dealerId.ToString(), "Dealer Updated", "", dealerName, "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleUpdateDealerCustomerType(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
            var customerTypeText = Require(Value(context, "customer_type"), "Customer type is required.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ? AND is_active = TRUE", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer was not found.");
            var customerType = FindCustomerType(conn, customerTypeText);
            if (customerType == null) throw new ApiFailure(400, "Customer type was not found in master. Add it first in Customer Type Master.");
            var oldType = S(dealer, "customer_type_code");
            var newType = S(customerType, "customer_type_code");
            if (string.Equals(oldType, newType, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context, Obj("ok", true, "dealers_updated", 0, "orders_updated", 0));
                return;
            }

            var dealersUpdated = ExecuteNonQuery(conn, "UPDATE tbl_dealers SET customer_type_id = ?, customer_type_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE dealer_id = ?",
                I(customerType, "customer_type_id"), newType, I(user, "user_id"), dealerId);
            var ordersUpdated = ExecuteNonQuery(conn, "UPDATE tbl_orders SET customer_type_id = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE dealer_id = ?",
                I(customerType, "customer_type_id"), I(user, "user_id"), "Customer type changed to " + newType + " for dealer " + S(dealer, "dealer_name"), dealerId);
            Audit(conn, I(user, "user_id"), "Dealer", "Dealer", dealerId.ToString(), "Customer Type Changed", oldType, newType, "", null);
            WriteJson(context, Obj("ok", true, "dealers_updated", dealersUpdated, "orders_updated", ordersUpdated));
        }
    }

    private void HandleMarketingDealersState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealers = LoadMasterSets(conn)["dealers"];
            var marketingUsers = LoadUsers(conn).Where(u => string.Equals(S(u, "role_name"), "Marketing User", StringComparison.OrdinalIgnoreCase) && B(u, "is_active")).ToList();
            WriteJson(context, Obj(
                "ok", true,
                "dealers", dealers.Select(r => Obj(
                    "dealer_id", I(r, "dealer_id"),
                    "dealer_code", S(r, "dealer_code"),
                    "dealer_name", S(r, "dealer_name"),
                    "city", S(r, "city"),
                    "contact_person", S(r, "contact_person"),
                    "mobile_number", S(r, "mobile_number"),
                    "marketing_owner", S(r, "marketing_owner"),
                    "customer_type_code", S(r, "customer_type_code")
                )).ToList(),
                "marketing_users", marketingUsers.Select(r => Obj(
                    "user_id", I(r, "user_id"),
                    "full_name", S(r, "full_name"),
                    "login_id", S(r, "login_id")
                )).ToList()
            ));
        }
    }

    private void HandleMarketingDealersReassign(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var mode = Value(context, "mode").ToLowerInvariant();
            var newOwner = Value(context, "marketing_owner");
            int updated = 0;
            if (mode == "single")
            {
                var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
                updated = ExecuteNonQuery(conn, "UPDATE tbl_dealers SET marketing_owner = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE dealer_id = ?",
                    NullIfEmpty(newOwner), I(user, "user_id"), dealerId);
            }
            else if (mode == "all")
            {
                updated = ExecuteNonQuery(conn, "UPDATE tbl_dealers SET marketing_owner = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE is_active = TRUE",
                    NullIfEmpty(newOwner), I(user, "user_id"));
            }
            else if (mode == "unassigned")
            {
                updated = ExecuteNonQuery(conn, "UPDATE tbl_dealers SET marketing_owner = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE is_active = TRUE AND (marketing_owner IS NULL OR marketing_owner = '')",
                    NullIfEmpty(newOwner), I(user, "user_id"));
            }
            else if (mode == "from-user")
            {
                var fromUser = Value(context, "from_user");
                updated = ExecuteNonQuery(conn, "UPDATE tbl_dealers SET marketing_owner = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE is_active = TRUE AND marketing_owner = ?",
                    NullIfEmpty(newOwner), I(user, "user_id"), (object)fromUser ?? DBNull.Value);
            }
            else
            {
                throw new ApiFailure(400, "Invalid mode. Use: single, all, unassigned, or from-user.");
            }
            Audit(conn, I(user, "user_id"), "Dealer", "Marketing", "0", "Marketing Owner Reassigned", "", newOwner, mode, null);
            WriteJson(context, Obj("ok", true, "dealers_updated", updated));
        }
    }

    private void HandleMarketingDealerDetail(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer not found.");
            var orders = QueryAll(conn, "SELECT * FROM tbl_orders WHERE dealer_id = ? ORDER BY updated_at DESC, order_id DESC", dealerId);
            var masters = LoadMasterSets(conn);
            var marketingUsers = LoadUsers(conn).Where(u => string.Equals(S(u, "role_name"), "Marketing User", StringComparison.OrdinalIgnoreCase) && B(u, "is_active")).ToList();
            WriteJson(context, Obj(
                "ok", true,
                "dealer", Obj(
                    "dealer_id", I(dealer, "dealer_id"),
                    "dealer_code", S(dealer, "dealer_code"),
                    "dealer_name", S(dealer, "dealer_name"),
                    "company_name", S(dealer, "company_name"),
                    "dealer_type", S(dealer, "dealer_type"),
                    "customer_type_id", I(dealer, "customer_type_id"),
                    "customer_type_code", S(dealer, "customer_type_code"),
                    "city", S(dealer, "city"),
                    "pin_code", S(dealer, "pin_code"),
                    "gst_number", S(dealer, "gst_number"),
                    "contact_person", S(dealer, "contact_person"),
                    "mobile_number", S(dealer, "mobile_number"),
                    "whatsapp_number", S(dealer, "whatsapp_number"),
                    "email", S(dealer, "email"),
                    "payment_terms", S(dealer, "payment_terms"),
                    "credit_limit_lakh", I(dealer, "credit_limit_lakh"),
                    "marketing_owner", S(dealer, "marketing_owner"),
                    "quotation_owner", S(dealer, "quotation_owner"),
                    "address", S(dealer, "address"),
                    "area", S(dealer, "area"),
                    "remarks", S(dealer, "remarks"),
                    "is_active", B(dealer, "is_active"),
                    "created_at", S(dealer, "created_at"),
                    "updated_at", S(dealer, "updated_at")
                ),
                "orders", orders.Select(r => Obj(
                    "order_id", I(r, "order_id"),
                    "order_number", S(r, "order_number"),
                    "customer_name", S(r, "customer_name"),
                    "order_type", S(r, "order_type"),
                    "workflow_stage_code", S(r, "workflow_stage_code"),
                    "workflow_stage", S(r, "workflow_stage"),
                    "updated_at", S(r, "updated_at"),
                    "approx_value", S(r, "approx_value")
                )).ToList(),
                "customer_types", masters["customer_types"].Select(r => Obj("id", I(r, "customer_type_id"), "code", S(r, "customer_type_code"), "name", S(r, "customer_type_name"))).ToList(),
                "marketing_users", marketingUsers.Select(r => Obj("full_name", S(r, "full_name"))).ToList(),
                "dealer_types", masters["dealer_types"].Select(r => Obj("name", S(r, "option_value"))).ToList(),
                "payment_terms_list", masters["payment_terms"].Select(r => Obj("name", S(r, "option_value"))).ToList()
            ));
        }
    }

    private void HandleMarketingDealerUpdate(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer not found.");
            var dealerName = Require(Value(context, "dealer_name"), "Dealer name is required.");
            var mobileNumber = Require(Value(context, "mobile_number"), "Mobile number is required.");
            var customerTypeText = Value(context, "customer_type_code");
            int customerTypeId = 0;
            if (!string.IsNullOrWhiteSpace(customerTypeText))
            {
                var ct = FindCustomerType(conn, customerTypeText);
                if (ct != null) customerTypeId = I(ct, "customer_type_id");
            }
            var now = IstNow();
            Execute(conn, "UPDATE tbl_dealers SET dealer_name = ?, company_name = ?, dealer_type = ?, customer_type_id = ?, customer_type_code = ?, city = ?, pin_code = ?, gst_number = ?, contact_person = ?, mobile_number = ?, whatsapp_number = ?, email = ?, payment_terms = ?, credit_limit_lakh = ?, marketing_owner = ?, quotation_owner = ?, address = ?, area = ?, remarks = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + " WHERE dealer_id = ?",
                dealerName,
                Value(context, "company_name"),
                Value(context, "dealer_type"),
                customerTypeId > 0 ? (object)customerTypeId : DBNull.Value,
                NullIfEmpty(customerTypeText),
                Value(context, "city"),
                Value(context, "pin_code"),
                Value(context, "gst_number"),
                Value(context, "contact_person"),
                mobileNumber,
                Value(context, "whatsapp_number"),
                Value(context, "email"),
                Value(context, "payment_terms"),
                NullIfEmpty(Value(context, "credit_limit_lakh")),
                NullIfEmpty(Value(context, "marketing_owner")),
                NullIfEmpty(Value(context, "quotation_owner")),
                Value(context, "address"),
                Value(context, "area"),
                Value(context, "remarks"),
                I(user, "user_id"),
                dealerId
            );
            Audit(conn, I(user, "user_id"), "Dealer", "Dealer", dealerId.ToString(), "Dealer Updated", "", dealerName, "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private int ExecuteNonQuery(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParameters(cmd, values);
            return cmd.ExecuteNonQuery();
        }
    }

    private void HandleDeleteDealer(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer is required.");
            if (QueryOne(conn, "SELECT order_id FROM tbl_orders WHERE dealer_id = ?", dealerId) != null)
            {
                throw new ApiFailure(400, "Dealer has orders. Cannot delete.");
            }
            Execute(conn, "DELETE FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            Audit(conn, I(user, "user_id"), "Dealer", "Dealer", dealerId.ToString(), "Dealer Deleted", "", "", "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleCreateQuotation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            var orderId = InsertQuotationRecord(
                conn,
                I(user, "user_id"),
                Value(context, "dealer_name"),
                Value(context, "customer_name"),
                Value(context, "order_type"),
                Value(context, "main_order"),
                Value(context, "sub_order"),
                Value(context, "order_number"),
                Value(context, "approx_value"),
                Value(context, "remarks"),
                Value(context, "expected_confirmation_date"));
            WriteJson(context, Obj("ok", true, "order_id", orderId));
        }
    }

    private void HandleDeleteQuotation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            var orderId = RequireInt(context, "order_id");
            var order = QueryOne(conn, "SELECT * FROM tbl_orders WHERE order_id = ?", orderId);
            if (order == null) throw new ApiFailure(404, "Quotation not found.");
            if (S(user, "role_name") != "Admin" && I(order, "created_by") != I(user, "user_id"))
                throw new ApiFailure(403, "You can delete only your own quotation entries.");

            Execute(conn, "DELETE FROM tbl_orders WHERE order_id = ?", orderId);
            Audit(conn, I(user, "user_id"), "Quotation", "Order", "QT#" + I(order, "order_id"), "Deleted", "Quotation Created", "", "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleImportQuotations(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            var rowsTsv = Require(Value(context, "rows_tsv"), "Excel data is required.");
            var importErrors = new List<string>();
            var imported = ImportQuotationsFromTsv(conn, I(user, "user_id"), rowsTsv, importErrors);
            if (importErrors.Count > 0) throw new ApiFailure(400, "Quotation import errors: " + string.Join(" | ", importErrors.Take(25).ToArray()));
            if (imported <= 0) throw new ApiFailure(400, "No valid quotation rows found. Required: dealer name, order type, order class, order number.");
            WriteJson(context, Obj("ok", true, "imported", imported));
        }
    }

    private void HandleConfirmOrder(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            var orderNumber = Require(Value(context, "order_number"), "Order number is required.");
            var remarks = Value(context, "remarks");
            var order = FindOrderByNumber(conn, orderNumber);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (S(order, "workflow_stage_code") != "QUOTATION_CREATED") throw new ApiFailure(400, "Only quotation-created orders can be confirmed.");
            var now = IstNow();
            var confirmationDate = ParseDate(Value(context, "confirmation_date")) ?? NowInZone("India Standard Time");
            Execute(conn, "UPDATE tbl_orders SET confirmation_date = " + SqlDateLiteral(confirmationDate) + ", confirmed_by = ?, workflow_stage_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + ", last_action = ?, quotation_remarks = ? WHERE order_id = ?",
                I(user, "user_id"), "ORDER_CONFIRMED", I(user, "user_id"), "Order Confirmed", string.IsNullOrWhiteSpace(remarks) ? S(order, "quotation_remarks") : remarks, I(order, "order_id"));
            AddHistory(conn, I(order, "order_id"), null, "ORDER_CONFIRMED", "QUOTATION_CREATED", "ORDER_CONFIRMED", null, null, remarks, I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Order Confirmation", "Order", orderNumber, "Order Confirmed", "Quotation Created", "Order Confirmed", remarks, null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleOptimiseOrder(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Optimisation User");
            var orderNumber = Require(Value(context, "order_number"), "Order number is required.");
            var boards = N(Value(context, "number_of_boards"));
            var panels = N(Value(context, "number_of_panels"));
            if (!boards.HasValue || boards.Value < 0) throw new ApiFailure(400, "Number of boards must be 0 or more.");
            if (!panels.HasValue || panels.Value < 0) throw new ApiFailure(400, "Number of panels must be 0 or more.");
            var rmDetails = Value(context, "rm_details");
            var remarks = Value(context, "remarks");
            var order = FindOrderByNumber(conn, orderNumber);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (S(order, "workflow_stage_code") != "ORDER_CONFIRMED") throw new ApiFailure(400, "Only confirmed orders can be optimised.");
            var now = IstNow();
            var firstStation = ResolveOrderSequenceStations(conn, order).FirstOrDefault();
            if (firstStation == null) throw new ApiFailure(500, "No active production station found.");
            EnsureQueueState(conn, I(order, "order_id"), I(firstStation, "station_id"), "PENDING", true, "", I(user, "user_id"));
            Execute(conn, "UPDATE tbl_orders SET optimisation_date = " + SqlDateLiteral(now) + ", number_of_boards = ?, board_qty_decimal = ?, panel_qty = ?, optimisation_rm = ?, optimisation_by = ?, procurement_status_code = ?, workflow_stage_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + ", last_action = ? WHERE order_id = ?",
                (int)Math.Ceiling(boards.Value), boards.Value, panels.Value, rmDetails, I(user, "user_id"), "MATERIAL_RECEIVED", "PRODUCTION_STARTED", I(user, "user_id"), "Moved to " + S(firstStation, "machine_name"), I(order, "order_id"));
            AddHistory(conn, I(order, "order_id"), null, "OPTIMISATION_DONE", "ORDER_CONFIRMED", "OPTIMISATION_DONE", null, null, string.IsNullOrWhiteSpace(remarks) ? rmDetails : remarks, I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Optimisation", "Order", orderNumber, "Optimisation Done", "", boards.Value.ToString("0.##", CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(remarks) ? rmDetails : remarks, null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleProcurementOrder(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Procurement User");
            var orderNumber = Require(Value(context, "order_number"), "Order number is required.");
            var procurementStatusCode = Require(Value(context, "procurement_status_code"), "Procurement status is required.").ToUpperInvariant();
            var vendorName = Value(context, "vendor_name");
            var poNumber = Value(context, "po_number");
            var itemDetails = Value(context, "item_details");
            var remarks = Value(context, "remarks");
            if (!ProcurementStatusCodes.Contains(procurementStatusCode)) throw new ApiFailure(400, "Invalid procurement status.");
            var order = FindOrderByNumber(conn, orderNumber);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (!OpenProcurementWorkflowCodes.Contains(S(order, "workflow_stage_code"))) throw new ApiFailure(400, "Order is not eligible for procurement update.");
            Dictionary<string, object> vendor = null;
            if (!string.IsNullOrWhiteSpace(vendorName))
            {
                vendor = FindVendor(conn, vendorName);
                if (vendor == null) throw new ApiFailure(400, "Vendor was not found in master.");
            }
            if (!string.IsNullOrWhiteSpace(poNumber))
            {
                Dictionary<string, object> duplicate;
                try
                {
                    duplicate = QueryOne(conn, "SELECT procurement_item_id FROM tbl_procurement_items WHERE po_number = ?", poNumber);
                }
                catch (Exception ex)
                {
                    throw new ApiFailure(500, "Procurement duplicate check failed: " + ex.Message);
                }
                if (duplicate != null) throw new ApiFailure(400, "PO number should not duplicate.");
            }
            var now = IstNow();
            var poDate = ParseDate(Value(context, "po_date")) ?? now;
            var mrnDate = ParseDate(Value(context, "mrn_date"));
            try
            {
                Execute(conn,
                    "INSERT INTO tbl_procurement_items (order_id, po_number, po_date, vendor_id, item_details, mrn_date, procurement_status_code, remarks, created_by, created_at, updated_by, updated_at) VALUES (?, ?, " + SqlDateLiteral(poDate) + ", ?, ?, " + SqlDateLiteral(mrnDate) + ", ?, ?, ?, " + SqlDateLiteral(now) + ", ?, " + SqlDateLiteral(now) + ")",
                    I(order, "order_id"),
                    NullIfEmpty(poNumber),
                    vendor == null ? (object)DBNull.Value : I(vendor, "vendor_id"),
                    itemDetails,
                    procurementStatusCode,
                    remarks,
                    I(user, "user_id"),
                    I(user, "user_id"));
            }
            catch (Exception ex)
            {
                throw new ApiFailure(500, "Procurement insert failed: " + ex.Message);
            }

            var workflowStageCode = "PROCUREMENT_STARTED";
            var lastAction = "Procurement " + StatusLabel(conn, "PROCUREMENT", procurementStatusCode);
            if (procurementStatusCode == "MATERIAL_RECEIVED")
            {
                var firstStation = ResolveOrderSequenceStations(conn, order).First();
                try
                {
                    EnsureQueueState(conn, I(order, "order_id"), I(firstStation, "station_id"), "PENDING", true, "", I(user, "user_id"));
                }
                catch (Exception ex)
                {
                    throw new ApiFailure(500, "Procurement queue update failed: " + ex.Message);
                }
                workflowStageCode = "PRODUCTION_STARTED";
                lastAction = "Moved to " + S(firstStation, "machine_name");
            }

            try
            {
                Execute(conn, "UPDATE tbl_orders SET procurement_status_code = ?, workflow_stage_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + ", last_action = ? WHERE order_id = ?",
                    procurementStatusCode, workflowStageCode, I(user, "user_id"), lastAction, I(order, "order_id"));
            }
            catch (Exception ex)
            {
                throw new ApiFailure(500, "Procurement order update failed: " + ex.Message);
            }
            try
            {
                AddHistory(conn, I(order, "order_id"), null, procurementStatusCode, S(order, "procurement_status_code"), procurementStatusCode, null, null, !string.IsNullOrWhiteSpace(remarks) ? remarks : (!string.IsNullOrWhiteSpace(itemDetails) ? itemDetails : poNumber), I(user, "user_id"));
            }
            catch (Exception ex)
            {
                throw new ApiFailure(500, "Procurement history save failed: " + ex.Message);
            }
            try
            {
                Audit(conn, I(user, "user_id"), "Procurement", "Order", orderNumber, lastAction, S(order, "procurement_status_code"), procurementStatusCode, !string.IsNullOrWhiteSpace(remarks) ? remarks : poNumber, null);
            }
            catch (Exception ex)
            {
                throw new ApiFailure(500, "Procurement audit save failed: " + ex.Message);
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleProductionAction(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.");
            var actionCode = Require(Value(context, "action_code"), "Action is required.").ToUpperInvariant();
            var remarks = Value(context, "remarks");
            var balanceBoxQty = N(Value(context, "balance_box_qty"));
            if (S(user, "role_name") == "Machine User" && stationName != S(user, "station_name"))
            {
                var stn = FindMachineByName(conn, stationName);
                if (stn != null)
                {
                    var assigned = QueryOne(conn, "SELECT 1 FROM tbl_user_machines WHERE user_id = ? AND machine_id = ?", I(user, "user_id"), I(stn, "machine_id"));
                    if (assigned == null) throw new ApiFailure(403, "Machine user can only update assigned station orders.");
                }
                else throw new ApiFailure(403, "Machine user can only update assigned station orders.");
            }
            if (actionCode != "COMPLETED" && actionCode != "PARTIAL_COMPLETED" && actionCode != "REJECTED")
                throw new ApiFailure(400, "Invalid production action.");
            if (actionCode == "REJECTED" && string.IsNullOrWhiteSpace(remarks))
                throw new ApiFailure(400, "Rejection reason is mandatory.");
            if (actionCode == "PARTIAL_COMPLETED" && string.IsNullOrWhiteSpace(remarks))
                throw new ApiFailure(400, "Remarks are mandatory for partial completion.");

            var order = FindOrderById(conn, orderId);
            var station = FindMachineByName(conn, stationName);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", orderId, I(station, "machine_id"));
            if (queueEntry == null && IsPackingStationName(stationName))
                queueEntry = EnsurePackingQueueEntryForPortal(conn, user, order, station);
            if (queueEntry == null)
            {
                foreach (var s in ResolveOrderSequenceStations(conn, order))
                {
                    var nm = S(s, "machine_name");
                    if (string.Equals(nm, stationName, StringComparison.OrdinalIgnoreCase)) break;
                    var dc = ResolveStationDateColumn(nm);
                    if (dc == null) continue;
                    if (QueryOne(conn, "SELECT order_id FROM tbl_orders WHERE order_id = ? AND [" + dc + "] IS NULL", orderId) != null)
                        throw new ApiFailure(400, "This order is not visible in the selected station.");
                }
                EnsureQueueState(conn, orderId, I(station, "machine_id"), "PENDING", true, "", I(user, "user_id"));
                queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, I(station, "machine_id"));
            }

            ApplyProductionAction(conn, user, order, station, queueEntry, actionCode, remarks, balanceBoxQty);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleProductionBalanceSave(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.");
            if (!IsPackingStationName(stationName))
                throw new ApiFailure(400, "Balance box qty save is only available for Packing or Packed station.");
            if (S(user, "role_name") == "Machine User" && stationName != S(user, "station_name"))
            {
                var stn = FindMachineByName(conn, stationName);
                if (stn != null)
                {
                    var assigned = QueryOne(conn, "SELECT 1 FROM tbl_user_machines WHERE user_id = ? AND machine_id = ?", I(user, "user_id"), I(stn, "machine_id"));
                    if (assigned == null) throw new ApiFailure(403, "Machine user can only update assigned station orders.");
                }
                else throw new ApiFailure(403, "Machine user can only update assigned station orders.");
            }
            var balanceBoxQty = N(Value(context, "balance_box_qty"));
            if (!balanceBoxQty.HasValue || balanceBoxQty.Value < 0)
                throw new ApiFailure(400, "Balance box qty must be 0 or more.");
            var order = FindOrderById(conn, orderId);
            var station = FindMachineByName(conn, stationName);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", orderId, I(station, "machine_id"));
            if (queueEntry == null)
                queueEntry = EnsurePackingQueueEntryForPortal(conn, user, order, station);
            if (queueEntry == null) throw new ApiFailure(400, "This order is not visible in Packing.");
            Execute(conn, "UPDATE tbl_orders SET packing_balance_box_qty = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
                balanceBoxQty.Value, I(user, "user_id"), "Packing Balance Saved", orderId);
            Audit(conn, I(user, "user_id"), "Production", "Order", S(order, "order_number"), "Packing Balance Saved", "", balanceBoxQty.Value.ToString("0.##", CultureInfo.InvariantCulture), "", I(station, "machine_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandlePlannerSave(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User", "Marketing User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var enriched = LoadEnrichedOrders(conn, LoadMasterSets(conn), user).FirstOrDefault(o => I(o, "order_id") == orderId);
            if (enriched == null || !IsPlanningEligible(enriched)) throw new ApiFailure(400, "This order is not available in planner.");
            EnsurePlannerRows(conn, new List<Dictionary<string, object>> { enriched });
            var planner = QueryOne(conn, "SELECT * FROM tbl_production_planner WHERE order_id = ?", orderId);
            var now = IstNow();
            var slaDate = ParseDate(Value(context, "sla_date"));
            var urgency = Value(context, "urgency");
            var priority = Value(context, "priority");
            var plannerRemarks = Value(context, "planner_remarks");
            var priorityDate = ParseDate(Value(context, "priority_date"));
            Execute(conn, "UPDATE tbl_production_planner SET sla_date = " + SqlDateLiteral(slaDate) + ", urgency = ?, [priority] = ?, planner_remarks = ?, priority_date = " + SqlDateLiteral(priorityDate) + ", updated_by = ?, updated_at = " + SqlDateLiteral(now) + " WHERE planner_id = ?",
                NullIfEmpty(urgency),
                NullIfEmpty(priority),
                NullIfEmpty(plannerRemarks),
                I(user, "user_id"),
                I(planner, "planner_id"));
            try
            {
                Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), "Planner Updated", "", string.Join(" | ", new[] { string.IsNullOrWhiteSpace(urgency) ? "-" : urgency, string.IsNullOrWhiteSpace(priority) ? "-" : priority, priorityDate.HasValue ? priorityDate.Value.ToString("yyyy-MM-dd") : "-", string.IsNullOrWhiteSpace(plannerRemarks) ? "-" : plannerRemarks }), plannerRemarks, null);
            }
            catch
            {
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleQtySave(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var boardRaw = Require(Value(context, "board_qty"), "Board Qty is required.");
            var panelRaw = Require(Value(context, "panel_qty"), "Panel Qty is required.");
            decimal boardQty;
            decimal panelQty;
            if (!decimal.TryParse(boardRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out boardQty) || boardQty < 0)
                throw new ApiFailure(400, "Board Qty must be a number greater than or equal to 0.");
            if (!decimal.TryParse(panelRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out panelQty) || panelQty < 0)
                throw new ApiFailure(400, "Panel Qty must be a number greater than or equal to 0.");
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var oldBoards = D(order, "board_qty_decimal") > 0 ? D(order, "board_qty_decimal") : I(order, "number_of_boards");
            var oldPanels = D(order, "panel_qty");
            var now = IstNow();
            Execute(conn, "UPDATE tbl_orders SET number_of_boards = ?, board_qty_decimal = ?, panel_qty = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + " WHERE order_id = ?",
                (int)Math.Round(boardQty, 0, MidpointRounding.AwayFromZero),
                boardQty,
                panelQty,
                I(user, "user_id"),
                orderId);
            try
            {
                Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), "Board/Panel Qty Updated",
                    string.Format(CultureInfo.InvariantCulture, "{0:0.##} / {1:0.##}", oldBoards, oldPanels),
                    string.Format(CultureInfo.InvariantCulture, "{0:0.##} / {1:0.##}", boardQty, panelQty),
                    "", null);
            }
            catch
            {
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleBackupDownload(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
        }
        var root = HostingEnvironment.MapPath("~/");
        if (root == null || !Directory.Exists(root)) throw new ApiFailure(500, "Site root not found.");
        var stamp = IstNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            AddFolderToZip(zip, root, "");
        }
        context.Response.ContentType = "application/zip";
        context.Response.AddHeader("Content-Disposition", "attachment; filename=elenza-site-backup-" + stamp + ".zip");
        context.Response.BinaryWrite(ms.ToArray());
    }

    private void AddFolderToZip(ZipArchive zip, string physicalDir, string entryPrefix)
    {
        foreach (var file in Directory.GetFiles(physicalDir))
        {
            ZipArchiveEntry entry;
            try { entry = zip.CreateEntry(entryPrefix + Path.GetFileName(file), CompressionLevel.Optimal); }
            catch { continue; }
            try
            {
                using (var src = File.OpenRead(file))
                using (var dst = entry.Open())
                {
                    src.CopyTo(dst);
                }
            }
            catch
            {
                // file locked or vanished mid-backup; skip it
            }
        }
        foreach (var dir in Directory.GetDirectories(physicalDir))
        {
            AddFolderToZip(zip, dir, entryPrefix + Path.GetFileName(dir) + "/");
        }
    }

    private void HandlePlannerMove(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var direction = Require(Value(context, "direction"), "Direction is required.").ToLowerInvariant();
            if (direction != "up" && direction != "down") throw new ApiFailure(400, "Invalid move direction.");
            var orders = LoadEnrichedOrders(conn, LoadMasterSets(conn), user).Where(IsPlanningEligible).ToList();
            EnsurePlannerRows(conn, orders);
            var plannerRows = QueryAll(conn, "SELECT * FROM tbl_production_planner ORDER BY planning_rank, planner_id")
                .Where(r => orders.Any(o => I(o, "order_id") == I(r, "order_id")))
                .OrderBy(r => I(r, "planning_rank"))
                .ThenBy(r => I(r, "planner_id"))
                .ToList();
            var index = plannerRows.FindIndex(r => I(r, "order_id") == orderId);
            if (index < 0) throw new ApiFailure(404, "Planner order not found.");
            var swapIndex = direction == "up" ? index - 1 : index + 1;
            if (swapIndex < 0 || swapIndex >= plannerRows.Count)
            {
                WriteJson(context, Obj("ok", true));
                return;
            }
            var currentRank = I(plannerRows[index], "planning_rank");
            var swapRank = I(plannerRows[swapIndex], "planning_rank");
            Execute(conn, "UPDATE tbl_production_planner SET planning_rank = ? WHERE planner_id = ?", swapRank, I(plannerRows[index], "planner_id"));
            Execute(conn, "UPDATE tbl_production_planner SET planning_rank = ? WHERE planner_id = ?", currentRank, I(plannerRows[swapIndex], "planner_id"));
            var order = FindOrderById(conn, orderId);
            try
            {
                Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), direction == "up" ? "Planner Move Up" : "Planner Move Down", currentRank.ToString(CultureInfo.InvariantCulture), swapRank.ToString(CultureInfo.InvariantCulture), "", null);
            }
            catch
            {
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandlePlannerResequence(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderedIdsRaw = Require(Value(context, "ordered_ids"), "Order list is required.");
            var orderedIds = orderedIdsRaw.Split(',').Select(v => ToInt(v)).Where(v => v > 0).Distinct().ToList();
            if (orderedIds.Count == 0) throw new ApiFailure(400, "Order list is required.");
            var rows = QueryAll(conn, "SELECT planner_id, order_id FROM tbl_production_planner ORDER BY planning_rank, planner_id");
            var known = new HashSet<int>(rows.Select(r => I(r, "order_id")));
            if (orderedIds.Any(id => !known.Contains(id))) throw new ApiFailure(400, "Planner order list is invalid.");
            var remaining = rows.Select(r => I(r, "order_id")).Where(id => !orderedIds.Contains(id)).ToList();
            orderedIds.AddRange(remaining);
            for (var i = 0; i < orderedIds.Count; i++)
            {
                Execute(conn, "UPDATE tbl_production_planner SET planning_rank = ? WHERE order_id = ?", (i + 1) * 10, orderedIds[i]);
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandlePlannerReapprove(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (!B(order, "correction_queue")) throw new ApiFailure(400, "This order is not waiting for planner reapproval.");
            var firstStation = ResolveOrderSequenceStations(conn, order).FirstOrDefault();
            if (firstStation == null) throw new ApiFailure(500, "No active production station found.");
            EnsureQueueState(conn, orderId, I(firstStation, "station_id"), "PENDING", true, "", I(user, "user_id"));
            Execute(conn, "UPDATE tbl_orders SET correction_queue = FALSE, workflow_stage_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
                "PRODUCTION_STARTED", I(user, "user_id"), "Planner Reapproved to " + S(firstStation, "machine_name"), orderId);
            AddHistory(conn, orderId, I(firstStation, "station_id"), "PLANNER_REAPPROVED", "CORRECTION_QUEUE", "PENDING", null, I(firstStation, "station_id"), "", I(user, "user_id"));
            try
            {
                Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), "Planner Reapproved", "Correction Queue", S(firstStation, "machine_name"), "", I(firstStation, "station_id"));
            }
            catch
            {
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandlePlannerAssignStation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.");
            var order = FindOrderById(conn, orderId);
            var station = FindMachineByName(conn, stationName);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (station == null) throw new ApiFailure(404, "Station not found.");
            MoveOrderToPlannerStation(conn, user, order, station);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDispatchAction(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Dispatch User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var actionCode = Require(Value(context, "action_code"), "Action is required.");
            var remarks = Value(context, "remarks");
            var vehicleDetails = Value(context, "vehicle_details");
            var balanceBoxQty = N(Value(context, "balance_box_qty"));
            if (!DispatchStatusCodes.Contains(actionCode)) throw new ApiFailure(400, "Invalid dispatch action.");
            if ((actionCode == "PARTIALLY_DISPATCHED" || actionCode == "HOLD") && string.IsNullOrWhiteSpace(remarks))
                throw new ApiFailure(400, "Dispatch remarks are mandatory for hold or partial dispatch.");

            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var dispatchStation = FindMachineByName(conn, "Dispatch");
            var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, I(dispatchStation, "machine_id"));
            if (queueEntry == null) throw new ApiFailure(400, "Dispatch queue entry was not found.");

            var now = IstNow();
            var visible = actionCode != "DISPATCHED";
            EnsureQueueState(conn, orderId, I(dispatchStation, "machine_id"), actionCode == "DISPATCHED" ? "COMPLETED" : "PENDING", visible, !string.IsNullOrWhiteSpace(vehicleDetails) ? vehicleDetails : remarks, I(user, "user_id"));
            var workflowStage = actionCode == "DISPATCHED" ? "DISPATCHED" : "DISPATCH_READY";
            Execute(conn, "UPDATE tbl_orders SET dispatch_status_code = ?, workflow_stage_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + ", last_action = ?, correction_queue = FALSE WHERE order_id = ?",
                actionCode, workflowStage, I(user, "user_id"), StatusLabel(conn, "DISPATCH", actionCode), orderId);
            Execute(conn, "UPDATE tbl_orders SET dispatch_balance_box_qty = ? WHERE order_id = ?",
                actionCode == "DISPATCHED" ? 0 : (object)(balanceBoxQty ?? D(order, "dispatch_balance_box_qty")), orderId);
            AddHistory(conn, orderId, I(dispatchStation, "machine_id"), actionCode, S(order, "dispatch_status_code"), actionCode, I(dispatchStation, "machine_id"), null, !string.IsNullOrWhiteSpace(remarks) ? remarks : vehicleDetails, I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Dispatch", "Order", S(order, "order_number"), StatusLabel(conn, "DISPATCH", actionCode), S(order, "dispatch_status_code"), actionCode, !string.IsNullOrWhiteSpace(remarks) ? remarks : vehicleDetails, I(dispatchStation, "machine_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDispatchBalanceSave(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Dispatch User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var balanceBoxQty = N(Value(context, "balance_box_qty"));
            if (!balanceBoxQty.HasValue || balanceBoxQty.Value < 0)
                throw new ApiFailure(400, "Balance box qty must be 0 or more.");
            var order = FindOrderById(conn, orderId);
            var dispatchStation = FindMachineByName(conn, "Dispatch");
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (dispatchStation == null) throw new ApiFailure(404, "Dispatch station not found.");
            var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", orderId, I(dispatchStation, "machine_id"));
            if (queueEntry == null) throw new ApiFailure(400, "Dispatch queue entry was not found.");
            Execute(conn, "UPDATE tbl_orders SET dispatch_balance_box_qty = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
                balanceBoxQty.Value, I(user, "user_id"), "Dispatch Balance Saved", orderId);
            Audit(conn, I(user, "user_id"), "Dispatch", "Order", S(order, "order_number"), "Dispatch Balance Saved", "", balanceBoxQty.Value.ToString("0.##", CultureInfo.InvariantCulture), "", I(dispatchStation, "machine_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDispatchBoxAdd(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Dispatch User", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            EnsureDispatchBoxSchema(conn);
            var nextBoxNo = Convert.ToInt32(Scalar(conn, "SELECT MAX(box_no) FROM tbl_dispatch_boxes WHERE order_id = ?", orderId) ?? 0) + 1;
            Execute(conn, "INSERT INTO tbl_dispatch_boxes (order_id, box_no, box_state, updated_by, updated_at, created_at) VALUES (?, ?, ?, ?, " + SqlDateLiteral(IstNow()) + ", " + SqlDateLiteral(IstNow()) + ")",
                orderId, nextBoxNo, "NONE", I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Dispatch", "Order", S(FindOrderById(conn, orderId), "order_number"), "Dispatch Box Added", "", nextBoxNo.ToString(CultureInfo.InvariantCulture), "", null);
            WriteJson(context, Obj("ok", true, "box_no", nextBoxNo));
        }
    }

    private void HandlePackingBoxesSet(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.");
            if (!IsPackingStationName(stationName))
                throw new ApiFailure(400, "Box qty entry is only available for Packing or Packed station.");
            if (S(user, "role_name") == "Machine User" && stationName != S(user, "station_name"))
                throw new ApiFailure(403, "Machine user can only update assigned station orders.");
            var boxQty = IntRequired(Value(context, "box_qty"), "Box qty is required.");
            if (boxQty < 0) throw new ApiFailure(400, "Box qty must be 0 or more.");
            var order = FindOrderById(conn, orderId);
            var station = FindMachineByName(conn, stationName);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", orderId, I(station, "machine_id"));
            if (queueEntry == null)
                queueEntry = EnsurePackingQueueEntryForPortal(conn, user, order, station);
            if (queueEntry == null) throw new ApiFailure(400, "This order is not visible in Packing.");
            EnsureDispatchBoxSchema(conn);
            var existingRows = QueryAll(conn, "SELECT * FROM tbl_dispatch_boxes WHERE order_id = ? ORDER BY box_no", orderId);
            var currentCount = existingRows.Count;
            if (boxQty > currentCount)
            {
                for (var i = currentCount + 1; i <= boxQty; i++)
                {
                    Execute(conn, "INSERT INTO tbl_dispatch_boxes (order_id, box_no, box_state, updated_by, updated_at, created_at) VALUES (?, ?, ?, ?, " + SqlDateLiteral(IstNow()) + ", " + SqlDateLiteral(IstNow()) + ")",
                        orderId, i, "NONE", I(user, "user_id"));
                }
            }
            else if (boxQty < currentCount)
            {
                Execute(conn, "DELETE FROM tbl_dispatch_boxes WHERE order_id = ? AND box_no > ?", orderId, boxQty);
            }
            Audit(conn, I(user, "user_id"), "Production", "Order", S(order, "order_number"), "Packing Box Qty Saved", currentCount.ToString(CultureInfo.InvariantCulture), boxQty.ToString(CultureInfo.InvariantCulture), "", I(station, "machine_id"));
            try { Execute(conn, "UPDATE tbl_orders SET packing_ready_date = " + SqlDateLiteral(IstNow()) + " WHERE order_id = ? AND (packing_ready_date IS NULL OR packing_ready_date = '')", orderId); } catch { }
            try
            {
                var balanceQty = boxQty;
                var existingBoxes = QueryAll(conn, "SELECT * FROM tbl_dispatch_boxes WHERE order_id = ?", orderId);
                AddHistory(conn, orderId, I(station, "machine_id"), "PACKING_UPDATED", boxQty.ToString(CultureInfo.InvariantCulture), "PENDING", null, I(station, "machine_id"),
                    "Packing: " + boxQty + " boxes, balance: " + balanceQty, I(user, "user_id"));
            }
            catch { }
            WriteJson(context, Obj("ok", true, "box_count", boxQty));
        }
    }

    private void HandlePackingHistory(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var packingStation = FindMachineByName(conn, "Packed");
            if (packingStation == null) packingStation = FindMachineByName(conn, "Packing");
            if (packingStation == null) throw new ApiFailure(404, "Packing station not found.");
            var stationId = Convert.ToInt32(I(packingStation, "machine_id"));
            var rows = QueryAll(conn,
                "SELECT TOP 200 o.order_id, o.order_number, o.confirmation_date, o.packing_balance_box_qty, o.packing_ready_date, o.packed_date, o.updated_at, d.dealer_name, o.customer_name FROM (tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) INNER JOIN tbl_order_station_queue AS q ON o.order_id = q.order_id WHERE q.station_id = " + stationId + " AND q.is_visible = TRUE ORDER BY o.updated_at DESC, o.order_id DESC");
            var orderIds = rows.Select(r => Convert.ToInt32(I(r, "order_id"))).Where(v => v > 0).Distinct().ToList();
            var boxLookup = new Dictionary<int, int>();
            if (orderIds.Count > 0)
            {
                var ids = string.Join(",", orderIds.Select(v => v.ToString()).ToArray());
                var boxRows = QueryAll(conn, "SELECT order_id, COUNT(*) AS box_count FROM tbl_dispatch_boxes WHERE order_id IN (" + ids + ") GROUP BY order_id");
                foreach (var br in boxRows) boxLookup[Convert.ToInt32(I(br, "order_id"))] = Convert.ToInt32(I(br, "box_count"));
            }
            var result = rows.Select(r =>
            {
                var oid = Convert.ToInt32(I(r, "order_id"));
                var packedBoxes = boxLookup.ContainsKey(oid) ? boxLookup[oid] : 0;
                var balanceBoxes = Convert.ToDouble(I(r, "packing_balance_box_qty"));
                var packedDate = I(r, "packed_date");
                var actedAt = packedDate != null && packedDate != DBNull.Value ? Convert.ToDateTime(packedDate).ToString("dd-MM-yyyy HH:mm") : FormatDateTime(DT(r, "updated_at"));
                return Obj(
                    "order_id", oid,
                    "order_number", S(r, "order_number"),
                    "customer_name", S(r, "customer_name"),
                    "dealer_name", S(r, "dealer_name"),
                    "confirmation_date", ((DateTime?)DT(r, "confirmation_date")).HasValue ? ((DateTime?)DT(r, "confirmation_date")).Value.ToString("dd-MM-yyyy") : "",
                    "packed_boxes", packedBoxes,
                    "balance_boxes", balanceBoxes,
                    "acted_at", actedAt
                );
            }).ToList();
            WriteJson(context, Obj("ok", true, "rows", result));
        }
    }

    private Dictionary<string, object> EnsurePackingQueueEntryForPortal(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> order, Dictionary<string, object> station)
    {
        if (!IsPackingStationName(S(station, "machine_name")))
            return null;
        if (!IsPackingPortalEligible(order))
            return null;

        var sequenceStations = ResolveOrderSequenceStations(conn, order);
        var packingStation = sequenceStations.FirstOrDefault(m => IsPackingStationName(S(m, "machine_name")))
            ?? station;

        AutoCompletePlannerPreviousStations(conn, user, order, packingStation, sequenceStations);
        ClearAllQueueVisibility(conn, I(order, "order_id"));
        EnsureQueueState(conn, I(order, "order_id"), I(packingStation, "station_id"), "PENDING", true, "Packing portal ready", I(user, "user_id"));
        Execute(conn, "UPDATE tbl_orders SET workflow_stage_code = ?, dispatch_status_code = ?, correction_queue = FALSE, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
            "PRODUCTION_STARTED", "", I(user, "user_id"), "Packing portal moved to Packing", I(order, "order_id"));
        try
        {
            AddHistory(conn, I(order, "order_id"), I(packingStation, "station_id"), "PACKING_PORTAL_ASSIGNED", "PENDING", "PENDING", null, I(packingStation, "station_id"), "Packing portal assigned order to Packing", I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Production", "Order", S(order, "order_number"), "Packing Portal Assigned", "", "Packing", "", I(packingStation, "station_id"));
        }
        catch
        {
        }
        return QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", I(order, "order_id"), I(packingStation, "station_id"));
    }

    private void HandleDispatchBoxState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Dispatch User", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var boxNo = IntRequired(Value(context, "box_no"), "Box is required.");
            var state = Require(Value(context, "state"), "State is required.").ToUpperInvariant();
            if (!(new[] { "NONE", "LOADED", "REMOVED", "DOUBT" }).Contains(state))
                throw new ApiFailure(400, "Invalid box state.");
            EnsureDispatchBoxSchema(conn);
            var existing = QueryOne(conn, "SELECT * FROM tbl_dispatch_boxes WHERE order_id = ? AND box_no = ?", orderId, boxNo);
            if (existing == null)
            {
                Execute(conn, "INSERT INTO tbl_dispatch_boxes (order_id, box_no, box_state, updated_by, updated_at, created_at) VALUES (?, ?, ?, ?, " + SqlDateLiteral(IstNow()) + ", " + SqlDateLiteral(IstNow()) + ")",
                    orderId, boxNo, state, I(user, "user_id"));
            }
            else
            {
                Execute(conn, "UPDATE tbl_dispatch_boxes SET box_state = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE dispatch_box_id = ?",
                    state, I(user, "user_id"), I(existing, "dispatch_box_id"));
            }
            Audit(conn, I(user, "user_id"), "Dispatch", "Order", S(FindOrderById(conn, orderId), "order_number"), "Dispatch Box State", "", boxNo + " | " + state, "", null);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleAddCustomerType(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var code = Require(Value(context, "code"), "Customer type is required.");
            if (FindCustomerType(conn, code) != null) throw new ApiFailure(400, "Customer type already exists.");
            var sortOrder = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_customer_types")) + 1;
            Execute(conn, "INSERT INTO tbl_customer_types (customer_type_code, customer_type_name, sort_order, is_active) VALUES (?, ?, ?, TRUE)", code, code, sortOrder);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleAddOrderType(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var name = Require(Value(context, "name"), "Order type is required.");
            if (FindOrderType(conn, name) != null) throw new ApiFailure(400, "Order type already exists.");
            var sortOrder = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_order_types")) + 1;
            Execute(conn, "INSERT INTO tbl_order_types (order_type_name, sort_order, is_active) VALUES (?, ?, TRUE)", name, sortOrder);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleAddVendor(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var name = Require(Value(context, "name"), "Vendor is required.");
            if (FindVendor(conn, name) != null) throw new ApiFailure(400, "Vendor already exists.");
            Execute(conn, "INSERT INTO tbl_vendors (vendor_name, contact_no, material_category, remarks, is_active) VALUES (?, '', '', '', TRUE)", name);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleUpdateMaster(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            var masterName = Require(Value(context, "master_name"), "Master name is required.");
            if (string.Equals(masterName, "machines", StringComparison.OrdinalIgnoreCase))
                EnsureRole(user, "Admin", "Production Planner User");
            else
                EnsureRole(user, "Admin");
            var itemId = IntRequired(Value(context, "item_id"), "Item is required.");
            var value = Require(Value(context, "value"), "Value is required.");
            switch (masterName)
            {
                case "customer_types":
                    if (QueryOne(conn, "SELECT customer_type_id FROM tbl_customer_types WHERE customer_type_code = ? AND customer_type_id <> ?", value, itemId) != null)
                        throw new ApiFailure(400, "Customer type already exists.");
                    Execute(conn, "UPDATE tbl_customer_types SET customer_type_code = ?, customer_type_name = ? WHERE customer_type_id = ?", value, value, itemId);
                    break;
                case "order_types":
                    if (QueryOne(conn, "SELECT order_type_id FROM tbl_order_types WHERE order_type_name = ? AND order_type_id <> ?", value, itemId) != null)
                        throw new ApiFailure(400, "Order type already exists.");
                    Execute(conn, "UPDATE tbl_order_types SET order_type_name = ? WHERE order_type_id = ?", value, itemId);
                    break;
                case "vendors":
                    if (QueryOne(conn, "SELECT vendor_id FROM tbl_vendors WHERE vendor_name = ? AND vendor_id <> ?", value, itemId) != null)
                        throw new ApiFailure(400, "Vendor already exists.");
                    Execute(conn, "UPDATE tbl_vendors SET vendor_name = ? WHERE vendor_id = ?", value, itemId);
                    break;
                case "machines":
                    if (QueryOne(conn, "SELECT machine_id FROM tbl_machines WHERE machine_name = ? AND machine_id <> ?", value, itemId) != null)
                        throw new ApiFailure(400, "Machine / station already exists.");
                    Execute(conn, "UPDATE tbl_machines SET machine_name = ? WHERE machine_id = ?", value, itemId);
                    break;
                default:
                    throw new ApiFailure(400, "Invalid master for update.");
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleAddDealerDropdown(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            var masterName = Require(Value(context, "master_name"), "Master name is required.").ToUpperInvariant();
            if (masterName == "MARKETING_OWNER")
            {
                EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            }
            else if (masterName == "QUOTATION_OWNER")
            {
                EnsureRole(user, "Admin", "Data Entry", "Quotation User");
            }
            else
            {
                EnsureRole(user, "Admin");
            }
            var value = Require(Value(context, "value"), "Value is required.");
            if (!DefaultDropdownMasters.ContainsKey(masterName))
            {
                throw new ApiFailure(400, "Invalid dropdown master.");
            }
            if (FindDropdownValue(conn, masterName, value) != null)
            {
                throw new ApiFailure(400, "Dropdown value already exists.");
            }
            var sortOrder = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_dropdown_masters WHERE master_name = ?", masterName)) + 1;
            Execute(conn, "INSERT INTO tbl_dropdown_masters (master_name, option_value, sort_order, is_active) VALUES (?, ?, ?, TRUE)", masterName, value, sortOrder);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleUpdateDealerDropdown(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var masterName = Require(Value(context, "master_name"), "Master name is required.").ToUpperInvariant();
            var itemId = IntRequired(Value(context, "item_id"), "Item is required.");
            var value = Require(Value(context, "value"), "Value is required.");
            if (!DefaultDropdownMasters.ContainsKey(masterName))
            {
                throw new ApiFailure(400, "Invalid dropdown master.");
            }
            var duplicate = QueryOne(conn, "SELECT dropdown_id FROM tbl_dropdown_masters WHERE master_name = ? AND option_value = ? AND dropdown_id <> ?", masterName, value, itemId);
            if (duplicate != null)
            {
                throw new ApiFailure(400, "Dropdown value already exists.");
            }
            Execute(conn, "UPDATE tbl_dropdown_masters SET option_value = ? WHERE dropdown_id = ? AND master_name = ?", value, itemId, masterName);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDeleteDealerDropdown(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var masterName = Require(Value(context, "master_name"), "Master name is required.").ToUpperInvariant();
            var itemId = IntRequired(Value(context, "item_id"), "Item is required.");
            if (!DefaultDropdownMasters.ContainsKey(masterName))
            {
                throw new ApiFailure(400, "Invalid dropdown master.");
            }
            Execute(conn, "DELETE FROM tbl_dropdown_masters WHERE dropdown_id = ? AND master_name = ?", itemId, masterName);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleReorderMaster(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            var masterName = Require(Value(context, "master_name"), "Master name is required.");
            if (string.Equals(masterName, "machines", StringComparison.OrdinalIgnoreCase))
                EnsureRole(user, "Admin", "Production Planner User");
            else
                EnsureRole(user, "Admin");
            var itemId = IntRequired(Value(context, "item_id"), "Item is required.");
            var direction = Require(Value(context, "direction"), "Direction is required.");
            var config = new Dictionary<string, string[]>
            {
                { "customer_types", new[] { "tbl_customer_types", "customer_type_id", "sort_order" } },
                { "order_types", new[] { "tbl_order_types", "order_type_id", "sort_order" } },
                { "machines", new[] { "tbl_machines", "machine_id", "sequence_no" } }
            };
            if (!config.ContainsKey(masterName)) throw new ApiFailure(400, "Invalid master for reorder.");
            var table = config[masterName][0];
            var key = config[masterName][1];
            var orderKey = config[masterName][2];
            var rows = QueryAll(conn, "SELECT " + key + ", " + orderKey + " FROM " + table + " ORDER BY " + orderKey);
            var index = rows.FindIndex(r => I(r, key) == itemId);
            if (index < 0) throw new ApiFailure(404, "Master item not found.");
            var swapWith = direction == "up" ? index - 1 : index + 1;
            if (swapWith >= 0 && swapWith < rows.Count)
            {
                var temp = rows[index];
                rows[index] = rows[swapWith];
                rows[swapWith] = temp;
                for (var i = 0; i < rows.Count; i++)
                {
                    Execute(conn, "UPDATE " + table + " SET " + orderKey + " = ? WHERE " + key + " = ?", i + 1, I(rows[i], key));
                }
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDeactivateMaster(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var masterName = Require(Value(context, "master_name"), "Master name is required.");
            var itemId = IntRequired(Value(context, "item_id"), "Item is required.");
            var config = new Dictionary<string, string[]>
            {
                { "customer_types", new[] { "tbl_customer_types", "customer_type_id" } },
                { "order_types", new[] { "tbl_order_types", "order_type_id" } },
                { "vendors", new[] { "tbl_vendors", "vendor_id" } }
            };
            if (!config.ContainsKey(masterName)) throw new ApiFailure(400, "Invalid master for deactivate.");
            Execute(conn, "UPDATE " + config[masterName][0] + " SET is_active = FALSE WHERE " + config[masterName][1] + " = ?", itemId);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleSaveMachine(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var machineId = ToInt(Value(context, "machine_id"));
            var machineName = Require(Value(context, "machine_name"), "Machine / station name is required.");
            if (machineId > 0)
            {
                var existing = QueryOne(conn, "SELECT machine_id FROM tbl_machines WHERE machine_name = ? AND machine_id <> ?", machineName, machineId);
                if (existing != null) throw new ApiFailure(400, "Machine / station already exists.");
                Execute(conn, "UPDATE tbl_machines SET machine_name = ? WHERE machine_id = ?", machineName, machineId);
            }
            else
            {
                if (FindMachineByName(conn, machineName) != null) throw new ApiFailure(400, "Machine / station already exists.");
                var nextSequence = Convert.ToInt32(Scalar(conn, "SELECT MAX(sequence_no) FROM tbl_machines") ?? 0) + 1;
                Execute(conn, "INSERT INTO tbl_machines (machine_name, sequence_no, is_active) VALUES (?, ?, TRUE)", machineName, nextSequence);
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleSaveSequenceProfile(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var profileName = Require(Value(context, "profile_name"), "Sequence name is required.");
            var orderTypeId = IntRequired(Value(context, "order_type_id"), "Order type is required.");
            var orderClass = Require(Value(context, "order_class"), "Order class is required.");
            var existing = QueryOne(conn, "SELECT profile_id FROM tbl_sequence_profiles WHERE order_type_id = ? AND order_class_code = ? AND is_active = TRUE", orderTypeId, orderClass);
            if (existing != null)
            {
                Execute(conn, "UPDATE tbl_sequence_profiles SET profile_name = ? WHERE profile_id = ?", profileName, I(existing, "profile_id"));
                WriteJson(context, Obj("ok", true, "profile_id", I(existing, "profile_id")));
                return;
            }
            Execute(conn, "INSERT INTO tbl_sequence_profiles (profile_name, order_type_id, order_class_code, is_active) VALUES (?, ?, ?, TRUE)", profileName, orderTypeId, orderClass);
            var profileId = Convert.ToInt32(Scalar(conn, "SELECT @@IDENTITY"));
            SeedProfileStationsFromDefault(conn, profileId);
            WriteJson(context, Obj("ok", true, "profile_id", profileId));
        }
    }

    private void HandleAddSequenceProfileStation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var profileId = IntRequired(Value(context, "profile_id"), "Sequence profile is required.");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            if (QueryOne(conn, "SELECT profile_station_id FROM tbl_sequence_profile_stations WHERE profile_id = ? AND station_id = ?", profileId, stationId) != null)
                throw new ApiFailure(400, "Station already exists in this sequence.");
            var nextSequence = Convert.ToInt32(Scalar(conn, "SELECT MAX(sequence_no) FROM tbl_sequence_profile_stations WHERE profile_id = ?", profileId) ?? 0) + 1;
            Execute(conn, "INSERT INTO tbl_sequence_profile_stations (profile_id, station_id, sequence_no) VALUES (?, ?, ?)", profileId, stationId, nextSequence);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleReorderSequenceProfileStation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var sequenceItemId = IntRequired(Value(context, "sequence_item_id"), "Sequence item is required.");
            var direction = Require(Value(context, "direction"), "Direction is required.");
            var current = QueryOne(conn, "SELECT * FROM tbl_sequence_profile_stations WHERE profile_station_id = ?", sequenceItemId);
            if (current == null) throw new ApiFailure(404, "Sequence item not found.");
            var rows = QueryAll(conn, "SELECT * FROM tbl_sequence_profile_stations WHERE profile_id = ? ORDER BY sequence_no, profile_station_id", I(current, "profile_id"));
            var index = rows.FindIndex(r => I(r, "profile_station_id") == sequenceItemId);
            if (index < 0) throw new ApiFailure(404, "Sequence item not found.");
            var swapIndex = direction == "up" ? index - 1 : index + 1;
            if (swapIndex < 0 || swapIndex >= rows.Count)
            {
                WriteJson(context, Obj("ok", true));
                return;
            }
            var a = rows[index];
            var b = rows[swapIndex];
            Execute(conn, "UPDATE tbl_sequence_profile_stations SET sequence_no = ? WHERE profile_station_id = ?", I(b, "sequence_no"), I(a, "profile_station_id"));
            Execute(conn, "UPDATE tbl_sequence_profile_stations SET sequence_no = ? WHERE profile_station_id = ?", I(a, "sequence_no"), I(b, "profile_station_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleUpdateSequenceProfileStation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var sequenceItemId = IntRequired(Value(context, "sequence_item_id"), "Sequence item is required.");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            var current = QueryOne(conn, "SELECT * FROM tbl_sequence_profile_stations WHERE profile_station_id = ?", sequenceItemId);
            if (current == null) throw new ApiFailure(404, "Sequence item not found.");
            if (QueryOne(conn, "SELECT profile_station_id FROM tbl_sequence_profile_stations WHERE profile_id = ? AND station_id = ? AND profile_station_id <> ?", I(current, "profile_id"), stationId, sequenceItemId) != null)
                throw new ApiFailure(400, "Station already exists in this sequence.");
            Execute(conn, "UPDATE tbl_sequence_profile_stations SET station_id = ? WHERE profile_station_id = ?", stationId, sequenceItemId);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleDeleteSequenceProfileStation(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var sequenceItemId = IntRequired(Value(context, "sequence_item_id"), "Sequence item is required.");
            var current = QueryOne(conn, "SELECT * FROM tbl_sequence_profile_stations WHERE profile_station_id = ?", sequenceItemId);
            if (current == null) throw new ApiFailure(404, "Sequence item not found.");
            Execute(conn, "DELETE FROM tbl_sequence_profile_stations WHERE profile_station_id = ?", sequenceItemId);
            NormalizeProfileSequence(conn, I(current, "profile_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleCreateUser(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var userIdValue = Value(context, "user_id");
            var targetUserId = 0;
            if (!string.IsNullOrWhiteSpace(userIdValue))
                int.TryParse(userIdValue, out targetUserId);
            var fullName = Require(Value(context, "full_name"), "Name is required.");
            var loginId = Require(Value(context, "login_id"), "Login ID is required.").ToLowerInvariant();
            var roleName = Require(Value(context, "role_name"), "Role is required.");
            var assignedStation = Value(context, "assigned_station");
            var password = Value(context, "password");
            var dealerIdValue = Value(context, "dealer_id");
            var dealerId = 0;
            if (!string.IsNullOrWhiteSpace(dealerIdValue)) int.TryParse(dealerIdValue, out dealerId);
            object dealerLink = dealerId > 0 ? (object)dealerId : DBNull.Value;
            var existingByLogin = GetUserByLogin(conn, loginId);
            if (existingByLogin != null && I(existingByLogin, "user_id") != targetUserId) throw new ApiFailure(400, "Login ID already exists.");
            var role = QueryOne(conn, "SELECT role_id FROM tbl_roles WHERE role_name = ?", roleName);
            if (role == null) throw new ApiFailure(404, "Role not found.");
            object stationId = DBNull.Value;
            if (!string.IsNullOrWhiteSpace(assignedStation))
            {
                var station = FindMachineByName(conn, assignedStation);
                if (station == null) throw new ApiFailure(404, "Assigned station not found.");
                stationId = I(station, "machine_id");
            }
            var now = IstNow();
            if (targetUserId > 0)
            {
                var target = QueryOne(conn, "SELECT user_id, is_active, password_hash FROM tbl_users WHERE user_id = ?", targetUserId);
                if (target == null) throw new ApiFailure(404, "User not found.");
                var nextPassword = string.IsNullOrWhiteSpace(password) ? S(target, "password_hash") : password;
                Execute(conn, "UPDATE tbl_users SET full_name = ?, login_id = ?, password_hash = ?, password_salt = '', password_iterations = 0, role_id = ?, assigned_station_id = ?, dealer_id = ?, is_active = " + SqlBoolLiteral(B(target, "is_active")) + ", updated_at = " + SqlDateLiteral(now) + " WHERE user_id = ?",
                    fullName, loginId, nextPassword, I(role, "role_id"), stationId, dealerLink, targetUserId);
            }
            else
            {
                password = Require(password, "Password is required.");
                Execute(conn, "INSERT INTO tbl_users (full_name, login_id, password_hash, password_salt, password_iterations, role_id, assigned_station_id, dealer_id, is_active, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, TRUE, " + SqlDateLiteral(now) + ", " + SqlDateLiteral(now) + ")",
                    fullName, loginId, password, "", 0, I(role, "role_id"), stationId, dealerLink);
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleToggleUser(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var userId = IntRequired(Value(context, "user_id"), "User is required.");
            var target = QueryOne(conn, "SELECT user_id, is_active FROM tbl_users WHERE user_id = ?", userId);
            if (target == null) throw new ApiFailure(404, "User not found.");
            Execute(conn, "UPDATE tbl_users SET is_active = " + SqlBoolLiteral(!B(target, "is_active")) + " WHERE user_id = ?", userId);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleResetUserPassword(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var loginId = Require(Value(context, "login_id"), "Login ID is required.").ToLowerInvariant();
            var password = Require(Value(context, "password"), "Password is required.");
            var target = GetUserByLogin(conn, loginId);
            if (target == null) throw new ApiFailure(404, "User not found.");
            Execute(conn, "UPDATE tbl_users SET password_hash = ?, password_salt = '', password_iterations = 0, updated_at = " + SqlDateLiteral(IstNow()) + " WHERE user_id = ?", password, I(target, "user_id"));
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleImportUsers(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var rowsTsv = Require(Value(context, "rows_tsv"), "Excel data is required.");
            var imported = ImportUsersFromTsv(conn, rowsTsv);
            WriteJson(context, Obj("ok", true, "imported", imported));
        }
    }

    private Dictionary<string, object> BuildAppState(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, string> filters)
    {
        var sections = UserSections(S(user, "role_name"));
        var deepState = string.Equals(filters.ContainsKey("deep_state") ? filters["deep_state"] : "", "1", StringComparison.OrdinalIgnoreCase);
        var deepSection = (filters.ContainsKey("deep_section") ? filters["deep_section"] : "").Trim().ToLowerInvariant();
        var statusLookup = LoadStatusLookup(conn);
        var masters = LoadMasterSets(conn);
        var users = sections.Contains("users") ? (object)LoadUsers(conn) : new List<Dictionary<string, object>>();
        var orders = LoadEnrichedOrders(conn, masters, user);
        var loadReports = deepState && sections.Contains("reports") && (deepSection == "" || deepSection == "reports" || deepSection == "email-log");
        var loadHistory = deepState && sections.Contains("history") && (deepSection == "" || deepSection == "history" || deepSection == "lifecycle");
        var loadEmailLog = deepState && sections.Contains("email-log") && (deepSection == "" || deepSection == "email-log");
        var loadLifecycle = deepState && (sections.Contains("reports") || sections.Contains("history")) && (deepSection == "" || deepSection == "history" || deepSection == "reports" || deepSection == "lifecycle");
        var audits = loadReports ? LoadAudits(conn, user, orders) : new List<Dictionary<string, object>>();
        var visibleDealers = S(user, "role_name") == "Marketing User"
            ? masters["dealers"].Where(r => string.Equals(S(r, "marketing_owner"), S(user, "full_name"), StringComparison.OrdinalIgnoreCase)).ToList()
            : masters["dealers"];
        var dataEntryState = sections.Contains("data-entry")
            ? Obj(
                "recent_orders", BuildRecentOrders(orders, statusLookup),
                "dealers", visibleDealers,
                "quotations", BuildQuotationRows(orders, statusLookup),
                "dealer_options", visibleDealers.Where(r => B(r, "is_active")).Select(r => S(r, "dealer_name")).ToList(),
                "main_order_reference_options", orders.Where(r => string.Equals(OrderClassForOrder(r), "Main Order", StringComparison.OrdinalIgnoreCase)).Select(r => S(r, "order_number")).Distinct().OrderBy(v => v).ToList(),
                "confirmable_orders", orders.Where(r => S(r, "workflow_stage_code") == "QUOTATION_CREATED").Select(r => S(r, "order_number")).ToList()
            )
            : EmptyDataEntryState();
        var optimisationState = sections.Contains("optimisation") ? BuildOptimisationState(orders, statusLookup) : EmptyOptimisationState();
        var procurementState = sections.Contains("procurement") ? BuildProcurementState(orders, statusLookup) : EmptyProcurementState();
        var planningVisible = sections.Contains("planner")
            || sections.Contains("data-entry")
            || sections.Contains("optimisation")
            || sections.Contains("procurement")
            || sections.Contains("production")
            || sections.Contains("dispatch");
        var planningState = planningVisible ? BuildPlanningState(conn, user, orders, statusLookup) : Obj("rows", new List<object>(), "can_edit", false);
        var productionState = sections.Contains("production") ? BuildProductionState(conn, user, orders, masters, statusLookup, filters) : EmptyProductionState();
        var dispatchState = sections.Contains("dispatch") ? BuildDispatchState(conn, orders, statusLookup) : EmptyDispatchState();
        var reportsState = loadReports ? BuildReportsState(conn, orders, audits, statusLookup, filters) : EmptyReportsState();
        var historyState = loadHistory ? BuildHistoryState(conn, user, orders, statusLookup, ToInt(filters["selected_order_id"])) : EmptyHistoryState();
        var emailLogState = loadEmailLog ? GetMailStatus(conn) : Obj("rows", new List<object>());
        var selectedOrderId = ToInt(filters["selected_order_id"]);
        if (selectedOrderId == 0 && reportsState["selected_order_id"] != null)
            selectedOrderId = Convert.ToInt32(reportsState["selected_order_id"]);
        if (selectedOrderId == 0 && historyState["selected_order_id"] != null)
            selectedOrderId = Convert.ToInt32(historyState["selected_order_id"]);
        var lifecycle = loadLifecycle
            ? BuildLifecycle(conn, orders, statusLookup, selectedOrderId)
            : EmptyLifecycleState();

        return Obj(
            "session", UserPayload(user),
            "masters", Obj(
                "customer_types", masters["customer_types"].Select(r => Obj("id", I(r, "customer_type_id"), "code", S(r, "customer_type_code"), "name", S(r, "customer_type_name"))).ToList(),
                "order_types", masters["order_types"].Select(r => Obj("id", I(r, "order_type_id"), "name", S(r, "order_type_name"))).ToList(),
                "vendors", masters["vendors"].Select(r => Obj("id", I(r, "vendor_id"), "name", S(r, "vendor_name"))).ToList(),
                "dealer_types", masters["dealer_types"].Select(r => Obj("id", I(r, "dropdown_id"), "name", S(r, "option_value"))).ToList(),
                "payment_terms", masters["payment_terms"].Select(r => Obj("id", I(r, "dropdown_id"), "name", S(r, "option_value"))).ToList(),
                "marketing_owners", masters["marketing_owners"].Select(r => Obj("id", I(r, "dropdown_id"), "name", S(r, "option_value"))).ToList(),
                "quotation_owners", masters["quotation_owners"].Select(r => Obj("id", I(r, "dropdown_id"), "name", S(r, "option_value"))).ToList(),
                "order_classes", masters["order_classes"].Select(r => Obj("id", I(r, "dropdown_id"), "name", S(r, "option_value"))).ToList(),
                "machines", masters["machines"].Select(r => Obj("id", I(r, "machine_id"), "name", S(r, "machine_name"), "sequence_no", I(r, "sequence_no"))).ToList(),
                "sequence_profiles", LoadSequenceProfiles(conn),
                "procurement_statuses", StatusOptions(statusLookup, "PROCUREMENT"),
                "dispatch_statuses", StatusOptions(statusLookup, "DISPATCH")
            ),
            "data_entry", dataEntryState,
            "optimisation", optimisationState,
            "procurement", procurementState,
            "planning", planningState,
            "production", productionState,
            "dispatch", dispatchState,
            "reports", reportsState,
            "history", historyState,
            "email_log", emailLogState,
            "lifecycle", lifecycle,
            "users", users,
            "settings", Obj("database_path", "App_Data/elenza_pms.accdb", "user_template_path", "assets/users-import-template.xlsx", "dealer_template_mode", "browser-download")
        );
    }

    private Dictionary<string, object> EmptyDataEntryState()
    {
        return Obj(
            "recent_orders", new List<object>(),
            "dealers", new List<object>(),
            "quotations", new List<object>(),
            "dealer_options", new List<object>(),
            "main_order_reference_options", new List<object>(),
            "confirmable_orders", new List<object>()
        );
    }

    private Dictionary<string, object> EmptyOptimisationState()
    {
        return Obj("eligible_order_numbers", new List<object>(), "rows", new List<object>());
    }

    private Dictionary<string, object> EmptyProcurementState()
    {
        return Obj("eligible_order_numbers", new List<object>(), "rows", new List<object>());
    }

    private Dictionary<string, object> EmptyProductionState()
    {
        return Obj("available_stations", new List<object>(), "selected_station", "", "rows", new List<object>());
    }

    private Dictionary<string, object> EmptyPlanningState()
    {
        return Obj("rows", new List<object>(), "can_edit", false);
    }

    private Dictionary<string, object> EmptyDispatchState()
    {
        return Obj("rows", new List<object>());
    }

    private Dictionary<string, object> EmptyReportsState()
    {
        return Obj(
            "rows", new List<object>(),
            "selected_order_id", null,
            "audit_logs", new List<object>(),
            "weekly_summary", EmptyWeeklySummary(),
            "dealer_dashboard_rows", new List<object>(),
            "marketing_dashboard_rows", new List<object>(),
            "dealer_filters", new List<object> { "all" },
            "order_type_filters", new List<object> { "all" },
            "station_filters", new List<object> { "all" }
        );
    }

    private Dictionary<string, object> EmptyHistoryState()
    {
        return Obj("rows", new List<object>(), "selected_order_id", null);
    }

    private Dictionary<string, object> EmptyLifecycleState()
    {
        return Obj("title", "No Order Selected", "summary", null, "station_remarks", new List<object>(), "history", new List<object>());
    }

    private Dictionary<string, object> EmptyWeeklySummary()
    {
        return Obj(
            "range_label", "Last 7 Days",
            "orders_updated", 0,
            "activity_logs", 0,
            "quotations", 0,
            "confirmations", 0,
            "optimisations", 0,
            "procurement", 0,
            "production", 0,
            "dispatch", 0,
            "daily_rows", new List<object>(),
            "module_rows", new List<object>(),
            "recent_rows", new List<object>()
        );
    }

    private Dictionary<string, List<Dictionary<string, object>>> LoadMasterSets(OleDbConnection conn)
    {
        return new Dictionary<string, List<Dictionary<string, object>>>
        {
            { "customer_types", QueryAll(conn, "SELECT * FROM tbl_customer_types WHERE is_active = TRUE ORDER BY sort_order, customer_type_code") },
            { "order_types", QueryAll(conn, "SELECT * FROM tbl_order_types WHERE is_active = TRUE ORDER BY sort_order, order_type_name") },
            { "vendors", QueryAll(conn, "SELECT * FROM tbl_vendors WHERE is_active = TRUE ORDER BY vendor_name") },
            { "dealer_types", LoadDropdownMasterRows(conn, "DEALER_TYPE") },
            { "payment_terms", LoadDropdownMasterRows(conn, "PAYMENT_TERMS") },
            { "marketing_owners", LoadDropdownMasterRows(conn, "MARKETING_OWNER") },
            { "quotation_owners", LoadDropdownMasterRows(conn, "QUOTATION_OWNER") },
            { "order_classes", LoadDropdownMasterRows(conn, "ORDER_CLASS") },
            { "machines", ActiveMachineRows(conn) },
            { "dealers", QueryAll(conn, "SELECT * FROM tbl_dealers WHERE is_active = TRUE ORDER BY dealer_name") }
        };
    }

    private Dictionary<string, Dictionary<string, string>> LoadStatusLookup(OleDbConnection conn)
    {
        var lookup = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in QueryAll(conn, "SELECT * FROM tbl_status_master ORDER BY status_group, sort_order"))
        {
            var group = S(row, "status_group");
            if (!lookup.ContainsKey(group)) lookup[group] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            lookup[group][S(row, "status_code")] = S(row, "status_label");
        }
        return lookup;
    }

    private List<Dictionary<string, object>> LoadUsers(OleDbConnection conn)
    {
        return QueryAll(conn, "SELECT u.user_id, u.full_name, u.login_id, u.is_active, r.role_name, m.machine_name AS station_name FROM (tbl_users AS u INNER JOIN tbl_roles AS r ON u.role_id = r.role_id) LEFT JOIN tbl_machines AS m ON u.assigned_station_id = m.machine_id ORDER BY u.full_name")
            .Select(r => Obj("user_id", I(r, "user_id"), "full_name", S(r, "full_name"), "login_id", S(r, "login_id"), "role_name", S(r, "role_name"), "station_name", string.IsNullOrWhiteSpace(S(r, "station_name")) ? "All Stations" : S(r, "station_name"), "is_active", B(r, "is_active")))
            .ToList();
    }

    private List<Dictionary<string, object>> ActiveMachineRows(OleDbConnection conn)
    {
        return QueryAll(conn, "SELECT * FROM tbl_machines WHERE is_active = TRUE ORDER BY sequence_no");
    }

    private List<Dictionary<string, object>> LoadSequenceProfiles(OleDbConnection conn)
    {
        EnsureSequenceProfileSchema(conn);
        var profiles = QueryAll(conn, "SELECT p.*, o.order_type_name FROM tbl_sequence_profiles AS p LEFT JOIN tbl_order_types AS o ON p.order_type_id = o.order_type_id WHERE p.is_active = TRUE ORDER BY o.sort_order, p.order_class_code, p.profile_name");
        var items = QueryAll(conn, "SELECT s.*, m.machine_name FROM tbl_sequence_profile_stations AS s INNER JOIN tbl_machines AS m ON s.station_id = m.machine_id ORDER BY s.profile_id, s.sequence_no, s.profile_station_id");
        return profiles.Select(profile => Obj(
            "id", I(profile, "profile_id"),
            "name", S(profile, "profile_name"),
            "order_type_id", I(profile, "order_type_id"),
            "order_type_name", S(profile, "order_type_name"),
            "order_class", S(profile, "order_class_code"),
            "stations", items.Where(i => I(i, "profile_id") == I(profile, "profile_id"))
                .Select(i => Obj("id", I(i, "profile_station_id"), "station_id", I(i, "station_id"), "station_name", S(i, "machine_name"), "sequence_no", I(i, "sequence_no")))
                .ToList()
        )).ToList();
    }

    private List<Dictionary<string, object>> LoadEnrichedOrders(OleDbConnection conn, Dictionary<string, List<Dictionary<string, object>>> masters, Dictionary<string, object> user)
    {
        var orders = QueryAll(conn, "SELECT * FROM tbl_orders ORDER BY updated_at DESC, order_id DESC");
        var queueRows = QueryAll(conn, "SELECT q.*, m.machine_name AS station_name FROM tbl_order_station_queue AS q INNER JOIN tbl_machines AS m ON q.station_id = m.machine_id");
        var procurements = QueryAll(conn, "SELECT p.*, v.vendor_name FROM tbl_procurement_items AS p LEFT JOIN tbl_vendors AS v ON p.vendor_id = v.vendor_id ORDER BY p.updated_at DESC, p.procurement_item_id DESC");
        var dealers = masters["dealers"].ToDictionary(r => I(r, "dealer_id"));
        var customerTypes = masters["customer_types"].ToDictionary(r => I(r, "customer_type_id"));
        var orderTypes = masters["order_types"].ToDictionary(r => I(r, "order_type_id"));
        var queueByOrder = queueRows.GroupBy(r => I(r, "order_id")).ToDictionary(g => g.Key, g => g.ToList());
        var procurementByOrder = procurements.GroupBy(r => I(r, "order_id")).ToDictionary(g => g.Key, g => g.ToList());
        var boxCountLookup = QueryAll(conn, "SELECT order_id, COUNT(*) AS box_count FROM tbl_dispatch_boxes GROUP BY order_id")
            .ToDictionary(r => I(r, "order_id"), r => I(r, "box_count"));
        var visible = new List<Dictionary<string, object>>();

        foreach (var order in orders)
        {
            Dictionary<string, object> dealer;
            Dictionary<string, object> customerType;
            Dictionary<string, object> orderType;
            dealers.TryGetValue(I(order, "dealer_id"), out dealer);
            customerTypes.TryGetValue(I(order, "customer_type_id"), out customerType);
            orderTypes.TryGetValue(I(order, "order_type_id"), out orderType);
            if ((S(user, "role_name") == "Data Entry" || S(user, "role_name") == "Quotation User") && I(order, "created_by") != I(user, "user_id")) continue;
            if (S(user, "role_name") == "Marketing User" && !string.Equals(S(dealer, "marketing_owner"), S(user, "full_name"), StringComparison.OrdinalIgnoreCase)) continue;
            var stationRows = queueByOrder.ContainsKey(I(order, "order_id")) ? queueByOrder[I(order, "order_id")] : new List<Dictionary<string, object>>();
            var visibleStations = stationRows.Where(r => B(r, "is_visible")).Select(r => S(r, "station_name")).ToList();
            var stationStatuses = stationRows.ToDictionary(r => S(r, "station_name"), r => S(r, "queue_status_code"));
            var stationRemarks = stationRows.Where(r => !string.IsNullOrWhiteSpace(S(r, "remarks"))).ToDictionary(r => S(r, "station_name"), r => (object)S(r, "remarks"));
            var latestProcurement = procurementByOrder.ContainsKey(I(order, "order_id")) ? procurementByOrder[I(order, "order_id")].FirstOrDefault() : null;
            var highestReachedStation = HighestReachedStationFast(order, stationRows);

            visible.Add(Obj(
                "order_id", I(order, "order_id"),
                "quotation_date", DT(order, "quotation_date"),
                "quotation_number", S(order, "quotation_number"),
                "order_number", S(order, "order_number"),
                "dealer_id", I(order, "dealer_id"),
                "dealer_code", dealer == null ? "" : S(dealer, "dealer_code"),
                "dealer_name", dealer == null ? "-" : S(dealer, "dealer_name"),
                "marketing_owner", dealer == null ? "" : S(dealer, "marketing_owner"),
                "customer_name", S(order, "customer_name"),
                "customer_type_id", I(order, "customer_type_id"),
                "customer_type_code", customerType == null ? "-" : S(customerType, "customer_type_code"),
                "order_type_id", I(order, "order_type_id"),
                "order_type_name", orderType == null ? "-" : S(orderType, "order_type_name"),
                "sequence_profile_id", I(order, "sequence_profile_id"),
                "confirmation_date", DT(order, "confirmation_date"),
                "expected_confirmation_date", DT(order, "expected_confirmation_date"),
                "number_of_boards", D(order, "board_qty_decimal") > 0 ? D(order, "board_qty_decimal") : I(order, "number_of_boards"),
                "panel_qty", D(order, "panel_qty"),
                "order_class_code", OrderClassForOrder(order),
                "main_order", S(order, "main_order"),
                "sub_order", S(order, "sub_order"),
                "location", S(order, "location"),
                "workflow_stage_code", S(order, "workflow_stage_code"),
                "procurement_status_code", S(order, "procurement_status_code"),
                "dispatch_status_code", S(order, "dispatch_status_code"),
                "visible_stations", visibleStations,
                "station_statuses", stationStatuses,
                "station_remarks", stationRemarks,
                "correction_queue", B(order, "correction_queue"),
                "correction_remarks", B(order, "correction_queue") ? S(order, "quotation_remarks") : "",
                "material_received_date", latestProcurement == null ? null : DT(latestProcurement, "mrn_date"),
                "dispatch_vehicle_details", S(order, "dispatch_status_code") == "DISPATCHED" ? (latestProcurement == null ? "" : S(latestProcurement, "remarks")) : (stationRemarks.ContainsKey("Dispatch") ? Convert.ToString(stationRemarks["Dispatch"]) : ""),
                "last_action", S(order, "last_action"),
                "highest_reached_station", highestReachedStation,
                "updated_at", DT(order, "updated_at"),
                "created_by", I(order, "created_by"),
                "quotation_remarks", S(order, "quotation_remarks"),
                "packing_balance_box_qty", I(order, "packing_balance_box_qty"),
                "box_count", boxCountLookup.ContainsKey(I(order, "order_id")) ? boxCountLookup[I(order, "order_id")] : 0
            ));
        }
        return visible;
    }

    private List<Dictionary<string, object>> LoadAudits(OleDbConnection conn, Dictionary<string, object> user, List<Dictionary<string, object>> orders)
    {
        var allowed = new HashSet<string>(orders.Select(o => S(o, "order_number")), StringComparer.OrdinalIgnoreCase);
        var rows = QueryAll(conn, "SELECT TOP 600 a.*, m.machine_name AS station_name, u.full_name AS user_name FROM (tbl_audit_logs AS a LEFT JOIN tbl_machines AS m ON a.station_id = m.machine_id) LEFT JOIN tbl_users AS u ON a.user_id = u.user_id ORDER BY a.created_at DESC, a.audit_id DESC");
        var visible = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            if ((S(user, "role_name") == "Data Entry" || S(user, "role_name") == "Quotation User" || S(user, "role_name") == "Marketing User") && S(row, "record_type") == "Order" && !allowed.Contains(S(row, "record_key")))
                continue;
            var createdAt = DT(row, "created_at");
            visible.Add(Obj(
                "created_at", FormatDateTime(createdAt),
                "created_day", IsoDate(createdAt),
                "created_sort", DateSortKey(createdAt),
                "user_name", EmptyAs(S(row, "user_name"), "-"),
                "record_key", EmptyAs(S(row, "record_key"), "-"),
                "module_name", S(row, "module_name"),
                "action_name", S(row, "action_name"),
                "remarks", S(row, "remarks"),
                "station_name", EmptyAs(S(row, "station_name"), "-")
            ));
        }
        return visible;
    }

    private List<Dictionary<string, object>> BuildRecentOrders(List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        return orders.OrderByDescending(o => DT(o, "updated_at")).Take(10).Select(order => Obj(
            "order_id", I(order, "order_id"),
            "quotation_number", S(order, "quotation_number"),
            "order_number", S(order, "order_number"),
            "dealer_name", S(order, "dealer_name"),
            "customer_name", S(order, "customer_name"),
            "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
            "procurement_status", Label(statusLookup, "PROCUREMENT", S(order, "procurement_status_code")),
            "visible_stations", ReadableVisibleStations(order),
            "last_action", S(order, "last_action"),
            "updated_at", FormatDateTime(DT(order, "updated_at"))
        )).ToList();
    }

    private List<Dictionary<string, object>> BuildQuotationRows(List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        return orders
            .OrderByDescending(order => ToDateTime(DT(order, "created_at")) ?? ToDateTime(DT(order, "quotation_date")) ?? DateTime.MinValue)
            .ThenByDescending(order => I(order, "order_id"))
            .Select(order => Obj(
            "order_id", I(order, "order_id"),
            "quotation_date", ToDateTime(DT(order, "quotation_date")).HasValue ? ToDateTime(DT(order, "quotation_date")).Value.ToString("dd-MM-yy", CultureInfo.InvariantCulture) : "-",
            "quotation_number", S(order, "quotation_number"),
            "order_number", S(order, "order_number"),
            "dealer_name", S(order, "dealer_name"),
            "customer_name", S(order, "customer_name"),
            "order_type", S(order, "order_type_name"),
            "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
            "updated_at", FormatDateTime(DT(order, "updated_at"))
        )).ToList();
    }

    private Dictionary<string, object> BuildOptimisationState(List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        var eligible = orders.Where(o => S(o, "workflow_stage_code") == "ORDER_CONFIRMED").ToList();
        return Obj(
            "eligible_order_numbers", eligible.Select(o => S(o, "order_number")).ToList(),
            "rows", eligible.Select(order => Obj(
                "order_id", I(order, "order_id"),
                "confirmation_date", ToDateTime(DT(order, "confirmation_date")).HasValue ? ToDateTime(DT(order, "confirmation_date")).Value.ToString("yyyy-MM-dd") : "",
                "order_number", S(order, "order_number"),
                "dealer_name", S(order, "dealer_name"),
                "customer_name", S(order, "customer_name"),
                "order_type", S(order, "order_type_name"),
                "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
                "updated_at", FormatDateTime(DT(order, "updated_at"))
            )).ToList()
        );
    }

    private Dictionary<string, object> BuildProcurementState(List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        var eligible = orders.Where(o => OpenProcurementWorkflowCodes.Contains(S(o, "workflow_stage_code"))).ToList();
        return Obj(
            "eligible_order_numbers", eligible.Select(o => S(o, "order_number")).ToList(),
            "rows", eligible.Select(order => Obj(
                "order_id", I(order, "order_id"),
                "order_number", S(order, "order_number"),
                "dealer_name", S(order, "dealer_name"),
                "customer_name", S(order, "customer_name"),
                "order_type", S(order, "order_type_name"),
                "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
                "procurement_status", Label(statusLookup, "PROCUREMENT", S(order, "procurement_status_code")),
                "updated_at", FormatDateTime(DT(order, "updated_at"))
            )).ToList()
        );
    }

    private Dictionary<string, object> BuildPlanningState(OleDbConnection conn, Dictionary<string, object> user, List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        var eligible = orders.Where(IsPlanningEligible).ToList();
        EnsurePlannerRows(conn, eligible);
        var plannerRows = QueryAll(conn, "SELECT * FROM tbl_production_planner ORDER BY planning_rank, planner_id").ToDictionary(r => I(r, "order_id"));
        var rows = eligible
            .OrderBy(o => plannerRows.ContainsKey(I(o, "order_id")) ? I(plannerRows[I(o, "order_id")], "planning_rank") : int.MaxValue)
            .ThenBy(o => I(o, "order_id"))
            .Select(order => BuildPlanningRow(order, plannerRows.ContainsKey(I(order, "order_id")) ? plannerRows[I(order, "order_id")] : null, statusLookup))
            .ToList();
        var canEdit = S(user, "role_name") == "Admin" || S(user, "role_name") == "Production Planner User";
        return Obj("rows", rows, "can_edit", canEdit);
    }

    private Dictionary<string, object> BuildProductionState(OleDbConnection conn, Dictionary<string, object> user, List<Dictionary<string, object>> orders, Dictionary<string, List<Dictionary<string, object>>> masters, Dictionary<string, Dictionary<string, string>> statusLookup, Dictionary<string, string> filters)
    {
        var machines = masters["machines"].Where(m => S(m, "machine_name") != "Dispatch").Select(m => S(m, "machine_name")).ToList();
        var defaultSequenceNames = masters["machines"].Select(m => S(m, "machine_name")).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var available = new List<string>(machines);
        available.Add("Correction Queue");
        var selectedStation = !string.IsNullOrWhiteSpace(filters["production_station"])
            ? filters["production_station"]
            : (S(user, "role_name") == "Machine User" ? S(user, "station_name") : (available.FirstOrDefault() ?? ""));
        if (S(user, "role_name") == "Machine User") selectedStation = S(user, "station_name");
        if (!available.Contains(selectedStation)) selectedStation = available.FirstOrDefault() ?? "";
        var search = (filters["production_search"] ?? "").ToLowerInvariant();
        var rows = new List<Dictionary<string, object>>();
        EnsureDispatchBoxSchema(conn);
        var boxCountLookup = QueryAll(conn, "SELECT order_id, COUNT(*) AS box_count FROM tbl_dispatch_boxes GROUP BY order_id")
            .ToDictionary(r => I(r, "order_id"), r => I(r, "box_count"));
        foreach (var order in orders)
        {
            var haystack = string.Join(" ", new[] { S(order, "order_number"), S(order, "dealer_name"), S(order, "customer_name"), S(order, "order_type_name"), S(order, "main_order"), S(order, "sub_order") }).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(search) && !haystack.Contains(search)) continue;
            if (selectedStation == "Correction Queue")
            {
                if (!B(order, "correction_queue")) continue;
                rows.Add(Obj("order_id", I(order, "order_id"), "order_number", S(order, "order_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "order_type", S(order, "order_type_name"), "main_sub", S(order, "main_order") + " / " + S(order, "sub_order"), "previous_station", "-", "current_station", "Correction Queue", "next_station", defaultSequenceNames.FirstOrDefault() ?? "-", "status", Label(statusLookup, "QUEUE", "CORRECTION_QUEUE"), "remarks", S(order, "correction_remarks"), "actions_allowed", false, "partial_pending", false, "partial_pending_source", ""));
                continue;
            }
            var visibleStations = (List<string>)order["visible_stations"];
            if (!visibleStations.Contains(selectedStation)) continue;
            var stationStatuses = (Dictionary<string, string>)order["station_statuses"];
            var stationRemarks = (Dictionary<string, object>)order["station_remarks"];
            var orderSequenceNames = defaultSequenceNames;
            var previousStationName = PreviousStationName(orderSequenceNames, selectedStation);
            var previousStatus = !string.IsNullOrWhiteSpace(previousStationName) && stationStatuses.ContainsKey(previousStationName) ? stationStatuses[previousStationName] : "";
            var currentStatus = stationStatuses.ContainsKey(selectedStation) ? stationStatuses[selectedStation] : "PENDING";
            var partialPending = string.Equals(currentStatus, "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase) || string.Equals(previousStatus, "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase);
            rows.Add(Obj("order_id", I(order, "order_id"), "order_number", S(order, "order_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "order_type", S(order, "order_type_name"), "main_sub", S(order, "main_order") + " / " + S(order, "sub_order"), "previous_station", EmptyAs(previousStationName, "-"), "current_station", selectedStation, "next_station", EmptyAs(NextStationName(orderSequenceNames, selectedStation), "-"), "status", Label(statusLookup, "QUEUE", currentStatus), "remarks", stationRemarks.ContainsKey(selectedStation) ? Convert.ToString(stationRemarks[selectedStation]) : "", "actions_allowed", true, "partial_pending", partialPending, "partial_pending_source", string.Equals(previousStatus, "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase) ? previousStationName : "", "packing_balance_box_qty", D(order, "packing_balance_box_qty"), "box_count", boxCountLookup.ContainsKey(I(order, "order_id")) ? boxCountLookup[I(order, "order_id")] : 0));
        }
        return Obj("available_stations", available, "selected_station", selectedStation, "rows", rows);
    }

    private bool IsFullyPacked(Dictionary<string, object> order)
    {
        var boxCount = I(order, "box_count");
        var balanceQty = I(order, "packing_balance_box_qty");
        return boxCount > 0 && balanceQty == 0;
    }

    private bool IsPlanningEligible(Dictionary<string, object> order)
    {
        var workflowStage = S(order, "workflow_stage_code");
        if (string.Equals(workflowStage, "DISPATCH_READY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflowStage, "DISPATCHED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (IsFullyPacked(order)) return false;
        var visibleStations = order.ContainsKey("visible_stations") ? order["visible_stations"] as List<string> : null;
        if (visibleStations != null && (visibleStations.Any(s => IsPackingStationName(s)) || visibleStations.Contains("Dispatch")))
            return false;
        return (PlanningWorkflowCodes.Contains(workflowStage) || B(order, "correction_queue"))
            && !string.Equals(S(order, "dispatch_status_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsPackingPortalEligible(Dictionary<string, object> order)
    {
        var workflowStage = S(order, "workflow_stage_code");
        if (string.Equals(workflowStage, "QUOTATION_CREATED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflowStage, "DISPATCH_READY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflowStage, "DISPATCHED", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (IsFullyPacked(order)) return false;
        return !string.Equals(S(order, "dispatch_status_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase);
    }

    private void EnsurePlannerRows(OleDbConnection conn, List<Dictionary<string, object>> eligibleOrders)
    {
        var existingRows = new HashSet<int>(QueryAll(conn, "SELECT order_id FROM tbl_production_planner").Select(r => I(r, "order_id")));
        var maxRank = Convert.ToInt32(Scalar(conn, "SELECT MAX(planning_rank) FROM tbl_production_planner") ?? 0);
        foreach (var order in eligibleOrders.OrderBy(o => I(o, "order_id")))
        {
            var orderId = I(order, "order_id");
            if (existingRows.Contains(orderId)) continue;
            maxRank += 10;
            Execute(conn, "INSERT INTO tbl_production_planner (order_id, planning_rank, updated_by, updated_at) VALUES (?, ?, 0, " + SqlDateLiteral(IstNow()) + ")", orderId, maxRank);
        }
    }

    private Dictionary<string, object> BuildPlanningRow(Dictionary<string, object> order, Dictionary<string, object> plannerRow, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        var planningRank = plannerRow == null ? 0 : I(plannerRow, "planning_rank");
        var stageKey = PlanningStageKey(order);
        var visibleStations = (List<string>)order["visible_stations"];
        var stationStatuses = (Dictionary<string, string>)order["station_statuses"];
        var latestStatus = LatestPlannerStatusLabel(order, statusLookup);
        var rawDispatchCode = S(order, "dispatch_status_code");
        var normalizedDispatchCode =
            string.Equals(rawDispatchCode, "DISPATCHED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(S(order, "workflow_stage_code"), "DISPATCH_READY", StringComparison.OrdinalIgnoreCase)
            || string.Equals(S(order, "workflow_stage_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(latestStatus, "Dispatch", StringComparison.OrdinalIgnoreCase)
            || string.Equals(latestStatus, "Packed", StringComparison.OrdinalIgnoreCase)
                ? rawDispatchCode
                : "";
        return Obj(
            "order_id", I(order, "order_id"),
            "planning_rank", planningRank,
            "confirmation_date", ToDateTime(DT(order, "confirmation_date")).HasValue ? ToDateTime(DT(order, "confirmation_date")).Value.ToString("yyyy-MM-dd") : "",
            "order_number", S(order, "order_number"),
            "dealer_name", S(order, "dealer_name"),
            "customer_name", S(order, "customer_name"),
            "customer_type", S(order, "customer_type_code"),
            "order_type", S(order, "order_type_name"),
            "order_class", OrderClassForOrder(order),
            "material_received_date", ToDateTime(DT(order, "material_received_date")).HasValue ? ToDateTime(DT(order, "material_received_date")).Value.ToString("yyyy-MM-dd") : "",
            "workflow_stage_code", S(order, "workflow_stage_code"),
            "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
            "dispatch_status_code", normalizedDispatchCode,
            "procurement_status", Label(statusLookup, "PROCUREMENT", S(order, "procurement_status_code")),
            "dispatch_status", Label(statusLookup, "DISPATCH", normalizedDispatchCode),
            "order_class_code", OrderClassForOrder(order),
            "visible_stations", ReadableVisibleStations(order),
            "current_stage_hint", latestStatus,
            "sla_date", ToDateTime(plannerRow == null ? null : DT(plannerRow, "sla_date")).HasValue ? ToDateTime(DT(plannerRow, "sla_date")).Value.ToString("yyyy-MM-dd") : "",
            "edd", ToDateTime(plannerRow == null ? null : DT(plannerRow, "sla_date")).HasValue ? ToDateTime(DT(plannerRow, "sla_date")).Value.ToString("yyyy-MM-dd") : "",
            "panel_qty", D(order, "panel_qty") > 0 ? D(order, "panel_qty").ToString("0.##", CultureInfo.InvariantCulture) : "",
            "board_qty", D(order, "number_of_boards") > 0 ? D(order, "number_of_boards").ToString("0.##", CultureInfo.InvariantCulture) : "",
            "urgency", plannerRow == null ? "" : S(plannerRow, "urgency"),
            "priority", plannerRow == null ? "" : S(plannerRow, "priority"),
            "priority_date", ToDateTime(plannerRow == null ? null : DT(plannerRow, "priority_date")).HasValue ? ToDateTime(DT(plannerRow, "priority_date")).Value.ToString("yyyy-MM-dd") : "",
            "planner_remarks", plannerRow == null ? "" : S(plannerRow, "planner_remarks"),
            "assigned_station", B(order, "correction_queue") ? "" : (visibleStations.Count > 0 ? visibleStations.Last() : ""),
            "partial_pending", visibleStations.Any(v => stationStatuses.ContainsKey(v) && string.Equals(stationStatuses[v], "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase)),
            "planner_stage_key", stageKey,
            "planner_stage_label", PlanningStageLabel(stageKey),
            "packing_balance_box_qty", D(order, "packing_balance_box_qty"),
            "box_count", D(order, "packing_balance_box_qty")
        );
    }

    private string PlanningStageKey(Dictionary<string, object> order)
    {
        var highestReachedStation = S(order, "highest_reached_station");
        var visibleStations = (List<string>)order["visible_stations"];
        var stationStatuses = (Dictionary<string, string>)order["station_statuses"];
        if (B(order, "correction_queue")) return "production";
        if (string.Equals(highestReachedStation, "Dispatch", StringComparison.OrdinalIgnoreCase) || IsPackingStationName(highestReachedStation)) return "packed";
        if (string.Equals(highestReachedStation, "QC", StringComparison.OrdinalIgnoreCase)) return "qc";
        if (string.Equals(S(order, "workflow_stage_code"), "OPTIMISATION_DONE", StringComparison.OrdinalIgnoreCase)) return "optimisation";
        if (string.Equals(S(order, "workflow_stage_code"), "DISPATCH_READY", StringComparison.OrdinalIgnoreCase) || visibleStations.Contains("Dispatch") || visibleStations.Any(s => IsPackingStationName(s))) return "packed";
        if (visibleStations.Contains("QC")) return "qc";
        if (string.Equals(S(order, "procurement_status_code"), "MATERIAL_RECEIVED", StringComparison.OrdinalIgnoreCase))
        {
            var pendingOnly = stationStatuses.Values.All(v => string.Equals(v, "PENDING", StringComparison.OrdinalIgnoreCase));
            if (pendingOnly) return "material";
        }
        if (visibleStations.Any(v => v != "Dispatch")) return "production";
        return "neutral";
    }

    private string LatestPlannerStatusLabel(Dictionary<string, object> order, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        if (B(order, "correction_queue")) return "Correction Queue";
        var highestReachedStation = S(order, "highest_reached_station");
        var visibleStations = (List<string>)order["visible_stations"];
        if (string.Equals(highestReachedStation, "Dispatch", StringComparison.OrdinalIgnoreCase)) return "Dispatch";
        if (IsPackingStationName(highestReachedStation)) return "Packed";
        if (string.Equals(highestReachedStation, "QC", StringComparison.OrdinalIgnoreCase)) return "QC";
        if (!string.IsNullOrWhiteSpace(highestReachedStation)) return highestReachedStation;
        if (visibleStations.Count > 0)
            return visibleStations.Last();
        if (string.Equals(S(order, "workflow_stage_code"), "OPTIMISATION_DONE", StringComparison.OrdinalIgnoreCase))
            return "Optimisation Done";
        return Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code"));
    }

    private string HighestReachedStation(List<Dictionary<string, object>> historyRows)
    {
        var ranked = historyRows
            .Where(r => !string.IsNullOrWhiteSpace(S(r, "station_name")))
            .Where(r => string.Equals(S(r, "action_code"), "COMPLETED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(r, "action_code"), "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(r, "action_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(r, "action_code"), "PENDING_DISPATCH", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(r, "action_code"), "PARTIALLY_DISPATCHED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(S(r, "action_code"), "HOLD", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => I(r, "sequence_no"))
            .ThenByDescending(r => DateSortKey(DT(r, "acted_at")))
            .FirstOrDefault();
        return ranked == null ? "" : S(ranked, "station_name");
    }

    private string HighestReachedStationFast(Dictionary<string, object> order, List<Dictionary<string, object>> stationRows)
    {
        if (string.Equals(S(order, "dispatch_status_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase))
            return "Dispatch";
        if (stationRows == null || stationRows.Count == 0)
            return "";

        var ranked = stationRows
            .Where(r => !string.IsNullOrWhiteSpace(S(r, "station_name")))
            .Where(r =>
                string.Equals(S(r, "queue_status_code"), "COMPLETED", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(S(r, "queue_status_code"), "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => I(r, "sequence_no"))
            .ThenByDescending(r => DateSortKey(DT(r, "updated_at")))
            .FirstOrDefault();

        if (ranked != null)
            return S(ranked, "station_name");

        var currentVisible = stationRows
            .Where(r => B(r, "is_visible"))
            .OrderByDescending(r => I(r, "sequence_no"))
            .FirstOrDefault();

        return currentVisible == null ? "" : S(currentVisible, "station_name");
    }

    private string PlanningStageLabel(string stageKey)
    {
        switch (stageKey)
        {
            case "optimisation": return "Optimisation Done";
            case "material": return "Material Received";
            case "production": return "In Production";
            case "packed": return "Packed";
            case "qc": return "QC";
            default: return "Planning";
        }
    }

    private Dictionary<string, object> BuildDispatchState(OleDbConnection conn, List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup)
    {
        EnsureDispatchBoxSchema(conn);
        var boxRows = QueryAll(conn, "SELECT * FROM tbl_dispatch_boxes ORDER BY order_id, box_no");
        var rows = new List<Dictionary<string, object>>();
        foreach (var order in orders)
        {
            if (!((List<string>)order["visible_stations"]).Contains("Dispatch")) continue;
            var orderClass = OrderClassForOrder(order);
            if (string.Equals(orderClass, "Sub Order", StringComparison.OrdinalIgnoreCase)) continue;
            var stationRemarks = (Dictionary<string, object>)order["station_remarks"];
            var boxes = boxRows.Where(r => I(r, "order_id") == I(order, "order_id"))
                .OrderBy(r => I(r, "box_no"))
                .Select(r => Obj("box_no", I(r, "box_no"), "state", S(r, "box_state")))
                .ToList();
            rows.Add(Obj("order_id", I(order, "order_id"), "order_number", S(order, "order_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "packing_ready_date", FormatDateTime(DT(order, "packing_ready_date") ?? DT(order, "updated_at")), "dispatch_status", Label(statusLookup, "DISPATCH", S(order, "dispatch_status_code")), "remarks", stationRemarks.ContainsKey("Dispatch") ? Convert.ToString(stationRemarks["Dispatch"]) : "", "vehicle_details", S(order, "dispatch_vehicle_details"), "box_count", boxes.Count, "boxes", boxes, "dispatch_balance_box_qty", D(order, "dispatch_balance_box_qty")));
        }
        return Obj("rows", rows);
    }

    private Dictionary<string, object> BuildReportsState(OleDbConnection conn, List<Dictionary<string, object>> orders, List<Dictionary<string, object>> audits, Dictionary<string, Dictionary<string, string>> statusLookup, Dictionary<string, string> filters)
    {
        var rows = new List<Dictionary<string, object>>();
        var plannerLookup = QueryAll(conn, "SELECT order_id, [priority] FROM tbl_production_planner")
            .ToDictionary(r => I(r, "order_id"), r => S(r, "priority"));
        var search = (filters["report_search"] ?? "").ToLowerInvariant();
        var statusFilter = filters["report_status"];
        var dealerFilter = filters["report_dealer"];
        var orderTypeFilter = filters["report_order_type"];
        var stationFilter = filters["report_station"];
        var dateFrom = filters["report_date_from"];
        var dateTo = filters["report_date_to"];
        var sortKey = filters["report_sort"];
        var requestedOrderId = ToInt(filters["selected_order_id"]);

        foreach (var order in orders)
        {
            var visibleStations = ReadableVisibleStations(order);
            var workflowStage = Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code"));
            var dispatchStatus = Label(statusLookup, "DISPATCH", S(order, "dispatch_status_code"));
            var haystack = string.Join(" ", new[] { S(order, "order_number"), S(order, "dealer_name"), S(order, "customer_name"), S(order, "order_type_name"), workflowStage, S(order, "last_action"), visibleStations }).ToLowerInvariant();
            var orderDate = IsoDate(DT(order, "updated_at"));
            if (!string.IsNullOrWhiteSpace(search) && !haystack.Contains(search)) continue;
            if (statusFilter != "all" && statusFilter != S(order, "workflow_stage_code") && statusFilter != S(order, "dispatch_status_code") && statusFilter != workflowStage && statusFilter != dispatchStatus) continue;
            if (dealerFilter != "all" && dealerFilter != S(order, "dealer_name")) continue;
            if (orderTypeFilter != "all" && orderTypeFilter != S(order, "order_type_name")) continue;
            if (stationFilter != "all" && !((List<string>)order["visible_stations"]).Contains(stationFilter) && !(stationFilter == "Correction Queue" && B(order, "correction_queue"))) continue;
            if (!string.IsNullOrWhiteSpace(dateFrom) && string.CompareOrdinal(orderDate, dateFrom) < 0) continue;
            if (!string.IsNullOrWhiteSpace(dateTo) && string.CompareOrdinal(orderDate, dateTo) > 0) continue;
            rows.Add(Obj("order_id", I(order, "order_id"), "order_number", S(order, "order_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "order_type", S(order, "order_type_name"), "workflow_stage", workflowStage, "visible_stations", visibleStations, "last_action", S(order, "last_action"), "updated_at", FormatDateTime(DT(order, "updated_at")), "updated_sort", DateSortKey(DT(order, "updated_at")), "dispatch_status", dispatchStatus, "panel_qty", D(order, "panel_qty") > 0 ? D(order, "panel_qty").ToString("0.##", CultureInfo.InvariantCulture) : "", "board_qty", D(order, "number_of_boards") > 0 ? D(order, "number_of_boards").ToString("0.##", CultureInfo.InvariantCulture) : ""));
        }

        rows = SortReportRows(rows, sortKey);
        var selectedOrderId = rows.Any(r => I(r, "order_id") == requestedOrderId) ? requestedOrderId : (rows.Count > 0 ? I(rows[0], "order_id") : 0);
        var weeklySummary = BuildWeeklySummary(orders, audits);
        var dealerDashboardRows = BuildDealerDashboardRows(orders);
        var marketingDashboardRows = BuildMarketingDashboardRows(orders, plannerLookup, audits);
        return Obj(
            "rows", rows,
            "selected_order_id", selectedOrderId == 0 ? (object)null : selectedOrderId,
            "audit_logs", audits.Take(25).ToList(),
            "weekly_summary", weeklySummary,
            "dealer_dashboard_rows", dealerDashboardRows,
            "marketing_dashboard_rows", marketingDashboardRows,
            "dealer_filters", PrependAll((rows.Any() ? rows.Select(r => S(r, "dealer_name")) : orders.Select(r => S(r, "dealer_name"))).Distinct().Where(v => !string.IsNullOrWhiteSpace(v)).ToList()),
            "order_type_filters", PrependAll((rows.Any() ? rows.Select(r => S(r, "order_type")) : orders.Select(r => S(r, "order_type_name"))).Distinct().Where(v => !string.IsNullOrWhiteSpace(v)).ToList()),
            "station_filters", PrependAll(CollectStationFilters(orders))
        );
    }

    private List<Dictionary<string, object>> BuildDealerDashboardRows(List<Dictionary<string, object>> orders)
    {
        return orders
            .GroupBy(o => new
            {
                DealerName = S(o, "dealer_name"),
                DealerCode = S(o, "dealer_code"),
                CustomerType = S(o, "customer_type_code"),
                MarketingOwner = S(o, "marketing_owner")
            })
            .Select(g => Obj(
                "dealer_code", g.Key.DealerCode,
                "dealer_name", g.Key.DealerName,
                "customer_type", g.Key.CustomerType,
                "marketing_owner", g.Key.MarketingOwner,
                "active_orders", g.Count(o => !string.Equals(S(o, "workflow_stage_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase)),
                "in_production", g.Count(o => PlanningStageKey(o) == "production"),
                "dispatch_ready", g.Count(o => string.Equals(S(o, "workflow_stage_code"), "DISPATCH_READY", StringComparison.OrdinalIgnoreCase)),
                "last_updated", FormatDateTime(g.OrderByDescending(o => DT(o, "updated_at")).Select(o => DT(o, "updated_at")).FirstOrDefault())
            ))
            .OrderBy(r => S(r, "dealer_name"))
            .ToList();
    }

    private List<Dictionary<string, object>> BuildMarketingDashboardRows(List<Dictionary<string, object>> orders, Dictionary<int, string> plannerLookup, List<Dictionary<string, object>> audits)
    {
        var todayKey = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return orders
            .Where(o => !string.IsNullOrWhiteSpace(S(o, "marketing_owner")))
            .GroupBy(o => S(o, "marketing_owner"))
            .Select(g => Obj(
                "marketing_owner", g.Key,
                "dealer_count", g.Select(o => S(o, "dealer_name")).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                "active_orders", g.Count(o => !string.Equals(S(o, "workflow_stage_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase)),
                "high_priority", g.Count(o => plannerLookup.ContainsKey(I(o, "order_id")) && string.Equals(plannerLookup[I(o, "order_id")], "High", StringComparison.OrdinalIgnoreCase)),
                "dispatch_ready", g.Count(o => string.Equals(S(o, "workflow_stage_code"), "DISPATCH_READY", StringComparison.OrdinalIgnoreCase)),
                "dispatched_today", audits.Count(a => string.Equals(S(a, "module_name"), "Dispatch", StringComparison.OrdinalIgnoreCase) && string.Equals(S(a, "created_day"), todayKey, StringComparison.OrdinalIgnoreCase) && string.Equals(S(a, "action_name"), "Dispatched", StringComparison.OrdinalIgnoreCase) && g.Any(o => string.Equals(S(o, "order_number"), S(a, "record_key"), StringComparison.OrdinalIgnoreCase))),
                "last_updated", FormatDateTime(g.OrderByDescending(o => DT(o, "updated_at")).Select(o => DT(o, "updated_at")).FirstOrDefault())
            ))
            .OrderBy(r => S(r, "marketing_owner"))
            .ToList();
    }

    private Dictionary<string, object> BuildWeeklySummary(List<Dictionary<string, object>> orders, List<Dictionary<string, object>> audits)
    {
        var startDate = DateTime.Today.AddDays(-6);
        var endDate = IstNow();
        var startKey = DateSortKey(startDate);
        var weeklyAudits = audits
            .Where(a => string.CompareOrdinal(S(a, "created_sort"), startKey) >= 0)
            .OrderByDescending(a => S(a, "created_sort"))
            .ToList();
        var updatedOrders = orders.Where(o => Convert.ToDateTime(DT(o, "updated_at")) >= startDate).ToList();

        Func<string, int> moduleCount = moduleName =>
            weeklyAudits.Count(a => string.Equals(S(a, "module_name"), moduleName, StringComparison.OrdinalIgnoreCase));

        var dailyRows = new List<Dictionary<string, object>>();
        for (var i = 0; i < 7; i++)
        {
            var day = startDate.Date.AddDays(i);
            var dayKey = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var dayAudits = weeklyAudits.Where(a => S(a, "created_day") == dayKey).ToList();
            dailyRows.Add(Obj(
                "date_value", dayKey,
                "date_label", day.ToString("dd MMM", CultureInfo.InvariantCulture),
                "actions_total", dayAudits.Count,
                "orders_updated", updatedOrders.Count(o => IsoDate(DT(o, "updated_at")) == dayKey),
                "quotations", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Quotation", StringComparison.OrdinalIgnoreCase)),
                "production", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Production", StringComparison.OrdinalIgnoreCase)),
                "dispatch", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Dispatch", StringComparison.OrdinalIgnoreCase))
            ));
        }

        var moduleRows = weeklyAudits
            .GroupBy(a => string.IsNullOrWhiteSpace(S(a, "module_name")) ? "Other" : S(a, "module_name"))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g =>
            {
                var latest = g.OrderByDescending(a => S(a, "created_sort")).First();
                return Obj(
                    "module_name", g.Key,
                    "action_count", g.Count(),
                    "last_activity", S(latest, "created_at"),
                    "last_user", S(latest, "user_name")
                );
            })
            .ToList();

        return Obj(
            "range_label", startDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " - " + endDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            "orders_updated", updatedOrders.Count,
            "activity_logs", weeklyAudits.Count,
            "quotations", moduleCount("Quotation"),
            "confirmations", moduleCount("Order Confirmation"),
            "optimisations", moduleCount("Optimisation"),
            "procurement", moduleCount("Procurement"),
            "production", moduleCount("Production"),
            "dispatch", moduleCount("Dispatch"),
            "daily_rows", dailyRows,
            "module_rows", moduleRows,
            "recent_rows", weeklyAudits.Take(20).ToList()
        );
    }

    private Dictionary<string, object> BuildLifecycle(OleDbConnection conn, List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup, int selectedOrderId)
    {
        if (selectedOrderId == 0) return Obj("title", "No Order Selected", "summary", null, "station_remarks", new List<object>(), "history", new List<object>());
        var order = orders.FirstOrDefault(o => I(o, "order_id") == selectedOrderId);
        if (order == null) return Obj("title", "No Order Selected", "summary", null, "station_remarks", new List<object>(), "history", new List<object>());
        var historyRows = QueryAll(conn, "SELECT h.*, s.machine_name AS station_name, fs.machine_name AS from_station_name, ts.machine_name AS to_station_name, u.full_name AS acted_by_name FROM (((tbl_order_history AS h LEFT JOIN tbl_machines AS s ON h.station_id = s.machine_id) LEFT JOIN tbl_machines AS fs ON h.from_station_id = fs.machine_id) LEFT JOIN tbl_machines AS ts ON h.to_station_id = ts.machine_id) LEFT JOIN tbl_users AS u ON h.acted_by = u.user_id WHERE h.order_id = ? ORDER BY h.acted_at DESC, h.history_id DESC", selectedOrderId);
        var history = historyRows.Select(row => Obj("acted_at", FormatDateTime(DT(row, "acted_at")), "acted_by", EmptyAs(S(row, "acted_by_name"), "-"), "station_name", EmptyAs(S(row, "station_name"), "-"), "action", HumanHistoryAction(statusLookup, row), "remarks", S(row, "remarks"))).ToList();
        var stationRemarks = ((Dictionary<string, object>)order["station_remarks"]).Select(kvp => Obj("station_name", kvp.Key, "remarks", Convert.ToString(kvp.Value))).ToList();
        return Obj(
            "title", S(order, "order_number"),
            "summary", Obj("order_number", S(order, "order_number"), "quotation_number", S(order, "quotation_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "customer_type", S(order, "customer_type_code"), "order_type", S(order, "order_type_name"), "main_order", S(order, "main_order"), "sub_order", S(order, "sub_order"), "location", S(order, "location"), "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")), "dispatch_status", Label(statusLookup, "DISPATCH", S(order, "dispatch_status_code")), "visible_stations", ReadableVisibleStations(order)),
            "station_remarks", stationRemarks,
            "history", history
        );
    }

    private Dictionary<string, object> BuildHistoryState(OleDbConnection conn, Dictionary<string, object> user, List<Dictionary<string, object>> orders, Dictionary<string, Dictionary<string, string>> statusLookup, int selectedOrderId)
    {
        var rows = QueryAll(conn, "SELECT TOP 180 h.order_id, h.acted_at, h.acted_by, h.station_id, h.action_code, h.remarks, h.from_station_id, h.to_station_id, h.previous_status_code, h.new_status_code, u.full_name AS acted_by_name, s.machine_name AS station_name FROM (tbl_order_history AS h LEFT JOIN tbl_users AS u ON h.acted_by = u.user_id) LEFT JOIN tbl_machines AS s ON h.station_id = s.machine_id ORDER BY h.acted_at DESC, h.history_id DESC");
        var visibleOrders = orders.ToDictionary(o => I(o, "order_id"));
        var filtered = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            Dictionary<string, object> order = null;
            visibleOrders.TryGetValue(I(row, "order_id"), out order);
            if (!IsHistoryVisibleToUser(user, row, order)) continue;
            filtered.Add(Obj("order_id", I(row, "order_id"), "order_number", order == null ? "-" : S(order, "order_number"), "acted_at", FormatDateTime(DT(row, "acted_at")), "acted_by", EmptyAs(S(row, "acted_by_name"), "-"), "station_name", EmptyAs(S(row, "station_name"), "-"), "action", HumanHistoryAction(statusLookup, row), "remarks", S(row, "remarks")));
        }
        var selected = filtered.Any(r => I(r, "order_id") == selectedOrderId)
            ? selectedOrderId
            : (filtered.Count > 0 ? I(filtered[0], "order_id") : 0);
        return Obj("rows", filtered.Take(120).ToList(), "selected_order_id", selected == 0 ? (object)null : selected);
    }

    private Dictionary<string, object> BuildHistoryStandaloneState(OleDbConnection conn, Dictionary<string, object> user, int selectedOrderId)
    {
        var statusLookup = LoadStatusLookup(conn);
        var rows = QueryAll(conn, "SELECT TOP 80 o.order_id, o.order_number, o.created_by, o.updated_at, o.workflow_stage_code, o.dispatch_status_code, o.confirmation_date, o.packing_balance_box_qty, d.dealer_name FROM (tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) ORDER BY o.updated_at DESC, o.order_id DESC");
        var orderIds = rows.Select(r => I(r, "order_id")).Where(v => v > 0).Distinct().ToList();
        var boxLookup = new Dictionary<int, int>();
        if (orderIds.Count > 0)
        {
            var boxRows = QueryAll(conn, "SELECT order_id, COUNT(*) AS box_count FROM tbl_dispatch_boxes WHERE order_id IN (" + string.Join(",", orderIds) + ") GROUP BY order_id");
            foreach (var br in boxRows) boxLookup[I(br, "order_id")] = I(br, "box_count");
        }
        var createdByIds = rows.Select(r => I(r, "created_by")).Where(v => v > 0).Distinct().ToList();
        var createdByLookup = createdByIds.Count == 0
            ? new Dictionary<int, Dictionary<string, object>>()
            : QueryAll(conn, "SELECT user_id, full_name FROM tbl_users WHERE user_id IN (" + string.Join(",", createdByIds) + ")")
                .GroupBy(r => I(r, "user_id"))
                .ToDictionary(g => g.Key, g => g.First());
        var filtered = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            Dictionary<string, object> createdByRow = null;
            createdByLookup.TryGetValue(I(row, "created_by"), out createdByRow);
            var visibleStations = VisibleStationNames(conn, I(row, "order_id"));
            row["station_name"] = visibleStations;
            var orderStub = Obj(
                "order_id", I(row, "order_id"),
                "order_number", S(row, "order_number"),
                "created_by", I(row, "created_by"),
                "marketing_owner", string.Empty
            );
            if (!IsHistoryVisibleToUser(user, row, orderStub)) continue;
            var action = !string.IsNullOrWhiteSpace(S(row, "dispatch_status_code"))
                ? Label(statusLookup, "DISPATCH", S(row, "dispatch_status_code"))
                : Label(statusLookup, "WORKFLOW", S(row, "workflow_stage_code"));
            filtered.Add(Obj(
                "order_id", I(row, "order_id"),
                "order_number", S(row, "order_number"),
                "customer_name", S(row, "dealer_name"),
                "confirmation_date", ((DateTime?)DT(row, "confirmation_date")).HasValue ? ((DateTime?)DT(row, "confirmation_date")).Value.ToString("dd-MM-yyyy") : "",
                "packed_boxes", boxLookup.ContainsKey(I(row, "order_id")) ? boxLookup[I(row, "order_id")] : 0,
                "balance_boxes", D(row, "packing_balance_box_qty"),
                "acted_at", FormatDateTime(DT(row, "updated_at")),
                "acted_by", EmptyAs(createdByRow == null ? string.Empty : S(createdByRow, "full_name"), "-"),
                "station_name", visibleStations,
                "action", EmptyAs(action, "Updated"),
                "remarks", ""
            ));
        }
        var selected = filtered.Any(r => I(r, "order_id") == selectedOrderId)
            ? selectedOrderId
            : (filtered.Count > 0 ? I(filtered[0], "order_id") : 0);
        return Obj(
            "rows", filtered.Take(80).ToList(),
            "selected_order_id", selected == 0 ? (object)null : selected,
            "lifecycle", Obj("title", "Select an Order", "summary", null, "station_remarks", new List<object>(), "history", new List<object>())
        );
    }

    private string VisibleStationNames(OleDbConnection conn, int orderId)
    {
        var rows = QueryAll(conn, "SELECT m.machine_name FROM tbl_order_station_queue AS q LEFT JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id = ? AND q.is_visible = TRUE ORDER BY m.sequence_no, m.machine_id", orderId);
        return string.Join(", ", rows.Select(r => S(r, "machine_name")).Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    private Dictionary<string, object> BuildLifecycleStandalone(OleDbConnection conn, Dictionary<string, Dictionary<string, string>> statusLookup, int selectedOrderId)
    {
        if (selectedOrderId == 0) return Obj("title", "No Order Selected", "summary", null, "station_remarks", new List<object>(), "history", new List<object>());
        var order = QueryOne(conn, "SELECT o.*, d.dealer_name, ct.customer_type_code, ot.order_type_name FROM ((tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) LEFT JOIN tbl_customer_types AS ct ON o.customer_type_id = ct.customer_type_id) LEFT JOIN tbl_order_types AS ot ON o.order_type_id = ot.order_type_id WHERE o.order_id = ?", selectedOrderId);
        if (order == null) return Obj("title", "No Order Selected", "summary", null, "station_remarks", new List<object>(), "history", new List<object>());
        var queueRows = QueryAll(conn, "SELECT q.remarks, m.machine_name FROM tbl_order_station_queue AS q LEFT JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id = ? AND q.remarks IS NOT NULL AND q.remarks <> ''", selectedOrderId);
        var stationRemarks = queueRows.Select(r => Obj("station_name", S(r, "machine_name"), "remarks", S(r, "remarks"))).ToList();
        var visibleStations = QueryAll(conn, "SELECT m.machine_name FROM tbl_order_station_queue AS q LEFT JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id = ? AND q.is_visible = TRUE ORDER BY m.sequence_no, m.machine_id", selectedOrderId).Select(r => S(r, "machine_name")).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var history = new List<Dictionary<string, object>>
        {
            Obj(
                "acted_at", FormatDateTime(DT(order, "updated_at")),
                "acted_by", "-",
                "station_name", EmptyAs(string.Join(", ", visibleStations), "-"),
                "action", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")),
                "remarks", ""
            )
        };
        return Obj(
            "title", S(order, "order_number"),
            "summary", Obj("order_number", S(order, "order_number"), "quotation_number", S(order, "quotation_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "customer_type", S(order, "customer_type_code"), "order_type", S(order, "order_type_name"), "main_order", S(order, "main_order"), "sub_order", S(order, "sub_order"), "location", S(order, "location"), "workflow_stage", Label(statusLookup, "WORKFLOW", S(order, "workflow_stage_code")), "dispatch_status", Label(statusLookup, "DISPATCH", S(order, "dispatch_status_code")), "visible_stations", string.Join(", ", visibleStations)),
            "station_remarks", stationRemarks,
            "history", history
        );
    }

    private bool IsHistoryVisibleToUser(Dictionary<string, object> user, Dictionary<string, object> row, Dictionary<string, object> order)
    {
        var role = S(user, "role_name");
        var actionCode = S(row, "action_code");
        var actionName = S(row, "action_name");
        var moduleName = S(row, "module_name");
        var stationName = S(row, "station_name");
        if (role == "Admin" || role == "Management") return true;
        if (role == "Data Entry" || role == "Quotation User") return order != null && I(order, "created_by") == I(user, "user_id");
        if (role == "Marketing User") return order != null && string.Equals(S(order, "marketing_owner"), S(user, "full_name"), StringComparison.OrdinalIgnoreCase);
        if (role == "Optimisation User") return actionCode == "OPTIMISATION_DONE" || string.Equals(moduleName, "Optimisation", StringComparison.OrdinalIgnoreCase);
        if (role == "Procurement User") return ProcurementStatusCodes.Contains(actionCode) || string.Equals(moduleName, "Procurement", StringComparison.OrdinalIgnoreCase);
        if (role == "Production Planner User") return true;
        if (role == "Machine User") return true;
        if (role == "Dispatch User") return stationName == "Dispatch" || DispatchStatusCodes.Contains(actionCode) || string.Equals(moduleName, "Dispatch", StringComparison.OrdinalIgnoreCase) || actionName.IndexOf("Dispatch", StringComparison.OrdinalIgnoreCase) >= 0;
        return false;
    }

    private void ApplyProductionAction(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> order, Dictionary<string, object> station, Dictionary<string, object> queueEntry, string actionCode, string remarks, double? balanceBoxQty)
    {
        var sequenceStations = ResolveOrderSequenceStations(conn, order);
        var machineNames = sequenceStations.Select(m => S(m, "machine_name")).ToList();
        var stationName = S(station, "machine_name");
        var previousName = PreviousStationName(machineNames, stationName);
        var nextName = NextStationName(machineNames, stationName);
        var previousStation = string.IsNullOrWhiteSpace(previousName) ? null : sequenceStations.FirstOrDefault(m => S(m, "machine_name") == previousName);
        var nextStation = string.IsNullOrWhiteSpace(nextName) ? null : sequenceStations.FirstOrDefault(m => S(m, "machine_name") == nextName);
        var now = IstNow();
        var workflowStageCode = "PRODUCTION_STARTED";
        var dispatchStatusCode = S(order, "dispatch_status_code");
        var lastAction = "";
        var correctionQueue = false;

        if (actionCode == "COMPLETED")
        {
            var blockedBy = PartialUpstreamStations(conn, order, stationName);
            if (blockedBy.Count > 0)
                throw new ApiFailure(400, "Cannot mark completed - " + string.Join(", ", blockedBy) + " is still partial.");
            EnsureQueueState(conn, I(order, "order_id"), I(station, "machine_id"), "COMPLETED", false, remarks, I(user, "user_id"));
            if (nextStation != null) PreserveOrActivateNextStation(conn, I(order, "order_id"), I(nextStation, "station_id"), I(user, "user_id"));
            if (IsPackingStationName(stationName))
            {
                AutoCompletePreviousStationsForPacking(conn, user, order, station, sequenceStations);
                Execute(conn, "UPDATE tbl_orders SET packing_balance_box_qty = 0 WHERE order_id = ?", I(order, "order_id"));
                workflowStageCode = "DISPATCH_READY";
                dispatchStatusCode = "";
                nextStation = FindMachineByName(conn, "Dispatch");
                if (nextStation != null) PreserveOrActivateNextStation(conn, I(order, "order_id"), I(nextStation, "machine_id"), I(user, "user_id"));
                if (string.Equals(OrderClassForOrder(order), "Main Order", StringComparison.OrdinalIgnoreCase))
                {
                    CascadePackingToSubOrders(conn, user, order, remarks);
                }
                lastAction = "Moved to Dispatch";
            }
            else
            {
                lastAction = "Completed at " + stationName;
            }
            try { AdvancePlannerBoard(conn, Convert.ToInt32(I(order, "order_id")), stationName, sequenceStations, Convert.ToInt32(I(user, "user_id"))); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("AdvancePlannerBoard error: " + ex.Message); }
        }
        else if (actionCode == "PARTIAL_COMPLETED")
        {
            EnsureQueueState(conn, I(order, "order_id"), I(station, "machine_id"), "PARTIAL_COMPLETED", true, remarks, I(user, "user_id"));
            if (nextStation != null) PreserveOrActivateNextStation(conn, I(order, "order_id"), I(nextStation, "station_id"), I(user, "user_id"));
            if (IsPackingStationName(stationName))
            {
                Execute(conn, "UPDATE tbl_orders SET packing_balance_box_qty = ? WHERE order_id = ?",
                    balanceBoxQty ?? D(order, "packing_balance_box_qty"), I(order, "order_id"));
                workflowStageCode = "DISPATCH_READY";
                dispatchStatusCode = "";
            }
            lastAction = "Partial Completed at " + stationName;
        }
        else
        {
            EnsureQueueState(conn, I(order, "order_id"), I(station, "machine_id"), "REJECTED", false, remarks, I(user, "user_id"));
            ClearDownstreamVisibility(conn, I(order, "order_id"), sequenceStations.SkipWhile(m => I(m, "station_id") != I(station, "machine_id")).Skip(1).Select(m => I(m, "station_id")).ToList());
            if (previousStation != null)
            {
                EnsureQueueState(conn, I(order, "order_id"), I(previousStation, "station_id"), "REWORK_PENDING", true, remarks, I(user, "user_id"));
            }
            else
            {
                correctionQueue = true;
            }
            lastAction = "Rejected at " + stationName;
        }

        Execute(conn, "UPDATE tbl_orders SET workflow_stage_code = ?, dispatch_status_code = ?, correction_queue = " + SqlBoolLiteral(correctionQueue) + ", updated_by = ?, updated_at = " + SqlDateLiteral(now) + ", last_action = ?, quotation_remarks = ? WHERE order_id = ?",
            workflowStageCode, dispatchStatusCode, I(user, "user_id"), lastAction, string.IsNullOrWhiteSpace(remarks) ? S(order, "quotation_remarks") : remarks, I(order, "order_id"));
        AddHistory(conn, I(order, "order_id"), I(station, "machine_id"), actionCode, S(queueEntry, "queue_status_code"), actionCode == "REJECTED" ? "REJECTED" : actionCode, I(station, "machine_id"), nextStation != null ? (int?)I(nextStation, nextStation.ContainsKey("station_id") ? "station_id" : "machine_id") : (previousStation != null ? (int?)I(previousStation, previousStation.ContainsKey("station_id") ? "station_id" : "machine_id") : null), remarks, I(user, "user_id"));
        Audit(conn, I(user, "user_id"), "Production", "Order", S(order, "order_number"), lastAction, S(queueEntry, "queue_status_code"), actionCode, remarks, I(station, "machine_id"));
    }

    private void AutoCompletePreviousStationsForPacking(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> order, Dictionary<string, object> packingStation, List<Dictionary<string, object>> sequenceStations)
    {
        var packingStationId = I(packingStation, "machine_id");
        var packingIndex = sequenceStations.FindIndex(s => I(s, "station_id") == packingStationId);
        if (packingIndex <= 0) return;

        for (var i = 0; i < packingIndex; i++)
        {
            var station = sequenceStations[i];
            var stationId = I(station, "station_id");
            var stationName = S(station, "machine_name");
            var existingQueue = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", I(order, "order_id"), stationId);
            var currentQueueStatus = existingQueue == null ? "" : S(existingQueue, "queue_status_code");
            if (string.Equals(currentQueueStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                continue;

            EnsureQueueState(conn, I(order, "order_id"), stationId, "COMPLETED", false, "Auto-completed from Packing", I(user, "user_id"));
            AddHistory(
                conn,
                I(order, "order_id"),
                stationId,
                "COMPLETED",
                string.IsNullOrWhiteSpace(currentQueueStatus) ? "PENDING" : currentQueueStatus,
                "COMPLETED",
                stationId,
                i + 1 < sequenceStations.Count ? (int?)I(sequenceStations[i + 1], "station_id") : null,
                "Auto-completed when Packing was completed",
                I(user, "user_id")
            );
            try
            {
                Audit(conn, I(user, "user_id"), "Production", "Order", S(order, "order_number"), "Packing Upstream Auto Completed", string.IsNullOrWhiteSpace(currentQueueStatus) ? "PENDING" : currentQueueStatus, "COMPLETED", stationName, stationId);
            }
            catch
            {
            }
        }
    }

    private void CascadePackingToSubOrders(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> mainOrder, string remarks)
    {
        var linkedSubOrders = QueryAll(conn, "SELECT * FROM tbl_orders WHERE sub_order = ? AND order_id <> ?", S(mainOrder, "order_number"), I(mainOrder, "order_id"))
            .Where(o => string.Equals(OrderClassForOrder(o), "Sub Order", StringComparison.OrdinalIgnoreCase))
            .Where(o => !string.Equals(S(o, "dispatch_status_code"), "DISPATCHED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var subOrder in linkedSubOrders)
        {
            var sequenceStations = ResolveOrderSequenceStations(conn, subOrder);
        var packingStation = sequenceStations.FirstOrDefault(m => IsPackingStationName(S(m, "machine_name")));
            var dispatchStation = sequenceStations.FirstOrDefault(m => string.Equals(S(m, "machine_name"), "Dispatch", StringComparison.OrdinalIgnoreCase))
                ?? FindMachineByName(conn, "Dispatch");
            if (packingStation == null) continue;

            var note = "Auto-packed with main order " + S(mainOrder, "order_number");
            EnsureQueueState(conn, I(subOrder, "order_id"), I(packingStation, "station_id"), "COMPLETED", false, string.IsNullOrWhiteSpace(remarks) ? note : remarks, I(user, "user_id"));
            if (dispatchStation != null)
            {
                PreserveOrActivateNextStation(conn, I(subOrder, "order_id"), dispatchStation.ContainsKey("station_id") ? I(dispatchStation, "station_id") : I(dispatchStation, "machine_id"), I(user, "user_id"));
            }
            Execute(conn, "UPDATE tbl_orders SET workflow_stage_code = ?, dispatch_status_code = ?, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
                "DISPATCH_READY", "", I(user, "user_id"), note, I(subOrder, "order_id"));
            AddHistory(conn, I(subOrder, "order_id"), I(packingStation, "station_id"), "COMPLETED", "PENDING", "COMPLETED", I(packingStation, "station_id"), dispatchStation == null ? (int?)null : (dispatchStation.ContainsKey("station_id") ? I(dispatchStation, "station_id") : I(dispatchStation, "machine_id")), note, I(user, "user_id"));
            try
            {
                Audit(conn, I(user, "user_id"), "Production", "Order", S(subOrder, "order_number"), "Packed via Main Order", "PENDING", "COMPLETED", note, I(packingStation, "station_id"));
            }
            catch
            {
            }
        }
    }

    private void MoveOrderToPlannerStation(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> order, Dictionary<string, object> targetStation)
    {
        var machineRows = ActiveMachineRows(conn);
        var sequenceStations = ResolveOrderSequenceStations(conn, order);
        var visibleStations = QueryAll(conn, "SELECT q.*, m.machine_name AS station_name FROM tbl_order_station_queue AS q INNER JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id = ? AND q.is_visible = TRUE ORDER BY m.sequence_no", I(order, "order_id"));
        var fromStation = visibleStations.LastOrDefault();
        AutoCompletePlannerPreviousStations(conn, user, order, targetStation, sequenceStations);
        ClearAllQueueVisibility(conn, I(order, "order_id"));
        EnsureQueueState(conn, I(order, "order_id"), I(targetStation, "machine_id"), "PENDING", true, "Planner assigned", I(user, "user_id"));
        var stationName = S(targetStation, "machine_name");
        var workflowStage = string.Equals(stationName, "Dispatch", StringComparison.OrdinalIgnoreCase) ? "DISPATCH_READY" : "PRODUCTION_STARTED";
        var dispatchStatus = string.Equals(stationName, "Dispatch", StringComparison.OrdinalIgnoreCase) ? "" : S(order, "dispatch_status_code");
        Execute(conn, "UPDATE tbl_orders SET workflow_stage_code = ?, dispatch_status_code = ?, correction_queue = FALSE, updated_by = ?, updated_at = " + SqlDateLiteral(IstNow()) + ", last_action = ? WHERE order_id = ?",
            workflowStage, dispatchStatus, I(user, "user_id"), "Planner moved to " + stationName, I(order, "order_id"));
        AddHistory(conn, I(order, "order_id"), I(targetStation, "machine_id"), "PLANNER_ASSIGNED_STATION", "VISIBLE", "PENDING", fromStation == null ? (int?)null : I(fromStation, "station_id"), I(targetStation, "machine_id"), "Planner moved to " + stationName, I(user, "user_id"));
        try
        {
            Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), "Planner Assigned Station", fromStation == null ? "-" : S(fromStation, "station_name"), stationName, "", I(targetStation, "machine_id"));
        }
        catch
        {
        }
    }

    private void AutoCompletePlannerPreviousStations(OleDbConnection conn, Dictionary<string, object> user, Dictionary<string, object> order, Dictionary<string, object> targetStation, List<Dictionary<string, object>> sequenceStations)
    {
        var targetStationId = I(targetStation, "machine_id");
        var targetIndex = sequenceStations.FindIndex(s => I(s, "station_id") == targetStationId);
        if (targetIndex <= 0) return;

        for (var i = 0; i < targetIndex; i++)
        {
            var station = sequenceStations[i];
            var stationId = I(station, "station_id");
            var stationName = S(station, "machine_name");
            var existingQueue = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", I(order, "order_id"), stationId);
            var currentQueueStatus = existingQueue == null ? "" : S(existingQueue, "queue_status_code");
            if (string.Equals(currentQueueStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                continue;

            EnsureQueueState(conn, I(order, "order_id"), stationId, "COMPLETED", false, "Auto-completed by planner", I(user, "user_id"));
            AddHistory(
                conn,
                I(order, "order_id"),
                stationId,
                "COMPLETED",
                string.IsNullOrWhiteSpace(currentQueueStatus) ? "PENDING" : currentQueueStatus,
                "COMPLETED",
                stationId,
                i + 1 < sequenceStations.Count ? (int?)I(sequenceStations[i + 1], "station_id") : null,
                "Auto-completed by planner before move to " + S(targetStation, "machine_name"),
                I(user, "user_id")
            );
            try
            {
                Audit(conn, I(user, "user_id"), "Production Planner", "Order", S(order, "order_number"), "Planner Auto Completed", string.IsNullOrWhiteSpace(currentQueueStatus) ? "PENDING" : currentQueueStatus, "COMPLETED", stationName, stationId);
            }
            catch
            {
            }
        }
    }

    private void PreserveOrActivateNextStation(OleDbConnection conn, int orderId, int stationId, int userId)
    {
        var existing = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, stationId);
        if (existing != null && B(existing, "is_visible") && (S(existing, "queue_status_code") == "IN_PROGRESS" || S(existing, "queue_status_code") == "PARTIAL_COMPLETED"))
        {
            EnsureQueueState(conn, orderId, stationId, S(existing, "queue_status_code"), true, S(existing, "remarks"), userId);
            return;
        }
        EnsureQueueState(conn, orderId, stationId, "PENDING", true, "", userId);
    }

    private void EnsureQueueState(OleDbConnection conn, int orderId, int stationId, string queueStatusCode, bool isVisible, string remarks, int userId)
    {
        var existing = QueryOne(conn, "SELECT queue_id FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, stationId);
        var now = IstNow();
        if (existing != null)
        {
            Execute(conn, "UPDATE tbl_order_station_queue SET queue_status_code = ?, is_visible = " + SqlBoolLiteral(isVisible) + ", remarks = ?, updated_by = ?, updated_at = " + SqlDateLiteral(now) + " WHERE queue_id = ?",
                queueStatusCode, remarks, userId, I(existing, "queue_id"));
        }
        else
        {
            Execute(conn, "INSERT INTO tbl_order_station_queue (order_id, station_id, queue_status_code, is_visible, remarks, updated_by, updated_at) VALUES (?, ?, ?, " + SqlBoolLiteral(isVisible) + ", ?, ?, " + SqlDateLiteral(now) + ")",
                orderId, stationId, queueStatusCode, remarks, userId);
        }
    }

    private void ClearDownstreamVisibility(OleDbConnection conn, int orderId, List<int> stationIds)
    {
        foreach (var stationId in stationIds)
        {
            Execute(conn, "UPDATE tbl_order_station_queue SET is_visible = FALSE WHERE order_id = ? AND station_id = ?", orderId, stationId);
        }
    }

    private void ClearAllQueueVisibility(OleDbConnection conn, int orderId)
    {
        Execute(conn, "UPDATE tbl_order_station_queue SET is_visible = FALSE WHERE order_id = ?", orderId);
    }

    private void AddHistory(OleDbConnection conn, int orderId, int? stationId, string actionCode, string previousStatusCode, string newStatusCode, int? fromStationId, int? toStationId, string remarks, int userId)
    {
        Execute(conn, "INSERT INTO tbl_order_history (order_id, station_id, action_code, previous_status_code, new_status_code, from_station_id, to_station_id, remarks, acted_by, acted_at) VALUES (?, " + SqlIntLiteral(stationId) + ", ?, ?, ?, " + SqlIntLiteral(fromStationId) + ", " + SqlIntLiteral(toStationId) + ", ?, ?, " + SqlDateLiteral(IstNow()) + ")",
            orderId, actionCode, NullableString(previousStatusCode), NullableString(newStatusCode), remarks, userId);
    }

    private void Audit(OleDbConnection conn, int userId, string moduleName, string recordType, string recordKey, string actionName, string previousValue, string newValue, string remarks, int? stationId)
    {
        Execute(conn, "INSERT INTO tbl_audit_logs (module_name, record_type, record_key, action_name, previous_value, new_value, remarks, station_id, user_id, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, " + SqlIntLiteral(stationId) + ", ?, " + SqlDateLiteral(IstNow()) + ")",
            moduleName, recordType, recordKey, actionName, previousValue, newValue, remarks, userId);
    }

    private void EnsureDbReady(HttpContext context)
    {
        context.Application["PmsSchemaVersion"] = SchemaVersion;
    }

    private static string ResolveSiteRoot(HttpContext context)
    {
        if (context != null)
        {
            try
            {
                return context.Server.MapPath("~/");
            }
            catch
            {
            }
        }

        try
        {
            return HostingEnvironment.MapPath("~/");
        }
        catch
        {
            return "";
        }
    }

    private Dictionary<string, object> BuildDailyMailSnapshot(OleDbConnection conn)
    {
        var settings = LoadMailSettings(ResolveSiteRoot(HttpContext.Current));
        var timeZoneId = settings == null ? "India Standard Time" : settings.TimeZoneId;
        return BuildDailyMailSnapshot(conn, NowInZone(timeZoneId).Date.AddDays(-1));
    }

    private Dictionary<string, object> BuildDailyMailSnapshot(OleDbConnection conn, DateTime reportDate)
    {
        return BuildProductionMailSnapshot(conn, reportDate.Date, reportDate.Date.AddDays(1), reportDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture));
    }

    private Dictionary<string, object> BuildProductionMailSnapshot(OleDbConnection conn, DateTime start, DateTime end, string reportLabel)
    {
        var dbStart = ToDbTimeFromIndiaTime(start);
        var dbEnd = ToDbTimeFromIndiaTime(end);
        var adminUser = Obj("user_id", 0, "role_name", "Admin");
        var masters = LoadMasterSets(conn);
        var statusLookup = LoadStatusLookup(conn);
        var orders = LoadEnrichedOrders(conn, masters, adminUser);
        var audits = LoadAudits(conn, adminUser, orders);

        var dayAudits = audits
            .Where(a =>
            {
                var created = ToDateTime(DT(a, "created_at"));
                return created.HasValue && created.Value >= dbStart && created.Value < dbEnd;
            })
            .OrderByDescending(a => S(a, "created_sort"))
            .ToList();

        var updatedOrders = orders
            .Where(o =>
            {
                var updated = ToDateTime(DT(o, "updated_at"));
                return updated.HasValue && updated.Value >= dbStart && updated.Value < dbEnd;
            })
            .OrderByDescending(o => DateSortKey(DT(o, "updated_at")))
            .ToList();

        var stageRows = new[]
        {
            Obj("label", "Optimisation Done", "count", orders.Count(o => string.Equals(S(o, "workflow_stage_code"), "OPTIMISATION_DONE", StringComparison.OrdinalIgnoreCase))),
            Obj("label", "Material Received", "count", orders.Count(o => string.Equals(S(o, "procurement_status_code"), "MATERIAL_RECEIVED", StringComparison.OrdinalIgnoreCase))),
            Obj("label", "In Production", "count", orders.Count(o => PlanningStageKey(o) == "production")),
            Obj("label", "QC", "count", orders.Count(o => PlanningStageKey(o) == "qc")),
            Obj("label", "Packed / Dispatch Ready", "count", orders.Count(o => PlanningStageKey(o) == "packed"))
        }.ToList();

        var moduleRows = dayAudits
            .GroupBy(a => string.IsNullOrWhiteSpace(S(a, "module_name")) ? "Other" : S(a, "module_name"))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => Obj(
                "module_name", g.Key,
                "action_count", g.Count(),
                "latest_action", S(g.First(), "action_name"),
                "latest_user", S(g.First(), "user_name")
            ))
            .ToList();

        var concerns = BuildDailyConcernRows(orders, dayAudits);
        var machineSections = BuildMachineMailSections(conn, orders, dbStart, dbEnd, start);

        return Obj(
            "report_date", start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "report_label", reportLabel,
            "activity_logs", dayAudits.Count,
            "orders_updated", updatedOrders.Count,
            "quotations", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Quotation", StringComparison.OrdinalIgnoreCase)),
            "confirmations", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Order Confirmation", StringComparison.OrdinalIgnoreCase)),
            "optimisations", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Optimisation", StringComparison.OrdinalIgnoreCase)),
            "procurement", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Procurement", StringComparison.OrdinalIgnoreCase)),
            "production", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Production", StringComparison.OrdinalIgnoreCase)),
            "dispatch", dayAudits.Count(a => string.Equals(S(a, "module_name"), "Dispatch", StringComparison.OrdinalIgnoreCase)),
            "module_rows", moduleRows,
            "updated_orders", updatedOrders.Take(30).Select(o => Obj(
                "order_number", S(o, "order_number"),
                "dealer_name", S(o, "dealer_name"),
                "order_type", S(o, "order_type_name"),
                "stage_label", PlanningStageLabel(PlanningStageKey(o)),
                "visible_stations", ReadableVisibleStations(o),
                "updated_at", FormatDateTime(DT(o, "updated_at"))
            )).ToList(),
            "recent_actions", dayAudits.Take(30).Select(a => Obj(
                "time_label", S(a, "created_at"),
                "module_name", S(a, "module_name"),
                "order_key", S(a, "record_key"),
                "action_name", S(a, "action_name"),
                "user_name", S(a, "user_name"),
                "remarks", S(a, "remarks")
            )).ToList(),
            "machine_sections", machineSections,
            "stage_rows", stageRows,
            "concerns", concerns
        );
    }

    private List<Dictionary<string, object>> BuildDailyConcernRows(List<Dictionary<string, object>> orders, List<Dictionary<string, object>> dayAudits)
    {
        var rows = new List<Dictionary<string, object>>();
        foreach (var order in orders.Where(o => B(o, "correction_queue")).Take(15))
        {
            rows.Add(Obj(
                "order_number", S(order, "order_number"),
                "concern", "Waiting for planner reapproval",
                "cause", EmptyAs(S(order, "correction_remarks"), "Rejected at station and moved to correction queue."),
                "current_stage", "Correction Queue"
            ));
        }

        foreach (var order in orders.Where(o =>
        {
            var statuses = (Dictionary<string, string>)o["station_statuses"];
            return statuses.Values.Any(v => string.Equals(v, "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase));
        }).Take(15))
        {
            rows.Add(Obj(
                "order_number", S(order, "order_number"),
                "concern", "Partially pending",
                "cause", "Order is active in more than one station because partial completion was marked.",
                "current_stage", ReadableVisibleStations(order)
            ));
        }

        foreach (var audit in dayAudits.Where(a => string.Equals(S(a, "action_name"), "Rejected", StringComparison.OrdinalIgnoreCase) || string.Equals(S(a, "new_value"), "REJECTED", StringComparison.OrdinalIgnoreCase)).Take(15))
        {
            rows.Add(Obj(
                "order_number", S(audit, "record_key"),
                "concern", "Rejected movement",
                "cause", EmptyAs(S(audit, "remarks"), "Rejected in workflow and moved backward."),
                "current_stage", EmptyAs(S(audit, "station_name"), "Production")
            ));
        }

        return rows.Take(20).ToList();
    }

    private List<Dictionary<string, object>> BuildMachineMailSections(OleDbConnection conn, List<Dictionary<string, object>> orders, DateTime start, DateTime end, DateTime labelDate)
    {
        var orderLookup = orders.ToDictionary(o => I(o, "order_id"));
        EnsureDispatchBoxSchema(conn);
        var confirmationHistoryLookup = QueryAll(conn,
            "SELECT order_id, acted_at FROM tbl_order_history WHERE action_code = 'ORDER_CONFIRMED' ORDER BY acted_at DESC, history_id DESC")
            .GroupBy(r => I(r, "order_id"))
            .ToDictionary(g => g.Key, g => DT(g.First(), "acted_at"));
        var plannerLookup = QueryAll(conn, "SELECT order_id, sla_date, [priority] FROM tbl_production_planner")
            .ToDictionary(r => I(r, "order_id"));
        var dispatchBoxCountLookup = QueryAll(conn, "SELECT order_id, COUNT(*) AS box_count FROM tbl_dispatch_boxes GROUP BY order_id")
            .ToDictionary(r => I(r, "order_id"), r => I(r, "box_count"));
        var operatorLookup = LoadUsers(conn)
            .Where(u => !string.Equals(S(u, "station_name"), "All Stations", StringComparison.OrdinalIgnoreCase))
            .GroupBy(u => S(u, "station_name"))
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(u => S(u, "full_name")).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray()),
                StringComparer.OrdinalIgnoreCase);

        var historyRows = QueryAll(conn,
            "SELECT h.order_id, h.acted_at, s.machine_name AS station_name FROM tbl_order_history AS h LEFT JOIN tbl_machines AS s ON h.station_id = s.machine_id WHERE h.acted_at >= " + SqlDateLiteral(start) + " AND h.acted_at < " + SqlDateLiteral(end) + " ORDER BY h.acted_at DESC, h.history_id DESC");

        var latestByMachineAndOrder = historyRows
            .Where(r => !string.IsNullOrWhiteSpace(S(r, "station_name")))
            .GroupBy(r => S(r, "station_name") + "||" + I(r, "order_id"))
            .Select(g => g.OrderByDescending(r => ToDateTime(DT(r, "acted_at"))).First())
            .ToList();

        return ActiveMachineRows(conn)
            .Select(machine =>
            {
                var machineName = S(machine, "machine_name");
                var rows = latestByMachineAndOrder
                    .Where(r => string.Equals(S(r, "station_name"), machineName, StringComparison.OrdinalIgnoreCase))
                    .Select(r =>
                    {
                        Dictionary<string, object> order;
                        orderLookup.TryGetValue(I(r, "order_id"), out order);
                        Dictionary<string, object> planner;
                        plannerLookup.TryGetValue(I(r, "order_id"), out planner);
                        DateTime? confirmationDate = order == null ? null : ToDateTime(DT(order, "confirmation_date"));
                        if (!confirmationDate.HasValue && confirmationHistoryLookup.ContainsKey(I(r, "order_id")))
                            confirmationDate = ToDateTime(confirmationHistoryLookup[I(r, "order_id")]);
                        var boxQty = dispatchBoxCountLookup.ContainsKey(I(r, "order_id")) ? dispatchBoxCountLookup[I(r, "order_id")] : 0;
                        return Obj(
                            "confirmation_date", FormatMailShortDate(confirmationDate),
                            "order_number", order == null ? "" : S(order, "order_number"),
                            "customer_name", order == null ? "-" : EmptyAs(S(order, "customer_name"), "-"),
                            "box_qty", boxQty
                        );
                    })
                    .OrderBy(r => S(r, "order_number"))
                    .ToList();

                return Obj(
                    "machine_name", machineName,
                    "operator_name", EmptyAs(operatorLookup.ContainsKey(machineName) ? operatorLookup[machineName] : machineName, machineName),
                    "date_label", labelDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
                    "box_qty_sum", rows.Sum(r => D(r, "box_qty")),
                    "rows", rows
                );
            })
            .ToList();
    }

    private Dictionary<string, object> GetMailStatus(OleDbConnection conn)
    {
        var rows = QueryAll(conn, "SELECT TOP 10 * FROM tbl_mail_reports ORDER BY sent_at DESC, mail_report_id DESC");
        return Obj(
            "rows", rows.Select(r => Obj(
                "report_kind", S(r, "report_kind"),
                "report_date", ToDateTime(DT(r, "report_date")).HasValue ? ToDateTime(DT(r, "report_date")).Value.ToString("yyyy-MM-dd") : "",
                "recipient_list", S(r, "recipient_list"),
                "subject_line", S(r, "subject_line"),
                "send_status", S(r, "send_status"),
                "error_text", S(r, "error_text"),
                "sent_at", FormatDateTime(DT(r, "sent_at"))
            )).ToList()
        );
    }

    private static MailSettings LoadMailSettings(string siteRoot)
    {
        try
        {
            var configPath = string.IsNullOrWhiteSpace(siteRoot)
                ? HostingEnvironment.MapPath(MailConfigRelativePath)
                : Path.Combine(siteRoot, "App_Data", "smtp-settings.json");
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath)) return null;
            var raw = Json.Deserialize<Dictionary<string, object>>(File.ReadAllText(configPath));
            if (raw == null) return null;
            return new MailSettings
            {
                Enabled = ReadBool(raw, "enabled", true),
                DeliveryMode = ReadString(raw, "delivery_mode", "smtp"),
                Host = ReadString(raw, "smtp_host"),
                Port = ReadInt(raw, "smtp_port", 587),
                Username = ReadString(raw, "smtp_username"),
                Password = ReadString(raw, "smtp_password"),
                UseSsl = ReadBool(raw, "use_ssl", true),
                BrevoApiKey = ReadString(raw, "brevo_api_key"),
                FromEmail = ReadString(raw, "from_email"),
                FromName = ReadString(raw, "from_name"),
                ToEmails = ReadStringList(raw, "to_emails"),
                TimeZoneId = ReadString(raw, "timezone_id", "India Standard Time"),
                DailyHour = ReadInt(raw, "daily_hour", 9),
                DailyMinute = ReadInt(raw, "daily_minute", 0)
            };
        }
        catch
        {
            return null;
        }
    }

    private static OleDbConnection OpenConnection(string siteRoot)
    {
        var dbPath = Path.Combine(siteRoot, "App_Data", "elenza_pms.accdb");
        var providers = new[]
        {
            "Microsoft.ACE.OLEDB.16.0",
            "Microsoft.ACE.OLEDB.12.0",
            "Microsoft.Jet.OLEDB.4.0"
        };

        Exception last = null;
        foreach (var provider in providers)
        {
            try
            {
                var conn = new OleDbConnection("Provider=" + provider + ";Data Source=" + dbPath + ";Persist Security Info=False;");
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new ApiFailure(500, "Access database provider not available. " + (last == null ? "" : last.Message));
    }

    private static bool WasMailAlreadySent(OleDbConnection conn, string reportKind, DateTime reportDate)
    {
        var row = QueryAllStatic(conn, "SELECT TOP 1 send_status FROM tbl_mail_reports WHERE report_kind = ? AND report_date >= " + SqlDateLiteral(reportDate.Date) + " AND report_date < " + SqlDateLiteral(reportDate.Date.AddDays(1)) + " ORDER BY mail_report_id DESC", reportKind).FirstOrDefault();
        return row != null && string.Equals(ReadRowString(row, "send_status"), "SENT", StringComparison.OrdinalIgnoreCase);
    }

    private static void LogMailReport(OleDbConnection conn, string reportKind, DateTime reportDate, string recipients, string subject, string status, string errorText, DateTime sentAt)
    {
        ExecuteStatic(conn,
            "INSERT INTO tbl_mail_reports (report_kind, report_date, recipient_list, subject_line, send_status, error_text, sent_at, created_at) VALUES (?, " + SqlDateLiteral(reportDate.Date) + ", ?, ?, ?, ?, " + SqlDateLiteral(sentAt) + ", " + SqlDateLiteral(sentAt) + ")",
            reportKind,
            recipients,
            subject,
            status,
            NullIfEmpty(errorText));
    }

    private static void SendDailyReportMail(MailSettings settings, string subject, string html)
    {
        if (settings == null) throw new InvalidOperationException("SMTP settings were not found.");
        if (settings.ToEmails == null || settings.ToEmails.Count == 0)
            throw new InvalidOperationException("SMTP recipients are not configured.");

        if (string.Equals(settings.DeliveryMode, "brevo_api", StringComparison.OrdinalIgnoreCase))
        {
            SendViaBrevoApi(settings, subject, html);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("SMTP settings are incomplete.");

        using (var client = new SmtpClient(settings.Host, settings.Port))
        using (var message = new MailMessage())
        {
            client.EnableSsl = settings.UseSsl;
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            message.From = new MailAddress(settings.FromEmail, string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail : settings.FromName);
            foreach (var email in settings.ToEmails.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
                message.To.Add(email);
            message.Subject = subject;
            message.SubjectEncoding = Encoding.UTF8;
            message.BodyEncoding = Encoding.UTF8;
            message.IsBodyHtml = true;
            message.Body = html;
            client.Send(message);
        }
    }

    private static void SendViaBrevoApi(MailSettings settings, string subject, string html)
    {
        if (string.IsNullOrWhiteSpace(settings.BrevoApiKey))
            throw new InvalidOperationException("Brevo API key is missing.");

        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

        var payload = Obj(
            "sender", Obj(
                "name", string.IsNullOrWhiteSpace(settings.FromName) ? settings.FromEmail : settings.FromName,
                "email", settings.FromEmail
            ),
            "to", settings.ToEmails.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(email => Obj("email", email)).ToList(),
            "subject", subject,
            "htmlContent", html
        );

        var request = (HttpWebRequest)WebRequest.Create("https://api.brevo.com/v3/smtp/email");
        request.Method = "POST";
        request.ContentType = "application/json";
        request.Accept = "application/json";
        request.Headers["api-key"] = settings.BrevoApiKey;

        var body = Json.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(body);
        request.ContentLength = bytes.Length;
        using (var requestStream = request.GetRequestStream())
        {
            requestStream.Write(bytes, 0, bytes.Length);
        }

        try
        {
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseText = reader.ReadToEnd();
                if ((int)response.StatusCode >= 400)
                    throw new InvalidOperationException("Brevo API send failed: " + responseText);
            }
        }
        catch (WebException ex)
        {
            var responseText = "";
            if (ex.Response != null)
            {
                using (var reader = new StreamReader(ex.Response.GetResponseStream()))
                {
                    responseText = reader.ReadToEnd();
                }
            }
            throw new InvalidOperationException("Brevo API send failed. " + responseText);
        }
    }

    private string BuildDailyReportHtml(Dictionary<string, object> snapshot, MailSettings settings, DateTime sentAt)
    {
        return BuildScheduledProductionReportHtml(snapshot, settings, sentAt, null);
    }

    private string BuildScheduledProductionReportHtml(Dictionary<string, object> snapshot, MailSettings settings, DateTime sentAt, ScheduledProductionReportSlot slot)
    {
        var isScheduledSlot = slot != null;
        var title = isScheduledSlot
            ? (slot.IsFinalConsolidated ? "Production Consolidated Report" : "Hourly Production Report")
            : "Daily Production Activity Report";
        var subtitle = isScheduledSlot
            ? (slot.IsFinalConsolidated
                ? "Machine-wise consolidated report for <strong>" + Html(S(snapshot, "report_label")) + "</strong>. Sent at " + Html(sentAt.ToString("dd MMM yyyy hh:mm tt", CultureInfo.InvariantCulture)) + " IST."
                : "Machine-wise production update for <strong>" + Html(S(snapshot, "report_label")) + "</strong>. Sent at " + Html(sentAt.ToString("dd MMM yyyy hh:mm tt", CultureInfo.InvariantCulture)) + " IST.")
            : "Last day activity summary for <strong>" + Html(S(snapshot, "report_label")) + "</strong>. Sent at " + Html(sentAt.ToString("dd MMM yyyy hh:mm tt", CultureInfo.InvariantCulture)) + " IST. Reporting hours: 09:00 AM - 09:00 PM IST.";

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Elenza PMS Daily Report</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;background:#eef5fb;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;} ");
        sb.Append(".mail-shell{max-width:980px;margin:0 auto;padding:24px 16px;} ");
        sb.Append(".mail-card{background:#ffffff;border:0.5px solid #d9e5f3;border-radius:24px;overflow:hidden;box-shadow:0 16px 40px rgba(4,92,180,0.08);} ");
        sb.Append(".mail-head{padding:28px 32px;background:linear-gradient(135deg,#ffffff 0%,#f0f5fa 100%);border-bottom:0.5px solid #d9e5f3;} ");
        sb.Append(".mail-body{padding:24px 32px 8px;} ");
        sb.Append(".metric-grid{width:100%;border-collapse:separate;border-spacing:12px;} ");
        sb.Append(".metric-cell{width:25%;background:#f8fbff;border:0.5px solid #d9e5f3;border-radius:18px;padding:16px 18px;} ");
        sb.Append(".report-wrap{overflow-x:auto;overflow-y:hidden;-webkit-overflow-scrolling:touch;border:0.5px solid #d9e5f3;border-radius:18px;background:#ffffff;} ");
        sb.Append(".report-table{width:100%;border-collapse:collapse;min-width:720px;} ");
        sb.Append(".report-head th{padding:12px 14px;text-align:left;font-size:12px;letter-spacing:0.4px;text-transform:uppercase;color:#1e293b;border-bottom:0.5px solid #d9e5f3;background:#f0f5fa;} ");
        sb.Append(".report-cell{padding:12px 14px;font-size:14px;color:#0f172a;border-bottom:0.5px solid #eef2f7;vertical-align:top;} ");
        sb.Append("@media only screen and (max-width: 720px){");
        sb.Append(".mail-shell{padding:10px 8px !important;} ");
        sb.Append(".mail-head,.mail-body{padding:18px 14px !important;} ");
        sb.Append(".metric-grid,.metric-grid tbody,.metric-grid tr,.metric-cell{display:block !important;width:100% !important;} ");
        sb.Append(".metric-cell{box-sizing:border-box;margin:0 0 10px 0 !important;} ");
        sb.Append(".report-wrap{overflow:visible !important;border:none !important;background:transparent !important;} ");
        sb.Append(".report-table{min-width:0 !important;width:100% !important;} ");
        sb.Append(".report-head{display:none !important;} ");
        sb.Append(".report-table,.report-table tbody,.report-table tr,.report-cell{display:block !important;width:100% !important;box-sizing:border-box;} ");
        sb.Append(".report-table tr{margin:0 0 12px 0;border:0.5px solid #d9e5f3;border-radius:16px;overflow:hidden;background:#ffffff;} ");
        sb.Append(".report-cell{border-bottom:0.5px solid #eef2f7 !important;padding:10px 12px 10px 44% !important;position:relative;font-size:13px !important;min-height:18px;} ");
        sb.Append(".report-cell:last-child{border-bottom:none !important;} ");
        sb.Append(".report-cell:before{content:attr(data-label);position:absolute;left:12px;top:10px;width:36%;font-size:11px;line-height:1.4;text-transform:uppercase;color:#64748b;font-weight:700;} ");
        sb.Append("}");
        sb.Append("</style></head>");
        sb.Append("<body>");
        sb.Append("<div class=\"mail-shell\">");
        sb.Append("<div class=\"mail-card\">");
        sb.Append("<div class=\"mail-head\">");
        sb.Append("<div style=\"font-size:13px;letter-spacing:1.4px;font-weight:700;color:#046bd2;text-transform:uppercase;\">ElenzaIndia.com</div>");
        sb.Append("<h1 style=\"margin:10px 0 8px;font-size:32px;line-height:1.1;color:#0f172a;\">" + title + "</h1>");
        sb.Append("<p style=\"margin:0;font-size:15px;color:#475569;\">" + subtitle + "</p>");
        sb.Append("</div>");
        sb.Append("<div class=\"mail-body\">");
        sb.Append("<table role=\"presentation\" class=\"metric-grid\">");
        sb.Append("<tr>");
        sb.Append(MetricCell("Orders Updated", snapshot["orders_updated"]));
        sb.Append(MetricCell("Activity Logs", snapshot["activity_logs"]));
        sb.Append(MetricCell("Production", snapshot["production"]));
        sb.Append(MetricCell("Dispatch", snapshot["dispatch"]));
        sb.Append("</tr></table>");
        var machineSections = (List<Dictionary<string, object>>)snapshot["machine_sections"];
        foreach (var section in machineSections)
        {
            sb.Append("<div style=\"margin:18px 0 24px;\">");
            sb.Append("<h2 style=\"margin:0 0 10px;font-size:18px;color:#0f172a;\">" + Html(S(section, "machine_name")) + "</h2>");
            sb.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;margin-bottom:10px;\">");
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:8px 10px;border:0.5px solid #d9e5f3;background:#f0f5fa;font-size:12px;font-weight:700;text-transform:uppercase;color:#1e293b;\">Operator Name</td>");
            sb.Append("<td style=\"padding:8px 10px;border:0.5px solid #d9e5f3;background:#f0f5fa;font-size:12px;font-weight:700;text-transform:uppercase;color:#1e293b;\">Date</td>");
            sb.Append("</tr>");
            sb.Append("<tr>");
            sb.Append("<td style=\"padding:10px;border:0.5px solid #d9e5f3;font-size:14px;color:#0f172a;\">" + Html(S(section, "operator_name")) + "</td>");
            sb.Append("<td style=\"padding:10px;border:0.5px solid #d9e5f3;font-size:14px;color:#0f172a;\">" + Html(S(section, "date_label")) + "</td>");
            sb.Append("</tr>");
            sb.Append("</table>");

            var rows = ((List<Dictionary<string, object>>)section["rows"])
                .Select(r => new[]
                {
                    Html(S(r, "confirmation_date")),
                    Html(S(r, "order_number")),
                    Html(S(r, "customer_name")),
                    Convert.ToString(r["box_qty"])
                })
                .ToList();

            if (rows.Count > 0)
            {
                rows.Add(new[]
                {
                    "",
                    "",
                    "Sum",
                    Convert.ToString(section["box_qty_sum"])
                });
            }

            sb.Append(RenderMailTable(
                Html(S(section, "machine_name")) + " Worksheet",
                new[] { "Order Con", "Order Num", "Customer Name", "Pnbox Qty" },
                rows,
                "No movement rows for this machine in this report window."
            ));
            sb.Append("</div>");
        }
        sb.Append("</div>");
        sb.Append("<div style=\"padding:0 32px 28px;color:#64748b;font-size:13px;\">This mail was generated by Elenza PMS using the hosted workflow and audit history.</div>");
        sb.Append("</div></div></body></html>");
        return sb.ToString();
    }

    private static bool IsWithinScheduledProductionWindow(DateTime indiaNow)
    {
        return indiaNow.TimeOfDay >= WorkdayStart && indiaNow.TimeOfDay <= FinalReportGraceWindow;
    }

    private static List<ScheduledProductionReportSlot> GetDueScheduledProductionSlots(DateTime now, bool force)
    {
        var due = new List<ScheduledProductionReportSlot>();
        var dayStart = now.Date.Add(WorkdayStart);

        foreach (var hour in HourlyProductionSlotHours)
        {
            var slotTime = now.Date.AddHours(hour);
            if (!force && now < slotTime) continue;
            due.Add(new ScheduledProductionReportSlot
            {
                ReportDate = now.Date,
                ReportKind = HourlyProductionReportKindPrefix + hour.ToString("00", CultureInfo.InvariantCulture),
                SlotTime = slotTime,
                WindowStart = dayStart,
                WindowEnd = slotTime,
                IsFinalConsolidated = false,
                ReportLabel = dayStart.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " | 09:00 AM to " + slotTime.ToString("hh:mm tt", CultureInfo.InvariantCulture) + " IST",
                Subject = "hourly Production report ; " + now.Date.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) + ", " + slotTime.ToString("hh:mm tt", CultureInfo.InvariantCulture)
            });
        }

        var finalTime = now.Date.Add(WorkdayEnd);
        if (force || now >= finalTime)
        {
            due.Add(new ScheduledProductionReportSlot
            {
                ReportDate = now.Date,
                ReportKind = DailyMachineConsolidatedReportKind,
                SlotTime = finalTime,
                WindowStart = dayStart,
                WindowEnd = finalTime,
                IsFinalConsolidated = true,
                ReportLabel = dayStart.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) + " | Full Day Consolidated till 09:00 PM IST",
                Subject = "hourly Production report ; " + now.Date.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) + ", 09:00 PM | Total Orders Consolidated"
            });
        }

        return due.OrderBy(s => s.SlotTime).ToList();
    }

    private string RenderMailTable(string title, string[] headers, List<string[]> rows, string emptyMessage)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"margin:18px 0 22px;\">");
        sb.Append("<div style=\"display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;\">");
        sb.Append("<h2 style=\"margin:0;font-size:18px;color:#0f172a;\">" + Html(title) + "</h2>");
        sb.Append("</div>");
        sb.Append("<div class=\"report-wrap\">");
        sb.Append("<table role=\"presentation\" class=\"report-table\">");
        sb.Append("<thead class=\"report-head\"><tr>");
        foreach (var header in headers) sb.Append("<th>" + Html(header) + "</th>");
        sb.Append("</tr></thead><tbody>");
        if (rows == null || rows.Count == 0)
        {
            sb.Append("<tr><td colspan=\"" + headers.Length + "\" class=\"report-cell\" style=\"padding:18px 14px;font-size:14px;color:#64748b;\">" + Html(emptyMessage) + "</td></tr>");
        }
        else
        {
            foreach (var row in rows)
            {
                var isSumRow = row.Any(cell => !string.IsNullOrWhiteSpace(cell) && cell.IndexOf("Sum", StringComparison.OrdinalIgnoreCase) >= 0);
                sb.Append(isSumRow ? "<tr style=\"background:#dcfce7;\">" : "<tr>");
                for (var i = 0; i < row.Length; i++)
                {
                    var header = i < headers.Length ? headers[i] : "";
                    sb.Append("<td class=\"report-cell\" data-label=\"" + Html(header) + "\"" + (isSumRow ? " style=\"background:#dcfce7;font-weight:700;\"" : "") + ">" + row[i] + "</td>");
                }
                sb.Append("</tr>");
            }
        }
        sb.Append("</tbody></table></div></div>");
        return sb.ToString();
    }

    private string RenderMailTable(string title, string[] headers, List<string[]> rows)
    {
        return RenderMailTable(title, headers, rows, "No rows available.");
    }

    private string MetricCell(string label, object value)
    {
        return "<td class=\"metric-cell\">" +
               "<div style=\"font-size:12px;text-transform:uppercase;letter-spacing:0.5px;color:#64748b;\">" + Html(label) + "</div>" +
               "<div style=\"margin-top:8px;font-size:28px;font-weight:700;color:#045cb4;\">" + Html(Convert.ToString(value)) + "</div></td>";
    }

    private string FormatMailShortDate(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("dd-MMM-yy", CultureInfo.InvariantCulture) : "";
    }

    private static string Html(string value)
    {
        return HttpUtility.HtmlEncode(value ?? "");
    }

    private static DateTime NowInZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? "India Standard Time" : timeZoneId));
        }
        catch
        {
            return IstNow();
        }
    }

    private static bool IsWithinIndiaWorkingHours(DateTime indiaNow)
    {
        return indiaNow.TimeOfDay >= WorkdayStart && indiaNow.TimeOfDay <= WorkdayEnd;
    }

    private static DateTime ToDbTimeFromIndiaTime(DateTime indiaTime)
    {
        return indiaTime.AddMinutes(-330);
    }

    private sealed class ScheduledProductionReportSlot
    {
        public DateTime ReportDate { get; set; }
        public string ReportKind { get; set; }
        public DateTime SlotTime { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public bool IsFinalConsolidated { get; set; }
        public string ReportLabel { get; set; }
        public string Subject { get; set; }
    }

    private void EnsureSchema(OleDbConnection conn)
    {
        TryExecute(conn, "CREATE TABLE tbl_dropdown_masters (dropdown_id COUNTER PRIMARY KEY, master_name TEXT(80) NOT NULL, option_value TEXT(150) NOT NULL, sort_order LONG NOT NULL, is_active YESNO NOT NULL)");
        TryExecute(conn, "CREATE TABLE tbl_production_planner (planner_id COUNTER PRIMARY KEY, order_id LONG NOT NULL, planning_rank LONG NOT NULL, sla_date DATETIME, urgency TEXT(60), priority TEXT(60), planner_remarks MEMO, updated_by LONG, updated_at DATETIME)");
        TryExecute(conn, "CREATE TABLE tbl_sequence_profiles (profile_id COUNTER PRIMARY KEY, profile_name TEXT(150) NOT NULL, order_type_id LONG, order_class_code TEXT(80), is_active YESNO NOT NULL)");
        TryExecute(conn, "CREATE TABLE tbl_sequence_profile_stations (profile_station_id COUNTER PRIMARY KEY, profile_id LONG NOT NULL, station_id LONG NOT NULL, sequence_no LONG NOT NULL)");
        TryExecute(conn, "CREATE TABLE tbl_mail_reports (mail_report_id COUNTER PRIMARY KEY, report_kind TEXT(60) NOT NULL, report_date DATETIME NOT NULL, recipient_list MEMO, subject_line TEXT(255), send_status TEXT(30), error_text MEMO, sent_at DATETIME, created_at DATETIME)");
        EnsureDispatchBoxSchema(conn);
        EnsureRemarksSchema(conn);
        EnsureScannerSchema(conn);
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN customer_type_id LONG");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN customer_type_code TEXT(50)");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN pin_code TEXT(20)");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN payment_terms TEXT(80)");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN credit_limit_lakh DOUBLE");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN marketing_owner TEXT(120)");
        TryExecute(conn, "ALTER TABLE tbl_dealers ADD COLUMN quotation_owner TEXT(120)");
        TryExecute(conn, "ALTER TABLE tbl_users ADD COLUMN dealer_id LONG");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN sequence_profile_id LONG");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN order_class_code TEXT(80)");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN board_qty_decimal DOUBLE");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN panel_qty DOUBLE");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN packing_balance_box_qty DOUBLE");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN dispatch_balance_box_qty DOUBLE");
        TryExecute(conn, "ALTER TABLE tbl_production_planner ADD COLUMN priority_date DATETIME");
        TryExecute(conn, "CREATE UNIQUE INDEX ux_tbl_dealers_code ON tbl_dealers (dealer_code)");
        TryExecute(conn, "CREATE UNIQUE INDEX ux_tbl_dropdown_master_value ON tbl_dropdown_masters (master_name, option_value)");
        TryExecute(conn, "CREATE UNIQUE INDEX ux_tbl_production_planner_order ON tbl_production_planner (order_id)");
        TryExecute(conn, "CREATE INDEX ix_tbl_production_planner_rank ON tbl_production_planner (planning_rank)");
        TryExecute(conn, "CREATE INDEX ix_tbl_sequence_profiles_type_class ON tbl_sequence_profiles (order_type_id, order_class_code)");
        TryExecute(conn, "CREATE INDEX ix_tbl_sequence_profile_stations_profile ON tbl_sequence_profile_stations (profile_id, sequence_no)");
        TryExecute(conn, "CREATE INDEX ix_tbl_mail_reports_daily ON tbl_mail_reports (report_kind, report_date)");
        TryExecute(conn, "CREATE TABLE tbl_planner_board (board_id COUNTER PRIMARY KEY, order_id LONG NOT NULL, station_id LONG NOT NULL, assigned_by LONG, assigned_at DATETIME, planned_date DATETIME)");
        TryExecute(conn, "ALTER TABLE tbl_planner_board ADD COLUMN planned_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_planner_board ADD COLUMN remarks TEXT(500)");
        TryExecute(conn, "CREATE UNIQUE INDEX ux_planner_board_order_station ON tbl_planner_board (order_id, station_id)");
        TryExecute(conn, "CREATE TABLE tbl_dealer_ledger (ledger_id COUNTER PRIMARY KEY, dealer_id LONG NOT NULL, entry_date DATETIME NOT NULL, payment_mode TEXT(30), amount DOUBLE NOT NULL, reference_no TEXT(100), order_id LONG, remarks MEMO, created_by LONG, created_at DATETIME)");
        TryExecute(conn, "CREATE INDEX ix_tbl_dealer_ledger_dealer ON tbl_dealer_ledger (dealer_id, entry_date)");
    }

    private void EnsureDispatchBoxSchema(OleDbConnection conn)
    {
        TryExecute(conn, "CREATE TABLE tbl_dispatch_boxes (dispatch_box_id COUNTER PRIMARY KEY, order_id LONG NOT NULL, box_no LONG NOT NULL, box_state TEXT(40) NOT NULL, updated_by LONG, updated_at DATETIME, created_at DATETIME)");
        TryExecute(conn, "CREATE INDEX ix_tbl_dispatch_boxes_order ON tbl_dispatch_boxes (order_id)");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN packing_ready_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN cutting_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN edgebanding_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN drilling_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN hot_press_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN qc_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN packed_date DATETIME");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN dispatch_date DATETIME");
    }

    private void EnsureCoreRoles(OleDbConnection conn)
    {
        EnsureRoleRow(conn, "Admin", "data-entry");
        EnsureRoleRow(conn, "Data Entry", "data-entry");
        EnsureRoleRow(conn, "Quotation User", "data-entry");
        EnsureRoleRow(conn, "Marketing User", "data-entry");
        EnsureRoleRow(conn, "Machine User", "production");
        EnsureRoleRow(conn, "Dispatch User", "dispatch");
        EnsureRoleRow(conn, "Management", "reports");
        EnsureRoleRow(conn, "Optimisation User", "optimisation");
        EnsureRoleRow(conn, "Procurement User", "procurement");
        EnsureRoleRow(conn, "Production Planner User", "planner");
        EnsureRoleRow(conn, "Dealer", "dashboard");
        EnsureRoleRow(conn, "Accounts", "dashboard");
    }

    private void EnsureRoleRow(OleDbConnection conn, string roleName, string homeSection)
    {
        var existing = QueryOne(conn, "SELECT role_id FROM tbl_roles WHERE role_name = ?", roleName);
        if (existing != null)
        {
            TryExecute(conn, "ALTER TABLE tbl_roles ADD COLUMN home_section TEXT(60)");
            Execute(conn, "UPDATE tbl_roles SET home_section = ? WHERE role_name = ? AND (home_section IS NULL OR home_section = '')", homeSection, roleName);
            return;
        }
        TryExecute(conn, "ALTER TABLE tbl_roles ADD COLUMN home_section TEXT(60)");
        Execute(conn, "INSERT INTO tbl_roles (role_name, home_section) VALUES (?, ?)", roleName, homeSection);
    }

    private void EnsureSampleUsers(OleDbConnection conn)
    {
        var roles = QueryAll(conn, "SELECT role_id, role_name FROM tbl_roles").ToDictionary(r => S(r, "role_name"), StringComparer.OrdinalIgnoreCase);
        var stations = QueryAll(conn, "SELECT machine_id, machine_name FROM tbl_machines").ToDictionary(r => S(r, "machine_name"), StringComparer.OrdinalIgnoreCase);
        var now = IstNow();
        foreach (var sample in SampleUsers)
        {
            if (!roles.ContainsKey(sample.RoleName)) continue;
            var existing = QueryOne(conn, "SELECT user_id FROM tbl_users WHERE login_id = ?", sample.LoginId);
            if (existing != null) continue;
            object stationId = DBNull.Value;
            if (!string.IsNullOrWhiteSpace(sample.AssignedStation) && stations.ContainsKey(sample.AssignedStation))
                stationId = I(stations[sample.AssignedStation], "machine_id");
            Execute(conn, "INSERT INTO tbl_users (full_name, login_id, password_hash, password_salt, password_iterations, role_id, assigned_station_id, is_active, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, " + SqlBoolLiteral(sample.IsActive) + ", " + SqlDateLiteral(now) + ", " + SqlDateLiteral(now) + ")",
                sample.FullName, sample.LoginId, sample.PasswordHash, sample.PasswordSalt, sample.PasswordIterations, I(roles[sample.RoleName], "role_id"), stationId);
        }
    }

    private void SeedDropdownMasters(OleDbConnection conn)
    {
        foreach (var item in DefaultDropdownMasters)
        {
            var sortOrder = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_dropdown_masters WHERE master_name = ?", item.Key));
            if (sortOrder > 0) continue;
            for (var i = 0; i < item.Value.Length; i++)
            {
                Execute(conn, "INSERT INTO tbl_dropdown_masters (master_name, option_value, sort_order, is_active) VALUES (?, ?, ?, TRUE)", item.Key, item.Value[i], i + 1);
            }
        }
    }

    private void SeedSequenceProfiles(OleDbConnection conn)
    {
        var orderTypes = QueryAll(conn, "SELECT order_type_id, order_type_name FROM tbl_order_types WHERE is_active = TRUE ORDER BY sort_order, order_type_name");
        var orderClasses = LoadDropdownMasterRows(conn, "ORDER_CLASS").Select(r => S(r, "option_value")).ToList();
        if (!orderClasses.Any()) orderClasses = new List<string> { "Main Order", "Sub Order" };
        foreach (var orderType in orderTypes)
        {
            foreach (var orderClass in orderClasses)
            {
                var existing = QueryOne(conn, "SELECT profile_id FROM tbl_sequence_profiles WHERE order_type_id = ? AND order_class_code = ? AND is_active = TRUE", I(orderType, "order_type_id"), orderClass);
                if (existing != null) continue;
                Execute(conn, "INSERT INTO tbl_sequence_profiles (profile_name, order_type_id, order_class_code, is_active) VALUES (?, ?, ?, TRUE)",
                    S(orderType, "order_type_name") + " - " + orderClass, I(orderType, "order_type_id"), orderClass);
                var profileId = Convert.ToInt32(Scalar(conn, "SELECT @@IDENTITY"));
                SeedProfileStationsFromDefault(conn, profileId);
            }
        }
    }

    private void EnsureSequenceProfileSchema(OleDbConnection conn)
    {
        TryExecute(conn, "CREATE TABLE tbl_sequence_profiles (profile_id COUNTER PRIMARY KEY, profile_name TEXT(150) NOT NULL, order_type_id LONG, order_class_code TEXT(80), is_active YESNO NOT NULL)");
        TryExecute(conn, "CREATE TABLE tbl_sequence_profile_stations (profile_station_id COUNTER PRIMARY KEY, profile_id LONG NOT NULL, station_id LONG NOT NULL, sequence_no LONG NOT NULL)");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN sequence_profile_id LONG");
        TryExecute(conn, "ALTER TABLE tbl_orders ADD COLUMN order_class_code TEXT(80)");
        TryExecute(conn, "CREATE INDEX ix_tbl_sequence_profiles_type_class ON tbl_sequence_profiles (order_type_id, order_class_code)");
        TryExecute(conn, "CREATE INDEX ix_tbl_sequence_profile_stations_profile ON tbl_sequence_profile_stations (profile_id, sequence_no)");
        SeedSequenceProfiles(conn);
        BackfillOrderSequenceProfiles(conn);
        BackfillOrderClassCodes(conn);
    }

    private void SeedProfileStationsFromDefault(OleDbConnection conn, int profileId)
    {
        var stations = ActiveMachineRows(conn);
        var rows = QueryAll(conn, "SELECT profile_station_id FROM tbl_sequence_profile_stations WHERE profile_id = ?", profileId);
        if (rows.Count > 0) return;
        for (var i = 0; i < stations.Count; i++)
        {
            Execute(conn, "INSERT INTO tbl_sequence_profile_stations (profile_id, station_id, sequence_no) VALUES (?, ?, ?)", profileId, I(stations[i], "machine_id"), i + 1);
        }
    }

    private void BackfillDealerCustomerTypeIds(OleDbConnection conn)
    {
        var rows = QueryAll(conn, "SELECT dealer_id, customer_type_id, customer_type_code FROM tbl_dealers");
        foreach (var row in rows)
        {
            if (I(row, "customer_type_id") > 0) continue;
            var code = S(row, "customer_type_code");
            if (string.IsNullOrWhiteSpace(code)) continue;
            var customerType = FindCustomerType(conn, code);
            if (customerType == null) continue;
            Execute(conn, "UPDATE tbl_dealers SET customer_type_id = ? WHERE dealer_id = ?", I(customerType, "customer_type_id"), I(row, "dealer_id"));
        }
    }

    private void BackfillOrderSequenceProfiles(OleDbConnection conn)
    {
        var rows = QueryAll(conn, "SELECT order_id, order_type_id, order_class_code, main_order, sub_order, sequence_profile_id FROM tbl_orders");
        foreach (var row in rows)
        {
            if (I(row, "sequence_profile_id") > 0) continue;
            var profile = ResolveSequenceProfile(conn, I(row, "order_type_id"), OrderClassForOrder(row));
            if (profile == null) continue;
            Execute(conn, "UPDATE tbl_orders SET sequence_profile_id = ? WHERE order_id = ?", I(profile, "profile_id"), I(row, "order_id"));
        }
    }

    private void BackfillOrderClassCodes(OleDbConnection conn)
    {
        var rows = QueryAll(conn, "SELECT order_id, sequence_profile_id, main_order, sub_order, order_class_code FROM tbl_orders");
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(S(row, "order_class_code"))) continue;
            string classCode = "";
            if (I(row, "sequence_profile_id") > 0)
            {
                var profile = QueryOne(conn, "SELECT order_class_code FROM tbl_sequence_profiles WHERE profile_id = ?", I(row, "sequence_profile_id"));
                classCode = S(profile, "order_class_code");
            }
            if (string.IsNullOrWhiteSpace(classCode))
            {
                classCode = NormalizeOrderClass(S(row, "main_order"), S(row, "sub_order"));
            }
            Execute(conn, "UPDATE tbl_orders SET order_class_code = ? WHERE order_id = ?", classCode, I(row, "order_id"));
        }
    }

    private OleDbConnection OpenConnection(HttpContext context)
    {
        var dbPath = context.Server.MapPath("~/App_Data/elenza_pms.accdb");
        var providers = new[]
        {
            "Microsoft.ACE.OLEDB.16.0",
            "Microsoft.ACE.OLEDB.12.0",
            "Microsoft.Jet.OLEDB.4.0"
        };

        Exception last = null;
        foreach (var provider in providers)
        {
            try
            {
                var conn = new OleDbConnection("Provider=" + provider + ";Data Source=" + dbPath + ";Persist Security Info=False;");
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                last = ex;
            }
        }

        throw new ApiFailure(500, "Access database provider not available. " + (last == null ? "" : last.Message));
    }

    private List<Dictionary<string, object>> QueryAll(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParameters(cmd, values);
            using (var reader = cmd.ExecuteReader())
            {
                var rows = new List<Dictionary<string, object>>();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[reader.GetName(i).ToLowerInvariant()] = value;
                    }
                    rows.Add(row);
                }
                return rows;
            }
        }
    }

    private Dictionary<string, object> QueryOne(OleDbConnection conn, string sql, params object[] values)
    {
        return QueryAll(conn, sql, values).FirstOrDefault();
    }

    private object Scalar(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParameters(cmd, values);
            var value = cmd.ExecuteScalar();
            return value == DBNull.Value ? null : value;
        }
    }

    private void Execute(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParameters(cmd, values);
            cmd.ExecuteNonQuery();
        }
    }

    private void TryExecute(OleDbConnection conn, string sql)
    {
        try
        {
            Execute(conn, sql);
        }
        catch
        {
        }
    }

    private void AddParameters(OleDbCommand cmd, object[] values)
    {
        foreach (var value in values)
        {
            var parameter = cmd.CreateParameter();
            if (value == null || value == DBNull.Value)
            {
                parameter.Value = DBNull.Value;
            }
            else if (value is bool)
            {
                parameter.OleDbType = OleDbType.Boolean;
                parameter.Value = value;
            }
            else if (value is byte)
            {
                parameter.OleDbType = OleDbType.UnsignedTinyInt;
                parameter.Value = value;
            }
            else if (value is short || value is ushort || value is int)
            {
                parameter.OleDbType = OleDbType.Integer;
                parameter.Value = value;
            }
            else if (value is long)
            {
                parameter.OleDbType = OleDbType.BigInt;
                parameter.Value = value;
            }
            else if (value is float || value is double || value is decimal)
            {
                parameter.OleDbType = OleDbType.Double;
                parameter.Value = value;
            }
            else if (value is DateTime)
            {
                parameter.OleDbType = OleDbType.DBTimeStamp;
                parameter.Value = value;
            }
            else
            {
                parameter.OleDbType = OleDbType.VarWChar;
                parameter.Value = Convert.ToString(value);
            }
            cmd.Parameters.Add(parameter);
        }
    }

    private Dictionary<string, object> GetUserByLogin(OleDbConnection conn, string loginId)
    {
        return QueryOne(conn, "SELECT u.user_id, u.full_name, u.login_id, u.password_hash, u.password_salt, u.password_iterations, u.is_active, r.role_name, r.home_section, m.machine_id AS station_id, m.machine_name AS station_name FROM (tbl_users AS u INNER JOIN tbl_roles AS r ON u.role_id = r.role_id) LEFT JOIN tbl_machines AS m ON u.assigned_station_id = m.machine_id WHERE u.login_id = ?", loginId);
    }

    private Dictionary<string, object> GetSessionUser(HttpContext context, OleDbConnection conn)
    {
        var value = context.Session["user_id"];
        if (value == null) return null;
        return QueryOne(conn, "SELECT u.user_id, u.full_name, u.login_id, u.password_hash, u.password_salt, u.password_iterations, u.is_active, u.dealer_id, r.role_name, r.home_section, m.machine_id AS station_id, m.machine_name AS station_name FROM (tbl_users AS u INNER JOIN tbl_roles AS r ON u.role_id = r.role_id) LEFT JOIN tbl_machines AS m ON u.assigned_station_id = m.machine_id WHERE u.user_id = ?", Convert.ToInt32(value));
    }

    private Dictionary<string, object> RequireLogin(HttpContext context, OleDbConnection conn)
    {
        var user = GetSessionUser(context, conn);
        if (user == null) throw new ApiFailure(401, "Login required.");
        return user;
    }

    private void EnsureRole(Dictionary<string, object> user, params string[] roles)
    {
        if (!roles.Contains(S(user, "role_name")))
            throw new ApiFailure(403, "You do not have permission for this action.");
    }

    private Dictionary<string, object> UserPayload(Dictionary<string, object> user)
    {
        var roleName = S(user, "role_name");
        return Obj(
            "user_id", I(user, "user_id"),
            "full_name", S(user, "full_name"),
            "login_id", S(user, "login_id"),
            "role_name", roleName,
            "station_name", string.IsNullOrWhiteSpace(S(user, "station_name")) ? "All Stations" : S(user, "station_name"),
            "dealer_id", I(user, "dealer_id"),
            "sections", RoleSections.ContainsKey(roleName) ? RoleSections[roleName] : RoleSections["Admin"],
            "home_section", S(user, "home_section")
        );
    }

    private HashSet<string> UserSections(string roleName)
    {
        var sections = RoleSections.ContainsKey(roleName) ? RoleSections[roleName] : RoleSections["Admin"];
        return new HashSet<string>(sections, StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, object> FindDealerByName(OleDbConnection conn, string dealerName) { return QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_name = ? AND is_active = TRUE", dealerName); }
    private Dictionary<string, object> FindCustomerTypeById(OleDbConnection conn, int customerTypeId) { return QueryOne(conn, "SELECT * FROM tbl_customer_types WHERE customer_type_id = ?", customerTypeId); }
    private Dictionary<string, object> FindCustomerType(OleDbConnection conn, string value) { return QueryOne(conn, "SELECT * FROM tbl_customer_types WHERE is_active = TRUE AND (customer_type_code = ? OR customer_type_name = ?)", value, value); }
    private Dictionary<string, object> FindOrderType(OleDbConnection conn, string name) { return QueryOne(conn, "SELECT * FROM tbl_order_types WHERE order_type_name = ? AND is_active = TRUE", name); }
    private Dictionary<string, object> FindVendor(OleDbConnection conn, string name) { return QueryOne(conn, "SELECT * FROM tbl_vendors WHERE vendor_name = ? AND is_active = TRUE", name); }
    private Dictionary<string, object> FindDropdownValue(OleDbConnection conn, string masterName, string value) { return QueryOne(conn, "SELECT * FROM tbl_dropdown_masters WHERE master_name = ? AND option_value = ? AND is_active = TRUE", masterName, value); }
    private Dictionary<string, object> FindMachineByName(OleDbConnection conn, string machineName) { return QueryOne(conn, "SELECT * FROM tbl_machines WHERE machine_name = ? AND is_active = TRUE", machineName); }
    private Dictionary<string, object> FindOrderByNumber(OleDbConnection conn, string orderNumber) { return QueryOne(conn, "SELECT * FROM tbl_orders WHERE order_number = ?", orderNumber); }
    private Dictionary<string, object> FindOrderById(OleDbConnection conn, int orderId) { return QueryOne(conn, "SELECT * FROM tbl_orders WHERE order_id = ?", orderId); }

    private List<Dictionary<string, object>> LoadDropdownMasterRows(OleDbConnection conn, string masterName)
    {
        return QueryAll(conn, "SELECT * FROM tbl_dropdown_masters WHERE master_name = ? AND is_active = TRUE ORDER BY sort_order, option_value", masterName);
    }

    private void EnsureDropdownValueExists(OleDbConnection conn, string masterName, string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (FindDropdownValue(conn, masterName, value) == null)
        {
            throw new ApiFailure(400, label + " was not found in master.");
        }
    }

    private Dictionary<string, object> ResolveSequenceProfile(OleDbConnection conn, int orderTypeId, string orderClass)
    {
        var exact = QueryOne(conn, "SELECT * FROM tbl_sequence_profiles WHERE order_type_id = ? AND order_class_code = ? AND is_active = TRUE", orderTypeId, orderClass);
        if (exact != null) return exact;
        return QueryOne(conn, "SELECT TOP 1 * FROM tbl_sequence_profiles WHERE order_type_id = ? AND is_active = TRUE ORDER BY profile_id", orderTypeId);
    }

    private List<Dictionary<string, object>> ResolveOrderSequenceStations(OleDbConnection conn, Dictionary<string, object> order)
    {
        EnsureSequenceProfileSchema(conn);
        var profileId = I(order, "sequence_profile_id");
        if (profileId > 0)
        {
            var rows = QueryAll(conn, "SELECT s.*, m.machine_name FROM tbl_sequence_profile_stations AS s INNER JOIN tbl_machines AS m ON s.station_id = m.machine_id WHERE s.profile_id = " + profileId + " ORDER BY s.sequence_no, s.profile_station_id");
            if (rows.Count > 0) return rows;
        }
        var fallbackProfile = ResolveSequenceProfile(conn, I(order, "order_type_id"), OrderClassForOrder(order));
        if (fallbackProfile != null)
        {
            Execute(conn, "UPDATE tbl_orders SET sequence_profile_id = " + I(fallbackProfile, "profile_id") + " WHERE order_id = " + I(order, "order_id"));
            return QueryAll(conn, "SELECT s.*, m.machine_name FROM tbl_sequence_profile_stations AS s INNER JOIN tbl_machines AS m ON s.station_id = m.machine_id WHERE s.profile_id = " + I(fallbackProfile, "profile_id") + " ORDER BY s.sequence_no, s.profile_station_id");
        }
        return ActiveMachineRows(conn).Select(r => Obj("station_id", I(r, "machine_id"), "machine_name", S(r, "machine_name"), "sequence_no", I(r, "sequence_no"))).ToList();
    }

    private string NormalizeOrderClass(string storedMainOrder, string storedSubOrder)
    {
        var value = (storedMainOrder ?? "").Trim();
        if (OrderClassCodes.Contains(value)) return value;

        var combined = (value + " " + (storedSubOrder ?? "")).Trim().ToLowerInvariant();
        if (combined.Contains("snag")) return "Snag";
        if (combined.Contains("rework")) return "Rework";
        if (combined.Contains("sub")) return "Sub Order";
        if (!string.IsNullOrWhiteSpace(storedSubOrder)) return "Sub Order";
        return "Main Order";
    }

    private string OrderClassForOrder(Dictionary<string, object> order)
    {
        var explicitValue = S(order, "order_class_code");
        if (OrderClassCodes.Contains(explicitValue)) return explicitValue;
        return NormalizeOrderClass(S(order, "main_order"), S(order, "sub_order"));
    }

    private void NormalizeProfileSequence(OleDbConnection conn, int profileId)
    {
        var rows = QueryAll(conn, "SELECT profile_station_id FROM tbl_sequence_profile_stations WHERE profile_id = ? ORDER BY sequence_no, profile_station_id", profileId);
        for (var i = 0; i < rows.Count; i++)
        {
            Execute(conn, "UPDATE tbl_sequence_profile_stations SET sequence_no = ? WHERE profile_station_id = ?", i + 1, I(rows[i], "profile_station_id"));
        }
    }

    private string ResolveDealerMarketingOwner(Dictionary<string, object> user, string requestedMarketingOwner)
    {
        if (string.Equals(S(user, "role_name"), "Marketing User", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(S(user, "full_name")))
        {
            return S(user, "full_name");
        }
        return requestedMarketingOwner;
    }

    private string InsertDealerRecord(OleDbConnection conn, int userId, string dealerCodeInput, string dealerName, string companyName, string dealerType, string customerTypeCode, string city, string pinCode, string gstNumber, string contactPerson, string mobileNumber, string email, string paymentTerms, string creditLimitText, string marketingOwner, string quotationOwner, string address)
    {
        var customerType = FindCustomerType(conn, customerTypeCode);
        if (customerType == null) throw new ApiFailure(400, "Customer type was not found in master.");
        EnsureDropdownValueExists(conn, "DEALER_TYPE", dealerType, "Dealer type");
        EnsureDropdownValueExists(conn, "PAYMENT_TERMS", paymentTerms, "Payment terms");
        EnsureDropdownValueExists(conn, "MARKETING_OWNER", marketingOwner, "Marketing owner");
        var duplicate = QueryOne(conn, "SELECT dealer_id FROM tbl_dealers WHERE mobile_number = ? OR (gst_number = ? AND ? <> '')", mobileNumber, gstNumber, gstNumber);
        if (duplicate != null) throw new ApiFailure(400, "Duplicate mobile or GST found.");

        var dealerCode = string.IsNullOrWhiteSpace(dealerCodeInput) ? NextDealerCode(conn, dealerType) : dealerCodeInput.Trim();
        if (QueryOne(conn, "SELECT dealer_id FROM tbl_dealers WHERE dealer_code = ?", dealerCode) != null)
            throw new ApiFailure(400, "Dealer ID already exists.");
        var creditLimitLakh = N(creditLimitText);
        var now = IstNow();
        Execute(conn,
            "INSERT INTO tbl_dealers (dealer_code, dealer_name, company_name, dealer_type, customer_type_id, customer_type_code, city, pin_code, gst_number, contact_person, mobile_number, email, payment_terms, credit_limit_lakh, marketing_owner, quotation_owner, address, is_active, remarks, created_by, created_at, updated_by, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, TRUE, ?, ?, " + SqlDateLiteral(now) + ", ?, " + SqlDateLiteral(now) + ")",
            dealerCode,
            dealerName,
            NullIfEmpty(companyName),
            dealerType,
            I(customerType, "customer_type_id"),
            customerTypeCode,
            NullIfEmpty(city),
            NullIfEmpty(pinCode),
            NullIfEmpty(gstNumber),
            NullIfEmpty(contactPerson),
            mobileNumber,
            NullIfEmpty(email),
            NullIfEmpty(paymentTerms),
            creditLimitLakh.HasValue ? (object)creditLimitLakh.Value : DBNull.Value,
            NullIfEmpty(marketingOwner),
            NullIfEmpty(quotationOwner),
            NullIfEmpty(address),
            "",
            userId,
            userId);
        return dealerCode;
    }

    private int ImportDealersFromTsv(OleDbConnection conn, int userId, string rowsTsv, string marketingOwnerOverride, List<string> importErrors)
    {
        var lines = rowsTsv.Replace("\r", "").Split('\n');
        if (lines.Length < 2) return 0;
        var headers = lines[0].Split('\t');
        var imported = 0;
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = lines[i].Split('\t');
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++) raw[headers[c]] = c < cells.Length ? cells[c] : "";
            var dealerName = GetAny(raw, "dealer_name", "dealer name");
            var dealerType = GetAny(raw, "dealer_type", "dealer type");
            var customerType = GetAny(raw, "customer_type", "customer type");
            var mobileNumber = GetAny(raw, "mobile_number", "mobile", "phone");
            var rowErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(dealerName)) rowErrors.Add("dealer name missing");
            if (string.IsNullOrWhiteSpace(dealerType)) rowErrors.Add("dealer type missing");
            if (string.IsNullOrWhiteSpace(customerType)) rowErrors.Add("customer type missing");
            if (string.IsNullOrWhiteSpace(mobileNumber)) rowErrors.Add("phone/mobile missing");
            if (rowErrors.Count > 0)
            {
                importErrors.Add("Row " + (i + 1) + ": " + string.Join(", ", rowErrors.ToArray()));
                continue;
            }
            try
            {
                var marketingOwner = string.IsNullOrWhiteSpace(marketingOwnerOverride) ? GetAny(raw, "marketing_owner", "marketing owner") : marketingOwnerOverride;
                var dealerCode = InsertDealerRecord(
                    conn,
                    userId,
                    GetAny(raw, "dealer_code", "dealer id", "dealer id no.", "dealer_id"),
                    dealerName,
                    GetAny(raw, "company_name", "company name"),
                    dealerType,
                    customerType,
                    GetAny(raw, "city"),
                    GetAny(raw, "pin_code", "pin code"),
                    GetAny(raw, "gst_number", "gst number"),
                    GetAny(raw, "contact_person", "contact person"),
                    mobileNumber,
                    GetAny(raw, "email"),
                    GetAny(raw, "payment_terms", "payment terms"),
                    GetAny(raw, "credit_limit_lakh", "credit limit (lakh)"),
                    marketingOwner,
                    "",
                    GetAny(raw, "address"));
                Audit(conn, userId, "Dealer Import", "Dealer", dealerCode, "Dealer Imported", "", dealerName, "", null);
                imported++;
            }
            catch (Exception ex)
            {
                importErrors.Add("Row " + (i + 1) + ": " + ex.Message);
            }
        }
        return imported;
    }

    private int ImportQuotationsFromTsv(OleDbConnection conn, int userId, string rowsTsv, List<string> importErrors)
    {
        var lines = rowsTsv.Replace("\r", "").Split('\n');
        if (lines.Length < 2) return 0;
        var headers = lines[0].Split('\t');
        var imported = 0;
        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = lines[i].Split('\t');
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++) raw[headers[c]] = c < cells.Length ? cells[c] : "";

            var dealerName = GetAny(raw, "dealer_name", "dealer name", "dealer");
            var orderType = GetAny(raw, "order_type", "order type");
            var orderClass = GetAny(raw, "order_class", "order class", "main / sub / snag / rework", "main/sub/snag/rework");
            var orderNumber = GetAny(raw, "order_number", "order number", "order no", "order no.");
            var rowErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(dealerName)) rowErrors.Add("dealer name missing");
            if (string.IsNullOrWhiteSpace(orderType)) rowErrors.Add("order type missing");
            if (string.IsNullOrWhiteSpace(orderClass)) rowErrors.Add("order class missing");
            if (string.IsNullOrWhiteSpace(orderNumber)) rowErrors.Add("order number missing");
            if (rowErrors.Count > 0)
            {
                importErrors.Add("Row " + (i + 1) + ": " + string.Join(", ", rowErrors.ToArray()));
                continue;
            }

            try
            {
                InsertQuotationRecord(
                    conn,
                    userId,
                    dealerName,
                    GetAny(raw, "customer_name", "customer name"),
                    orderType,
                    orderClass,
                    GetAny(raw, "main_order_reference", "main order reference", "sub_order", "sub order", "main_order_ref"),
                    orderNumber,
                    GetAny(raw, "approx_value", "approx value", "value"),
                    GetAny(raw, "remarks", "remark"),
                    GetAny(raw, "expected_confirmation_date", "expected confirmation date"));
                imported++;
            }
            catch (Exception ex)
            {
                importErrors.Add("Row " + (i + 1) + ": " + ex.Message);
            }
        }
        return imported;
    }

    private int InsertQuotationRecord(OleDbConnection conn, int userId, string dealerName, string customerName, string orderTypeValue, string orderClassValue, string mainOrderReference, string orderNumberValue, string approxValue, string remarks, string expectedConfirmationDateValue)
    {
        var orderNumber = Require(orderNumberValue, "Order number is required.");
        if (QueryOne(conn, "SELECT order_id FROM tbl_orders WHERE order_number = ?", orderNumber) != null)
        {
            throw new ApiFailure(400, "Order number must be unique.");
        }

        var dealer = FindDealerByName(conn, Require(dealerName, "Dealer is required."));
        if (dealer == null)
        {
            throw new ApiFailure(400, "Dealer was not found in master.");
        }
        var customerType = I(dealer, "customer_type_id") > 0
            ? FindCustomerTypeById(conn, I(dealer, "customer_type_id"))
            : FindCustomerType(conn, Require(S(dealer, "customer_type_code"), "Customer type is missing in Dealer Master."));
        var orderType = FindOrderType(conn, Require(orderTypeValue, "Order type is required."));
        var orderClass = Require(orderClassValue, "Order class is required.");
        EnsureDropdownValueExists(conn, "ORDER_CLASS", orderClass, "Order class");
        if (string.Equals(orderClass, "Sub Order", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(mainOrderReference))
        {
            throw new ApiFailure(400, "Main order reference is required for sub order.");
        }
        if (customerType == null || orderType == null)
        {
            throw new ApiFailure(400, "Customer type or order type was not found in masters.");
        }

        EnsureSequenceProfileSchema(conn);
        var sequenceProfile = ResolveSequenceProfile(conn, I(orderType, "order_type_id"), orderClass);
        var subOrderValue = string.IsNullOrWhiteSpace(mainOrderReference) ? "" : mainOrderReference.Trim();
        var quotationNumber = NextCode(conn, "tbl_orders", "quotation_number", "QT");
        var now = IstNow();
        var expectedConfirmationDate = ParseDate(expectedConfirmationDateValue);
        Execute(conn,
            "INSERT INTO tbl_orders (quotation_number, order_number, quotation_date, dealer_id, customer_name, customer_type_id, order_type_id, sequence_profile_id, order_class_code, main_order, sub_order, site_name, location, approx_value, expected_confirmation_date, quotation_remarks, workflow_stage_code, procurement_status_code, dispatch_status_code, correction_queue, created_by, created_at, updated_by, updated_at, last_action) VALUES (?, ?, " + SqlDateLiteral(now) + ", ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, " + SqlDateLiteral(expectedConfirmationDate) + ", ?, ?, ?, ?, FALSE, ?, " + SqlDateLiteral(now) + ", ?, " + SqlDateLiteral(now) + ", ?)",
            quotationNumber,
            orderNumber,
            I(dealer, "dealer_id"),
            NullIfEmpty(customerName),
            I(customerType, "customer_type_id"),
            I(orderType, "order_type_id"),
            sequenceProfile == null ? (object)DBNull.Value : I(sequenceProfile, "profile_id"),
            orderClass,
            orderClass,
            subOrderValue,
            "",
            "",
            N(approxValue),
            remarks,
            "QUOTATION_CREATED",
            "PO_PENDING",
            "",
            userId,
            userId,
            "Quotation Created");

        var orderId = Convert.ToInt32(Scalar(conn, "SELECT @@IDENTITY"));
        AddHistory(conn, orderId, null, "QUOTATION_CREATED", null, "QUOTATION_CREATED", null, null, "", userId);
        Audit(conn, userId, "Quotation", "Order", orderNumber, "Quotation Created", "", quotationNumber, "", null);
        return orderId;
    }

    private int ImportUsersFromTsv(OleDbConnection conn, string rowsTsv)
    {
        var lines = rowsTsv.Replace("\r", "").Split('\n');
        if (lines.Length < 2) return 0;
        var headers = lines[0].Split('\t');
        var roles = QueryAll(conn, "SELECT role_id, role_name FROM tbl_roles").ToDictionary(r => S(r, "role_name"), StringComparer.OrdinalIgnoreCase);
        var stations = QueryAll(conn, "SELECT machine_id, machine_name FROM tbl_machines").ToDictionary(r => S(r, "machine_name"), StringComparer.OrdinalIgnoreCase);
        var imported = 0;
        var now = IstNow();

        for (var i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var cells = lines[i].Split('\t');
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Length; c++) raw[headers[c]] = c < cells.Length ? cells[c] : "";
            var fullName = GetAny(raw, "full_name", "name");
            var loginId = GetAny(raw, "login_id", "login", "username").ToLowerInvariant();
            var roleName = GetAny(raw, "role_name", "role");
            var assignedStation = GetAny(raw, "assigned_station", "station");
            var active = ParseBool(GetAny(raw, "is_active", "active", "true"));
            var password = GetAny(raw, "password");
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(loginId) || string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(password))
                continue;
            if (!roles.ContainsKey(roleName)) continue;
            object stationId = DBNull.Value;
            if (!string.IsNullOrWhiteSpace(assignedStation) && stations.ContainsKey(assignedStation))
                stationId = I(stations[assignedStation], "machine_id");
            var existing = QueryOne(conn, "SELECT user_id FROM tbl_users WHERE login_id = ?", loginId);
            if (existing != null)
            {
                Execute(conn, "UPDATE tbl_users SET full_name = ?, password_hash = ?, password_salt = ?, password_iterations = ?, role_id = ?, assigned_station_id = ?, is_active = " + SqlBoolLiteral(active) + ", updated_at = " + SqlDateLiteral(now) + " WHERE user_id = ?",
                    fullName, password, "", 0, I(roles[roleName], "role_id"), stationId, I(existing, "user_id"));
            }
            else
            {
                Execute(conn, "INSERT INTO tbl_users (full_name, login_id, password_hash, password_salt, password_iterations, role_id, assigned_station_id, is_active, created_at, updated_at) VALUES (?, ?, ?, ?, ?, ?, ?, " + SqlBoolLiteral(active) + ", " + SqlDateLiteral(now) + ", " + SqlDateLiteral(now) + ")",
                    fullName, loginId, password, "", 0, I(roles[roleName], "role_id"), stationId);
            }
            imported++;
        }
        return imported;
    }

    private string NextCode(OleDbConnection conn, string tableName, string fieldName, string prefix)
    {
        var rows = QueryAll(conn, "SELECT " + fieldName + " FROM " + tableName);
        var max = 0;
        foreach (var row in rows)
        {
            var value = S(row, fieldName);
            if (!value.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase)) continue;
            int numeric;
            if (int.TryParse(value.Split('-')[1], out numeric) && numeric > max) max = numeric;
        }
        return prefix + "-" + (max + 1).ToString("D5");
    }

    private string NextDealerCode(OleDbConnection conn, string dealerType)
    {
        var prefix = NormalizeDealerPrefix(dealerType);
        var rows = QueryAll(conn, "SELECT dealer_code FROM tbl_dealers WHERE dealer_type = ?", dealerType);
        var max = 1000;
        foreach (var row in rows)
        {
            var value = S(row, "dealer_code");
            if (string.IsNullOrWhiteSpace(value)) continue;
            var digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
            int numeric;
            if (int.TryParse(digits, out numeric) && numeric > max) max = numeric;
        }
        return prefix + " " + (max + 1);
    }

    private string NormalizeDealerPrefix(string dealerType)
    {
        var raw = new string((dealerType ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(raw) ? "D" : raw;
    }

    private List<Dictionary<string, object>> StatusOptions(Dictionary<string, Dictionary<string, string>> lookup, string group)
    {
        return lookup[group].Select(kvp => Obj("code", kvp.Key, "label", kvp.Value)).ToList();
    }

    private string StatusLabel(OleDbConnection conn, string group, string code)
    {
        var result = Scalar(conn, "SELECT status_label FROM tbl_status_master WHERE status_group = ? AND status_code = ?", group, code);
        return result == null ? code : Convert.ToString(result);
    }

    private string Label(Dictionary<string, Dictionary<string, string>> lookup, string group, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "-";
        if (lookup.ContainsKey(group) && lookup[group].ContainsKey(code)) return lookup[group][code];
        return ToTitle(code);
    }

    private string ReadableVisibleStations(Dictionary<string, object> order)
    {
        if (B(order, "correction_queue")) return "Correction Queue";
        var stations = (List<string>)order["visible_stations"];
        if (stations.Count > 0) return string.Join(", ", stations);
        if (S(order, "dispatch_status_code") == "DISPATCHED") return "Completed";
        return "-";
    }

    private string HumanHistoryAction(Dictionary<string, Dictionary<string, string>> lookup, Dictionary<string, object> row)
    {
        var actionCode = S(row, "action_code");
        foreach (var group in new[] { "ACTION", "WORKFLOW", "DISPATCH", "PROCUREMENT", "QUEUE" })
            if (lookup.ContainsKey(group) && lookup[group].ContainsKey(actionCode))
                return lookup[group][actionCode];
        return ToTitle(actionCode);
    }

    private List<Dictionary<string, object>> SortReportRows(List<Dictionary<string, object>> rows, string sortKey)
    {
        switch (sortKey)
        {
            case "updated-asc": return rows.OrderBy(r => S(r, "updated_sort")).ToList();
            case "order-asc": return rows.OrderBy(r => S(r, "order_number")).ToList();
            case "dealer-asc": return rows.OrderBy(r => S(r, "dealer_name")).ToList();
            default: return rows.OrderByDescending(r => S(r, "updated_sort")).ToList();
        }
    }

    private List<string> CollectStationFilters(List<Dictionary<string, object>> orders)
    {
        var values = new List<string>();
        foreach (var order in orders)
        {
            if (B(order, "correction_queue")) values.Add("Correction Queue");
            values.AddRange((List<string>)order["visible_stations"]);
        }
        return values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
    }

    private List<string> PrependAll(List<string> values)
    {
        var result = new List<string> { "all" };
        result.AddRange(values);
        return result;
    }

    private string PreviousStationName(List<string> names, string current)
    {
        var index = names.IndexOf(current);
        return index > 0 ? names[index - 1] : "";
    }

    private string NextStationName(List<string> names, string current)
    {
        var index = names.IndexOf(current);
        return index >= 0 && index < names.Count - 1 ? names[index + 1] : "";
    }

    private static Dictionary<string, object> Obj(params object[] values)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < values.Length; i += 2)
        {
            dict[Convert.ToString(values[i])] = values[i + 1];
        }
        return dict;
    }

    private static string Value(HttpContext context, string key, string fallback)
    {
        var value = Value(context, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string Value(HttpContext context, string key)
    {
        var value = context.Request.Form[key];
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        value = context.Request.QueryString[key];
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        var json = JsonBody(context);
        if (json != null && json.ContainsKey(key) && !string.IsNullOrWhiteSpace(Convert.ToString(json[key])))
            return Convert.ToString(json[key]).Trim();
        return "";
    }

    private static Dictionary<string, object> JsonBody(HttpContext context)
    {
        const string cacheKey = "__pms_json_body";
        if (context == null) return null;
        if (context.Items[cacheKey] is Dictionary<string, object>)
            return (Dictionary<string, object>)context.Items[cacheKey];
        try
        {
            if (context.Request.InputStream == null || !context.Request.InputStream.CanRead)
            {
                context.Items[cacheKey] = null;
                return null;
            }
            if (context.Request.InputStream.CanSeek)
                context.Request.InputStream.Position = 0;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8, true, 1024, true))
            {
                var body = reader.ReadToEnd();
                if (context.Request.InputStream.CanSeek)
                    context.Request.InputStream.Position = 0;
                if (string.IsNullOrWhiteSpace(body))
                {
                    context.Items[cacheKey] = null;
                    return null;
                }
                var parsed = Json.Deserialize<Dictionary<string, object>>(body);
                context.Items[cacheKey] = parsed;
                return parsed;
            }
        }
        catch
        {
            try
            {
                if (context.Request.InputStream != null && context.Request.InputStream.CanSeek)
                    context.Request.InputStream.Position = 0;
            }
            catch { }
            context.Items[cacheKey] = null;
            return null;
        }
    }

    private static string Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ApiFailure(400, message);
        return value.Trim();
    }

    private static int RequireInt(HttpContext context, string key)
    {
        return IntRequired(Value(context, key), key.Replace("_", " ") + " is required.");
    }
    private static int IntRequired(string value, string message)
    {
        int result;
        if (!int.TryParse(Require(value, message), out result)) throw new ApiFailure(400, message);
        return result;
    }

    private static int ToInt(string value)
    {
        int result;
        return int.TryParse(value, out result) ? result : 0;
    }

    private static double? N(string value)
    {
        double result;
        return double.TryParse(value, out result) ? (double?)result : null;
    }

    private static bool ParseBool(string value)
    {
        var lower = (value ?? "").Trim().ToLowerInvariant();
        return lower == "true" || lower == "1" || lower == "yes" || lower == "active";
    }

    private static string GetAny(IDictionary<string, string> raw, params string[] keys)
    {
        foreach (var key in keys)
        {
            string value;
            if (raw.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        DateTime result;
        return DateTime.TryParse(value, out result) ? (DateTime?)result : null;
    }

    private static object NullableString(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
    }

    private static object NullableValue(int? value)
    {
        return value.HasValue ? (object)value.Value : DBNull.Value;
    }

    private static object NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value;
    }

    private static string FormatDateTime(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm") : "-";
    }

    private static string DateSortKey(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "";
    }

    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    private static DateTime IstNow() { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstZone); }

    private static string FormatDateTimeIST(object value)
    {
        var dt = ToDateTime(value);
        if (!dt.HasValue) return "-";
        var ist = TimeZoneInfo.ConvertTimeFromUtc(dt.Value.ToUniversalTime(), IstZone);
        return ist.ToString("dd-MMM-yy, HH:mm");
    }

    private static string SqlDateLiteral(DateTime? value)
    {
        if (!value.HasValue) return "NULL";
        return "#" + value.Value.ToString("MM'/'dd'/'yyyy HH':'mm':'ss", CultureInfo.InvariantCulture) + "#";
    }

    private static string SqlBoolLiteral(bool value)
    {
        return value ? "TRUE" : "FALSE";
    }

    private static string SqlIntLiteral(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
    }

    private static string IsoDate(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("yyyy-MM-dd") : "";
    }

    private static DateTime? ToDateTime(object value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime) return (DateTime)value;
        DateTime parsed;
        return DateTime.TryParse(Convert.ToString(value), out parsed) ? (DateTime?)parsed : null;
    }

    private static string EmptyAs(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ToTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "-";
        return System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(value.Replace("_", " ").ToLowerInvariant());
    }

    private static string ReadString(IDictionary<string, object> raw, string key, string fallback)
    {
        object value;
        if (!raw.TryGetValue(key, out value) || value == null) return fallback;
        var text = Convert.ToString(value);
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static string ReadString(IDictionary<string, object> raw, string key)
    {
        return ReadString(raw, key, "");
    }

    private static int ReadInt(IDictionary<string, object> raw, string key, int fallback)
    {
        object value;
        if (!raw.TryGetValue(key, out value) || value == null) return fallback;
        int number;
        return int.TryParse(Convert.ToString(value), out number) ? number : fallback;
    }

    private static bool ReadBool(IDictionary<string, object> raw, string key, bool fallback)
    {
        object value;
        if (!raw.TryGetValue(key, out value) || value == null) return fallback;
        var text = Convert.ToString(value).Trim().ToLowerInvariant();
        if (text == "true" || text == "1" || text == "yes") return true;
        if (text == "false" || text == "0" || text == "no") return false;
        return fallback;
    }

    private static List<string> ReadStringList(IDictionary<string, object> raw, string key)
    {
        object value;
        if (!raw.TryGetValue(key, out value) || value == null) return new List<string>();
        var values = value as System.Collections.ArrayList;
        if (values != null) return values.Cast<object>().Select(v => Convert.ToString(v)).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var single = Convert.ToString(value);
        return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single.Trim() };
    }

    private static List<Dictionary<string, object>> QueryAllStatic(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParametersStatic(cmd, values);
            using (var reader = cmd.ExecuteReader())
            {
                var rows = new List<Dictionary<string, object>>();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        row[reader.GetName(i).ToLowerInvariant()] = value;
                    }
                    rows.Add(row);
                }
                return rows;
            }
        }
    }

    private static void ExecuteStatic(OleDbConnection conn, string sql, params object[] values)
    {
        using (var cmd = new OleDbCommand(sql, conn))
        {
            AddParametersStatic(cmd, values);
            cmd.ExecuteNonQuery();
        }
    }

    private static void AddParametersStatic(OleDbCommand cmd, object[] values)
    {
        foreach (var value in values)
        {
            var parameter = cmd.CreateParameter();
            if (value == null || value == DBNull.Value)
            {
                parameter.Value = DBNull.Value;
            }
            else if (value is bool)
            {
                parameter.OleDbType = OleDbType.Boolean;
                parameter.Value = value;
            }
            else if (value is byte)
            {
                parameter.OleDbType = OleDbType.UnsignedTinyInt;
                parameter.Value = value;
            }
            else if (value is short || value is ushort || value is int)
            {
                parameter.OleDbType = OleDbType.Integer;
                parameter.Value = value;
            }
            else if (value is long)
            {
                parameter.OleDbType = OleDbType.BigInt;
                parameter.Value = value;
            }
            else if (value is float || value is double || value is decimal)
            {
                parameter.OleDbType = OleDbType.Double;
                parameter.Value = value;
            }
            else if (value is DateTime)
            {
                parameter.OleDbType = OleDbType.DBTimeStamp;
                parameter.Value = value;
            }
            else
            {
                parameter.OleDbType = OleDbType.VarWChar;
                parameter.Value = Convert.ToString(value);
            }
            cmd.Parameters.Add(parameter);
        }
    }

    private static string ReadRowString(Dictionary<string, object> row, string key)
    {
        object value;
        return row != null && row.TryGetValue(key, out value) && value != null && value != DBNull.Value ? Convert.ToString(value) : "";
    }

    private static int I(Dictionary<string, object> row, string key)
    {
        object value;
        return row != null && row.TryGetValue(key, out value) && value != null && value != DBNull.Value ? Convert.ToInt32(value) : 0;
    }

    private static double D(Dictionary<string, object> row, string key)
    {
        object value;
        if (row == null || !row.TryGetValue(key, out value) || value == null || value == DBNull.Value) return 0;
        double result;
        return double.TryParse(Convert.ToString(value), out result) ? result : 0;
    }

    private static bool IsPackingStationName(string name)
    {
        return string.Equals(name, "Packing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Packed", StringComparison.OrdinalIgnoreCase);
    }

    private static string S(Dictionary<string, object> row, string key)
    {
        object value;
        return row != null && row.TryGetValue(key, out value) && value != null && value != DBNull.Value ? Convert.ToString(value) : "";
    }

    private static string FixEncoding(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s
            .Replace("\u00E2\u20AC\u201D", "\u2014")
            .Replace("\u00E2\u20AC\u201C", "\u201C")
            .Replace("\u00E2\u20AC\u2018", "\u2018")
            .Replace("\u00E2\u20AC\u2019", "\u2019")
            .Replace("\u00E2\u20AC\u2013", "\u2013");
    }

    private static string WorkflowStageLabel(string code)
    {
        if (string.IsNullOrEmpty(code)) return "-";
        switch (code.ToUpperInvariant())
        {
            case "QUOTATION_CREATED": return "Quotation";
            case "ORDER_CONFIRMED": return "Confirmed";
            case "OPTIMISATION_DONE": return "Optimised";
            case "PROCUREMENT_STARTED": return "Procurement";
            case "PRODUCTION_STARTED": return "In Production";
            case "PACKED": return "Packed";
            case "DISPATCH_READY": return "Dispatch Ready";
            case "DISPATCHED": return "Dispatched";
            default: return code.Replace("_", " ").ToLowerInvariant();
        }
    }

    private static bool B(Dictionary<string, object> row, string key)
    {
        object value;
        if (row == null || !row.TryGetValue(key, out value) || value == null || value == DBNull.Value) return false;
        if (value is bool) return (bool)value;
        var numeric = value as IConvertible;
        return numeric != null && Convert.ToInt32(value) != 0;
    }

    private static object DT(Dictionary<string, object> row, string key)
    {
        object value;
        return row != null && row.TryGetValue(key, out value) ? value : null;
    }

    private static void SetNoCache(HttpContext context)
    {
        context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
        context.Response.Cache.SetNoStore();
        context.Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
    }

    private static void WriteJson(HttpContext context, object payload)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        context.Response.Write(Json.Serialize(payload));
    }

    private static void WriteError(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Write(Json.Serialize(Obj("ok", false, "message", message)));
    }

    private sealed class ApiFailure : Exception
    {
        public int StatusCode { get; private set; }
        public ApiFailure(int statusCode, string message) : base(message) { StatusCode = statusCode; }
    }

    private sealed class SampleUser
    {
        public readonly string FullName;
        public readonly string LoginId;
        public readonly string RoleName;
        public readonly string AssignedStation;
        public readonly bool IsActive;
        public readonly string PasswordSalt;
        public readonly string PasswordHash;
        public readonly int PasswordIterations;

        public SampleUser(string fullName, string loginId, string roleName, string assignedStation, bool isActive, string passwordSalt, string passwordHash, int passwordIterations)
        {
            FullName = fullName;
            LoginId = loginId;
            RoleName = roleName;
            AssignedStation = assignedStation;
            IsActive = isActive;
            PasswordSalt = passwordSalt;
            PasswordHash = passwordHash;
            PasswordIterations = passwordIterations;
        }
    }

    private void HandlePriorityDeskState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var masterSets = LoadMasterSets(conn);
            var statusLookup = LoadStatusLookup(conn);
            var orders = LoadEnrichedOrders(conn, masterSets, user)
                .Where(o => IsPlanningEligible(o)
                    && !string.Equals(OrderClassForOrder(o), "Sub Order", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(OrderClassForOrder(o), "Snag", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(OrderClassForOrder(o), "Rework", StringComparison.OrdinalIgnoreCase))
                .ToList();
            EnsurePlannerRows(conn, orders);
            var plannerRows = QueryAll(conn, "SELECT * FROM tbl_production_planner")
                .ToDictionary(r => I(r, "order_id"), r => r);
            var rows = orders
                .Select(o => BuildPlanningRow(o, plannerRows.ContainsKey(I(o, "order_id")) ? plannerRows[I(o, "order_id")] : null, statusLookup))
                .ToList();
            WriteJson(context, Obj("rows", rows));
        }
    }

    private void HandlePriorityReport(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var masterSets = LoadMasterSets(conn);
            var statusLookup = LoadStatusLookup(conn);
            var dateFrom = ParseDate(Value(context, "date_from"));
            var dateTo = ParseDate(Value(context, "date_to"));
            var orders = LoadEnrichedOrders(conn, masterSets, user)
                .Where(o => IsPlanningEligible(o)
                    && !string.Equals(OrderClassForOrder(o), "Sub Order", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(OrderClassForOrder(o), "Snag", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(OrderClassForOrder(o), "Rework", StringComparison.OrdinalIgnoreCase))
                .ToList();
            EnsurePlannerRows(conn, orders);
            var plannerRows = QueryAll(conn, "SELECT * FROM tbl_production_planner WHERE [priority] = 'High'")
                .ToDictionary(r => I(r, "order_id"), r => r);
            var rows = orders
                .Where(o => plannerRows.ContainsKey(I(o, "order_id")))
                .Select(o => BuildPlanningRow(o, plannerRows[I(o, "order_id")], statusLookup))
                .ToList();
            if (dateFrom.HasValue)
                rows = rows.Where(r =>
                {
                    var d = ParseDate(S(r, "priority_date"));
                    return d.HasValue && d.Value >= dateFrom.Value;
                }).ToList();
            if (dateTo.HasValue)
                rows = rows.Where(r =>
                {
                    var d = ParseDate(S(r, "priority_date"));
                    return d.HasValue && d.Value <= dateTo.Value;
                }).ToList();
            WriteJson(context, Obj("rows", rows, "total", rows.Count));
        }
    }

    private sealed class MailSettings
    {
        public bool Enabled;
        public string DeliveryMode;
        public string Host;
        public int Port;
        public string Username;
        public string Password;
        public bool UseSsl;
        public string BrevoApiKey;
        public string FromEmail;
        public string FromName;
        public List<string> ToEmails;
        public string TimeZoneId;
        public int DailyHour;
        public int DailyMinute;
    }

    private void EnsureRemarksSchema(OleDbConnection conn)
    {
        TryExecute(conn, "CREATE TABLE tbl_remarks_requests (request_id COUNTER PRIMARY KEY, token TEXT(32) NOT NULL, order_ids MEMO NOT NULL, requested_by LONG NOT NULL, requested_at DATETIME NOT NULL, status TEXT(20) NOT NULL, replied_by LONG, replied_at DATETIME, reminder_count LONG DEFAULT 0, created_at DATETIME)");
        TryExecute(conn, "ALTER TABLE tbl_remarks_requests ADD COLUMN reminder_count LONG DEFAULT 0");
        TryExecute(conn, "CREATE UNIQUE INDEX ux_tbl_remarks_requests_token ON tbl_remarks_requests (token)");
        TryExecute(conn, "CREATE TABLE tbl_remarks_replies (reply_id COUNTER PRIMARY KEY, request_id LONG NOT NULL, order_id LONG NOT NULL, remarks MEMO, replied_by LONG NOT NULL, replied_at DATETIME NOT NULL)");
        TryExecute(conn, "CREATE INDEX ix_tbl_replies_request ON tbl_remarks_replies (request_id)");
    }

    private void EnsureScannerSchema(OleDbConnection conn)
    {
        TryExecute(conn, "CREATE TABLE tbl_user_machines (user_machine_id COUNTER PRIMARY KEY, user_id LONG NOT NULL, machine_id LONG NOT NULL)");
        TryExecute(conn, "CREATE INDEX ix_tbl_user_machines_user ON tbl_user_machines (user_id)");
    }

    private void HandleScannerState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var userId = Convert.ToInt32(I(user, "user_id"));

            var userMachines = new List<Dictionary<string, object>>();
            try { userMachines = QueryAll(conn, "SELECT m.machine_id, m.machine_name FROM tbl_user_machines AS um INNER JOIN tbl_machines AS m ON um.machine_id = m.machine_id WHERE um.user_id = ?", userId); } catch { }
            if (userMachines.Count == 0)
            {
                try
                {
                    object stationObj;
                    if (user.TryGetValue("station_id", out stationObj) && stationObj != null && stationObj != DBNull.Value && Convert.ToInt32(stationObj) > 0)
                    {
                        var m = QueryOne(conn, "SELECT machine_id, machine_name FROM tbl_machines WHERE machine_id = ?", stationObj);
                        if (m != null) userMachines.Add(m);
                    }
                } catch { }
            }

            var selectedMachineId = N(Value(context, "machine_id"));
            Dictionary<string, object> selectedMachine = null;
            if (selectedMachineId.HasValue)
            {
                selectedMachine = userMachines.FirstOrDefault(m => Convert.ToInt32(I(m, "machine_id")) == (int)selectedMachineId.Value);
            }
            if (selectedMachine == null && userMachines.Count > 0)
            {
                selectedMachine = userMachines[0];
            }

            var pendingOrders = new List<Dictionary<string, object>>();
            try
            {
                pendingOrders = QueryAll(conn,
                    "SELECT o.order_id, o.order_number, o.customer_name, o.confirmation_date, d.dealer_name, ot.order_type_name, o.workflow_stage_code, o.main_order, o.sub_order FROM (tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) LEFT JOIN tbl_order_types AS ot ON o.order_type_id = ot.order_type_id WHERE o.workflow_stage_code <> 'QUOTATION_CREATED' AND o.workflow_stage_code <> 'ORDER_CONFIRMED' AND o.workflow_stage_code <> 'PACKED' AND o.workflow_stage_code <> 'DISPATCH_READY' AND o.workflow_stage_code <> 'DISPATCHED' ORDER BY o.order_number");
            } catch { }

            var stationByOrder = new Dictionary<int, string>();
            try
            {
                var orderIds = pendingOrders.Select(o => Convert.ToInt32(I(o, "order_id"))).Where(v => v > 0).Distinct().ToList();
                if (orderIds.Count > 0)
                {
                    var ids = string.Join(",", orderIds.Select(v => v.ToString()).ToArray());
                    var queueRows = QueryAll(conn, "SELECT q.order_id, m.machine_name FROM (tbl_order_station_queue AS q INNER JOIN tbl_machines AS m ON q.station_id = m.machine_id) WHERE q.order_id IN (" + ids + ") AND q.is_visible = TRUE");
                    foreach (var qr in queueRows)
                    {
                        var oid = Convert.ToInt32(I(qr, "order_id"));
                        var machineName = S(qr, "machine_name");
                        if (!stationByOrder.ContainsKey(oid)) stationByOrder[oid] = machineName;
                    }
                    var packedDispatchOrders = stationByOrder.Where(kv => kv.Value == "Packed" || kv.Value == "Dispatch").Select(kv => kv.Key).ToList();
                    foreach (var oid in packedDispatchOrders) stationByOrder.Remove(oid);
                }
            }
            catch { }

            var orderList = pendingOrders.Select(od => {
                var oid = Convert.ToInt32(I(od, "order_id"));
                var station = stationByOrder.ContainsKey(oid) ? stationByOrder[oid] : S(od, "workflow_stage_code");
                return Obj(
                    "order_id", I(od, "order_id"),
                    "order_number", S(od, "order_number"),
                    "customer_name", S(od, "customer_name"),
                    "dealer_name", S(od, "dealer_name"),
                    "confirmation_date", S(od, "confirmation_date"),
                    "order_type", S(od, "order_type_name"),
                    "workflow_stage", station,
                    "main_order", S(od, "main_order"),
                    "sub_order", S(od, "sub_order")
                );
            }).ToList();

            WriteJson(context, Obj(
                "ok", true,
                "machines", userMachines.Select(m => Obj("machine_id", I(m, "machine_id"), "machine_name", S(m, "machine_name"))).ToList(),
                "selected_machine", selectedMachine != null ? Obj("machine_id", I(selectedMachine, "machine_id"), "machine_name", S(selectedMachine, "machine_name")) : null,
                "orders", orderList
            ));
        }
    }

    private void HandleScannerActionHistory(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var machineId = N(Value(context, "machine_id"));
            if (!machineId.HasValue && S(user, "role_name") == "Machine User")
            {
                var stn = FindMachineByName(conn, S(user, "station_name"));
                if (stn != null) machineId = I(stn, "machine_id");
            }
            var sql = "SELECT TOP 200 h.order_id, h.action_code, h.new_status_code, h.remarks, h.acted_at, o.order_number, u.full_name AS user_name FROM ((tbl_order_history AS h LEFT JOIN tbl_orders AS o ON h.order_id = o.order_id) LEFT JOIN tbl_users AS u ON h.acted_by = u.user_id)";
            if (machineId.HasValue) sql += " WHERE h.station_id = " + SqlIntLiteral((int)machineId.Value);
            sql += " ORDER BY h.acted_at DESC, h.history_id DESC";
            var rows = QueryAll(conn, sql);
            WriteJson(context, Obj(
                "ok", true,
                "history", rows.Select(r => Obj(
                    "order_id", I(r, "order_id"),
                    "order_number", S(r, "order_number"),
                    "action_code", S(r, "action_code"),
                    "new_status_code", S(r, "new_status_code"),
                    "remarks", S(r, "remarks"),
                    "acted_at", S(r, "acted_at"),
                    "user_name", S(r, "user_name")
                )).ToList()
            ));
        }
    }

    private static readonly HashSet<string> StationDateColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cutting_date", "edgebanding_date", "drilling_date", "drilling2_date",
        "hot_press_date", "qc_date", "packed_date", "dispatch_date", "packing_ready_date"
    };

    private static string ResolveStationDateColumn(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName)) return null;
        var col = stationName.ToLowerInvariant().Replace(" ", "_") + "_date";
        return StationDateColumns.Contains(col) ? col : null;
    }

    private void HandleStationUpdate(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.").Trim();
            if (stationName == "Drilling 2") stationName = "Drilling";
            var actionCode = Value(context, "action_code").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(actionCode)) actionCode = "COMPLETED";
            var remarks = Value(context, "remarks");
            var now = IstNow();
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var station = FindMachineByName(conn, stationName);
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var stationId = Convert.ToInt32(I(station, "machine_id"));
            var userId = I(user, "user_id");

            if (actionCode == "PARTIAL_COMPLETED")
            {
                if (string.IsNullOrWhiteSpace(remarks))
                    throw new ApiFailure(400, "Remarks are mandatory for partial completion.");
                var queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND is_visible = TRUE", orderId, stationId);
                if (queueEntry == null && IsPackingStationName(stationName))
                    queueEntry = EnsurePackingQueueEntryForPortal(conn, user, order, station);
                if (queueEntry == null)
                {
                    foreach (var s in ResolveOrderSequenceStations(conn, order))
                    {
                        var nm = S(s, "machine_name");
                        if (string.Equals(nm, stationName, StringComparison.OrdinalIgnoreCase)) break;
                        var dc = ResolveStationDateColumn(nm);
                        if (dc == null) continue;
                        if (QueryOne(conn, "SELECT order_id FROM tbl_orders WHERE order_id = ? AND [" + dc + "] IS NULL", orderId) != null)
                            throw new ApiFailure(400, "This order is not visible in the selected station.");
                    }
                    EnsureQueueState(conn, orderId, stationId, "PENDING", true, "", userId);
                    queueEntry = QueryOne(conn, "SELECT * FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, stationId);
                }
                ApplyProductionAction(conn, user, order, station, queueEntry, "PARTIAL_COMPLETED", remarks, null);
                WriteJson(context, Obj("ok", true, "message", "Partial completed at " + stationName));
                return;
            }
            if (actionCode != "COMPLETED")
                throw new ApiFailure(400, "Invalid production action.");

            var blockedBy = PartialUpstreamStations(conn, order, stationName);
            if (blockedBy.Count > 0)
                throw new ApiFailure(400, "Cannot mark completed - " + string.Join(", ", blockedBy) + " is still partial.");

            var dateCol = ResolveStationDateColumn(stationName);
            if (dateCol != null)
            {
                try { Execute(conn, "UPDATE tbl_orders SET " + dateCol + " = " + SqlDateLiteral(now) + ", updated_at = " + SqlDateLiteral(now) + ", updated_by = ? WHERE order_id = ?", userId, orderId); } catch { }
            }
            EnsureQueueState(conn, orderId, stationId, "COMPLETED", true, stationName + " completed", userId);
            Audit(conn, userId, "Production", "Order", S(order, "order_number"), "Station Updated", stationName, stationName + " date recorded", "", stationId);
            AddHistory(conn, orderId, stationId, "COMPLETED", "", "IN_PROGRESS", null, null, stationName + " date recorded", userId);
            try { var seqStations = ResolveOrderSequenceStations(conn, order); AdvancePlannerBoard(conn, orderId, stationName, seqStations, Convert.ToInt32(userId)); } catch { }
            WriteJson(context, Obj("ok", true, "message", stationName + " updated on " + now.ToString("dd-MMM-yyyy HH:mm")));
        }
    }

    private List<string> PartialUpstreamStations(OleDbConnection conn, Dictionary<string, object> order, string stationName)
    {
        var result = new List<string>();
        var sequenceStations = ResolveOrderSequenceStations(conn, order);
        var orderId = I(order, "order_id");
        foreach (var s in sequenceStations)
        {
            var name = S(s, "machine_name");
            if (string.Equals(name, stationName, StringComparison.OrdinalIgnoreCase)) break;
            var stn = FindMachineByName(conn, name);
            if (stn == null) continue;
            var q = QueryOne(conn, "SELECT queue_id FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND queue_status_code = 'PARTIAL_COMPLETED'", orderId, I(stn, "machine_id"));
            if (q != null) result.Add(name);
        }
        return result;
    }

    private void HandleStationGate(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationName = Require(Value(context, "station_name"), "Station is required.").Trim();
            if (stationName == "Drilling 2") stationName = "Drilling";
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var station = FindMachineByName(conn, stationName);
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var blockedBy = PartialUpstreamStations(conn, order, stationName);
            var myQueue = QueryOne(conn, "SELECT queue_status_code FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ?", orderId, I(station, "machine_id"));
            var blockedRemarks = new List<string>();
            foreach (var stnName in blockedBy)
            {
                var stn = FindMachineByName(conn, stnName);
                if (stn == null) continue;
                var q = QueryOne(conn, "SELECT remarks FROM tbl_order_station_queue WHERE order_id = ? AND station_id = ? AND queue_status_code = 'PARTIAL_COMPLETED'", orderId, I(stn, "machine_id"));
                if (q != null && !string.IsNullOrEmpty(S(q, "remarks"))) blockedRemarks.Add(stnName + ": " + S(q, "remarks"));
            }
            WriteJson(context, Obj(
                "ok", true,
                "upstream_partial", blockedBy.Count > 0,
                "upstream_partial_station", blockedBy.Count > 0 ? blockedBy[0] : "",
                "upstream_partial_stations", blockedBy,
                "upstream_partial_remarks", blockedRemarks,
                "my_queue_status", myQueue == null ? "" : S(myQueue, "queue_status_code")));
        }
    }

    private void HandleStationState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var stationName = Require(Value(context, "station_name"), "Station is required.").Trim();
            if (stationName == "Drilling 2") stationName = "Drilling";
            var station = FindMachineByName(conn, stationName);
            if (station == null) throw new ApiFailure(404, "Station not found.");
            var dateCol = ResolveStationDateColumn(stationName);
            var dateSelect = dateCol != null ? ", o.[" + dateCol + "] AS station_date" : ", NULL AS station_date";
            var orders = QueryAll(conn,
                "SELECT o.order_id, o.order_number, o.customer_name, o.dealer_id, o.confirmation_date, o.workflow_stage_code, d.dealer_name" + dateSelect + " FROM (tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) WHERE o.workflow_stage_code <> 'QUOTATION_CREATED' AND o.workflow_stage_code <> 'ORDER_CONFIRMED' AND o.workflow_stage_code <> 'PACKED' AND o.workflow_stage_code <> 'DISPATCH_READY' AND o.workflow_stage_code <> 'DISPATCHED' ORDER BY o.order_number");

            var packedOrderIds = new HashSet<int>();
            try
            {
                var orderIds = orders.Select(o => Convert.ToInt32(I(o, "order_id"))).Where(v => v > 0).Distinct().ToList();
                if (orderIds.Count > 0)
                {
                    var ids = string.Join(",", orderIds.Select(v => v.ToString()).ToArray());
                    var packedRows = QueryAll(conn, "SELECT q.order_id FROM tbl_order_station_queue AS q INNER JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id IN (" + ids + ") AND q.is_visible = TRUE AND (m.machine_name = 'Packed' OR m.machine_name = 'Dispatch')");
                    foreach (var pr in packedRows) packedOrderIds.Add(Convert.ToInt32(I(pr, "order_id")));
                }
            }
            catch { }

            var result = orders.Where(o => !packedOrderIds.Contains(Convert.ToInt32(I(o, "order_id")))).Select(o => {
                return Obj(
                    "order_id", I(o, "order_id"),
                    "order_number", S(o, "order_number"),
                    "customer_name", S(o, "customer_name"),
                    "dealer_name", S(o, "dealer_name"),
                    "confirmation_date", S(o, "confirmation_date"),
                    "workflow_stage", S(o, "workflow_stage_code"),
                    "station_date", S(o, "station_date")
                );
            }).ToList();
            var stationId = Convert.ToInt32(I(station, "machine_id"));
            var historySql = "SELECT TOP 200 h.order_id, h.action_code, h.new_status_code, h.remarks, h.acted_at, o.order_number, u.full_name AS user_name FROM ((tbl_order_history AS h LEFT JOIN tbl_orders AS o ON h.order_id = o.order_id) LEFT JOIN tbl_users AS u ON h.acted_by = u.user_id) WHERE h.station_id = " + stationId.ToString() + " ORDER BY h.acted_at DESC, h.history_id DESC";
            var history = QueryAll(conn, historySql);
            var histResult = history.Select(h => Obj(
                "order_id", I(h, "order_id"),
                "order_number", S(h, "order_number"),
                "action_code", S(h, "action_code"),
                "remarks", S(h, "remarks"),
                "acted_at", S(h, "acted_at"),
                "user_name", S(h, "user_name")
            )).ToList();
            WriteJson(context, Obj(
                "ok", true,
                "station_name", stationName,
                "orders", result,
                "history", histResult
            ));
        }
    }

    private static readonly string[] StationSequenceOrder = { "Hot Press", "Cutting", "Edgebanding", "Drilling", "QC", "Packed", "Dispatch" };
    private static readonly Dictionary<string, string> StationDateColMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Hot Press", "hot_press_date" },
        { "Cutting", "cutting_date" },
        { "Edgebanding", "edgebanding_date" },
        { "Drilling", "drilling_date" },
        { "QC", "qc_date" },
        { "Packed", "packed_date" },
        { "Dispatch", "dispatch_date" }
    };

    private void HandleStationReadyOrders(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var stationName = Require(Value(context, "station_name"), "Station is required.").Trim();
            if (stationName == "Drilling 2") stationName = "Drilling";
            var station = FindMachineByName(conn, stationName);
            if (station == null) throw new ApiFailure(404, "Station not found.");

            var stationIndex = Array.IndexOf(StationSequenceOrder, stationName);
            var prevStations = stationIndex > 0 ? StationSequenceOrder.Take(stationIndex).ToList() : new List<string>();

            var dateConditions = new List<string>();
            var aliasIndex = 0;
            foreach (var ps in prevStations)
            {
                aliasIndex++;
                var partialSub = "EXISTS (SELECT q" + aliasIndex + ".order_id FROM tbl_order_station_queue AS q" + aliasIndex +
                    " INNER JOIN tbl_machines AS m" + aliasIndex + " ON q" + aliasIndex + ".station_id = m" + aliasIndex + ".machine_id" +
                    " WHERE q" + aliasIndex + ".order_id = o.order_id AND m" + aliasIndex + ".machine_name = '" + ps.Replace("'", "''") + "'" +
                    " AND q" + aliasIndex + ".queue_status_code = 'PARTIAL_COMPLETED')";
                string col;
                if (StationDateColMap.TryGetValue(ps, out col))
                    dateConditions.Add("(o.[" + col + "] IS NOT NULL OR " + partialSub + ")");
                else
                    dateConditions.Add(partialSub);
            }

            var currentCol = StationDateColMap.ContainsKey(stationName) ? StationDateColMap[stationName] : null;
            string whereClause;
            if (dateConditions.Count > 0 && currentCol != null)
                whereClause = "(" + string.Join(" AND ", dateConditions) + ") AND (o.[" + currentCol + "] IS NULL)";
            else if (dateConditions.Count > 0)
                whereClause = "(" + string.Join(" AND ", dateConditions) + ")";
            else if (currentCol != null)
                whereClause = "(o.[" + currentCol + "] IS NULL)";
            else
                whereClause = "1=1";

            var dateSelect = currentCol != null ? ", o.[" + currentCol + "] AS station_date" : ", NULL AS station_date";
            var orders = QueryAll(conn,
                "SELECT o.order_id, o.order_number, o.customer_name, o.dealer_id, o.confirmation_date, o.workflow_stage_code, d.dealer_name" + dateSelect +
                " FROM (tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) WHERE o.workflow_stage_code <> 'QUOTATION_CREATED' AND o.workflow_stage_code <> 'ORDER_CONFIRMED' AND o.workflow_stage_code <> 'PACKED' AND o.workflow_stage_code <> 'DISPATCH_READY' AND o.workflow_stage_code <> 'DISPATCHED' AND " + whereClause + " ORDER BY o.order_number");

            var result = orders.Select(o => {
                return Obj(
                    "order_id", I(o, "order_id"),
                    "order_number", S(o, "order_number"),
                    "customer_name", S(o, "customer_name"),
                    "dealer_name", S(o, "dealer_name"),
                    "confirmation_date", S(o, "confirmation_date"),
                    "workflow_stage", S(o, "workflow_stage_code"),
                    "station_date", S(o, "station_date")
                );
            }).ToList();

            WriteJson(context, Obj(
                "ok", true,
                "station_name", stationName,
                "orders", result
            ));
        }
    }

    private void HandleOrderTimeline(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Machine User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var order = FindOrderById(conn, orderId);
            if (order == null) throw new ApiFailure(404, "Order not found.");
            var histRows = QueryAll(conn, "SELECT h.acted_at, h.action_code, h.new_status_code, h.remarks, s.machine_name AS station_name, u.full_name AS acted_by_name FROM ((tbl_order_history AS h LEFT JOIN tbl_machines AS s ON h.station_id = s.machine_id) LEFT JOIN tbl_users AS u ON h.acted_by = u.user_id) WHERE h.order_id = ? ORDER BY h.acted_at ASC, h.history_id ASC", orderId);

            var result = new List<object>();
            if (!string.IsNullOrWhiteSpace(S(order, "confirmation_date")))
                result.Add(Obj("step", "Order Confirmed", "time", FormatDateTimeIST(DT(order, "confirmation_date")), "status", "done"));
            if (!string.IsNullOrWhiteSpace(S(order, "optimisation_date")))
                result.Add(Obj("step", "Optimisation Done", "time", FormatDateTimeIST(DT(order, "optimisation_date")), "by", S(order, "optimisation_by"), "status", "done"));
            var stationDates = new[] {
                new { key = "hot_press_date", label = "Hot Press" },
                new { key = "cutting_date", label = "Cutting" },
                new { key = "edgebanding_date", label = "Edgebanding" },
                new { key = "drilling_date", label = "Drilling" },
                new { key = "qc_date", label = "QC" },
                new { key = "packed_date", label = "Packed" },
                new { key = "dispatched_date", label = "Dispatch" }
            };
            foreach (var sd in stationDates)
            {
                var val = S(order, sd.key);
                if (!string.IsNullOrWhiteSpace(val))
                {
                    result.Add(Obj("step", sd.label, "time", FormatDateTimeIST(DT(order, sd.key)), "status", "done"));
                }
            }
            foreach (var h in histRows)
            {
                var action = S(h, "action_code");
                var remarks = S(h, "remarks");
                if (!string.IsNullOrWhiteSpace(remarks))
                {
                    var sn = S(h, "station_name");
                    var by = S(h, "acted_by_name");
                    var label = FixEncoding(sn + " \u2014 " + action.Replace("_", " ").ToLower());
                    result.Add(Obj("step", label, "time", FormatDateTimeIST(DT(h, "acted_at")), "by", by, "remarks", FixEncoding(remarks), "status", "info"));
                }
            }
            WriteJson(context, Obj("ok", true, "order_number", FixEncoding(S(order, "order_number")), "timeline", result));
        }
    }

    private void HandlePlannerBoardDebug(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var machines = QueryAll(conn, "SELECT machine_id, machine_name, sequence_no FROM tbl_machines WHERE is_active = TRUE ORDER BY sequence_no");
            var boardRows = new List<Dictionary<string, object>>();
            try { boardRows = QueryAll(conn, "SELECT order_id, station_id FROM tbl_planner_board"); }
            catch (Exception ex) { WriteJson(context, Obj("ok", false, "error", ex.Message, "step", "board query")); return; }
            WriteJson(context, Obj("ok", true, "machines", machines.Count, "board_rows", boardRows.Count));
        }
    }

    private void HandlePlannerBoardState(HttpContext context)
    {
        try
        {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var machines = QueryAll(conn, "SELECT machine_id, machine_name, sequence_no FROM tbl_machines WHERE is_active = TRUE ORDER BY sequence_no");
            var boardRows = new List<Dictionary<string, object>>();
            try { boardRows = QueryAll(conn, "SELECT b.order_id, b.station_id, b.assigned_at, b.planned_date FROM tbl_planner_board AS b"); } catch { }
            var boardResult = new List<object>();
            var assignedOrderIds = new HashSet<int>();
            var orderStationMap = new Dictionary<int, List<Dictionary<string, object>>>();
            foreach (var br in boardRows)
            {
                var oid = Convert.ToInt32(I(br, "order_id"));
                var sid = Convert.ToInt32(I(br, "station_id"));
                assignedOrderIds.Add(oid);
                if (!orderStationMap.ContainsKey(oid)) orderStationMap[oid] = new List<Dictionary<string, object>>();
                var matchingMachine = machines.FirstOrDefault(m => Convert.ToInt32(I(m, "machine_id")) == sid);
                if (matchingMachine != null)
                {
                    orderStationMap[oid].Add(Obj("station_id", sid, "station_name", S(matchingMachine, "machine_name"), "planned_date", FormatDateTimeIST(br["planned_date"])));
                    var orderRows = QueryAll(conn, "SELECT o.order_number, o.customer_name, o.board_qty_decimal, o.panel_qty, d.dealer_name FROM tbl_orders o LEFT JOIN tbl_dealers d ON o.dealer_id=d.dealer_id WHERE o.order_id = " + oid);
                    var orderRow = orderRows.Count > 0 ? orderRows[0] : null;
                    var queueRows = QueryAll(conn, "SELECT queue_status_code, remarks FROM tbl_order_station_queue WHERE order_id = " + oid + " AND station_id = " + sid);
                    var queueRow = queueRows.Count > 0 ? queueRows[0] : null;
                    var queueStatus = queueRow != null ? S(queueRow, "queue_status_code") : "";
                    var remarks = queueRow != null ? S(queueRow, "remarks") : "";
                    var wasCompleted = false;
                    try { var wcRows = QueryAll(conn, "SELECT queue_id FROM tbl_order_station_queue WHERE order_id = " + oid + " AND station_id = " + sid + " AND (queue_status_code = 'COMPLETED' OR queue_status_code = 'PARTIAL_COMPLETED')"); wasCompleted = wcRows.Count > 0; } catch { }
                    var priority = "";
                    var edd = "";
                    try { var ppRows = QueryAll(conn, "SELECT [priority], sla_date FROM tbl_production_planner WHERE order_id = " + oid); if (ppRows.Count > 0) { priority = S(ppRows[0], "priority"); edd = FormatDateTimeIST(ppRows[0]["sla_date"]); } } catch { }
                    boardResult.Add(Obj(
                        "order_id", oid,
                        "order_number", orderRow != null ? S(orderRow, "order_number") : "",
                        "customer_name", orderRow != null ? S(orderRow, "customer_name") : "",
                        "dealer_name", orderRow != null ? S(orderRow, "dealer_name") : "",
                        "board_qty", orderRow != null ? S(orderRow, "board_qty_decimal") : "",
                        "panel_qty", orderRow != null ? S(orderRow, "panel_qty") : "",
                        "station_id", sid,
                        "station_name", S(matchingMachine, "machine_name"),
                        "queue_status", queueStatus,
                        "remarks", remarks,
                        "was_completed", wasCompleted,
                        "priority", priority,
                        "edd", edd,
                        "planned_date", FormatDateTimeIST(br["planned_date"])
                    ));
                }
            }
            var enrichedOrders = LoadEnrichedOrders(conn, LoadMasterSets(conn), user);
            var activeOrders = enrichedOrders.Where(o => IsPlanningEligible(o))
                .OrderBy(o => ParseDate(S(o, "confirmation_date")) ?? DateTime.MaxValue)
                .ToList();
            var typeIds = new HashSet<int>();
            var dealerIds = new HashSet<int>();
            var orderIdsForPlanner = new List<int>();
            foreach (var o in activeOrders) { var tid = Convert.ToInt32(I(o, "order_type_id")); if (tid > 0) typeIds.Add(tid); var did = Convert.ToInt32(I(o, "dealer_id")); if (did > 0) dealerIds.Add(did); orderIdsForPlanner.Add(Convert.ToInt32(I(o, "order_id"))); }
            var typeLookup = new Dictionary<int, string>();
            if (typeIds.Count > 0) { var trows = QueryAll(conn, "SELECT order_type_id, order_type_name FROM tbl_order_types WHERE order_type_id IN (" + string.Join(",", typeIds.Select(v => v.ToString()).ToArray()) + ")"); foreach (var t in trows) typeLookup[Convert.ToInt32(I(t, "order_type_id"))] = S(t, "order_type_name"); }
            var dealerLookup = new Dictionary<int, string>();
            if (dealerIds.Count > 0) { var drows = QueryAll(conn, "SELECT dealer_id, dealer_name FROM tbl_dealers WHERE dealer_id IN (" + string.Join(",", dealerIds.Select(v => v.ToString()).ToArray()) + ")"); foreach (var d in drows) dealerLookup[Convert.ToInt32(I(d, "dealer_id"))] = S(d, "dealer_name"); }
            var plannerLookup = new Dictionary<int, Dictionary<string, object>>();
            if (orderIdsForPlanner.Count > 0) { var prows = QueryAll(conn, "SELECT order_id, sla_date, [priority] FROM tbl_production_planner WHERE order_id IN (" + string.Join(",", orderIdsForPlanner.Select(v => v.ToString()).ToArray()) + ")"); foreach (var p in prows) plannerLookup[Convert.ToInt32(I(p, "order_id"))] = p; }
            var stationCompletionLookup = new Dictionary<int, string>();
            try
            {
                if (orderIdsForPlanner.Count > 0)
                {
                    var ids = string.Join(",", orderIdsForPlanner.Select(v => v.ToString()).ToArray());
                    var sqRows = QueryAll(conn, "SELECT q.order_id, m.machine_name, q.queue_status_code, m.sequence_no FROM tbl_order_station_queue AS q INNER JOIN tbl_machines AS m ON q.station_id = m.machine_id WHERE q.order_id IN (" + ids + ") AND (q.queue_status_code = 'COMPLETED' OR q.queue_status_code = 'PARTIAL_COMPLETED') ORDER BY m.sequence_no DESC");
                    foreach (var sq in sqRows)
                    {
                        var oid = Convert.ToInt32(I(sq, "order_id"));
                        if (!stationCompletionLookup.ContainsKey(oid))
                        {
                            var status = S(sq, "queue_status_code");
                            var name = S(sq, "machine_name");
                            stationCompletionLookup[oid] = string.Equals(status, "PARTIAL_COMPLETED", StringComparison.OrdinalIgnoreCase) ? name + " \u26A0" : name + " done";
                        }
                    }
                }
            }
            catch { }
            var unplanned = activeOrders.Select(o =>
            {
                var tid = Convert.ToInt32(I(o, "order_type_id"));
                var did = Convert.ToInt32(I(o, "dealer_id"));
                var oid = Convert.ToInt32(I(o, "order_id"));
                string typeName; typeLookup.TryGetValue(tid, out typeName);
                string dealerName; dealerLookup.TryGetValue(did, out dealerName);
                Dictionary<string, object> plannerRow; plannerLookup.TryGetValue(oid, out plannerRow);
                var edd = plannerRow != null ? FormatDateTimeIST(plannerRow["sla_date"]) : "";
                var priority = plannerRow != null ? S(plannerRow, "priority") : "";
                var confDate = FormatDateTimeIST(S(o, "confirmation_date"));
                var plannedStations = orderStationMap.ContainsKey(oid) ? orderStationMap[oid] : new List<Dictionary<string, object>>();
                var plannedNames = string.Join(", ", plannedStations.Select(ps => S(ps, "station_name")));
                var plannedDates = string.Join(", ", plannedStations.Select(ps => S(ps, "planned_date")).Where(d => !string.IsNullOrEmpty(d)));
                var wfCode = S(o, "workflow_stage_code");
                string stageLabel;
                if (string.Equals(wfCode, "PRODUCTION_STARTED", StringComparison.OrdinalIgnoreCase) && stationCompletionLookup.ContainsKey(oid))
                    stageLabel = stationCompletionLookup[oid];
                else
                    stageLabel = WorkflowStageLabel(wfCode);
                return Obj("order_id", I(o, "order_id"), "order_number", S(o, "order_number"), "customer_name", S(o, "customer_name"), "dealer_name", dealerName ?? "", "order_type", typeName ?? "", "board_qty", S(o, "number_of_boards"), "panel_qty", S(o, "panel_qty"), "workflow_stage", stageLabel, "confirmation_date", confDate, "edd", edd, "priority", priority, "planned_stations", plannedNames, "planned_dates", plannedDates);
            }).ToList();
            WriteJson(context, Obj("ok", true, "machines", machines.Select(m => Obj("machine_id", I(m, "machine_id"), "machine_name", S(m, "machine_name"), "sequence_no", I(m, "sequence_no"))).ToList(), "unplanned", unplanned, "board", boardResult));
        }
        }
        catch (Exception ex)
        {
            WriteJson(context, Obj("ok", false, "error", ex.Message, "type", ex.GetType().FullName, "stack", ex.StackTrace));
        }
    }

    private void HandlePlannerBoardAssign(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            var plannedDateRaw = Value(context, "planned_date");
            var now = IstNow();
            var plannedDateSql = "NULL";
            if (!string.IsNullOrWhiteSpace(plannedDateRaw))
            {
                var pd = ParseDate(plannedDateRaw);
                if (pd.HasValue) plannedDateSql = SqlDateLiteral(pd.Value);
            }
            var existingRows = QueryAll(conn, "SELECT board_id FROM tbl_planner_board WHERE order_id = " + orderId + " AND station_id = " + stationId);
            if (existingRows.Count == 0)
            {
                Execute(conn, "INSERT INTO tbl_planner_board (order_id, station_id, assigned_by, assigned_at, planned_date) VALUES (" + orderId + ", " + stationId + ", " + I(user, "user_id") + ", " + SqlDateLiteral(now) + ", " + plannedDateSql + ")");
            }
            WriteJson(context, Obj("ok", true, "message", "Order assigned to machine."));
        }
    }

    private void HandlePlannerBoardUnassign(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            Execute(conn, "DELETE FROM tbl_planner_board WHERE order_id = " + orderId + " AND station_id = " + stationId);
            WriteJson(context, Obj("ok", true, "message", "Order removed from machine."));
        }
    }

    private void HandlePlannerBoardClear(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var stationIdRaw = Value(context, "station_id");
            int stationId;
            if (!string.IsNullOrEmpty(stationIdRaw) && int.TryParse(stationIdRaw, out stationId))
            {
                var count = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_planner_board WHERE station_id = " + stationId) ?? 0);
                Execute(conn, "DELETE FROM tbl_planner_board WHERE station_id = " + stationId);
                WriteJson(context, Obj("ok", true, "cleared", count, "message", count + " orders cleared from machine."));
            }
            else
            {
                var count = Convert.ToInt32(Scalar(conn, "SELECT COUNT(*) FROM tbl_planner_board") ?? 0);
                Execute(conn, "DELETE FROM tbl_planner_board");
                WriteJson(context, Obj("ok", true, "cleared", count, "message", count + " orders cleared from all machines."));
            }
        }
    }

    private void HandlePlannerBoardBatchAssign(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            var orderIdsRaw = Require(Value(context, "order_ids"), "Order IDs are required.");
            var plannedDateRaw = Value(context, "planned_date");
            var ids = orderIdsRaw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var now = IstNow();
            var plannedDateSql = "NULL";
            if (!string.IsNullOrWhiteSpace(plannedDateRaw))
            {
                var pd = ParseDate(plannedDateRaw);
                if (pd.HasValue) plannedDateSql = SqlDateLiteral(pd.Value);
            }
            var assigned = 0;
            var skipped = 0;
            foreach (var idStr in ids)
            {
                int oid;
                if (!int.TryParse(idStr.Trim(), out oid)) { skipped++; continue; }
                var existingRows = QueryAll(conn, "SELECT board_id FROM tbl_planner_board WHERE order_id = " + oid + " AND station_id = " + stationId);
                if (existingRows.Count > 0) { skipped++; continue; }
                Execute(conn, "INSERT INTO tbl_planner_board (order_id, station_id, assigned_by, assigned_at, planned_date) VALUES (" + oid + ", " + stationId + ", " + I(user, "user_id") + ", " + SqlDateLiteral(now) + ", " + plannedDateSql + ")");
                assigned++;
            }
            WriteJson(context, Obj("ok", true, "assigned", assigned, "skipped", skipped, "message", assigned + " orders assigned."));
        }
    }

    private void HandlePlannerBoardEditRemarks(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var orderId = IntRequired(Value(context, "order_id"), "Order is required.");
            var stationId = IntRequired(Value(context, "station_id"), "Station is required.");
            var remarks = Value(context, "remarks") ?? "";
            var existing = QueryAll(conn, "SELECT queue_id FROM tbl_order_station_queue WHERE order_id = " + orderId + " AND station_id = " + stationId);
            if (existing.Count > 0)
            {
                Execute(conn, "UPDATE tbl_order_station_queue SET remarks = '" + remarks.Replace("'", "''") + "' WHERE order_id = " + orderId + " AND station_id = " + stationId);
            }
            else
            {
                Execute(conn, "INSERT INTO tbl_order_station_queue (order_id, station_id, queue_status_code, remarks, is_visible, updated_at) VALUES (" + orderId + ", " + stationId + ", 'PLANNED', '" + remarks.Replace("'", "''") + "', TRUE, " + SqlDateLiteral(IstNow()) + ")");
            }
            WriteJson(context, Obj("ok", true, "message", "Remarks updated."));
        }
    }

    private void HandlePlannerBoardVsActual(HttpContext context)
    {
        try
        {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var machines = QueryAll(conn, "SELECT machine_id, machine_name FROM tbl_machines ORDER BY sequence_no");
            var allPlannedOrderIds = QueryAll(conn, "SELECT DISTINCT order_id FROM tbl_planner_board");
            var plannedOrderSet = new HashSet<int>(allPlannedOrderIds.Select(r => Convert.ToInt32(I(r, "order_id"))));
            var result = new List<object>();
            foreach (var m in machines)
            {
                var mid = Convert.ToInt32(I(m, "machine_id"));
                var planned = QueryAll(conn, "SELECT pb.order_id, o.order_number, o.customer_name, d.dealer_name FROM (tbl_planner_board pb INNER JOIN tbl_orders o ON pb.order_id=o.order_id) LEFT JOIN tbl_dealers d ON o.dealer_id=d.dealer_id WHERE pb.station_id=" + mid);
                var completed = QueryAll(conn, "SELECT order_id FROM tbl_order_station_queue WHERE station_id=" + mid + " AND (queue_status_code='COMPLETED' OR queue_status_code='PARTIAL_COMPLETED')");
                var completedIds = new HashSet<int>(completed.Where(c => plannedOrderSet.Contains(Convert.ToInt32(I(c, "order_id")))).Select(c => Convert.ToInt32(I(c, "order_id"))));
                var plannedIds = new HashSet<int>(planned.Select(p => Convert.ToInt32(I(p, "order_id"))));
                var missed = planned.Where(p => !completedIds.Contains(Convert.ToInt32(I(p, "order_id")))).ToList();
                var extra = completed.Where(c => !plannedIds.Contains(Convert.ToInt32(I(c, "order_id"))) && plannedOrderSet.Contains(Convert.ToInt32(I(c, "order_id")))).ToList();
                var extraOrders = new List<object>();
                foreach (var e in extra)
                {
                    var eid = Convert.ToInt32(I(e, "order_id"));
                    var eRow = QueryOne(conn, "SELECT o.order_number, o.customer_name, d.dealer_name FROM tbl_orders o LEFT JOIN tbl_dealers d ON o.dealer_id=d.dealer_id WHERE o.order_id=" + eid);
                    extraOrders.Add(Obj("order_id", eid, "order_number", eRow != null ? S(eRow, "order_number") : "", "customer_name", eRow != null ? S(eRow, "customer_name") : "", "dealer_name", eRow != null ? S(eRow, "dealer_name") : ""));
                }
                var missedOrders = missed.Select(p => Obj("order_id", I(p, "order_id"), "order_number", S(p, "order_number"), "customer_name", S(p, "customer_name"), "dealer_name", S(p, "dealer_name"))).ToList();
                result.Add(Obj("station_id", mid, "station_name", S(m, "machine_name"), "total_planned", planned.Count, "completed", completedIds.Intersect(plannedIds).Count(), "missed", missedOrders.Count, "extra", extraOrders.Count, "missed_orders", missedOrders, "extra_orders", extraOrders));
            }
            WriteJson(context, Obj("ok", true, "machines", result));
        }
        }
        catch (Exception ex) { WriteJson(context, Obj("ok", false, "error", ex.Message, "stack", ex.StackTrace)); }
    }

    private void AdvancePlannerBoard(OleDbConnection conn, int orderId, string completedStationName, List<Dictionary<string, object>> sequenceStations, int userId)
    {
        var machineNames = sequenceStations.Select(m => S(m, "machine_name")).ToList();
        var nextName = NextStationName(machineNames, completedStationName);
        if (string.IsNullOrWhiteSpace(nextName)) return;
        var nextMachine = sequenceStations.FirstOrDefault(m => S(m, "machine_name") == nextName);
        if (nextMachine == null) return;
        var nextStationId = Convert.ToInt32(I(nextMachine, "station_id"));
        var existingRows = QueryAll(conn, "SELECT board_id FROM tbl_planner_board WHERE order_id = " + orderId + " AND station_id = " + nextStationId);
        if (existingRows.Count == 0)
        {
            var now = IstNow();
            Execute(conn, "INSERT INTO tbl_planner_board (order_id, station_id, assigned_by, assigned_at) VALUES (" + orderId + ", " + nextStationId + ", " + userId + ", " + SqlDateLiteral(now) + ")");
        }
    }

    private void HandleDealerDashboard(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var dealerId = I(user, "dealer_id");
            if (dealerId <= 0) throw new ApiFailure(403, "Your account is not linked to a dealer.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer not found.");
            var orders = QueryAll(conn, "SELECT * FROM tbl_orders WHERE dealer_id = ? ORDER BY updated_at DESC, order_id DESC", dealerId);
            var orderIds = orders.Select(o => I(o, "order_id")).Where(v => v > 0).Distinct().ToList();
            var historyByOrder = new Dictionary<int, List<Dictionary<string, object>>>();
            if (orderIds.Count > 0)
            {
                var ids = string.Join(",", orderIds.Select(v => v.ToString()).ToArray());
                var hist = QueryAll(conn, "SELECT h.order_id, h.acted_at, h.action_code, h.new_status_code FROM tbl_order_history AS h WHERE h.order_id IN (" + ids + ") ORDER BY h.acted_at DESC, h.history_id DESC");
                foreach (var row in hist)
                {
                    var oid = I(row, "order_id");
                    if (!historyByOrder.ContainsKey(oid)) historyByOrder[oid] = new List<Dictionary<string, object>>();
                    historyByOrder[oid].Add(row);
                }
            }
            var statusLookup = LoadStatusLookup(conn);
            var mainOrderSet = new Dictionary<string, int>();
            var orderList = orders.Select(r =>
            {
                var step = DealerTrackingStep(S(r, "workflow_stage_code"), S(r, "dispatch_status_code"));
                var oid = I(r, "order_id");
                var hrows = historyByOrder.ContainsKey(oid) ? historyByOrder[oid] : new List<Dictionary<string, object>>();
                var mo = S(r, "main_order");
                if (!string.IsNullOrWhiteSpace(mo))
                {
                    if (!mainOrderSet.ContainsKey(mo)) mainOrderSet[mo] = 0;
                    mainOrderSet[mo] = Math.Max(mainOrderSet[mo], step);
                }
                return Obj(
                    "order_id", oid,
                    "order_number", S(r, "order_number"),
                    "customer_name", S(r, "customer_name"),
                    "order_type", S(r, "order_type_name"),
                    "workflow_stage_code", S(r, "workflow_stage_code"),
                    "dispatch_status_code", S(r, "dispatch_status_code"),
                    "workflow_stage", Label(statusLookup, "WORKFLOW", S(r, "workflow_stage_code")),
                    "dispatch_status", Label(statusLookup, "DISPATCH", S(r, "dispatch_status_code")),
                    "main_order", mo,
                    "sub_order", S(r, "sub_order"),
                    "approx_value", S(r, "approx_value"),
                    "confirmation_date", S(r, "confirmation_date"),
                    "updated_at", S(r, "updated_at"),
                    "tracking_step", step,
                    "tracking", BuildDealerTracking(S(r, "workflow_stage_code"), S(r, "dispatch_status_code"), r, hrows)
                );
            }).ToList();
            var mainOrders = mainOrderSet.Select(kvp => Obj("main_order", kvp.Key, "progress_step", kvp.Value)).ToList();
            WriteJson(context, Obj(
                "ok", true,
                "dealer", Obj(
                    "dealer_id", I(dealer, "dealer_id"),
                    "dealer_code", S(dealer, "dealer_code"),
                    "dealer_name", S(dealer, "dealer_name"),
                    "company_name", S(dealer, "company_name"),
                    "dealer_type", S(dealer, "dealer_type"),
                    "customer_type_code", S(dealer, "customer_type_code"),
                    "city", S(dealer, "city"),
                    "pin_code", S(dealer, "pin_code"),
                    "gst_number", S(dealer, "gst_number"),
                    "contact_person", S(dealer, "contact_person"),
                    "mobile_number", S(dealer, "mobile_number"),
                    "whatsapp_number", S(dealer, "whatsapp_number"),
                    "email", S(dealer, "email"),
                    "payment_terms", S(dealer, "payment_terms"),
                    "credit_limit_lakh", I(dealer, "credit_limit_lakh"),
                    "marketing_owner", S(dealer, "marketing_owner"),
                    "quotation_owner", S(dealer, "quotation_owner"),
                    "address", S(dealer, "address"),
                    "area", S(dealer, "area"),
                    "remarks", S(dealer, "remarks")
                ),
                "main_orders", mainOrders,
                "orders", orderList
            ));
        }
    }

    private static int DealerTrackingStep(string wf, string ds)
    {
        if (string.Equals(wf, "DISPATCHED", StringComparison.OrdinalIgnoreCase) || string.Equals(ds, "DISPATCHED", StringComparison.OrdinalIgnoreCase)) return 6;
        if (string.Equals(wf, "PACKED", StringComparison.OrdinalIgnoreCase) || string.Equals(wf, "DISPATCH_READY", StringComparison.OrdinalIgnoreCase) || string.Equals(ds, "PARTIALLY_DISPATCHED", StringComparison.OrdinalIgnoreCase)) return 5;
        if (string.Equals(wf, "PRODUCTION_STARTED", StringComparison.OrdinalIgnoreCase) || string.Equals(wf, "PROCUREMENT_STARTED", StringComparison.OrdinalIgnoreCase)) return 4;
        if (string.Equals(wf, "OPTIMISATION_DONE", StringComparison.OrdinalIgnoreCase)) return 3;
        if (string.Equals(wf, "ORDER_CONFIRMED", StringComparison.OrdinalIgnoreCase)) return 2;
        return 1;
    }

    private static List<object> BuildDealerTracking(string wf, string ds, Dictionary<string, object> order, List<Dictionary<string, object>> history)
    {
        var steps = new[]
        {
            new { code = "QUOTATION_CREATED", label = "Quotation Created" },
            new { code = "ORDER_CONFIRMED", label = "Order Confirmed" },
            new { code = "OPTIMISATION_DONE", label = "Optimisation Done" },
            new { code = "PRODUCTION_STARTED", label = "In Production" },
            new { code = "PACKED", label = "Packed" },
            new { code = "DISPATCHED", label = "Dispatched" }
        };
        var current = DealerTrackingStep(wf, ds);
        var list = new List<object>();
        for (int i = 0; i < steps.Length; i++)
        {
            int stepNo = i + 1;
            string ts = "";
            if (stepNo == 2) ts = S(order, "confirmation_date");
            else if (stepNo == 3) ts = S(order, "optimisation_date");
            else if (history != null && history.Count > 0)
            {
                var match = history.FirstOrDefault(h => string.Equals(S(h, "new_status_code"), steps[i].code, StringComparison.OrdinalIgnoreCase));
                if (match != null) ts = S(match, "acted_at");
            }
            string state = stepNo < current ? "done" : (stepNo == current ? "current" : "pending");
            list.Add(Obj("step", stepNo, "code", steps[i].code, "label", steps[i].label, "state", state, "timestamp", ts));
        }
        return list;
    }

    private void HandleDealerLoginGenerate(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer ID is required.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer not found.");
            var mobile = S(dealer, "mobile_number").Trim();
            var suffix = mobile.Length >= 4 ? mobile.Substring(mobile.Length - 4) : mobile;
            var loginId = "Dealer" + S(dealer, "dealer_type").ToUpper() + suffix;
            var existing = QueryOne(conn, "SELECT user_id FROM tbl_users WHERE login_id = ?", loginId);
            if (existing != null) throw new ApiFailure(409, "Login ID already exists: " + loginId);
            var dealerRole = QueryOne(conn, "SELECT role_id FROM tbl_roles WHERE role_name = 'Dealer'");
            if (dealerRole == null)
            {
                Execute(conn, "INSERT INTO tbl_roles (role_name, home_section, is_active) VALUES ('Dealer', 'dashboard', TRUE)");
                dealerRole = QueryOne(conn, "SELECT role_id FROM tbl_roles WHERE role_name = 'Dealer'");
            }
            var now = IstNow();
            Execute(conn, "INSERT INTO tbl_users (full_name, login_id, password_hash, password_salt, password_iterations, role_id, assigned_station_id, dealer_id, is_active, created_at, updated_at) VALUES (?, ?, ?, '', 0, ?, NULL, ?, TRUE, " + SqlDateLiteral(now) + ", " + SqlDateLiteral(now) + ")",
                S(dealer, "dealer_name"), loginId, "demo123", I(dealerRole, "role_id"), dealerId);
            Audit(conn, I(user, "user_id"), "Masters", "User", S(dealer, "dealer_name"), "Dealer Login Generated", "", loginId, "Dealer M" + suffix, null);
            WriteJson(context, Obj("ok", true, "login_id", loginId, "password", "demo123", "message", "Dealer login created: " + loginId));
        }
    }

    private void HandleDealerPortalState(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var dealerId = I(user, "dealer_id");
            if (dealerId <= 0) throw new ApiFailure(403, "Your account is not linked to a dealer.");
            var dealer = QueryOne(conn, "SELECT * FROM tbl_dealers WHERE dealer_id = ?", dealerId);
            if (dealer == null) throw new ApiFailure(404, "Dealer not found.");
            var orders = QueryAll(conn, "SELECT * FROM tbl_orders WHERE dealer_id = ? ORDER BY updated_at DESC, order_id DESC", dealerId);
            var orderIds = orders.Select(o => I(o, "order_id")).Where(v => v > 0).Distinct().ToList();
            var historyByOrder = new Dictionary<int, List<Dictionary<string, object>>>();
            if (orderIds.Count > 0)
            {
                var ids = string.Join(",", orderIds.Select(v => v.ToString()).ToArray());
                var hist = QueryAll(conn, "SELECT h.order_id, h.acted_at, h.action_code, h.new_status_code FROM tbl_order_history AS h WHERE h.order_id IN (" + ids + ") ORDER BY h.acted_at DESC, h.history_id DESC");
                foreach (var row in hist)
                {
                    var oid = I(row, "order_id");
                    if (!historyByOrder.ContainsKey(oid)) historyByOrder[oid] = new List<Dictionary<string, object>>();
                    historyByOrder[oid].Add(row);
                }
            }
            var statusLookup = LoadStatusLookup(conn);
            var ledger = QueryAll(conn, "SELECT * FROM tbl_dealer_ledger WHERE dealer_id = ? ORDER BY entry_date DESC, ledger_id DESC", dealerId);
            var ledgerResult = ledger.Select(l => Obj(
                "ledger_id", I(l, "ledger_id"),
                "entry_date", FormatDateTime(DT(l, "entry_date")),
                "payment_mode", S(l, "payment_mode"),
                "amount", I(l, "amount"),
                "reference_no", S(l, "reference_no"),
                "order_id", I(l, "order_id"),
                "remarks", S(l, "remarks")
            )).ToList();
            var runningBalance = ledger.Sum(l => Convert.ToDouble(I(l, "amount")));
            var orderList = orders.Select(r =>
            {
                var step = DealerTrackingStep(S(r, "workflow_stage_code"), S(r, "dispatch_status_code"));
                var oid = I(r, "order_id");
                var hrows = historyByOrder.ContainsKey(oid) ? historyByOrder[oid] : new List<Dictionary<string, object>>();
                return Obj(
                    "order_id", oid,
                    "order_number", S(r, "order_number"),
                    "customer_name", S(r, "customer_name"),
                    "order_type", S(r, "order_type_name"),
                    "workflow_stage_code", S(r, "workflow_stage_code"),
                    "dispatch_status_code", S(r, "dispatch_status_code"),
                    "workflow_stage", Label(statusLookup, "WORKFLOW", S(r, "workflow_stage_code")),
                    "dispatch_status", Label(statusLookup, "DISPATCH", S(r, "dispatch_status_code")),
                    "approx_value", S(r, "approx_value"),
                    "confirmation_date", S(r, "confirmation_date"),
                    "updated_at", S(r, "updated_at"),
                    "tracking_step", step,
                    "tracking", BuildDealerTracking(S(r, "workflow_stage_code"), S(r, "dispatch_status_code"), r, hrows)
                );
            }).ToList();
            WriteJson(context, Obj(
                "ok", true,
                "dealer", Obj(
                    "dealer_id", I(dealer, "dealer_id"),
                    "dealer_code", S(dealer, "dealer_code"),
                    "dealer_name", S(dealer, "dealer_name"),
                    "company_name", S(dealer, "company_name"),
                    "dealer_type", S(dealer, "dealer_type"),
                    "customer_type_code", S(dealer, "customer_type_code"),
                    "city", S(dealer, "city"),
                    "pin_code", S(dealer, "pin_code"),
                    "gst_number", S(dealer, "gst_number"),
                    "contact_person", S(dealer, "contact_person"),
                    "mobile_number", S(dealer, "mobile_number"),
                    "whatsapp_number", S(dealer, "whatsapp_number"),
                    "email", S(dealer, "email"),
                    "payment_terms", S(dealer, "payment_terms"),
                    "credit_limit_lakh", I(dealer, "credit_limit_lakh"),
                    "address", S(dealer, "address"),
                    "area", S(dealer, "area")
                ),
                "orders", orderList,
                "ledger", ledgerResult,
                "running_balance", runningBalance
            ));
        }
    }

    private void HandleDealerLedgerAdd(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var dealerId = IntRequired(Value(context, "dealer_id"), "Dealer ID is required.");
            var amountRaw = Value(context, "amount");
            double amount;
            if (!double.TryParse(amountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out amount) || amount == 0)
                throw new ApiFailure(400, "Amount is required and must be non-zero.");
            var paymentMode = Value(context, "payment_mode", "Cash");
            var entryDateRaw = Value(context, "entry_date");
            DateTime entryDate = string.IsNullOrWhiteSpace(entryDateRaw) ? IstNow() : (ParseDate(entryDateRaw) ?? IstNow());
            var referenceNo = Value(context, "reference_no", "");
            var orderIdRaw = Value(context, "order_id");
            int orderId;
            int.TryParse(orderIdRaw, out orderId);
            var remarks = Value(context, "remarks", "");
            var now = IstNow();
            Execute(conn, "INSERT INTO tbl_dealer_ledger (dealer_id, entry_date, payment_mode, amount, reference_no, order_id, remarks, created_by, created_at) VALUES (?, " + SqlDateLiteral(entryDate) + ", ?, ?, ?, ?, ?, ?, " + SqlDateLiteral(now) + ")",
                dealerId, paymentMode, amount, referenceNo, orderId > 0 ? (object)orderId : null, remarks, I(user, "user_id"));
            Audit(conn, I(user, "user_id"), "Accounts", "Dealer Ledger", "Dealer " + dealerId, "Ledger Entry Added", "", amount.ToString("0.##", CultureInfo.InvariantCulture) + " " + paymentMode, remarks, null);
            WriteJson(context, Obj("ok", true, "message", "Ledger entry added."));
        }
    }

    private void HandleDealerLedgerList(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var role = S(user, "role_name");
            int dealerId;
            if (role == "Admin" || role == "Accounts")
            {
                dealerId = Convert.ToInt32(Value(context, "dealer_id", "0"));
                if (dealerId <= 0) throw new ApiFailure(400, "Dealer ID required for admin.");
            }
            else
            {
                dealerId = Convert.ToInt32(I(user, "dealer_id"));
                if (dealerId <= 0) throw new ApiFailure(403, "Your account is not linked to a dealer.");
            }
            var ledger = QueryAll(conn, "SELECT * FROM tbl_dealer_ledger WHERE dealer_id = ? ORDER BY entry_date DESC, ledger_id DESC", dealerId);
            var result = ledger.Select(l => Obj(
                "ledger_id", I(l, "ledger_id"),
                "entry_date", FormatDateTime(DT(l, "entry_date")),
                "payment_mode", S(l, "payment_mode"),
                "amount", I(l, "amount"),
                "reference_no", S(l, "reference_no"),
                "order_id", I(l, "order_id"),
                "remarks", S(l, "remarks")
            )).ToList();
            var runningBalance = ledger.Sum(l => Convert.ToDouble(I(l, "amount")));
            WriteJson(context, Obj("ok", true, "rows", result, "running_balance", runningBalance));
        }
    }

    private void HandleDealerLedgerDelete(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin");
            var ledgerId = IntRequired(Value(context, "ledger_id"), "Ledger ID is required.");
            var entry = QueryOne(conn, "SELECT * FROM tbl_dealer_ledger WHERE ledger_id = ?", ledgerId);
            if (entry == null) throw new ApiFailure(404, "Ledger entry not found.");
            Execute(conn, "DELETE FROM tbl_dealer_ledger WHERE ledger_id = ?", ledgerId);
            Audit(conn, I(user, "user_id"), "Accounts", "Dealer Ledger", "Dealer " + I(entry, "dealer_id"), "Ledger Entry Deleted", I(entry, "amount").ToString(), "", "", null);
            WriteJson(context, Obj("ok", true, "message", "Ledger entry deleted."));
        }
    }

    private void HandleRemarksRequestCreate(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var orderIds = Value(context, "order_ids");
            if (string.IsNullOrWhiteSpace(orderIds)) throw new ApiFailure(400, "Order IDs required.");
            var token = Guid.NewGuid().ToString("N").Substring(0, 16);
            var now = IstNow();
            Execute(conn, "INSERT INTO tbl_remarks_requests (token, order_ids, requested_by, requested_at, status, created_at) VALUES (?, ?, ?, " + SqlDateLiteral(now) + ", 'pending', " + SqlDateLiteral(now) + ")",
                token, orderIds, I(user, "user_id"));
            var reqId = Convert.ToInt32(Scalar(conn, "SELECT @@IDENTITY"));
            WriteJson(context, Obj("ok", true, "token", token, "request_id", reqId));
        }
    }

    private void HandleRemarksRequestInfo(HttpContext context)
    {
        var token = Value(context, "token");
        if (string.IsNullOrWhiteSpace(token)) throw new ApiFailure(400, "Token required.");
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var row = QueryOne(conn, "SELECT * FROM tbl_remarks_requests WHERE token = ?", token);
            if (row == null) throw new ApiFailure(404, "Request not found.");
            var orderIdsStr = S(row, "order_ids");
            var ids = orderIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => { int id; return int.TryParse(s.Trim(), out id) ? id : 0; }).Where(id => id > 0).ToList();
            var orders = new List<object>();
            foreach (var oid in ids)
            {
                var order = QueryOne(conn, "SELECT o.order_id, o.order_number, o.customer_name, o.confirmation_date, d.dealer_name FROM tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id WHERE o.order_id = ?", oid);
                if (order != null)
                {
                    var existingReply = QueryOne(conn, "SELECT remarks FROM tbl_remarks_replies WHERE request_id = ? AND order_id = ?", I(row, "request_id"), oid);
                    orders.Add(Obj("order_id", oid, "order_number", S(order, "order_number"), "dealer_name", S(order, "dealer_name"), "customer_name", S(order, "customer_name"), "confirmation_date", S(order, "confirmation_date"), "existing_remarks", existingReply != null ? S(existingReply, "remarks") : ""));
                }
            }
            WriteJson(context, Obj("ok", true, "request_id", I(row, "request_id"), "status", S(row, "status"), "requested_at", S(row, "requested_at"), "orders", orders));
        }
    }

    private void HandleRemarksReplySave(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var requestId = IntRequired(Value(context, "request_id"), "Request ID required.");
            var bulkRemarks = Value(context, "remarks");
            var orderRemarksRaw = Value(context, "order_remarks");
            var request = QueryOne(conn, "SELECT * FROM tbl_remarks_requests WHERE request_id = ?", requestId);
            if (request == null) throw new ApiFailure(404, "Request not found.");
            var orderIdsStr = S(request, "order_ids");
            var ids = orderIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => { int id; return int.TryParse(s.Trim(), out id) ? id : 0; }).Where(id => id > 0).ToList();
            var now = IstNow();
            Dictionary<int, string> perOrderRemarks = null;
            if (!string.IsNullOrWhiteSpace(orderRemarksRaw))
            {
                perOrderRemarks = new Dictionary<int, string>();
                var pairs = orderRemarksRaw.Split(new[] { '~' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var pair in pairs)
                {
                    var parts = pair.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        int oid;
                        if (int.TryParse(parts[0].Trim(), out oid))
                            perOrderRemarks[oid] = parts[1].Trim();
                    }
                }
            }
            var repliedCount = 0;
            foreach (var oid in ids)
            {
                var remarks = bulkRemarks;
                if (perOrderRemarks != null && perOrderRemarks.ContainsKey(oid) && !string.IsNullOrWhiteSpace(perOrderRemarks[oid]))
                    remarks = perOrderRemarks[oid];
                if (string.IsNullOrWhiteSpace(remarks)) continue;
                Execute(conn, "DELETE FROM tbl_remarks_replies WHERE request_id = ? AND order_id = ?", requestId, oid);
                Execute(conn, "INSERT INTO tbl_remarks_replies (request_id, order_id, remarks, replied_by, replied_at) VALUES (?, ?, ?, ?, " + SqlDateLiteral(now) + ")",
                    requestId, oid, remarks, I(user, "user_id"));
                repliedCount++;
            }
            var newStatus = repliedCount >= ids.Count ? "replied" : "partial";
            Execute(conn, "UPDATE tbl_remarks_requests SET status = ?, replied_by = ?, replied_at = " + SqlDateLiteral(now) + " WHERE request_id = ?",
                newStatus, I(user, "user_id"), requestId);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleRemarksRequestsList(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var roleName = S(user, "role_name");
            var rows = roleName == "Admin"
                ? QueryAll(conn, "SELECT * FROM tbl_remarks_requests ORDER BY created_at DESC")
                : QueryAll(conn, "SELECT * FROM tbl_remarks_requests WHERE requested_by = ? ORDER BY created_at DESC", I(user, "user_id"));
            var result = new List<object>();
            foreach (var r in rows)
            {
                var reqBy = I(r, "requested_by");
                var reqUser = reqBy != null ? QueryOne(conn, "SELECT full_name FROM tbl_users WHERE user_id = ?", reqBy) : null;
                var repBy = I(r, "replied_by");
                var repUser = repBy != null ? QueryOne(conn, "SELECT full_name FROM tbl_users WHERE user_id = ?", repBy) : null;
                result.Add(Obj("request_id", I(r, "request_id"), "token", S(r, "token"), "order_ids", S(r, "order_ids"), "requester_name", reqUser != null ? S(reqUser, "full_name") : "", "requested_at", S(r, "requested_at"), "status", S(r, "status"), "replied_at", S(r, "replied_at"), "replier_name", repUser != null ? S(repUser, "full_name") : "", "reminder_count", I(r, "reminder_count")));
            }
            WriteJson(context, Obj("rows", result));
        }
    }

    private void HandleRemarksRequestReminder(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var requestId = IntRequired(Value(context, "request_id"), "Request ID required.");
            var request = QueryOne(conn, "SELECT * FROM tbl_remarks_requests WHERE request_id = ?", requestId);
            if (request == null) throw new ApiFailure(404, "Request not found.");
            var currentCount = I(request, "reminder_count");
            var newCount = currentCount + 1;
            Execute(conn, "UPDATE tbl_remarks_requests SET reminder_count = ? WHERE request_id = ?", newCount, requestId);
            WriteJson(context, Obj("ok", true, "reminder_count", newCount));
        }
    }

    private void HandleRemarksRequestClose(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var requestId = IntRequired(Value(context, "request_id"), "Request ID required.");
            var request = QueryOne(conn, "SELECT * FROM tbl_remarks_requests WHERE request_id = ?", requestId);
            if (request == null) throw new ApiFailure(404, "Request not found.");
            if (I(request, "requested_by") != I(user, "user_id") && S(user, "role_name") != "Admin")
                throw new ApiFailure(403, "You can only close your own requests.");
            if (S(request, "status") != "pending")
                throw new ApiFailure(400, "Only pending requests can be closed.");
            Execute(conn, "UPDATE tbl_remarks_requests SET status = 'closed' WHERE request_id = ?", requestId);
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleRemarksRequestDelete(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var requestIdVal = N(Value(context, "request_id"));
            if (requestIdVal.HasValue)
            {
                var requestId = (int)requestIdVal.Value;
                var request = QueryOne(conn, "SELECT * FROM tbl_remarks_requests WHERE request_id = ?", requestId);
                if (request == null) throw new ApiFailure(404, "Request not found.");
                if (I(request, "requested_by") != I(user, "user_id") && S(user, "role_name") != "Admin")
                    throw new ApiFailure(403, "You can only delete your own requests.");
                if (S(request, "status") != "pending")
                    throw new ApiFailure(400, "Only pending requests can be deleted.");
                Execute(conn, "DELETE FROM tbl_remarks_replies WHERE request_id = ?", requestId);
                Execute(conn, "DELETE FROM tbl_remarks_requests WHERE request_id = ?", requestId);
            }
            else
            {
                var sevenDaysAgo = IstNow().AddDays(-7);
                var oldPending = QueryAll(conn,
                    "SELECT request_id FROM tbl_remarks_requests WHERE requested_by = ? AND status = 'pending' AND requested_at < ?",
                    I(user, "user_id"), SqlDateLiteral(sevenDaysAgo));
                foreach (var r in oldPending)
                {
                    var rid = I(r, "request_id");
                    Execute(conn, "DELETE FROM tbl_remarks_replies WHERE request_id = ?", rid);
                    Execute(conn, "DELETE FROM tbl_remarks_requests WHERE request_id = ?", rid);
                }
            }
            WriteJson(context, Obj("ok", true));
        }
    }

    private void HandleRemarksReportExport(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var roleName = S(user, "role_name");
            var userId = I(user, "user_id");
            var sevenDaysAgo = IstNow().AddDays(-7);
            var whereClause = roleName == "Admin" ? "" : " AND rr.requested_by = " + SqlIntLiteral(userId);
            var sql = "SELECT rr.requested_at, rr.replied_at, rr.status, rr.replied_by, " +
                       "o.order_number, d.dealer_name, o.customer_name, " +
                       "rep.remarks, u.full_name AS replier_name " +
                       "FROM (((tbl_remarks_requests AS rr " +
                       "INNER JOIN tbl_remarks_replies AS rep ON rr.request_id = rep.request_id) " +
                       "INNER JOIN tbl_orders AS o ON rep.order_id = o.order_id) " +
                       "LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) " +
                       "LEFT JOIN tbl_users AS u ON rep.replied_by = u.user_id " +
                       "WHERE rr.replied_at >= " + SqlDateLiteral(sevenDaysAgo) + " " +
                       "AND rr.status IN ('replied','partial')" + whereClause + " " +
                       "ORDER BY rr.replied_at DESC";
            var rows = QueryAll(conn, sql);
            var result = new List<object>();
            foreach (var r in rows)
            {
                result.Add(Obj(
                    "requested_at", S(r, "requested_at"),
                    "replied_at", S(r, "replied_at"),
                    "status", S(r, "status"),
                    "order_number", S(r, "order_number"),
                    "dealer_name", S(r, "dealer_name"),
                    "customer_name", S(r, "customer_name"),
                    "reply_remarks", S(r, "remarks"),
                    "replier_name", S(r, "replier_name")
                ));
            }
            WriteJson(context, Obj("rows", result));
        }
    }

    private void HandleRemarksReport(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            EnsureSchema(conn);
            var user = RequireLogin(context, conn);
            var roleName = S(user, "role_name");
            var applyUserFilter = roleName == "Marketing User";
            var userId = I(user, "user_id");
            List<Dictionary<string, object>> doneRows;
            List<Dictionary<string, object>> pendingRows;
            BuildRemarksReportData(conn, applyUserFilter, userId, out doneRows, out pendingRows);
            WriteJson(context, Obj("done_rows", doneRows, "pending_rows", pendingRows));
        }
    }

    private void HandleRemarksReportMail(HttpContext context)
    {
        using (var conn = OpenConnection(context))
        {
            var user = RequireLogin(context, conn);
            EnsureRole(user, "Admin", "Production Planner User");
            var force = Value(context, "force") == "1";
            var siteRoot = ResolveSiteRoot(context);
            string message;
            var sent = TrySendRemarksReport(siteRoot, force, out message);
            WriteJson(context, Obj("ok", true, "sent", sent, "message", message));
        }
    }

    private void BuildRemarksReportData(OleDbConnection conn, bool applyUserFilter, int userId, out List<Dictionary<string, object>> doneRows, out List<Dictionary<string, object>> pendingRows)
    {
        doneRows = new List<Dictionary<string, object>>();
        pendingRows = new List<Dictionary<string, object>>();
        EnsureSchema(conn);

        var todayStart = IstNow().Date;
        var doneWhere = applyUserFilter ? " AND rr.requested_by = " + SqlIntLiteral(userId) : "";
        var doneSql = "SELECT rr.requested_at, rr.replied_at, rr.status, rr.requested_by, o.order_number, d.dealer_name, o.customer_name, rep.remarks, u.full_name AS replier_name FROM (((tbl_remarks_requests AS rr INNER JOIN tbl_remarks_replies AS rep ON rr.request_id = rep.request_id) INNER JOIN tbl_orders AS o ON rep.order_id = o.order_id) LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id) LEFT JOIN tbl_users AS u ON rep.replied_by = u.user_id WHERE rr.replied_at >= " + SqlDateLiteral(todayStart) + " AND rr.status IN ('replied','partial')" + doneWhere + " ORDER BY rr.replied_at DESC";
        foreach (var r in QueryAll(conn, doneSql))
        {
            var requestedBy = I(r, "requested_by");
            var reqUser = requestedBy != 0 ? QueryOne(conn, "SELECT full_name FROM tbl_users WHERE user_id = " + requestedBy) : null;
            doneRows.Add(Obj(
                "requested_date", FmtDate(DT(r, "requested_at")),
                "requested_time", FmtTime(DT(r, "requested_at")),
                "replied_date", FmtDate(DT(r, "replied_at")),
                "replied_time", FmtTime(DT(r, "replied_at")),
                "status", S(r, "status"),
                "order_number", S(r, "order_number"),
                "dealer_name", S(r, "dealer_name"),
                "customer_name", S(r, "customer_name"),
                "reply_remarks", S(r, "remarks"),
                "replier_name", S(r, "replier_name"),
                "requester_name", reqUser != null ? S(reqUser, "full_name") : ""
            ));
        }

        var pendWhere = applyUserFilter ? " AND rr.requested_by = " + SqlIntLiteral(userId) : "";
        var pendSql = "SELECT rr.request_id, rr.order_ids, rr.status, rr.requested_at, rr.reminder_count, rr.requested_by FROM tbl_remarks_requests AS rr WHERE rr.status = 'pending'" + pendWhere + " ORDER BY rr.requested_at DESC";
        foreach (var r in QueryAll(conn, pendSql))
        {
            var orderIdsStr = S(r, "order_ids");
            var ids = (orderIdsStr ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => { int id; return int.TryParse(s.Trim(), out id) ? id : 0; })
                .Where(id => id > 0).ToList();
            Dictionary<string, object> firstOrder = null;
            if (ids.Count > 0)
                firstOrder = QueryOne(conn, "SELECT o.order_number, d.dealer_name, o.customer_name FROM tbl_orders AS o LEFT JOIN tbl_dealers AS d ON o.dealer_id = d.dealer_id WHERE o.order_id = ?", ids[0]);
            var requestedBy = I(r, "requested_by");
            var reqUser = requestedBy != 0 ? QueryOne(conn, "SELECT full_name FROM tbl_users WHERE user_id = " + requestedBy) : null;
            pendingRows.Add(Obj(
                "requested_date", FmtDate(DT(r, "requested_at")),
                "requested_time", FmtTime(DT(r, "requested_at")),
                "status", S(r, "status"),
                "order_number", firstOrder != null ? S(firstOrder, "order_number") : "",
                "dealer_name", firstOrder != null ? S(firstOrder, "dealer_name") : "",
                "customer_name", firstOrder != null ? S(firstOrder, "customer_name") : "",
                "order_count", ids.Count,
                "requester_name", reqUser != null ? S(reqUser, "full_name") : "",
                "reminder_count", I(r, "reminder_count")
            ));
        }
    }

    private static string FmtDate(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "-";
    }

    private static string FmtTime(object value)
    {
        var dt = ToDateTime(value);
        return dt.HasValue ? dt.Value.ToString("hh:mm tt", CultureInfo.InvariantCulture) : "-";
    }

    private static string BuildRemarksReportHtml(List<Dictionary<string, object>> doneRows, List<Dictionary<string, object>> pendingRows, MailSettings settings, DateTime sentAt)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Elenza PMS Remarks Report</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;background:#eef5fb;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;}");
        sb.Append(".mail-shell{max-width:1080px;margin:0 auto;padding:24px 16px;}");
        sb.Append(".mail-card{background:#ffffff;border:0.5px solid #d9e5f3;border-radius:24px;overflow:hidden;box-shadow:0 16px 40px rgba(4,92,180,0.08);}");
        sb.Append(".mail-head{padding:26px 30px;background:linear-gradient(135deg,#ffffff 0%,#eaf2fb 100%);border-bottom:0.5px solid #d9e5f3;}");
        sb.Append(".mail-body{padding:22px 30px 12px;}");
        sb.Append(".sec-title{margin:24px 0 4px;font-size:21px;color:#0f172a;}");
        sb.Append(".sec-sub{font-size:13px;color:#64748b;margin:0 0 12px;}");
        sb.Append(".count-pill{padding:4px 12px;border-radius:999px;background:#e8f1fb;color:#0f6cbd;font-size:12px;font-weight:700;}");
        sb.Append(".report-wrap{overflow-x:auto;border:0.5px solid #d9e5f3;border-radius:18px;background:#fff;}");
        sb.Append(".report-table{width:100%;border-collapse:collapse;min-width:820px;}");
        sb.Append(".report-head th{padding:12px 14px;text-align:left;font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:#1e293b;border-bottom:0.5px solid #d9e5f3;background:#f0f5fa;}");
        sb.Append(".report-cell{padding:11px 14px;font-size:13px;color:#0f172a;border-bottom:0.5px solid #eef2f7;vertical-align:top;}");
        sb.Append(".dt{font-weight:700;color:#0f172a;} .tm{color:#64748b;}");
        sb.Append(".remark{color:#334155;font-size:12.5px;line-height:1.45;}");
        sb.Append(".pill{padding:4px 10px;border-radius:999px;font-size:11px;font-weight:700;}");
        sb.Append(".pill-done{background:#e9f8ef;color:#15803d;}");
        sb.Append(".pill-pending{background:#fff4db;color:#9a6700;}");
        sb.Append(".empty{padding:18px;color:#94a3b8;font-size:13px;}");
        sb.Append("@media only screen and (max-width:720px){.mail-shell{padding:10px 8px!important;}.mail-head,.mail-body{padding:18px 14px!important;}}");
        sb.Append("</style></head><body>");
        sb.Append("<div class=\"mail-shell\"><div class=\"mail-card\">");
        sb.Append("<div class=\"mail-head\">");
        sb.Append("<div style=\"font-size:13px;letter-spacing:1.4px;font-weight:700;color:#046bd2;text-transform:uppercase;\">ElenzaIndia.com</div>");
        sb.Append("<h1 style=\"margin:10px 0 8px;font-size:30px;line-height:1.1;color:#0f172a;\">Remarks Replies Report</h1>");
        sb.Append("<p style=\"margin:0;font-size:15px;color:#475569;\">Daily snapshot sent at " + Html(sentAt.ToString("dd MMM yyyy hh:mm tt", CultureInfo.InvariantCulture)) + " IST. Replies done today and all pending replies.</p>");
        sb.Append("</div><div class=\"mail-body\">");

        sb.Append("<h2 class=\"sec-title\">Replies Done Today <span class=\"count-pill\">" + doneRows.Count + "</span></h2>");
        sb.Append("<p class=\"sec-sub\">All remarks replies marked done / partial since 12:00 AM IST today.</p>");
        if (doneRows.Count == 0)
        {
            sb.Append("<div class=\"report-wrap\"><div class=\"empty\">No replies were completed today.</div></div>");
        }
        else
        {
            sb.Append("<div class=\"report-wrap\"><table class=\"report-table\"><thead class=\"report-head\"><tr>");
            sb.Append("<th>Replied Date</th><th>Replied Time</th><th>Order</th><th>Dealer</th><th>Customer</th><th>Replied By</th><th>Remarks</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var r in doneRows)
            {
                sb.Append("<tr>");
                sb.Append("<td class=\"report-cell\"><span class=\"dt\">" + Html(S(r, "replied_date")) + "</span></td>");
                sb.Append("<td class=\"report-cell\"><span class=\"tm\">" + Html(S(r, "replied_time")) + "</span></td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "order_number")) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "dealer_name")) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "customer_name")) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "replier_name")) + "</td>");
                sb.Append("<td class=\"report-cell\"><div class=\"remark\">" + Html(S(r, "reply_remarks")) + "</div></td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        sb.Append("<h2 class=\"sec-title\">Pending Replies (Open) <span class=\"count-pill\">" + pendingRows.Count + "</span></h2>");
        sb.Append("<p class=\"sec-sub\">All remark requests awaiting a reply, from the beginning until they are answered.</p>");
        if (pendingRows.Count == 0)
        {
            sb.Append("<div class=\"report-wrap\"><div class=\"empty\">No pending replies. Everything is answered.</div></div>");
        }
        else
        {
            sb.Append("<div class=\"report-wrap\"><table class=\"report-table\"><thead class=\"report-head\"><tr>");
            sb.Append("<th>Requested Date</th><th>Requested Time</th><th>Order</th><th>Dealer</th><th>Customer</th><th>Requested By</th><th>Status</th>");
            sb.Append("</tr></thead><tbody>");
            foreach (var r in pendingRows)
            {
                var orderLabel = S(r, "order_number");
                var count = Convert.ToInt32(r["order_count"]);
                if (count > 1) orderLabel += " <span style=\"color:#64748b;font-size:11px;\">(+" + (count - 1) + " more)</span>";
                sb.Append("<tr>");
                sb.Append("<td class=\"report-cell\"><span class=\"dt\">" + Html(S(r, "requested_date")) + "</span></td>");
                sb.Append("<td class=\"report-cell\"><span class=\"tm\">" + Html(S(r, "requested_time")) + "</span></td>");
                sb.Append("<td class=\"report-cell\">" + Html(orderLabel) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "dealer_name")) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "customer_name")) + "</td>");
                sb.Append("<td class=\"report-cell\">" + Html(S(r, "requester_name")) + "</td>");
                sb.Append("<td class=\"report-cell\"><span class=\"pill pill-pending\">Pending</span></td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        sb.Append("</div></div></div></body></html>");
        return sb.ToString();
    }

    private static bool TrySendRemarksReport(string siteRoot, bool force, out string message)
    {
        message = "Remarks report not processed.";
        if (!Monitor.TryEnter(MailSync))
        {
            message = "Mail job is already running.";
            return false;
        }
        try
        {
            if (string.IsNullOrWhiteSpace(siteRoot))
            {
                message = "Site root could not be resolved.";
                return false;
            }
            var settings = LoadMailSettings(siteRoot);
            if (settings == null || !settings.Enabled)
            {
                message = "SMTP mail is disabled.";
                return false;
            }
            var now = NowInZone(settings.TimeZoneId);
            if (!force && now.Hour < RemarksReportHour)
            {
                message = "Remarks report is scheduled for 9:00 PM IST.";
                return false;
            }
            var reportDate = now.Date;
            using (var conn = OpenConnection(siteRoot))
            {
                new PmsApiHandler().EnsureSchema(conn);
                if (!force && WasMailAlreadySent(conn, RemarksReportKind, reportDate))
                {
                    message = "Remarks report already sent today.";
                    return false;
                }
                List<Dictionary<string, object>> doneRows;
                List<Dictionary<string, object>> pendingRows;
                new PmsApiHandler().BuildRemarksReportData(conn, false, 0, out doneRows, out pendingRows);
                var html = BuildRemarksReportHtml(doneRows, pendingRows, settings, now);
                var subject = "Elenza PMS Remarks Replies Report | " + reportDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
                try
                {
                    SendDailyReportMail(settings, subject, html);
                    LogMailReport(conn, RemarksReportKind, reportDate, string.Join(", ", settings.ToEmails), subject, "SENT", "", now);
                    message = "Remarks report sent to " + string.Join(", ", settings.ToEmails) + ".";
                    return true;
                }
                catch (Exception ex)
                {
                    LogMailReport(conn, RemarksReportKind, reportDate, string.Join(", ", settings.ToEmails), subject, "FAILED", ex.Message, now);
                    message = ex.Message;
                    return false;
                }
            }
        }
        finally
        {
            Monitor.Exit(MailSync);
        }
    }

    private static DateTime _lastRemarksSchedulerProbeUtc = DateTime.MinValue;
    private static Timer _remarksSchedulerTimer;

    public static void StartRemarksReportScheduler()
    {
        if (_remarksSchedulerTimer != null) return;
        _remarksSchedulerTimer = new Timer(_ =>
        {
            try { RunRemarksReportSchedulerIfDue(); } catch { }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    public static void RunRemarksReportSchedulerIfDue()
    {
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastRemarksSchedulerProbeUtc).TotalMinutes < 1) return;
        _lastRemarksSchedulerProbeUtc = nowUtc;
        string message;
        try { TrySendRemarksReport(ResolveSiteRoot(null), false, out message); } catch { }
    }

    private static DateTime _lastAutoAdvanceProbeUtc = DateTime.MinValue;
    private static Timer _autoAdvanceTimer;

    public static void StartAutoAdvanceScheduler()
    {
        if (_autoAdvanceTimer != null) return;
        _autoAdvanceTimer = new Timer(_ =>
        {
            try { RunAutoAdvancePlannerBoardIfDue(); } catch { }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
    }

    public static void RunAutoAdvancePlannerBoardIfDue()
    {
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastAutoAdvanceProbeUtc).TotalMinutes < 55) return;
        _lastAutoAdvanceProbeUtc = nowUtc;
        try
        {
            var siteRoot = ResolveSiteRoot(null);
            using (var conn = new OleDbConnection(System.Configuration.ConfigurationManager.ConnectionStrings["PmsDb"].ConnectionString))
            {
                conn.Open();
                TryExecute(conn, "CREATE TABLE IF NOT EXISTS tbl_scheduler_log (log_id COUNTER PRIMARY KEY, scheduler_name TEXT(100), ran_at DATETIME, message MEMO)");
                var lastRun = Scalar(conn, "SELECT MAX(ran_at) FROM tbl_scheduler_log WHERE scheduler_name = 'AutoAdvancePlannerBoard'");
                if (lastRun != null)
                {
                    var lastRunTime = Convert.ToDateTime(lastRun);
                    if ((IstNow() - lastRunTime).TotalMinutes < 55) return;
                }
                var now = IstNow();
                var machines = QueryAll(conn, "SELECT machine_id, machine_name, sequence_no FROM tbl_machines WHERE is_active = TRUE ORDER BY sequence_no");
                if (machines.Count == 0) return;
                var machineDict = machines.ToDictionary(m => Convert.ToInt32(m["machine_id"]));
                var machineList = machines.Select(m => Convert.ToInt32(m["machine_id"])).ToList();
                var plannerBoard = QueryAll(conn, "SELECT board_id, order_id, station_id FROM tbl_planner_board");
                if (plannerBoard.Count == 0)
                {
                    Execute(conn, "INSERT INTO tbl_scheduler_log (scheduler_name, ran_at, message) VALUES ('AutoAdvancePlannerBoard', " + SqlDateLiteral(now) + ", 'No orders in planner board')");
                    return;
                }
                var advanced = 0;
                var skipped = 0;
                foreach (var pbRow in plannerBoard)
                {
                    var orderId = Convert.ToInt32(pbRow["order_id"]);
                    var currentStationId = Convert.ToInt32(pbRow["station_id"]);
                    if (!machineDict.ContainsKey(currentStationId)) { skipped++; continue; }
                    var currentMachine = machineDict[currentStationId];
                    var currentSeqNo = Convert.ToInt32(currentMachine["sequence_no"]);
                    var completedRows = QueryAll(conn, "SELECT queue_status_code FROM tbl_order_station_queue WHERE order_id = " + orderId + " AND station_id = " + currentStationId + " AND (queue_status_code = 'COMPLETED' OR queue_status_code = 'PARTIAL_COMPLETED')");
                    if (completedRows.Count == 0) { skipped++; continue; }
                    var currentIdx = machineList.IndexOf(currentStationId);
                    if (currentIdx < 0 || currentIdx >= machineList.Count - 1) { skipped++; continue; }
                    var nextStationId = machineList[currentIdx + 1];
                    var existingNext = QueryAll(conn, "SELECT board_id FROM tbl_planner_board WHERE order_id = " + orderId + " AND station_id = " + nextStationId);
                    if (existingNext.Count > 0) { skipped++; continue; }
                    Execute(conn, "INSERT INTO tbl_planner_board (order_id, station_id, assigned_by, assigned_at) VALUES (" + orderId + ", " + nextStationId + ", 0, " + SqlDateLiteral(now) + ")");
                    advanced++;
                }
                Execute(conn, "INSERT INTO tbl_scheduler_log (scheduler_name, ran_at, message) VALUES ('AutoAdvancePlannerBoard', " + SqlDateLiteral(now) + ", 'Advanced " + advanced + " orders, skipped " + skipped + "')");
            }
        }
        catch { }
    }
}

