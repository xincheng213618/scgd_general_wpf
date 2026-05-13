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
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                string? filePath = TrySelectCsvSavePath($"DiameterLine_Export_{channel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportAngleModeToCsv(filePath, channel, CreateExportContext());
                MessageBox.Show($"数据已成功导出到:\n{filePath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"成功导出方位角模式CSV: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"方位角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"方位角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                string? filePath = TrySelectCsvSavePath($"RCircle_Export_{channel}_{ConoscopeConfig.CurrentModel}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                if (filePath == null)
                {
                    return;
                }

                ConoscopeExportService.ExportCircleModeToCsv(filePath, channel, CreateExportContext());
                MessageBox.Show($"数据已成功导出到:\n{filePath}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"成功导出极角模式CSV: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"极角模式导出失败: {ex.Message}", ex);
                MessageBox.Show($"极角模式导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ExportChannel GetSelectedExportChannel()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbExportChannel, ExportChannel.Y);
        }

        private string? TrySelectCsvSavePath(string defaultFileName)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
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
                throw new InvalidOperationException("XYZ 数据未加载");
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
                }
            };
        }

        public void AdvancedExport()
        {
            try
            {
                if (currentBitmapSource == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show($"高级导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PerformAdvancedExport(AdvancedExportSettings settings)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                int filesExported = 0;

                using System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();
                folderDialog.Description = "选择导出文件夹";
                folderDialog.ShowNewFolderButton = true;

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                string outputFolder = folderDialog.SelectedPath;

                if (settings.EnableCrossSection)
                {
                    ExportCrossSectionToFolder(settings, timestamp, outputFolder, ref filesExported);
                    MessageBox.Show($"截面导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
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

                MessageBox.Show($"导出完成，共导出 {filesExported} 个文件到:\n{outputFolder}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"高级导出完成: {filesExported} 个文件");
            }
            catch (Exception ex)
            {
                log.Error($"高级导出执行失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private CurrentCurveExportSettings? ShowCurrentCurveExportDialog()
        {
            CurrentCurveExportDialog dialog = new CurrentCurveExportDialog
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.Settings : null;
        }

        private void btnExportCurrentAzimuth_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedPolarLine == null)
                {
                    MessageBox.Show("请先选择一个方位角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                CurrentCurveExportSettings? exportSettings = ShowCurrentCurveExportDialog();
                if (exportSettings == null)
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
                    new ConoscopeCrossSectionExportOptions
                    {
                        StepDegrees = exportSettings.StepDegrees,
                        IncludeMetadata = exportSettings.IncludeMetadata
                    });
                MessageBox.Show($"方位角 {selectedPolarLine.Angle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"单个方位角导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"导出当前方位角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportCurrentPolar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (selectedCircleLine == null)
                {
                    MessageBox.Show("请先选择一个极角", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (YMat == null)
                {
                    MessageBox.Show("请先加载图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ExportChannel channel = GetSelectedExportChannel();
                CurrentCurveExportSettings? exportSettings = ShowCurrentCurveExportDialog();
                if (exportSettings == null)
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
                    new ConoscopeCrossSectionExportOptions
                    {
                        StepDegrees = exportSettings.StepDegrees,
                        IncludeMetadata = exportSettings.IncludeMetadata
                    });
                MessageBox.Show($"极角 {selectedCircleLine.RadiusAngle}° 导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                log.Info($"单个极角导出成功: {filePath}");
            }
            catch (Exception ex)
            {
                log.Error($"导出当前极角失败: {ex.Message}", ex);
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}