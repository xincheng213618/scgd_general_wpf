using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Conoscope.Analysis;
using Conoscope.Core;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private enum FocusCircleToolKind
        {
            Draw,
            Select,
            Erase,
        }

        private bool isUpdatingFocusCircleToolSelection;
        private bool shouldRestoreReferenceInteractionAfterFocusMode;
        private static int lastFocusPoiTemplateId = -1;
        private int focusPoiTemplateLoadVersion;
        private bool isUpdatingFocusPoiTemplateSelection;

        private void InitializeFocusPointTools()
        {
            SyncReferenceInteractionToggle();
            SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
            UpdateFocusCircleModeState();
            LoadFocusPoiTemplatesAsync();
        }

        private void LoadFocusPoiTemplatesAsync(int preferredTemplateId = -1, bool applySelectedTemplate = true)
        {
            int version = ++focusPoiTemplateLoadVersion;
            int templateId = preferredTemplateId > 0 ? preferredTemplateId : lastFocusPoiTemplateId;
            SetFocusPoiTemplateControlsEnabled(false);

            if (MySqlControl.GetInstance().IsConnect)
            {
                PopulateFocusPoiTemplates(version, templateId, applySelectedTemplate);
                return;
            }

            PopulateFocusPoiTemplates(version, templateId, applySelectedTemplate);
        }

        private void PopulateFocusPoiTemplates(int version, int preferredTemplateId, bool applySelectedTemplate)
        {
            if (version != focusPoiTemplateLoadVersion || cbFocusPoiTemplate == null)
            {
                return;
            }

            if (!MySqlControl.GetInstance().IsConnect)
            {
                SetFocusPoiTemplateControlsEnabled(false);
                return;
            }

            new TemplatePoi().Load();
            ObservableCollection<TemplateModel<PoiParam>> templates = TemplatePoi.Params.CreateEmpty();

            isUpdatingFocusPoiTemplateSelection = true;
            try
            {
                cbFocusPoiTemplate.ItemsSource = templates;
                int selectedIndex = 0;
                if (preferredTemplateId > 0)
                {
                    int matchedIndex = templates.Select((item, index) => new { item, index })
                        .FirstOrDefault(item => item.item.Value.Id == preferredTemplateId)?.index ?? -1;
                    if (matchedIndex >= 0)
                    {
                        selectedIndex = matchedIndex;
                    }
                }

                cbFocusPoiTemplate.SelectedIndex = selectedIndex;
            }
            finally
            {
                isUpdatingFocusPoiTemplateSelection = false;
            }

            SetFocusPoiTemplateControlsEnabled(true);
            if (applySelectedTemplate && cbFocusPoiTemplate.SelectedValue is PoiParam poiParam && poiParam.Id != -1)
            {
                ApplyFocusPoiTemplate(poiParam);
            }
        }

        private void SetFocusPoiTemplateControlsEnabled(bool isEnabled)
        {
            if (cbFocusPoiTemplate != null)
            {
                cbFocusPoiTemplate.IsEnabled = isEnabled;
            }

            if (btnSaveFocusPoiTemplate != null)
            {
                btnSaveFocusPoiTemplate.IsEnabled = isEnabled;
            }

            if (btnManageFocusPoiTemplate != null)
            {
                btnManageFocusPoiTemplate.IsEnabled = isEnabled;
            }
        }

        private void cbFocusPoiTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingFocusPoiTemplateSelection || cbFocusPoiTemplate.SelectedValue is not PoiParam poiParam || poiParam.Id == -1)
            {
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            ApplyFocusPoiTemplate(poiParam);
        }

        private void btnSaveFocusPoiTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetFocusPoiTemplateForSave(out PoiParam? poiParam) || poiParam == null)
            {
                return;
            }

            if (ImageView.FocusCircles.Count == 0)
            {
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SaveFocusPoiTemplate(poiParam);
            lastFocusPoiTemplateId = poiParam.Id;
            LoadFocusPoiTemplatesAsync(poiParam.Id, applySelectedTemplate: false);
            MessageBox.Show($"已保存关注点到 POI 模板: {poiParam.Name}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnManageFocusPoiTemplate_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = cbFocusPoiTemplate.SelectedIndex > 0 ? cbFocusPoiTemplate.SelectedIndex - 1 : 0;
            TemplateEditorWindow templateEditorWindow = new(new TemplatePoi(), selectedIndex)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            templateEditorWindow.ShowDialog();
            LoadFocusPoiTemplatesAsync(lastFocusPoiTemplateId, applySelectedTemplate: false);
        }

        private bool TryGetFocusPoiTemplateForSave(out PoiParam? poiParam)
        {
            poiParam = null;
            if (!MySqlControl.GetInstance().IsConnect)
            {
                MessageBox.Show("数据库未连接，无法保存 POI 模板。", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (cbFocusPoiTemplate.SelectedValue is PoiParam selectedPoiParam && selectedPoiParam.Id != -1)
            {
                poiParam = selectedPoiParam;
                return true;
            }

            string templateName = $"Conoscope关注点_{DateTime.Now:yyyyMMddHHmmss}";
            TemplatePoi templatePoi = new();
            templatePoi.Load();
            templatePoi.Create(templateName);
            templatePoi.Load();
            poiParam = TemplatePoi.Params.LastOrDefault(item => string.Equals(item.Key, templateName, StringComparison.Ordinal))?.Value;
            if (poiParam != null)
            {
                return true;
            }

            MessageBox.Show("创建 POI 模板失败。", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private void ApplyFocusPoiTemplate(PoiParam poiParam)
        {
            if (poiParam.Id == -1)
            {
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            PoiParam.LoadPoiDetailFromDB(poiParam);
            ImageView.ReplaceFocusCirclesFromPoiPoints(poiParam.PoiPoints);
            UpdateFocusCircleToolbarState();
        }

        private void SaveFocusPoiTemplate(PoiParam poiParam)
        {
            UpdatePoiTemplateImageSize(poiParam);
            poiParam.PoiPoints.Clear();
            foreach (DVCircleText circle in ImageView.FocusCircles)
            {
                double radiusX = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                double radiusY = Math.Max(circle.Attribute.RadiusY, ConoscopeImageHost.MinimumFocusCircleRadius);
                poiParam.PoiPoints.Add(new PoiPoint
                {
                    Id = TryGetPoiDetailId(circle),
                    Name = ResolveFocusCircleName(circle),
                    PointType = GraphicTypes.Circle,
                    PixX = circle.Attribute.Center.X,
                    PixY = circle.Attribute.Center.Y,
                    PixWidth = Math.Max(1, radiusX * 2),
                    PixHeight = Math.Max(1, radiusY * 2)
                });
            }

            poiParam.Save2DB();
        }

        private void UpdatePoiTemplateImageSize(PoiParam poiParam)
        {
            if (ImageView.ImageShow.Source is BitmapSource bitmapSource)
            {
                poiParam.Width = bitmapSource.PixelWidth;
                poiParam.Height = bitmapSource.PixelHeight;
            }
            else if (ImageView.ImageShow.Source != null)
            {
                poiParam.Width = (int)Math.Round(ImageView.ImageShow.Source.Width);
                poiParam.Height = (int)Math.Round(ImageView.ImageShow.Source.Height);
            }
        }

        private static int TryGetPoiDetailId(DVCircleText circle)
        {
            return int.TryParse(circle.Attribute.Name, out int id) && id > 0 ? id : 0;
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

            bool isFocusCircleModeEnabled = tglFocusCircleMode?.IsChecked == true;
            ApplyFocusCircleReferenceInteractionLock(isFocusCircleModeEnabled);

            bool useDrawTool = isFocusCircleModeEnabled && tglFocusCircleDrawTool?.IsChecked == true;
            bool useSelectTool = isFocusCircleModeEnabled && tglFocusCircleSelectTool?.IsChecked == true;
            bool useEraseTool = isFocusCircleModeEnabled && tglFocusCircleEraseTool?.IsChecked == true;

            if (isFocusCircleModeEnabled && !useDrawTool && !useSelectTool && !useEraseTool)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
                useDrawTool = true;
            }

            ImageView.SetFocusCircleEditMode(isFocusCircleModeEnabled);
            ImageView.SetFocusCircleSelectionEnabled(isFocusCircleModeEnabled && useSelectTool);
            ImageView.SetFocusCircleDrawMode(useDrawTool);
            ImageView.SetFocusCircleEraseMode(useEraseTool);
            UpdateFocusCircleToolbarState();
            UpdatePanModeState();
        }

        private void ApplyFocusCircleReferenceInteractionLock(bool isFocusCircleModeEnabled)
        {
            if (tglReferenceInteraction == null)
            {
                return;
            }

            if (isFocusCircleModeEnabled)
            {
                if (tglReferenceInteraction.IsChecked == true)
                {
                    shouldRestoreReferenceInteractionAfterFocusMode = true;
                    tglReferenceInteraction.IsChecked = false;
                }

                tglReferenceInteraction.IsEnabled = false;
                return;
            }

            tglReferenceInteraction.IsEnabled = true;
            if (shouldRestoreReferenceInteractionAfterFocusMode)
            {
                shouldRestoreReferenceInteractionAfterFocusMode = false;
                if (tglReferenceInteraction.IsChecked != true)
                {
                    tglReferenceInteraction.IsChecked = true;
                }
            }
        }

        private void UpdateFocusCircleToolbarState()
        {
            bool isFocusCircleModeEnabled = tglFocusCircleMode?.IsChecked == true;
            if (tglFocusCircleDrawTool != null)
            {
                tglFocusCircleDrawTool.IsEnabled = isFocusCircleModeEnabled;
            }

            if (tglFocusCircleSelectTool != null)
            {
                tglFocusCircleSelectTool.IsEnabled = isFocusCircleModeEnabled;
            }

            if (tglFocusCircleEraseTool != null)
            {
                tglFocusCircleEraseTool.IsEnabled = isFocusCircleModeEnabled;
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

        private void SetFocusCircleToolSelection(FocusCircleToolKind toolKind)
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
                    tglFocusCircleDrawTool.IsChecked = toolKind == FocusCircleToolKind.Draw;
                }

                if (tglFocusCircleSelectTool != null)
                {
                    tglFocusCircleSelectTool.IsChecked = toolKind == FocusCircleToolKind.Select;
                }

                if (tglFocusCircleEraseTool != null)
                {
                    tglFocusCircleEraseTool.IsChecked = toolKind == FocusCircleToolKind.Erase;
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
                SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleDrawTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection && tglFocusCircleMode?.IsChecked == true && tglFocusCircleSelectTool?.IsChecked != true && tglFocusCircleEraseTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
                return;
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleSelectTool_Checked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Select);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleSelectTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection && tglFocusCircleMode?.IsChecked == true && tglFocusCircleDrawTool?.IsChecked != true && tglFocusCircleEraseTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
                return;
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Checked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Erase);
            }

            UpdateFocusCircleModeState();
        }

        private void tglFocusCircleEraseTool_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!isUpdatingFocusCircleToolSelection && tglFocusCircleMode?.IsChecked == true && tglFocusCircleDrawTool?.IsChecked != true && tglFocusCircleSelectTool?.IsChecked != true)
            {
                SetFocusCircleToolSelection(FocusCircleToolKind.Draw);
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
                errorMessage = Properties.Resources.MsgDrawFocusPointsFirst;
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
                MessageBox.Show(Properties.Resources.MsgFocusPointNotReady, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (circles.Count == 0)
            {
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
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
                circle.Attribute.Msg = string.Format(Properties.Resources.FocusPointYUV, result.Y.ToString("F3"), result.u.ToString("F4"), result.v.ToString("F4"));
                circle.Render();
            }

            if (results.Count == 0)
            {
                string message = failedCircles.Count > 0 ? string.Join(Environment.NewLine, failedCircles) : Properties.Resources.MsgNoFocusPointPixelsCalc;
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
                errorMessage = string.Format(Properties.Resources.MsgFocusPointNoPixels, ResolveFocusCircleName(circle));
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
            return $"[{Properties.Resources.FocusPointLabel}] {viewName} - {ResolveFocusCircleName(circle)}";
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
