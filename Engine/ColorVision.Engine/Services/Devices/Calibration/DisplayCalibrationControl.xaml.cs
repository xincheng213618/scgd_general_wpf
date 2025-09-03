﻿using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Calibration.Views;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO;

namespace ColorVision.Engine.Services.Devices.Calibration
{


    /// <summary>
    /// DisplaySMUControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayCalibrationControl : UserControl, IDisPlayControl
    {

        public DeviceCalibration Device { get; set; }
        private MQTTCalibration DeviceService { get => Device.DService;  }
        public string DisPlayName => Device.Config.Name;

        public DisplayCalibrationControl(DeviceCalibration device)
        {
            Device = device;
            InitializeComponent();
            DeviceService.MsgReturnReceived += Service_OnCalibrationEvent;

        }

        public ViewCalibration View { get=> Device.View; }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

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

            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration);
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();

            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);


            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };

                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                    SetVisibility(TextBlockUnInit, Visibility.Collapsed);
                }
                // Default state
                HideAllButtons();

                switch (status)
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
            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += UpdateUI;
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private void Service_OnCalibrationEvent(MsgReturn msg)
        {
            if (msg.DeviceCode !=  Device.Code) return;

            switch (msg.EventName)
            {
                case MQTTFileServerEventEnum.Event_File_List_All:
                    DeviceListAllFilesParam data = JsonConvert.DeserializeObject<DeviceListAllFilesParam>(JsonConvert.SerializeObject(msg.Data));
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
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(msg.Data));
                    if (pm_dl != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            View.ImageView.OpenImage(pm_dl.FileName);
                        });
                    }
                    break;
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
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.cvcie;*.cvraw|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            DeviceService.GetRawFiles(deviceService.Code, deviceService.ServiceTypes.ToString());
        }



        public TemplateControl TemplateControl { get; set; }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (Device.PhyCamera == null)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "在使用校正前，请先配置对映的物理相机", "ColorVision");
                return;
            }
            if (sender is Button button)
            {
                TemplateEditorWindow windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                    return;
                }
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
            bool? isSN = AlgBatchSelect.IsSelected;
            bool? isRaw = AlgRawSelect.IsSelected;
            if (isSN.HasValue && isSN.Value)
            {
                if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                {
                    MessageBox1.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
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
                MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
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

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                DeviceService.Open(deviceService.Code, deviceService.ServiceTypes.ToString(), CB_RawImageFiles.Text, FileExtType.CIE);
        }

        private void Button_OpenLocal_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(ImageFile.Text))
            {
                MessageBox.Show("找不到图像文件");
                return;
            }
            Device.View.ImageView.OpenImage(ImageFile.Text);
        }
    }
}
