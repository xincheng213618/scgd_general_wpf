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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Templates
{
    /// <summary>
    /// CalibrationUpload.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationUpload : Window
    {
        public CalibrationUpload()
        {
            InitializeComponent();
            this.DragEnter += (s, e) =>
            {
                e.Effects = DragDropEffects.Scroll;
                e.Handled = true;
            };
        }


        private void UIElement_OnDrop(object sender, DragEventArgs e)
        {
            var b = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (b)
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a.First();
                TxtCalibrationFile.Text = a.First();
            }
        }

        private void UIElement_OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
        }
    }
}
