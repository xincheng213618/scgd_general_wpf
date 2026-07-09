using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.DetectScreenDefects
{
    public partial class DisplayDetectScreenDefects : UserControl
    {
        public AlgorithmDetectScreenDefects IAlgorithm { get; set; }

        public DisplayDetectScreenDefects(AlgorithmDetectScreenDefects iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;

            ComboxTemplate.ItemsSource = TemplateDetectScreenDefects.Params;
            if (TemplateDetectScreenDefects.Params.Count > 0)
                ComboxTemplate.SelectedIndex = 0;

            ComboxPoiTemplate.ItemsSource = TemplatePoi.Params;
            if (TemplatePoi.Params.Count > 0)
                ComboxPoiTemplate.SelectedIndex = 0;

            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().GetImageSourceServices();
                if (CB_SourceImageFiles.Items.Count > 0)
                    CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();
        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择屏幕缺陷检测模板")) return;
            if (ComboxTemplate.SelectedValue is not TemplateJsonParam param) return;

            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }

                MsgRecord msg = IAlgorithm.SendCommand(param, code, type, imgFileName, fileExtType);
                ServicesHelper.SendCommand(sender, msg);
            }
        }

        private bool TryGetImageInput(out string imgFileName, out FileExtType fileExtType)
        {
            fileExtType = FileExtType.Tif;
            imgFileName = AlgRawSelect.IsSelected == true ? CB_RawImageFiles.Text : ImageFile.Text;

            if (string.IsNullOrWhiteSpace(imgFileName))
            {
                MessageBox1.Show(Application.Current.MainWindow, "图像文件不能为空，请先选择图像文件", "ColorVision");
                return false;
            }

            fileExtType = ServicesHelper.ResolveFileExtType(imgFileName);
            return true;
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif, *.tiff, *.cvcie, *.cvraw)|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.cvcie;*.cvraw|All files (*.*)|*.*";
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
            IAlgorithm.DService.GetRawFiles(deviceService.Code, deviceService.ServiceTypes.ToString());
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                IAlgorithm.DService.Open(deviceService.Code, deviceService.ServiceTypes.ToString(), CB_RawImageFiles.Text, ServicesHelper.ResolveFileExtType(CB_RawImageFiles.Text));
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
