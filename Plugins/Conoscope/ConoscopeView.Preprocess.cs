using Conoscope.Core;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializeFilterControls()
        {
            isUpdatingFilterControls = true;
            try
            {
                MigrateLegacyDustRemovalFilterType();
                SelectComboBoxItemByTag(cbFilterType, NormalizeFilterType(ConoscopeConfig.FilterType).ToString());
                SelectComboBoxItemByTag(cbDustMode, ConoscopeConfig.DustRemovalMode.ToString());
                chkClampNonPositiveXyzOnLoad.IsChecked = ConoscopeConfig.ClampNonPositiveXyzOnLoad;
                chkDustRemovalEnabled.IsChecked = ConoscopeConfig.DustRemovalEnabled;

                sliderKernelSize.Value = ConoscopeConfig.FilterKernelSize;
                sliderSigma.Value = ConoscopeConfig.FilterSigma;
                sliderD.Value = ConoscopeConfig.FilterD;
                sliderSigmaColor.Value = ConoscopeConfig.FilterSigmaColor;
                sliderSigmaSpace.Value = ConoscopeConfig.FilterSigmaSpace;
                sliderDustThreshold.Value = ConoscopeConfig.DustThresholdPercent;
                sliderDustMinArea.Value = ConoscopeConfig.DustMinArea;
                sliderDustMaxArea.Value = Math.Max(ConoscopeConfig.DustMinArea, ConoscopeConfig.DustMaxArea);
                sliderDustRepairRadius.Value = ConoscopeConfig.DustRepairRadius;
            }
            finally
            {
                isUpdatingFilterControls = false;
            }

            UpdateFilterParameterVisibility(GetSelectedFilterType());
            UpdatePreprocessSummary();
        }

        private void FilterParameter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFilterControls || !IsInitialized)
            {
                return;
            }

            SaveFilterControlsToConfig();
            UpdateFilterParameterVisibility(GetSelectedFilterType());
            UpdatePreprocessSummary();
        }

        private void SaveFilterControlsToConfig()
        {
            ConoscopeConfig.ClampNonPositiveXyzOnLoad = chkClampNonPositiveXyzOnLoad?.IsChecked == true;
            ConoscopeConfig.FilterType = NormalizeFilterType(GetSelectedFilterType());
            ConoscopeConfig.FilterKernelSize = NormalizeKernelSize((int)(sliderKernelSize?.Value ?? ConoscopeConfig.FilterKernelSize));
            ConoscopeConfig.FilterSigma = sliderSigma?.Value ?? ConoscopeConfig.FilterSigma;
            ConoscopeConfig.FilterD = Math.Max(1, (int)(sliderD?.Value ?? ConoscopeConfig.FilterD));
            ConoscopeConfig.FilterSigmaColor = sliderSigmaColor?.Value ?? ConoscopeConfig.FilterSigmaColor;
            ConoscopeConfig.FilterSigmaSpace = sliderSigmaSpace?.Value ?? ConoscopeConfig.FilterSigmaSpace;
            ConoscopeConfig.DustRemovalEnabled = IsDustRemovalEnabled();
            ConoscopeConfig.DustRemovalMode = GetSelectedDustRemovalMode();
            ConoscopeConfig.DustThresholdPercent = sliderDustThreshold?.Value ?? ConoscopeConfig.DustThresholdPercent;
            ConoscopeConfig.DustMinArea = Math.Max(1, (int)(sliderDustMinArea?.Value ?? ConoscopeConfig.DustMinArea));
            ConoscopeConfig.DustMaxArea = Math.Max(ConoscopeConfig.DustMinArea, (int)(sliderDustMaxArea?.Value ?? ConoscopeConfig.DustMaxArea));
            ConoscopeConfig.DustRepairRadius = Math.Max(1, (int)(sliderDustRepairRadius?.Value ?? ConoscopeConfig.DustRepairRadius));
        }

        private void UpdatePreprocessSummary()
        {
            if (tbPreprocessSummary == null)
            {
                return;
            }

            string openPolicy = ConoscopeConfig.ApplyFilterOnOpen ? "打开时应用" : "手动应用";
            string clampPolicy = ConoscopeConfig.ClampNonPositiveXyzOnLoad ? "XYZ<=0 修正" : "不修正 XYZ";
            string dustPolicy = ConoscopeConfig.DustRemovalEnabled ? $"灰尘滤除 {ConoscopeConfig.DustRemovalMode}" : "无灰尘滤除";
            string filterPolicy = NormalizeFilterType(ConoscopeConfig.FilterType) == ImageFilterType.None
                ? "无滤波"
                : ConoscopeConfig.FilterType.ToString();
            string pseudoColorPolicy = $"{GetChannelLabel(ConoscopeConfig.DisplayChannel)} / {FormatColormapName(ConoscopeConfig.PseudoColorMap)}";

            tbPreprocessSummary.Text = $"{openPolicy} / {clampPolicy} / {dustPolicy} / {filterPolicy} / 伪彩 {pseudoColorPolicy}";
        }

        private static string FormatColormapName(ColorVision.Core.ColormapTypes colormapType)
        {
            const string prefix = "COLORMAP_";
            string name = colormapType.ToString();
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name[prefix.Length..]
                : name;
        }

        private void MigrateLegacyDustRemovalFilterType()
        {
            const int legacyDustRemovalFilterValue = 6;
            if ((int)ConoscopeConfig.FilterType == legacyDustRemovalFilterValue)
            {
                ConoscopeConfig.DustRemovalEnabled = true;
                ConoscopeConfig.FilterType = ImageFilterType.None;
            }
        }

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null)
            {
                return;
            }

            ImageFilterType selectedFilter = GetSelectedFilterType();

            if (!isUpdatingFilterControls)
            {
                SaveFilterControlsToConfig();
            }

            UpdateFilterParameterVisibility(selectedFilter);
            UpdatePreprocessSummary();

            if (sliderKernelSize != null && sliderSigma != null && sliderD != null && sliderSigmaColor != null && sliderSigmaSpace != null)
            {
                sliderKernelSize.IsEnabled = false;
                sliderSigma.IsEnabled = false;
                sliderD.IsEnabled = false;
                sliderSigmaColor.IsEnabled = false;
                sliderSigmaSpace.IsEnabled = false;

                switch (selectedFilter)
                {
                    case ImageFilterType.None:
                        break;
                    case ImageFilterType.LowPass:
                    case ImageFilterType.MovingAverage:
                    case ImageFilterType.Median:
                        sliderKernelSize.IsEnabled = true;
                        break;
                    case ImageFilterType.Gaussian:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = true;
                        break;
                    case ImageFilterType.Bilateral:
                        sliderD.IsEnabled = true;
                        sliderSigmaColor.IsEnabled = true;
                        sliderSigmaSpace.IsEnabled = true;
                        break;
                }
            }
        }

        private void UpdateFilterParameterVisibility(ImageFilterType selectedFilter)
        {
            if (rowFilterKernel == null || rowFilterSigma == null || rowFilterD == null || rowFilterSigmaColor == null || rowFilterSigmaSpace == null
                || rowDustMode == null || rowDustThreshold == null || rowDustMinArea == null || rowDustMaxArea == null || rowDustRepairRadius == null)
            {
                return;
            }

            bool showKernel = selectedFilter is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median;
            bool showSigma = selectedFilter == ImageFilterType.Gaussian;
            bool showBilateral = selectedFilter == ImageFilterType.Bilateral;
            bool showDust = IsDustRemovalEnabled();

            rowFilterKernel.Visibility = showKernel ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigma.Visibility = showSigma ? Visibility.Visible : Visibility.Collapsed;
            rowFilterD.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaColor.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaSpace.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowDustMode.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustThreshold.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMinArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMaxArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustRepairRadius.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;

            if (sliderDustThreshold != null && sliderDustMinArea != null && sliderDustMaxArea != null && sliderDustRepairRadius != null && cbDustMode != null)
            {
                sliderDustThreshold.IsEnabled = showDust;
                sliderDustMinArea.IsEnabled = showDust;
                sliderDustMaxArea.IsEnabled = showDust;
                sliderDustRepairRadius.IsEnabled = showDust;
                cbDustMode.IsEnabled = showDust;
            }
        }

        private OpenCvSharp.Mat ApplyFilterToMat(OpenCvSharp.Mat src, ImageFilterType filterType, int kernelSize, double sigma, int d, double sigmaColor, double sigmaSpace)
        {
            OpenCvSharp.Mat dst = new OpenCvSharp.Mat();
            OpenCvSharp.Mat workMat = src;

            switch (filterType)
            {
                case ImageFilterType.LowPass:
                    OpenCvSharp.Cv2.Blur(workMat, dst, new OpenCvSharp.Size(kernelSize, kernelSize));
                    break;
                case ImageFilterType.MovingAverage:
                    OpenCvSharp.Cv2.BoxFilter(workMat, dst, workMat.Type(), new OpenCvSharp.Size(kernelSize, kernelSize));
                    break;
                case ImageFilterType.Gaussian:
                    OpenCvSharp.Cv2.GaussianBlur(workMat, dst, new OpenCvSharp.Size(kernelSize, kernelSize), sigma);
                    break;
                case ImageFilterType.Median:
                    if (src.Depth() == OpenCvSharp.MatType.CV_32F)
                    {
                        OpenCvSharp.Cv2.MedianBlur(workMat, dst, kernelSize);
                    }
                    else
                    {
                        OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
                        workMat.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
                        OpenCvSharp.Cv2.MedianBlur(floatMat, dst, kernelSize);
                        OpenCvSharp.Mat result = new OpenCvSharp.Mat();
                        dst.ConvertTo(result, src.Type());
                        floatMat.Dispose();
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                case ImageFilterType.Bilateral:
                    if (src.Depth() == OpenCvSharp.MatType.CV_32F)
                    {
                        OpenCvSharp.Cv2.BilateralFilter(workMat, dst, d, sigmaColor, sigmaSpace);
                    }
                    else
                    {
                        OpenCvSharp.Mat floatMat = new OpenCvSharp.Mat();
                        workMat.ConvertTo(floatMat, OpenCvSharp.MatType.CV_32FC1);
                        OpenCvSharp.Cv2.BilateralFilter(floatMat, dst, d, sigmaColor, sigmaSpace);
                        OpenCvSharp.Mat result = new OpenCvSharp.Mat();
                        dst.ConvertTo(result, src.Type());
                        floatMat.Dispose();
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                default:
                    return src.Clone();
            }

            return dst;
        }

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreprocessFromCurrentSettings();
        }

        internal void ApplyPreprocessFromCurrentSettings()
        {
            try
            {
                if (!HasXyzData())
                {
                    MessageBox.Show("请先获取图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!HasPreprocessEnabled())
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show("已恢复原始数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RestoreOriginalMats();
                log.Info($"开始应用预处理: clamp={ConoscopeConfig.ClampNonPositiveXyzOnLoad}, dust={ConoscopeConfig.DustRemovalEnabled}, filter={ConoscopeConfig.FilterType}");
                ApplyPreprocessToCurrentMats();
                RefreshDisplayedImage();

                log.Info("预处理应用成功，数据已更新");
                MessageBox.Show("预处理应用成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                MessageBox.Show($"应用滤波失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClampNonPositiveXyzValuesIfEnabled()
        {
            if (!ConoscopeConfig.ClampNonPositiveXyzOnLoad || XMat == null || YMat == null || ZMat == null)
            {
                return;
            }

            int clampedX = ClampMatToPositiveFloor(XMat, MinPositiveXyzValue);
            int clampedY = ClampMatToPositiveFloor(YMat, MinPositiveXyzValue);
            int clampedZ = ClampMatToPositiveFloor(ZMat, MinPositiveXyzValue);
            if (clampedX + clampedY + clampedZ > 0)
            {
                log.Warn($"加载时已将 XYZ<=0 修正为 {MinPositiveXyzValue}: X={clampedX}, Y={clampedY}, Z={clampedZ}");
            }
        }

        private static int ClampMatToPositiveFloor(OpenCvSharp.Mat mat, float lowerBound)
        {
            using OpenCvSharp.Mat mask = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Compare(mat, OpenCvSharp.Scalar.All(0), mask, OpenCvSharp.CmpTypes.LE);
            int count = OpenCvSharp.Cv2.CountNonZero(mask);
            if (count > 0)
            {
                mat.SetTo(OpenCvSharp.Scalar.All(lowerBound), mask);
            }

            return count;
        }

        private void ApplyPreprocessToCurrentMats()
        {
            if (ConoscopeConfig.DustRemovalEnabled)
            {
                ApplyDustRemovalToCurrentMats();
            }

            ImageFilterType filterType = NormalizeFilterType(ConoscopeConfig.FilterType);
            if (filterType != ImageFilterType.None)
            {
                ApplyFilterToCurrentMats(filterType);
            }
        }

        private bool HasPreprocessEnabled()
        {
            return ConoscopeConfig.ClampNonPositiveXyzOnLoad
                || ConoscopeConfig.DustRemovalEnabled
                || NormalizeFilterType(ConoscopeConfig.FilterType) != ImageFilterType.None;
        }

        private void ApplyFilterToCurrentMats(ImageFilterType filterType)
        {
            if (filterType == ImageFilterType.None)
            {
                return;
            }

            int kernelSize = ConoscopeConfig.FilterKernelSize;
            double sigma = ConoscopeConfig.FilterSigma;
            int d = ConoscopeConfig.FilterD;
            double sigmaColor = ConoscopeConfig.FilterSigmaColor;
            double sigmaSpace = ConoscopeConfig.FilterSigmaSpace;

            if (XMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(XMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                XMat.Dispose();
                XMat = filtered;
            }
            if (YMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(YMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                YMat.Dispose();
                YMat = filtered;
            }
            if (ZMat != null)
            {
                OpenCvSharp.Mat filtered = ApplyFilterToMat(ZMat, filterType, kernelSize, sigma, d, sigmaColor, sigmaSpace);
                ZMat.Dispose();
                ZMat = filtered;
            }

            log.Info($"滤波应用到XYZ通道完成: {filterType}, kernelSize={kernelSize}");
        }

        private void ApplyDustRemovalToCurrentMats()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return;
            }

            DustRemovalOptions options = GetDustRemovalOptions();
            int darkComponents;
            int brightComponents;
            using OpenCvSharp.Mat darkMask = ShouldDetectDarkDust(options.Mode)
                ? CreateDustMask(YMat, options, darkSpot: true, out darkComponents)
                : CreateEmptyMask(YMat, out darkComponents);
            using OpenCvSharp.Mat brightMask = ShouldDetectBrightDust(options.Mode)
                ? CreateDustMask(YMat, options, darkSpot: false, out brightComponents)
                : CreateEmptyMask(YMat, out brightComponents);

            int darkPixels = OpenCvSharp.Cv2.CountNonZero(darkMask);
            int brightPixels = OpenCvSharp.Cv2.CountNonZero(brightMask);
            if (darkPixels == 0 && brightPixels == 0)
            {
                log.Info($"灰尘滤除未检测到候选区域: mode={options.Mode}, threshold={options.ThresholdPercent:F1}%");
                return;
            }

            XMat = ReplaceChannelWithDustRepair(XMat, darkMask, brightMask, options);
            YMat = ReplaceChannelWithDustRepair(YMat, darkMask, brightMask, options);
            ZMat = ReplaceChannelWithDustRepair(ZMat, darkMask, brightMask, options);

            log.Info($"灰尘滤除完成: mode={options.Mode}, darkComponents={darkComponents}, brightComponents={brightComponents}, darkPixels={darkPixels}, brightPixels={brightPixels}, threshold={options.ThresholdPercent:F1}%, area={options.MinArea}-{options.MaxArea}, radius={options.RepairRadius}");
        }

        private DustRemovalOptions GetDustRemovalOptions()
        {
            int minArea = Math.Max(1, ConoscopeConfig.DustMinArea);
            int maxArea = Math.Max(minArea, ConoscopeConfig.DustMaxArea);
            return new DustRemovalOptions(
                ConoscopeConfig.DustRemovalMode,
                ConoscopeConfig.DustThresholdPercent,
                minArea,
                maxArea,
                Math.Max(1, ConoscopeConfig.DustRepairRadius));
        }

        private static bool ShouldDetectDarkDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.DarkSpot or DustRemovalMode.Both;
        }

        private static bool ShouldDetectBrightDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.BrightSpot or DustRemovalMode.Both;
        }

        private static OpenCvSharp.Mat CreateEmptyMask(OpenCvSharp.Mat source, out int componentCount)
        {
            componentCount = 0;
            return new OpenCvSharp.Mat(source.Rows, source.Cols, OpenCvSharp.MatType.CV_8UC1, new OpenCvSharp.Scalar(0));
        }

        private static OpenCvSharp.Mat CreateDustMask(OpenCvSharp.Mat luminance, DustRemovalOptions options, bool darkSpot, out int componentCount)
        {
            using OpenCvSharp.Mat gray8 = NormalizeToGray8(luminance);
            int backgroundKernelSize = NormalizeKernelSize(options.RepairRadius * 2 + 1);
            using OpenCvSharp.Mat kernel = OpenCvSharp.Cv2.GetStructuringElement(
                OpenCvSharp.MorphShapes.Ellipse,
                new OpenCvSharp.Size(backgroundKernelSize, backgroundKernelSize));
            using OpenCvSharp.Mat background = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat diff = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat rawMask = new OpenCvSharp.Mat();

            OpenCvSharp.Cv2.MorphologyEx(gray8, background, darkSpot ? OpenCvSharp.MorphTypes.Close : OpenCvSharp.MorphTypes.Open, kernel);
            if (darkSpot)
            {
                OpenCvSharp.Cv2.Subtract(background, gray8, diff);
            }
            else
            {
                OpenCvSharp.Cv2.Subtract(gray8, background, diff);
            }

            double threshold = Math.Max(1, Math.Min(255, 255.0 * options.ThresholdPercent / 100.0));
            OpenCvSharp.Cv2.Threshold(diff, rawMask, threshold, 255, OpenCvSharp.ThresholdTypes.Binary);

            OpenCvSharp.Mat filteredMask = FilterMaskByArea(rawMask, options.MinArea, options.MaxArea, out componentCount);
            if (componentCount > 0)
            {
                int dilateKernelSize = NormalizeKernelSize(Math.Max(1, options.RepairRadius));
                using OpenCvSharp.Mat dilateKernel = OpenCvSharp.Cv2.GetStructuringElement(
                    OpenCvSharp.MorphShapes.Ellipse,
                    new OpenCvSharp.Size(dilateKernelSize, dilateKernelSize));
                OpenCvSharp.Cv2.Dilate(filteredMask, filteredMask, dilateKernel);
            }

            return filteredMask;
        }

        private static OpenCvSharp.Mat NormalizeToGray8(OpenCvSharp.Mat source)
        {
            OpenCvSharp.Mat normalized = new OpenCvSharp.Mat();
            OpenCvSharp.Mat gray8 = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.Normalize(source, normalized, 0, 255, OpenCvSharp.NormTypes.MinMax);
            normalized.ConvertTo(gray8, OpenCvSharp.MatType.CV_8UC1);
            normalized.Dispose();
            return gray8;
        }

        private static OpenCvSharp.Mat FilterMaskByArea(OpenCvSharp.Mat rawMask, int minArea, int maxArea, out int componentCount)
        {
            OpenCvSharp.Mat filtered = new OpenCvSharp.Mat(rawMask.Rows, rawMask.Cols, OpenCvSharp.MatType.CV_8UC1, new OpenCvSharp.Scalar(0));
            using OpenCvSharp.Mat labels = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat stats = new OpenCvSharp.Mat();
            using OpenCvSharp.Mat centroids = new OpenCvSharp.Mat();

            int labelsCount = OpenCvSharp.Cv2.ConnectedComponentsWithStats(rawMask, labels, stats, centroids);
            componentCount = 0;
            for (int labelIndex = 1; labelIndex < labelsCount; labelIndex++)
            {
                int area = stats.At<int>(labelIndex, 4);
                if (area < minArea || area > maxArea)
                {
                    continue;
                }

                using OpenCvSharp.Mat componentMask = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.InRange(labels, new OpenCvSharp.Scalar(labelIndex), new OpenCvSharp.Scalar(labelIndex), componentMask);
                filtered.SetTo(new OpenCvSharp.Scalar(255), componentMask);
                componentCount++;
            }

            return filtered;
        }

        private static OpenCvSharp.Mat? ReplaceChannelWithDustRepair(OpenCvSharp.Mat? channel, OpenCvSharp.Mat darkMask, OpenCvSharp.Mat brightMask, DustRemovalOptions options)
        {
            if (channel == null)
            {
                return null;
            }

            OpenCvSharp.Mat repaired = ApplyDustRepairToChannel(channel, darkMask, brightMask, options);
            channel.Dispose();
            return repaired;
        }

        private static OpenCvSharp.Mat ApplyDustRepairToChannel(OpenCvSharp.Mat source, OpenCvSharp.Mat darkMask, OpenCvSharp.Mat brightMask, DustRemovalOptions options)
        {
            OpenCvSharp.Mat result = source.Clone();
            int backgroundKernelSize = NormalizeKernelSize(options.RepairRadius * 2 + 1);
            using OpenCvSharp.Mat kernel = OpenCvSharp.Cv2.GetStructuringElement(
                OpenCvSharp.MorphShapes.Ellipse,
                new OpenCvSharp.Size(backgroundKernelSize, backgroundKernelSize));

            if (OpenCvSharp.Cv2.CountNonZero(darkMask) > 0)
            {
                using OpenCvSharp.Mat darkBackground = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.MorphologyEx(source, darkBackground, OpenCvSharp.MorphTypes.Close, kernel);
                darkBackground.CopyTo(result, darkMask);
            }

            if (OpenCvSharp.Cv2.CountNonZero(brightMask) > 0)
            {
                using OpenCvSharp.Mat brightBackground = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.MorphologyEx(source, brightBackground, OpenCvSharp.MorphTypes.Open, kernel);
                brightBackground.CopyTo(result, brightMask);
            }

            return result;
        }

        private readonly struct DustRemovalOptions
        {
            public DustRemovalOptions(DustRemovalMode mode, double thresholdPercent, int minArea, int maxArea, int repairRadius)
            {
                Mode = mode;
                ThresholdPercent = thresholdPercent;
                MinArea = minArea;
                MaxArea = maxArea;
                RepairRadius = repairRadius;
            }

            public DustRemovalMode Mode { get; }
            public double ThresholdPercent { get; }
            public int MinArea { get; }
            public int MaxArea { get; }
            public int RepairRadius { get; }
        }

        private static int NormalizeKernelSize(int kernelSize)
        {
            kernelSize = Math.Max(1, kernelSize);
            return kernelSize % 2 == 0 ? kernelSize + 1 : kernelSize;
        }
    }
}
