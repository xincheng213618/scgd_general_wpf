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
                    LocalizedText.Get("ConnectSourceMeter"),
                    LocalizedText.Get("ConnectSourceMeter"),
                    Brushes.Red,
                    contentFactory: _ => Manager.SmuController.ConnectButtonText,
                    tooltipFactory: stats => Manager.SmuController.IsOpen
                        ? LocalizedText.Get("DisconnectSourceMeter")
                        : TimedButtonOperationTextFormatter.BuildTooltip(LocalizedText.Get("ConnectSourceMeter"), stats),
                    minimumExpectedDurationMs: 2000);
            }

            if (!operations.Contains(SmuMeasureButton))
            {
                operations.Register(
                    SmuMeasureButton,
                    "smu-measure",
                    LocalizedText.Get("SmuMeasureOrSet"),
                    LocalizedText.Get("MeasureAndReadSourceMeter"),
                    Brushes.Red,
                    minimumExpectedDurationMs: 1000);
            }

            if (!operations.Contains(SmuCloseOutputButton))
            {
                operations.Register(
                    SmuCloseOutputButton,
                    "smu-close-output",
                    LocalizedText.Get("CloseOutput"),
                    LocalizedText.Get("CloseSourceMeterOutput"),
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
            Manager.MeasurementMode = eqeEnabled ? LocalizedText.Get("LuminousFluxMode") : LocalizedText.Get("BrightnessChromaticityMode");
        }

        private void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("SelectDataToRecalculate"), LocalizedText.Get("PromptTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: disconnecting ? LocalizedText.Get("DisconnectSourceMeter") : LocalizedText.Get("ConnectSourceMeter"));

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
                            ? LocalizedText.Get("SourceMeterConnectFailedCheckSettings")
                            : Manager.SmuController.LastErrorMessage;
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, LocalizedText.Get("ConnectionFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(disconnecting ? "SMU 断开失败" : "SMU 连接失败", ex);
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    ex.Message,
                    disconnecting ? LocalizedText.Get("DisconnectionFailedTitle") : LocalizedText.Get("ConnectionFailedTitle"),
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: LocalizedText.Get("SmuMeasureOrSet"));
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
                        ? LocalizedText.Get("SourceMeterReadFailed")
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, LocalizedText.Get("ReadFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 测量失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, LocalizedText.Get("ReadFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: LocalizedText.Get("CloseOutput"));
                success = Manager.SmuController.CloseOutput();
                if (success)
                {
                    log.Info("SMU 输出已关闭");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? LocalizedText.Get("SourceMeterCloseOutputFailed")
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, LocalizedText.Get("CloseOutputFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 关闭输出失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, LocalizedText.Get("CloseOutputFailedTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }
    }
}
