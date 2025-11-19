using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using log4net;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate
{

    public class SFREditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SFREditorTool));

        private readonly ImageView _imageView;

        public SFREditorTool(ImageView imageView)
        {
            _imageView = imageView;
        }

        public void Execute()
        {
            if (_imageView.HImageCache is not HImage hImage) return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Task.Run(() =>
                {
                    int maxLen = 1024;
                    double[] freq = new double[maxLen];
                    double[] sfr_r = new double[maxLen];
                    double[] sfr_g = new double[maxLen];
                    double[] sfr_b = new double[maxLen];
                    double[] sfr_l = new double[maxLen];
                    int outLen;
                    int channelCount;
                    
                    double mtf10_r, mtf50_r, mtf10c_r, mtf50c_r;
                    double mtf10_g, mtf50_g, mtf10c_g, mtf50c_g;
                    double mtf10_b, mtf50_b, mtf10c_b, mtf50c_b;
                    double mtf10_l, mtf50_l, mtf10c_l, mtf50c_l;

                    int ret = OpenCVMediaHelper.M_CalSFRMultiChannel(
                        (HImage)_imageView.HImageCache, 1.0,
                        0, 0, hImage.cols, hImage.rows,
                        freq, sfr_r, sfr_g, sfr_b, sfr_l,
                        maxLen,
                        out outLen,
                        out channelCount,
                        out mtf10_r, out mtf50_r, out mtf10c_r, out mtf50c_r,
                        out mtf10_g, out mtf50_g, out mtf10c_g, out mtf50c_g,
                        out mtf10_b, out mtf50_b, out mtf10c_b, out mtf50c_b,
                        out mtf10_l, out mtf50_l, out mtf10c_l, out mtf50c_l);

                    if (ret == 0 && outLen > 0)
                    {
                        // Copy the data to appropriately sized arrays
                        double[] freqData = new double[outLen];
                        Array.Copy(freq, freqData, outLen);

                        // Show the plot window on UI thread
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var plotWindow = new SfrSimplePlotWindow();
                            
                            if (channelCount == 4)
                            {
                                // RGB + L channels
                                double[] sfrDataR = new double[outLen];
                                double[] sfrDataG = new double[outLen];
                                double[] sfrDataB = new double[outLen];
                                double[] sfrDataL = new double[outLen];
                                Array.Copy(sfr_r, sfrDataR, outLen);
                                Array.Copy(sfr_g, sfrDataG, outLen);
                                Array.Copy(sfr_b, sfrDataB, outLen);
                                Array.Copy(sfr_l, sfrDataL, outLen);
                                
                                plotWindow.SetMultiChannelData(freqData,
                                    sfrDataR, sfrDataG, sfrDataB, sfrDataL,
                                    mtf10_r, mtf50_r, mtf10c_r, mtf50c_r,
                                    mtf10_g, mtf50_g, mtf10c_g, mtf50c_g,
                                    mtf10_b, mtf50_b, mtf10c_b, mtf50c_b,
                                    mtf10_l, mtf50_l, mtf10c_l, mtf50c_l);
                            }
                            else
                            {
                                // Single L channel
                                double[] sfrDataL = new double[outLen];
                                Array.Copy(sfr_l, sfrDataL, outLen);
                                plotWindow.SetData(freqData, sfrDataL, mtf10_l, mtf50_l, mtf10c_l, mtf50c_l, "L");
                            }
                            
                            plotWindow.Owner = Application.Current.GetActiveWindow();
                            plotWindow.Show();
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            MessageBox.Show($"SFR 计算失败，返回码: {ret}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            });
        }
    }

    /// <summary>
    /// DVRectangle 右键菜单：执行裁剪操作
    /// 模仿 DVLineDVContextMenu 的结构。
    /// </summary>
    public class DVRectangleDVContextMenuSFR : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            if (context.ImageView.HImageCache is not HImage hImage) return menuItems;

            double DpiX = context.Config.GetProperties<double>("DpiX");
            double DpiY = context.Config.GetProperties<double>("DpiY");

            double DpiSacleX  = DpiX / 96.0;
            double DpiSacleY = DpiY / 96.0; // 每毫米多少像素

            // 图像尺寸
            int imgWidth = hImage.cols;
            int imgHeight = hImage.rows;

            // 用户绘制的矩形
            int x = (int)Math.Round(dvRectangle.Rect.X * DpiSacleX);
            int y = (int)Math.Round(dvRectangle.Rect.Y * DpiSacleY);
            int w = (int)Math.Round(dvRectangle.Rect.Width * DpiSacleX);
            int h = (int)Math.Round(dvRectangle.Rect.Height * DpiSacleY);

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
                int maxLen = 1024;
                double[] freq = new double[maxLen];
                double[] sfr_r = new double[maxLen];
                double[] sfr_g = new double[maxLen];
                double[] sfr_b = new double[maxLen];
                double[] sfr_l = new double[maxLen];
                int outLen;
                int channelCount;
                
                double mtf10_r, mtf50_r, mtf10c_r, mtf50c_r;
                double mtf10_g, mtf50_g, mtf10c_g, mtf50c_g;
                double mtf10_b, mtf50_b, mtf10c_b, mtf50c_b;
                double mtf10_l, mtf50_l, mtf10c_l, mtf50c_l;

                if (context.ImageView.HImageCache == null) return;
                
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_CalSFRMultiChannel(
                        (HImage)context.ImageView.HImageCache, 1.0,
                        roiX, roiY, roiW, roiH,
                        freq, sfr_r, sfr_g, sfr_b, sfr_l,
                        maxLen,
                        out outLen,
                        out channelCount,
                        out mtf10_r, out mtf50_r, out mtf10c_r, out mtf50c_r,
                        out mtf10_g, out mtf50_g, out mtf10c_g, out mtf50c_g,
                        out mtf10_b, out mtf50_b, out mtf10c_b, out mtf50c_b,
                        out mtf10_l, out mtf50_l, out mtf10c_l, out mtf50c_l);

                    if (ret == 0 && outLen > 0)
                    {
                        // Copy the data to appropriately sized arrays
                        double[] freqData = new double[outLen];
                        Array.Copy(freq, freqData, outLen);

                        // Show the plot window on UI thread
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            var plotWindow = new SfrSimplePlotWindow();
                            
                            if (channelCount == 4)
                            {
                                // RGB + L channels
                                double[] sfrDataR = new double[outLen];
                                double[] sfrDataG = new double[outLen];
                                double[] sfrDataB = new double[outLen];
                                double[] sfrDataL = new double[outLen];
                                Array.Copy(sfr_r, sfrDataR, outLen);
                                Array.Copy(sfr_g, sfrDataG, outLen);
                                Array.Copy(sfr_b, sfrDataB, outLen);
                                Array.Copy(sfr_l, sfrDataL, outLen);
                                
                                plotWindow.SetMultiChannelData(freqData,
                                    sfrDataR, sfrDataG, sfrDataB, sfrDataL,
                                    mtf10_r, mtf50_r, mtf10c_r, mtf50c_r,
                                    mtf10_g, mtf50_g, mtf10c_g, mtf50c_g,
                                    mtf10_b, mtf50_b, mtf10c_b, mtf50c_b,
                                    mtf10_l, mtf50_l, mtf10c_l, mtf50c_l);
                            }
                            else
                            {
                                // Single L channel
                                double[] sfrDataL = new double[outLen];
                                Array.Copy(sfr_l, sfrDataL, outLen);
                                plotWindow.SetData(freqData, sfrDataL, mtf10_l, mtf50_l, mtf10c_l, mtf50c_l, "L");
                            }
                            
                            plotWindow.Owner = Application.Current.GetActiveWindow();
                            plotWindow.Show();
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            MessageBox.Show($"SFR 计算失败，返回码: {ret}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            };
            menuItems.Add(cropSave);
            return menuItems;
        }
    }
}
