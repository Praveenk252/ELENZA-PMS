using System.Configuration;
using System.Data.OleDb;

namespace LSPOrderTracking.App_Code
{
    public static class DbHelper
    {
        public static string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["LspAccessConnection"].ConnectionString; }
        }

        public static OleDbConnection CreateConnection()
        {
            return new OleDbConnection(ConnectionString);
        }
    }
}
