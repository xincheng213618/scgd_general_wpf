using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using cvColorVision;
using MQTTMessageLib.FileServer;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using static OpenCvSharp.ML.SVM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.KB
{
    /// <summary>
    /// DisplaySFR.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayKB : UserControl
    {
        public AlgorithmKB IAlgorithm { get; set; }
        public DisplayKB(AlgorithmKB iAlgorithm)
        {
            IAlgorithm = iAlgorithm;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = IAlgorithm;

        }

        private void RunTemplate_Click(object sender, RoutedEventArgs e)
        {
            IRECT rect = new IRECT(877, 983, 1200, 1290);
            float exposure = 1.0f;
            string luminFile = "C:\\Users\\17917\\Desktop\\cfg\\4colorCaliForDemo.dat";
            Mat image = Cv2.ImRead("C:\\Users\\17917\\Desktop\\0926keyboard\\200-16.tif", ImreadModes.Color);

            int width = image.Width;
            int height = image.Height;
            int channels = image.Channels();
            int bpp = image.ElemSize() * 8;

            IntPtr imgData = image.Data;

            KeyBoardDLL.CM_InitialKeyBoardSrc(width, height, 16, 1, imgData, 1, 100, luminFile);
            float haloGray = KeyBoardDLL.CM_CalculateHalo(rect, 20, 128, 18, "path/to/save");
            float keyGray = KeyBoardDLL.CM_CalculateKey(rect, 30, 128, "path/to/save");
            IntPtr pData = Marshal.AllocHGlobal(width * height * channels);
            int result = KeyBoardDLL.CM_GetKeyBoardResult(ref width, ref height, ref bpp, ref channels, pData);

            Console.WriteLine($"Halo Gray: {haloGray}, Key Gray: {keyGray}, Result: {result}");
        }
    }
}
