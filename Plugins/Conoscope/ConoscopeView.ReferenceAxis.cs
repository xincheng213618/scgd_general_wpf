using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using Conoscope.Properties;
using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void NotifyReferenceStateChanged()
        {
            SyncReferenceInteractionToggle();
            RaiseWindowQuickControlStateChanged();
        }

        private void InitializeCoordinateAxis(Point center, int radius)
        {
            ConoscopeCoordinateAxisParam axisParam = CoordinateAxisConfig;
            axisParam.PropertyChanged -= CoordinateAxisParam_PropertyChanged;
            axisParam.PropertyChanged += CoordinateAxisParam_PropertyChanged;
            axisParam.MaxAngle = MaxAngle;
            axisParam.ConoscopeCoefficient = currentPixelsPerDegree;
            axisParam.CenterX = center.X;
            axisParam.CenterY = center.Y;
            axisParam.AxisRadius = radius;
            axisParam.ReferenceRadiusAngle = Math.Max(0, Math.Min(axisParam.ReferenceRadiusAngle, MaxAngle));

            coordinateAxisController?.ReferenceChanged -= CoordinateAxisController_ReferenceChanged;
            coordinateAxisController?.PointerMoved -= CoordinateAxisController_PointerMoved;
            coordinateAxisController?.PointerLeft -= CoordinateAxisController_PointerLeft;
            coordinateAxisController?.Dispose();
            coordinateAxisController = new ConoscopeCoordinateAxisController(ImageView.ImageShow, ImageView.Zoombox1, axisParam);
            coordinateAxisController.ReferenceChanged += CoordinateAxisController_ReferenceChanged;
            coordinateAxisController.PointerMoved += CoordinateAxisController_PointerMoved;
            coordinateAxisController.PointerLeft += CoordinateAxisController_PointerLeft;
            coordinateAxisController.Configure(center, radius, MaxAngle, currentPixelsPerDegree);
            coordinateAxisController.Show();
            UpdateReferencePlotHeader();
        }

        private void CoordinateAxisParam_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyReferenceStateChanged();

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceMode))
            {
                ApplyCoordinateAxisReference();
                return;
            }

            if (e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceAngle)
                || e.PropertyName == nameof(ConoscopeCoordinateAxisParam.ReferenceRadiusAngle))
            {
                if (coordinateAxisController?.IsUpdatingReference != true)
                {
                    ApplyCoordinateAxisReference();
                }
            }
        }

        private void CoordinateAxisController_ReferenceChanged(object? sender, ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            UpdateCieWindowSelection(e.Position);
            HideCoordinateDragOverlay();

            if (!e.IsValueChanged)
            {
                return;
            }

            if (e.Mode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdateCoordinateAxisAzimuth(e.Angle);
            }
            else
            {
                UpdateCoordinateAxisPolar(e.RadiusAngle);
            }
        }

        private void CoordinateAxisController_PointerMoved(object? sender, ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            UpdateCieWindowSelection(e.Position);
            ShowCoordinateDragOverlay(e);
        }

        private void CoordinateAxisController_PointerLeft(object? sender, EventArgs e)
        {
            HideCoordinateDragOverlay();
        }

        private void ShowCoordinateDragOverlay(ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            CoordinateDragOverlayText.Text = GetCoordinateDragOverlayText(e);
            CoordinateDragOverlay.Visibility = Visibility.Visible;
        }

        private string GetCoordinateDragOverlayText(ConoscopeCoordinateReferenceChangedEventArgs e)
        {
            if (currentBitmapSource == null)
            {
                return GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle);
            }

            if (!TryGetChromaticityAtPosition(e.Position, out PixelChromaticitySample sample))
            {
                return GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle);
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            double displayValue = GetChannelValue(sample.XyzX, sample.XyzY, sample.X, sample.Y, sample.Z, displayChannel);
            double azimuthAngle = FocusPointMeasurementService.GetFullAzimuthAngle(e.Position, currentImageCenter);
            double polarAngle = FocusPointMeasurementService.GetPolarRadiusAngle(e.Position, currentImageCenter, currentImageRadius, MaxAngle);

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.ReferenceFormat, GetReferenceValueText(e.Mode, e.Angle, e.RadiusAngle)));
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.PixelCoordFormat, sample.ImageX, sample.ImageY));
            builder.AppendLine(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.PolarCoordFormat, azimuthAngle.ToString("F2"), polarAngle.ToString("F2")));
            builder.AppendLine($"{ConoscopeChannelDisplayFormatter.GetLabel(displayChannel)}: {ConoscopeChannelDisplayFormatter.FormatValue(displayValue, displayChannel)}");
            builder.AppendLine($"XYZ: X={sample.X:F4}, Y={sample.Y:F4}, Z={sample.Z:F4}");
            builder.AppendLine($"xy: x={sample.Chromaticity.x:F6}, y={sample.Chromaticity.y:F6}");
            builder.Append($"uv: u={sample.Chromaticity.u:F6}, v={sample.Chromaticity.v:F6}, CCT={(sample.Chromaticity.Cct > 0 ? $"{sample.Chromaticity.Cct:F0}K" : "--")}");
            return builder.ToString();
        }

        private void UpdateCieWindowSelection(Point position)
        {
            if (cieWindow == null)
            {
                return;
            }

            if (TryGetChromaticityAtPosition(position, out PixelChromaticitySample sample))
            {
                cieWindow.ChangeSelect(sample.Chromaticity.x, sample.Chromaticity.y);
            }
        }

        private bool TryGetChromaticityAtPosition(Point position, out PixelChromaticitySample sample)
        {
            sample = default;
            if (currentBitmapSource == null || !HasXyzData())
            {
                return false;
            }

            int imageWidth = currentBitmapSource.PixelWidth;
            int imageHeight = currentBitmapSource.PixelHeight;
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return false;
            }

            int imageX = ConoscopeNumericHelper.ClampToInt((int)Math.Round(position.X), 0, imageWidth - 1);
            int imageY = ConoscopeNumericHelper.ClampToInt((int)Math.Round(position.Y), 0, imageHeight - 1);

            int xyzWidth = YMat?.Width ?? XMat?.Width ?? ZMat?.Width ?? imageWidth;
            int xyzHeight = YMat?.Height ?? XMat?.Height ?? ZMat?.Height ?? imageHeight;
            if (xyzWidth <= 0 || xyzHeight <= 0)
            {
                return false;
            }

            int xyzX = ConoscopeNumericHelper.ClampToInt(imageX, 0, xyzWidth - 1);
            int xyzY = ConoscopeNumericHelper.ClampToInt(imageY, 0, xyzHeight - 1);
            ExtractXYZValues(xyzX, xyzY, out double X, out double Y, out double Z);
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
            sample = new PixelChromaticitySample(imageX, imageY, xyzX, xyzY, X, Y, Z, chromaticity);
            return true;
        }

        private void HideCoordinateDragOverlay()
        {
            CoordinateDragOverlay.Visibility = Visibility.Collapsed;
        }

        private void ApplyCoordinateAxisReference()
        {
            if (coordinateAxisController == null)
            {
                SetReferencePlotLimits();
                UpdateReferencePlotHeader();
                return;
            }

            if (coordinateAxisController.Axis.Attribute.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                UpdateCoordinateAxisAzimuth(coordinateAxisController.Axis.Attribute.ReferenceAngle);
            }
            else
            {
                UpdateCoordinateAxisPolar(coordinateAxisController.Axis.Attribute.ReferenceRadiusAngle);
            }

            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
        }

        private void UpdateCoordinateAxisAzimuth(double angle)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            angle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(angle);

            PolarAngleLine curve = coordinateAxisReferenceCurve as PolarAngleLine ?? new PolarAngleLine();
            curve.Angle = angle;
            curve.Samples.Clear();

            (Point Start, Point End) endpoints = ConoscopeCoordinateAxisVisual.GetAzimuthLineEndpoints(currentImageCenter, currentImageRadius, angle);
            ExtractRgbAlongLine(curve, endpoints.End, endpoints.Start);

            coordinateAxisReferenceCurve = curve;
            selectedReferenceCurve = curve;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdateReferencePlot();
        }

        private void UpdateCoordinateAxisPolar(double radiusAngle)
        {
            if (currentBitmapSource == null)
            {
                return;
            }

            radiusAngle = Math.Clamp(radiusAngle, 0, MaxAngle);

            ConcentricCircleLine curve = coordinateAxisReferenceCurve as ConcentricCircleLine ?? new ConcentricCircleLine();
            curve.RadiusAngle = radiusAngle;
            curve.Samples.Clear();
            ExtractRgbAlongCircle(curve, currentImageCenter, radiusAngle);

            coordinateAxisReferenceCurve = curve;
            selectedReferenceCurve = curve;
            SetReferencePlotLimits();
            UpdateReferencePlotHeader();
            UpdateReferencePlot();
        }

        private void DisposeCoordinateAxis()
        {
            if (coordinateAxisController != null)
            {
                coordinateAxisController.ReferenceChanged -= CoordinateAxisController_ReferenceChanged;
                coordinateAxisController.PointerMoved -= CoordinateAxisController_PointerMoved;
                coordinateAxisController.PointerLeft -= CoordinateAxisController_PointerLeft;
                coordinateAxisController.Axis.Attribute.PropertyChanged -= CoordinateAxisParam_PropertyChanged;
                coordinateAxisController.Dispose();
                coordinateAxisController = null;
            }

            selectedReferenceCurve = null;
            coordinateAxisReferenceCurve = null;
        }

        private void CreateAndAnalyzePolarLines()
        {
            try
            {
                if (ImageView.ImageShow.Source == null)
                {
                    log.Warn("图像未加载，无法创建极角线");
                    return;
                }

                BitmapSource? bitmapSource = ImageView.ImageShow.Source as BitmapSource;
                if (bitmapSource == null)
                {
                    log.Error("无法获取图像源");
                    return;
                }

                int imageWidth = bitmapSource.PixelWidth;
                int imageHeight = bitmapSource.PixelHeight;

                currentPixelsPerDegree = CurrentModelProfile.GetConoscopeCoefficient(imageWidth, imageHeight);
                int radius = (int)Math.Round(MaxAngle * currentPixelsPerDegree);

                Point center = new Point(imageWidth / 2.0, imageHeight / 2.0);

                currentBitmapSource = bitmapSource;
                currentImageCenter = center;
                currentImageRadius = radius;

                ImageView.SetFocusCircleBoundary(center, radius);

                InitializeCoordinateAxis(center, radius);

                log.Info($"图像尺寸: {imageWidth}x{imageHeight}, 中心: ({center.X}, {center.Y}), 半径: {radius}, 系数: {currentPixelsPerDegree:F6}px/deg");

                selectedReferenceCurve = null;
                coordinateAxisReferenceCurve = null;

                coordinateAxisController?.BringToFront();
                ApplyCoordinateAxisReference();
            }
            catch (Exception ex)
            {
                log.Error($"创建极角线失败: {ex.Message}", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgPolarLineCreateFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
