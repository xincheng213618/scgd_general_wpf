using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Ghost
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayGhost : UserControl
    {
        public AlgorithmGhost IAlgorithm { get; set; }
        public DisplayGhost(AlgorithmGhost fOVAlgorithm)
        {
            IAlgorithm = fOVAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplateGhost.Params;
            ComboxTemplate.SelectedIndex = 0;
            ComboxCVOLEDCOLOR.ItemsSource = from e1 in Enum.GetValues<CVOLEDCOLOR>().Cast<CVOLEDCOLOR>()
                                            select new KeyValuePair<string, CVOLEDCOLOR>(e1.ToString(), e1);
            ComboxCVOLEDCOLOR.SelectedIndex = 0;        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!ServicesHelper.IsTemplateSelected(ComboxTemplate, "请先选择Ghost模板")) return;

            if (ComboxTemplate.SelectedValue is not GhostParam param) return;


            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                
                MsgRecord msg = IAlgorithm.SendCommand(code, type, imgFileName, fileExtType, param);
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
