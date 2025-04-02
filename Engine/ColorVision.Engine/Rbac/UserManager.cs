using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using log4net;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Rbac
{
    public class UserManagerService : IMainWindowInitialized
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(UserManagerService));
        public  Task Initialize()
        {
            UserManager.GetInstance().Load();
            return Task.CompletedTask;
        }
    }



    public class UserManager:ViewModelBase
    {
        private static UserManager _instance;
        private static readonly object Locker = new();
        public static UserManager GetInstance() { lock (Locker) { return _instance ??= new UserManager(); } }
        public RelayCommand CreateCommand { get; set; }
        public RelayCommand LoginCommand { get; set; }
        public RelayCommand LogoutCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }
        public RelayCommand EditCommand { get; set; }

        public static UserConfig Config => UserConfig.Instance;

        public UserManager()
        {

            string sql = "CREATE TABLE IF NOT EXISTS `t_scgd_sys_user_detail` (\r\n  `id` int(11) NOT NULL AUTO_INCREMENT,\r\n  `user_id` int(11) NOT NULL,\r\n  `gender` varchar(10) DEFAULT NULL,\r\n  `email` varchar(255) DEFAULT NULL,\r\n  `phone` varchar(20) DEFAULT NULL,\r\n  `address` varchar(255) DEFAULT NULL,\r\n  `company` varchar(255) DEFAULT NULL,\r\n  `department` varchar(255) DEFAULT NULL,\r\n  `position` varchar(255) DEFAULT NULL,\r\n  `remark` varchar(256) DEFAULT NULL,\r\n  `user_image` varchar(255) DEFAULT 'Config\\\\user.jpg',\r\n  PRIMARY KEY (`id`) USING BTREE,\r\n  KEY `fk_user_id` (`user_id`),\r\n  CONSTRAINT `fk_user_id` FOREIGN KEY (`user_id`) REFERENCES `t_scgd_sys_user` (`id`) ON DELETE CASCADE\r\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 ROW_FORMAT=DYNAMIC COMMENT='用户详细信息表';";
            MySqlControl.GetInstance().ExecuteNonQuery(sql);

            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            SaveCommand = new RelayCommand(a => Save());
            EditCommand = new RelayCommand(a => Edit());
        }

        public void Edit()
        {
            new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            Save();
        }

        public UserModel? UserModel { get; set; }

        public UserDetailModel UserDetailModel { get; set; }

        public void Load()
        {
            IsLogin = UserDao.Instance.Checklogin(UserConfig.Instance.Account,UserConfig.Instance.UserPwd);

            UserModel = UserDao.Instance.GetByParam(new System.Collections.Generic.Dictionary<string, object>() { { "name","admin" } });
            if (UserModel == null)
            {
                UserModel = new UserModel() { UserName = "admin", UserPwd = "admin" };
                UserDao.Instance.Save(UserModel);
                UserDetailModel = new UserDetailModel { UserId = UserModel.Id };
                UserDetailDao.Instance.Save(UserDetailModel);
            }
            var  detailModel = UserDetailDao.Instance.GetByParam(new System.Collections.Generic.Dictionary<string, object>() { { "user_id", UserModel.Id } });
            if (detailModel !=null)
            {

                UserDetailModel = detailModel;
            }
            else
            {
                UserDetailModel = new UserDetailModel { UserId = UserModel.Id };
                UserDetailDao.Instance.Save(UserDetailModel);
            }
        }


        public bool IsLogin { get => _IsLogin; set { _IsLogin = true; NotifyPropertyChanged(); } }
        private bool _IsLogin;



        public void Save()
        {
            UserDetailDao.Instance.Save(UserDetailModel);
            IsLogin = true;
            Authorization.Instance.PermissionMode = UserDetailModel.PermissionMode;
        }
    }
}
