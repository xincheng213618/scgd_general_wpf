using ColorVision.Engine.Services;
using ColorVision.Themes.Controls;
using MQTTMessageLib.FileServer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Matching
{

    /// <summary>
    /// DisplayImageCropping.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayMatching : UserControl
    {
        public AlgorithmMatching IAlgorithm { get; set; }
        public DisplayMatching(AlgorithmMatching fOVAlgorithm)
        {
            IAlgorithm = fOVAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplateMatch.Params;        }


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
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif;*.tiff|All files (*.*)|*.*";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ImageFile.Text = openFileDialog.FileName;
            }
        }





        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (TryGetImageInput(out string imgFileName, out FileExtType fileExtType))
            {
                string type = string.Empty;
                string code = string.Empty;
                
                var msg = IAlgorithm.SendCommand(code, type, imgFileName, fileExtType);
                ServicesHelper.SendCommand(sender, msg);
            }
        }

    }
}
