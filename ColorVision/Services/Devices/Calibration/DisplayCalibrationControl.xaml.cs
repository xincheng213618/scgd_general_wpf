using ColorVision.Common.Utilities;
using ColorVision.Net;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Interfaces;
using ColorVision.Services.Msg;
using ColorVision.Solution;
using ColorVision.Themes;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        public DisplayCalibrationControl(DeviceCalibration device)
        {
            this.Device = device;
            InitializeComponent();
            netFileUtil = new NetFileUtil(SolutionManager.GetInstance().CurrentSolution.FullName + "\\Cache");
            netFileUtil.handler += NetFileUtil_handler;
            DeviceService.OnMessageRecved += Service_OnCalibrationEvent;
            this.PreviewMouseDown += UserControl_PreviewMouseDown;

        }

        private void Service_OnCalibrationEvent(object sender, MessageRecvArgs arg)
        {
            switch (arg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(arg.Data));
                    DoShowFileList(data);
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
                    //View.OpenImage(arg.FileData);
                });
                handler?.Close();
            }
            else
            {
                handler?.Close();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    //MessageBox.IsShow(Application.Current.MainWindow, "文件打开失败", "ColorVision");
                });
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Device;
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
                    MsgRecord msgRecord = DeviceService.Calibration(param, ImageFile.Text, Device.Config.ExpTimeR, Device.Config.ExpTimeG, Device.Config.ExpTimeB );
                    Helpers.SendCommand(button, msgRecord);

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
    }
}
