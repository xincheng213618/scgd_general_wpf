using ColorVision.Common.MVVM;
using ColorVision.UserSpace;

namespace ColorVision.Engine.UserSpace
{
    public class UserManager:ViewModelBase
    {
        private static UserManager _instance;
        private static readonly object Locker = new();
        public static UserManager GetInstance() { lock (Locker) { return _instance ??= new UserManager(); } }
        public RelayCommand CreateCommand { get; set; }

        public RelayCommand SaveCommand { get; set; }

        public static UserConfig Config => UserConfig.Instance;

        public UserManager()
        {

        }






    }
}
