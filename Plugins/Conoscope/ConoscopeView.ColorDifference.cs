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
                ComboBoxHelper.SelectItemByTag(cbColorDifferenceReference, ColorDifferenceConfig.ReferenceMode.ToString());
                txtColorDifferenceCustomU.Text = ColorDifferenceConfig.CustomU.ToString("F4", CultureInfo.InvariantCulture);
                txtColorDifferenceCustomV.Text = ColorDifferenceConfig.CustomV.ToString("F4", CultureInfo.InvariantCulture);
            }
            finally
            {
                isUpdatingColorDifferenceControls = false;
            }

            UpdateColorDifferenceReferenceUi();
        }

        private void ApplyColorDifferenceReferenceMode(ColorDifferenceReferenceMode mode, bool refreshDisplay)
        {
            isUpdatingColorDifferenceControls = true;
            try
            {
                ComboBoxHelper.SelectItemByTag(cbColorDifferenceReference, mode.ToString());
                ColorDifferenceConfig.ReferenceMode = mode;
            }
            finally
            {
                isUpdatingColorDifferenceControls = false;
            }

            UpdateColorDifferenceReferenceUi();
            RaiseWindowQuickControlStateChanged();
            RefreshColorDifferenceDisplayIfNeeded(refreshDisplay);
        }

        private void ApplyColorDifferenceCustomReference(double u, double v, bool refreshDisplay)
        {
            ColorDifferenceConfig.CustomU = u;
            ColorDifferenceConfig.CustomV = v;

            isUpdatingColorDifferenceControls = true;
            try
            {
                if (txtColorDifferenceCustomU != null)
                {
                    txtColorDifferenceCustomU.Text = u.ToString("F4", CultureInfo.InvariantCulture);
                }

                if (txtColorDifferenceCustomV != null)
                {
                    txtColorDifferenceCustomV.Text = v.ToString("F4", CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                isUpdatingColorDifferenceControls = false;
            }

            UpdateColorDifferenceReferenceUi();
            RaiseWindowQuickControlStateChanged();
            RefreshColorDifferenceDisplayIfNeeded(refreshDisplay && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom);
        }

        private void RefreshColorDifferenceDisplayIfNeeded(bool refreshDisplay)
        {
            if (!refreshDisplay || GetSelectedDisplayChannel() != ExportChannel.ColorDifference || !HasXyzData())
            {
                return;
            }

            try
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
            catch (Exception ex)
            {
                log.Error($"刷新色差显示失败: {ex.Message}", ex);
                MessageBox.Show(ex.Message, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void SetWindowQuickColorDifferenceReferenceMode(ColorDifferenceReferenceMode mode)
        {
            ApplyColorDifferenceReferenceMode(mode, refreshDisplay: true);
        }

        public void SetWindowQuickColorDifferenceCustomReference(double u, double v)
        {
            ApplyColorDifferenceCustomReference(u, v, refreshDisplay: true);
        }

        public void SaveCurrentAsGlobalColorDifferenceReference()
        {
            if (!HasXyzData() || XMat == null || YMat == null || ZMat == null)
            {
                MessageBox.Show(Properties.Resources.MsgLoadImageFirstColorDiff, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using OpenCvSharp.Mat referenceUMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieU);
            using OpenCvSharp.Mat referenceVMat = ConoscopeColorimetry.CreateChannelMat(XMat, YMat, ZMat, ExportChannel.CieV);
            GlobalReferences.SaveColorDifferenceReference(referenceUMat, referenceVMat, Filename);

            ApplyColorDifferenceReferenceMode(ColorDifferenceReferenceMode.ReferenceImage, refreshDisplay: false);
            ConoscopeModuleService.RefreshAllReferenceState();
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
            return ComboBoxHelper.GetSelectedEnumByTag(cbColorDifferenceReference, ColorDifferenceConfig.ReferenceMode);
        }

        private static ConoscopeUvReference GetStandardColorDifferenceReference(ColorDifferenceReferenceMode mode)
        {
            return mode switch
            {
                ColorDifferenceReferenceMode.D65 => new ConoscopeUvReference(0.1978, 0.4684),
                ColorDifferenceReferenceMode.D50 => new ConoscopeUvReference(0.2009, 0.4707),
                ColorDifferenceReferenceMode.A => new ConoscopeUvReference(0.2560, 0.5242),
                ColorDifferenceReferenceMode.D75 => new ConoscopeUvReference(0.1952, 0.4670),
                _ => throw new InvalidOperationException(Properties.Resources.MsgNoFixedLightSource)
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

            ColorDifferenceConfig.CustomU = u;
            ColorDifferenceConfig.CustomV = v;
            reference = new ConoscopeUvReference(u, v);
            return true;
        }

        private ConoscopeUvReference? TryResolvePointColorDifferenceReference()
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
                    MessageBox.Show(Properties.Resources.MsgInvalidCustomUV, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                return customReference;
            }

            if (mode == ColorDifferenceReferenceMode.ImageCenter)
            {
                return TryCalculateImageCenterColorDifferenceReference();
            }

            MessageBox.Show(Properties.Resources.MsgMeasuredBaseNeedsSave, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        private ConoscopeUvReference? TryCalculateImageCenterColorDifferenceReference()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return null;
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
                MessageBox.Show(Properties.Resources.MsgNoPixelsInCenter, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
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

            if (GlobalReferences.HasColorDifferenceReference)
            {
                btnSaveColorDifferenceReference.Content = "更新全局基准图";
                btnSaveColorDifferenceReference.Background = Brushes.LightGreen;
                btnSaveColorDifferenceReference.Foreground = Brushes.Black;
            }
            else
            {
                btnSaveColorDifferenceReference.Content = "保存全局基准图";
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
                ColorDifferenceReferenceMode.ImageCenter => Properties.Resources.MsgBaseCenter50px,
                ColorDifferenceReferenceMode.Custom => $"自定义: u={ColorDifferenceConfig.CustomU:F4}, v={ColorDifferenceConfig.CustomV:F4}",
                ColorDifferenceReferenceMode.ReferenceImage => GlobalReferences.ColorDifferenceReferenceUMat == null
                    ? "未保存全局色差基准图"
                    : $"全局基准图: {Path.GetFileName(GlobalReferences.ColorDifferenceReferenceFileName)}",
                _ => string.Empty
            };
        }

        private OpenCvSharp.Mat? CreateColorDifferenceMat()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return null;
            }

            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                if (!EnsureColorDifferenceReferenceReady()) return null;
                return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, GlobalReferences.ColorDifferenceReferenceUMat!, GlobalReferences.ColorDifferenceReferenceVMat!);
            }

            ConoscopeUvReference? reference = TryResolvePointColorDifferenceReference();
            if (reference == null) return null;
            return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, reference.Value.U, reference.Value.V);
        }

        private bool CanRefreshColorDifferenceDisplay()
        {
            if (GetSelectedColorDifferenceReferenceMode() != ColorDifferenceReferenceMode.ReferenceImage)
            {
                return true;
            }

            if (!GlobalReferences.HasColorDifferenceReference)
            {
                UpdateColorDifferenceReferenceUi();
                return false;
            }

            if (XMat != null && GlobalReferences.ColorDifferenceReferenceUMat != null
                && (XMat.Width != GlobalReferences.ColorDifferenceReferenceUMat.Width || XMat.Height != GlobalReferences.ColorDifferenceReferenceUMat.Height))
            {
                UpdateColorDifferenceReferenceUi();
                return false;
            }

            return true;
        }

        private bool EnsureColorDifferenceReferenceReady()
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();
            if (mode == ColorDifferenceReferenceMode.ReferenceImage && !GlobalReferences.HasColorDifferenceReference)
            {
                MessageBox.Show(Properties.Resources.MsgGlobalColorDifferenceReferenceRequired, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (mode == ColorDifferenceReferenceMode.ReferenceImage && XMat != null && GlobalReferences.ColorDifferenceReferenceUMat != null
                && (XMat.Width != GlobalReferences.ColorDifferenceReferenceUMat.Width || XMat.Height != GlobalReferences.ColorDifferenceReferenceUMat.Height))
            {
                MessageBox.Show(Properties.Resources.MsgImageSizeMismatch, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (mode == ColorDifferenceReferenceMode.Custom && !TryParseCustomColorDifferenceReference(out _))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidCustomUvReference, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void ColorDifferenceReference_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingColorDifferenceControls)
            {
                return;
            }

            ColorDifferenceConfig.ReferenceMode = GetSelectedColorDifferenceReferenceMode();
            UpdateColorDifferenceReferenceUi();
            RaiseWindowQuickControlStateChanged();

            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
                catch (Exception ex)
                {
                    log.Error($"切换色差基准失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(Properties.Resources.MsgInvalidCustomUV, Properties.Resources.PanelColorDiff, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateColorDifferenceReferenceUi();
            RaiseWindowQuickControlStateChanged();
            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference
                && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom
                && HasXyzData())
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
            }
        }

        private void btnSaveColorDifferenceReference_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentAsGlobalColorDifferenceReference();
        }

        private void btnCalculateColorDifference_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureColorDifferenceReferenceReady())
            {
                return;
            }

            ComboBoxHelper.SelectItemByTag(cbDisplayChannel, ExportChannel.ColorDifference.ToString());
            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && HasXyzData())
            {
                RefreshDisplayedImage();
                UpdateReferencePlot();
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

            if (channel == ExportChannel.Contrast)
            {
                return GetContrastValue(ix, iy, Y);
            }

            return ConoscopeColorimetry.GetChannelValue(X, Y, Z, channel);
        }

        private double GetColorDifferenceValue(int ix, int iy, double X, double Y, double Z)
        {
            ColorDifferenceReferenceMode mode = GetSelectedColorDifferenceReferenceMode();

            if (mode == ColorDifferenceReferenceMode.ReferenceImage)
            {
                if (GlobalReferences.ColorDifferenceReferenceUMat == null || GlobalReferences.ColorDifferenceReferenceVMat == null)
                {
                    return 0;
                }

                int sx = ConoscopeNumericHelper.ClampToInt(ix, 0, GlobalReferences.ColorDifferenceReferenceUMat.Width - 1);
                int sy = ConoscopeNumericHelper.ClampToInt(iy, 0, GlobalReferences.ColorDifferenceReferenceUMat.Height - 1);
                return ConoscopeColorimetry.CalculateColorDifference(X, Y, Z, GlobalReferences.ColorDifferenceReferenceUMat.At<float>(sy, sx), GlobalReferences.ColorDifferenceReferenceVMat.At<float>(sy, sx));
            }

            ConoscopeUvReference? reference = TryResolvePointColorDifferenceReference();
            if (reference == null) return 0;
            return ConoscopeColorimetry.CalculateColorDifference(X, Y, Z, reference.Value.U, reference.Value.V);
        }
    }
}
