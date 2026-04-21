using Spectrum.Data;
using Spectrum.Models;
using System.Threading.Tasks;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        internal void UpdateEqeColumnsVisibility(bool eqeEnabled)
        {
            if (!IsInitialized) return;

            EqePanel.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
            EqeGroupBox.Visibility = eqeEnabled ? Visibility.Visible : Visibility.Collapsed;
            // double.NaN = auto-size (visible), 0 = hidden
            double width = eqeEnabled ? double.NaN : 0;
            ColEqe.Width = width;
            ColLuminousFlux.Width = width;
            ColRadiantFlux.Width = width;
            ColLuminousEfficacy.Width = width;
            ColVoltage.Width = width;
            ColCurrent.Width = width;
            ColRecalculated.Width = width;
            // Hide brightness column in 光通量模式
            ColBrightness.Width = eqeEnabled ? 0 : double.NaN;
            // Update measurement mode
            Manager.MeasurementMode = eqeEnabled ? "光通量模式" : "亮色度模式";
        }

        private void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先选择要重新计算的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selectedItems)
            {
                item.CalculateEqeParams(voltage, currentMA);
                item.IsRecalculated = true;
                ViewResultManager.UpdateEqeFields(item, isRecalculated: true);
            }
        }

        private async void SmuConnect_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.SmuController.IsBusy) return;

            if (Manager.SmuController.IsOpen)
            {
                await Manager.SmuController.CloseAsync();
                log.Info("SMU 已断开");
            }
            else
            {
                bool ok = await Manager.SmuController.OpenAsync();
                if (ok)
                {
                    log.Info($"SMU 连接成功: {Manager.SmuController.Version}");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? "源表连接失败，请检查设备名称和连接方式"
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void SmuMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (!Manager.SmuController.IsOpen || Manager.SmuController.IsBusy) return;

            bool ok = await Manager.SmuController.MeasureAndApplyAsync();
            if (ok)
            {
                var (voltage, currentMA) = Manager.SmuController.GetVI();
                MainWindowConfig.Instance.EqeVoltage = voltage;
                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                log.Debug($"SMU 测量结果: V={voltage}, I={currentMA}mA");
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                    ? "源表读取失败"
                    : Manager.SmuController.LastErrorMessage;
                MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "读取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SmuCloseOutput_Click(object sender, RoutedEventArgs e)
        {
            if (!Manager.SmuController.CanCloseOutput) return;

            Manager.SmuController.CloseOutput();
            log.Info("SMU 输出已关闭");
        }
    }
}
