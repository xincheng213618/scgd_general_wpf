using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public partial class ModelViewer3DWindow : Window
    {
        public ModelViewer3DWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            ViewerControl.DisposeViewer();
        }
    }
}
