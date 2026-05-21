using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            ExportAngleMode();
        }

        public void ExportAngleMode()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                string? filePath = TrySelectCsvSavePath($"DiameterLine_Export_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportAngleModeToCsv(filePath, channel, CreateExportContext());
                MessageBox.Show(string.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"成功导出方位角模式CSV: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgAzimuthExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            ExportCircleMode();
        }

        public void ExportCircleMode()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                string? filePath = TrySelectCsvSavePath($"RCircle_Export_{channel}_{ConoscopeConfig.CurrentModel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportCircleModeToCsv(filePath, channel, CreateExportContext());
                MessageBox.Show(string.Format(Properties.Resources.MsgExportSuccess, filePath), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"成功导出极角模式CSV: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgPolarExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ExportChannel GetSelectedExportChannel()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbExportChannel, ExportChannel.Y);
        }

        private ExportChannel GetSelectedCurrentCurveChannel()
        {
            return GetSelectedDisplayChannel();
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

                AdvancedExportDialog dialog = new AdvancedExportDialog { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true)
                {
                    AdvancedExportSettings settings = dialog.Settings;
                    PerformAdvancedExport(settings);
                }
            }
            catch (Exception ex)
            {
                log.Error($"高级导出失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgAdvancedExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (settings.EnableCrossSection)
                {
                    ExportCrossSectionToFolder(settings, timestamp, outputFolder, ref filesExported);
                    MessageBox.Show(string.Format(Properties.Resources.MsgSectionExportDone, filesExported, outputFolder), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                    log.Info($"截面导出完成: {filesExported} 个文件");
                    return;
                }

                if (settings.ExportAzimuth)
                {
                    ConoscopeExportContext exportContext = CreateExportContext();
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Azimuth_{channel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportAzimuthWithStep(filePath, channel, exportContext, settings.AzimuthStep, settings.RadialStep);
                        filesExported++;
                        log.Info($"方位角导出成功: {filePath}");
                    }
                }

                if (settings.ExportPolar)
                {
                    ConoscopeExportContext exportContext = CreateExportContext();
                    foreach (ExportChannel channel in settings.Channels)
                    {
                        string filename = $"{settings.FilePrefix}_Polar_{channel}_{ConoscopeConfig.CurrentModel}_{timestamp}.csv";
                        string filePath = Path.Combine(outputFolder, filename);
                        ConoscopeExportService.ExportPolarWithStep(filePath, channel, exportContext, settings.PolarStep, settings.CircumferentialStep);
                        filesExported++;
                        log.Info($"极角导出成功: {filePath}");
                    }
                }

                MessageBox.Show(string.Format(Properties.Resources.MsgExportDone, filesExported, outputFolder), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"高级导出完成: {filesExported} 个文件");
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCrossSectionToFolder(AdvancedExportSettings settings, string timestamp, string outputFolder, ref int filesExported)
        {
            try
            {
                ConoscopeExportContext exportContext = CreateExportContext();
                foreach (ExportChannel channel in settings.Channels)
                {
                    string sectionType = settings.CrossSectionType == CrossSectionType.Azimuth ? "Azimuth" : "Polar";
                    string filename = $"{settings.FilePrefix}_CrossSection_{sectionType}_{settings.CrossSectionAngle}deg_{channel}_{timestamp}.csv";
                    string filePath = Path.Combine(outputFolder, filename);

                    if (settings.CrossSectionType == CrossSectionType.Azimuth)
                    {
                        ConoscopeExportService.ExportAzimuthCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle);
                    }
                    else
                    {
                        ConoscopeExportService.ExportPolarCrossSection(filePath, channel, exportContext, settings.CrossSectionAngle);
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
            if (CurrentModelProfile.CoordinateAxisParam.ReferenceMode == ConoscopeCoordinateReferenceMode.AzimuthLine)
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
            CurrentCurveExportDialog dialog = new CurrentCurveExportDialog(GetCurrentCurveExportOptions())
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.ExportOptions : null;
        }

        private static ConoscopeCrossSectionExportOptions GetCurrentCurveExportOptions()
        {
            ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
            return new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = exportConfig.CurrentCurveStepDegrees,
                IncludeMetadata = exportConfig.IncludeMetadata
            };
        }

        private static void SaveCurrentCurveExportOptions(ConoscopeCrossSectionExportOptions options)
        {
            ConoscopeExportSettings exportConfig = ConoscopeManager.GetInstance().Config.Export;
            exportConfig.CurrentCurveStepDegrees = options.StepDegrees;
            exportConfig.IncludeMetadata = options.IncludeMetadata;

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception ex)
            {
                log.Error($"保存当前曲线导出配置失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgCurveExportConfigSaveFailed, ex.Message), Properties.Resources.TitleCurrentCurveExport, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedPolarLine == null)
                {
                    MessageBox.Show(Properties.Resources.MsgSelectAzimuthFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedCurrentCurveChannel();
                ConoscopeCrossSectionExportOptions? exportOptions = ShowCurrentCurveExportDialog();
                if (exportOptions == null)
                {
                    return;
                }

                string? filePath = TrySelectCsvSavePath($"Azimuth_{selectedPolarLine.Angle}deg_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportAzimuthCrossSection(
                    filePath,
                    channel,
                    CreateExportContext(),
                    selectedPolarLine.Angle,
                    exportOptions);
                MessageBox.Show(string.Format(Properties.Resources.MsgAzimuthExportSuccess, selectedPolarLine.Angle), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                SaveCurrentCurveExportOptions(exportOptions);
                log.Info($"单个方位角导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"导出当前方位角失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedCircleLine == null)
                {
                    MessageBox.Show(Properties.Resources.MsgSelectPolarFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedCurrentCurveChannel();
                ConoscopeCrossSectionExportOptions? exportOptions = ShowCurrentCurveExportDialog();
                if (exportOptions == null)
                {
                    return;
                }

                string? filePath = TrySelectCsvSavePath($"Polar_{selectedCircleLine.RadiusAngle}deg_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportPolarCrossSection(
                    filePath,
                    channel,
                    CreateExportContext(),
                    selectedCircleLine.RadiusAngle,
                    exportOptions);
                MessageBox.Show(string.Format(Properties.Resources.MsgPolarExportSuccess, selectedCircleLine.RadiusAngle), Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                SaveCurrentCurveExportOptions(exportOptions);
                log.Info($"单个极角导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"导出当前极角失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgExportFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
