using System;
using System.Collections.Generic;
using System.Linq;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.UserSpace.Dao;
using log4net;
using MySql.Data.MySqlClient;

namespace ColorVision.UserSpace.Dao
{
    [Table("t_scgd_sys_user")]
    public class UserModel : VPKModel
    {
        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 租户ID
        /// </summary>
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;

        public  List<Tenant?> UserTenants()
        {
            return UserTenantDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "user_id", Id } }).Select(item => TenantDao.Instance.GetById(item.Id)).ToList();
        }

        //性别
        public Gender Gender { get => _Gender; set { _Gender = value; NotifyPropertyChanged(); } }
        private Gender _Gender;
        public string Email { get => _Email; set { _Email = value; NotifyPropertyChanged(); } }
        private string _Email = string.Empty;

        public string Phone { get => _Phone; set { _Phone = value; NotifyPropertyChanged(); } }
        private string _Phone = string.Empty;

        public string Address { get => _Address; set { _Address = value; NotifyPropertyChanged(); } }
        private string _Address = string.Empty;

        public string Company { get => _Company; set { _Company = value; NotifyPropertyChanged(); } }
        private string _Company = string.Empty;

        public string Department { get => _Department; set { _Department = value; NotifyPropertyChanged(); } }
        private string _Department = string.Empty;

        public string Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private string _Position = string.Empty;

        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;

        public string UserImage { get => _UserImage; set { _UserImage = value; NotifyPropertyChanged(); } }
        private string _UserImage = "Config\\user.jpg";

    };


    public class UserDao:BaseTableDao<UserModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));
        public MySqlConnection MySqlConnection => MySqlControl.MySqlConnection;

        public static UserDao Instance { get; set; } = new UserDao();

        public UserDao():base("t_scgd_sys_user")
        {

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
            if (!MySqlControl.IsConnect) return false;

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
