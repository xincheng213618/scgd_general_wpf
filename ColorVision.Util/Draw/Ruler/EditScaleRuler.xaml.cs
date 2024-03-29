using ColorVision.Draw.Ruler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Draw.Ruler
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
            this.DataContext = DrawingVisualScaleHost1;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
