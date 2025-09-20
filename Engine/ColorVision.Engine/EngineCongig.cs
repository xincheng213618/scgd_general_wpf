using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;

namespace ColorVision.Engine
{
    public class EngineCongig : ViewModelBase, IConfig, IConfigSettingProvider
    {
        public static EngineCongig Instance { get; set; } = ConfigService.Instance.GetRequiredService<EngineCongig>();

        /// <summary>
        /// 超能模式 - 对于某些需要使用服务的功能，本地化实现
        /// </summary>
        public bool SuperMode { get => _SuperMode; set { _SuperMode = value; OnPropertyChanged(); } }
        private bool _SuperMode = true;


        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = "SuperMode",
                    Description = "超能模式 - 对于某些需要使用服务的功能，本地化实现",
                    Order =1,
                    Type = ConfigSettingType.Bool,
                    Group ="Engine",
                    BindingName = nameof(SuperMode),
                    Source = Instance
                }
            };
        }

    }
}
