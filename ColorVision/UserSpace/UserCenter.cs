using ColorVision.Settings;

namespace ColorVision.UserSpace
{
    public class UserCenter
    {
        private static UserCenter _instance;
        private static readonly object _locker = new();
        public static UserCenter GetInstance() { lock (_locker) { return _instance ??= new UserCenter(); } }

        public UserConfig UserConfig { get => SoftwareConfig.UserConfig; }

        public int TenantId { get => UserConfig.TenantId;}   

        public SoftwareConfig SoftwareConfig { get; set; }
        public UserCenter()
        {
            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
        }



    }
}
