using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Media
{
    /// <summary>
    /// 用于还原窗口
    /// </summary>

    public class WindowStatus
    {
        public object Root { get; set; }
        public Grid Parent { get; set; }

        public WindowStyle WindowStyle { get; set; }

        public WindowState WindowState { get; set; }

        public ResizeMode ResizeMode { get; set; }
    }
}
