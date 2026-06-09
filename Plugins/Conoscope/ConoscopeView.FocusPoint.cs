#pragma warning disable CA1863
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SqlSugar;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private enum FocusCircleToolKind
        {
            None,
            Draw,
            Select,
            Erase,
        }

        private bool isUpdatingFocusCircleToolSelection;
        private static int lastFocusPoiTemplateId = -1;
        private int focusPoiTemplateLoadVersion;
        private bool isUpdatingFocusPoiTemplateSelection;

        private void InitializeFocusPointTools()
        {
            SyncReferenceInteractionToggle();
            SetFocusCircleToolSelection(FocusCircleToolKind.None);
            UpdateFocusCircleModeState();
            LoadFocusPoiTemplatesAsync();
            UpdateSelectedFocusPointInfo();
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
            if (isUpdatingFocusPoiTemplateSelection || cbFocusPoiTemplate.SelectedValue is not PoiParam poiParam)
            {
                return;
            }

            if (poiParam.Id == -1)
            {
                lastFocusPoiTemplateId = -1;
                ImageView.ClearFocusCircles();
                UpdateFocusCircleToolbarState();
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
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!SaveFocusPoiTemplate(poiParam))
            {
                return;
            }

            lastFocusPoiTemplateId = poiParam.Id;
            LoadFocusPoiTemplatesAsync(poiParam.Id, applySelectedTemplate: false);
            MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFocusPoiTemplateSaved, poiParam.Name), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateSaveRequiresDatabase, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (cbFocusPoiTemplate.SelectedValue is PoiParam selectedPoiParam && selectedPoiParam.Id != -1)
            {
                poiParam = selectedPoiParam;
                return true;
            }

            string templateName = string.Format(Properties.Resources.Conoscope_FocusPointTemplateName, DateTime.Now.ToString("yyyyMMddHHmmss"));
            TemplatePoi templatePoi = new();
            templatePoi.Load();
            templatePoi.Create(templateName);
            templatePoi.Load();
            poiParam = TemplatePoi.Params.LastOrDefault(item => string.Equals(item.Key, templateName, StringComparison.Ordinal))?.Value;
            if (poiParam != null)
            {
                return true;
            }

            MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateCreateFailed, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private bool SaveFocusPoiTemplate(PoiParam poiParam)
        {
            if (ImageView.ImageShow.Source is BitmapSource bmp)
            {
                poiParam.Width = bmp.PixelWidth;
                poiParam.Height = bmp.PixelHeight;
            }
            else if (ImageView.ImageShow.Source != null)
            {
                poiParam.Width = (int)Math.Round(ImageView.ImageShow.Source.Width);
                poiParam.Height = (int)Math.Round(ImageView.ImageShow.Source.Height);
            }
            poiParam.PoiPoints.Clear();
            foreach (DVCircleText circle in ImageView.FocusCircles)
            {
                double radiusX = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                double radiusY = Math.Max(circle.Attribute.RadiusY, ConoscopeImageHost.MinimumFocusCircleRadius);
                poiParam.PoiPoints.Add(new PoiPoint
                {
                    Id = 0,
                    Name = ResolveFocusCircleName(circle),
                    PointType = GraphicTypes.Circle,
                    PixX = circle.Attribute.Center.X,
                    PixY = circle.Attribute.Center.Y,
                    PixWidth = Math.Max(1, radiusX * 2),
                    PixHeight = Math.Max(1, radiusY * 2)
                });
            }
            try
            {
                int ret = SaveFocusPoiTemplateToDb(poiParam);
                if (ret == -1)
                {
                    MessageBox.Show(Properties.Resources.MsgFocusPoiTemplateSaveFailedCheckLog, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("保存 Conoscope 关注点 POI 模板失败", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgFocusPoiTemplateSaveFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private static int SaveFocusPoiTemplateToDb(PoiParam poiParam)
        {
            PoiMasterModel poiMasterModel = new(poiParam);
            int ret = PoiMasterDao.Instance.Save(poiMasterModel);
            if (ret == -1)
            {
                return ret;
            }

            if (poiParam.Id <= 0 && poiMasterModel.Id > 0)
            {
                poiParam.Id = poiMasterModel.Id;
            }

            List<PoiDetailModel> poiDetails = new();
            foreach (PoiPoint point in poiParam.PoiPoints)
            {
                poiDetails.Add(new PoiDetailModel(poiParam.Id, point)
                {
                    Id = 0
                });
            }

            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });

            db.Ado.BeginTran();
            try
            {
                db.Deleteable<PoiDetailModel>().Where(x => x.Pid == poiParam.Id).ExecuteCommand();
                if (poiDetails.Count > 0)
                {
                    db.Insertable(poiDetails).ExecuteCommand();
                }

                db.Ado.CommitTran();
                return 1;
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
        }


        private void SyncReferenceInteractionToggle()
        {
            bool isInteractionEnabled = tglFocusCircleMode?.IsChecked != true;
            if (CoordinateAxisConfig.IsInteractionEnabled != isInteractionEnabled)
            {
                CoordinateAxisConfig.IsInteractionEnabled = isInteractionEnabled;
            }

            if (!isInteractionEnabled)
            {
                HideCoordinateDragOverlay();
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
            SyncReferenceInteractionToggle();

            bool useDrawTool = isFocusCircleModeEnabled && tglFocusCircleDrawTool?.IsChecked == true;
            bool useEraseTool = isFocusCircleModeEnabled && tglFocusCircleEraseTool?.IsChecked == true;
            bool useSelectTool = isFocusCircleModeEnabled && !useDrawTool && !useEraseTool;

            ImageView.SetFocusCircleEditMode(isFocusCircleModeEnabled);
            ImageView.SetFocusCircleSelectionEnabled(isFocusCircleModeEnabled && useSelectTool);
            ImageView.SetFocusCircleDrawMode(useDrawTool);
            ImageView.SetFocusCircleEraseMode(useEraseTool);
            UpdateFocusCircleToolbarState();
            UpdatePanModeState();
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

        private void ImageView_FocusCircleCalculationRequested(object? sender, ConoscopeFocusCircleCalculationRequestedEventArgs e)
        {
            CalculateFocusPoints(e.Circles);
        }

        private void ImageView_FocusCircleEditRequested(object? sender, ConoscopeFocusCircleEditRequestedEventArgs e)
        {
            OpenFocusPointPolarEditor(e.Circle);
        }

        private void OpenFocusPointPolarEditor(DVCircleText circle)
        {
            FocusPointPolarEditModel editModel = new();
            editModel.Initialize(this, circle);

            PropertyEditorWindow editorWindow = new(editModel)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Title = Properties.Resources.TitleFocusPointPolarEditor
            };
            editorWindow.Submited += (_, _) =>
            {
                circle.Render();
                ImageView.RefreshFocusCircleSelection();
                UpdateSelectedFocusPointInfo();
            };
            editorWindow.ShowDialog();
        }

        private void UpdateSelectedFocusPointInfo()
        {
            if (tbSelectedFocusPointInfo == null || sepSelectedFocusPointInfo == null || ImageView == null)
            {
                return;
            }

            DVCircleText? circle = (tglFocusCircleMode?.IsChecked == true) ? null : ImageView.SelectedFocusCircle;
            if (circle == null)
            {
                tbSelectedFocusPointInfo.Text = string.Empty;
                tbSelectedFocusPointInfo.ToolTip = Properties.Resources.TipSelectedFocusPoint;
                tbSelectedFocusPointInfo.Visibility = Visibility.Collapsed;
                sepSelectedFocusPointInfo.Visibility = Visibility.Collapsed;
                return;
            }

            string text = BuildSelectedFocusPointInfo(circle, includeMeasurement: true);
            tbSelectedFocusPointInfo.Text = text;
            tbSelectedFocusPointInfo.ToolTip = text;
            tbSelectedFocusPointInfo.Visibility = Visibility.Visible;
            sepSelectedFocusPointInfo.Visibility = Visibility.Visible;
        }

        private string BuildSelectedFocusPointInfo(DVCircleText circle, bool includeMeasurement)
        {
            double radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            string circleName = ResolveFocusCircleName(circle);
            double azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(circle.Attribute.Center, currentImageCenter);
            double polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(circle.Attribute.Center, currentImageCenter, currentImageRadius, MaxAngle);
            double radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, currentPixelsPerDegree, currentImageRadius, MaxAngle);
            string info = string.Format(Properties.Resources.Conoscope_FocusPointInfo, circleName, azimuthDegrees, polarDegrees, radiusPixels, radiusDegrees);

            if (includeMeasurement
                && TryCalculateFocusPointAverage(circle.Attribute.Center, radiusPixels, out _, out double avgY, out _, out int sampleCount))
            {
                info += $"  N {sampleCount}  Y {avgY:F3}";
            }

            return info;
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

            capture = MeasurementCapture.FromFocusPoints(slotName, string.IsNullOrWhiteSpace(Filename) ? "CurrentView" : Path.GetFileName(Filename), points);
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
                MessageBox.Show(Properties.Resources.MsgFocusPointNotReady, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (circles.Count == 0)
            {
                MessageBox.Show(Properties.Resources.MsgDrawFocusPointsFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ObservableCollection<PoiResultCIExyuvData> results = new();
            List<string> failedCircles = new();

            foreach (DVCircleText circle in circles.Where(static item => item != null).Distinct())
            {
                if (!TryCreateFocusPointResult(circle, out PoiResultCIExyuvData? result, out int sampleCount, out string? errorMessage))
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        failedCircles.Add(errorMessage);
                    }
                    continue;
                }

                results.Add(result);
                double msgRadiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius), currentPixelsPerDegree, currentImageRadius, MaxAngle);
                circle.Attribute.Msg = $"{Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.FocusPointYUV, result.Y.ToString("F3"), result.u.ToString("F4"), result.v.ToString("F4"))}  R:{msgRadiusDegrees:F2}°  N:{sampleCount}";
                circle.Render();
            }

            if (results.Count == 0)
            {
                string message = failedCircles.Count > 0 ? string.Join(Environment.NewLine, failedCircles) : Properties.Resources.MsgNoFocusPointPixelsCalc;
                MessageBox.Show(message, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(string.Join(Environment.NewLine, failedCircles), Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            UpdateFocusCircleToolbarState();
            UpdateSelectedFocusPointInfo();
        }

        private bool TryCreateFocusPointResult(DVCircleText circle, out PoiResultCIExyuvData result, out int sampleCount, out string? errorMessage)
        {
            result = new PoiResultCIExyuvData();
            sampleCount = 0;
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out sampleCount, out errorMessage))
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
                Height = (int)Math.Round(circle.Attribute.RadiusY * 2)
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

        private bool TryCreateFocusPointMeasurement(DVCircleText circle, out ImageMeasurement measurement, out int sampleCount, out string? errorMessage)
        {
            measurement = default!;
            errorMessage = null;
            sampleCount = 0;

            if (XMat == null || YMat == null || ZMat == null || currentBitmapSource == null)
            {
                return false;
            }

            double radius = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            string label = $"[{Properties.Resources.FocusPointLabel}] {(string.IsNullOrWhiteSpace(Filename) ? "CurrentView" : Path.GetFileName(Filename))} - {ResolveFocusCircleName(circle)}";
            if (!FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                XMat, YMat, ZMat,
                currentBitmapSource.PixelWidth, currentBitmapSource.PixelHeight,
                circle.Attribute.Center, radius,
                out double avgX, out double avgY, out double avgZ, out sampleCount))
            {
                errorMessage = CompositeFormatCache.Format(Properties.Resources.MsgFocusPointNoPixels, label);
                return false;
            }

            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(avgX, avgY, avgZ);
            measurement = new ImageMeasurement(label, avgX, avgY, avgZ, chromaticity);
            return true;
        }

        private bool TryCreateFocusPointMeasurementPoint(DVCircleText circle, out MeasurementPoint point, out string? errorMessage)
        {
            point = default!;
            if (!TryCreateFocusPointMeasurement(circle, out ImageMeasurement measurement, out _, out errorMessage))
            {
                return false;
            }

            string pointName = ResolveFocusCircleName(circle);
            double radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            double azimuthDegrees = FocusPointMeasurementService.GetFullAzimuthAngle(circle.Attribute.Center, currentImageCenter);
            double polarDegrees = FocusPointMeasurementService.GetPolarRadiusAngle(circle.Attribute.Center, currentImageCenter, currentImageRadius, MaxAngle);
            double radiusDegrees = FocusPointMeasurementService.GetFocusCircleRadiusAngle(radiusPixels, currentPixelsPerDegree, currentImageRadius, MaxAngle);
            point = new MeasurementPoint(pointName, pointName, measurement, azimuthDegrees, polarDegrees, radiusDegrees);
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

            return FocusPointMeasurementService.TryCalculateCircleRoiAverage(
                XMat, YMat, ZMat,
                currentBitmapSource.PixelWidth, currentBitmapSource.PixelHeight,
                imageCenter, imageRadius,
                out avgX, out avgY, out avgZ, out sampleCount);
        }

        private static string ResolveFocusCircleName(DVCircleText circle)
        {
            return FocusPointMeasurementService.ResolveFocusCircleName(circle.Attribute.Text, circle.Attribute.Id);
        }


    }
}
