using ColorVision.Common.MVVM;
using ColorVision.Net;
using ColorVision.Solution.V.Files;
using ColorVision.Common.Utilities;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Services.Devices.Camera.Views
{
    public class VExportCIE : ViewModelBase
    {
        public VExportCIE(string filePath)
        {
            FilePath = filePath;
            FileExtType = filePath.Contains(".cvraw") ? FileExtType.Raw : filePath.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;
            if (FileExtType == FileExtType.CIE)
            {
                IsExportChannelX = true;
                IsExportChannelY = true;
                IsExportChannelZ = true;

                if (CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile cVCIEFile) > 0 && !string.IsNullOrEmpty(cVCIEFile.srcFileName))
                {
                    if (!File.Exists(cVCIEFile.srcFileName))
                        cVCIEFile.srcFileName = Path.Combine(Path.GetDirectoryName(filePath)??string.Empty, Path.GetFileNameWithoutExtension(filePath) + ".cvraw");
                    if (File.Exists(cVCIEFile.srcFileName))
                    {
                        IsExportSrc = CVFileUtil.ReadCIEFileHeader(cVCIEFile.srcFileName ,out CVCIEFile cvraw) > 0;
                    }
                }
            }else if (FileExtType == FileExtType.Raw)
            {
                IsExportSrc = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile cvraw) > 0;
            }


        }

        public bool IsCVRaw { get => FileExtType == FileExtType.Raw; }

        public bool IsCVCIE { get => FileExtType == FileExtType.CIE; }


        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;

        public string SavePath { get => _SavePath; set { _SavePath = value; NotifyPropertyChanged(); } }
        private string _SavePath;

        public FileExtType FileExtType { get => _FileExtType; set { _FileExtType = value; NotifyPropertyChanged(); } }
        private FileExtType _FileExtType;
        public bool IsExportChannelX { get => _IsExportChannelX; set { _IsExportChannelX = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelX;

        public bool IsExportChannelY { get => _IsExportChannelY; set { _IsExportChannelY = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelY;

        public bool IsExportChannelZ { get => _IsExportChannelZ; set { _IsExportChannelZ = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelZ;

        public bool IsExportSrc { get => _IsExportSrc; set { _IsExportSrc = value; NotifyPropertyChanged(); } }
        private bool _IsExportSrc;
        public bool IsExportChannelR { get => _IsExportChannelR; set { _IsExportChannelR = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelR;

        public bool IsExportChannelG { get => _IsExportChannelG; set { _IsExportChannelG = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelG;

        public bool IsExportChannelB { get => _IsExportChannelB; set { _IsExportChannelB = value; NotifyPropertyChanged(); } }
        private bool _IsExportChannelB;

    }


    /// <summary>
    /// ExportCamera.xaml 的交互逻辑
    /// </summary>
    public partial class ExportCamera : Window
    {

        public string CIEFilePath { get; set; }
        public VExportCIE VExportCIE { get; set; }

        public ExportCamera(string cIEFilePath)
        {
            VExportCIE = new VExportCIE(cIEFilePath);
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            if (!CVFileUtil.IsCIEFile(VExportCIE.FilePath))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "导出仅支持CIE文件", "ColorVision");
                return;
            }
            this.DataContext = VExportCIE;

        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                TextBoxSave.Text = dialog.SelectedPath;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

            CVFileUtil.SaveToTif(CIEFilePath,"");
            this.Close();
        }


    }
}
