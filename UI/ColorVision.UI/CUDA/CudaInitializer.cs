#pragma warning disable
using ColorVision.Common.NativeMethods;
using log4net;
using System.Runtime.InteropServices;

namespace ColorVision.UI.CUDA
{
    public class CudaInitializer : InitializerBase
    {

        public static ConfigCuda Config => ConfigCuda.Instance;

        public override int Order => 7;
        public override string Name => nameof(CudaInitializer);

        private static readonly ILog log = LogManager.GetLogger(typeof(CudaInitializer));

        public CudaInitializer() { }  

        public override async Task InitializeAsync()
        {
            await Task.Delay(0);
            Config.IsCudaSupported = CheckCudaSupport();
            if (Config.IsCudaSupported)
            {
                log.Info(Properties.Resources.CheckingCUDASupport);

                Config.DeviceNames = new string[Config.DeviceCount];
                Config.ComputeCapabilities = new (int Major, int Minor)[Config.DeviceCount];
                Config.TotalMemories = new ulong[Config.DeviceCount];

                for (int i = 0; i < Config.DeviceCount; i++)
                {
                    byte[] name = new byte[100];
                    Nvcuda.cuDeviceGetName(name, name.Length, i);
                    Config.DeviceNames[i] = System.Text.Encoding.ASCII.GetString(name).TrimEnd('\0');

                    Nvcuda.cuDeviceComputeCapability(out int major, out int minor, i);
                    Config.ComputeCapabilities[i] = (major, minor);

                    Nvcuda.cuDeviceTotalMem_v2(out ulong totalMem, i);
                    Config.TotalMemories[i] = totalMem;
                }

                if (Config.IsEnabled)
                {
                    for (int i = 0; i < Config.DeviceCount; i++)
                    {
                        log.Info($"Device {i}:");
                        log.Info($"Name: {Config.DeviceNames[i]}");
                        log.Info($"Compute Capability: {Config.ComputeCapabilities[i].Major}.{Config.ComputeCapabilities[i].Minor}");
                        log.Info($"Total Memory: {Config.TotalMemories[i] / (1024.0 * 1024.0 * 1024.0):F0} GB");
                    }
                }
            }
            else
            {
                log.Info("CUDA is either not supported or not enabled.");
            }

        }

        private static bool CheckCudaSupport()
        {
            try
            {
                int result = Nvcuda.cuInit(0);
                if (result != 0)
                {
                    return false;
                }

                result = Nvcuda.cuDeviceGetCount(out int deviceCount);
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
