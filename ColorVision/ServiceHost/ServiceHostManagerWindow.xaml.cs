using System;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.Update.Export;
using log4net;
using AppResources = ColorVision.Properties.Resources;

namespace ColorVision.ServiceHost
{
    public partial class ServiceHostManagerWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceHostManagerWindow));

        public ServiceHostManagerWindow()
        {
            InitializeComponent();
            PathText.Text = $"Package: {ServiceHostProtocol.PackageExecutablePath}{Environment.NewLine}Installed: {ServiceHostProtocol.InstalledExecutablePath}";
            Loaded += async (_, _) => await RefreshStatusAsync().ConfigureAwait(true);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStatusAsync().ConfigureAwait(true);
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            await RunOperationAsync("Install", ColorVisionServiceHostManager.InstallAsync).ConfigureAwait(true);
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(this, "Uninstall ColorVisionServiceHost?", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            await RunOperationAsync("Uninstall", ColorVisionServiceHostManager.UninstallAsync).ConfigureAwait(true);
        }

        private async void PingButton_Click(object sender, RoutedEventArgs e)
        {
            await RunPipeCommandAsync("ping").ConfigureAwait(true);
        }

        private async void FileAssociationButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            AppendLog("> file association");
            try
            {
                bool success = await FileAssociationHelper.RegisterAssociationsAsync().ConfigureAwait(true);
                AppendLog(success ? "OK: file association registered" : "FAILED: file association registration failed");
                MessageBox.Show(this,
                    success ? AppResources.RegistryAppliedSuccess : AppResources.ComRegistrationFailed,
                    "ColorVision",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                log.Error("File association registration failed.", ex);
                AppendLog(ex.Message);
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void RegisterThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            AppendLog("> register thumbnail");
            try
            {
                await ThumbnailServiceHostCommands.ExecuteAsync(
                    "register-thumbnail",
                    AppResources.ThumbnailRegistrationSuccess,
                    AppResources.RegistrationFailed,
                    log).ConfigureAwait(true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void UnregisterThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            SetBusy(true);
            AppendLog("> unregister thumbnail");
            try
            {
                await ThumbnailServiceHostCommands.ExecuteAsync(
                    "unregister-thumbnail",
                    AppResources.ThumbnailUnregistered,
                    AppResources.UnregistrationFailed,
                    log).ConfigureAwait(true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task RunOperationAsync(string name, Func<System.Threading.CancellationToken, Task<ServiceHostOperationResult>> operation)
        {
            SetBusy(true);
            AppendLog($"> {name}");
            try
            {
                ServiceHostOperationResult result = await operation(System.Threading.CancellationToken.None).ConfigureAwait(true);
                AppendLog(result.Summary);
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
            finally
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                SetBusy(false);
            }
        }

        private async Task RunPipeCommandAsync(string command)
        {
            SetBusy(true);
            AppendLog($"> pipe {command}");
            try
            {
                ServiceHostResponse response = await ServiceHostPipeClient.SendAsync(command, TimeSpan.FromSeconds(3)).ConfigureAwait(true);
                AppendLog(response.ToDisplayText());
            }
            catch (Exception ex)
            {
                AppendLog(ex.Message);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task RefreshStatusAsync()
        {
            try
            {
                ServiceHostStatus status = await ColorVisionServiceHostManager.QueryStatusAsync().ConfigureAwait(true);
                StatusText.Text = $"Status: {status.DisplayText}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Status: unknown";
                AppendLog(ex.Message);
            }
        }

        private void SetBusy(bool busy)
        {
            RefreshButton.IsEnabled = !busy;
            InstallButton.IsEnabled = !busy;
            UninstallButton.IsEnabled = !busy;
            PingButton.IsEnabled = !busy;
            FileAssociationButton.IsEnabled = !busy;
            RegisterThumbnailButton.IsEnabled = !busy;
            UnregisterThumbnailButton.IsEnabled = !busy;
        }

        private void AppendLog(string message)
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            LogBox.ScrollToEnd();
        }
    }
}
