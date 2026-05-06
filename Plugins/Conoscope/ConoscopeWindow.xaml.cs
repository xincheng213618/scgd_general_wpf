using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Conoscope.Analysis;
using Conoscope.Core;
using Conoscope.MVS;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public class MenuConoscopeWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "VAM";

        public override void Execute()
        {
            ConoscopeWindow conoscopeWindow = new ConoscopeWindow();
            conoscopeWindow.Show();
        }
    }

    public class ConoscopeWindowConfig : WindowConfig
    {
        public static ConoscopeWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConoscopeWindowConfig>();
    }

    public partial class ConoscopeWindow : Window, IDisposable
    {
        public static ConoscopeWindow? Instance { get; private set; }

        private ThemeChangedHandler? themeChangedHandler;
        private bool isUpdatingModelSelection;
        private bool isRunningOperation;

        private MVSViewWindow? observationCameraWindow;

        public ConoscopeWindow()
            : this(createInitialView: false)
        {
        }

        public ConoscopeWindow(string filePath)
            : this(createInitialView: false)
        {
            OpenConoscope(filePath);
        }

        private ConoscopeWindow(bool createInitialView)
        {
            InitializeComponent();
            Instance = this;
            Title = "Conoscope " + (Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty);
            DataContext = ConoscopeManager.GetInstance();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            InitializeTheme();
            InitializeModelSelector();
            InitializeOperationControls();

            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            ServiceManager.GetInstance().ServiceChanged += ServiceManager_ServiceChanged;
            RefreshWindowModelState();

            if (createInitialView)
            {
                AddConoscopeView(null, activate: true);
            }

            Closed += (s, e) =>
            {
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }

                Dispose();
            };
        }

        public ConoscopeView? ActiveView => GetActiveView();

        public void OpenConoscope(string filename)
        {
            AddConoscopeView(filename, activate: true);
        }

        internal void RefreshConoscopeConfiguration()
        {
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshConoscopeConfiguration();
            }

            RefreshWindowModelState();
        }

        private void InitializeTheme()
        {
            void ThemeChange(Theme theme)
            {
                DockingManager.Theme = theme == Theme.Dark
                    ? new AvalonDock.Themes.Vs2013DarkTheme()
                    : new AvalonDock.Themes.Vs2013LightTheme();
            }

            themeChangedHandler = ThemeChange;
            ThemeChange(ThemeManager.Current.CurrentUITheme);
            ThemeManager.Current.CurrentUIThemeChanged += themeChangedHandler;
        }

        private void InitializeModelSelector()
        {
            cbModelType.ItemsSource = Enum.GetValues<ConoscopeModelType>();
            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = ConoscopeManager.GetInstance().Config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }
        }

        private void InitializeOperationControls()
        {
            RefreshFlowTemplates();
            RefreshCameraDevices();
        }

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

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            RefreshWindowModelState();
        }

        private void RefreshWindowModelState()
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            tbCurrentModel.Text = config.CurrentModelProfile.DisplayName;
            btnOpenObservationCamera.Visibility = config.CurrentModelProfile.HasObservationCamera
                ? Visibility.Visible
                : Visibility.Collapsed;

            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }

            MenuService.Instance?.RefreshMenuItemsByGuid(MenuItemConstants.View);
        }

        private ConoscopeView AddConoscopeView(string? filePath, bool activate)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string existingContentId = GetContentId(filePath);
                LayoutDocument? existingDocument = ViewDocumentPane.Children
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(item => item.ContentId == existingContentId);
                if (existingDocument?.Content is ConoscopeView existingView)
                {
                    SelectDocument(existingDocument);
                    ConoscopeModuleService.Activate(existingView);
                    return existingView;
                }
            }

            ConoscopeView view = new ConoscopeView();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                view.OpenConoscope(filePath);
            }

            LayoutDocument layoutDocument = new LayoutDocument
            {
                Title = string.IsNullOrWhiteSpace(filePath) ? "Conoscope" : Path.GetFileName(filePath),
                ContentId = string.IsNullOrWhiteSpace(filePath) ? $"StandaloneConoscope:{Guid.NewGuid():N}" : GetContentId(filePath),
                Content = view,
                CanClose = true,
                CanFloat = true
            };

            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    ConoscopeModuleService.Activate(view);
                }
            };
            layoutDocument.Closing += (s, e) => view.Dispose();

            ViewDocumentPane.Children.Add(layoutDocument);
            if (activate)
            {
                SelectDocument(layoutDocument);
            }

            return view;
        }

        private void SelectDocument(LayoutDocument document)
        {
            ViewDocumentPane.SelectedContentIndex = ViewDocumentPane.IndexOf(document);
            document.IsActive = true;
            if (document.Content is ConoscopeView view)
            {
                ConoscopeModuleService.Activate(view);
            }
        }

        private ConoscopeView? GetActiveView()
        {
            LayoutDocument? activeDocument = ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => item.IsActive);

            if (activeDocument?.Content is ConoscopeView activeView)
            {
                return activeView;
            }

            int selectedIndex = ViewDocumentPane.SelectedContentIndex;
            if (selectedIndex >= 0 && selectedIndex < ViewDocumentPane.Children.Count
                && ViewDocumentPane.Children[selectedIndex] is LayoutDocument selectedDocument
                && selectedDocument.Content is ConoscopeView selectedView)
            {
                return selectedView;
            }

            return null;
        }

        private ConoscopeView[] GetOpenViews()
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .Select(item => item.Content as ConoscopeView)
                .Where(item => item != null)
                .Cast<ConoscopeView>()
                .ToArray();
        }

        private static string GetContentId(string filePath)
        {
            return "StandaloneConoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
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
            return ConoscopeManager.GetInstance().Config.NdCalibrationBindings
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
                    SetOperationStatus($"ND {ndPort} 未绑定校正", Brushes.Gray);
                }

                return false;
            }

            cbCalibrationTemplate.SelectedItem = matched;
            if (reportStatus)
            {
                SetOperationStatus($"ND {ndPort} 已匹配校正: {matched.Key}", Brushes.LimeGreen);
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

        private static double[] GetCameraExpTimes(DeviceCamera camera)
        {
            return camera.Config.IsExpThree
                ? new[] { camera.DisplayConfig.ExpTimeR, camera.DisplayConfig.ExpTimeG, camera.DisplayConfig.ExpTimeB }
                : new[] { camera.DisplayConfig.ExpTime };
        }

        private void SetOperationBusy(bool busy)
        {
            isRunningOperation = busy;
            btnRunFlow.IsEnabled = !busy && GetSelectedFlowTemplate() != null;
            btnCaptureCamera.IsEnabled = !busy && GetSelectedCamera() != null;
            btnRefreshCameraDevices.IsEnabled = !busy;
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
                MessageBox.Show("请选择相机设备", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show("请选择相机设备和有效的校正模板", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryGetNdPort(out int ndPort))
            {
                MessageBox.Show("请输入有效的 ND 端口", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            ConoscopeNdCalibrationBinding? binding = FindNdCalibrationBinding(camera, ndPort);
            if (binding == null)
            {
                binding = new ConoscopeNdCalibrationBinding();
                config.NdCalibrationBindings.Add(binding);
            }

            binding.CameraCode = camera.Config.Code ?? string.Empty;
            binding.CameraName = camera.Config.Name ?? string.Empty;
            binding.NdPort = ndPort;
            binding.CalibrationTemplateId = calibrationTemplate.Id;
            binding.CalibrationTemplateName = calibrationTemplate.Key;
            ConfigService.Instance.Save<ConoscopeConfig>();
            SetOperationStatus($"已绑定 ND {ndPort} -> {calibrationTemplate.Key}", Brushes.LimeGreen);
        }

        private void btnClearNdCalibrationBinding_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null || !TryGetNdPort(out int ndPort))
            {
                MessageBox.Show("请选择相机并输入有效的 ND 端口", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConoscopeNdCalibrationBinding? binding = FindNdCalibrationBinding(camera, ndPort);
            if (binding == null)
            {
                SetOperationStatus($"ND {ndPort} 没有绑定校正", Brushes.Gray);
                return;
            }

            ConoscopeManager.GetInstance().Config.NdCalibrationBindings.Remove(binding);
            ConfigService.Instance.Save<ConoscopeConfig>();
            SetOperationStatus($"已解除 ND {ndPort} 的校正绑定", Brushes.LimeGreen);
        }

        private void btnOpenContrastTest_Click(object sender, RoutedEventArgs e)
        {
            new ContrastTestWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void btnOpenColorGamut_Click(object sender, RoutedEventArgs e)
        {
            new ColorGamutWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private static async Task<string?> WaitForFlowCvcieAsync(FlowControlData flowResult)
        {
            MeasureBatchModel? batch = null;
            if (!string.IsNullOrWhiteSpace(flowResult.SerialNumber))
            {
                batch = BatchResultMasterDao.Instance.GetByCode(flowResult.SerialNumber);
            }

            batch ??= FlowEngineManager.GetInstance().Batch;
            if (batch == null || batch.Id <= 0)
            {
                return null;
            }

            for (int i = 0; i < 10; i++)
            {
                string? filePath = FindCvcieFile(MeasureImgResultDao.Instance.GetAllByBatchId(batch.Id));
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    return filePath;
                }

                await Task.Delay(300);
            }

            return null;
        }

        private static async Task<string?> WaitForCameraCvcieAsync(MsgRecord msgRecord)
        {
            for (int i = 0; i < 8; i++)
            {
                int masterId = TryReadMsgReturnInt(msgRecord.MsgReturn, "MasterId");
                if (masterId > 0)
                {
                    MeasureResultImgModel? result = MeasureImgResultDao.Instance.GetById(masterId);
                    string? filePath = GetCvcieFilePath(result);
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        return filePath;
                    }
                }

                await Task.Delay(300);
            }

            return null;
        }

        private static string? FindCvcieFile(IEnumerable<MeasureResultImgModel> results)
        {
            foreach (MeasureResultImgModel result in results)
            {
                string? filePath = GetCvcieFilePath(result);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }

        private static string? GetCvcieFilePath(MeasureResultImgModel? result)
        {
            if (result == null)
            {
                return null;
            }

            foreach (string? candidate in new[] { result.FileUrl, result.RawFile })
            {
                if (string.IsNullOrWhiteSpace(candidate) || !File.Exists(candidate))
                {
                    continue;
                }

                if (string.Equals(Path.GetExtension(candidate), ".cvcie", StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static async Task<MsgRecordState> WaitForMsgRecordAsync(MsgRecord msgRecord)
        {
            if (IsFinalState(msgRecord.MsgRecordState))
            {
                return msgRecord.MsgRecordState;
            }

            TaskCompletionSource<MsgRecordState> taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(object? sender, MsgRecordState state)
            {
                if (IsFinalState(state))
                {
                    taskCompletionSource.TrySetResult(state);
                }
            }

            msgRecord.MsgRecordStateChanged += Handler;
            try
            {
                if (IsFinalState(msgRecord.MsgRecordState))
                {
                    return msgRecord.MsgRecordState;
                }

                return await taskCompletionSource.Task;
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= Handler;
            }
        }

        private static bool IsFinalState(MsgRecordState state)
        {
            return state is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout;
        }

        private static int TryReadMsgReturnInt(MsgReturn? msgReturn, string propertyName)
        {
            try
            {
                if (msgReturn?.Data == null)
                {
                    return 0;
                }

                dynamic data = msgReturn.Data;
                object? value = propertyName switch
                {
                    "MasterId" => data.MasterId,
                    "Port" => data.Port,
                    _ => null
                };

                return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private async void btnRunFlow_Click(object sender, RoutedEventArgs e)
        {
            TemplateModel<FlowParam>? flowTemplate = GetSelectedFlowTemplate();
            if (flowTemplate == null)
            {
                MessageBox.Show("请选择流程", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus($"正在执行流程: {flowTemplate.Key}", Brushes.DodgerBlue);

                FlowControlData? result = await FlowEngineManager.GetInstance().DisplayFlow.RunFlowAndWaitAsync(flowTemplate);
                if (result == null)
                {
                    SetOperationStatus("流程未启动", Brushes.OrangeRed);
                    return;
                }

                if (result.FlowStatus != FlowStatus.Completed)
                {
                    SetOperationStatus($"流程结束: {result.FlowStatus}", Brushes.OrangeRed);
                    MessageBox.Show($"流程执行未完成: {result.FlowStatus}\n{result.Params}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetOperationStatus("流程完成，正在查找 CVCIE 结果", Brushes.DodgerBlue);
                string? filePath = await WaitForFlowCvcieAsync(result);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    OpenConoscope(filePath);
                    SetOperationStatus($"已打开 {Path.GetFileName(filePath)}", Brushes.LimeGreen);
                }
                else
                {
                    SetOperationStatus("流程结果未找到 CVCIE", Brushes.OrangeRed);
                    MessageBox.Show("流程执行完成，但结果中没有找到 .cvcie 文件", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus("流程执行失败", Brushes.OrangeRed);
                MessageBox.Show($"流程执行失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetOperationBusy(false);
            }
        }

        private async void btnCaptureCamera_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show("请选择相机设备", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus($"正在拍照: {camera.Config.Name}", Brushes.DodgerBlue);

                MsgRecord msgRecord = camera.DService.GetData(
                    GetCameraExpTimes(camera),
                    GetSelectedCalibrationParam(),
                    new AutoExpTimeParam { Id = -1, Name = string.Empty },
                    new TemplateJsonParam { Id = -1, Name = string.Empty });

                MsgRecordState state = await WaitForMsgRecordAsync(msgRecord);
                if (state != MsgRecordState.Success)
                {
                    SetOperationStatus($"拍照失败: {state}", Brushes.OrangeRed);
                    MessageBox.Show($"拍照失败: {state}\n{msgRecord.MsgReturn?.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetOperationStatus("拍照完成，正在查找 CVCIE 结果", Brushes.DodgerBlue);
                string? filePath = await WaitForCameraCvcieAsync(msgRecord);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    OpenConoscope(filePath);
                    SetOperationStatus($"已打开 {Path.GetFileName(filePath)}", Brushes.LimeGreen);
                }
                else
                {
                    SetOperationStatus("拍照结果未找到 CVCIE", Brushes.OrangeRed);
                    MessageBox.Show("拍照完成，但结果中没有找到 .cvcie 文件", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus("拍照失败", Brushes.OrangeRed);
                MessageBox.Show($"拍照失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetOperationBusy(false);
            }
        }

        private async void btnSetNDPort_Click(object sender, RoutedEventArgs e)
        {
            DeviceCamera? camera = GetSelectedCamera();
            if (camera == null)
            {
                MessageBox.Show("请选择相机设备", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtNDPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port))
            {
                MessageBox.Show("请输入有效的 ND 端口", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus($"正在切换 ND: {port}", Brushes.DodgerBlue);
                camera.Config.NDPort = port;
                MsgRecord msgRecord = camera.DService.SetNDPort();
                MsgRecordState state = await WaitForMsgRecordAsync(msgRecord);
                if (state == MsgRecordState.Success)
                {
                    if (!ApplyNdCalibrationBinding(camera, port, reportStatus: true))
                    {
                        SetOperationStatus($"ND 已切换到 {port}", Brushes.LimeGreen);
                    }
                }
                else
                {
                    SetOperationStatus($"ND 切换失败: {state}", Brushes.OrangeRed);
                    MessageBox.Show($"ND 切换失败: {state}\n{msgRecord.MsgReturn?.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus("ND 切换失败", Brushes.OrangeRed);
                MessageBox.Show($"ND 切换失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("请选择相机设备", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetOperationBusy(true);
                SetOperationStatus("正在读取 ND", Brushes.DodgerBlue);
                MsgRecord msgRecord = camera.DService.GetPort();
                MsgRecordState state = await WaitForMsgRecordAsync(msgRecord);
                if (state == MsgRecordState.Success)
                {
                    int port = TryReadMsgReturnInt(msgRecord.MsgReturn, "Port");
                    camera.Config.NDPort = port;
                    txtNDPort.Text = port.ToString(CultureInfo.InvariantCulture);
                    if (!ApplyNdCalibrationBinding(camera, port, reportStatus: true))
                    {
                        SetOperationStatus($"当前 ND: {port}", Brushes.LimeGreen);
                    }
                }
                else
                {
                    SetOperationStatus($"ND 读取失败: {state}", Brushes.OrangeRed);
                    MessageBox.Show($"ND 读取失败: {state}\n{msgRecord.MsgReturn?.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                SetOperationStatus("ND 读取失败", Brushes.OrangeRed);
                MessageBox.Show($"ND 读取失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void btnSaveWindowConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
                ConfigService.Instance.Save<ConoscopeWindowConfig>();
                ConfigService.Instance.Save<FlowEngineConfig>();
                SetOperationStatus("配置已保存", Brushes.LimeGreen);
                MessageBox.Show("配置已保存", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetOperationStatus("配置保存失败", Brushes.OrangeRed);
                MessageBox.Show($"配置保存失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    OpenConoscope(filename);
                }
            }
        }

        private void btnNewView_Click(object sender, RoutedEventArgs e)
        {
            AddConoscopeView(null, activate: true);
            SetOperationStatus("已新建视图", Brushes.LimeGreen);
        }

        private void btnRefreshCameraDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshCameraDevices();
            SetOperationStatus("相机列表已刷新", Brushes.LimeGreen);
        }

        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.AdvancedExport();
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingModelSelection || cbModelType.SelectedItem is not ConoscopeModelType conoscopeModelType)
            {
                return;
            }

            ConoscopeManager.GetInstance().Config.CurrentModel = conoscopeModelType;
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            if (observationCameraWindow != null && observationCameraWindow.IsVisible)
            {
                observationCameraWindow.Activate();
                return;
            }

            observationCameraWindow = new MVSViewWindow();
            observationCameraWindow.Closed += (s, e) =>
            {
                observationCameraWindow = null;
                tbObservationCameraStatus.Text = Properties.Resources.NotOpened;
                tbObservationCameraStatus.Foreground = Brushes.Gray;
            };
            tbObservationCameraStatus.Text = "已打开";
            tbObservationCameraStatus.Foreground = Brushes.LimeGreen;
            observationCameraWindow.Show();
        }

        public void Dispose()
        {
            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ServiceManager.GetInstance().ServiceChanged -= ServiceManager_ServiceChanged;
            if (themeChangedHandler != null)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= themeChangedHandler;
                themeChangedHandler = null;
            }

            foreach (ConoscopeView view in GetOpenViews())
            {
                view.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
