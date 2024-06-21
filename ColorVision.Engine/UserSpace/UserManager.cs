using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;
using ColorVision.UserSpace;
using System.Windows;

namespace ColorVision.Engine.UserSpace
{
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

        public void Save()
        {
            Authorization.Instance.PermissionMode = UserConfig.Instance.PerMissionMode;
        }




    }
}
