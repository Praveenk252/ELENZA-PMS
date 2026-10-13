<%@ WebHandler Language="C#" Class="PingHandler" %>

using System;
using System.Web;

public class PingHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.Write("{\"ok\":true,\"runtime\":\"aspnet\"}");
    }

    public bool IsReusable => true;
}
