using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
using cvColorVision;
using MQTTMessageLib.FileServer;
using System;
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
            IRECT rect = new IRECT(0, 0, 100, 50);
            int width = 1920, height = 1080, bpp = 24, channels = 3;
            float exposure = 1.0f;
            string luminFile = "path/to/luminFile";

            IntPtr imgData = IntPtr.Zero; // Assume this is initialized with image data
            KeyBoardDLL.CM_InitialKeyBoardSrc(width, height, bpp, channels, imgData, 0, exposure, luminFile);
            float haloGray = KeyBoardDLL.CM_CalculateHalo(rect, 20, 128, 18, "path/to/save");

            float keyGray = KeyBoardDLL.CM_CalculateKey(rect, 30, 128, "path/to/save");
            IntPtr pData = IntPtr.Zero; // Assume this is initialized with sufficient memory
            int result = KeyBoardDLL.CM_GetKeyBoardResult(ref width, ref height, ref bpp, ref channels, pData);

            Console.WriteLine($"Halo Gray: {haloGray}, Key Gray: {keyGray}, Result: {result}");
        }
    }
}
