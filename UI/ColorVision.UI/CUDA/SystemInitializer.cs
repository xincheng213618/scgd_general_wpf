namespace ColorVision.UI.CUDA
{
    public class SystemInitializer : InitializerBase
    {
        public override int Order => 8;

        private readonly IMessageUpdater _messageUpdater;

        public SystemInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public override string Name => nameof(SystemInitializer);

        public override async Task InitializeAsync()
        {
            _messageUpdater.Update("Debug Mode: " + SystemHelper.IsDebugMode());
            _messageUpdater.Update("OS Version: " + SystemHelper.GetOSVersion());
            _messageUpdater.Update(".NET Version: " + SystemHelper.GetDotNetVersion());
            _messageUpdater.Update("Application Version: " + SystemHelper.GetApplicationVersion());
            _messageUpdater.Update("User Name: " + SystemHelper.GetUserName());
            _messageUpdater.Update("Machine Name: " + SystemHelper.GetMachineName());
            _messageUpdater.Update("Memory Info: " + SystemHelper.GetMemoryInfo());
            _messageUpdater.Update("System Language: " + SystemHelper.GetSystemLanguage());
            _messageUpdater.Update("Screen Resolution: " + SystemHelper.GetScreenResolution());
            _messageUpdater.Update("LocalCpuInfo: " + SystemHelper.LocalCpuInfo);

            


        }

    }
}
