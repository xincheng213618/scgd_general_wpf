using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.Services.RC
{
    public delegate void UseRcServicelHandler(bool IsUseRcServicel);

    public class RCSetting : ViewModelBase, IConfigSecure
    {
        public static RCSetting Instance => ConfigHandler1.GetInstance().GetRequiredService<RCSetting>();

        public static RCServiceControl RCServiceControl => RCServiceControl.GetInstance();

        public RCServiceConfig RCServiceConfig { get; set; } = new RCServiceConfig();
        public bool IsUseRCService { get => _IsUseRCService; set { _IsUseRCService = value; NotifyPropertyChanged(); } }
        private bool _IsUseRCService = true;

        public ObservableCollection<RCServiceConfig> RCServiceConfigs { get; set; } = new ObservableCollection<RCServiceConfig>();

        public void Decrypt()
        {
            RCServiceConfig.AppSecret = Cryptography.AESDecrypt(RCServiceConfig.AppSecret, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
        }

        public void Encryption()
        {
            RCServiceConfig.AppSecret = Cryptography.AESEncrypt(RCServiceConfig.AppSecret, GlobalConst.ConfigAESKey, GlobalConst.ConfigAESVector);
        }
    }
}
