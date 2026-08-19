#pragma warning disable CA1863
using ColorVision.Themes.Controls;
using Spectrum.Update;
using SpectrumResources = Spectrum.Properties.Resources;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        private CancellationTokenSource? continuousMeasurementCancellation;
        private Task? continuousMeasurementTask;
        private int continuousFailureCount;

        internal bool CanInstallUpdate(out string reason)
        {
            if (Manager.IsBusy || continuousMeasurementTask is { IsCompleted: false })
            {
                reason = UpdateText.Get("UpdateDeferredMeasurementBusy", "测量或设备操作正在进行，更新已安全延后。请停止测量后重试安装。");
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private async void AutoIntTime_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox1.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                (bool entered, float? integrationTime) = await Manager.TryGetAutoIntegrationTimeAsync();
                if (!entered)
                {
                    MessageBox1.Show(SpectrumResources.OperationInProgressPleaseWait);
                    return;
                }

                if (integrationTime.HasValue)
                {
                    Manager.IntTime = integrationTime.Value;
                    log.Info($"自动积分时间获取成功: {integrationTime.Value}ms");
                }
                else
                {
                    MessageBox1.Show("自动积分时间获取失败，请查看日志。");
                }
            }
            catch (Exception ex)
            {
                log.Error("自动积分时间异常", ex);
                MessageBox1.Show(ex.GetBaseException().Message);
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                int result = await Manager.PerformDarkCalibrationAsync();
                if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumResources.OperationInProgressPleaseWait);
                }
                else if (result == 1)
                {
                    log.Info("校零成功");
                    MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.ZeroCalibrationSuccess);
                }
                else
                {
                    string errorMessage = cvColorVision.Spectrometer.GetErrorMessage(result);
                    log.Error($"校零失败: {errorMessage}");
                    MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationFailed, errorMessage));
                }
            }
            catch (Exception ex)
            {
                log.Error("校零异常", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationException, ex.Message));
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                (string Path, string Sha256)? magnitudeSnapshot = null;
                try
                {
                    magnitudeSnapshot = CaptureMagnitudeFileSnapshot();
                }
                catch (Exception ex)
                {
                    log.Warn("无法记录本次测量使用的幅值 DAT，结果不会用于光谱校正。", ex);
                }

                SpectrumMeasurementResult result = await Manager.MeasureAsync();
                if (!result.IsSuccess)
                    ShowMeasurementFailure(result);
                else
                    TrackCorrectionMeasurementResult(result, magnitudeSnapshot);
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private void SetOperationButtonsEnabled(bool enabled)
        {
            void ApplyState()
            {
                button3.IsEnabled = enabled;
                button5.IsEnabled = enabled;
                button6.IsEnabled = enabled;
                ButtonAutoInt.IsEnabled = enabled;
            }

            if (Dispatcher.CheckAccess())
                ApplyState();
            else
                Dispatcher.Invoke(ApplyState);
        }

        private async void Button4_Click_1(object sender, RoutedEventArgs e)
        {
            if (Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            SetOperationButtonsEnabled(false);
            try
            {
                int result = await Manager.PerformAdaptiveDarkCalibrationAsync();
                if (result == SpectrometerManager.OperationBusy)
                {
                    MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                }
                else if (result == 1)
                {
                    log.Info("自适应校零成功");
                    MessageBox.Show(SpectrumResources.AdaptiveAutoDarkSuccess);
                }
                else
                {
                    string errorMessage = cvColorVision.Spectrometer.GetErrorMessage(result);
                    log.Error($"自适应校零失败: {errorMessage}");
                    MessageBox.Show(string.Format(SpectrumResources.AdaptiveAutoDarkFailed, errorMessage));
                }
            }
            finally
            {
                SetOperationButtonsEnabled(true);
            }
        }

        private async void Button6_Click(object sender, RoutedEventArgs e)
        {
            if (continuousMeasurementTask is { IsCompleted: false } || Manager.IsDeviceBusy)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }

            if (Manager.EnableAutodark && !Manager.ShutterController.IsConnected)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.NoShutterAutoZero,
                    SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            continuousMeasurementCancellation = new CancellationTokenSource();
            continuousFailureCount = 0;
            SetContinuousMeasurementUi(true);
            continuousMeasurementTask = RunContinuousMeasurementAsync(continuousMeasurementCancellation.Token);
            bool completedNormally = false;
            try
            {
                await continuousMeasurementTask;
                completedNormally = !continuousMeasurementCancellation.IsCancellationRequested;
            }
            catch (OperationCanceledException)
            {
                log.Info("连续测量已停止");
            }
            finally
            {
                int failureCount = continuousFailureCount;
                continuousMeasurementCancellation.Dispose();
                continuousMeasurementCancellation = null;
                continuousMeasurementTask = null;
                Manager.LoopMeasureNum = 0;
                SetContinuousMeasurementUi(false);

                if (completedNormally && Manager.MeasurementNum > 0)
                    MessageBox.Show(this, string.Format(SpectrumResources.ContinuousTestCompletedWithFailureCount, failureCount));
            }
        }

        private async Task RunContinuousMeasurementAsync(CancellationToken cancellationToken)
        {
            log.Info($"连续测量开始，总数 {Manager.MeasurementNum}");
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int completedCount = 0;

            while (Manager.MeasurementNum <= 0 || completedCount < Manager.MeasurementNum)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (string Path, string Sha256)? magnitudeSnapshot = null;
                try
                {
                    magnitudeSnapshot = CaptureMagnitudeFileSnapshot();
                }
                catch (Exception ex)
                {
                    log.Warn("无法记录本次连续测量使用的幅值 DAT，结果不会用于光谱校正。", ex);
                }

                SpectrumMeasurementResult result = await Manager.MeasureAsync(cancellationToken);
                if (!result.IsSuccess)
                {
                    continuousFailureCount++;
                    log.Warn($"连续测量失败: {result.ErrorMessage}");
                }
                else
                {
                    TrackCorrectionMeasurementResult(result, magnitudeSnapshot);
                }

                completedCount++;
                Manager.LoopMeasureNum = completedCount;
                UpdateContinuousProgress(completedCount, stopwatch.Elapsed);

                if (Manager.MeasurementNum > 0 && completedCount >= Manager.MeasurementNum)
                    break;

                await Task.Delay(Manager.MeasurementInterval, cancellationToken);
            }
        }

        private void UpdateContinuousProgress(int completedCount, TimeSpan elapsed)
        {
            ElapsedTimeText.Text = FormatTimeSpan(elapsed);
            if (Manager.MeasurementNum <= 0)
                return;

            ContinuousProgressBar.Value = (double)completedCount / Manager.MeasurementNum * 100;
            double remainingSeconds = elapsed.TotalSeconds / completedCount * (Manager.MeasurementNum - completedCount);
            RemainingTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(remainingSeconds));
        }

        private void SetContinuousMeasurementUi(bool running)
        {
            button6.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
            button7.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            button7.IsEnabled = running;
            TimeEstimationPanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            button3.IsEnabled = !running;
            button5.IsEnabled = !running;
            ButtonAutoInt.IsEnabled = !running;
            if (running)
            {
                ContinuousProgressBar.Value = 0;
                ElapsedTimeText.Text = "--:--";
                RemainingTimeText.Text = "--:--";
            }
        }

        private static string FormatTimeSpan(TimeSpan value) => value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}"
            : $"{value.Minutes:D2}:{value.Seconds:D2}";

        private static void ShowMeasurementFailure(SpectrumMeasurementResult result)
        {
            string message = result.IsBusy
                ? SpectrumResources.OperationInProgressPleaseWait
                : result.ErrorMessage ?? "测量失败，请查看日志。";
            MessageBox.Show(Application.Current.GetActiveWindow(), message,
                SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            button7.IsEnabled = false;
            continuousMeasurementCancellation?.Cancel();
        }

        private void CancelContinuousMeasurement() => continuousMeasurementCancellation?.Cancel();
    }
}
