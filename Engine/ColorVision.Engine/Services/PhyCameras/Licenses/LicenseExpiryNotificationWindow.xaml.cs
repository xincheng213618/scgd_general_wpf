using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    public class LicenseExpiryInfo
    {
        public PhyCamera Camera { get; set; }
        public string CameraName { get; set; }
        public string StatusMessage { get; set; }
        public string LicenseInfo { get; set; }
        public Brush StatusColor { get; set; }
        public Brush StatusTextColor { get; set; }
        public bool IsExpired { get; set; }
        public int DaysRemaining { get; set; }
    }

    /// <summary>
    /// LicenseExpiryNotificationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LicenseExpiryNotificationWindow : Window
    {
        private List<LicenseExpiryInfo> _licenseInfos;
        private PhyCamera _firstCamera;

        public bool DontShowAgain { get; private set; }

        public LicenseExpiryNotificationWindow(List<LicenseExpiryInfo> licenseInfos)
        {
            InitializeComponent();
            this.ApplyCaption();

            _licenseInfos = licenseInfos ?? new List<LicenseExpiryInfo>();
            _firstCamera = _licenseInfos.FirstOrDefault()?.Camera;

            InitializeUI();
        }

        private void InitializeUI()
        {
            // Set summary text
            int expiredCount = _licenseInfos.Count(li => li.IsExpired);
            int expiringCount = _licenseInfos.Count(li => !li.IsExpired);

            if (expiredCount > 0 && expiringCount > 0)
            {
                TextBlockSummary.Text = $"检测到 {expiredCount} 个相机许可证已过期，{expiringCount} 个相机许可证即将过期";
            }
            else if (expiredCount > 0)
            {
                TextBlockSummary.Text = $"检测到 {expiredCount} 个相机许可证已过期";
            }
            else if (expiringCount > 0)
            {
                TextBlockSummary.Text = $"检测到 {expiringCount} 个相机许可证即将过期";
            }

            // Bind camera list
            CameraListControl.ItemsSource = _licenseInfos;
        }

        private void ButtonGoToManager_Click(object sender, RoutedEventArgs e)
        {
            DontShowAgain = CheckBoxDontShowAgain.IsChecked == true;

            // Close this window
            this.DialogResult = true;
            this.Close();

            // Open PhyCameraManagerWindow
            Application.Current.Dispatcher.Invoke(() =>
            {
                var managerWindow = new PhyCameraManagerWindow
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                // Show the window
                managerWindow.Show();

                // Select the first camera with license issue
                if (_firstCamera != null && managerWindow.IsLoaded)
                {
                    // Try to select the camera in the tree view
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // Set the camera as selected
                            foreach (var camera in PhyCameraManager.GetInstance().PhyCameras)
                            {
                                camera.IsSelected = false;
                            }
                            _firstCamera.IsSelected = true;

                            // Trigger the selection changed event by updating the TreeView
                            var treeView = managerWindow.TreeView1;
                            if (treeView != null)
                            {
                                treeView.UpdateLayout();
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore errors in selection
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            });
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            DontShowAgain = CheckBoxDontShowAgain.IsChecked == true;
            this.DialogResult = false;
            this.Close();
        }
    }
}
