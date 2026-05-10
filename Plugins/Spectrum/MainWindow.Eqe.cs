using ColorVision.UI;
using Spectrum.Configs;
using Spectrum.Data;
using Spectrum.Models;
using Spectrum.TimedButtons;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Spectrum
{
    public partial class MainWindow
    {
        private bool _smuTimedButtonsInitialized;

        private TimedButtonOperationRegistry EnsureSmuTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(SpectrumTimedButtonHost.BuildOperationKey);

            if (!operations.Contains(SmuConnectButton))
            {
                operations.Register(
                    SmuConnectButton,
                    "smu-connect-toggle",
                    "连接源表",
                    "连接源表",
                    Brushes.Red,
                    contentFactory: _ => Manager.SmuController.ConnectButtonText,
                    tooltipFactory: stats => Manager.SmuController.IsOpen
                        ? "断开源表"
                        : TimedButtonOperationTextFormatter.BuildTooltip("连接源表", stats),
                    minimumExpectedDurationMs: 2000);
            }

            if (!operations.Contains(SmuMeasureButton))
            {
                operations.Register(
                    SmuMeasureButton,
                    "smu-measure",
                    "点亮/设置",
                    "点亮并读取源表",
                    Brushes.Red,
                    minimumExpectedDurationMs: 1000);
            }

            if (!operations.Contains(SmuCloseOutputButton))
            {
                operations.Register(
                    SmuCloseOutputButton,
                    "smu-close-output",
                    "关闭输出",
                    "关闭源表输出",
                    Brushes.Red,
                    minimumExpectedDurationMs: 600);
            }

            return operations;
        }

        internal void InitializeSmuTimedButtons()
        {
            EnsureSmuTimedButtonOperations();

            if (_smuTimedButtonsInitialized)
            {
                return;
            }

            Manager.SmuController.PropertyChanged += SmuController_PropertyChanged;
            _smuTimedButtonsInitialized = true;
        }

        internal void CleanupSmuTimedButtons()
        {
            if (_smuTimedButtonsInitialized)
            {
                Manager.SmuController.PropertyChanged -= SmuController_PropertyChanged;
                _smuTimedButtonsInitialized = false;
            }

            this.DisposeTimedButtonOperations();
        }

        private void SmuController_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SmuController.IsOpen)
                && e.PropertyName != nameof(SmuController.ConnectButtonText))
            {
                return;
            }

            void RefreshConnectButton()
            {
                this.TryGetTimedButtonOperations()?.RefreshIdleState(SmuConnectButton);
            }

            if (Dispatcher.CheckAccess())
            {
                RefreshConnectButton();
                return;
            }

            Dispatcher.BeginInvoke(RefreshConnectButton);
        }

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
            if (sender is not Button button || Manager.SmuController.IsBusy) return;

            bool disconnecting = Manager.SmuController.IsOpen;
            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: disconnecting ? "断开源表" : "连接源表");

                if (disconnecting)
                {
                    await Manager.SmuController.CloseAsync();
                    success = !Manager.SmuController.IsOpen;
                    if (success)
                    {
                        log.Info("SMU 已断开");
                    }
                }
                else
                {
                    success = await Manager.SmuController.OpenAsync();
                    if (success)
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
            catch (Exception ex)
            {
                log.Error(disconnecting ? "SMU 断开失败" : "SMU 连接失败", ex);
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    ex.Message,
                    disconnecting ? "断开失败" : "连接失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
                this.TryGetTimedButtonOperations()?.RefreshIdleState(SmuConnectButton);
            }
        }

        private async void SmuMeasure_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || !Manager.SmuController.IsOpen || Manager.SmuController.IsBusy) return;

            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: "点亮/设置");
                success = await Manager.SmuController.MeasureAndApplyAsync();
                if (success)
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
            catch (Exception ex)
            {
                log.Error("SMU 测量失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "读取失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }

        private void SmuCloseOutput_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || !Manager.SmuController.CanCloseOutput) return;

            TimedButtonOperationScope? operationScope = null;
            bool success = false;

            try
            {
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: "关闭输出");
                success = Manager.SmuController.CloseOutput();
                if (success)
                {
                    log.Info("SMU 输出已关闭");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? "关闭源表输出失败"
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, "关闭输出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 关闭输出失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "关闭输出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }
    }
}
