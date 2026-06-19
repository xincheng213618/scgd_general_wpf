#pragma warning disable CS8602
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using Conoscope.ApplicationServices.Capture;
using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
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
            string? selectedCameraCode = GetSelectedCamera()?.Config.Code;
            List<DeviceCamera> cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();

            cbCameraDevice.ItemsSource = cameras;

            if (!string.IsNullOrWhiteSpace(selectedCameraCode))
            {
                cbCameraDevice.SelectedItem = cameras.FirstOrDefault(item => item.Config.Code == selectedCameraCode);
            }

            if (cbCameraDevice.SelectedItem == null && cameras.Count > 0)
            {
                cbCameraDevice.SelectedIndex = 0;
            }

            RefreshCalibrationTemplates();
            btnCaptureCamera.IsEnabled = !isRunningOperation && GetSelectedCamera() != null;
        }

        private void RefreshCalibrationTemplates()
        {
            DeviceCamera? camera = GetSelectedCamera();
            TemplateModel<CalibrationParam>? previous = cbCalibrationTemplate.SelectedItem as TemplateModel<CalibrationParam>;
            int previousId = previous?.Id ?? -1;
            string previousKey = previous?.Key ?? string.Empty;

            cbCalibrationTemplate.ItemsSource = camera?.PhyCamera?.CalibrationParams.CreateEmpty();
            TemplateModel<CalibrationParam>? target = cbCalibrationTemplate.Items.OfType<TemplateModel<CalibrationParam>>()
                .FirstOrDefault(item => item.Id == previousId || string.Equals(item.Key, previousKey, StringComparison.OrdinalIgnoreCase));

            cbCalibrationTemplate.SelectedItem = target;
            if (cbCalibrationTemplate.SelectedItem == null)
            {
                cbCalibrationTemplate.SelectedIndex = cbCalibrationTemplate.Items.Count > 0 ? 0 : -1;
            }
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

        private bool ShouldReuseActiveViewOnCapture()
        {
            return chkReuseActiveViewOnCapture?.IsChecked == true && ActiveView != null;
        }

        private TimedButtonOperationRegistry EnsureCaptureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildTimedOperationKey);

            operations.Register(btnRunFlow, options =>
            {
                options.RunningText = Properties.Resources.StatusExecuting;
                options.ProgressForeground = Brushes.DodgerBlue;
                options.MinimumExpectedDurationMs = DefaultFlowExpectedDurationMs;
            });

            operations.Register(btnCaptureCamera, options =>
            {
                options.RunningText = Properties.Resources.StatusCapturing;
                options.ProgressForeground = Brushes.DodgerBlue;
                options.MinimumExpectedDurationMs = DefaultCameraCaptureExpectedDurationMs;
            });

            return operations;
        }

        private static string BuildTimedOperationKey(string actionKey)
        {
            return $"conoscope:capture:{actionKey}";
        }

        private TimedButtonOperationScope? BeginTrackedOperation(Button button, string progressLabel, double expectedDurationMs)
        {
            TimedButtonOperationRegistry operations = EnsureCaptureTimedButtonOperations();
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
                MessageBox.Show(Properties.Resources.MsgSelectCamera, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private async void btnRunFlow_Click(object sender, RoutedEventArgs e)
        {
            TemplateModel<FlowParam>? flowTemplate = GetSelectedFlowTemplate();
            if (flowTemplate == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectFlow, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnRunFlow, Properties.Resources.BtnExecute, DefaultFlowExpectedDurationMs);
                SetOperationBusy(true);

                ConoscopeFlowCaptureResult result = await ConoscopeCaptureWorkflow.RunFlowAsync(flowTemplate);
                if (!result.Started)
                {
                    return;
                }

                if (!result.Completed)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFlowFailedDetail, result.FlowResult.FlowStatus, result.FlowResult.Params), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.MsgFlowCvcieNotFoundDetail, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFlowFailedDetail, ex.Message, string.Empty), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(Properties.Resources.MsgSelectCamera, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimedButtonOperationScope? operationScope = null;
            bool operationSucceeded = false;

            try
            {
                operationScope = BeginTrackedOperation(btnCaptureCamera, Properties.Resources.BtnCapturePhoto, DefaultCameraCaptureExpectedDurationMs);
                SetOperationBusy(true);

                ConoscopeCameraCaptureResult result = await ConoscopeCaptureWorkflow.CaptureCameraAsync(camera, GetSelectedCalibrationParam());
                if (!result.Succeeded)
                {
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgCaptureFailedDetail, result.State, result.MessageRecord.MsgReturn?.Message), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.HasFile)
                {
                    OpenConoscope(result.FilePath!, result.ExposureSummary, preferReuseActiveView: ShouldReuseActiveViewOnCapture());
                    operationSucceeded = true;
                }
                else
                {
                    MessageBox.Show(Properties.Resources.MsgCaptureCvcieNotFoundDetail, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgCaptureFailedDetail, ex.Message, string.Empty), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                operationScope?.Complete(operationSucceeded);
                StopOperationProgress();
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
            btnCaptureCamera.IsEnabled = !isRunningOperation && GetSelectedCamera() != null;
        }

        private void cbCalibrationTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
