using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.SFR
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySFR : UserControl
    {
        public AlgorithmSFR IAlgorithm { get; set; }
        public DisplaySFR(AlgorithmSFR fOVAlgorithm)
        {
            IAlgorithm = fOVAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxSFRTemplate.ItemsSource = TemplateSFR.Params;
            ComboxSFRTemplate.SelectedIndex = 0;
            ComboxPoiTemplate2.ItemsSource = TemplatePoi.Params;
            ComboxPoiTemplate2.SelectedIndex = 0;        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxSFRTemplate, "请先选择SFR模板")) return;

            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                var pm = TemplateSFR.Params[ComboxSFRTemplate.SelectedIndex].Value;
                string type = string.Empty;
                string code = string.Empty;
                
                MsgRecord msg = IAlgorithm.SendCommand(code, type, imgFileName, fileExtType, pm.Id, ComboxSFRTemplate.Text);
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
