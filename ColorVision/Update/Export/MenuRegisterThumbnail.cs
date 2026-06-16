#pragma warning disable CA1863
using ColorVision.Properties;
using ColorVision.ServiceHost;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update.Export
{
    public class MenuRegisterThumbnail : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1001;
        public override string Header => Resources.MenuRegisterThumbnail;
        public override Visibility Visibility => Visibility.Collapsed;

        private static readonly ILog log = LogManager.GetLogger(typeof(MenuRegisterThumbnail));

        public override async void Execute()
        {
            await ThumbnailServiceHostCommands.ExecuteAsync(
                "register-thumbnail",
                Resources.ThumbnailRegistrationSuccess,
                Resources.RegistrationFailed,
                log).ConfigureAwait(true);
        }
    }

    public class MenuUnregisterThumbnail : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1002;
        public override string Header => Resources.MenuUnregisterThumbnail;
        public override Visibility Visibility => Visibility.Collapsed;

        private static readonly ILog log = LogManager.GetLogger(typeof(MenuUnregisterThumbnail));

        public override async void Execute()
        {
            await ThumbnailServiceHostCommands.ExecuteAsync(
                "unregister-thumbnail",
                Resources.ThumbnailUnregistered,
                Resources.UnregistrationFailed,
                log).ConfigureAwait(true);
        }
    }

    internal static class ThumbnailServiceHostCommands
    {
        public static async Task ExecuteAsync(string command, string successMessage, string failureFormat, ILog log)
        {
            try
            {
                string appPath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to resolve executable path.");
                string appDirectory = Path.GetDirectoryName(appPath) ?? throw new InvalidOperationException("Unable to resolve executable directory.");
                string comHostDll = Path.Combine(appDirectory, "ColorVision.ShellExtension.comhost.dll");

                if (!File.Exists(comHostDll))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(),
                        string.Format(Resources.ShellExtensionNotFound, comHostDll),
                        "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string thumbnailCacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");

                ServiceHostResponse response = await ServiceHostPipeClient.SendAsync(
                    command,
                    new { appDirectory, thumbnailCacheDirectory },
                    TimeSpan.FromSeconds(30)).ConfigureAwait(true);

                if (response.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(),
                        successMessage,
                        "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                log.Warn($"{command} failed: {response.ToDisplayText()}");
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    string.Format(failureFormat, response.Message),
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error($"{command} failed.", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    string.Format(failureFormat, ex.Message),
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
