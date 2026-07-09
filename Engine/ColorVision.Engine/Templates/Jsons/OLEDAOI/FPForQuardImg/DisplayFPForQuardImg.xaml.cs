using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg
{
    /// <summary>
    /// DisplayFPForQuardImg.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayFPForQuardImg : UserControl
    {
        public AlgorithmFPForQuardImg IAlgorithm { get; set; }
        public DisplayFPForQuardImg(AlgorithmFPForQuardImg iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;

            ComboxTemplate.ItemsSource = TemplateFPForQuardImg.Params;
            ComboxTemplate.SelectedIndex = 0;

            ComboxPoiTemplate2.ItemsSource = TemplatePoi.Params;
            ComboxPoiTemplate2.SelectedIndex = 0;        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择亮点检测模板")) return;

            if (ComboxTemplate.SelectedValue is not TemplateJsonParam param) return;

            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                
                MsgRecord msg = IAlgorithm.SendCommand(param, code, type, imgFileName, fileExtType);
                ServicesHelper.SendCommand(sender, msg);
            }
        }

        private bool TryGetImageInput(out string imgFileName, out FileExtType fileExtType)
        {
            fileExtType = FileExtType.Tif;
            imgFileName = ImageFile.Text;

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
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.cvcie;*.cvraw|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }

    }
}
