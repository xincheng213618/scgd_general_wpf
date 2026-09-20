using ColorVision.Common.ThirdPartyApps;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Themes;
using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ColorVision.ToolPlugins.CameraDriver
{
    public sealed class CameraDriverAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            yield return new ThirdPartyAppInfo
            {
                Name = CameraDriverText.Get("Header"),
                Group = ThirdPartyAppGroupNames.CommonTools,
                Category = ThirdPartyAppCategory.Internal,
                RequiredPermission = PermissionMode.Guest,
                Order = -895,
                IconGlyph = "\uE722",
                LaunchAction = CameraDriverWindow.ShowWindow
            };
        }
    }

    public partial class CameraDriverWindow : Window
    {
        private readonly CancellationTokenSource _lifetime = new();
        private readonly CameraDriverWindowModel _model;

        public CameraDriverWindow() : this(new(new CameraDriverInstallationService(), new CameraDriverPackageDownloader(
                () => AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault()
                    ?? throw new InvalidOperationException(CameraDriverText.Get("DownloadUnavailable")),
                Environments.DirToolPackageCache, () => DownloadFileConfig.Instance.Authorization))) { }

        internal CameraDriverWindow(CameraDriverWindowModel model)
        {
            _model = model;
            InitializeComponent();
            DataContext = _model;
            this.ApplyCaption();
        }

        public static void ShowWindow()
        {
            CameraDriverWindow? existing = Application.Current.Windows.OfType<CameraDriverWindow>().FirstOrDefault();
            if (existing != null)
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }
            new CameraDriverWindow { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await _model.RefreshAsync(_lifetime.Token);
        private async void Refresh_Click(object sender, RoutedEventArgs e) => await _model.RefreshAsync(_lifetime.Token);
        private async void Install_Click(object sender, RoutedEventArgs e) => await _model.InstallAsync(_lifetime.Token);
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Window_Closed(object? sender, EventArgs e) { _lifetime.Cancel(); _lifetime.Dispose(); }

        private void DownloadPage_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(CameraDriverInstallationService.DownloadPage) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
