using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.Util;
using Gu.Wpf.Geometry;
using log4net;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

    public class CADPoints
    {
        public Point X1 { get; set; } = new Point() { X=100, Y=100 };
        public Point X2 { get; set; } = new Point() { X = 300, Y = 100 };
        public Point X3 { get; set; } = new Point() { X = 300, Y = 300 };
        public Point X4 { get; set; } = new Point() { X = 100, Y = 300 };
        public Point Center { get; set; } = new Point() { X = 200, Y = 200 };

    }

    /// <summary>
    /// WindowFocusPoint.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPoint : Window
    {

        public enum BorderType
        {
            [Description("绝对值")]
            Absolute,
            [Description("相对值")]
            Relative
        }
        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();


        CADPoints CADPoints { get; set; } = new CADPoints();

        public WindowFocusPoint()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            StackPanelCADPoints.DataContext = CADPoints;
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


            ImageShow.VisualsAdd += (s, e) =>
            {
                if (s is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && s is Visual visual1)
                {
                    DrawingVisualLists.Add(visual);
                    visual.GetAttribute().PropertyChanged += (s1, e1) =>
                    {
                        if (e1.PropertyName == "IsShow")
                        {
                            ListView1.ScrollIntoView(visual);
                            ListView1.SelectedIndex = DrawingVisualLists.IndexOf(visual);
                            if (visual.GetAttribute().IsShow == true)
                            {
                                if (!ImageShow.ContainsVisual(visual1))
                                {
                                    ImageShow.AddVisual(visual1);
                                }
                            }
                            else
                            {
                                if (ImageShow.ContainsVisual(visual1))
                                {
                                    ImageShow.RemoveVisual(visual1);
                                }
                            }
                        }
                    };

                }
            };

            //如果是不显示
            ImageShow.VisualsRemove += (s, e) =>
            {
                if (s is IDrawingVisual visual)
                {
                    if (visual.GetAttribute().IsShow)
                        DrawingVisualLists.Remove(visual);
                }
            };
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

        private void OpenCAD_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TextBoxCADW.Text, out int width))
                width = 400;
            if (!int.TryParse(TextBoxCADH.Text, out int height))
                height = 300;

            BitmapImage bitmapImage = ImageUtil.CreateSolidColorBitmap(width,height, System.Windows.Media.Colors.White);
            ImageShow.Source = bitmapImage;
            Zoombox1.ZoomUniform();

            Zoombox1.LayoutUpdated += (s, e) =>
            {
                foreach (var item in DrawingVisualLists)
                {
                    DrawAttributeBase drawAttributeBase = item.GetAttribute();
                    if (drawAttributeBase is CircleAttribute circleAttribute)
                    {
                        circleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    }
                    else if (drawAttributeBase is RectangleAttribute rectangleAttribute)
                    {
                        rectangleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    }
                }
            };
        }

        public List<DrawingVisualCircle> DefaultPoint { get; set; } = new List<DrawingVisualCircle>();

        private void SetDeafult_Click(object sender, RoutedEventArgs e)
        {
            List<Point> Points = new List<Point>()
            {
                new Point(CADPoints.X1.X, CADPoints.X1.Y),
                new Point(CADPoints.X2.X, CADPoints.X2.Y),
                new Point(CADPoints.X3.X, CADPoints.X3.Y),
                new Point(CADPoints.X4.X, CADPoints.X4.Y),
                new Point(CADPoints.Center.X, CADPoints.Center.Y),
            };


            foreach (var item in DefaultPoint)
            {
                ImageShow.RemoveVisual(item);
                DrawingVisualLists.Remove(item);

            }
            DefaultPoint.Clear();

            for (int i = 0; i < Points.Count; i++)
            {

                DrawingVisualCircleWord drawingVisualCircle = new DrawingVisualCircleWord();
                drawingVisualCircle.Attribute.Center = Points[i];
                drawingVisualCircle.Attribute.Radius = 5;
                drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 2);
                drawingVisualCircle.Attribute.ID = i+1;
                drawingVisualCircle.Render();
                DefaultPoint.Add(drawingVisualCircle);
                ImageShow.AddVisual(drawingVisualCircle);
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
                    foreach (var item in DrawingVisualLists)
                    {
                        DrawAttributeBase drawAttributeBase = item.GetAttribute();
                        if (drawAttributeBase is CircleAttribute circleAttribute)
                        {
                            circleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        }
                        else if (drawAttributeBase is RectangleAttribute rectangleAttribute)
                        {
                            rectangleAttribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        }
                    }
                };
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

            if (DrawingVisual != null && DrawingVisual is IDrawingVisual drawing)
            {
                var ContextMenu = new ContextMenu();

                MenuItem menuItem = new MenuItem() { Header = "隐藏(_H)" };
                menuItem.Click += (s, e) =>
                {
                    drawing.GetAttribute().IsShow = false;
                };
                MenuItem menuIte2 = new MenuItem() { Header = "删除(_D)" };

                menuIte2.Click += (s, e) =>
                {
                    ImageShow.RemoveVisual(DrawingVisual);
                    PropertyGrid2.SelectedObject = null;
                };
                ContextMenu.Items.Add(menuItem);
                ContextMenu.Items.Add(menuIte2);
                ImageShow.ContextMenu = ContextMenu;
            }
            else
            {
                ImageShow.ContextMenu = null;
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

                if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);
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



                int start = DrawingVisualLists.Count;
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



        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            ImageShow.Clear();
            DrawingVisualLists.Clear();
            PropertyGrid2.SelectedObject = null;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[listView.SelectedIndex] is IDrawingVisual drawingVisual && drawingVisual is Visual visual)
            {

                PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();
                ImageShow.TopVisual(visual);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is Visual visual && visual is IDrawingVisual iDdrawingVisual)
            {

                ListView1.ScrollIntoView(iDdrawingVisual);
                ListView1.SelectedIndex = DrawingVisualLists.IndexOf(iDdrawingVisual);
                if (checkBox.IsChecked == true)
                {
                    if (!ImageShow.ContainsVisual(visual))
                    {
                        iDdrawingVisual.GetAttribute().IsShow = true;
                        ImageShow.AddVisual(visual);
                    }

                }
                else
                {
                    if (ImageShow.ContainsVisual(visual))
                    {
                        iDdrawingVisual.GetAttribute().IsShow = false;
                        ImageShow.RemoveVisual(visual);
                    }
                }
            }
        }
        private void MenuItem_DrawingVisual_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Visual visual)
            {
                PropertyGrid2.SelectedObject = null;
                ImageShow.RemoveVisual(visual);
            }
        }

        private void ListView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[ListView1.SelectedIndex] is Visual visual)
                {
                    ImageShow.RemoveVisual(visual);
                    PropertyGrid2.SelectedObject = null;
                }
            }
        }
    }
}
