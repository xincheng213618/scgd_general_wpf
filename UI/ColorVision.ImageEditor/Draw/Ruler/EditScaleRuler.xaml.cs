using System;
using System.Windows;

namespace ColorVision.ImageEditor.Draw.Ruler
{
    /// <summary>
    /// EditScaleRuler.xaml 的交互逻辑
    /// </summary>
    public partial class EditScaleRuler : Window
    {
        DrawingVisualScaleHost DrawingVisualScaleHost1;
        public EditScaleRuler(DrawingVisualScaleHost drawingVisualScaleHost)
        {
            DrawingVisualScaleHost1 = drawingVisualScaleHost;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = DrawingVisualScaleHost1;
            Resources = null;
        }

        private void Cal_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(TextPiel.Text,out double piex) && double.TryParse(TextActual.Text, out double actual))
            {
                DrawingVisualScaleHost1.ActualLength =  actual/ piex;
            }
            CalPopup.IsOpen = false;
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            CalPopup.IsOpen = true;
        }
    }
}
