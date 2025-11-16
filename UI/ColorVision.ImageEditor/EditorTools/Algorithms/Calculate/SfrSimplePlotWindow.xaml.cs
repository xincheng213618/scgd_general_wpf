using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate
{
    public partial class SfrSimplePlotWindow : Window
    {
        private double _mtf10Norm;
        private double _mtf50Norm;
        private double _mtf10CyPix;
        private double _mtf50CyPix;

        public SfrSimplePlotWindow()
        {
            InitializeComponent();
        }

        public void SetData(double[] frequencies, double[] sfrValues, 
            double mtf10Norm, double mtf50Norm, double mtf10CyPix, double mtf50CyPix,
            string label = "SFR")
        {
            Plot.SetData(frequencies, sfrValues, label);
            
            _mtf10Norm = mtf10Norm;
            _mtf50Norm = mtf50Norm;
            _mtf10CyPix = mtf10CyPix;
            _mtf50CyPix = mtf50CyPix;
            
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

        private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"SFR_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var csvBuilder = new StringBuilder();
                    
                    // Header with MTF values
                    csvBuilder.AppendLine("# SFR Data Export");
                    csvBuilder.AppendLine($"# MTF10 (normalized): {_mtf10Norm:F5}");
                    csvBuilder.AppendLine($"# MTF50 (normalized): {_mtf50Norm:F5}");
                    csvBuilder.AppendLine($"# MTF10 (cy/pix): {_mtf10CyPix:F5}");
                    csvBuilder.AppendLine($"# MTF50 (cy/pix): {_mtf50CyPix:F5}");
                    csvBuilder.AppendLine();
                    
                    // Column headers
                    csvBuilder.AppendLine("Frequency,MTF");
                    
                    // Data rows
                    var data = Plot.GetData();
                    if (data.frequencies != null && data.sfrValues != null)
                    {
                        int n = Math.Min(data.frequencies.Length, data.sfrValues.Length);
                        for (int i = 0; i < n; i++)
                        {
                            csvBuilder.AppendLine($"{data.frequencies[i]:F6},{data.sfrValues[i]:F6}");
                        }
                    }
                    
                    File.WriteAllText(saveDialog.FileName, csvBuilder.ToString(), Encoding.UTF8);
                    MessageBox.Show($"数据已成功导出到:\n{saveDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
