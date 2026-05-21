using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializeContrastControls()
        {
            isUpdatingContrastControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbContrastReferenceKind, ContrastConfig.ReferenceKind.ToString());
            }
            finally
            {
                isUpdatingContrastControls = false;
            }

            UpdateContrastReferenceUi();
        }

        private ContrastReferenceKind GetSelectedContrastReferenceKind()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbContrastReferenceKind, ContrastConfig.ReferenceKind);
        }

        private static string GetContrastReferenceKindText(ContrastReferenceKind kind)
        {
            return kind == ContrastReferenceKind.Black ? "黑场" : "白场";
        }

        private void UpdateContrastReferenceUi()
        {
            if (tbContrastReferenceStatus == null || btnSaveContrastReference == null)
            {
                return;
            }

            ContrastReferenceKind kind = GetSelectedContrastReferenceKind();
            tbContrastReferenceStatus.Text = GetContrastReferenceStatusText(kind);

            if (contrastReferenceYMat != null)
            {
                btnSaveContrastReference.Content = $"已保存{GetContrastReferenceKindText(kind)}基准";
                btnSaveContrastReference.Background = Brushes.LightGreen;
                btnSaveContrastReference.Foreground = Brushes.Black;
            }
            else
            {
                btnSaveContrastReference.Content = "保存基准图";
                btnSaveContrastReference.ClearValue(BackgroundProperty);
                btnSaveContrastReference.ClearValue(ForegroundProperty);
            }
        }

        private string GetContrastReferenceStatusText(ContrastReferenceKind kind)
        {
            if (contrastReferenceYMat == null)
            {
                return "未保存对比度基准图。先打开或采集一张白图/黑图，选择基准类型后保存。";
            }

            if (YMat != null && (YMat.Width != contrastReferenceYMat.Width || YMat.Height != contrastReferenceYMat.Height))
            {
                return "对比度基准图尺寸与当前图像不一致，请重新保存基准图。";
            }

            return $"已保存{GetContrastReferenceKindText(kind)}基准: {Path.GetFileName(contrastReferenceFileName)}";
        }

        private void EnsureContrastReferenceReady()
        {
            if (contrastReferenceYMat == null)
            {
                throw new InvalidOperationException("请先保存对比度基准图");
            }

            if (YMat != null && (YMat.Width != contrastReferenceYMat.Width || YMat.Height != contrastReferenceYMat.Height))
            {
                throw new InvalidOperationException("对比度基准图尺寸与当前图像不一致，请重新保存基准图");
            }
        }

        private bool CanRefreshContrastDisplay()
        {
            if (contrastReferenceYMat == null)
            {
                UpdateContrastReferenceUi();
                return false;
            }

            if (YMat != null && (YMat.Width != contrastReferenceYMat.Width || YMat.Height != contrastReferenceYMat.Height))
            {
                UpdateContrastReferenceUi();
                return false;
            }

            return true;
        }

        private OpenCvSharp.Mat CreateContrastMat()
        {
            if (YMat == null)
            {
                throw new InvalidOperationException(Properties.Resources.XYZDataNotLoaded);
            }

            EnsureContrastReferenceReady();
            return ConoscopeColorimetry.CreateContrastMat(YMat, contrastReferenceYMat!, GetSelectedContrastReferenceKind());
        }

        private double GetContrastValue(int ix, int iy, double currentY)
        {
            if (contrastReferenceYMat == null || YMat == null)
            {
                return double.NaN;
            }

            if (YMat.Width != contrastReferenceYMat.Width || YMat.Height != contrastReferenceYMat.Height)
            {
                return double.NaN;
            }

            if (ix < 0 || iy < 0 || ix >= contrastReferenceYMat.Width || iy >= contrastReferenceYMat.Height)
            {
                return double.NaN;
            }

            double referenceY = contrastReferenceYMat.At<float>(iy, ix);
            return ConoscopeColorimetry.CalculateContrast(currentY, referenceY, GetSelectedContrastReferenceKind());
        }

        private void ContrastReferenceKind_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingContrastControls)
            {
                return;
            }

            ContrastConfig.ReferenceKind = GetSelectedContrastReferenceKind();
            UpdateContrastReferenceUi();

            if (GetSelectedDisplayChannel() == ExportChannel.Contrast && HasXyzData() && CanRefreshContrastDisplay())
            {
                try
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
                catch (Exception ex)
                {
                    log.Error($"切换对比度基准类型失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "对比度", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void btnSaveContrastReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (YMat == null)
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                contrastReferenceYMat?.Dispose();
                contrastReferenceYMat = YMat.Clone();
                contrastReferenceFileName = Filename;
                ContrastConfig.ReferenceKind = GetSelectedContrastReferenceKind();
                UpdateContrastReferenceUi();
            }
            catch (Exception ex)
            {
                log.Error($"保存对比度基准图失败: {ex.Message}", ex);
                MessageBox.Show($"保存对比度基准图失败: {ex.Message}", Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCalculateContrast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureContrastReferenceReady();
                bool refreshAfterSelection = GetSelectedDisplayChannel() == ExportChannel.Contrast;
                ComboBoxHelper.SelectItemByTag(cbDisplayChannel, ExportChannel.Contrast.ToString());
                if (refreshAfterSelection && GetSelectedDisplayChannel() == ExportChannel.Contrast && HasXyzData())
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
            }
            catch (Exception ex)
            {
                log.Error($"计算对比度失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, "对比度", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
