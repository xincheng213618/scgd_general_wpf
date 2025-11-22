using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate
{
    public class ArtculationConfig
    {
               /// <summary>
        /// 焦点算法
        /// </summary>
        public FocusAlgorithm FocusAlgorithm { get; set; } = FocusAlgorithm.Laplacian;
    }

    /// <summary>
    /// DVRectangle 右键菜单：执行裁剪操作
    /// 模仿 DVLineDVContextMenu 的结构。
    /// </summary>
    public class DVRectangleDVContextMenu : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(EditorContext context, object obj)
        {
            List<MenuItem> menuItems = new();
            if (obj is not IRectangle dvRectangle) return menuItems;

            if (context.ImageView.HImageCache is not HImage hImage) return menuItems;

            // 图像尺寸
            int imgWidth = hImage.cols;
            int imgHeight = hImage.rows;

            // 用户绘制的矩形
            int x = (int)Math.Round(dvRectangle.Rect.X);
            int y = (int)Math.Round(dvRectangle.Rect.Y);
            int w = (int)Math.Round(dvRectangle.Rect.Width);
            int h = (int)Math.Round(dvRectangle.Rect.Height);

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

            var cropSave = new MenuItem { Header = "清晰度评估" };
            cropSave.Click += (s, e) =>
            {

                ArtculationConfig sharpnessConfig = new ArtculationConfig();
                PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(sharpnessConfig)
                {
                    Title = "清晰度评估参数设置",
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                propertyEditorWindow.ShowDialog();

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (context.ImageView.HImageCache == null) return;
                    Task.Run(() =>
                    {
                        double articulation = OpenCVMediaHelper.M_CalArtculation((HImage)context.ImageView.HImageCache, sharpnessConfig.FocusAlgorithm, new RoiRect(roiX,roiY,roiW,roiH));
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"图像清晰度评估结果: {articulation}",
                                "清晰度评估",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });
                    });
                });
            };
            menuItems.Add(cropSave);
            return menuItems;
        }
    }


    /// <summary>
    /// 边缘清晰度评估
    /// </summary>
    public class ArtculationEditorTool
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ArtculationEditorTool));

        private readonly ImageView _imageView;

        public ArtculationEditorTool(ImageView imageView)
        {
            _imageView = imageView;
        }

        public void Execute()
        {
            if (_imageView.HImageCache == null) return;

            ArtculationConfig sharpnessConfig  = new ArtculationConfig();
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(sharpnessConfig)
            {
                Title = "清晰度评估参数设置",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            propertyEditorWindow.ShowDialog();

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Task.Run(() =>
                {
                    double articulation = OpenCVMediaHelper.M_CalArtculation((HImage)_imageView.HImageCache, sharpnessConfig.FocusAlgorithm, new RoiRect(0,0, (int)Math.Round(_imageView.Width), (int)Math.Round(_imageView.Height)));
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"图像清晰度评估结果: {articulation}",
                            "清晰度评估",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                });
            });
        }
    }
}
