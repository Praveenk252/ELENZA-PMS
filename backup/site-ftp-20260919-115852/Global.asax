<%@ Application Language="C#" %>
<script runat="server">
    void Application_Start(object sender, EventArgs e)
    {
        System.Diagnostics.Trace.TraceInformation("Global.asax v2 - " + DateTime.Now);
        try { PmsApiHandler.StartRemarksReportScheduler(); } catch { }
    }
</script>
