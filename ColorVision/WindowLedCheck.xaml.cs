using ColorVision.Extension;
using ColorVision.MQTT;
using Gu.Wpf.Geometry;
using log4net;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision
{
    /// <summary>
    /// WindowFocusPoint.xaml 的交互逻辑
    /// </summary>
    public partial class WindowLedCheck : Window
    {

        public enum BorderType
        {
            [Description("绝对值")]
            Absolute,
            [Description("相对值")]
            Relative
        }
        public ObservableCollection<DrawingVisualCircle> DrawingVisualCircleLists { get; set; } = new ObservableCollection<DrawingVisualCircle>();


        public WindowLedCheck()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualCircleLists;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                OpenImage(filePath);
            }
        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                ImageShow.Source = new BitmapImage(new Uri(filePath));
                Zoombox1.ZoomUniform();

                Zoombox1.LayoutUpdated += (s, e) =>
                {
                    foreach (var item in DrawingVisualCircleLists)
                    {
                        item.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    }
                };


                //ImageShow.VisualsAdd += (s, e) =>
                //{
                //    if (s is Visual visual && visual is DrawingVisualCircle drawingVisualCircle)
                //    {
                //        DrawingVisualCircleLists.Add(drawingVisualCircle);
                //    }
                //};

                //ImageShow.VisualsRemove += (s, e) =>
                //{
                //    if (s is Visual visual && visual is DrawingVisualCircle drawingVisualCircle)
                //    {
                //        DrawingVisualCircleLists.Remove(drawingVisualCircle);
                //    }
                //};
            }
        }
        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }
        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);
            if (DrawingVisual != null)
            {
                var ContextMenu = new ContextMenu();
                MenuItem menuIte2 = new MenuItem() { Header = "删除" };
                menuIte2.Click += (s, e) =>
                {
                    ImageShow.RemoveVisual(DrawingVisual);


                };
                ContextMenu.Items.Add(menuIte2);
                this.ContextMenu = ContextMenu;
            }
            else
            {
                this.ContextMenu = null;
            }

        }
        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas)
            {
                var MouseDownP = e.GetPosition(drawCanvas);
                if (drawCanvas.GetVisual(MouseDownP) is DrawingVisualCircle drawingVisual)
                {
                    if (PropertyGrid2.SelectedObject is CircleAttribute viewModelBase)
                    {

                    }
                    PropertyGrid2.SelectedObject = drawingVisual.Attribute;


                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualCircleLists.IndexOf(drawingVisual);
                }
            }
        }

        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(tbX.Text, out int cols))
                cols = 0;

            if (!int.TryParse(tbY.Text, out int rows))
                rows = 0;


            if (rows < 1 || cols < 1)
            {
                MessageBox.Show("点阵数的行列不能小于1");
                return;
            }

            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                if (!double.TryParse(TextBoxUp.Text, out double startU))
                    startU = 0;

                if (!double.TryParse(TextBoxDown.Text, out double startD))
                    startD = 0;

                if (!double.TryParse(TextBoxLeft.Text, out double startL))
                    startL = 0;
                if (!double.TryParse(TextBoxRight.Text, out double startR))
                    startR = 0;


                if (ComboBoxBorderType.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key== BorderType.Relative)
                {
                    startU = bitmapImage.PixelHeight * startU / 100;
                    startD = bitmapImage.PixelHeight * startD / 100;

                    startL = bitmapImage.PixelWidth * startL / 100;
                    startR = bitmapImage.PixelWidth * startR / 100;
                }

                double StepRow = (bitmapImage.PixelHeight - startD - startU) / (rows-1);
                double StepCol= (bitmapImage.PixelWidth - startL - startR) / (cols-1);



                int start = DrawingVisualCircleLists.Count;
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        if (RadioButtonCircle.IsChecked==true)
                        {
                            DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircleWord();
                            drawingVisualCircle.Attribute.Center = new Point(startL + StepCol * j, startU + StepRow * i);
                            drawingVisualCircle.Attribute.Radius = 100;
                            drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                            drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 10);
                            drawingVisualCircle.Attribute.ID = start + i * cols + j +1;
                            drawingVisualCircle.Render();
                            ImageShow.AddVisual(drawingVisualCircle);
                            DrawingVisualCircleLists.Add(drawingVisualCircle);
                        }
                        else
                        {
                            DrawingVisualRectangle drawingVisualCircle = new  DrawingVisualRectangle();
                            drawingVisualCircle.Attribute.Rect = new Rect(startL + StepCol * j, startU + StepRow * i,100,100);
                            drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                            drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 10);
                            drawingVisualCircle.Render();
                            ImageShow.AddVisual(drawingVisualCircle);
                        }


                    }
                }

            }




        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ComboBoxBorderType.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()
                                                                  select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType.SelectedIndex = 0;
            ComboBoxBorderType.SelectionChanged += (s, e) =>
            {
                if (ComboBoxBorderType.SelectedItem is KeyValuePair<string, BorderType> KeyValue && KeyValue.Value is BorderType communicateType)
                {

                }
            };
            WindowState = WindowState.Maximized;
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            ImageShow.Clear();
            DrawingVisualCircleLists.Clear();
            PropertyGrid2.SelectedObject = null;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualCircleLists[listView.SelectedIndex] is DrawingVisualCircle drawingVisual)
            {
                if (PropertyGrid2.SelectedObject is CircleAttribute viewModelBase)
                {
                    viewModelBase.PropertyChanged -= (s, e) =>
                    {
                    };
                }

                PropertyGrid2.SelectedObject = drawingVisual.Attribute;
                drawingVisual.Attribute.PropertyChanged += (s, e) =>
                {

                };
                ImageShow.TopVisual(drawingVisual);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is DrawingVisualCircle drawingVisualCircle)
            {
                if (checkBox.IsChecked == true)
                {
                    if (!ImageShow.ContainsVisual(drawingVisualCircle))
                    {
                        ImageShow.AddVisual(drawingVisualCircle);
                    }

                }
                else
                {
                    if (ImageShow.ContainsVisual(drawingVisualCircle))
                        ImageShow.RemoveVisual(drawingVisualCircle);
                }
            }
        }
    }
}
