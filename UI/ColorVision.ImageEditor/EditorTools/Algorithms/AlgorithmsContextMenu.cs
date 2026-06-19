using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    /// <summary>
    /// 图像算法上下文菜单 - 提供图像处理算法的右键菜单
    /// </summary>
    public record class AlgorithmsContextMenu(ImageProcessingContext imageContext) : IIEditorToolContextMenu
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
                Header = Properties.Resources.Algorithm_AlgorithmCalls,
            });

            RelayCommand SFRCommand = new(o =>
            {
                var tool = new SFREditorTool(imageContext);
                tool.Execute();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "SFR",
                Order = 1,
                Header = Properties.Resources.Algorithm_SfrMtfAnalysis,
                Command = SFRCommand
            });

            RelayCommand ArtculationCommand = new(o =>
            {
                var tool = new ArtculationEditorTool(imageContext);
                tool.Execute();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "Artculation",
                Order = 1,
                Header = Properties.Resources.Artculation_MenuHeader,
                Command = ArtculationCommand
            });


            // 反相 - 直接应用，无需参数
            RelayCommand invertCommand = new(o =>
            {
                var tool = new InvertEditorTool(imageContext);
                tool.Execute();
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
                var tool = new AutoLevelsAdjustEditorTool(imageContext);
                tool.Execute();
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
                    var window = new WhiteBalanceWindow(imageContext)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                },
                o =>
                {
                    // 白平衡仅适用于多通道（彩色）图像
                    int channels = imageContext.Config.GetProperties<int>("Channel");
                    return channels > 1;
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
                var window = new GammaCorrectionWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var window = new BrightnessContrastWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var window = new ThresholdWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var tool = new RemoveMoireEditorTool(imageContext);
                tool.Execute();
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
                var tool = new SharpenEditorTool(imageContext);
                tool.Execute();
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
                var window = new GaussianBlurWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var window = new MedianBlurWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var window = new EdgeDetectionWindow(imageContext)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
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
                var tool = new HistogramEqualizationEditorTool(imageContext);
                tool.Execute();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "HistogramEqualization", 
                Order = 12, 
                Header = ColorVision.ImageEditor.Properties.Resources.HistogramEqualization, 
                Command = histogramEqualizationCommand 
            });

            RelayCommand erodeCommand = new(o =>
            {
                var window = new MorphologyWindow(imageContext, 0)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "Algorithms",
                GuidId = "Erode",
                Order = 13,
                Header = "腐蚀",
                Command = erodeCommand
            });

            RelayCommand dilateCommand = new(o =>
            {
                var window = new MorphologyWindow(imageContext, 1)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "Algorithms",
                GuidId = "Dilate",
                Order = 14,
                Header = "膨胀",
                Command = dilateCommand
            });

            RelayCommand morphologyCommand = new(o =>
            {
                var window = new MorphologyWindow(imageContext, 2)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "Algorithms",
                GuidId = "MorphologyEx",
                Order = 15,
                Header = "形态学操作",
                Command = morphologyCommand
            });

            RelayCommand bilateralFilterCommand = new(o =>
            {
                var window = new FilterDenoiseWindow(imageContext, 0)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "Algorithms",
                GuidId = "BilateralFilter",
                Order = 16,
                Header = "双边滤波",
                Command = bilateralFilterCommand
            });

            RelayCommand blurCommand = new(o =>
            {
                var window = new FilterDenoiseWindow(imageContext, 1)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window.ShowDialog();
            });
            MenuItemMetadatas.Add(new MenuItemMetadata()
            {
                OwnerGuid = "Algorithms",
                GuidId = "Blur",
                Order = 17,
                Header = "均值模糊",
                Command = blurCommand
            });

            return MenuItemMetadatas;
        }
    }
}
