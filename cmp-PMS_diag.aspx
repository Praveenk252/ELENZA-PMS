<%@ Page Language="C#" %>
<%@ Import Namespace="System.IO" %>
<script runat="server">
protected void Page_Load(object sender, EventArgs e)
{
    Response.ContentType = "text/plain";
    var rootPath = Server.MapPath("~/App_Data/Cabinets.accdb");
    var pmsPath = Server.MapPath("/pms/App_Data/Cabinets.accdb");
    Response.Write("AppDomainAppPath=" + HttpRuntime.AppDomainAppPath + "\n");
    Response.Write("~/App_Data=" + rootPath + "\n");
    Response.Write("~/App_Data exists=" + File.Exists(rootPath) + "\n");
    Response.Write("/pms/App_Data=" + pmsPath + "\n");
    Response.Write("/pms/App_Data exists=" + File.Exists(pmsPath) + "\n");
}
</script>
