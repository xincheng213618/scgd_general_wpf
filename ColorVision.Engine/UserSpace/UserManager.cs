using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UserSpace;
using ColorVision.UserSpace.Dao;
using cvColorVision;
using log4net;
using System;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.Engine.UserSpace
{
    public class UserManagerService : IMainWindowInitialized
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(UserManagerService));
        public void Initialize()
        {
            UserManager.GetInstance().Load();
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
            LoginCommand = new RelayCommand(a => new LoginWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            SaveCommand = new RelayCommand(a => Save());
            EditCommand = new RelayCommand(a => new UserEdit(this) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public void Load()
        {
            IsLogin = UserDao.Instance.Checklogin(UserConfig.Instance.Account,UserConfig.Instance.UserPwd);
        }


        public bool IsLogin { get => _IsLogin; set { _IsLogin = true; NotifyPropertyChanged(); } }
        private bool _IsLogin;



        public void Save()
        {
            Authorization.Instance.PermissionMode = UserConfig.Instance.PerMissionMode;
        }




    }
}
