using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.FullScreen
{
    public class PlacementStatus
    {
        public object Root { get; set; }
        public Panel Parent { get; set; }
        public ContentControl ContentParent { get; set; }
        public WindowStyle WindowStyle { get; set; }

        public WindowState WindowState { get; set; }

        public ResizeMode ResizeMode { get; set; }
    }
}
