using System;
using System.Security.Policy;
using ColorVision.MySql;
using log4net;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Crypto.Generators;

namespace ColorVision.User
{
    public class Administrator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));
        public MySqlControl MySqlControl { get; set; }
        public MySqlConnection MySqlConnection { get => MySqlControl.MySqlConnection; }


        public Administrator()
        {
            MySqlControl = MySqlControl.GetInstance();
        }

        public bool CheckDatabase(string account, string password)
        {
            try
            {
                string query = "SELECT passwd FROM t_scgd_sys_user WHERE username = @username";
                using (var command = new MySqlCommand(query, MySqlConnection))
                {
                    command.Parameters.AddWithValue("@username", account);
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        string hashedPassword = Convert.ToString(result);
                        return string.Equals(hashedPassword, hashedPassword, StringComparison.Ordinal);
                    }
                    return false;
                }
            }
            catch (MySqlException ex)
            {
                // Handle exception
                Console.WriteLine("MySQL error: " + ex.Message);
                return false;
            }
        }



    }
}
