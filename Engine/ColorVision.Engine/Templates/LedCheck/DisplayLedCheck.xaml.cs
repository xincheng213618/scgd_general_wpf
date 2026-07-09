using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.LedCheck
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayLedCheck : UserControl
    {
        public AlgorithmLedCheck IAlgorithm { get; set; }
        public DisplayLedCheck(AlgorithmLedCheck iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplateLedCheck.Params;
            ComboxTemplate.SelectedIndex = 0;

            ComboxPoiTemplate.ItemsSource = TemplatePoi.Params.CreateEmpty();
            ComboxPoiTemplate.SelectedIndex = 0;        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择灯珠检测模板")) return;
            if (!ServicesHelper.IsTemplateSelected(ComboxPoiTemplate, "请先选择关注点模板")) return;

            if (ComboxTemplate.SelectedValue is not LedCheckParam param) return;
            if (ComboxPoiTemplate.SelectedValue is not PoiParam poiParam) return;

            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                
                MsgRecord msg = IAlgorithm.SendCommand(param, poiParam,code, type, imgFileName, fileExtType);
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
