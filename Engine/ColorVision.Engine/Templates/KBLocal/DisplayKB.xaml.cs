using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.Net;
using cvColorVision;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.KB
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayKB : UserControl
    {
        public AlgorithmKBLocal IAlgorithm { get; set; }
        public DisplayKB(AlgorithmKBLocal iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;
            ComboxTemplate.ItemsSource = TemplatePoi.Params;
            ComboxKBTemplate.ItemsSource = TemplateKB.Params;
        }


        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxTemplate.SelectedValue is not PoiParam poiParam) return;
            if (!File.Exists(IAlgorithm.LuminFile))
            {
                MessageBox.Show("请先选择标定文件");
                return;
            }
            string imgFileName = ImageFile.Text;
            if (!File.Exists(imgFileName))
            {
                MessageBox.Show("图像文件不存在");
                return;
            }

            string luminFile = IAlgorithm.LuminFile;
            OpenCvSharp.Mat image;
            if (CVFileUtil.IsCIEFile(imgFileName))
            {
                int index = CVFileUtil.ReadCIEFileHeader(imgFileName,out CVCIEFile cvcie);
                if (index > 0)
                {
                    CVFileUtil.ReadCIEFileData(imgFileName, ref cvcie, index);
                    if (cvcie.bpp == 16)
                    {
                        image = OpenCvSharp.Mat.FromPixelData(cvcie.cols, cvcie.rows, OpenCvSharp.MatType.CV_16UC(cvcie.channels), cvcie.data);

                    }
                    else
                    {
                        image = OpenCvSharp.Mat.FromPixelData(cvcie.cols, cvcie.rows, OpenCvSharp.MatType.CV_8UC(cvcie.channels), cvcie.data);

                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                 image = OpenCvSharp.Cv2.ImRead(imgFileName, OpenCvSharp.ImreadModes.Unchanged);
            }

            int width = image.Width;
            int height = image.Height;
            int channels = image.Channels();
            int bpp = image.ElemSize() * 8;
            IntPtr imgData = image.Data;
            KeyBoardDLL.CM_InitialKeyBoardSrc(width, height, bpp, channels, imgData, IAlgorithm.SaveProcessData, IAlgorithm.SaveFolderPath, IAlgorithm.Exp, luminFile,1);


            PoiParam.LoadPoiDetailFromDB(poiParam);

            string csvFilePath = IAlgorithm.SaveFolderPath + "\\output.csv";
            using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("Name,rect,HaloGray,haloGray1,KeyGray,KeyGray1");
                foreach (var item in poiParam.PoiPoints)
                {
                    if (item.PointType == RiPointTypes.Rect)
                    {
                        try
                        {
                            IRECT rect = new IRECT((int)(item.PixX - (int)item.PixWidth / 2), (int)(item.PixY - (int)item.PixHeight / 2), (int)item.PixWidth, (int)item.PixHeight);
                            float haloGray = -1;
                            uint haloGray1 = 0;
                            uint Keygray1 = 0;
                            if (CB_CalculateHalo.IsChecked == true)
                            {
                                haloGray = KeyBoardDLL.CM_CalculateHalo(rect, item.Param.HaloOutMOVE, item.Param.HaloThreadV, 15, IAlgorithm.SaveFolderPath + $"\\{item.Name}", ref haloGray1);
                                haloGray = (float)(haloGray * item.Param.HaloScale);
                            }
                            float keyGray = -1;
                            if (CB_CalculateKey.IsChecked == true)
                            {
                                keyGray = KeyBoardDLL.CM_CalculateKey(rect, item.Param.KeyOutMOVE, item.Param.KeyThreadV, IAlgorithm.SaveFolderPath + $"\\{item.Name}", ref Keygray1);
                                keyGray = (float)(keyGray * item.Param.KeyScale);
                            }
                            if (item.Name.Contains(',') || item.Name.Contains('\"'))
                            {
                                item.Name = $"\"{item.Name.Replace("\"", "\"\"")}\"";
                            }
                            writer.WriteLine($"{item.Name},{rect},{haloGray},{haloGray1},{keyGray},{Keygray1}");

                        }
                        catch
                        {
                            
                        }

                    }
                }
            }

            IntPtr pData = Marshal.AllocHGlobal(width * height * channels);

            int rw = 0; int rh = 0; int rBpp = 0;int rChannel = 0; 

            byte[] pDst1 = new byte[image.Cols * image.Rows * 3 * bpp];

            int result = KeyBoardDLL.CM_GetKeyBoardResult(ref rw, ref rh, ref rBpp, ref rChannel, pDst1);
            OpenCvSharp.Mat mat;
            if (rBpp == 8)
            {
               mat = OpenCvSharp.Mat.FromPixelData(rh, rw, OpenCvSharp.MatType.CV_8UC(rChannel), pDst1);

            }
            else
            {
                 mat = OpenCvSharp.Mat.FromPixelData(rh, rw, OpenCvSharp.MatType.CV_16UC(rChannel), pDst1);
            }

            string Imageresult = $"{IAlgorithm.SaveFolderPath}\\{Path.GetFileName(imgFileName)}_{poiParam.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
            mat.SaveImage(Imageresult);

            ImageView imageView = new();
            Window window = new() { Title = Properties.Resources.QuickPreview };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(Imageresult);
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }

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

        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {

        }

        private void Open_Raw_File(object sender, RoutedEventArgs e)
        {
        }

        private void GenTemplateKB_Click(object sender, RoutedEventArgs e)
        {
            TemplateJsonKBParamCoveretConfig.Instance.DoHalo = CB_CalculateHalo.IsChecked == true;
            TemplateJsonKBParamCoveretConfig.Instance.DoKey = CB_CalculateKey.IsChecked == true;
            new TemplateKB().OpenCreate();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TemplateJsonKBParamCoveretConfig.Instance.DoHalo = CB_CalculateHalo.IsChecked == true;
            TemplateJsonKBParamCoveretConfig.Instance.DoKey = CB_CalculateKey.IsChecked == true;
        }
    }
}
