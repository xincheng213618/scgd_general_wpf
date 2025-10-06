#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views;
using ColorVision.Database;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms
{
    /// <summary>
    /// DisplayAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayThirdPartyAlgorithms : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayThirdPartyAlgorithms));

        public DeviceThirdPartyAlgorithms Device { get; set; }

        public MQTTThirdPartyAlgorithms DService { get => Device.DService; }


        public ThirdPartyAlgorithmsView View { get => Device.View; }

        public string DisPlayName => Device.Config.Name;

        public DisplayThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device)
        {
            Device = device;
            InitializeComponent();

        }




        private void Service_OnAlgorithmEvent(MsgReturn arg)
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
                                data.Files.Reverse();
                                CB_RawImageFiles.ItemsSource = data.Files;
                                CB_RawImageFiles.SelectedIndex = 0;
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
                    break;
                default:
                    List<AlgResultMasterModel> resultMaster = null;
                    if (arg.Data.MasterId > 0)
                    {
                        resultMaster = new List<AlgResultMasterModel>();
                        int MasterId = arg.Data.MasterId;
                        AlgResultMasterModel model = AlgResultMasterDao.Instance.GetById(MasterId);
                        resultMaster.Add(model);
                    }
                    foreach (AlgResultMasterModel result in resultMaster)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Device.View.AlgResultMasterModelDataDraw(result);
                        });
                    }
                    break;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;


            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });

            void ConfigChanged()
            {
                CB_ThirdPartyAlgorithms.ItemsSource = ThirdPartyAlgorithmsDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "pid", Device.DLLModel?.Id } });
                CB_ThirdPartyAlgorithms.SelectedIndex = 0;
            }
            Device.ConfigChanged += (s, e) => ConfigChanged();
            ConfigChanged();

            void ThirdPartyAlgorithmsChanged()
            {
                if (CB_ThirdPartyAlgorithms.SelectedValue is not ThirdPartyAlgorithmsModel model) return;

                CB_Templates.ItemsSource = TemplateThirdParty.Params.GetValue(model.Code);
                CB_Templates.SelectedIndex = 0;
            }
            CB_ThirdPartyAlgorithms.SelectionChanged += (s, e) => ThirdPartyAlgorithmsChanged();
            ThirdPartyAlgorithmsChanged();

            this.AddViewConfig(View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);

            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration);
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();

            UpdateCB_SourceImageFiles();
            DService.MsgReturnReceived += Service_OnAlgorithmEvent;

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility){ if (element.Visibility != visibility) element.Visibility = visibility; };
                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
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
                    default:
                        SetVisibility(StackPanelContent, Visibility.Visible);
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

        private bool IsTemplateSelected(ComboBox comboBox, string errorMessage)
        {
            if (comboBox.SelectedIndex == -1)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), errorMessage, "ColorVision");
                return false;
            }
            return true;
        }
           

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            DService.GetCIEFiles(code, type);
        }

        private void Button_Click_Upload(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "CVCIE files (*.cvcie) | *.cvcie";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DService.UploadCIEFile(openFileDialog.FileName);
            }
        }




        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }





        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            string type = string.Empty;
            string code = string.Empty;
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
            {
                type = deviceService.ServiceTypes.ToString();
                code = deviceService.Code;
            }
            DService.GetRawFiles(code, type);
        }


        private void TemplateSetting_Click(object sender, RoutedEventArgs e)
        {
            if (CB_ThirdPartyAlgorithms.SelectedValue is not ThirdPartyAlgorithmsModel model) return;

            new TemplateEditorWindow(new TemplateThirdParty(model.Code)){ Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
        private void Templates_Click(object sender, RoutedEventArgs e)
        {
            if (CB_Templates.SelectedValue is not TemplateJsonParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }


        private bool GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)
        {
            sn = string.Empty;
            fileExtType = FileExtType.Tif;
            imgFileName = string.Empty;

            bool? isSN = AlgBatchSelect.IsSelected;
            bool? isRaw = AlgRawSelect.IsSelected;

            if (isSN == true)
            {
                if (string.IsNullOrWhiteSpace(AlgBatchCode.Text))
                {
                    MessageBox1.Show(Application.Current.MainWindow, "批次号不能为空，请先输入批次号", "ColorVision");
                    return false;
                }
                sn = AlgBatchCode.Text;
            }
            else if (isRaw == true)
            {
                imgFileName = CB_RawImageFiles.Text;
                fileExtType = FileExtType.Raw;
            }
            else
            {
                imgFileName = ImageFile.Text;
            }
            if (string.IsNullOrWhiteSpace(imgFileName))
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

        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {

        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {

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
