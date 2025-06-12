using ColorVision.Common.MVVM;
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
            IsLogin = true;

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
