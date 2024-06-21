using System;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
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

        // 其他属性和方法...

        public bool Checklogin(string account, string password)
        {
            bool isValidUser = false;

            string query = "SELECT COUNT(1) FROM Users WHERE Account = @Account AND UserPwd = @UserPwd";

            using (var command = new MySqlCommand(query, MySqlConnection))
            {
                command.Parameters.AddWithValue("@Account", account);
                command.Parameters.AddWithValue("@UserPwd", password);

                try
                {
                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        string hashedPassword = Convert.ToString(result);
                        return string.Equals(password, hashedPassword, StringComparison.Ordinal);
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
                }
            }

            return isValidUser;
        }
    }
}
