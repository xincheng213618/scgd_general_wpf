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

            if (!SaveFocusPoiTemplate(poiParam))
            {
                return;
            }

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

        private bool SaveFocusPoiTemplate(PoiParam poiParam)
        {
            UpdatePoiTemplateImageSize(poiParam);
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
                    MessageBox.Show("保存失败，具体报错信息请查看日志。", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("保存 Conoscope 关注点 POI 模板失败", ex);
                MessageBox.Show($"保存失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void SyncReferenceInteractionToggle()
        {
            if (tglReferenceInteraction == null)
            {
                return;
            }

            bool isInteractionEnabled = CoordinateAxisConfig.IsInteractionEnabled;
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
            CoordinateAxisConfig.IsInteractionEnabled = isEnabled;
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
                Title = "关注点极坐标编辑"
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

            DVCircleText? circle = ImageView.SelectedFocusCircle;
            if (circle == null)
            {
                tbSelectedFocusPointInfo.Text = string.Empty;
                tbSelectedFocusPointInfo.ToolTip = "当前选中关注点";
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
            Point center = circle.Attribute.Center;
            double azimuthDegrees = GetFullAzimuthAngle(center);
            double polarDegrees = GetPolarRadiusAngle(center);
            double radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            double radiusDegrees = GetFocusCircleRadiusAngle(radiusPixels);
            string info = $"{ResolveFocusCircleName(circle)}  方位 {azimuthDegrees:F2}°  极角 {polarDegrees:F2}°  R {radiusPixels:F1}px/{radiusDegrees:F2}°";

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
                if (!TryCreateFocusPointResult(circle, out PoiResultCIExyuvData? result, out int sampleCount, out string? errorMessage))
                {
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        failedCircles.Add(errorMessage);
                    }
                    continue;
                }

                results.Add(result);
                circle.Attribute.Msg = FormatFocusPointOverlayMessage(circle, result, sampleCount);
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
            UpdateSelectedFocusPointInfo();
        }

        private string FormatFocusPointOverlayMessage(DVCircleText circle, PoiResultCIExyuvData result, int sampleCount)
        {
            double radiusDegrees = GetFocusCircleRadiusAngle(Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius));
            return $"{string.Format(Properties.Resources.FocusPointYUV, result.Y.ToString("F3"), result.u.ToString("F4"), result.v.ToString("F4"))}  R:{radiusDegrees:F2}°  N:{sampleCount}";
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

        private bool TryCreateFocusPointMeasurement(DVCircleText circle, out ImageMeasurement measurement, out string? errorMessage)
        {
            return TryCreateFocusPointMeasurement(circle, out measurement, out _, out errorMessage);
        }

        private bool TryCreateFocusPointMeasurement(DVCircleText circle, out ImageMeasurement measurement, out int sampleCount, out string? errorMessage)
        {
            measurement = default!;
            errorMessage = null;
            sampleCount = 0;

            double radius = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
            if (!TryCalculateFocusPointAverage(circle.Attribute.Center, radius, out double avgX, out double avgY, out double avgZ, out sampleCount))
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

        private double GetPolarDistancePixels(double polarDegrees)
        {
            double clampedPolar = Math.Max(0, Math.Min(polarDegrees, MaxAngle));
            if (currentPixelsPerDegree > double.Epsilon)
            {
                return clampedPolar * currentPixelsPerDegree;
            }

            if (currentImageRadius > 0 && MaxAngle > double.Epsilon)
            {
                return clampedPolar / MaxAngle * currentImageRadius;
            }

            return 0;
        }

        private double GetPolarAngleFromDistancePixels(double distancePixels)
        {
            double distance = Math.Max(0, distancePixels);
            if (currentPixelsPerDegree > double.Epsilon)
            {
                return Math.Max(0, Math.Min(distance / currentPixelsPerDegree, MaxAngle));
            }

            if (currentImageRadius > 0)
            {
                return Math.Max(0, Math.Min(distance / currentImageRadius * MaxAngle, MaxAngle));
            }

            return 0;
        }

        private double GetFocusCircleRadiusPixelsFromAngle(double radiusDegrees)
        {
            double angle = Math.Max(0, radiusDegrees);
            if (currentPixelsPerDegree > double.Epsilon)
            {
                return Math.Max(ConoscopeImageHost.MinimumFocusCircleRadius, angle * currentPixelsPerDegree);
            }

            if (currentImageRadius > 0 && MaxAngle > double.Epsilon)
            {
                return Math.Max(ConoscopeImageHost.MinimumFocusCircleRadius, angle / MaxAngle * currentImageRadius);
            }

            return ConoscopeImageHost.MinimumFocusCircleRadius;
        }

        private Point CreatePointFromPolar(double azimuthDegrees, double distancePixels)
        {
            double radians = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(azimuthDegrees) * Math.PI / 180.0;
            return new Point(
                currentImageCenter.X + Math.Cos(radians) * distancePixels,
                currentImageCenter.Y - Math.Sin(radians) * distancePixels);
        }

        public sealed class FocusPointPolarEditModel : ViewModelBase
        {
            private ConoscopeView? owner;
            private DVCircleText? circle;
            private bool isUpdating;
            private string name = string.Empty;
            private double azimuthDegrees;
            private double polarDegrees;
            private double distancePixels;
            private double radiusPixels;
            private double radiusDegrees;

            public FocusPointPolarEditModel()
            {
            }

            public void Initialize(ConoscopeView owner, DVCircleText circle)
            {
                this.owner = owner;
                this.circle = circle;
                SyncFromCircle();
            }

            [Category("关注点"), DisplayName("名称")]
            public string Name
            {
                get => name;
                set
                {
                    string newValue = value ?? string.Empty;
                    if (name == newValue)
                    {
                        return;
                    }

                    name = newValue;
                    if (circle != null)
                    {
                        circle.Attribute.Text = name;
                        ApplyVisualUpdate();
                    }

                    OnPropertyChanged();
                }
            }

            [Category("位置"), DisplayName("方位角(°)")]
            public double AzimuthDegrees
            {
                get => azimuthDegrees;
                set
                {
                    double normalized = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(value);
                    if (AreClose(azimuthDegrees, normalized))
                    {
                        return;
                    }

                    azimuthDegrees = normalized;
                    OnPropertyChanged();
                    ApplyCenterFromPolar();
                }
            }

            [Category("位置"), DisplayName("极角(°)")]
            public double PolarDegrees
            {
                get => polarDegrees;
                set
                {
                    double clamped = owner == null ? Math.Max(0, value) : Math.Max(0, Math.Min(value, owner.MaxAngle));
                    if (AreClose(polarDegrees, clamped))
                    {
                        return;
                    }

                    polarDegrees = clamped;
                    distancePixels = owner?.GetPolarDistancePixels(polarDegrees) ?? distancePixels;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DistancePixels));
                    ApplyCenterFromPolar();
                }
            }

            [Category("位置"), DisplayName("距离(px)")]
            public double DistancePixels
            {
                get => distancePixels;
                set
                {
                    double clamped = ClampDistancePixels(value);
                    if (AreClose(distancePixels, clamped))
                    {
                        return;
                    }

                    distancePixels = clamped;
                    polarDegrees = owner?.GetPolarAngleFromDistancePixels(distancePixels) ?? polarDegrees;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PolarDegrees));
                    ApplyCenterFromPolar();
                }
            }

            [Category("大小"), DisplayName("半径(px)")]
            public double RadiusPixels
            {
                get => radiusPixels;
                set
                {
                    double clamped = Math.Max(value, ConoscopeImageHost.MinimumFocusCircleRadius);
                    if (AreClose(radiusPixels, clamped))
                    {
                        return;
                    }

                    radiusPixels = clamped;
                    radiusDegrees = owner?.GetFocusCircleRadiusAngle(radiusPixels) ?? radiusDegrees;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusDegrees));
                    ApplyRadius();
                }
            }

            [Category("大小"), DisplayName("半径(°)")]
            public double RadiusDegrees
            {
                get => radiusDegrees;
                set
                {
                    double clamped = owner == null ? Math.Max(0, value) : Math.Max(0, Math.Min(value, owner.MaxAngle));
                    if (AreClose(radiusDegrees, clamped))
                    {
                        return;
                    }

                    radiusDegrees = clamped;
                    radiusPixels = owner?.GetFocusCircleRadiusPixelsFromAngle(radiusDegrees) ?? radiusPixels;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RadiusPixels));
                    ApplyRadius();
                }
            }

            private void SyncFromCircle()
            {
                if (owner == null || circle == null)
                {
                    return;
                }

                isUpdating = true;
                try
                {
                    name = ResolveFocusCircleName(circle);
                    Point center = circle.Attribute.Center;
                    azimuthDegrees = owner.GetFullAzimuthAngle(center);
                    polarDegrees = owner.GetPolarRadiusAngle(center);
                    distancePixels = (center - owner.currentImageCenter).Length;
                    radiusPixels = Math.Max(circle.Attribute.Radius, ConoscopeImageHost.MinimumFocusCircleRadius);
                    radiusDegrees = owner.GetFocusCircleRadiusAngle(radiusPixels);
                }
                finally
                {
                    isUpdating = false;
                }

                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(AzimuthDegrees));
                OnPropertyChanged(nameof(PolarDegrees));
                OnPropertyChanged(nameof(DistancePixels));
                OnPropertyChanged(nameof(RadiusPixels));
                OnPropertyChanged(nameof(RadiusDegrees));
            }

            private void ApplyCenterFromPolar()
            {
                if (isUpdating || owner == null || circle == null)
                {
                    return;
                }

                circle.Attribute.Center = owner.CreatePointFromPolar(azimuthDegrees, distancePixels);
                ApplyVisualUpdate();
            }

            private void ApplyRadius()
            {
                if (isUpdating || owner == null || circle == null)
                {
                    return;
                }

                circle.Attribute.Radius = radiusPixels;
                circle.Attribute.RadiusY = radiusPixels;
                ApplyVisualUpdate();
            }

            private double ClampDistancePixels(double value)
            {
                double distance = Math.Max(0, value);
                if (owner?.currentImageRadius > 0)
                {
                    distance = Math.Min(distance, owner.currentImageRadius);
                }

                return distance;
            }

            private void ApplyVisualUpdate()
            {
                if (owner == null || circle == null)
                {
                    return;
                }

                circle.Render();
                owner.ImageView.RefreshFocusCircleSelection();
                owner.UpdateSelectedFocusPointInfo();
            }

            private static bool AreClose(double left, double right)
            {
                return Math.Abs(left - right) < 0.000001;
            }
        }
    }
}
