using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Settings;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Services.RC
{
    public delegate void UseRcServicelHandler(bool IsUseRcServicel);

    public class RCSettingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "启用RC",
                                Description = "启用RC",
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(RCSetting.IsUseRCService),
                                Source = RCSetting.Instance
                            },
                            new ConfigSettingMetadata
                            {
                                Name = "打开CVWinSMS",
                                Description = "在软件启动时，如果未打开RC,则打开RC",
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(RCManagerConfig.IsOpenCVWinSMS),
                                Source = RCManagerConfig.Instance
                            }
            };
        }
    }


    public class RCSetting : ViewModelBase, IConfigSecure
    {
        public static RCSetting Instance => ConfigHandler.GetInstance().GetRequiredService<RCSetting>();

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
