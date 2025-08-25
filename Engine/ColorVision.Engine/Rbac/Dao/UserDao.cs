using ColorVision.Database;
using ColorVision.UI.Authorizations;
using log4net;
using SqlSugar;

namespace ColorVision.Engine.Rbac
{

    [SugarTable("t_scgd_sys_user")]
    public class UserModel : VPKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string UserName { get => _UserName; set { _UserName = value; OnPropertyChanged(); } }
        private string _UserName = string.Empty;

        [SugarColumn(ColumnName ="code")]
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code = string.Empty;

        [SugarColumn(ColumnName ="pwd")]
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; OnPropertyChanged(); } }
        private string _UserPwd = string.Empty;

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark = string.Empty;
    }


    [SugarTable("t_scgd_sys_user_detail")]
    public class UserDetailModel : VPKModel
    {
        [SugarColumn(ColumnName ="user_id")]
        public int UserId { get => _UserId; set { _UserId = value; OnPropertyChanged(); } }
        private int _UserId;

        [SugarColumn(ColumnName ="permission_mode")]
        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; OnPropertyChanged(); } }
        private PermissionMode _PermissionMode;

        [SugarColumn(ColumnName ="email")]
        public string Email { get => _Email; set { _Email = value; OnPropertyChanged(); } }
        private string _Email = string.Empty;

        [SugarColumn(ColumnName ="phone")]
        public string Phone { get => _Phone; set { _Phone = value; OnPropertyChanged(); } }
        private string _Phone = string.Empty;

        [SugarColumn(ColumnName ="address")]
        public string Address { get => _Address; set { _Address = value; OnPropertyChanged(); } }
        private string _Address = string.Empty;

        [SugarColumn(ColumnName ="company")]
        public string Company { get => _Company; set { _Company = value; OnPropertyChanged(); } }
        private string _Company = string.Empty;

        [SugarColumn(ColumnName ="department")]
        public string Department { get => _Department; set { _Department = value; OnPropertyChanged(); } }
        private string _Department = string.Empty;

        [SugarColumn(ColumnName ="position")]
        public string Position { get => _Position; set { _Position = value; OnPropertyChanged(); } }
        private string _Position = string.Empty;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string _Remark = string.Empty;

        [SugarColumn(ColumnName ="user_image")]
        public string UserImage { get => _UserImage; set { _UserImage = value; OnPropertyChanged(); } }
        private string _UserImage;

    };

    public class UserDetailDao : BaseTableDao<UserDetailModel>
    {
        public static UserDetailDao Instance { get; set; } = new UserDetailDao();

    }



    public class UserDao:BaseTableDao<UserModel>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(UserDao));
        public static UserDao Instance { get; set; } = new UserDao();


    }
}
