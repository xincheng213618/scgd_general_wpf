using ColorVision.UI;
using Spectrum.Configs;
using Spectrum.Data;
using Spectrum.Models;
using SpectrumResources = Spectrum.Properties.Resources;
using Spectrum.TimedButtons;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum
{
    public partial class MainWindow
    {
        private bool _smuTimedButtonsInitialized;

        private TimedButtonOperationRegistry EnsureSmuTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(SpectrumTimedButtonHost.BuildOperationKey);

            operations.Register(SmuConnectButton, options =>
            {
                options.ContentFactory = _ => Manager.SmuController.ConnectButtonText;
                options.ToolTipFactory = stats => Manager.SmuController.IsOpen
                    ? SpectrumResources.DisconnectSourceMeter
                    : TimedButtonOperationTextFormatter.BuildTooltip(SpectrumResources.ConnectSourceMeter, stats);
                options.MinimumExpectedDurationMs = 2000;
            });

            operations.Register(SmuMeasureButton, options =>
            {
                options.MinimumExpectedDurationMs = 1000;
            });

            operations.Register(SmuCloseOutputButton, options =>
            {
                options.MinimumExpectedDurationMs = 600;
            });

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
            Manager.MeasurementMode = eqeEnabled ? SpectrumResources.LuminousFluxMode : SpectrumResources.BrightnessChromaticityMode;
        }

        private void CalculateEqe_Click(object sender, RoutedEventArgs e)
        {
            float voltage = MainWindowConfig.Instance.EqeVoltage;
            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;

            var selectedItems = ViewResultList.SelectedItems.Cast<ViewResultSpectrum>().ToList();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.SelectDataToRecalculate, SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Information);
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: disconnecting ? SpectrumResources.DisconnectSourceMeter : SpectrumResources.ConnectSourceMeter);

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
                            ? SpectrumResources.SourceMeterConnectFailedCheckSettings
                            : Manager.SmuController.LastErrorMessage;
                        MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.ConnectionFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(disconnecting ? "SMU 断开失败" : "SMU 连接失败", ex);
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    ex.Message,
                    disconnecting ? SpectrumResources.DisconnectionFailedTitle : SpectrumResources.ConnectionFailedTitle,
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: SpectrumResources.SmuMeasureOrSet);
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
                        ? SpectrumResources.SourceMeterReadFailed
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.ReadFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 测量失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, SpectrumResources.ReadFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                operationScope = EnsureSmuTimedButtonOperations().Begin(button, runningText: SpectrumResources.CloseOutput);
                success = Manager.SmuController.CloseOutput();
                if (success)
                {
                    log.Info("SMU 输出已关闭");
                }
                else
                {
                    string errorMessage = string.IsNullOrWhiteSpace(Manager.SmuController.LastErrorMessage)
                        ? SpectrumResources.SourceMeterCloseOutputFailed
                        : Manager.SmuController.LastErrorMessage;
                    MessageBox.Show(Application.Current.GetActiveWindow(), errorMessage, SpectrumResources.CloseOutputFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("SMU 关闭输出失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, SpectrumResources.CloseOutputFailedTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                operationScope?.Complete(success);
            }
        }
    }
}
