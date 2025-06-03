using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI.Authorizations;
using log4net;
using MySql.Data.MySqlClient;
using System;

namespace ColorVision.Engine.Rbac
{

    [Table("t_scgd_sys_user")]
    public class UserModel : VPKModel
    {
        [Column("name")]
        public string UserName { get => _UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        [Column("code")]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code = string.Empty;

        [Column("pwd")]
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;

        [Column("is_enable")]
        public bool IsEnable { get; set; } = true;
        [Column("is_delete")]
        public bool? IsDelete { get; set; } = false;

        [Column("remark")]
        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;
    }


    [Table("t_scgd_sys_user_detail")]
    public class UserDetailModel : VPKModel
    {
        [Column("user_id")]
        public int UserId { get => _UserId; set { _UserId = value; NotifyPropertyChanged(); } }
        private int _UserId;

        [Column("permission_mode")]
        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; NotifyPropertyChanged(); } }
        private PermissionMode _PermissionMode;

        [Column("email")]
        public string Email { get => _Email; set { _Email = value; NotifyPropertyChanged(); } }
        private string _Email = string.Empty;

        [Column("phone")]
        public string Phone { get => _Phone; set { _Phone = value; NotifyPropertyChanged(); } }
        private string _Phone = string.Empty;

        [Column("address")]
        public string Address { get => _Address; set { _Address = value; NotifyPropertyChanged(); } }
        private string _Address = string.Empty;

        [Column("company")]
        public string Company { get => _Company; set { _Company = value; NotifyPropertyChanged(); } }
        private string _Company = string.Empty;

        [Column("department")]
        public string Department { get => _Department; set { _Department = value; NotifyPropertyChanged(); } }
        private string _Department = string.Empty;

        [Column("position")]
        public string Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private string _Position = string.Empty;

        [Column("remark")]
        public string Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string _Remark = string.Empty;

        [Column("user_image")]
        public string UserImage { get => _UserImage; set { _UserImage = value; NotifyPropertyChanged(); } }
        private string _UserImage;

    };

    public class UserDetailDao : BaseTableDao<UserDetailModel>
    {
        public static UserDetailDao Instance { get; set; } = new UserDetailDao();

        public UserDetailDao() : base("t_scgd_sys_user_detail")
        {

        }
    }



    public class UserDao:BaseTableDao<UserModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseDao));
        public static UserDao Instance { get; set; } = new UserDao();

        public UserDao():base("t_scgd_sys_user")
        {

        }
    }
}
