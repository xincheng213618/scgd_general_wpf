using ColorVision.Common.Utilities;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate
{
    public partial class SfrSimplePlotWindow : Window
    {
        public SfrSimplePlotWindow()
        {
            InitializeComponent();
        }

        public void SetData(double[] frequencies, double[] sfrValues, 
            double mtf10Norm, double mtf50Norm, double mtf10CyPix, double mtf50CyPix,
            string label = "SFR")
        {
            Plot.SetData(frequencies, sfrValues, label);
            
            TxtMtf50Norm.Text = $"{mtf50Norm:F5}";
            TxtMtf50CyPix.Text = $"{mtf50CyPix:F5}";
            TxtMtf10Norm.Text = $"{mtf10Norm:F5}";
            TxtMtf10CyPix.Text = $"{mtf10CyPix:F5}";
        }

        private void BtnMtfAtFreq_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtFreq.Text, out var f))
            {
                TxtResult.Text = "频率输入错误";
                return;
            }

            if (Plot.TryEvaluateMtfAtFrequency(f, out var mtf))
                TxtResult.Text = $"MTF({f:F4}) = {mtf:F5}";
            else
                TxtResult.Text = "计算失败";
        }

        private void BtnFreqAtMtf_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtMtf.Text, out var m))
            {
                TxtResult.Text = "MTF输入错误";
                return;
            }

            if (Plot.TryEvaluateFrequencyAtMtf(m, out var freq))
                TxtResult.Text = $"Freq(MTF={m:F4}) = {freq:F5}";
            else
                TxtResult.Text = "计算失败";
        }
    }
}
