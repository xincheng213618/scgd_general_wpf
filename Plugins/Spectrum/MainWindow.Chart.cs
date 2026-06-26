#pragma warning disable CA1805,CA1822,CS8604
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using Spectrum.Data;
using SpectrumResources = Spectrum.Properties.Resources;
using ScottPlot;
using ScottPlot.Plottables;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        private WindowCIE? _cieWindow;
        private CieMarker? _currentCieMarker;

        public void DrawCIEPoinr(double fx, double fy ,double fu,double fv)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (ThreadStart)delegate ()
                    {
                        UpdateCieSelection(fx, fy, fu, fv);
                    });
                    return;
                }

                UpdateCieSelection(fx, fy, fu, fv);
            }
            catch
            {

            }

        }

        private void UpdateCieSelection(double fx, double fy, double fu, double fv)
        {
            CieChromaticity xy = new(fx, fy);
            if (!IsUsableChromaticity(xy))
            {
                xy = CieColorConverter.Uv1976ToXy(new CieChromaticity(fu, fv));
            }

            _currentCieMarker = IsUsableChromaticity(xy)
                ? new CieMarker(SpectrumResources.SampleLabel, xy, System.Windows.Media.Colors.Red)
                : null;

            _cieWindow?.SetSelectedMarker(_currentCieMarker);
        }

        private void OpenCieWindow_Click(object sender, RoutedEventArgs e)
        {
            ShowCieWindow();
        }

        internal void ShowCieWindow()
        {
            if (_cieWindow == null)
            {
                _cieWindow = new WindowCIE { Owner = this };
                _cieWindow.Closed += CieWindow_Closed;
            }

            _cieWindow.SetSelectedMarker(_currentCieMarker);
            _cieWindow.Show();
            _cieWindow.Activate();
        }

        private void CieWindow_Closed(object? sender, EventArgs e)
        {
            if (_cieWindow != null)
            {
                _cieWindow.Closed -= CieWindow_Closed;
                _cieWindow = null;
            }
        }

        private void ClearCieSelection()
        {
            _currentCieMarker = null;
            _cieWindow?.SetSelectedMarker(null);
        }

        private void CloseCieWindow()
        {
            if (_cieWindow == null)
            {
                return;
            }

            WindowCIE window = _cieWindow;
            _cieWindow = null;
            window.Closed -= CieWindow_Closed;
            window.Close();
        }

        private static bool IsUsableChromaticity(CieChromaticity xy)
        {
            return xy.IsFinite
                && (Math.Abs(xy.X) > double.Epsilon || Math.Abs(xy.Y) > double.Epsilon)
                && xy.X >= 0
                && xy.X <= 1
                && xy.Y >= 0
                && xy.Y <= 1;
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
                SpectrumTypeText.Text = SpectrumResources.AbsoluteSpectrum;
            }
            else
            {
                wpfplot1.Visibility = Visibility.Visible;
                wpfplot2.Visibility = Visibility.Collapsed;
                SpectrumTypeText.Text = SpectrumResources.相对光谱;
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
