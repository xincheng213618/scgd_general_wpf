using Spectrum.Data;
using Spectrum.Models;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        private void SpectrumMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isLuminousFlux = RadioLuminousFluxMode.IsChecked == true;
            MainWindowConfig.Instance.EqeEnabled = isLuminousFlux;
            UpdateEqeColumnsVisibility(isLuminousFlux);
        }

        private void UpdateEqeColumnsVisibility(bool eqeEnabled)
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

        private void SmuConnect_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.SmuController.IsOpen)
            {
                Manager.SmuController.Close();
                ButtonSmuConnect.Content = "连接源表";
                ButtonSmuMeasure.IsEnabled = false;
                log.Info("SMU 已断开");
            }
            else
            {
                bool ok = Manager.SmuController.Open();
                if (ok)
                {
                    ButtonSmuConnect.Content = "断开源表";
                    ButtonSmuMeasure.IsEnabled = true;
                    log.Info($"SMU 连接成功: {Manager.SmuController.Version}");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "源表连接失败，请检查设备名称和连接方式", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void SmuMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (!Manager.SmuController.IsOpen) return;
            Manager.SmuController.ApplySettings();
            bool ok = Manager.SmuController.MeasureData();
            if (ok)
            {
                var (voltage, currentMA) = Manager.SmuController.GetVI();
                MainWindowConfig.Instance.EqeVoltage = voltage;
                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                log.Debug($"SMU 测量结果: V={voltage}, I={currentMA}mA");
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "源表读取失败", "读取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
