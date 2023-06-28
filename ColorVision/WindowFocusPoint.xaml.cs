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
        public ObservableCollection<DrawingVisualCircle> DrawingVisualCircleLists { get; set; } = new ObservableCollection<DrawingVisualCircle>();


        CADPoints CADPoints { get; set; } = new CADPoints();

        public WindowFocusPoint()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualCircleLists;

            StackPanelCADPoints.DataContext = CADPoints;

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

            BitmapImage bitmapImage = CreateSolidColorBitmap(width,height, System.Windows.Media.Colors.White);
            ImageShow.Source = bitmapImage;
            Zoombox1.ZoomUniform();

            Zoombox1.LayoutUpdated += (s, e) =>
            {
                foreach (var item in DrawingVisualCircleLists)
                {
                    item.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                }
            };
        }

        public static BitmapImage CreateSolidColorBitmap(int width, int height, System.Windows.Media.Color color)
        {
            // 创建一个 WriteableBitmap，用于绘制纯色图像
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);

            // 将所有像素设置为指定的颜色
           
            writeableBitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)writeableBitmap.BackBuffer;
                int stride = writeableBitmap.BackBufferStride;

                for (int y = 0; y < writeableBitmap.PixelHeight; y++)
                {
                    for (int x = 0; x < writeableBitmap.PixelWidth; x++)
                    {
                        pBackBuffer[y * stride + 4 * x] = color.B;     // 蓝色通道
                        pBackBuffer[y * stride + 4 * x + 1] = color.G; // 绿色通道
                        pBackBuffer[y * stride + 4 * x + 2] = color.R; // 红色通道
                        pBackBuffer[y * stride + 4 * x + 3] = color.A; // 透明度通道
                    }
                }
            }


            writeableBitmap.Unlock();

            BitmapImage bitmapImage = new BitmapImage();
            using (var stream = new System.IO.MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                encoder.Save(stream);
                stream.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }



        DrawingVisualCircle drawingVisualCirclex1;
        DrawingVisualCircle drawingVisualCirclex2;
        DrawingVisualCircle drawingVisualCirclex3;
        DrawingVisualCircle drawingVisualCirclex4;
        DrawingVisualCircle drawingVisualCirclecenter;


        private void SetDeafult_Click(object sender, RoutedEventArgs e)
        {
            drawingVisualCirclex1 = new DrawingVisualCircleWord();
            drawingVisualCirclex1.Attribute.Center = new Point(CADPoints.X1.X, CADPoints.X1.Y);
            drawingVisualCirclex1.Attribute.Radius = 5;
            drawingVisualCirclex1.Attribute.Brush = Brushes.Transparent;
            drawingVisualCirclex1.Attribute.Pen = new Pen(Brushes.Red, 2);
            drawingVisualCirclex1.Attribute.ID = 0;
            drawingVisualCirclex1.Render();


            drawingVisualCirclex2 = new DrawingVisualCircleWord();
            drawingVisualCirclex2.Attribute.Center = new Point(CADPoints.X2.X, CADPoints.X2.Y);
            drawingVisualCirclex2.Attribute.Radius = 5;
            drawingVisualCirclex2.Attribute.Brush = Brushes.Transparent;
            drawingVisualCirclex2.Attribute.Pen = new Pen(Brushes.Red, 2);
            drawingVisualCirclex2.Attribute.ID = 0;
            drawingVisualCirclex2.Render();

            drawingVisualCirclex3 = new DrawingVisualCircleWord();
            drawingVisualCirclex3.Attribute.Center = new Point(CADPoints.X3.X, CADPoints.X3.Y);
            drawingVisualCirclex3.Attribute.Radius = 5;
            drawingVisualCirclex3.Attribute.Brush = Brushes.Transparent;
            drawingVisualCirclex3.Attribute.Pen = new Pen(Brushes.Red, 2);
            drawingVisualCirclex3.Attribute.ID = 0;
            drawingVisualCirclex3.Render();

            drawingVisualCirclex4 = new DrawingVisualCircleWord();
            drawingVisualCirclex4.Attribute.Center = new Point(CADPoints.X4.X, CADPoints.X4.Y);
            drawingVisualCirclex4.Attribute.Radius = 5;
            drawingVisualCirclex4.Attribute.Brush = Brushes.Transparent;
            drawingVisualCirclex4.Attribute.Pen = new Pen(Brushes.Red, 2);
            drawingVisualCirclex4.Attribute.ID = 0;
            drawingVisualCirclex4.Render();

           drawingVisualCirclecenter = new DrawingVisualCircleWord();
           drawingVisualCirclecenter.Attribute.Center = new Point(CADPoints.Center.X, CADPoints.Center.Y);
           drawingVisualCirclecenter.Attribute.Radius = 5;
           drawingVisualCirclecenter.Attribute.Brush = Brushes.Transparent;
           drawingVisualCirclecenter.Attribute.Pen = new Pen(Brushes.Red, 2);
           drawingVisualCirclecenter.Attribute.ID = 0;
            drawingVisualCirclecenter.Render();





            ImageShow.AddVisual(drawingVisualCirclex1);
            ImageShow.AddVisual(drawingVisualCirclex2);
            ImageShow.AddVisual(drawingVisualCirclex3);
            ImageShow.AddVisual(drawingVisualCirclex4);
            ImageShow.AddVisual(drawingVisualCirclecenter);



            DrawingVisualCircleLists.Add(drawingVisualCirclex1);
            DrawingVisualCircleLists.Add(drawingVisualCirclex2);
            DrawingVisualCircleLists.Add(drawingVisualCirclex3);
            DrawingVisualCircleLists.Add(drawingVisualCirclex4);
            DrawingVisualCircleLists.Add(drawingVisualCirclecenter);
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
                //        DrawingVisualLists.Add(drawingVisualCircle);
                //    }
                //};

                //ImageShow.VisualsRemove += (s, e) =>
                //{
                //    if (s is Visual visual && visual is DrawingVisualCircle drawingVisualCircle)
                //    {
                //        DrawingVisualLists.Remove(drawingVisualCircle);
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
