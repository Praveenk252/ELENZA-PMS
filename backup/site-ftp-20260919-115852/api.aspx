<%@ Page Language="C#" ValidateRequest="false" EnableSessionState="True" %>
<script runat="server">
protected void Page_Load(object sender, EventArgs e)
{
    new PmsApiHandler().ProcessRequest(Context);
}
</script>
