using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Calibration
{


    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibrationControl : UserControl, IDisPlayControl
    {

        public DeviceCalibration Device { get; set; }
        private MQTTCalibration DeviceService { get => Device.DeviceService;  }
        public string DisPlayName => Device.Config.Name;

        public DisplayCalibrationControl(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();
            DeviceService.MsgReturnReceived += Service_OnCalibrationEvent;
            PreviewMouseDown += UserControl_PreviewMouseDown;

        }

        public ViewCalibration View { get=> Device.View; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;
            Device.ConfigChanged += (s, e) =>
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams;
                ComboxCalibrationTemplate.SelectedIndex = 0;
            };
            PhyCameraManager.GetInstance().Loaded += (s, e) =>
            {
                ComboxCalibrationTemplate.ItemsSource = Device.PhyCamera?.CalibrationParams;
                ComboxCalibrationTemplate.SelectedIndex = 0;
            };
            
            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private void Service_OnCalibrationEvent(MsgReturn arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    switch (data.FileExtType)
                    {
                        case FileExtType.Raw:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
                                CB_RawImageFiles.ItemsSource = data.Files;
                                CB_RawImageFiles.SelectedIndex = 0;
                            });
                            break;
                        case FileExtType.Src:
                            break;
                        case FileExtType.CIE:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                //CB_CIEImageFiles.ItemsSource = data.Files;
                                //CB_CIEImageFiles.SelectedIndex = 0;
                            });
                            break;
                        case FileExtType.Calibration:
                            break;
                        case FileExtType.Tif:
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "请先配置物理相机", "ColorVision");
                return;
            }

            if (sender is Button button)
            {

                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    string sn = string.Empty;
                    string imgFileName = ImageFile.Text;
                    FileExtType fileExtType = FileExtType.Tif;

                    if (GetSN(ref sn, ref imgFileName, ref fileExtType))
                    {
                        var pm = Device.PhyCamera.CalibrationParams[ComboxCalibrationTemplate.SelectedIndex].Value;

                        MsgRecord msgRecord = DeviceService.Calibration(param, imgFileName, fileExtType, pm.Id, ComboxCalibrationTemplate.Text, sn, (float)Device.Config.ExpTimeR, (float)Device.Config.ExpTimeG, (float)Device.Config.ExpTimeB);
                        ServicesHelper.SendCommand(button, msgRecord);
                    }
                }
            }
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }
        private void doOpen(string fileName, FileExtType extType)
        {
            DeviceService.Open(fileName, extType);
        }
        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CB_RawImageFiles.Text))
            {
                MessageBox.Show("请先选中图片");
                return;
            }
            doOpen(CB_RawImageFiles.Text, FileExtType.Raw);
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            DeviceService.GetRawFiles();
        }
        public TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }
            if (sender is Button button)
            {
                WindowTemplate windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        var ITemplate = new TemplateCalibrationParam(Device.PhyCamera);
                        windowTemplate = new WindowTemplate(ITemplate);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info(Engine.Properties.Resources.UnderDevelopment);
                        break;
                }
            }
        }



        private bool GetSN(ref string sn, ref string imgFileName, ref FileExtType fileExtType)
        {
            bool? isSN = AlgBatchSelect.IsChecked;
            bool? isRaw = AlgRawSelect.IsChecked;
            if (isSN.HasValue && isSN.Value)
            {
                if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                {
                    MessageBox.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                    return false;
                }
                sn = AlgBatchCode.Text;
                imgFileName = string.Empty;
            }
            else if (isRaw.HasValue && isRaw.Value)
            {
                imgFileName = CB_RawImageFiles.Text;
                fileExtType = FileExtType.Raw;
                sn = string.Empty;
            }
            else
            {
                imgFileName = ImageFile.Text;
                fileExtType = FileExtType.Tif;
                sn = string.Empty;
            }
            if (string.IsNullOrWhiteSpace(sn) && string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }
            return true;
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}
