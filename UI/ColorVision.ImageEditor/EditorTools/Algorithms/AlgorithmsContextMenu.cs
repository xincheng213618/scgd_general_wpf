using ColorVision.Common.MVVM;
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
                Header = "图像算法", 
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
                Header = "反相", 
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
                Header = "自动色阶调整", 
                Command = autoLevelsCommand 
            });

            // 白平衡 - 打开窗口调整
            RelayCommand whiteBalanceCommand = new(o =>
            {
                if (context.ImageView != null)
                {
                    var window = new WhiteBalanceWindow(context.ImageView)
                    {
                        Owner = Application.Current.GetActiveWindow()
                    };
                    window.ShowDialog();
                }
            });
            MenuItemMetadatas.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "WhiteBalance", 
                Order = 3, 
                Header = "白平衡调整", 
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
                Header = "伽马校正", 
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
                Header = "亮度对比度调整", 
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
                Header = "阈值处理", 
                Command = thresholdCommand 
            });
            return MenuItemMetadatas;
        }
    }
}
