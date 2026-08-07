#pragma warning disable CA1863
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

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
            ReferenceCurve? curve = CoordinateAxisConfig.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine
                ? selectedPolarLine
                : selectedCircleLine;
            UpdateReferenceCurvePlot(curve);
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

        private double GetStablePolarReferenceRadiusMaximum(ExportChannel channel, IReadOnlyList<PolarPlotPoint> points)
        {
            double curveMaximum = 0;
            for (int index = 0; index < points.Count; index++)
            {
                double radius = points[index].Radius;
                if (double.IsFinite(radius))
                {
                    curveMaximum = Math.Max(curveMaximum, radius);
                }
            }

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

            double radialMaximum = GetStablePolarReferenceRadiusMaximum(channel, points);
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

        private void ExtractRgbAlongLine(PolarAngleLine curve, Point start, Point end)
        {
            try
            {
                if (YMat == null)
                {
                    return;
                }

                int imageWidth = YMat.Width;
                int imageHeight = YMat.Height;

                double deltaX = end.X - start.X;
                double deltaY = end.Y - start.Y;
                double lineLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                int numSamples = (int)lineLength;

                if (numSamples <= 1)
                {
                    log.Warn($"线长度太短 ({numSamples} 像素)，无法采样");
                    return;
                }

                curve.Samples.EnsureCapacity(numSamples);
                for (int i = 0; i < numSamples; i++)
                {
                    double t = i / (double)(numSamples - 1);
                    double x = start.X + t * deltaX;
                    double y = start.Y + t * deltaY;

                    int ix = Math.Clamp((int)Math.Round(x), 0, imageWidth - 1);
                    int iy = Math.Clamp((int)Math.Round(y), 0, imageHeight - 1);

                    double position = -MaxAngle + t * MaxAngle * 2;

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    curve.Samples.Add(new RgbSample(position, ix, iy, X, Y, Z));
                }

                log.Info($"完成采样: 方位角{curve.Angle}°, 采样点数{curve.Samples.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取数据失败: {ex.Message}", ex);
            }
        }

        private void ExtractRgbAlongCircle(ConcentricCircleLine curve, Point center, double radiusAngle)
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
                curve.Samples.EnsureCapacity(numSamples);
                for (int i = 0; i < numSamples; i++)
                {
                    double anglePos = i * 360.0 / numSamples;
                    double radians = anglePos * Math.PI / 180.0;
                    double x = center.X + radiusPixels * Math.Cos(radians);
                    double y = center.Y - radiusPixels * Math.Sin(radians);

                    int ix = Math.Clamp((int)Math.Round(x), 0, imageWidth - 1);
                    int iy = Math.Clamp((int)Math.Round(y), 0, imageHeight - 1);

                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    curve.Samples.Add(new RgbSample(anglePos, ix, iy, X, Y, Z));
                }

                log.Info($"完成采样: 极角半径角度{curve.RadiusAngle}°, 采样点数{curve.Samples.Count}");
            }
            catch (Exception ex)
            {
                log.Error($"提取极角数据失败: {ex.Message}", ex);
            }
        }

        private void UpdateReferenceCurvePlot(ReferenceCurve? curve)
        {
            try
            {
                if (curve == null || curve.Samples.Count == 0)
                {
                    wpfPlotReference.Plot.Clear();
                    wpfPlotReference.Refresh();
                    polarPlotReference?.Clear();
                    return;
                }

                ExportChannel channel = GetSelectedDisplayChannel();
                if (referencePlotDisplayMode == ReferencePlotDisplayMode.Polar)
                {
                    PolarPlotPoint[] points = new PolarPlotPoint[curve.Samples.Count];
                    for (int index = 0; index < curve.Samples.Count; index++)
                    {
                        RgbSample sample = curve.Samples[index];
                        double angle = curve.IsClosed
                            ? ConvertCircleAngleToPolarDisplayAngle(sample.Position)
                            : NormalizePolarPlotAngle(sample.Position);
                        points[index] = new PolarPlotPoint(angle, GetChannelValue(sample, channel));
                    }

                    UpdatePolarReferencePlot(points, channel, curve.IsClosed);
                    return;
                }

                wpfPlotReference.Plot.Clear();
                double[] positions = new double[curve.Samples.Count];
                double[] values = new double[curve.Samples.Count];
                for (int index = 0; index < curve.Samples.Count; index++)
                {
                    RgbSample sample = curve.Samples[index];
                    positions[index] = sample.Position;
                    values[index] = GetChannelValue(sample, channel);
                }

                ScottPlot.Plottables.Scatter scatter = wpfPlotReference.Plot.Add.Scatter(positions, values);
                scatter.Color = GetPlotColor(channel);
                scatter.LineWidth = 2;
                scatter.LegendText = ConoscopeChannelDisplayFormatter.GetLabel(channel);

                string channelLabel = ConoscopeChannelDisplayFormatter.GetLabel(channel);
                string title = curve is ConcentricCircleLine circle
                    ? string.Format(Properties.Resources.Conoscope_CircleDistributionTitle, circle.RadiusAngle, channelLabel)
                    : string.Format(Properties.Resources.Conoscope_PolarDistributionTitle, ((PolarAngleLine)curve).Angle, channelLabel);
                wpfPlotReference.Plot.Title(title);
                wpfPlotReference.Plot.XLabel(curve.IsClosed ? Properties.Resources.Conoscope_CircleAngleDegrees : Properties.Resources.Conoscope_AngleDegrees);
                wpfPlotReference.Plot.YLabel(ConoscopeChannelDisplayFormatter.GetAxisLabel(channel));
                wpfPlotReference.Plot.Legend.IsVisible = true;
                wpfPlotReference.Plot.Axes.AutoScale();
                wpfPlotReference.Refresh();

                log.Info($"更新参考曲线: {curve}");
            }
            catch (Exception ex)
            {
                log.Error($"更新参考曲线失败: {ex.Message}", ex);
            }
        }
    }
}
