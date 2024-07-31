using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using System.Runtime.InteropServices;

namespace ColorVision.UI.CUDA
{
    public class ConfigCuda:ViewModelBase,IConfig, IConfigSettingProvider
    {
        public static ConfigCuda Instance => ConfigHandler.GetInstance().GetRequiredService<ConfigCuda>();

        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; NotifyPropertyChanged(); } }
        private bool _IsEnabled = true;

        public bool IsCudaSupported { get => _IsCudaSupported; set { _IsCudaSupported = value; NotifyPropertyChanged(); } }
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

    public class CudaInitializer : IInitializer
    {
        [DllImport("nvcuda.dll")]
        private static extern int cuInit(uint Flags);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceGetCount(out int count);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceGetName(byte[] name, int len, int dev);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceComputeCapability(out int major, out int minor, int dev);

        [DllImport("nvcuda.dll")]
        private static extern int cuDeviceTotalMem(out ulong bytes, int dev);

        [DllImport("nvcuda.dll", EntryPoint = "cuDeviceTotalMem_v2")]
        private static extern int cuDeviceTotalMem_v2(out ulong bytes, int device);
        public static ConfigCuda Config => ConfigCuda.Instance;

        public int Order => 7;

        private readonly IMessageUpdater _messageUpdater;

        public CudaInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public async Task InitializeAsync()
        {
            Config.IsCudaSupported = CheckCudaSupport();
            if (Config.IsCudaSupported)
            {
                _messageUpdater.UpdateMessage("正在检测是否支持CUDA");

                Config.DeviceNames = new string[Config.DeviceCount];
                Config.ComputeCapabilities = new (int Major, int Minor)[Config.DeviceCount];
                Config.TotalMemories = new ulong[Config.DeviceCount];

                for (int i = 0; i < Config.DeviceCount; i++)
                {
                    // 获取设备名称
                    byte[] name = new byte[100];
                    cuDeviceGetName(name, name.Length, i);
                    Config.DeviceNames[i] = System.Text.Encoding.ASCII.GetString(name).TrimEnd('\0');

                    // 获取计算能力
                    cuDeviceComputeCapability(out int major, out int minor, i);
                    Config.ComputeCapabilities[i] = (major, minor);

                    // 获取总内存
                    cuDeviceTotalMem_v2(out ulong totalMem, i);
                    Config.TotalMemories[i] = totalMem;
                }

                if (Config.IsEnabled)
                {
                    for (int i = 0; i < Config.DeviceCount; i++)
                    {
                        await Task.Delay(10);
                        _messageUpdater.UpdateMessage($"Device {i}:");
                        _messageUpdater.UpdateMessage($"  Name: {Config.DeviceNames[i]}");
                        _messageUpdater.UpdateMessage($"  Compute Capability: {Config.ComputeCapabilities[i].Major}.{Config.ComputeCapabilities[i].Minor}");
                        _messageUpdater.UpdateMessage($"  Total Memory: {Config.TotalMemories[i] / (1024.0 * 1024.0 * 1024.0):F0} GB");
                    }
                    await Task.Delay(100);
                }
            }
            else
            {
                _messageUpdater.UpdateMessage("CUDA is either not supported or not enabled.");
            }

        }

        private static bool CheckCudaSupport()
        {
            try
            {
                int result = cuInit(0);
                if (result != 0)
                {
                    return false;
                }

                result = cuDeviceGetCount(out int deviceCount);
                if (result != 0 || deviceCount == 0)
                {
                    return false;
                }

                Config.DeviceCount = deviceCount;
                return true;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }
    }
}
