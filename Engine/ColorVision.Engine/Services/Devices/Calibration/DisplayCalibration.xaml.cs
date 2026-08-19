using ColorVision.Database;
#pragma warning disable CS8601
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services;
using ColorVision.FileIO;
using ColorVision.ImageEditor.EditorTools.Filters;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using MQTTMessageLib.FileServer;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class DisplayCalibrationConfig : IDisplayConfigBase
    {
        [Browsable(false)]
        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; OnPropertyChanged(); } }
        private double _ExpTimeR = 10;

        [Browsable(false)]
        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; OnPropertyChanged(); } }
        private double _ExpTimeG = 10;

        [Browsable(false)]
        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; OnPropertyChanged(); } }
        private double _ExpTimeB = 10;

        [Category("高级设置")]
        [DisplayName("使用三通道曝光")]
        [Description("分别配置 R、G、B 三个曝光值；关闭时只使用一个曝光值。")]
        public bool IsAdvancedExposure { get => _IsAdvancedExposure; set { _IsAdvancedExposure = value; OnPropertyChanged(); } }
        private bool _IsAdvancedExposure;

        [Category("高级设置")]
        [DisplayName("使用本地校正")]
        [Description("使用进程内优化校正；关闭后通过 MQTT 校正服务执行。")]
        public bool UseLocalCalibration { get => _UseLocalCalibration; set { _UseLocalCalibration = value; OnPropertyChanged(); } }
        private bool _UseLocalCalibration = true;

        [Browsable(false)]
        public DisplayShaderFilterState DisplayShaderFilter { get => _DisplayShaderFilter; set { _DisplayShaderFilter = value ?? new DisplayShaderFilterState(); OnPropertyChanged(); } }
        private DisplayShaderFilterState _DisplayShaderFilter = new DisplayShaderFilterState();
    }

    /// <summary>
    /// DisplayCalibration.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibration : UserControl, IDisPlayControl,IDisposable
    {
        private bool _isInitialized;
        private bool _isDisposed;
        private DeviceStatusType _deviceStatus = DeviceStatusType.Unknown;

        public DeviceCalibration Device { get; set; }
        private MQTTCalibration DeviceService { get => Device.DService;  }
        public string DisPlayName => Device.Config.Name;

        public DisplayCalibration(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();

        }

        public ViewCalibration View { get=> Device.View; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            if (_isInitialized || _isDisposed)
                return;

            _isInitialized = true;
            DataContext = Device;
            EnsureTimedButtonOperations();

            UpdateCalibrationTemplates();
            Device.ConfigChanged += Device_ConfigChanged;
            PhyCameraManager.GetInstance().Loaded += PhyCameraManager_Loaded;
            this.AddViewConfig(View, DisPlayName);
            this.ApplyChangedSelectedColor(DisPlayBorder);

            ImageFile.TextChanged += ImageFile_TextChanged;
            Device.DisplayConfig.PropertyChanged += DisplayConfig_PropertyChanged;

            UpdateFileExposureInfo(ImageFile.Text);
            DService_DeviceStatusChanged(sender, Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += DService_DeviceStatusChanged;
        }

        private void Device_ConfigChanged(object? sender, EventArgs e) => UpdateCalibrationTemplates();

        private void PhyCameraManager_Loaded(object? sender, EventArgs e) => UpdateCalibrationTemplates();

        private void UpdateCalibrationTemplates()
        {
            if (_isDisposed)
                return;

            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            if (_isDisposed)
                return;

            _deviceStatus = e;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(RefreshServiceState);
                return;
            }
            RefreshServiceState();
        }

        private void DisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DisplayCalibrationConfig.UseLocalCalibration))
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(RefreshServiceState);
                    return;
                }
                RefreshServiceState();
            }
        }

        private void RefreshServiceState()
        {
            if (_isDisposed)
                return;

            void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; }

            void HideAllButtons()
            {
                SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                SetVisibility(StackPanelContent, Visibility.Collapsed);
                SetVisibility(TextBlockUnInit, Visibility.Collapsed);
            }
            // Default state
            HideAllButtons();
            if (Device.DisplayConfig.UseLocalCalibration)
            {
                SetVisibility(StackPanelContent, Visibility.Visible);
                return;
            }

            switch (_deviceStatus)
            {
                case DeviceStatusType.Unauthorized:
                    SetVisibility(ButtonUnauthorized, Visibility.Visible);
                    break;
                case DeviceStatusType.Unknown:
                    SetVisibility(TextBlockUnknow, Visibility.Visible);
                    break;
                case DeviceStatusType.OffLine:
                    break;
                case DeviceStatusType.UnInit:
                    SetVisibility(TextBlockUnInit, Visibility.Visible);
                    break;
                case DeviceStatusType.Closed:
                    break;
                case DeviceStatusType.LiveOpened:
                case DeviceStatusType.Opened:
                    SetVisibility(StackPanelContent, Visibility.Visible);
                    break;
                case DeviceStatusType.Closing:
                case DeviceStatusType.Opening:
                default:
                    // No specific action needed
                    break;
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private async void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.BeforeCalibrationSetupPhysicalCamera, "ColorVision");
                return;
            }

            if (sender is Button button)
            {
                EnsureTimedButtonOperations();

                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    string sn = string.Empty;
                    string imgFileName = ImageFile.Text;
                    FileExtType fileExtType = FileExtType.Tif;

                    if (GetSN(ref sn, ref imgFileName, ref fileExtType))
                    {
                        var pm = Device.PhyCamera.CalibrationParams[ComboxCalibrationTemplate.SelectedIndex].Value;
                        float[] exposure = GetExposureValues();
                        if (Device.DisplayConfig.UseLocalCalibration)
                        {
                            await RunLocalCalibrationAsync(button, pm, imgFileName, exposure);
                        }
                        else
                        {
                            MsgRecord msgRecord = DeviceService.Calibration(param, imgFileName, fileExtType, pm.Id, ComboxCalibrationTemplate.Text, sn, exposure[0], exposure[1], exposure[2]);
                            this.SendTimedCommand(button, msgRecord, onTerminalStateChanged: state =>
                            {
                                if (state == MsgRecordState.Fail)
                                {
                                    MessageBox.Show(Application.Current.GetActiveWindow(), $"Fail,{msgRecord.MsgReturn.Message}", "ColorVision");
                                }
                            });
                        }
                    }
                }
            }
        }

        private float[] GetExposureValues()
        {
            float exposure = (float)Device.DisplayConfig.ExpTimeR;
            return Device.DisplayConfig.IsAdvancedExposure
                ? new[] { exposure, (float)Device.DisplayConfig.ExpTimeG, (float)Device.DisplayConfig.ExpTimeB }
                : new[] { exposure, exposure, exposure };
        }

        private async Task RunLocalCalibrationAsync(Button button, CalibrationParam calibration, string imageFileName, float[] exposure)
        {
            TimedButtonOperationRegistry operations = EnsureTimedButtonOperations();
            TimedButtonOperationScope? operationScope = operations.Begin(button);
            bool succeeded = false;
            try
            {
                string serialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                MeasureResultImgModel result = await Task.Run(() => LocalFileCalibrationService.Calibrate(
                    Device,
                    calibration,
                    imageFileName,
                    serialNumber,
                    exposure));
                if (_isDisposed) return;

                Device.View.ShowResult(result);
                succeeded = true;
            }
            catch (Exception ex)
            {
                if (!_isDisposed)
                {
                    MessageBox1.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                }
            }
            finally
            {
                if (!_isDisposed)
                {
                    operationScope?.Complete(succeeded);
                    operations.RefreshIdleState(button);
                }
            }
        }

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(BuildButtonOperationKey);
            operations.Register(CalibrationButton, options =>
            {
                options.ExpectedDurationProvider = () => Math.Max(500, GetExposureValues().Sum());
            });
            return operations;
        }

        private string BuildButtonOperationKey(string actionKey)
        {
            return $"calibration:{Device.Config.Code}:{actionKey}";
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = ServicesHelper.ImageFileDialogFilter;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

        private void UpdateFileExposureInfo(string filePath)
        {
            bool isCVFile = false;
            bool fileExists = !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath.Trim());
            if (fileExists)
            {
                string normalizedPath = filePath.Trim();
                string ext = Path.GetExtension(normalizedPath).ToLowerInvariant();
                if (ext == ".cvraw" || ext == ".cvcie")
                {
                    int headerEnd = CVFileUtil.ReadCIEFileHeader(normalizedPath, out CVCIEFile cvcie);
                    if (headerEnd > 0 && cvcie.Exp != null && cvcie.Exp.Any(value => value > 0))
                    {
                        isCVFile = true;
                        Device.DisplayConfig.ExpTimeR = cvcie.Exp[0];
                        Device.DisplayConfig.ExpTimeG = cvcie.Exp[Math.Min(1, cvcie.Exp.Length - 1)];
                        Device.DisplayConfig.ExpTimeB = cvcie.Exp[Math.Min(2, cvcie.Exp.Length - 1)];
                    }
                }
            }
            ExposurePanel.Visibility = fileExists ? Visibility.Visible : Visibility.Collapsed;
            TextBoxExp.IsReadOnly = isCVFile;
            SliderExp.IsEnabled = !isCVFile;
            TextBoxExpR.IsReadOnly = isCVFile;
            SliderExpR.IsEnabled = !isCVFile;
            TextBoxExpG.IsReadOnly = isCVFile;
            SliderExpG.IsEnabled = !isCVFile;
            TextBoxExpB.IsReadOnly = isCVFile;
            SliderExpB.IsEnabled = !isCVFile;
        }

        private void ImageFile_TextChanged(object sender, TextChangedEventArgs e) => UpdateFileExposureInfo(ImageFile.Text);

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfigurePhysicalCameraBeforeCalibration, "ColorVision");
                return;
            }
            if (sender is Button button)
            {
                TemplateEditorWindow windowTemplate;
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        var ITemplate = new TemplateCalibrationParam(Device.PhyCamera);
                        windowTemplate = new TemplateEditorWindow(ITemplate);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info(Properties.Resources.UnderDevelopment);
                        break;
                }
            }
        }



        private bool GetSN(ref string sn, ref string imgFileName, ref FileExtType fileExtType)
        {
            imgFileName = ImageFile.Text;
            fileExtType = FileExtType.Tif;
            sn = string.Empty;

            if (string.IsNullOrWhiteSpace(sn) && string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.ImageFileCannotBeEmpty, "ColorVision");
                return false;
            }
            if (Path.GetExtension(imgFileName).Contains("cvraw"))
            {
                fileExtType = FileExtType.Raw;
            }
            else if (Path.GetExtension(imgFileName).Contains("cvcie"))
            {
                fileExtType = FileExtType.CIE;
            }
            else if (Path.GetExtension(imgFileName).Contains("tif"))
            {
                fileExtType = FileExtType.Tif;
            }
            else
            {
                fileExtType = FileExtType.Src;
            }
            return true;
        }


        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            Device.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            Device.ConfigChanged -= Device_ConfigChanged;
            PhyCameraManager.GetInstance().Loaded -= PhyCameraManager_Loaded;
            ImageFile.TextChanged -= ImageFile_TextChanged;
            Device.DisplayConfig.PropertyChanged -= DisplayConfig_PropertyChanged;
            this.DisposeTimedButtonOperations();
            ComboxCalibrationTemplate.ItemsSource = null;
            DataContext = null;
            GC.SuppressFinalize(this);
        }
    }
}
