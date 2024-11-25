using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using cvColorVision;
using MQTTMessageLib.FileServer;
using OpenCvSharp;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.KB
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayKBLocal : UserControl
    {
        public AlgorithmKBLocal IAlgorithm { get; set; }
        public DisplayKBLocal(AlgorithmKBLocal iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplatePoi.Params;
        }


        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!AlgorithmHelper.IsTemplateSelected(ComboxTemplate, "请先选择关注点模板")) return;
            if (ComboxTemplate.SelectedValue is not PoiParam poiParam) return;
            if (!GetAlgSN(out string sn, out string imgFileName, out FileExtType fileExtType)) return;

            if (!File.Exists(IAlgorithm.LuminFile))
            {
                MessageBox.Show("请先选择标定文件");
                return;
            }
            string luminFile = IAlgorithm.LuminFile;

            Mat image = Cv2.ImRead(imgFileName, ImreadModes.Unchanged);
            int width = image.Width;
            int height = image.Height;
            int channels = image.Channels();
            int bpp = image.ElemSize() * 8;
            IntPtr imgData = image.Data;
            KeyBoardDLL.CM_InitialKeyBoardSrc(width, height, bpp, channels, imgData, 1, 100, luminFile);
            PoiParam.LoadPoiDetailFromDB(poiParam);

            string csvFilePath = IAlgorithm.SaveFolderPath + "\\output.csv";
            using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("Name,HaloGray,KeyGray");
                foreach (var item in poiParam.PoiPoints)
                {
                    if (item.PointType == RiPointTypes.Rect)
                    {
                        try
                        {
                            IRECT rect = new IRECT((int)(item.PixX - (int)item.PixWidth / 2), (int)(item.PixY - (int)item.PixHeight / 2), (int)item.PixWidth, (int)item.PixHeight);
                            float haloGray = -1;
                            if (CB_CalculateHalo.IsChecked == true)
                            {
                                 haloGray = KeyBoardDLL.CM_CalculateHalo(rect, 20, IAlgorithm.HaloThreadV, 15, IAlgorithm.SaveFolderPath + $"\\{item.Name}");
                            }
                            float keyGray = -1;
                            if (CB_CalculateKey.IsChecked == true)
                            {
                                 keyGray = KeyBoardDLL.CM_CalculateKey(rect, 35, IAlgorithm.KeyThreadV, IAlgorithm.SaveFolderPath + $"\\{item.Name}");
                            }
                            if (item.Name.Contains(',') || item.Name.Contains('"'))
                            {
                                item.Name = $"\"{item.Name.Replace("\"", "\"\"")}\"";
                            }
                            writer.WriteLine($"{item.Name},{haloGray},{keyGray}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }

                    }
                }
            }
            IntPtr pData = Marshal.AllocHGlobal(width * height * channels);
            int rw = 0; int rh = 0; int rBpp = 0;int rChannel = 0; ;
            byte[] pDst1 = new byte[image.Cols * image.Rows * 3 * 16];
            int result = KeyBoardDLL.CM_GetKeyBoardResult(ref rw, ref rh, ref rBpp, ref rChannel, pDst1);
            Mat mat = Mat.FromPixelData( rh,rw ,MatType.CV_16UC3, pDst1);
            string Imageresult = $"{IAlgorithm.SaveFolderPath}\\{Path.GetFileName(imgFileName)}_{poiParam.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
            mat.SaveImage(Imageresult);
            IAlgorithm.Device.View.ImageView.OpenImage(Imageresult);
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
            return true;
        }

        private void Open_File(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png, *.tif)|*.jpg;*.jpeg;*.png;*.tif|All files (*.*)|*.*";
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

        private void Open_Raw_File(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ImageFile.Text))
            {
                IAlgorithm.Device.View.ImageView.OpenImage(ImageFile.Text);
            }
        }
    }
}
