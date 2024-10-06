using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Themes.Controls;
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

        }
    }
}
