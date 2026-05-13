using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Core;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void RefreshDisplayedImage()
        {
            if (!HasXyzData())
            {
                UpdatePseudoColorLegendVisibility(false);
                return;
            }

            ExportChannel displayChannel = GetSelectedDisplayChannel();
            ConoscopePseudoColorRenderResult renderResult = ConoscopePseudoColorRenderer.Render(
                XMat!,
                YMat!,
                ZMat!,
                displayChannel,
                ConoscopeConfig.PseudoColorMap,
                CreateColorDifferenceMat);

            UpdatePseudoColorLegend(renderResult.Channel, renderResult.MinValue, renderResult.MaxValue);

            DisposeCoordinateAxis();
            ImageView.Clear();
            ImageView.SetImageSource(renderResult.Bitmap);
            ImageView.UpdateZoomAndScale();

            CreateAndAnalyzePolarLines();
        }

        private void UpdatePseudoColorLegend(ExportChannel channel, double minValue, double maxValue)
        {
            currentReferenceScaleChannel = channel;
            currentReferenceScaleMaximum = maxValue;

            if (tbPseudoColorLegendTitle == null || tbPseudoColorLegendMin == null || tbPseudoColorLegendMax == null)
            {
                return;
            }

            UpdatePseudoColorMapPreview();
            tbPseudoColorLegendTitle.Text = GetChannelLabel(channel);
            tbPseudoColorLegendMin.Text = FormatChannelValue(minValue, channel);
            tbPseudoColorLegendMax.Text = FormatChannelValue(maxValue, channel);
            UpdatePseudoColorLegendVisibility(true);
        }

        private void UpdatePseudoColorMapPreview()
        {
            if (imgPseudoColorLegend == null)
            {
                return;
            }

            imgPseudoColorLegend.Source = ColormapConstats.CreatePreviewImage(ConoscopeConfig.PseudoColorMap);
        }

        private void UpdatePseudoColorLegendVisibility(bool isVisible)
        {
            if (PseudoColorLegendPanel == null)
            {
                return;
            }

            PseudoColorLegendPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
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
                return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, colorDifferenceReferenceUMat!, colorDifferenceReferenceVMat!);
            }

            ConoscopeUvReference reference = ResolvePointColorDifferenceReference();
            return ConoscopeColorimetry.CreateColorDifferenceMat(XMat, YMat, ZMat, reference.U, reference.V);
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

        private bool HasXyzData()
        {
            return XMat != null && YMat != null && ZMat != null;
        }

        private ImageFilterType GetSelectedFilterType()
        {
            if (cbFilterType?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string filterTag
                && Enum.TryParse(filterTag, out ImageFilterType filterType))
            {
                return NormalizeFilterType(filterType);
            }

            if (cbFilterType?.SelectedIndex >= 0)
            {
                return NormalizeFilterType((ImageFilterType)cbFilterType.SelectedIndex);
            }

            return NormalizeFilterType(ConoscopeConfig.FilterType);
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private bool IsDustRemovalEnabled()
        {
            return chkDustRemovalEnabled?.IsChecked == true;
        }

        private DustRemovalMode GetSelectedDustRemovalMode()
        {
            if (cbDustMode?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string modeTag
                && Enum.TryParse(modeTag, out DustRemovalMode mode))
            {
                return mode;
            }

            return ConoscopeConfig.DustRemovalMode;
        }

        private ExportChannel GetSelectedDisplayChannel()
        {
            if (cbDisplayChannel?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string channelTag &&
                Enum.TryParse(channelTag, out ExportChannel channel))
            {
                return channel;
            }

            return ConoscopeConfig.DisplayChannel;
        }

        private void DisplayChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDisplayControls)
            {
                return;
            }

            ExportChannel channel = GetSelectedDisplayChannel();
            ConoscopeConfig.DisplayChannel = channel;
            UpdateColorDifferencePanelVisibility();

            if (HasXyzData())
            {
                try
                {
                    RefreshDisplayedImage();
                    UpdateReferencePlot();
                }
                catch (Exception ex)
                {
                    log.Error($"刷新显示通道失败: {ex.Message}", ex);
                    MessageBox.Show(ex.Message, "色差计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ExportChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateColorDifferencePanelVisibility();
        }

        private void btnSaveConoscopeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFilterControlsToConfig();
                ConfigService.Instance.Save<ConoscopeConfig>();
                MessageBox.Show("配置已保存", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"保存 Conoscope 配置失败: {ex.Message}", ex);
                MessageBox.Show($"保存配置失败: {ex.Message}", "Conoscope", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (GetSelectedDisplayChannel() == ExportChannel.ColorDifference && GetSelectedColorDifferenceReferenceMode() == ColorDifferenceReferenceMode.Custom && HasXyzData())
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
                    SelectComboBoxItemByTag(cbColorDifferenceReference, ColorDifferenceReferenceMode.ReferenceImage.ToString());
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
                SelectComboBoxItemByTag(cbDisplayChannel, ExportChannel.ColorDifference.ToString());
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
    }
}
