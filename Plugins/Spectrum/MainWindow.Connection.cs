#pragma warning disable CA1822,CA1863
using cvColorVision;
using Spectrum.License;
using SpectrumResources = Spectrum.Properties.Resources;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        public IntPtr SpectrometerHandle => Manager.Handle;

        //连接光谱仪
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int result = await Manager.ConnectAsync();
                if (result == 1)
                {
                    button3.IsEnabled = true;
                    button5.IsEnabled = true;
                    button6.IsEnabled = true;
                }
                else if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox.Show("光谱仪驱动当前不可用。请先关闭直连诊断窗口；若刚才释放失败，请重启程序。");
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(result);
                    log.Error($"光谱仪连接失败: {errorMsg}");
                    await CheckDeviceAndPromptLicenseAsync(errorMsg);
                }
            }
            catch (Exception ex)
            {
                log.Error("光谱仪连接异常", ex);
                MessageBox.Show(ex.Message);
            }
        }

        //断开连接
        private async void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int result = await Manager.DisconnectAsync();
                if (result != 1)
                {
                    log.Warn($"断开光谱仪时原生接口返回错误: {Spectrometer.GetErrorMessage(result)}");
                }
            }
            catch (Exception ex)
            {
                log.Error("断开光谱仪异常", ex);
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// On connection failure, detect if a device exists via CM_Emission_GetAllSN.
        /// If exactly one device is found, it's likely a license issue - open the license manager.
        /// </summary>
        private async Task CheckDeviceAndPromptLicenseAsync(string errorMsg)
        {
            try
            {
                string? serialNumber = await Task.Run(() => Manager.FindSingleDetectedSerialNumber());
                if (!string.IsNullOrEmpty(serialNumber))
                {
                    log.Info($"检测到设备 {serialNumber}，连接失败可能是许可证问题");
                    var msgResult = MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(SpectrumResources.ConnectionFailedWithDeviceDetected, errorMsg, serialNumber),
                        SpectrumResources.ConnectionFailedLicenseCheckTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (msgResult == MessageBoxResult.Yes)
                    {
                        new LicenseManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"设备检测失败: {ex.Message}");
            }

            // Default: just show the error message
            MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ConnectionFailedWithError, errorMsg));
        }
    }
}
