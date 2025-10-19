using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public record class AlgorithmsEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var items = new List<MenuItemMetadata>();

            // Create parent menu item for Algorithms
            items.Add(new MenuItemMetadata() 
            { 
                GuidId = "Algorithms", 
                Order = 200, 
                Header = "算法", 
                Icon = MenuItemIcon.TryFindResource("DrawingImageMax") 
            });

            // Add algorithm menu items
            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "InvertImage", 
                Order = 1, 
                Header = "反相", 
                Command = new InvertEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "AutoLevelsAdjust", 
                Order = 2, 
                Header = "自动色阶调整", 
                Command = new AutoLevelsAdjustEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "WhiteBalance", 
                Order = 3, 
                Header = "白平衡调整...", 
                Command = new WhiteBalanceEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "GammaCorrection", 
                Order = 4, 
                Header = "伽马校正...", 
                Command = new GammaCorrectionEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "BrightnessContrast", 
                Order = 5, 
                Header = "亮度对比度...", 
                Command = new BrightnessContrastEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "Threshold", 
                Order = 6, 
                Header = "阈值处理...", 
                Command = new ThresholdEditorTool(context).Command 
            });

            items.Add(new MenuItemMetadata() 
            { 
                OwnerGuid = "Algorithms", 
                GuidId = "RemoveMoire", 
                Order = 7, 
                Header = "滤除摩尔纹", 
                Command = new RemoveMoireEditorTool(context).Command 
            });

            return items;
        }
    }
}
