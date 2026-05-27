using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializePlot(ScottPlot.WPF.WpfPlot plot, string title)
        {
            plot.Plot.Title(title);
            plot.Plot.XLabel("Degrees");
            plot.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(ExportChannel.Y));
            plot.Plot.Legend.FontName = ScottPlot.Fonts.Detect("中文");

            string fontSample = "中文 Luminance Voltage";
            plot.Plot.Axes.Title.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect(fontSample);
            plot.Plot.Axes.Bottom.Label.FontName = ScottPlot.Fonts.Detect(fontSample);

            plot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromColor(System.Drawing.Color.LightGray);
            plot.Plot.Grid.MajorLineWidth = 1;
            plot.Plot.Axes.SetLimits(-MaxAngle, MaxAngle, 0, 600);

            plot.Refresh();
        }

        private void UpdateReferencePlotDisplayMode()
        {
            bool isPolar = referencePlotDisplayMode == ReferencePlotDisplayMode.Polar;

            if (wpfPlotReference != null)
            {
                wpfPlotReference.Visibility = isPolar ? Visibility.Collapsed : Visibility.Visible;
            }

            if (polarPlotReference != null)
            {
                polarPlotReference.Visibility = isPolar ? Visibility.Visible : Visibility.Collapsed;
            }

            if (tglReferencePolarMode != null && tglReferencePolarMode.IsChecked != isPolar)
            {
                tglReferencePolarMode.IsChecked = isPolar;
            }
        }

        private void tglReferencePolarMode_Checked(object sender, RoutedEventArgs e)
        {
            referencePlotDisplayMode = ReferencePlotDisplayMode.Polar;
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlot();
        }

        private void tglReferencePolarMode_Unchecked(object sender, RoutedEventArgs e)
        {
            referencePlotDisplayMode = ReferencePlotDisplayMode.Cartesian;
            UpdateReferencePlotDisplayMode();
            UpdateReferencePlot();
        }

        private void UpdateReferencePlotHeader()
        {
            ConoscopeCoordinateAxisParam axisParam = CoordinateAxisConfig;
            tbReferenceMode.Text = axisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine ? Properties.Resources.RefAzimuthLine : Properties.Resources.RefPolarCircle;
            tbReferenceValue.Text = GetReferenceValueText(axisParam.ReferenceMode, axisParam.ReferenceAngle, axisParam.ReferenceRadiusAngle);
        }

        private static string GetReferenceValueText(ConoscopeCoordinateReferenceMode mode, double angle, double radiusAngle)
        {
            return mode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? $"{angle:F2}°"
                : $"R={radiusAngle:F2}°";
        }

        private void SetReferencePlotLimits()
        {
            if (CoordinateAxisConfig.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                wpfPlotReference.Plot.Axes.SetLimitsX(-MaxAngle, MaxAngle);
            }
            else
            {
                wpfPlotReference.Plot.Axes.SetLimitsX(0, 360);
            }
        }

        private void UpdateReferencePlot()
        {
            if (CoordinateAxisConfig.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdatePlot();
            }
            else
            {
                UpdatePlotForCircle();
            }
        }

        private static SolidColorBrush GetChannelPlotBrush(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => Brushes.Gold,
                ExportChannel.Y => Brushes.LimeGreen,
                ExportChannel.Z => Brushes.Violet,
                ExportChannel.CieX => Brushes.OrangeRed,
                ExportChannel.CieY => Brushes.SeaGreen,
                ExportChannel.CieU => Brushes.DodgerBlue,
                ExportChannel.CieV => Brushes.MediumPurple,
                ExportChannel.ColorDifference => Brushes.Crimson,
                ExportChannel.Contrast => Brushes.DeepSkyBlue,
                _ => Brushes.LimeGreen
            };
        }

        private static double GetNicePolarReferenceRadiusMaximum(double maxValue)
        {
            if (maxValue <= 0)
            {
                return 1;
            }

            const int ringCount = 6;
            double rawStep = maxValue / ringCount;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double normalized = rawStep / magnitude;
            double niceNormalized = normalized <= 1 ? 1
                : normalized <= 1.5 ? 1.5
                : normalized <= 2 ? 2
                : normalized <= 2.5 ? 2.5
                : normalized <= 3 ? 3
                : normalized <= 4 ? 4
                : normalized <= 5 ? 5
                : 10;

            return niceNormalized * magnitude * ringCount;
        }

        private double GetStablePolarReferenceRadiusMaximum(ExportChannel channel, IEnumerable<double> values)
        {
            double curveMaximum = values
                .Where(double.IsFinite)
                .DefaultIfEmpty(0)
                .Max();

            double scaleMaximum = curveMaximum;
            if (channel == currentReferenceScaleChannel
                && double.IsFinite(currentReferenceScaleMaximum)
                && currentReferenceScaleMaximum > 0)
            {
                scaleMaximum = Math.Max(scaleMaximum, currentReferenceScaleMaximum);
            }

            return GetNicePolarReferenceRadiusMaximum(scaleMaximum);
        }

        private static double NormalizePolarPlotAngle(double angleDegrees)
        {
            double normalized = angleDegrees % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }

        private static double ConvertCircleAngleToPolarDisplayAngle(double angleDegrees)
        {
            return NormalizePolarPlotAngle(90.0 - angleDegrees);
        }

        private void UpdatePolarReferencePlot(IReadOnlyList<PolarPlotPoint> points, ExportChannel channel, bool closePath)
        {
            if (polarPlotReference == null)
            {
                return;
            }

            double radialMaximum = GetStablePolarReferenceRadiusMaximum(channel, points.Select(point => point.Radius));
            polarPlotReference.UpdatePlot(
                points,
                GetChannelPlotBrush(channel),
                Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.RadiusFormat, ConoscopeChannelDisplayFormatter.GetAxisLabel(channel)),
                radialMaximum,
                closePath);
        }

        private static ScottPlot.Color GetPlotColor(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => ScottPlot.Color.FromColor(System.Drawing.Color.Gold),
                ExportChannel.Y => ScottPlot.Color.FromColor(System.Drawing.Color.LimeGreen),
                ExportChannel.Z => ScottPlot.Color.FromColor(System.Drawing.Color.Violet),
                ExportChannel.CieX => ScottPlot.Color.FromColor(System.Drawing.Color.OrangeRed),
                ExportChannel.CieY => ScottPlot.Color.FromColor(System.Drawing.Color.SeaGreen),
                ExportChannel.CieU => ScottPlot.Color.FromColor(System.Drawing.Color.DodgerBlue),
                ExportChannel.CieV => ScottPlot.Color.FromColor(System.Drawing.Color.MediumPurple),
                ExportChannel.ColorDifference => ScottPlot.Color.FromColor(System.Drawing.Color.Crimson),
                ExportChannel.Contrast => ScottPlot.Color.FromColor(System.Drawing.Color.DeepSkyBlue),
                _ => ScottPlot.Color.FromColor(System.Drawing.Color.LimeGreen)
            };
        }

        private void ExtractRgbAlongLine(PolarAngleLine polarLine, Point start, Point end, BitmapSource bitmapSource, int radius)
        {
            try
            {
                if (YMat == null)
                {
                    return;
                }

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;

                double lineLength = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2));
                int numSamples = (int)lineLength;

                if (numSamples <= 1)
                {
                    log.Warn($"线长度太短 ({numSamples} 像素)，无法采样");
                    return;
                }

                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * (end.X - start.X);
                    double y = start.Y + t * (end.Y - start.Y);

                    int ix = Math.Max(0, Math.Min(imageWidth - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(imageHeight - 1, (int)Math.Round(y)));

                    double position = -MaxAngle + (i / (double)(numSamples - 1)) * MaxAngle * 2;

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);

                    polarLine.RgbData.Add(new RgbSample
                    {
                        Position = position,
                        DX = ix,
                        DY = iy,
                        X = X,
                        Y = Y,
                        Z = Z,
                    });
                }

                log.Info($"完成采样: 方位角{polarLine.Angle}°, 采样点数{polarLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
            }
        }

        private void ExtractRgbAlongCircle(ConcentricCircleLine circleLine, Point center, double radiusAngle, BitmapSource bitmapSource)
        {
            try
            {
                if (YMat == null)
                {
                    return;
                }

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;
                double radiusPixels = radiusAngle * currentPixelsPerDegree;

                const int numSamples = 360;
                for (int i = 0; i < numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples;
                    double radians = anglePos * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y - radiusPixels * Math.Sin(radians);

                    int ix = Math.Max(0, Math.Min(imageWidth - 1, (int)Math.Round(x)));
                    int iy = Math.Max(0, Math.Min(imageHeight - 1, (int)Math.Round(y)));

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);

                    circleLine.RgbData.Add(new RgbSample
                    {
                        Position = anglePos,
                        DX = ix,
                        DY = iy,
                        X = X,
                        Y = Y,
                        Z = Z
                    });
                }

                log.Info($"完成采样: 极角半径角度{circleLine.RadiusAngle}°, 采样点数{circleLine.RgbData.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取极角数据失败: {ex.Message}", ex);
            }
        }

        private void UpdatePlot()
        {
            try
            {
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                    {
                        polarPlotReference?.Clear();
                        return;
                    }

                    ExportChannel polarChannel = GetSelectedDisplayChannel();
                    PolarPlotPoint[] polarPoints = selectedPolarLine.RgbData
                        .Select(sample => new PolarPlotPoint(NormalizePolarPlotAngle(sample.Position), GetChannelValue(sample, polarChannel)))
                        .ToArray();
                    UpdatePolarReferencePlot(polarPoints, polarChannel, closePath: false);
                    return;
                }

                wpfPlotReference.Plot.Clear();

                if (selectedPolarLine == null || selectedPolarLine.RgbData.Count == 0)
                {
                    wpfPlotReference.Refresh();
                    return;
                }

                double[] positions = selectedPolarLine.RgbData.Select(sample => sample.Position).ToArray();
                ExportChannel channel = GetSelectedDisplayChannel();
                double[] values = selectedPolarLine.RgbData.Select(sample => GetChannelValue(sample, channel)).ToArray();
                ScottPlot.Plottables.Scatter scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = ConoscopeChannelDisplayFormatter.GetLabel(channel);

                wpfPlotReference.Plot.Title(string.Format(Properties.Resources.Conoscope_PolarDistributionTitle, selectedPolarLine.Angle, ConoscopeChannelDisplayFormatter.GetLabel(channel)));
                wpfPlotReference.Plot.XLabel(Properties.Resources.Conoscope_AngleDegrees);
                wpfPlotReference.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();

                wpfPlotReference.Refresh();

                log.Info($"更新图表: 方位角{selectedPolarLine.Angle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新图表失败: {ex.Message}", ex);
            }
        }

        private void UpdatePlotForCircle()
        {
            try
            {
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    if (selectedCircleLine == null || selectedCircleLine.RgbData.Count == 0)
                    {
                        polarPlotReference?.Clear();
                        return;
                    }

                    ExportChannel polarChannel = GetSelectedDisplayChannel();
                    PolarPlotPoint[] polarPoints = selectedCircleLine.RgbData
                        .Select(sample => new PolarPlotPoint(ConvertCircleAngleToPolarDisplayAngle(sample.Position), GetChannelValue(sample, polarChannel)))
                        .ToArray();
                    UpdatePolarReferencePlot(polarPoints, polarChannel, closePath: true);
                    return;
                }

                wpfPlotReference.Plot.Clear();

                if (selectedCircleLine == null || selectedCircleLine.RgbData.Count == 0)
                {
                    wpfPlotReference.Refresh();
                    return;
                }

                double[] positions = selectedCircleLine.RgbData.Select(sample => sample.Position).ToArray();
                ExportChannel channel = GetSelectedDisplayChannel();
                double[] values = selectedCircleLine.RgbData.Select(sample => GetChannelValue(sample, channel)).ToArray();
                ScottPlot.Plottables.Scatter scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = ConoscopeChannelDisplayFormatter.GetLabel(channel);

                wpfPlotReference.Plot.Title(string.Format(Properties.Resources.Conoscope_CircleDistributionTitle, selectedCircleLine.RadiusAngle, ConoscopeChannelDisplayFormatter.GetLabel(channel)));
                wpfPlotReference.Plot.XLabel(Properties.Resources.Conoscope_CircleAngleDegrees);
                wpfPlotReference.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();

                wpfPlotReference.Refresh();

                log.Info($"更新图表: 极角半径角度{selectedCircleLine.RadiusAngle}°");
            }
            catch (Exception ex)
            {
                log.Error($"更新极角图表失败: {ex.Message}", ex);
            }
        }
    }
}
