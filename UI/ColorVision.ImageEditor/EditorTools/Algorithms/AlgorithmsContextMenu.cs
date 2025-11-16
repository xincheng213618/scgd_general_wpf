using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 图像算法上下文菜单 - 提供图像处理算法的右键菜单
    /// </summary>
    public record class AlgorithmsContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();
            
            // 主菜单项
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                GuidId = "Algorithms", 
                Order = 103, 
                Header = ColorVision.ImageEditor.Properties.Resources.ImageAlgorithm, 
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                GuidId = "AlgorithmsCall",
                Order = 104,
                Header = "AlgorithmsCall",
            });

            // 反相 - 直接应用，无需参数
            RelayCommand ArtculationCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new ArtculationEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "Artculation",
                Order = 1,
                Header = "Calculate Sharpness (ROI)",
                Command = ArtculationCommand
            });


            // 反相 - 直接应用，无需参数
            RelayCommand invertCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new InvertEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "InvertImage", 
                Order = 1, 
                Header = ColorVision.ImageEditor.Properties.Resources.Invert, 
                Command = invertCommand 
            });

            // 自动色阶调整 - 直接应用，无需参数
            RelayCommand autoLevelsCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new AutoLevelsAdjustEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "AutoLevelsAdjust", 
                Order = 2, 
                Header = ColorVision.ImageEditor.Properties.Resources.AutoLevelsAdjustment, 
                Command = autoLevelsCommand 
            });

            // 白平衡 - 打开窗口调整 (仅适用于多通道图像)
            RelayCommand whiteBalanceCommand = new(
                o =>
                {
                    if (context.ImageView != null)
                    {
                        var window = new WhiteBalanceWindow(context.ImageView)
                        {
                            Owner = Application.Current.GetActiveWindow()
                        };
                        window.ShowDialog();
                    }
                },
                o =>
                {
                    // 白平衡仅适用于多通道（彩色）图像
                    if (context.ImageView?.Config != null)
                    {
                        int channels = context.ImageView.Config.GetProperties<int>("Channel");
                        return channels > 1;
                    }
                    return false;
                });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "WhiteBalance", 
                Order = 3, 
                Header =ColorVision.ImageEditor.Properties.Resources.WhiteBalanceAdjustment, 
                Command = whiteBalanceCommand 
            });

            // 伽马校正 - 打开窗口调整
            RelayCommand gammaCorrectionCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new GammaCorrectionWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "GammaCorrection", 
                Order = 4, 
                Header = ColorVision.ImageEditor.Properties.Resources.GammaCorrection, 
                Command = gammaCorrectionCommand 
            });

            // 亮度对比度 - 打开窗口调整
            RelayCommand brightnessContrastCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new BrightnessContrastWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "BrightnessContrast", 
                Order = 5, 
                Header = ColorVision.ImageEditor.Properties.Resources.LuminanceContrastAdjustment, 
                Command = brightnessContrastCommand 
            });

            // 阈值处理 - 打开窗口调整
            RelayCommand thresholdCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new ThresholdWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "Threshold", 
                Order = 6, 
                Header = ColorVision.ImageEditor.Properties.Resources.ThresholdProcessing, 
                Command = thresholdCommand 
            });

            // 去除摩尔纹 - 直接应用，无需参数
            RelayCommand removeMoireCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new RemoveMoireEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "RemoveMoire", 
                Order = 7, 
                Header = ColorVision.ImageEditor.Properties.Resources.MoireRemove, 
                Command = removeMoireCommand 
            });

            // 锐化 - 直接应用，无需参数
            RelayCommand sharpenCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new SharpenEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "Sharpen", 
                Order = 8, 
                Header = ColorVision.ImageEditor.Properties.Resources.Sharpening, 
                Command = sharpenCommand 
            });

            // 高斯模糊 - 打开窗口调整
            RelayCommand gaussianBlurCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new GaussianBlurWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "GaussianBlur", 
                Order = 9, 
                Header = ColorVision.ImageEditor.Properties.Resources.GaussianBlur, 
                Command = gaussianBlurCommand 
            });

            // 中值滤波 - 打开窗口调整
            RelayCommand medianBlurCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new MedianBlurWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "MedianBlur", 
                Order = 10, 
                Header = ColorVision.ImageEditor.Properties.Resources.MedianFilter, 
                Command = medianBlurCommand 
            });

            // 边缘检测 - 打开窗口调整
            RelayCommand edgeDetectionCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new EdgeDetectionWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "EdgeDetection", 
                Order = 11, 
                Header = ColorVision.ImageEditor.Properties.Resources.Canny, 
                Command = edgeDetectionCommand 
            });

            // 直方图均衡化 - 直接应用，无需参数
            RelayCommand histogramEqualizationCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var tool = new HistogramEqualizationEditorTool(context.ImageView);
                    tool.Execute();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "HistogramEqualization", 
                Order = 12, 
                Header = ColorVision.ImageEditor.Properties.Resources.HistogramEqualization, 
                Command = histogramEqualizationCommand 
            });

            return MenuItemMetadatas;
        }
    }
}
