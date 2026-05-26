using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        public void ExportAngleMode()
        {
            try
            {
                if (!TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, "DiameterLine_Export_"))
                {
                    return;
                }

                ConoscopeExportService.ExportAngleModeToCsv(filePath!, channel, CreateExportContext(), ConoscopeManager.GetInstance().Config.Export.DecimalPlaces);
                OnExportSuccess(filePath!);
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgAzimuthExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ExportCircleMode()
        {
            try
            {
                if (!TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, "RCircle_Export_", ConoscopeConfig.CurrentModel.ToString()))
                {
                    return;
                }

                ConoscopeExportService.ExportCircleModeToCsv(filePath!, channel, CreateExportContext(), ConoscopeManager.GetInstance().Config.Export.DecimalPlaces);
                OnExportSuccess(filePath!);
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgPolarExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryPrepareSimpleExport(out ExportChannel channel, out string? filePath, string filePrefix, string? suffix = null)
        {
            channel = default;
            filePath = null;

            if (currentBitmapSource == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            channel = GetSelectedCurrentCurveChannel();
            if (!EnsureExportChannelReady(channel))
            {
                return false;
            }

            string suffixPart = string.IsNullOrEmpty(suffix) ? "" : $"{suffix}_";
            string fileName = $"{filePrefix}{channel}_{suffixPart}{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            filePath = TrySelectCsvSavePath(fileName);
            return filePath != null;
        }

        private void OnExportSuccess(string filePath)
        {
            MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            log.Info($"导出成功: {filePath}");
        }

        private ExportChannel GetSelectedExportChannel() => selectedExportChannel;
        private ExportChannel GetSelectedCurrentCurveChannel() => GetSelectedDisplayChannel();

        private bool EnsureExportChannelReady(ExportChannel channel)
        {
            bool ready = channel == ExportChannel.Y
                ? YMat != null
                : HasXyzData()
                    && (channel != ExportChannel.Contrast || CanRefreshContrastDisplay())
                    && (channel != ExportChannel.ColorDifference || CanRefreshColorDifferenceDisplay());

            if (!ready)
            {
                MessageBox.Show(Properties.Resources.XYZDataNotLoaded, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return ready;
        }

        private string? TrySelectCsvSavePath(string defaultFileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = Properties.Resources.LabelSaveFilterCsv,
                DefaultExt = "csv",
                FileName = defaultFileName,
                RestoreDirectory = true
            };

            return saveFileDialog.ShowDialog() == true ? saveFileDialog.FileName : null;
        }

        private ConoscopeExportContext CreateExportContext()
        {
            if (YMat == null)
            {
                throw new InvalidOperationException(Properties.Resources.XYZDataNotLoaded);
            }

            double pixelsPerDegree = currentPixelsPerDegree > 0
                ? currentPixelsPerDegree
                : CurrentModelProfile.GetConoscopeCoefficient(YMat.Width, YMat.Height);

            return new ConoscopeExportContext
            {
                ModelName = ConoscopeConfig.CurrentModel.ToString(),
                ImageWidth = YMat.Width,
                ImageHeight = YMat.Height,
                Center = currentImageCenter,
                MaxAngle = MaxAngle,
                PixelsPerDegree = pixelsPerDegree,
                ReadXyz = (ix, iy) =>
                {
                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    return new ConoscopeXyzValue(X, Y, Z);
                },
                ReadColorDifference = (ix, iy) =>
                {
                    ExtractXYZValues(ix, iy, out double X, out double Y, out double Z);
                    return GetColorDifferenceValue(ix, iy, X, Y, Z);
                },
                ReadContrast = (ix, iy) =>
                {
                    EnsureContrastReferenceReady();
                    ExtractXYZValues(ix, iy, out _, out double Y, out _);
                    return GetContrastValue(ix, iy, Y);
                }
            };
        }

        public void AdvancedExport()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AdvancedExportSettings settings = GetAdvancedExportSettings();
                int decimalPlaces = ConoscopeManager.GetInstance().Config.Export.DecimalPlaces;
                AdvancedExportDialog dialog = new AdvancedExportDialog(settings, decimalPlaces) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true)
                {
                    settings = dialog.Settings;
                    SaveAdvancedExportSettings(settings);

                    if (!HasXyzData() && settings.Channels.Exists(channel => channel != ExportChannel.Y))
                    {
                        MessageBox.Show(Properties.Resources.XYZDataNotLoaded, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    PerformAdvancedExport(settings);
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgAdvancedExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static AdvancedExportSettings GetAdvancedExportSettings()
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            ConoscopeAdvancedExportState saved = config.AdvancedExport;

            return new AdvancedExportSettings
            {
                FilePrefix = saved.FilePrefix,
                Channels = saved.Channels is { Count: > 0 }
                    ? new List<ExportChannel>(saved.Channels)
                    : new List<ExportChannel> { ExportChannel.Y },
                ExportAzimuth = saved.ExportAzimuth,
                ExportPolar = saved.ExportPolar,
                AzimuthStep = saved.AzimuthStep,
                RadialStep = saved.RadialStep,
                PolarStep = saved.PolarStep,
                CircumferentialStep = saved.CircumferentialStep,
                DecimalPlaces = config.Export.DecimalPlaces,
                EnableCrossSection = saved.EnableCrossSection,
                CrossSectionType = saved.UseAzimuthCrossSection ? CrossSectionType.Azimuth : CrossSectionType.Polar,
                CrossSectionAzimuthAngle = saved.CrossSectionAzimuthAngle,
                CrossSectionPolarAngle = saved.CrossSectionPolarAngle,
                CrossSectionAngle = saved.UseAzimuthCrossSection ? saved.CrossSectionAzimuthAngle : saved.CrossSectionPolarAngle
            };
        }

        private static void SaveAdvancedExportSettings(AdvancedExportSettings settings)
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            config.AdvancedExport = new ConoscopeAdvancedExportState
            {
                FilePrefix = settings.FilePrefix,
                Channels = settings.Channels.Count > 0
                    ? new List<ExportChannel>(settings.Channels)
                    : new List<ExportChannel> { ExportChannel.Y },
                ExportAzimuth = settings.ExportAzimuth,
                ExportPolar = settings.ExportPolar,
                AzimuthStep = settings.AzimuthStep,
                RadialStep = settings.RadialStep,
                PolarStep = settings.PolarStep,
                CircumferentialStep = settings.CircumferentialStep,
                EnableCrossSection = settings.EnableCrossSection,
                UseAzimuthCrossSection = settings.CrossSectionType == CrossSectionType.Azimuth,
                CrossSectionAzimuthAngle = settings.CrossSectionAzimuthAngle,
                CrossSectionPolarAngle = settings.CrossSectionPolarAngle
            };
            config.Export.DecimalPlaces = settings.DecimalPlaces;

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception ex)
            {
                log.Warn($"保存高级导出配置失败: {ex.Message}");
            }
        }

        private void PerformAdvancedExport(AdvancedExportSettings settings)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int filesExported = 0;

                using System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = Properties.Resources.MsgSelectExportFolder;
                folderDialog.ShowNewFolderButton = true;

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                string outputFolder = folderDialog.SelectedPath;
                ConoscopeExportContext exportContext = CreateExportContext();

                if (settings.ExportAzimuth)
                {
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Azimuth_{channel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportAzimuthWithStep(filePath, channel, exportContext, settings.AzimuthStep, settings.RadialStep, settings.DecimalPlaces);
                        filesExported++;
                        log.Info($"方位角导出成功: {filePath}");
                    }
                }

                if (settings.ExportPolar)
                {
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Polar_{channel}_{ConoscopeConfig.CurrentModel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportPolarWithStep(filePath, channel, exportContext, settings.PolarStep, settings.CircumferentialStep, settings.DecimalPlaces);
                        filesExported++;
                        log.Info($"极角导出成功: {filePath}");
                    }
                }

                if (settings.EnableCrossSection)
                {
                    ExportCrossSectionToFolder(exportContext, settings, timestamp, outputFolder, ref filesExported);
                }

                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportDone, filesExported, outputFolder), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"高级导出完成: {filesExported} 个文件");
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCrossSectionToFolder(ConoscopeExportContext exportContext, AdvancedExportSettings settings, string timestamp, string outputFolder, ref int filesExported)
        {
            try
            {
                ConoscopeCrossSectionExportOptions exportOptions = new ConoscopeCrossSectionExportOptions
                {
                    StepDegrees = settings.CrossSectionType == CrossSectionType.Azimuth
                        ? settings.RadialStep
                        : settings.CircumferentialStep,
                    IncludeMetadata = true,
                    DecimalPlaces = settings.DecimalPlaces
                };
                foreach (ExportChannel channel in settings.Channels)
                {
                    string sectionType = settings.CrossSectionType == CrossSectionType.Azimuth ? "Azimuth" : "Polar";
                    string filename = $"{settings.FilePrefix}_CrossSection_{sectionType}_{settings.CrossSectionAngle}deg_{channel}_{timestamp}.csv";
                    string filePath = Path.Combine(outputFolder, filename);

                    if (settings.CrossSectionType == CrossSectionType.Azimuth)
                    {
                        ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle, exportOptions);
                    }
                    else
                    {
                        ConoscopeExportService.ExportPolarCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle, exportOptions);
                    }

                    filesExported++;
                    log.Info($"截面导出成功: {filePath}");
                }
            }
            catch (Exception ex)
            {
                log.Error($"截面导出失败: {ex.Message}", ex);
                throw;
            }
        }

        private void btnExportCurrentReference_Click(object sender, RoutedEventArgs e)
        {
            if (CoordinateAxisConfig.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
            {
                btnExportCurrentAzimuth_Click(sender, e);
            }
            else
            {
                btnExportCurrentPolar_Click(sender, e);
            }
        }

        private ConoscopeCrossSectionExportOptions? ShowCurrentCurveExportDialog()
        {
            ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
            ConoscopeCrossSectionExportOptions currentOptions = new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = exportConfig.CurrentCurveStepDegrees,
                IncludeMetadata = exportConfig.IncludeMetadata,
                DecimalPlaces = exportConfig.DecimalPlaces
            };

            CurrentCurveExportDialog dialog = new CurrentCurveExportDialog(currentOptions)
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.ExportOptions : null;
        }

        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPolarLine == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectAzimuthFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryExportCurrentCrossSection(
                "Azimuth",
                selectedPolarLine.Angle,
                Properties.Resources.MsgAzimuthExportSuccess,
                (filePath, channel, context, angle, options) =>
                    ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, context, angle, options));
        }

        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            if (selectedCircleLine == null)
            {
                MessageBox.Show(Properties.Resources.MsgSelectPolarFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TryExportCurrentCrossSection(
                "Polar",
                selectedCircleLine.RadiusAngle,
                Properties.Resources.MsgPolarExportSuccess,
                (filePath, channel, context, angle, options) =>
                    ConoscopeExportService.ExportPolarCrossSection(filePath, channel, context, angle, options));
        }

        private void TryExportCurrentCrossSection(
            string sectionLabel,
            double angle,
            string successMessageResource,
            Action<string, ExportChannel, ConoscopeExportContext, double, ConoscopeCrossSectionExportOptions> exportAction)
        {
            try
            {
                if (YMat == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedCurrentCurveChannel();
                if (!EnsureExportChannelReady(channel))
                {
                    return;
                }

                ConoscopeCrossSectionExportOptions? exportOptions = ShowCurrentCurveExportDialog();
                if (exportOptions == null)
                {
                    return;
                }

                string? filePath = TrySelectCsvSavePath($"{sectionLabel}_{angle}deg_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                exportAction(filePath, channel, CreateExportContext(), angle, exportOptions);
                MessageBox.Show(CompositeFormatCache.Format(successMessageResource, angle), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);

                ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
                exportConfig.CurrentCurveStepDegrees = exportOptions.StepDegrees;
                exportConfig.IncludeMetadata = exportOptions.IncludeMetadata;
                exportConfig.DecimalPlaces = exportOptions.DecimalPlaces;
                try
                {
                    ConfigService.Instance.Save<ConoscopeConfig>();
                }
                catch (Exception ex)
                {
                    log.Warn($"保存当前曲线导出选项失败: {ex.Message}");
                }

                log.Info($"{sectionLabel}截面导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"{sectionLabel}截面导出失败: {ex.Message}", ex);
                MessageBox.Show(CompositeFormatCache.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
