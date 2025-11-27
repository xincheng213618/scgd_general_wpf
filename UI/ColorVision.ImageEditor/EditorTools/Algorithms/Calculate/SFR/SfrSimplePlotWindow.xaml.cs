using ColorVision.Common.Utilities;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR
{
    public partial class SfrSimplePlotWindow : Window
    {
        private double _mtf10Norm;
        private double _mtf50Norm;
        private double _mtf10CyPix;
        private double _mtf50CyPix;
        
        private bool _isMultiChannel = false;
        private double _mtf10NormR, _mtf50NormR, _mtf10CyPixR, _mtf50CyPixR;
        private double _mtf10NormG, _mtf50NormG, _mtf10CyPixG, _mtf50CyPixG;
        private double _mtf10NormB, _mtf50NormB, _mtf10CyPixB, _mtf50CyPixB;
        private double _mtf10NormL, _mtf50NormL, _mtf10CyPixL, _mtf50CyPixL;

        public SfrSimplePlotWindow()
        {
            InitializeComponent();
        }

        public void SetData(double[] frequencies, double[] sfrValues, 
            double mtf10Norm, double mtf50Norm, double mtf10CyPix, double mtf50CyPix,
            string label = "SFR")
        {
            _isMultiChannel = false;
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

        public void SetMultiChannelData(double[] frequencies,
            double[] sfrR, double[] sfrG, double[] sfrB, double[] sfrL,
            double mtf10R, double mtf50R, double mtf10cR, double mtf50cR,
            double mtf10G, double mtf50G, double mtf10cG, double mtf50cG,
            double mtf10B, double mtf50B, double mtf10cB, double mtf50cB,
            double mtf10L, double mtf50L, double mtf10cL, double mtf50cL)
        {
            _isMultiChannel = true;
            Plot.SetMultiChannelData(frequencies, sfrR, sfrG, sfrB, sfrL);
            
            _mtf10NormR = mtf10R; _mtf50NormR = mtf50R; _mtf10CyPixR = mtf10cR; _mtf50CyPixR = mtf50cR;
            _mtf10NormG = mtf10G; _mtf50NormG = mtf50G; _mtf10CyPixG = mtf10cG; _mtf50CyPixG = mtf50cG;
            _mtf10NormB = mtf10B; _mtf50NormB = mtf50B; _mtf10CyPixB = mtf10cB; _mtf50CyPixB = mtf50cB;
            _mtf10NormL = mtf10L; _mtf50NormL = mtf50L; _mtf10CyPixL = mtf10cL; _mtf50CyPixL = mtf50cL;
            
            // Display L channel by default (most important)
            _mtf10Norm = mtf10L;
            _mtf50Norm = mtf50L;
            _mtf10CyPix = mtf10cL;
            _mtf50CyPix = mtf50cL;
            
            // Show all channel values
            TxtMtf50Norm.Text = $"R:{mtf50R:F4} G:{mtf50G:F4} B:{mtf50B:F4} L:{mtf50L:F4}";
            TxtMtf50CyPix.Text = $"R:{mtf50cR:F4} G:{mtf50cG:F4} B:{mtf50cB:F4} L:{mtf50cL:F4}";
            TxtMtf10Norm.Text = $"R:{mtf10R:F4} G:{mtf10G:F4} B:{mtf10B:F4} L:{mtf10L:F4}";
            TxtMtf10CyPix.Text = $"R:{mtf10cR:F4} G:{mtf10cG:F4} B:{mtf10cB:F4} L:{mtf10cL:F4}";
            
            // Show channel visibility controls
            TxtChannelVisibility.Visibility = Visibility.Visible;
            ChkShowR.Visibility = Visibility.Visible;
            ChkShowG.Visibility = Visibility.Visible;
            ChkShowB.Visibility = Visibility.Visible;
            ChkShowL.Visibility = Visibility.Visible;
        }

        private void ChkChannel_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isMultiChannel) return;
            
            Plot.SetChannelVisibility(
                ChkShowR.IsChecked == true,
                ChkShowG.IsChecked == true,
                ChkShowB.IsChecked == true,
                ChkShowL.IsChecked == true);
        }

        private void CmbQueryChannel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_isMultiChannel || Plot == null) return;
            
            string channel = CmbQueryChannel.SelectedIndex switch
            {
                0 => "L",
                1 => "R",
                2 => "G",
                3 => "B",
                _ => "L"
            };
            
            Plot.SetQueryChannel(channel);
        }

        private void BtnMtfAtFreq_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtFreq.Text, out var freq))
            {
                TxtResult.Text = "频率输入错误";
                return;
            }

            string channel = Plot.GetQueryChannel();
            double mtf = Plot.FindMtfAtFreq(freq);
            if (!double.IsNaN(mtf))
                TxtResult.Text = $"[{channel}] MTF(Freq={freq:F4}) = {mtf:F5}";
            else
                TxtResult.Text = $"[{channel}] 未找到对应MTF";
        }

        private void BtnFreqAtMtf_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtMtf.Text, out var m))
            {
                TxtResult.Text = "MTF输入错误";
                return;
            }

            string channel = Plot.GetQueryChannel();
            double freq = Plot.FindFreqAtThreshold(m);
            if (freq > 0)
                TxtResult.Text = $"[{channel}] Freq(MTF={m:F4}) = {freq:F5}";
            else
                TxtResult.Text = $"[{channel}] 未找到对应频率";
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
                    
                    if (_isMultiChannel)
                    {
                        // Multi-channel export
                        csvBuilder.AppendLine("# Multi-Channel SFR Data Export");
                        csvBuilder.AppendLine($"# R Channel - MTF10 (norm): {_mtf10NormR:F5}, MTF50 (norm): {_mtf50NormR:F5}, MTF10 (cy/pix): {_mtf10CyPixR:F5}, MTF50 (cy/pix): {_mtf50CyPixR:F5}");
                        csvBuilder.AppendLine($"# G Channel - MTF10 (norm): {_mtf10NormG:F5}, MTF50 (norm): {_mtf50NormG:F5}, MTF10 (cy/pix): {_mtf10CyPixG:F5}, MTF50 (cy/pix): {_mtf50CyPixG:F5}");
                        csvBuilder.AppendLine($"# B Channel - MTF10 (norm): {_mtf10NormB:F5}, MTF50 (norm): {_mtf50NormB:F5}, MTF10 (cy/pix): {_mtf10CyPixB:F5}, MTF50 (cy/pix): {_mtf50CyPixB:F5}");
                        csvBuilder.AppendLine($"# L Channel - MTF10 (norm): {_mtf10NormL:F5}, MTF50 (norm): {_mtf50NormL:F5}, MTF10 (cy/pix): {_mtf10CyPixL:F5}, MTF50 (cy/pix): {_mtf50CyPixL:F5}");
                        csvBuilder.AppendLine();
                        csvBuilder.AppendLine("Frequency,MTF_R,MTF_G,MTF_B,MTF_L");
                        
                        var multiData = Plot.GetMultiChannelData();
                        if (multiData != null)
                        {
                            var data = Plot.GetData();
                            if (data.frequencies != null && multiData.ContainsKey("R"))
                            {
                                int n = data.frequencies.Length;
                                for (int i = 0; i < n; i++)
                                {
                                    csvBuilder.AppendLine($"{data.frequencies[i]:F6},{multiData["R"][i]:F6},{multiData["G"][i]:F6},{multiData["B"][i]:F6},{multiData["L"][i]:F6}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Single channel export
                        csvBuilder.AppendLine("# SFR Data Export");
                        csvBuilder.AppendLine($"# MTF10 (normalized): {_mtf10Norm:F5}");
                        csvBuilder.AppendLine($"# MTF50 (normalized): {_mtf50Norm:F5}");
                        csvBuilder.AppendLine($"# MTF10 (cy/pix): {_mtf10CyPix:F5}");
                        csvBuilder.AppendLine($"# MTF50 (cy/pix): {_mtf50CyPix:F5}");
                        csvBuilder.AppendLine();
                        csvBuilder.AppendLine("Frequency,MTF");
                        
                        var data = Plot.GetData();
                        if (data.frequencies != null && data.sfrValues != null)
                        {
                            int n = Math.Min(data.frequencies.Length, data.sfrValues.Length);
                            for (int i = 0; i < n; i++)
                            {
                                csvBuilder.AppendLine($"{data.frequencies[i]:F6},{data.sfrValues[i]:F6}");
                            }
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
