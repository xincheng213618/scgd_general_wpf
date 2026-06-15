using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.MTF
{

    /// <summary>
    /// DisplayImageCropping.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMTF : UserControl
    {
        public AlgorithmMTF IAlgorithm { get; set; }
        public DisplayMTF(AlgorithmMTF fOVAlgorithm)
        {
            IAlgorithm = fOVAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxMTFTemplate.ItemsSource = TemplateMTF.Params;
            ComboxMTFTemplate.SelectedIndex = 0;

            ComboxPoiTemplate2.ItemsSource = TemplatePoi.Params;
            ComboxPoiTemplate2.SelectedIndex = 0;

            void UpdateCB_SourceImageFiles()
            {
                CB_SourceImageFiles.ItemsSource = ServiceManager.GetInstance().GetImageSourceServices();
                CB_SourceImageFiles.SelectedIndex = 0;
            }
            ServiceManager.GetInstance().DeviceServices.CollectionChanged += (s, e) => UpdateCB_SourceImageFiles();
            UpdateCB_SourceImageFiles();
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
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files (*.*)|*.*";
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
                IAlgorithm.DService.Open(deviceService.Code, deviceService.ServiceTypes.ToString(), CB_RawImageFiles.Text, FileExtType.CIE);
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
        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxMTFTemplate, "请先选择MTF模板")) return;
            if (!ServicesHelper.IsTemplateSelected(ComboxPoiTemplate2, "请先选择关注点模板")) return;
            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                if (CB_SourceImageFiles.SelectedItem is DeviceService deviceService)
                {
                    type = deviceService.ServiceTypes.ToString();
                    code = deviceService.Code;
                }
                var pm = TemplateMTF.Params[ComboxMTFTemplate.SelectedIndex].Value;
                var poi_pm = TemplatePoi.Params[ComboxPoiTemplate2.SelectedIndex].Value;
                var msg = IAlgorithm.SendCommand(code, type, imgFileName, fileExtType, pm.Id, ComboxMTFTemplate.Text, poi_pm.Id, ComboxPoiTemplate2.Text);
                ServicesHelper.SendCommand(sender, msg);
            }
        }
    }
}
