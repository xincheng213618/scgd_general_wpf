using Spectrum.Data;
using Spectrum.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using ScottPlot;
using ScottPlot.Plottables;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum
{
    public partial class MainWindow
    {
        public void DrawCIEPoinr(double fx, double fy ,double fu,double fv)
        {
            try
            {
                if (src1931 != null)
                {
                    Mat cir1931 = src1931.Clone();
                    OpenCvSharp.Point p1;
                    p1.X = Convert.ToInt32(Math.Round(fx * 10 * 97 + 104));
                    p1.Y = Convert.ToInt32(Math.Round(881 - fy * 10 * 97));
                    Cv2.Circle(cir1931, p1.X, p1.Y, 10, new Scalar(0, 0, 255), -1, LineTypes.Link8, 0);
                    pic1931 = cir1931.ToWriteableBitmap();
                }
                if (src1976 != null)
                {
                    Mat cir1976 = src1976.Clone();
                    OpenCvSharp.Point p2;
                    p2.X = Convert.ToInt32(Math.Round(fu * 10 * 154 + 49));
                    p2.Y = Convert.ToInt32(Math.Round(973 - fv * 10 * 154));
                    Cv2.Circle(cir1976, p2.X, p2.Y, 10, new Scalar(0, 0, 255), -1, LineTypes.Link8, 0);
                    pic1976 = cir1976.ToWriteableBitmap();
                }
                // Update both CIE diagram images simultaneously
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
                {
                    image1931.Source = pic1931;
                    image1976.Source = pic1976;
                });
            }
            catch (Exception ex)
            {

            }

        }

        /// <summary>
        /// Updates the spectral parameter display panel with calculation details.
        /// Shows formulas and computed values to increase user credibility.
        /// </summary>
        private void UpdateCieParameterDisplay(ViewResultSpectrum result)
        {
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
            {
                // Dominant Wavelength
                TextDominantWavelength.Text = $"◆ 主波长 (Dominant Wavelength): {result.fLd:F2} nm\n"
                    + $"  从D65白点(0.3127, 0.3290)向样本色度点(x={result.fx:F4}, y={result.fy:F4})延伸，\n"
                    + $"  与CIE 1931光谱轨迹的交点对应波长即为主波长";

                // FWHM (Half-width)
                TextFWHM.Text = $"◆ 半波宽 FWHM: {result.fHW:F2} nm\n"
                    + $"  峰值波长 λp={result.fLp:F2} nm，在峰值强度50%处的两侧波长差 Δλ=λ₂−λ₁";

                // CCT (Correlated Color Temperature)
                TextCCT.Text = $"◆ 相关色温 CCT: {result.fCCT:F0} K\n"
                    + $"  McCamy近似: n=(x−0.3320)/(0.1858−y)，CCT=449n³+3525n²+6823.3n+5520.33";

                // Excitation Purity
                TextExcitationPurity.Text = $"◆ 激发纯度 (Excitation Purity): {result.ExcitationPurity:F4} ({result.ExcitationPurity * 100:F2}%)\n"
                    + $"  pe = √((x−xn)²+(y−yn)²) / √((xd−xn)²+(yd−yn)²)\n"
                    + $"  白点(xn,yn)=(0.3127,0.3290)，主波长点(xd,yd)由光谱轨迹查表获得\n"
                    + $"  色纯度: {result.fPur:F2}%";

                // Color Rendering Index
                TextColorRendering.Text = $"◆ 显色指数 Ra: {result.fRa:F1}\n"
                    + $"  基于CIE 1931 2°观察者标准，使用8组TCS测试色样，\n"
                    + $"  Ri=100−4.6×ΔEi，Ra=ΣRi/8";

                // Chromaticity coordinates
                TextChromaticity.Text = $"◆ 色度坐标:\n"
                    + $"  CIE 1931: x={result.fx:F4}, y={result.fy:F4}\n"
                    + $"  CIE 1976: u'={result.fu:F4}, v'={result.fv:F4}\n"
                    + $"  三刺激值: X={result.fCIEx:F4}, Y={result.fCIEy:F4}, Z={result.fCIEz:F4}";
            });
        }

        public List<Scatter> ScatterPlots => ViewResultManager.ScatterPlots;
        public List<Scatter> AbsoluteScatterPlots => ViewResultManager.AbsoluteScatterPlots;

        bool MulComparison;
        Scatter? LastMulSelectComparsion;
        private bool IsShowingAbsoluteSpectrum { get; set; } = false;

        private void DrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                DrawAbsolutePlot();
                return;
            }

            wpfplot1.Plot.Axes.SetLimitsX(380, 780);
            wpfplot1.Plot.Axes.SetLimitsY(-0.05, 1);
            wpfplot1.Plot.Axes.Bottom.Min = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect1;
            wpfplot1.Plot.Axes.Bottom.Max = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect2;
            wpfplot1.Plot.Axes.Left.Min = -0.05;
            wpfplot1.Plot.Axes.Left.Max = 1;

            if (ScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = ScatterPlots[ViewResultList.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot1.Plot.PlottableList.Add(LastMulSelectComparsion);

                }
                else
                {
                    var temp = ScatterPlots[ViewResultList.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot1.Plot.PlottableList.Add(temp);
                    wpfplot1.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;

                }
            }

            wpfplot1.Refresh();
        }

        private void DrawAbsolutePlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            wpfplot2.Plot.Axes.SetLimitsX(380, 780);
            wpfplot2.Plot.Axes.Bottom.Min = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect1;
            wpfplot2.Plot.Axes.Bottom.Max = ViewResultSpectrums[ViewResultList.SelectedIndex].fSpect2;
            wpfplot2.Plot.Axes.Left.Min = -0.05;
            wpfplot2.Plot.Axes.Left.Max = double.NaN;
            if (AbsoluteScatterPlots.Count > 0)
            {
                if (MulComparison)
                {
                    if (LastMulSelectComparsion != null)
                    {
                        LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        LastMulSelectComparsion.LineWidth = 1;
                        LastMulSelectComparsion.MarkerSize = 1;
                    }

                    LastMulSelectComparsion = AbsoluteScatterPlots[ViewResultList.SelectedIndex];
                    LastMulSelectComparsion.LineWidth = 3;
                    LastMulSelectComparsion.MarkerSize = 3;
                    LastMulSelectComparsion.Color = Color.FromColor(System.Drawing.Color.Red);
                    wpfplot2.Plot.PlottableList.Add(LastMulSelectComparsion);
                }
                else
                {
                    var temp = AbsoluteScatterPlots[ViewResultList.SelectedIndex];
                    temp.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                    temp.LineWidth = 1;
                    temp.MarkerSize = 1;

                    wpfplot2.Plot.PlottableList.Add(temp);
                    wpfplot2.Plot.Remove(LastMulSelectComparsion);
                    LastMulSelectComparsion = temp;
                }
            }

            wpfplot2.Refresh();
        }

        private void ToggleSpectrumType_Click(object sender, RoutedEventArgs e)
        {
            IsShowingAbsoluteSpectrum = !IsShowingAbsoluteSpectrum;

            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot1.Visibility = Visibility.Collapsed;
                wpfplot2.Visibility = Visibility.Visible;
                SpectrumTypeText.Text = "绝对光谱";
            }
            else
            {
                wpfplot1.Visibility = Visibility.Visible;
                wpfplot2.Visibility = Visibility.Collapsed;
                SpectrumTypeText.Text = "相对光谱";
            }

            ReDrawPlot();
        }

        private void ReDrawPlot()
        {
            if (ViewResultList.SelectedIndex < 0) return;

            if (IsShowingAbsoluteSpectrum)
            {
                wpfplot2.Plot.Clear();
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = AbsoluteScatterPlots[i];
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot2.Plot.PlottableList.Add(plot);
                    }
                }
                DrawAbsolutePlot();
            }
            else
            {
                wpfplot1.Plot.Clear();
                // Re-add spectrum color bar after clearing
                AddSpectrumColorBar(wpfplot1);
                LastMulSelectComparsion = null;
                if (MulComparison)
                {
                    ViewResultList.SelectedIndex = ViewResultList.Items.Count > 0 && ViewResultList.SelectedIndex == -1 ? 0 : ViewResultList.SelectedIndex;
                    for (int i = 0; i < ViewResultSpectrums.Count; i++)
                    {
                        if (i == ViewResultList.SelectedIndex) continue;
                        var plot = ScatterPlots[i];
                        plot.Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod);
                        plot.LineWidth = 1;
                        plot.MarkerSize = 1;
                        wpfplot1.Plot.PlottableList.Add(plot);
                    }
                }
                DrawPlot();
            }
        }

        /// <summary>
        /// Adds a visible spectrum rainbow color bar to the bottom of the chart.
        /// Uses ScottPlot Rectangle annotations for each wavelength step.
        /// </summary>
        private void AddSpectrumColorBar(ScottPlot.WPF.WpfPlot plotControl)
        {
            // Add colored rectangles from 380 to 780 nm
            for (int wl = 380; wl < 780; wl += 2)
            {
                var color = WavelengthToColor.Convert(wl);
                var scottColor = new ScottPlot.Color(color.R, color.G, color.B);

                var rect = plotControl.Plot.Add.Rectangle(wl, wl + 2, -0.01, -0.06);
                rect.FillColor = scottColor;
                rect.LineColor = scottColor;
                rect.LineWidth = 0;
            }
        }

        private void ButtonMul_Click(object sender, RoutedEventArgs e)
        {
            MulComparison = !MulComparison;
            if (ViewResultList.SelectedIndex <= -1)
            {
                if (ViewResultList.Items.Count == 0)
                    return;
                ViewResultList.SelectedIndex = 0;
            }
            ReDrawPlot();
        }
    }
}
