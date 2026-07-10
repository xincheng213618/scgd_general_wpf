using ColorVision.Engine.Messages;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Services;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayLEDStripDetectionV2 : UserControl
    {
        public AlgorithmLEDStripDetectionV2 IAlgorithm { get; set; }
        public DisplayLEDStripDetectionV2(AlgorithmLEDStripDetectionV2 iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            
            ComboxTemplate.ItemsSource = TemplateLEDStripDetectionV2.Params;
            ComboxTemplate.SelectedIndex = 0;

            ComboxPoiTemplate.ItemsSource = TemplatePoi.Params;
            ComboxPoiTemplate.SelectedIndex = 0;        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择模板")) return;
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
                MessageBox1.Show(Application.Current.MainWindow, Properties.Resources.ImageFileCannotBeEmpty, "ColorVision");
                return false;
            }

            fileExtType = ServicesHelper.ResolveFileExtType(imgFileName);
            return true;
        }



        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = ServicesHelper.ImageFileDialogFilter;
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }


    }
}
