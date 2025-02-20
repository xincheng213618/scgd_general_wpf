using ColorVision.Common.MVVM;

namespace ColorVision.UI.CUDA
{

    public class ConfigCuda:ViewModelBase,IConfig, IConfigSettingProvider
    {
        public static ConfigCuda Instance => ConfigService.Instance.GetRequiredService<ConfigCuda>();

        public bool IsEnabled { get => _IsEnabled; set { if (!IsCudaSupported) return;  _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled = true;

        public bool IsCudaSupported { get => _IsCudaSupported; set { _IsCudaSupported = value; NotifyPropertyChanged(); if (!value) IsEnabled = false; } }
        private bool _IsCudaSupported;

        public int DeviceCount { get => _DeviceCount; set { _DeviceCount = value; NotifyPropertyChanged(); } }
        private int _DeviceCount;

        public string[] DeviceNames { get => _DeviceNames; set { _DeviceNames = value; NotifyPropertyChanged(); } }
        private string[] _DeviceNames;
        public (int Major, int Minor)[] ComputeCapabilities { get => _ComputeCapabilities; set { _ComputeCapabilities = value; NotifyPropertyChanged(); } }
        private (int Major, int Minor)[] _ComputeCapabilities;

        public ulong[] TotalMemories { get => _TotalMemories; set { _TotalMemories = value; NotifyPropertyChanged(); } }
        private ulong[] _TotalMemories;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            List<ConfigSettingMetadata> configSettingMetadatas = new List<ConfigSettingMetadata>();
            if (Instance.IsCudaSupported)
            {
                ConfigSettingMetadata configSettingMetadata = new ConfigSettingMetadata()
                {
                    Type = ConfigSettingType.Bool,
                    Order = 1,
                    Name = "CUDA",
                    Description = "是否启用CUDA",
                    BindingName = nameof(IsEnabled),
                    Source = Instance
                };

                configSettingMetadatas.Add(configSettingMetadata);
            }
            return configSettingMetadatas;
        }
    }
}
