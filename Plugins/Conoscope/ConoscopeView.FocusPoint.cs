using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor.Draw;
using Conoscope.Analysis;
using Conoscope.Core;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private bool isUpdatingFocusCircleToolSelection;

        private void InitializeFocusPointTools()
        {
            SyncReferenceInteractionToggle();
            SetFocusCircleToolSelection(useDrawTool: true, useEraseTool: false);
            UpdateFocusCircleModeState();
        }

        private void SyncReferenceInteractionToggle()
        {
            if (tglReferenceInteraction == null)
            {
                return;
            }

            bool isInteractionEnabled = CurrentModelProfile.CoordinateAxisParam.IsInteractionEnabled;
            if (tglReferenceInteraction.IsChecked != isInteractionEnabled)
            {
                tglReferenceInteraction.IsChecked = isInteractionEnabled;
            }
        }

        private void tglFocusCircleMode_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleMode_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateFocusCircleModeState();
        }

        private void UpdateFocusCircleModeState()
        {
            if (ImageView == null)
            {
                return;
            }

            bool isEditMode = tglFocusCircleMode?.IsChecked == true;
            bool isReferenceInteractionEnabled = tglReferenceInteraction?.IsChecked == true;
            bool useEraseTool = isEditMode && tglFocusCircleEraseTool?.IsChecked == true;
            bool useDrawTool = isEditMode && !useEraseTool;

            if (isEditMode && tglFocusCircleDrawTool?.IsChecked != true && tglFocusCircleEraseTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(useDrawTool: true, useEraseTool: false);
                useDrawTool = true;
            }

            ImageView.SetFocusCircleEditMode(isEditMode);
            ImageView.SetFocusCircleSelectionEnabled(!isEditMode && !isReferenceInteractionEnabled);
            ImageView.SetFocusCircleDrawMode(useDrawTool);
            ImageView.SetFocusCircleEraseMode(useEraseTool);
            UpdateFocusCircleToolbarState();
            UpdatePanModeState();
        }

        private void UpdateFocusCircleToolbarState()
        {
            bool isEditMode = tglFocusCircleMode?.IsChecked == true;
            if (tglFocusCircleDrawTool != null)
            {
                tglFocusCircleDrawTool.IsEnabled = isEditMode;
            }

            if (tglFocusCircleEraseTool != null)
            {
                tglFocusCircleEraseTool.IsEnabled = isEditMode;
            }

            bool hasFocusCircles = ImageView.FocusCircles.Count > 0;
            if (btnCalculateFocusCircles != null)
            {
                btnCalculateFocusCircles.IsEnabled = hasFocusCircles;
            }

            if (btnClearFocusCircles != null)
            {
                btnClearFocusCircles.IsEnabled = hasFocusCircles;
            }
        }

        private void SetFocusCircleToolSelection(bool useDrawTool, bool useEraseTool)
        {
            if (isUpdatingFocusCircleToolSelection)
            {
                return;
            }

            isUpdatingFocusCircleToolSelection = true;
            try
            {
                if (tglFocusCircleDrawTool != null)
                {
                    tglFocusCircleDrawTool.IsChecked = useDrawTool;
                }

                if (tglFocusCircleEraseTool != null)
                {
                    tglFocusCircleEraseTool.IsChecked = useEraseTool;
                }
            }
            finally
            {
                isUpdatingFocusCircleToolSelection = false;
            }
        }

        private void tglFocusCircleDrawTool_Checked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection)
            {
                SetFocusCircleToolSelection(useDrawTool: true, useEraseTool: false);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleDrawTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection && tglFocusCircleMode?.IsChecked == true && tglFocusCircleEraseTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(useDrawTool: true, useEraseTool: false);
                return;
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Checked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection)
            {
                SetFocusCircleToolSelection(useDrawTool: false, useEraseTool: true);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection && tglFocusCircleMode?.IsChecked == true && tglFocusCircleDrawTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(useDrawTool: true, useEraseTool: false);
                return;
            }

            UpdateFocusCircleModeState();
        }

        private void btnCalculateFocusCircles_Click(object sender, RoutedEventArgs e)
        {
            CalculateFocusPoints(ImageView.FocusCircles);
            UpdateFocusCircleToolbarState();
        }

        private void btnClearFocusCircles_Click(object sender, RoutedEventArgs e)
        {
            ImageView.ClearFocusCircles();
            UpdateFocusCircleToolbarState();
        }

        private void tglReferenceInteraction_Checked(object sender, RoutedEventArgs e)
        {
            SetReferenceInteractionEnabled(true);
        }

        private void tglReferenceInteraction_Unchecked(object sender, RoutedEventArgs e)
        {
            SetReferenceInteractionEnabled(false);
        }

        private void SetReferenceInteractionEnabled(bool isEnabled)
        {
            CurrentModelProfile.CoordinateAxisParam.IsInteractionEnabled = isEnabled;
            if (!isEnabled)
            {
                HideCoordinateDragOverlay();
            }

            UpdateFocusCircleModeState();
        }

        private void ImageView_FocusCircleCalculationRequested(object? sender, ConoscopeFocusCircleCalculationRequestedEventArgs e)
        {
            CalculateFocusPoints(e.Circles);
        }

        public bool TryGetFocusPointMeasurementCapture(string slotName, out MeasurementCapture capture, out string? errorMessage)
        {
            capture = default!;
            errorMessage = null;

            IReadOnlyList<DVCircleText> focusCircles = ImageView.FocusCircles;
            if (focusCircles.Count == 0)
            {
                errorMessage = "请先在当前视图中绘制关注点圆。";
                return false;
            }

            List<MeasurementPoint> points = new(focusCircles.Count);
            foreach (DVCircleText circle in focusCircles)
            {
                if (!TryCreateFocusPointMeasurementPoint(circle, out MeasurementPoint point, out errorMessage))
                {
                    return false;
                }

                points.Add(point);
            }

            capture = MeasurementCapture.FromFocusPoints(slotName, GetFocusPointCaptureSourceLabel(), points);
            return true;
        }

        public bool TryGetLatestFocusPointMeasurement(out ImageMeasurement measurement, out string? errorMessage)
        {
            measurement = default!;
            errorMessage = null;

            if (!TryGetFocusPointMeasurementCapture("Latest", out MeasurementCapture capture, out errorMessage))
            {
                return false;
            }

            measurement = capture.Points[^1].Measurement;
            return true;
        }

        private void CalculateFocusPoints(IReadOnlyList<DVCircleText> circles)
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null || currentBitmapSource == null)
            {
                MessageBox.Show("当前图像尚未准备好关注点计算。", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (circles.Count == 0)
            {
                MessageBox.Show("请先绘制关注点圆。", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ObservableCollection<PoiResultCIExyuvData> results = new();
            List<string> failedCircles = new();

            foreach (DVCircleText circle in circles.Where(static item => item != null).Distinct())
            {
                if (!TryCreateFocusPointResult(circle, out PoiResultCIExyuvData? result, out string? errorMessage))
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        failedCircles.Add(errorMessage);
                    }
                    continue;
                }

                results.Add(result);
                circle.Attribute.Msg = $"Y:{result.Y:F3}  u:{result.u:F4}  v:{result.v:F4}";
                circle.Render();
            }

            if (results.Count == 0)
            {
                string message = failedCircles.Count > 0 ? string.Join(Environment.NewLine, failedCircles) : "没有可用于计算的关注点像素。";
                MessageBox.Show(message, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            WindowCVCIE cieResultWindow = new(results)
            {
                Owner = Window.GetWindow(this)
            };
            cieResultWindow.Show();
            cieResultWindow.Activate();

            if (failedCircles.Count > 0)
            {
                MessageBox.Show(string.Join(Environment.NewLine, failedCircles), "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UpdateFocusCircleToolbarState();
        }

        private bool TryCreateFocusPointResult(DVCircleText circle, out PoiResultCIExyuvData result, out string? errorMessage)
        {
            result = new PoiResultCIExyuvData();
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out errorMessage))
            {
                return false;
            }

            ConoscopeChromaticity chromaticity = measurement.Chromaticity;
            double dominantWave = ColorimetryHelper.CalculateDominantWavelength(measurement.Chromaticity.x, measurement.Chromaticity.y);
            if (!double.IsFinite(dominantWave) || dominantWave < 0)
            {
                dominantWave = 0;
            }

            POIPoint poiPoint = new()
            {
                Name = ResolveFocusCircleName(circle),
                PixelX = (int)Math.Round(circle.Attribute.Center.X),
                PixelY = (int)Math.Round(circle.Attribute.Center.Y),
                PointType = CVCommCore.CVAlgorithm.POIPointTypes.Circle,
                Width = (int)Math.Round(circle.Attribute.Radius * 2),
                Height = (int)Math.Round(circle.Attribute.Radius * 2)
            };

            result.Point = poiPoint;
            result.X = measurement.X;
            result.Y = measurement.Y;
            result.Z = measurement.Z;
            result.x = measurement.Chromaticity.x;
            result.y = measurement.Chromaticity.y;
            result.u = measurement.Chromaticity.u;
            result.v = measurement.Chromaticity.v;
            result.CCT = chromaticity.Cct;
            result.Wave = dominantWave;
            return true;
        }

        private bool TryCreateFocusPointMeasurement(DVCircleText circle, out ImageMeasurement measurement, out string? errorMessage)
        {
            measurement = default!;
            errorMessage = null;

            if (!TryCalculateFocusPointAverage(circle.Attribute.Center, circle.Attribute.Radius, out double avgX, out double avgY, out double avgZ, out int _))
            {
                errorMessage = $"{ResolveFocusCircleName(circle)} 没有可用像素。";
                return false;
            }

            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(avgX, avgY, avgZ);
            measurement = new ImageMeasurement(CreateFocusPointMeasurementLabel(circle), avgX, avgY, avgZ, chromaticity);
            return true;
        }

        private bool TryCreateFocusPointMeasurementPoint(DVCircleText circle, out MeasurementPoint point, out string? errorMessage)
        {
            point = default!;
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out errorMessage))
            {
                return false;
            }

            Point center = circle.Attribute.Center;
            double azimuthDegrees = GetFullAzimuthAngle(center);
            double polarDegrees = GetPolarRadiusAngle(center);
            double radiusDegrees = GetFocusCircleRadiusAngle(circle.Attribute.Radius);
            string pointName = ResolveFocusCircleName(circle);

            point = new MeasurementPoint(
                pointName,
                pointName,
                measurement,
                azimuthDegrees,
                polarDegrees,
                radiusDegrees);
            return true;
        }

        private bool TryCalculateFocusPointAverage(Point imageCenter, double imageRadius, out double avgX, out double avgY, out double avgZ, out int sampleCount)
        {
            avgX = 0;
            avgY = 0;
            avgZ = 0;
            sampleCount = 0;

            if (XMat == null || YMat == null || ZMat == null || currentBitmapSource == null || imageRadius <= 0)
            {
                return false;
            }

            int xyzWidth = XMat.Width;
            int xyzHeight = XMat.Height;
            if (xyzWidth <= 0 || xyzHeight <= 0 || currentBitmapSource.PixelWidth <= 0 || currentBitmapSource.PixelHeight <= 0)
            {
                return false;
            }

            double scaleX = (double)xyzWidth / currentBitmapSource.PixelWidth;
            double scaleY = (double)xyzHeight / currentBitmapSource.PixelHeight;
            double centerX = imageCenter.X * scaleX;
            double centerY = imageCenter.Y * scaleY;
            double radiusX = Math.Max(imageRadius * scaleX, 0.5);
            double radiusY = Math.Max(imageRadius * scaleY, 0.5);

            int startX = Math.Max(0, (int)Math.Floor(centerX - radiusX));
            int endX = Math.Min(xyzWidth - 1, (int)Math.Ceiling(centerX + radiusX));
            int startY = Math.Max(0, (int)Math.Floor(centerY - radiusY));
            int endY = Math.Min(xyzHeight - 1, (int)Math.Ceiling(centerY + radiusY));

            double sumX = 0;
            double sumY = 0;
            double sumZ = 0;

            for (int iy = startY; iy <= endY; iy++)
            {
                double dy = radiusY <= 0 ? 0 : (iy - centerY) / radiusY;
                double dy2 = dy * dy;
                if (dy2 > 1)
                {
                    continue;
                }

                for (int ix = startX; ix <= endX; ix++)
                {
                    double dx = radiusX <= 0 ? 0 : (ix - centerX) / radiusX;
                    if (dx * dx + dy2 > 1)
                    {
                        continue;
                    }

                    ExtractXYZValues(ix, iy, out double xValue, out double yValue, out double zValue);
                    if (!double.IsFinite(xValue) || !double.IsFinite(yValue) || !double.IsFinite(zValue))
                    {
                        continue;
                    }

                    sumX += xValue;
                    sumY += yValue;
                    sumZ += zValue;
                    sampleCount++;
                }
            }

            if (sampleCount <= 0)
            {
                return false;
            }

            avgX = sumX / sampleCount;
            avgY = sumY / sampleCount;
            avgZ = sumZ / sampleCount;
            return true;
        }

        private static string ResolveFocusCircleName(DVCircleText circle)
        {
            return string.IsNullOrWhiteSpace(circle.Attribute.Text) ? $"Focus_{circle.Attribute.Id}" : circle.Attribute.Text;
        }

        private string CreateFocusPointMeasurementLabel(DVCircleText circle)
        {
            string viewName = string.IsNullOrWhiteSpace(Filename) ? "CurrentView" : Path.GetFileName(Filename);
            return $"[关注点] {viewName} - {ResolveFocusCircleName(circle)}";
        }

        private string GetFocusPointCaptureSourceLabel()
        {
            return string.IsNullOrWhiteSpace(Filename) ? "CurrentView" : Path.GetFileName(Filename);
        }

        private double GetFocusCircleRadiusAngle(double radiusPixels)
        {
            if (currentPixelsPerDegree > double.Epsilon)
            {
                return Math.Max(0, radiusPixels / currentPixelsPerDegree);
            }

            if (currentImageRadius > 0)
            {
                return Math.Max(0, Math.Min(radiusPixels / currentImageRadius * MaxAngle, MaxAngle));
            }

            return 0;
        }
    }
}