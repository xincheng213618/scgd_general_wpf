using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Configs;
using System.Collections.Generic;

namespace ColorVision.Services
{
    public class ServicesConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = "IsDefaultOpenService",
                    Description =  "IsDefaultOpenService",
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(ServicesConfig.IsDefaultOpenService),
                    Source = ServicesConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Name = "IsRetorePlayControls",
                    Description =  "IsRetorePlayControls",
                    Order = 16,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(ServicesConfig.IsRetorePlayControls),
                    Source = ServicesConfig.Instance,
                }
            };
        }
    }


    public class ServicesConfig : ViewModelBase, IConfig
    {
        public static ServicesConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ServicesConfig>();

        public int ShowType { get; set; }

        public bool IsDefaultOpenService { get => _IsDefaultOpenService; set { _IsDefaultOpenService = value; NotifyPropertyChanged(); } }
        private bool _IsDefaultOpenService;

        public bool IsRetorePlayControls { get => _IsRetorePlayControls; set { _IsRetorePlayControls = value; NotifyPropertyChanged(); } }
        private bool _IsRetorePlayControls = true;

    }
}
