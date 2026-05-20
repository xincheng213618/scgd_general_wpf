using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Messages;
using ColorVision.UI;
using Conoscope.ApplicationServices.Capture;
using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private const string FlowRunOperationActionKey = "flow-run";
        private const string CameraCaptureOperationActionKey = "camera-capture";
        private const double DefaultFlowExpectedDurationMs = 20000;
        private const double DefaultCameraCaptureExpectedDurationMs = 20000;

        private void RefreshFlowTemplates()
        {
            int preferredId = GetSelectedFlowTemplate()?.Id ?? FlowEngineConfig.Instance.LastSelectFlow;
            cbFlowTemplate.ItemsSource = null;
            cbFlowTemplate.ItemsSource = TemplateFlow.Params;
            if (TemplateFlow.Params.Count > 0)
            {
                cbFlowTemplate.SelectedItem = TemplateFlow.Params.FirstOrDefault(item => item.Id == preferredId)
                    ?? TemplateFlow.Params[0];
            }

            btnRunFlow.IsEnabled = !isRunningOperation && GetSelectedFlowTemplate() != null;
        }

        private void RefreshCameraDevices()
        {
            DeviceCamera? selectedCamera = cbCameraDevice.SelectedItem as DeviceCamera;
            string? selectedCode = selectedCamera?.Config.Code;
            List<DeviceCamera> cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
            cbCameraDevice.ItemsSource = cameras;

            if (!string.IsNullOrWhiteSpace(selectedCode))
            {
                cbCameraDevice.SelectedItem = cameras.FirstOrDefault(item => item.Config.Code == selectedCode);
            }

            if (cbCameraDevice.SelectedItem == null && cameras.Count > 0)
            {
                cbCameraDevice.SelectedIndex = 0;
            }

            RefreshCalibrationTemplates();
            RefreshNdControls();
        }

        private void RefreshCalibrationTemplates()
        {
            DeviceCamera? camera = GetSelectedCamera();
            TemplateModel<CalibrationParam>? previous = cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>;
            int previousId = previous?.Id ?? -1;
            string previousKey = previous?.Key ?? string.Empty;

            cbCalibrationTemplate.ItemsSource = camera?.PhyCamera?.CalibrationParams.CreateEmpty();
            TemplateModel<CalibrationParam>? target = null;
            if (camera != null)
            {
                target = ResolveNdCalibrationTemplate(camera, camera.Config.NDPort);
            }

            target ??= cbCalibrationTemplate.Items.OfType<TemplateModel<CalibrationParam>>()
                .FirstOrDefault(item => item.Id == previousId || string.Equals(item.Key, previousKey, StringComparison.OrdinalIgnoreCase));

            cbCalibrationTemplate.SelectedItem = target;
            if (cbCalibrationTemplate.SelectedItem == null)
            {
                cbCalibrationTemplate.SelectedIndex = cbCalibrationTemplate.Items.Count > 0 ? 0 : -1;
            }
        }

        private void RefreshNdControls()
        {
            DeviceCamera? camera = GetSelectedCamera();
            bool isNdAvailable = camera?.Config.CFW.IsUseCFW == true && camera.Config.CFW.IsNDPort;
            txtNDPort.IsEnabled = isNdAvailable && !isRunningOperation;
            btnSetNDPort.IsEnabled = isNdAvailable && !isRunningOperation;
            btnGetNDPort.IsEnabled = isNdAvailable && !isRunningOperation;
            btnBindNdCalibration.IsEnabled = isNdAvailable && !isRunningOperation && cbCalibrationTemplate.SelectedItem is TemplateModel<CalibrationParam>;
            btnClearNdCalibrationBinding.IsEnabled = isNdAvailable && !isRunningOperation;
            txtNDPort.Text = camera?.Config.NDPort.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private void ServiceManager_ServiceChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshCameraDevices));
        }

        private TemplateModel<FlowParam>? GetSelectedFlowTemplate()
        {
            return cbFlowTemplate.SelectedItem as TemplateModel<FlowParam>;
        }

        private DeviceCamera? GetSelectedCamera()
        {
            return cbCameraDevice.SelectedItem as DeviceCamera;
        }

        private CalibrationParam GetSelectedCalibrationParam()
        {
            return (cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>)?.Value
                ?? new CalibrationParam { Id = -1, Name = string.Empty };
        }

        private TemplateModel<CalibrationParam>? GetSelectedCalibrationTemplate()
        {
            return cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>;
        }

        private static ConoscopeNdCalibrationBinding? FindNdCalibrationBinding(DeviceCamera camera, int ndPort)
        {
            string cameraCode = camera.Config.Code ?? string.Empty;
            return ConoscopeManager.GetInstance().Config.Capture.NdCalibrationBindings
                .FirstOrDefault(item => item.NdPort == ndPort
                    && string.Equals(item.CameraCode, cameraCode, StringComparison.OrdinalIgnoreCase));
        }

        private TemplateModel<CalibrationParam>? ResolveNdCalibrationTemplate(DeviceCamera camera, int ndPort)
        {
            ConoscopeNdCalibrationBinding? binding = FindNdCalibrationBinding(camera, ndPort);
            if (binding == null)
            {
                return null;
            }

            return cbCalibrationTemplate.Items.OfType<TemplateModel<CalibrationParam>>()
                .FirstOrDefault(item => item.Id == binding.CalibrationTemplateId
                    || string.Equals(item.Key, binding.CalibrationTemplateName, StringComparison.OrdinalIgnoreCase));
        }

        private bool ApplyNdCalibrationBinding(DeviceCamera camera, int ndPort, bool reportStatus)
        {
            TemplateModel<CalibrationParam>? matched = ResolveNdCalibrationTemplate(camera, ndPort);
            if (matched == null)
            {
                if (reportStatus)
                {
                    SetOperationStatus(string.Format(Properties.Resources.MsgNdNotBoundCalibration, ndPort), Brushes.Gray);
                }

                return false;
            }

            cbCalibrationTemplate.SelectedItem = matched;
            if (reportStatus)
            {
                SetOperationStatus(string.Format(Properties.Resources.MsgNdMatchedCalibration, ndPort, matched.Key), Brushes.LimeGreen);
            }

            return true;
        }

        private bool TryGetNdPort(out int ndPort)
        {
            if (int.TryParse(txtNDPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out ndPort))
            {
                return true;
            }

            DeviceCamera? camera = GetSelectedCamera();
            ndPort = camera?.Config.NDPort ?? 0;
            return camera != null;
        }

        private bool ShouldReuseActiveViewOnCapture()
        {
            return chkReuseActiveViewOnCapture?.IsChecked == true && ActiveView != null;
        }

        private TimedButtonOperationRegistry EnsureCaptureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildTimedOperationKey);

            if (!operations.Contains(btnRunFlow))
            {
                object? originalContent = btnRunFlow.Content;
                operations.Register(
                    btnRunFlow,
                    new TimedButtonOperationOptions
                    {
                        OperationKey = BuildTimedOperationKey(FlowRunOperationActionKey),
                        RunningText = Properties.Resources.StatusExecuting,
                        ProgressForeground = Brushes.DodgerBlue,
                        ExpectedDurationProvider = () => GetTimedOperationExpectedDurationMs(FlowRunOperationActionKey, DefaultFlowExpectedDurationMs),
                        ContentFactory = _ => originalContent ?? Properties.Resources.BtnExecute,
                        ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip("执行当前流程并打开结果图像", stats),
                        MinimumExpectedDurationMs = 2000
                    });
            }

            if (!operations.Contains(btnCaptureCamera))
            {
                object? originalContent = btnCaptureCamera.Content;
                operations.Register(
                    btnCaptureCamera,
                    new TimedButtonOperationOptions
                    {
                        OperationKey = BuildTimedOperationKey(CameraCaptureOperationActionKey),
                        RunningText = Properties.Resources.StatusCapturing,
                        ProgressForeground = Brushes.DodgerBlue,
                        ExpectedDurationProvider = () => GetTimedOperationExpectedDurationMs(CameraCaptureOperationActionKey, DefaultCameraCaptureExpectedDurationMs),
                        ContentFactory = _ => originalContent ?? Properties.Resources.BtnCapturePhoto,
                        ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip("执行相机拍照并打开结果图像", stats),
                        MinimumExpectedDurationMs = 2000
                    });
            }

            return operations;
        }

        private static string BuildTimedOperationKey(string actionKey)
        {
            return $"conoscope:capture:{actionKey}";
        }

        private static double NormalizeExpectedDuration(double durationMs, double fallbackMs)
        {
            double resolved = durationMs > 0 ? durationMs : fallbackMs;
            return Math.Max(1000, resolved);
        }

        private static double GetTimedOperationExpectedDurationMs(string actionKey, double fallbackMs)
        {
            string operationKey = BuildTimedOperationKey(actionKey);
            TimedButtonOperationStats? stats = TimedButtonOperationStatsManager.GetAll()
                .FirstOrDefault(item => string.Equals(item.OperationKey, operationKey, StringComparison.Ordinal))
                ?.Stats;

            if (stats != null)
            {
                if (stats.SuccessCount > 1 && stats.AverageElapsedMs > 0)
                {
                    return NormalizeExpectedDuration(stats.AverageElapsedMs, fallbackMs);
                }

                if (stats.LastElapsedMs > 0)
                {
                    return NormalizeExpectedDuration(stats.LastElapsedMs, fallbackMs);
                }

                if (stats.WarmupElapsedMs > 0)
                {
                    return NormalizeExpectedDuration(stats.WarmupElapsedMs, fallbackMs);
                }
            }

            return NormalizeExpectedDuration(fallbackMs, fallbackMs);
        }

        private TimedButtonOperationScope? BeginTrackedOperation(Button button, string actionKey, string progressLabel, double fallbackExpectedDurationMs)
        {
            TimedButtonOperationRegistry operations = EnsureCaptureTimedButtonOperations();
            double expectedDurationMs = GetTimedOperationExpectedDurationMs(actionKey, fallbackExpectedDurationMs);
            TimedButtonOperationScope? operationScope = operations.Begin(button, expectedDurationMs, progressLabel);
            StartOperationProgress(progressLabel, expectedDurationMs);
            return operationScope;
        }

        private void SetOperationBusy(bool busy)
        {
            isRunningOperation = busy;
            btnRunFlow.IsEnabled = !busy && GetSelectedFlowTemplate() != null;
            btnCaptureCamera.IsEnabled = !busy && GetSelectedCamera() != null;
            btnRefreshCameraDevices.IsEnabled = !busy;
            btnApplyPreprocessToActiveView.IsEnabled = !busy && ActiveView != null;
            RefreshNdControls();
        }

        private void SetOperationStatus(string text, Brush? brush = null)
        {
            tbOperationStatus.Text = text;
            tbOperationStatus.Foreground = brush ?? Brushes.Gray;
        }

        private void btnEditFlowTemplates_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = Math.Max(0, cbFlowTemplate.SelectedIndex);
            new TemplateEditorWindow(new TemplateFlow(), selectedIndex)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();

            RefreshFlowTemplates();
        }

        private void btnEditCalibrationTemplates_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera?.PhyCamera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int selectedIndex = Math.Max(0, cbCalibrationTemplate.SelectedIndex - 1);
            new TemplateEditorWindow(new TemplateCalibrationParam(camera.PhyCamera), selectedIndex)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();

            RefreshCalibrationTemplates();
        }

        private void btnBindNdCalibration_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            TemplateModel<CalibrationParam>? calibrationTemplate = GetSelectedCalibrationTemplate();
            if (camera == null || calibrationTemplate == null || calibrationTemplate.Id < 0)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCameraAndCalibration, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetNdPort(out int ndPort))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidNdPort, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConoscopeNdCalibrationBinding? binding = FindNdCalibrationBinding(camera, ndPort);
            if (binding == null)
            {
                binding = new ConoscopeNdCalibrationBinding();
                CaptureConfig.NdCalibrationBindings.Add(binding);
            }

            binding.CameraCode = camera.Config.Code ?? string.Empty;
            binding.CameraName = camera.Config.Name ?? string.Empty;
            binding.NdPort = ndPort;
            binding.CalibrationTemplateId = calibrationTemplate.Id;
            binding.CalibrationTemplateName = calibrationTemplate.Key;
            ConfigService.Instance.Save<ConoscopeConfig>();
            SetOperationStatus(string.Format(Properties.Resources.MsgNdBound, ndPort, calibrationTemplate.Key), Brushes.LimeGreen);
        }

        private void btnClearNdCalibrationBinding_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null || !TryGetNdPort(out int ndPort))
            {
                MessageBox.Show(Properties.Resources.MsgSelectCameraAndNd, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConoscopeNdCalibrationBinding? binding = FindNdCalibrationBinding(camera, ndPort);
            if (binding == null)
            {
                SetOperationStatus(string.Format(Properties.Resources.MsgNdNotBound, ndPort), Brushes.Gray);
                return;
            }

            CaptureConfig.NdCalibrationBindings.Remove(binding);
            ConfigService.Instance.Save<ConoscopeConfig>();
            SetOperationStatus(string.Format(Properties.Resources.MsgNdUnbound, ndPort), Brushes.LimeGreen);
        }

        private async void btnRunFlow_Click(object sender, RoutedEventArgs e)
        {
            TemplateModel<FlowParam>? flowTemplate = GetSelectedFlowTemplate();
            if (flowTemplate == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectFlow, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnRunFlow, FlowRunOperationActionKey, "执行流程", DefaultFlowExpectedDurationMs);
                SetOperationBusy(true);
                SetOperationStatus(string.Format(Properties.Resources.MsgFlowExecuting, flowTemplate.Key), Brushes.DodgerBlue);

                ConoscopeFlowCaptureResult result = await ConoscopeCaptureWorkflow.RunFlowAsync(flowTemplate);
                if (!result.Started)
                {
                    SetOperationStatus(Properties.Resources.MsgFlowNotStarted, Brushes.OrangeRed);
                    return;
                }

                if (!result.Completed)
                {
                    SetOperationStatus(string.Format(Properties.Resources.MsgFlowIncomplete, result.FlowResult!.FlowStatus), Brushes.OrangeRed);
                    MessageBox.Show(string.Format(Properties.Resources.MsgFlowFailedDetail, result.FlowResult.FlowStatus, result.FlowResult.Params), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                    SetOperationStatus(string.Format(Properties.Resources.MsgFlowResultOpened, System.IO.Path.GetFileName(result.FilePath)), Brushes.LimeGreen);
                }
                else
                {
                    SetOperationStatus(Properties.Resources.MsgFlowCvcieNotFound, Brushes.OrangeRed);
                    MessageBox.Show(Properties.Resources.MsgFlowCvcieNotFoundDetail, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus(Properties.Resources.MsgFlowFailed, Brushes.OrangeRed);
                MessageBox.Show(string.Format(Properties.Resources.MsgFlowFailedDetail, ex.Message, string.Empty), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                operationScope?.Complete(operationSucceeded);
                StopOperationProgress();
                SetOperationBusy(false);
            }
        }

        private async void btnCaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnCaptureCamera, CameraCaptureOperationActionKey, "相机拍照", DefaultCameraCaptureExpectedDurationMs);
                SetOperationBusy(true);
                SetOperationStatus(string.Format(Properties.Resources.MsgCapturingPhoto, camera.Config.Name), Brushes.DodgerBlue);

                ConoscopeCameraCaptureResult result = await ConoscopeCaptureWorkflow.CaptureCameraAsync(camera, GetSelectedCalibrationParam());
                if (!result.Succeeded)
                {
                    SetOperationStatus(string.Format(Properties.Resources.MsgCaptureFailed, result.State), Brushes.OrangeRed);
                    MessageBox.Show(string.Format(Properties.Resources.MsgCaptureFailedDetail, result.State, result.MessageRecord.MsgReturn?.Message), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, result.ExposureSummary, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                    SetOperationStatus(string.Format(Properties.Resources.MsgFlowResultOpened, System.IO.Path.GetFileName(result.FilePath)), Brushes.LimeGreen);
                }
                else
                {
                    SetOperationStatus(Properties.Resources.MsgCaptureCvcieNotFound, Brushes.OrangeRed);
                    MessageBox.Show(Properties.Resources.MsgCaptureCvcieNotFoundDetail, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus(Properties.Resources.MsgCaptureFailedTitle, Brushes.OrangeRed);
                MessageBox.Show(string.Format(Properties.Resources.MsgCaptureFailedDetail, ex.Message, string.Empty), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                operationScope?.Complete(operationSucceeded);
                StopOperationProgress();
                SetOperationBusy(false);
            }
        }

        private async void btnSetNDPort_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtNDPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidNdPort, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus(string.Format(Properties.Resources.MsgSwitchingNd, port), Brushes.DodgerBlue);
                camera.Config.NDPort = port;
                MsgRecord msgRecord = camera.DService.SetNDPort();
                MsgRecordState state = await ConoscopeCaptureWorkflow.WaitForMsgRecordAsync(msgRecord);
                if (state == MsgRecordState.Success)
                {
                    if (!ApplyNdCalibrationBinding(camera, port, reportStatus: true))
                    {
                        SetOperationStatus(string.Format(Properties.Resources.MsgNdSwitched, port), Brushes.LimeGreen);
                    }
                }
                else
                {
                    SetOperationStatus(string.Format(Properties.Resources.MsgNdSwitchFailed, state), Brushes.OrangeRed);
                    MessageBox.Show(string.Format(Properties.Resources.MsgNdSwitchFailedDetail, state, msgRecord.MsgReturn?.Message), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus(Properties.Resources.MsgNdSwitchFailed, Brushes.OrangeRed);
                MessageBox.Show(string.Format(Properties.Resources.MsgNdSwitchFailedDetail, ex.Message, string.Empty), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetOperationBusy(false);
            }
        }

        private async void btnGetNDPort_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectCamera, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus(Properties.Resources.MsgReadingNd, Brushes.DodgerBlue);
                MsgRecord msgRecord = camera.DService.GetPort();
                MsgRecordState state = await ConoscopeCaptureWorkflow.WaitForMsgRecordAsync(msgRecord);
                if (state == MsgRecordState.Success)
                {
                    int port = ConoscopeCaptureWorkflow.ReadMsgReturnInt(msgRecord.MsgReturn, "Port");
                    camera.Config.NDPort = port;
                    txtNDPort.Text = port.ToString(CultureInfo.InvariantCulture);
                    if (!ApplyNdCalibrationBinding(camera, port, reportStatus: true))
                    {
                        SetOperationStatus(string.Format(Properties.Resources.MsgCurrentNd, port), Brushes.LimeGreen);
                    }
                }
                else
                {
                    SetOperationStatus(string.Format(Properties.Resources.MsgNdReadFailed, state), Brushes.OrangeRed);
                    MessageBox.Show(string.Format(Properties.Resources.MsgNdReadFailedDetail, state, msgRecord.MsgReturn?.Message), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus(Properties.Resources.MsgNdReadFailed, Brushes.OrangeRed);
                MessageBox.Show(string.Format(Properties.Resources.MsgNdReadFailedDetail, ex.Message, string.Empty), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetOperationBusy(false);
            }
        }

        private void cbFlowTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GetSelectedFlowTemplate() is TemplateModel<FlowParam> flowTemplate)
            {
                FlowEngineConfig.Instance.LastSelectFlow = flowTemplate.Id;
            }

            btnRunFlow.IsEnabled = !isRunningOperation && GetSelectedFlowTemplate() != null;
        }

        private void cbCameraDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshCalibrationTemplates();
            RefreshNdControls();
            btnCaptureCamera.IsEnabled = !isRunningOperation && GetSelectedCamera() != null;
        }

        private void cbCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshNdControls();
        }
    }
}
