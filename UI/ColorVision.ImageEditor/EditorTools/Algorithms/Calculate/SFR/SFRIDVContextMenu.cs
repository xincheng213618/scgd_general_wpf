using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR
{

    public class SFREditorTool
    {
        private readonly ImageProcessingContext _image;

        public SFREditorTool(ImageProcessingContext image)
        {
            _image = image;
        }

        public void Execute()
        {
            if (_image.HImageCache is not HImage hImage) return;

            SfrAnalysisRunner.Run(hImage, new RoiRect(0, 0, hImage.cols, hImage.rows));
        }
    }

    internal static class SfrAnalysisRunner
    {
        private const int MaxLength = 1024;

        private readonly struct SfrMetrics
        {
            public SfrMetrics(double mtf10Norm, double mtf50Norm, double mtf10CyPix, double mtf50CyPix)
            {
                Mtf10Norm = mtf10Norm;
                Mtf50Norm = mtf50Norm;
                Mtf10CyPix = mtf10CyPix;
                Mtf50CyPix = mtf50CyPix;
            }

            public double Mtf10Norm { get; }
            public double Mtf50Norm { get; }
            public double Mtf10CyPix { get; }
            public double Mtf50CyPix { get; }
        }

        private sealed class SfrCalculationResult
        {
            public int ReturnCode { get; init; }
            public int ChannelCount { get; init; }
            public double[] Frequency { get; init; } = Array.Empty<double>();
            public double[] SfrR { get; init; } = Array.Empty<double>();
            public double[] SfrG { get; init; } = Array.Empty<double>();
            public double[] SfrB { get; init; } = Array.Empty<double>();
            public double[] SfrL { get; init; } = Array.Empty<double>();
            public SfrMetrics MetricsR { get; init; }
            public SfrMetrics MetricsG { get; init; }
            public SfrMetrics MetricsB { get; init; }
            public SfrMetrics MetricsL { get; init; }

            public bool IsSuccess => ReturnCode == 0 && Frequency.Length > 0 && SfrL.Length > 0;

            public static SfrCalculationResult Failure(int returnCode)
            {
                return new SfrCalculationResult { ReturnCode = returnCode };
            }
        }

        public static void Run(HImage image, RoiRect roi)
        {
            Task.Run(() =>
            {
                try
                {
                    SfrCalculationResult result = Calculate(image, roi);
                    Application.Current.Dispatcher.BeginInvoke(() => ShowResult(result));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                        MessageBox.Show($"SFR 计算异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private static SfrCalculationResult Calculate(HImage image, RoiRect roi)
        {
            double[] freq = new double[MaxLength];
            double[] sfrR = new double[MaxLength];
            double[] sfrG = new double[MaxLength];
            double[] sfrB = new double[MaxLength];
            double[] sfrL = new double[MaxLength];

            int ret = OpenCVMediaHelper.M_CalSFRMultiChannel(
                image, 1.0, roi,
                freq, sfrR, sfrG, sfrB, sfrL,
                MaxLength,
                out int outLen,
                out int channelCount,
                out double mtf10R, out double mtf50R, out double mtf10cR, out double mtf50cR,
                out double mtf10G, out double mtf50G, out double mtf10cG, out double mtf50cG,
                out double mtf10B, out double mtf50B, out double mtf10cB, out double mtf50cB,
                out double mtf10L, out double mtf50L, out double mtf10cL, out double mtf50cL);

            if (ret != 0 || outLen <= 0)
            {
                return SfrCalculationResult.Failure(ret);
            }

            outLen = Math.Min(outLen, MaxLength);
            bool hasRgb = channelCount == 4;

            return new SfrCalculationResult
            {
                ReturnCode = ret,
                ChannelCount = channelCount,
                Frequency = CopyPrefix(freq, outLen),
                SfrR = hasRgb ? CopyPrefix(sfrR, outLen) : Array.Empty<double>(),
                SfrG = hasRgb ? CopyPrefix(sfrG, outLen) : Array.Empty<double>(),
                SfrB = hasRgb ? CopyPrefix(sfrB, outLen) : Array.Empty<double>(),
                SfrL = CopyPrefix(sfrL, outLen),
                MetricsR = new SfrMetrics(mtf10R, mtf50R, mtf10cR, mtf50cR),
                MetricsG = new SfrMetrics(mtf10G, mtf50G, mtf10cG, mtf50cG),
                MetricsB = new SfrMetrics(mtf10B, mtf50B, mtf10cB, mtf50cB),
                MetricsL = new SfrMetrics(mtf10L, mtf50L, mtf10cL, mtf50cL)
            };
        }

        private static double[] CopyPrefix(double[] values, int length)
        {
            double[] copy = new double[length];
            Array.Copy(values, copy, length);
            return copy;
        }

        private static void ShowResult(SfrCalculationResult result)
        {
            if (!result.IsSuccess)
            {
                MessageBox.Show($"SFR 计算失败，返回码: {result.ReturnCode}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var plotWindow = new SfrSimplePlotWindow();
            if (result.ChannelCount == 4)
            {
                plotWindow.SetMultiChannelData(
                    result.Frequency,
                    result.SfrR,
                    result.SfrG,
                    result.SfrB,
                    result.SfrL,
                    result.MetricsR.Mtf10Norm, result.MetricsR.Mtf50Norm, result.MetricsR.Mtf10CyPix, result.MetricsR.Mtf50CyPix,
                    result.MetricsG.Mtf10Norm, result.MetricsG.Mtf50Norm, result.MetricsG.Mtf10CyPix, result.MetricsG.Mtf50CyPix,
                    result.MetricsB.Mtf10Norm, result.MetricsB.Mtf50Norm, result.MetricsB.Mtf10CyPix, result.MetricsB.Mtf50CyPix,
                    result.MetricsL.Mtf10Norm, result.MetricsL.Mtf50Norm, result.MetricsL.Mtf10CyPix, result.MetricsL.Mtf50CyPix);
            }
            else
            {
                plotWindow.SetData(
                    result.Frequency,
                    result.SfrL,
                    result.MetricsL.Mtf10Norm,
                    result.MetricsL.Mtf50Norm,
                    result.MetricsL.Mtf10CyPix,
                    result.MetricsL.Mtf50CyPix,
                    "L");
            }

            plotWindow.Owner = Application.Current.GetActiveWindow();
            plotWindow.Show();
        }
    }

    /// <summary>
    /// DVRectangle 右键菜单：执行裁剪操作
    /// 模仿 DVLineDVContextMenu 的结构。
    /// </summary>
    public class SFRIDVContextMenu : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly ImageViewConfig _config;

        public SFRIDVContextMenu(ImageProcessingContext imageContext, ImageViewConfig config)
        {
            _imageContext = imageContext;
            _config = config;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            if (_imageContext.HImageCache is not HImage hImage) return menuItems;

            double DpiX = _config.GetProperties<double>("DpiX");
            double DpiY = _config.GetProperties<double>("DpiY");

            double DpiScaleX  = DpiX / 96.0;
            double DpiScaleY = DpiY / 96.0; // 每毫米多少像素

            // 图像尺寸
            int imgWidth = hImage.cols;
            int imgHeight = hImage.rows;

            // 用户绘制的矩形
            int x = (int)Math.Round(dvRectangle.Rect.X * DpiScaleX);
            int y = (int)Math.Round(dvRectangle.Rect.Y * DpiScaleY);
            int w = (int)Math.Round(dvRectangle.Rect.Width * DpiScaleX);
            int h = (int)Math.Round(dvRectangle.Rect.Height * DpiScaleY);

            // 先保证宽高为正
            if (w <= 0 || h <= 0)
            {
                return menuItems;
            }

            // 与图像交集：裁剪到 [0, imgWidth/Height)
            int x2 = x + w;
            int y2 = y + h;

            int roiX = Math.Max(0, x);
            int roiY = Math.Max(0, y);
            int roiX2 = Math.Min(imgWidth, x2);
            int roiY2 = Math.Min(imgHeight, y2);

            int roiW = roiX2 - roiX;
            int roiH = roiY2 - roiY;

            // 如果没有交集或太小，则直接提示
            if (roiW <= 0 || roiH <= 0)
            {
                return menuItems;
            }

            var cropSave = new MenuItem { Header = "SFR/MTF 分析" };
            cropSave.Click += (s, e) =>
            {
                if (_imageContext.HImageCache is not HImage image) return;

                SfrAnalysisRunner.Run(image, new RoiRect(roiX, roiY, roiW, roiH));
            };
            menuItems.Add(cropSave);
            return menuItems;
        }
    }
}

