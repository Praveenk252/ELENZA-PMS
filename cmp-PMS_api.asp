<%@ Language=JScript %>
<%
Response.Buffer = true;
Response.CacheControl = "no-cache";
Response.AddHeader("Pragma", "no-cache");
Response.Expires = -1;
%>
<!--#include file="includes/pms.asp" -->
<%
routeApi();
%>
