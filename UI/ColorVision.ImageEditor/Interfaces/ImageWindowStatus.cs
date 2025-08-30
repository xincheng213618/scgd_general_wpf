#pragma warning disable CS8625
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public class ImageWindowStatus
    {
        public object Root { get; set; }
        public Panel Parent { get; set; }
        public ContentControl ContentParent { get; set; }
        public WindowStyle WindowStyle { get; set; }

        public WindowState WindowState { get; set; }

        public ResizeMode ResizeMode { get; set; }
    }
}
