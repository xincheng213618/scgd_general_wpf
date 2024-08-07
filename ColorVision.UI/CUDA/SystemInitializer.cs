using System.Runtime.InteropServices;

namespace ColorVision.UI.CUDA
{
    public class SystemInitializer : IInitializer
    {
        public int Order => 8;

        private readonly IMessageUpdater _messageUpdater;

        public SystemInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }



        public async Task InitializeAsync()
        {
            _messageUpdater.UpdateMessage("Debug Mode: " + SystemHelper.IsDebugMode());
            _messageUpdater.UpdateMessage("OS Version: " + SystemHelper.GetOSVersion());
            _messageUpdater.UpdateMessage(".NET Version: " + SystemHelper.GetDotNetVersion());
            _messageUpdater.UpdateMessage("Application Version: " + SystemHelper.GetApplicationVersion());
            _messageUpdater.UpdateMessage("User Name: " + SystemHelper.GetUserName());
            _messageUpdater.UpdateMessage("Machine Name: " + SystemHelper.GetMachineName());
            _messageUpdater.UpdateMessage("Memory Info: " + SystemHelper.GetMemoryInfo());
            _messageUpdater.UpdateMessage("System Language: " + SystemHelper.GetSystemLanguage());
            _messageUpdater.UpdateMessage("Screen Resolution: " + SystemHelper.GetScreenResolution());
            await Task.Delay(1);

        }

    }
}
