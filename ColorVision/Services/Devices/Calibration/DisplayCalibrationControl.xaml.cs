using ColorVision.Common.Utilities;
using ColorVision.Net;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Devices.Calibration.Views;
using ColorVision.Services.Core;
using ColorVision.Services.Msg;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Solution;
using ColorVision.Themes;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Services.Dao;
using MQTTMessageLib.Camera;
using ColorVision.Extension;

namespace ColorVision.Services.Devices.Calibration
{


    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibrationControl : UserControl, IDisPlayControl
    {

        public DeviceCalibration Device { get; set; }
        private MQTTCalibration DeviceService { get => Device.DeviceService;  }
        private IPendingHandler? handler { get; set; }
        private NetFileUtil netFileUtil;
        public ObservableCollection<TemplateModel<CalibrationParam>> CalibrationParams { get; set; }
        public DisplayCalibrationControl(DeviceCalibration device)
        {
            this.Device = device;
            InitializeComponent();
            netFileUtil = new NetFileUtil(SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache");
            netFileUtil.handler += NetFileUtil_handler;
            DeviceService.OnMessageRecved += Service_OnCalibrationEvent;
            this.PreviewMouseDown += UserControl_PreviewMouseDown;

        }

        public ViewCalibration View { get=> Device.View; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;

            CalibrationParams = Device.CalibrationParams;
            ComboxCalibrationTemplate.ItemsSource = Device.CalibrationParams;
            ComboxCalibrationTemplate.SelectedIndex = 0;


            this.AddViewConfig(View, ComboxView);

        }


        MeasureImgResultDao measureImgResultDao = new MeasureImgResultDao();
        private void Service_OnCalibrationEvent(object sender, MessageRecvArgs arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    DoShowFileList(data);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    if (pm_dl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, FileExtType.CIE);
                    }
                    break;
                case MQTTCameraEventEnum.Event_GetData:
                    int masterId = Convert.ToInt32(arg.Data.MasterId);
                    List<MeasureImgResultModel> resultMaster = null;
                    if (masterId > 0)
                    {
                        resultMaster = new List<MeasureImgResultModel>();
                        MeasureImgResultModel model = measureImgResultDao.GetById(masterId);
                        if (model != null)
                            resultMaster.Add(model);
                    }
                    else
                    {
                        resultMaster = measureImgResultDao.GetAllByBatchCode(arg.SerialNumber);
                    }
                    if (resultMaster != null)
                    {
                        foreach (MeasureImgResultModel result in resultMaster)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Device.View.ShowResult(result);
                            });
                        }
                    }
                    break;
            }


        }
        private void DoShowFileList(DeviceListAllFilesParam data)
        {
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
        }
        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
                handler?.Close();
            }
            else
            {
                handler?.Close();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
            }
        }



        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");  } }
        private bool _IsSelected;

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
        }

        private void Calibration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (ComboxCalibrationTemplate.SelectedValue is CalibrationParam param)
                {
                    string sn = string.Empty;
                    string imgFileName = ImageFile.Text;
                    FileExtType fileExtType = FileExtType.Tif;

                    if (GetSN(ref sn, ref imgFileName, ref fileExtType))
                    {
                        var pm = CalibrationParams[ComboxCalibrationTemplate.SelectedIndex].Value;

                        MsgRecord msgRecord = DeviceService.Calibration(param, imgFileName, fileExtType, pm.Id, ComboxCalibrationTemplate.Text, sn, (float)Device.Config.ExpTimeR, (float)Device.Config.ExpTimeG, (float)Device.Config.ExpTimeB);
                        Helpers.SendCommand(button, msgRecord);
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
            string localName = netFileUtil.GetCacheFileFullName(fileName);
            if (string.IsNullOrEmpty(localName) || !System.IO.File.Exists(localName))
            {
                DeviceService.Open(fileName, extType);
            }
            else
            {
                netFileUtil.OpenLocalFile(localName, extType);
            }
        }
        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CB_RawImageFiles.Text))
            {
                MessageBox.Show("请先选中图片");
                return;
            }
            handler = PendingBox.Show(Application.Current.MainWindow, "", "打开图片", true);
            handler.Cancelling += delegate
            {
                handler?.Close();
            };
            doOpen(CB_RawImageFiles.Text, FileExtType.Raw);

        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            DeviceService.GetRawFiles();
        }
        public TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                TemplateControl = TemplateControl.GetInstance();
                SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                WindowTemplate windowTemplate;
                if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
                {
                    MessageBox.Show(Application.Current.MainWindow, Properties.Resource.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
                switch (button.Tag?.ToString() ?? string.Empty)
                {
                    case "Calibration":
                        CalibrationControl calibration;
                        if (Device.CalibrationParams.Count > 0)
                        {
                            calibration = new CalibrationControl(Device, Device.CalibrationParams[0].Value);
                        }
                        else
                        {
                            calibration = new CalibrationControl(Device);
                        }
                        windowTemplate = new WindowTemplate(TemplateType.Calibration, calibration, Device, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                    default:
                        HandyControl.Controls.Growl.Info(Properties.Resource.UnderDevelopment);
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
