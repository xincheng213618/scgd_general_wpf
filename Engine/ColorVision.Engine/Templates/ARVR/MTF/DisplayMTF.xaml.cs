using ColorVision.Engine.Templates.MTF;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.MTF
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

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!AlgorithmHelper.IsTemplateSelected(ComboxMTFTemplate, "请先选择MTF模板")) return;
            if (!AlgorithmHelper.IsTemplateSelected(ComboxPoiTemplate2, "请先选择关注点模板")) return;
            if (GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType))
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
                var ss = IAlgorithm.SendCommand(code, type, imgFileName, fileExtType, pm.Id, ComboxMTFTemplate.Text, sn, poi_pm.Id, ComboxPoiTemplate2.Text);
                ServicesHelper.SendCommand(ss, "MTF");
            }
        }
    }
}
