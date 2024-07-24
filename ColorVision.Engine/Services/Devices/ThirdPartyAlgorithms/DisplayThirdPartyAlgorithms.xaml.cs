#pragma warning disable CS8604,CS0168,CS8629,CA1822,CS8602
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using CVCommCore;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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


        public AlgorithmView View { get => Device.View; }

        public string DisPlayName => Device.Config.Name;

        private IPendingHandler? handler { get; set; }

        private NetFileUtil netFileUtil;

        public DisplayThirdPartyAlgorithms(DeviceThirdPartyAlgorithms device)
        {
            Device = device;
            InitializeComponent();

            netFileUtil = new NetFileUtil();
            netFileUtil.handler += NetFileUtil_handler;
        }

        private void NetFileUtil_handler(object sender, NetFileEvent arg)
        {
            if (arg.Code == 0 && arg.FileData.data != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    View.OpenImage(arg.FileData);
                });
            }

            handler?.Close();
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
                            });
                            break;
                        case FileExtType.Src:
                            break;
                        case FileExtType.CIE:
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                data.Files.Reverse();
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
                case MQTTFileServerEventEnum.Event_File_Upload:
                    DeviceFileUpdownParam pm_up = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    netFileUtil.TaskStartUploadFile(pm_up.IsLocal, pm_up.ServerEndpoint, pm_up.FileName);
                    break;
                case MQTTFileServerEventEnum.Event_File_Download:
                    DeviceFileUpdownParam pm_dl = JsonConvert.DeserializeObject<DeviceFileUpdownParam>(JsonConvert.SerializeObject(arg.Data));
                    if (pm_dl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(pm_dl.FileName)) netFileUtil.TaskStartDownloadFile(pm_dl.IsLocal, pm_dl.ServerEndpoint, pm_dl.FileName, FileExtType.CIE);
                    }
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
                    else
                    {
                        resultMaster = AlgResultMasterDao.Instance.GetAllByBatchCode(arg.SerialNumber);
                    }
                    foreach (AlgResultMasterModel result in resultMaster)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Device.View.AlgResultMasterModelDataDraw(result);
                        });
                    }
                    handler?.Close();
                    break;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            ComboxTemplateFindDotsArray.ItemsSource = TemplateThirdParty.Params.GetValue("findDotsArrayImp");
            ComboxTemplateFindDotsArray.SelectedIndex = 0;

            CTRebuildPixelsImp.ItemsSource = TemplateThirdParty.Params.GetValue("rebuildPixelsImp");
            CTRebuildPixelsImp.SelectedIndex = 0;

            CTFindPixelDefectsForRebuildPicImp.ItemsSource = TemplateThirdParty.Params.GetValue("findPixelDefectsForRebuildPicImp");
            CTFindPixelDefectsForRebuildPicImp.SelectedIndex = 0;

            CTFindPixelDefectsForRebuildPicGradingImp.ItemsSource = TemplateThirdParty.Params.GetValue("findPixelDefectsForRebuildPicGradingImp");
            CTFindPixelDefectsForRebuildPicGradingImp.SelectedIndex = 0;

            CTFindParticlesForRebuildPicImp.ItemsSource = TemplateThirdParty.Params.GetValue("findParticlesForRebuildPicImp");
            CTFindParticlesForRebuildPicImp.SelectedIndex = 0;


            CTFillParticlesImp.ItemsSource = TemplateThirdParty.Params.GetValue("fillParticlesImp");
            CTFillParticlesImp.SelectedIndex = 0;

            CTFindMuraImp.ItemsSource = TemplateThirdParty.Params.GetValue("findMuraImp");
            CTFindMuraImp.SelectedIndex = 0;

            CTFindLineImp.ItemsSource = TemplateThirdParty.Params.GetValue("findLineImp");
            CTFindLineImp.SelectedIndex = 0;

            CTCombineSpacingDataImp.ItemsSource = TemplateThirdParty.Params.GetValue("combineSpacingDataImp");
            CTCombineSpacingDataImp.SelectedIndex = 0;



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
#if (DEBUG == false)
            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += UpdateUI;
#endif
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
                handler = PendingBox.Show(Application.Current.MainWindow, "", "上传", true);
                handler.Cancelling += delegate
                {
                    handler?.Close();
                };
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
            if (sender is Button button && !string.IsNullOrEmpty(button.Tag.ToString()))
            {
                new WindowTemplate(new TemplateThirdParty(button.Tag.ToString())) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            }
        }

        private void FindDotsArray_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxTemplateFindDotsArray.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType,code, type );
        }

        private void RebuildPixelsImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTRebuildPixelsImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FindPixelDefectsForRebuildPicImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFindPixelDefectsForRebuildPicImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FindPixelDefectsForRebuildPicGradingImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFindPixelDefectsForRebuildPicGradingImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FindParticlesForRebuildPicImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFindParticlesForRebuildPicImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FillParticlesImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFillParticlesImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FindMuraImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFindMuraImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void FindLineImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTFindLineImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;

            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            string type = deviceService.ServiceTypes.ToString();
            string code = deviceService.Code;

            DService.CallFunction(findDotsArrayParam, sn, imgFileName, fileExtType, code, type);
        }

        private void CombineSpacingDataImp_Click(object sender, RoutedEventArgs e)
        {
            if (CTCombineSpacingDataImp.SelectedValue is not FindDotsArrayParam findDotsArrayParam) return;
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

            bool? isSN = AlgBatchSelect.IsChecked;
            bool? isRaw = AlgRawSelect.IsChecked;

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
            return true;
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

        private void Button_Click_RawOpen(object sender, RoutedEventArgs e)
        {

        }


    }
}
