using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    public sealed record CMStereoBinocularLocalAnalysis(
        ImageProcessingContext ImageContext,
        DrawEditorContext DrawContext,
        ImageViewConfig Config) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(_ => Open());
            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "P2StereoBinocularLocalAnalysis",
                    Order = 7,
                    Header = "双目标定融合",
                    Command = command
                }
            };
        }

        private void Open()
        {
            if (ImageContext.HImageCache is not HImage)
            {
                MessageBox.Show("当前没有可作为左图的图像。", "双目标定融合", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StereoFusionDebugWindow window = new(ImageContext, DrawContext, Config)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.Show();
        }
    }
}
