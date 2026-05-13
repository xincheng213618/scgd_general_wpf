using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializeColorDifferenceControls()
        {
            isUpdatingColorDifferenceControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbColorDifferenceReference, ConoscopeConfig.ColorDifferenceReferenceMode.ToString());
                txtColorDifferenceCustomU.Text = ConoscopeConfig.ColorDifferenceCustomU.ToString("F4", CultureInfo.InvariantCulture);
                txtColorDifferenceCustomV.Text = ConoscopeConfig.ColorDifferenceCustomV.ToString("F4", CultureInfo.InvariantCulture);
            }
            finally
            {
                isUpdatingColorDifferenceControls = false;
            }

            UpdateColorDifferenceReferenceUi();
        }

        private void UpdateColorDifferencePanelVisibility()
        {
            if (gbColorDifference == null)
            {
                return;
            }

            gbColorDifference.Visibility = Visibility.Visible;
        }

        private ColorDifferenceReferenceMode GetSelectedColorDifferenceReferenceMode()
        {
            return ComboBoxHelper.GetSelectedEnumByTag(cbColorDifferenceReference, ConoscopeConfig.ColorDifferenceReferenceMode);
        }

        private static ConoscopeUvReference GetStandardColorDifferenceReference(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => new ConoscopeUvReference(0.1978, 0.4684),
                ColorDifferenceReferenceMode.D50 => new ConoscopeUvReference(0.2009, 0.4707),
                ColorDifferenceReferenceMode.A => new ConoscopeUvReference(0.2560, 0.5242),
                ColorDifferenceReferenceMode.D75 => new ConoscopeUvReference(0.1952, 0.4670),
                _ => throw new InvalidOperationException("当前色差基准不是固定光源")
            };
        }

        private bool TryParseCustomColorDifferenceReference(out ConoscopeUvReference reference)
        {
            reference = default;
            if (!ConoscopeNumericHelper.TryParseDouble(txtColorDifferenceCustomU?.Text, out double u)
                || !ConoscopeNumericHelper.TryParseDouble(txtColorDifferenceCustomV?.Text, out double v))
            {
                return false;
            }

            ConoscopeConfig.ColorDifferenceCustomU = u;
            ConoscopeConfig.ColorDifferenceCustomV = v;
            reference = new ConoscopeUvReference(u, v);
            return true;
        }

        private ConoscopeUvReference ResolvePointColorDifferenceReference()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode is ColorDifferenceReferenceMode.D65 or ColorDifferenceReferenceMode.D50 or ColorDifferenceReferenceMode.A or ColorDifferenceReferenceMode.D75)
            {
                return GetStandardColorDifferenceReference(mode);
            }

            if (mode == ColorDifferenceReferenceMode.Custom)
            {
                if (!TryParseCustomColorDifferenceReference(out ConoscopeUvReference customReference))
                {
                    throw new InvalidOperationException("请输入有效的自定义 u/v 基准坐标");
                }

                return customReference;
            }

            if (mode == ColorDifferenceReferenceMode.ImageCenter)
            {
                return CalculateImageCenterColorDifferenceReference();
            }

            throw new InvalidOperationException("实测基准图需要保存基准图后逐点计算");
        }

        private ConoscopeUvReference CalculateImageCenterColorDifferenceReference()
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("请先加载图像");
            }

            int centerX = XMat.Width / 2;
            int centerY = XMat.Height / 2;
            const int roiRadius = 25;
            double sumU = 0;
            double sumV = 0;
            int count = 0;

            int startY = Math.Max(0, centerY - roiRadius);
            int endY = Math.Min(XMat.Height - 1, centerY + roiRadius);
            int startX = Math.Max(0, centerX - roiRadius);
            int endX = Math.Min(XMat.Width - 1, centerX + roiRadius);

            for (int y = startY; y <= endY; y++)
            {
                for (int x = startX; x <= endX; x++)
                {
                    int dx = x - centerX;
                    int dy = y - centerY;
                    if (dx * dx + dy * dy > roiRadius * roiRadius)
                    {
                        continue;
                    }

                    ExtractXYZValues(x, y, out double X, out double Y, out double Z);
                    ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);
                    sumU += chromaticity.u;
                    sumV += chromaticity.v;
                    count++;
                }
            }

            if (count == 0)
            {
                throw new InvalidOperationException("图像中心 50px 关注点内没有可用像素");
            }

            return new ConoscopeUvReference(sumU / count, sumV / count);
        }

        private void UpdateColorDifferenceReferenceUi()
        {
            if (panelColorDifferenceCustomUv == null || tbColorDifferenceReferenceStatus == null || btnSaveColorDifferenceReference == null)
            {
                return;
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            panelColorDifferenceCustomUv.Visibility = mode == ColorDifferenceReferenceMode.Custom ? Visibility.Visible : Visibility.Collapsed;
            tbColorDifferenceReferenceStatus.Text = GetColorDifferenceReferenceStatusText(mode);

            if (colorDifferenceReferenceUMat != null && colorDifferenceReferenceVMat != null)
            {
                btnSaveColorDifferenceReference.Content = "基准图已保存";
                btnSaveColorDifferenceReference.Background = Brushes.LightGreen;
                btnSaveColorDifferenceReference.Foreground = Brushes.Black;
            }
            else
            {
                btnSaveColorDifferenceReference.Content = "保存色差基准图";
                btnSaveColorDifferenceReference.ClearValue(BackgroundProperty);
                btnSaveColorDifferenceReference.ClearValue(ForegroundProperty);
            }
        }

        private string GetColorDifferenceReferenceStatusText(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => "D65: u=0.1978, v=0.4684",
                ColorDifferenceReferenceMode.D50 => "D50: u=0.2009, v=0.4707",
                ColorDifferenceReferenceMode.A => "A: u=0.2560, v=0.5242",
                ColorDifferenceReferenceMode.D75 => "D75: u=0.1952, v=0.4670",
                ColorDifferenceReferenceMode.ImageCenter => "基准: 当前图像中心直径 50px 关注点平均 uv",
                ColorDifferenceReferenceMode.Custom => $"自定义: u={ConoscopeConfig.ColorDifferenceCustomU:F4}, v={ConoscopeConfig.ColorDifferenceCustomV:F4}",
                ColorDifferenceReferenceMode.ReferenceImage => colorDifferenceReferenceUMat == null
                    ? "基准: 尚未保存实测基准图"
                    : $"基准图: {Path.GetFileName(colorDifferenceReferenceFileName)}",
                _ => string.Empty
            };
        }

        private OpenCvSharp.Mat CreateColorDifferenceMat()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                throw new InvalidOperationException("XYZ 数据未加载");
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                EnsureColorDifferenceReferenceReady();
                return ColorDifferenceMatFactory.Create(XMat, YMat, ZMat, colorDifferenceReferenceUMat!, colorDifferenceReferenceVMat!);
            }

            ConoscopeUvReference reference = ResolvePointColorDifferenceReference();
            return ColorDifferenceMatFactory.Create(XMat, YMat, ZMat, reference);
        }

        private void EnsureColorDifferenceReferenceReady()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage && (colorDifferenceReferenceUMat == null || colorDifferenceReferenceVMat == null))
            {
                throw new InvalidOperationException("请先点击“保存色差基准图”，再计算实测图色差");
            }

            if (mode == ColorDifferenceReferenceMode.ReferenceImage && XMat != null && colorDifferenceReferenceUMat != null
                && (XMat.Width != colorDifferenceReferenceUMat.Width || XMat.Height != colorDifferenceReferenceUMat.Height))
            {
                throw new InvalidOperationException("当前图像尺寸与色差基准图不一致，无法逐点计算");
            }

            if (mode == ColorDifferenceReferenceMode.Custom && !TryParseCustomColorDifferenceReference(out _))
            {
                throw new InvalidOperationException("请输入有效的自定义 u/v 基准坐标");
            }
        }

        private void ColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingColorDifferenceControls)
            {
                return;
            }

            ConoscopeConfig.ColorDifferenceReferenceMode = GetSelectedColorDifferenceReferenceMode();
            UpdateColorDifferenceReferenceUi();

            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                }
                catch (Exception ex)
                {
                    log.Error($"切换色差基准失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ColorDifferenceCustom_LostFocus(object sender, RoutedEventArgs e)
        {
            if (isUpdatingColorDifferenceControls)
            {
                return;
            }

            if (!TryParseCustomColorDifferenceReference(out _))
            {
                MessageBox.Show("请输入有效的自定义 u/v 基准坐标", "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateColorDifferenceReferenceUi();
            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference
                && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom
                && HasXyzData())
            {
                RefreshDisplayedImage();
            }
        }

        private void btnSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
                {
                    MessageBox.Show("请先加载一张实测图", "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                colorDifferenceReferenceUMat?.Dispose();
                colorDifferenceReferenceVMat?.Dispose();
                colorDifferenceReferenceUMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieU);
                colorDifferenceReferenceVMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieV);
                colorDifferenceReferenceFileName = Filename;

                isUpdatingColorDifferenceControls = true;
                try
                {
                    ComboBoxHelper.SelectItemByTag(cbColorDifferenceReference, ColorDifferenceReferenceMode.ReferenceImage.ToString());
                    ConoscopeConfig.ColorDifferenceReferenceMode = ColorDifferenceReferenceMode.ReferenceImage;
                }
                finally
                {
                    isUpdatingColorDifferenceControls = false;
                }

                UpdateColorDifferenceReferenceUi();
            }
            catch (Exception ex)
            {
                log.Error($"保存色差基准图失败: {ex.Message}", ex);
                MessageBox.Show($"保存色差基准图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCalculateColorDifference_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EnsureColorDifferenceReferenceReady();
                ComboBoxHelper.SelectItemByTag(cbDisplayChannel, ExportChannel.ColorDifference.ToString());
                if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
                {
                    RefreshDisplayedImage();
                }
            }
            catch (Exception ex)
            {
                log.Error($"计算色差失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private double GetChannelValue(RgbSample sample, ExportChannel channel)
        {
            return GetChannelValue((int)Math.Round(sample.DX), (int)Math.Round(sample.DY), sample.X, sample.Y, sample.Z, channel);
        }

        private double GetChannelValue(int ix, int iy, double X, double Y, double Z, ExportChannel channel)
        {
            if (channel == ExportChannel.ColorDifference)
            {
                return GetColorDifferenceValue(ix, iy, X, Y, Z);
            }

            return ConoscopeColorimetry.GetChannelValue(X, Y, Z, channel);
        }

        private double GetColorDifferenceValue(int ix, int iy, double X, double Y, double Z)
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            ConoscopeChromaticity chromaticity = ConoscopeColorimetry.Calculate(X, Y, Z);

            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                EnsureColorDifferenceReferenceReady();
                if (colorDifferenceReferenceUMat == null || colorDifferenceReferenceVMat == null)
                {
                    return 0;
                }

                return ColorDifferenceMatFactory.GetValue(ix, iy, X, Y, Z, colorDifferenceReferenceUMat, colorDifferenceReferenceVMat);
            }

            ConoscopeUvReference reference = ResolvePointColorDifferenceReference();
            return ColorDifferenceMatFactory.GetValue(X, Y, Z, reference);
        }
    }
}