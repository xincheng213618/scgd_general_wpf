using System;
using ColorVision.MySql;
using ColorVision.MySql.ORM;
using log4net;
using MySql.Data.MySqlClient;

namespace ColorVision.UserSpace.Dao
{
    public class UserDao
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));
        public MySqlControl MySqlControl { get; set; }
        public MySqlConnection MySqlConnection { get => MySqlControl.MySqlConnection; }

        public UserDao()
        {
            MySqlControl = MySqlControl.GetInstance();
        }


        public void GetAllUser()
        {
            string query = "SELECT * FROM t_scgd_sys_user WHERE 1=1";

            using var command = new MySqlCommand(query, MySqlConnection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                Console.WriteLine(reader.GetString(0));
            }

        }

        public bool Checklogin(string account, string password)
        {
            try
            {
                string query = "SELECT pwd FROM t_scgd_sys_user WHERE name = @name";
                using (var command = new MySqlCommand(query, MySqlConnection))
                {
                    command.Parameters.AddWithValue("@name", account);
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        string hashedPassword = Convert.ToString(result);
                        return string.Equals(password, hashedPassword, StringComparison.Ordinal);
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
