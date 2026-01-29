using ColorVision.UI.CUDA;
using log4net;

namespace ColorVision.UI.Desktop.CUDA
{
    public class SystemInitializer : InitializerBase
    {
        public override int Order => 8;

        private static readonly ILog log = LogManager.GetLogger(typeof(SystemInitializer));

        public SystemInitializer() { }

        public override string Name => nameof(SystemInitializer);

        public override async Task InitializeAsync()
        {
            await Task.Delay(1);
            log.Info("Debug Mode: " + SystemHelper.IsDebugMode());
            log.Info("OS Version: " + SystemHelper.GetOSVersion());
            log.Info(".NET Version: " + SystemHelper.GetDotNetVersion());
            log.Info("Application Version: " + SystemHelper.GetApplicationVersion());
            log.Info("User Name: " + SystemHelper.GetUserName());
            log.Info("Machine Name: " + SystemHelper.GetMachineName());
            log.Info("Memory Info: " + SystemHelper.GetMemoryInfo());
            log.Info("System Language: " + SystemHelper.GetSystemLanguage());
            log.Info("Screen Resolution: " + SystemHelper.GetScreenResolution());
            log.Info("LocalCpuInfo: " + SystemHelper.LocalCpuInfo);
        }

    }
}
