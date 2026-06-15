using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIOutput;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Themes.Controls;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayPoi : UserControl
    {
        public AlgorithmPoi IAlgorithm { get; set; }
        public DisplayPoi(AlgorithmPoi fOVAlgorithm)
        {
            IAlgorithm = fOVAlgorithm;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(EngineCommands.TakePhotoCommand, RunTemplate_Click, (s, e) => e.CanExecute = true));
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.Focusable = true;
            DataContext = IAlgorithm;

            ComboxPoiTemplate.ItemsSource = TemplatePoi.Params;
            ComboxPoiTemplate.SelectedIndex = 0;

            ComboxPoiFilter.ItemsSource = TemplatePoiFilterParam.Params.CreateEmpty();
            ComboxPoiFilter.SelectedIndex = 0;

            ComboxPoiOutput.ItemsSource = TemplatePoiOutputParam.Params.CreateEmpty();
            ComboxPoiOutput.SelectedIndex = 0;

            ComboxPoiRevise.ItemsSource = TemplatePoiReviseParam.Params.CreateEmpty();
            ComboxPoiRevise.SelectedIndex = 0;

            CBPOIStorageModel.ItemsSource = EnumExtensions.ToKeyValuePairs<POIStorageModel>();

            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().GetImageSourceServices();
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();
        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxPoiTemplate, "请先选择关注点模板")) return;
            if (!ServicesHelper.IsTemplateSelected(ComboxPoiFilter, "需要选择关注点过滤模板")) return;

            if (ComboxPoiTemplate.SelectedValue is not PoiParam poiParam) return;
            if (ComboxPoiFilter.SelectedValue is not PoiFilterParam pOIFilterParam) return;
            if (ComboxPoiRevise.SelectedValue is not PoiReviseParam pOICalParam) return;
            if (ComboxPoiOutput.SelectedValue is not PoiOutputParam poiOutputParam) return;


            if (TryGetImageInput(out string imgFileName))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                MsgRecord msg = IAlgorithm.SendCommand(code, type, imgFileName, poiParam, pOIFilterParam, pOICalParam, poiOutputParam);
                ServicesHelper.SendCommand(sender, msg);
            }

        }

        private bool TryGetImageInput(out string imgFileName)
        {
            imgFileName = LoaclFileTabItem.IsSelected ? ImageFile.Text : CB_CIEImageFiles.Text;
            if (string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }

            return true;
        }


        private void Button_Click_RawRefresh(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is not DeviceService deviceService) return;
            IAlgorithm.DService.GetCIEFiles(deviceService.Code, deviceService.ServiceTypes.ToString());
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                IAlgorithm.DService.Open(deviceService.Code, deviceService.ServiceTypes.ToString(), CB_CIEImageFiles.Text, FileExtType.CIE);
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.cvcie)|*.cvcie|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

        private void Button_OpenLocal_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(ImageFile.Text))
            {
                MessageBox.Show("找不到图像文件");
                return;
            }
            IAlgorithm.Device.View.ImageView.OpenImage(ImageFile.Text);
        }
    }
}
