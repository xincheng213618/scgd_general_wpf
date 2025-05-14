using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Engine.Services
{
    public class ServicesConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        { 
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name =   Properties.Resources.IsDefaultOpenService,
                    Description =  Properties.Resources.IsDefaultOpenService,
                    Group ="Engine",
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(ServicesConfig.IsAutoConfig),
                    Source = ServicesConfig.Instance,
                },
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.IsRetorePlayControls,
                    Description = Properties.Resources.IsRetorePlayControls,
                    Group ="Engine",
                    Order = 16,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(DisPlayManagerConfig.IsRetore),
                    Source = DisPlayManagerConfig.Instance,
                }
            };
        }
    }


    public class ServicesConfig : ViewModelBase, IConfig
    {
        public static ServicesConfig Instance => ConfigService.Instance.GetRequiredService<ServicesConfig>();

        public int ShowType { get; set; }

        public bool IsAutoConfig { get => _IsAutoConfig; set { _IsAutoConfig = value; NotifyPropertyChanged(); } }
        private bool _IsAutoConfig = true;

    }
}
