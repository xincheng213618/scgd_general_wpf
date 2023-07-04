using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Template;
using ColorVision.Util;
using cvColorVision;
using Gu.Wpf.Geometry;
using HandyControl.Tools.Extension;
using log4net;
using Microsoft.VisualBasic.Logging;
using MySqlX.XDevAPI.Relational;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.Zoombox;

namespace ColorVision.Template
{


    /// <summary>
    /// 关注点模板
    /// </summary>
    public class PoiParam : ParamBase
    {
        private static int No = 1;
        public PoiParam()
        {
            ID = No++;
        }
        public PoiParam(PoiMasterModel dbModel)
        {
            this._ID = dbModel.Id;
            this._PoiName = dbModel.Name;
            this._Width = dbModel.Width;
            this._Height = dbModel.Height;
            this._Type = dbModel.Type;
            this.DatumAreaPoints.X1X = (int)dbModel.LeftTopX;
            this.DatumAreaPoints.X1Y = (int)dbModel.LeftTopY;
        }


        public string PoiName { get { return _PoiName; } set { _PoiName = value; } }
        private string _PoiName;
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;

        public int Type { get => _Type; set { _Type = value; NotifyPropertyChanged(); } }
        private int _Type;


        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;

        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;


        /// <summary>
        /// 关注点列表
        /// </summary>
        public List<PoiParamData> PoiPoints { get; set; } = new List<PoiParamData>();

        public DatumAreaPoints DatumAreaPoints { get; set; } = new DatumAreaPoints();

        public int DeafultCircleRadius { get; set; } = 10;

        public int DeafultRectWidth { get; set; } = 20;

        public int DeafultRectHeight { get; set; } = 20;
    }


    public enum RiPointTypes
    {
        Circle = 0,
        Rect = 1,
        Mask = 2
    }

    public class PoiParamData
    {
        public PoiParamData(PoiDetailModel dbModel)
        {
            ID = dbModel.Id;
            Name = dbModel.Name;
            switch (dbModel.Type)
            {
                case 0:
                    PointType = RiPointTypes.Circle; break;
                case 1:
                    PointType = RiPointTypes.Rect; break;
                case 2:
                    PointType = RiPointTypes.Mask; break;
                default:
                    PointType = RiPointTypes.Circle; break;
            }
            PixX = (double)dbModel.PixX;
            PixY = (double)dbModel.PixY;
            PixWidth = (double)dbModel.PixWidth;
            PixHeight = (double)dbModel.PixHeight;
        }

        public PoiParamData()
        {
        }

        /// <summary>
        /// 数据库ID
        /// </summary>
        [JsonIgnore()]
        public int Pid { get; set; }
        public int  ID { set; get; }

        public string Name { set; get; }
        public RiPointTypes PointType { set; get; }
        public double PixX { set; get; }
        public double PixY { set; get; }
        public double PixWidth { set; get; }
        public double PixHeight { set; get; }
    }



    public class DatumAreaPoints:ViewModelBase
    {
        [JsonIgnore()]
        public int X1X { get => (int)X1.X; set { X1 = new Point( value, X1.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X1Y { get => (int)X1.Y; set { X1 = new Point(X1.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X2X { get => (int)X2.X; set { X2 = new Point(value, X2.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X2Y { get => (int)X2.Y; set { X2 = new Point(X2.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X3X { get => (int)X3.X; set { X3 = new Point(value, X3.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X3Y { get => (int)X3.Y; set { X3 = new Point(X3.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X4X { get => (int)X4.X; set { X4 = new Point(value, X4.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int X4Y { get => (int)X4.Y; set { X4 = new Point(X4.X, value); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int CenterX { get => (int)Center.X; set { Center = new Point(value, Center.Y); NotifyPropertyChanged(); } }
        [JsonIgnore]
        public int CenterY { get => (int)Center.Y; set { Center = new Point(Center.X, value); NotifyPropertyChanged(); } }

        public Point X1 { get; set; } = new Point() { X = 100, Y = 100 };
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

        DatumAreaPoints DatumAreaPoints { get; set; } = new DatumAreaPoints();

        public WindowFocusPoint()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            PoiParam = new PoiParam();
            DatumAreaPoints = PoiParam.DatumAreaPoints;
            StackPanelDatumAreaPoints.DataContext = PoiParam.DatumAreaPoints;
            this.DataContext = PoiParam;
        }

        PoiParam PoiParam { get; set; }
        public WindowFocusPoint(PoiParam poiParam)
        {
            PoiParam = poiParam;
            DatumAreaPoints = PoiParam.DatumAreaPoints; 
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            StackPanelDatumAreaPoints.DataContext = DatumAreaPoints;
            this.DataContext = PoiParam;
        }

        public bool IsLayoutUpdated { get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; if(value) UpdateVisualLayout();  } }
        private bool _IsLayoutUpdated = true;

        private void UpdateVisualLayout()
        {
            foreach (var item in DefaultPoint)
            {
                if (item is DrawingVisualDatumCircle visualDatumCircle)
                {
                    visualDatumCircle.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                }
            }

            if (drawingVisualDatum != null && drawingVisualDatum is DrawingVisualDatumCircle dw)
            {
                dw.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
            }

            if (IsLayoutUpdated)
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
            }
        }



        private async void Window_Initialized(object sender, EventArgs e)
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

            double oldmax = Zoombox1.ContentMatrix.M11;
            Zoombox1.LayoutUpdated += (s, e) =>
            {
                if (oldmax != Zoombox1.ContentMatrix.M11)
                {
                    oldmax = Zoombox1.ContentMatrix.M11;
                    UpdateVisualLayout();
                }
            };

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;
                await Task.Delay(200);

                LoadPoiFromDb(PoiParam);
                WaitControlProgressBar.Value = 10;

                if (PoiParam.PoiPoints.Count > 100)
                {
                    IsLayoutUpdated = false;

                }

                CreateImage(PoiParam.Width, PoiParam.Height, System.Windows.Media.Colors.White,false);
                WaitControlProgressBar.Value = 20;
                PoiParamToDrawingVisual(PoiParam);

            }
            else
            {
                PoiParam.Width = 400;
                PoiParam.Height = 300;
            }

            this.Closed += (s, e) =>
            {
                PoiParam.PoiPoints.Clear();
                foreach (var item in DrawingVisualLists)
                {
                    DrawAttributeBase drawAttributeBase = item.GetAttribute();
                    if (drawAttributeBase is CircleAttribute circle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID =circle.ID,
                            Name = circle.Name,
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius,
                            PixHeight = circle.Radius,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);

                    }
                    else if (drawAttributeBase is RectangleAttribute rectangle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID =rectangle.ID,
                            Name = rectangle.Name,
                            PointType = RiPointTypes.Rect,
                            PixX = rectangle.Rect.X,
                            PixY = rectangle.Rect.Y,
                            PixWidth = rectangle.Rect.Width,
                            PixHeight = rectangle.Rect.Height,
                        };
                        PoiParam.PoiPoints.Add(poiParamData);
                    }
                }
            };

            SettingGroup.DataContext = this;

        }

        private void LoadPoiFromDb(PoiParam poiParam)
        {
            TemplateControl.GetInstance().LoadPoiDetailFromDB(poiParam);
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
            CreateImage(PoiParam.Width, PoiParam.Height, Colors.White,false);
        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                if (ImageShow.Source == null)
                {
                    ImageShow.Source = new BitmapImage(new Uri(filePath));
                    Zoombox1.ZoomUniform();
                }
                else
                {
                    ImageShow.Source = new BitmapImage(new Uri(filePath));
                }
                PoiParam.Width = bitmapImage.PixelWidth;
                PoiParam.Height = bitmapImage.PixelHeight;
            }
        }
        private bool Init;

        private void CreateImage(int width, int height, System.Windows.Media.Color color,bool IsClear = true)
        {
            Thread thread = new Thread(() => 
            {
                BitmapImage bitmapImage = ImageUtil.CreateSolidColorBitmap(width, height, color);
                bitmapImage.Freeze();
                Application.Current.Dispatcher.Invoke(() =>
                {

                    if (ImageShow.Source == null)
                    {
                        ImageShow.Source = bitmapImage;
                        Zoombox1.ZoomUniform();
                    }
                    else
                    {
                        ImageShow.Source = bitmapImage;
                    }
                    if (IsClear)
                    {
                        ImageShow.Clear();
                        DrawingVisualLists.Clear();
                        PropertyGrid2.SelectedObject = null;
                    }
                    if (Init)
                    {
                        WaitControl.Visibility = Visibility.Hidden;
                        WaitControlProgressBar.Visibility = Visibility.Hidden;
                    }
                    Init = true;

                });
            });
            thread.Start();

        }

        private async void PoiParamToDrawingVisual(PoiParam poiParam)
        {
            int i = 0;


            foreach (var item in poiParam.PoiPoints)
            {
                i++;
                if (i % 50 == 0)
                {
                    WaitControlProgressBar.Value = 20 + i * 79 / poiParam.PoiPoints.Count;
                    await Task.Delay(10);
                }
                switch (item.PointType)
                {
                    case RiPointTypes.Circle:
                        DrawingVisualCircleWord drawingVisualCircle = new DrawingVisualCircleWord();
                        drawingVisualCircle.Attribute.Center = new Point(item.PixX, item.PixY);
                        drawingVisualCircle.Attribute.Radius = item.PixWidth;
                        drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                        drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        drawingVisualCircle.Attribute.ID = item.ID;
                        drawingVisualCircle.Attribute.Name = item.Name;
                        drawingVisualCircle.Render();
                        ImageShow.AddVisual(drawingVisualCircle);
                        break;
                    case RiPointTypes.Rect:
                        DrawingVisualRectangle drawingVisualRectangle = new DrawingVisualRectangle();
                        drawingVisualRectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                        drawingVisualRectangle.Attribute.Brush = Brushes.Transparent;
                        drawingVisualRectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        drawingVisualRectangle.Attribute.ID = item.ID;
                        drawingVisualRectangle.Attribute.Name = item.Name;
                        drawingVisualRectangle.Render();
                        ImageShow.AddVisual(drawingVisualRectangle);
                        break;
                    case RiPointTypes.Mask:
                        break;
                }
            }
            WaitControlProgressBar.Value = 99;

            if (Init)
            {
                WaitControl.Visibility = Visibility.Hidden;
                WaitControlProgressBar.Visibility = Visibility.Hidden;
            }
            Init = true;
        }





        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

        private void SetDeafult_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButtonBuildMode2.IsChecked == true)
            {
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

                    if (ComboBoxBorderType.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                    {
                        startU = bitmapImage.PixelHeight * startU / 100;
                        startD = bitmapImage.PixelHeight * startD / 100;

                        startL = bitmapImage.PixelWidth * startL / 100;
                        startR = bitmapImage.PixelWidth * startR / 100;


                    }

                    PoiParam.DatumAreaPoints.X1X = (int)startL;
                    PoiParam.DatumAreaPoints.X1Y = (int)startU;
                    PoiParam.DatumAreaPoints.X2X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumAreaPoints.X2Y = (int)startU;
                    PoiParam.DatumAreaPoints.X3X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumAreaPoints.X3Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumAreaPoints.X4X = (int)startR;
                    PoiParam.DatumAreaPoints.X4Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumAreaPoints.CenterX = (int)bitmapImage.PixelWidth / 2;
                    PoiParam.DatumAreaPoints.CenterY = bitmapImage.PixelHeight / 2;

                }
            }


            List<Point> Points = new List<Point>()
            {
                new Point(DatumAreaPoints.X1.X, DatumAreaPoints.X1.Y),
                new Point(DatumAreaPoints.X2.X, DatumAreaPoints.X2.Y),
                new Point(DatumAreaPoints.X3.X, DatumAreaPoints.X3.Y),
                new Point(DatumAreaPoints.X4.X, DatumAreaPoints.X4.Y),
                new Point(DatumAreaPoints.Center.X, DatumAreaPoints.Center.Y),
            };

            foreach (var item in DefaultPoint)
            {
                ImageShow.RemoveVisual(item);
            }
            DefaultPoint.Clear();

            for (int i = 0; i < Points.Count; i++)
            {
                DrawingVisualDatumCircle drawingVisual = new DrawingVisualDatumCircle();
                drawingVisual.Attribute.Center = Points[i];
                drawingVisual.Attribute.Radius = 5;
                drawingVisual.Attribute.Brush = Brushes.Blue;
                drawingVisual.Attribute.Pen = new Pen(Brushes.Blue, 2);
                drawingVisual.Attribute.ID = i + 1;
                drawingVisual.Render();
                DefaultPoint.Add(drawingVisual);
                ImageShow.AddVisual(drawingVisual);
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
            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                int start = DrawingVisualLists.Count;

                if (RadioButtonAreaRect.IsChecked == true)
                {

                    double startU = 0;
                    double startD = 0;

                    double startL = 0;
                    double startR = 0;

                    if (!int.TryParse(tbX.Text, out int cols))
                        cols = 0;

                    if (!int.TryParse(tbY.Text, out int rows))
                        rows = 0;

                    if (rows < 1 || cols < 1)
                    {
                        MessageBox.Show("点阵数的行列不能小于1");
                        return;
                    }

                    if (!double.TryParse(tbWidth.Text, out double Width))
                        Width = 0;
                    if (!double.TryParse(tbHeight.Text, out double Height))
                        Height = 0;


                    startU =  PoiParam.DatumAreaPoints.CenterY - Height/2;
                    startD = bitmapImage.PixelHeight - PoiParam.DatumAreaPoints.CenterY - Height / 2;


                    startL = PoiParam.DatumAreaPoints.CenterX - Width / 2;
                    startR = bitmapImage.PixelWidth - PoiParam.DatumAreaPoints.CenterX - Width / 2;

                    //if (RadioButtonBuildMode1.IsChecked == true)
                    //{
                    //    startU = PoiParam.DatumAreaPoints.X1.X;
                    //    startD = bitmapImage.PixelHeight - PoiParam.DatumAreaPoints.X3.Y;


                    //    startL = PoiParam.DatumAreaPoints.X1.Y;
                    //    startR = bitmapImage.PixelWidth - PoiParam.DatumAreaPoints.X3.X;
                    //}
                    //else if (RadioButtonBuildMode2.IsChecked == true)
                    //{
                    //    if (!double.TryParse(TextBoxUp.Text, out startU))
                    //        startU = 0;

                    //    if (!double.TryParse(TextBoxDown.Text, out startD))
                    //        startD = 0;

                    //    if (!double.TryParse(TextBoxLeft.Text, out startL))
                    //        startL = 0;
                    //    if (!double.TryParse(TextBoxRight.Text, out startR))
                    //        startR = 0;

                    //    if (ComboBoxBorderType.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                    //    {
                    //        startU = bitmapImage.PixelHeight * startU / 100;
                    //        startD = bitmapImage.PixelHeight * startD / 100;

                    //        startL = bitmapImage.PixelWidth * startL / 100;
                    //        startR = bitmapImage.PixelWidth * startR / 100;
                    //    }
                    //}
                    //else if (RadioButtonBuildMode3.IsChecked == true)
                    //{
                    //    return;
                    //}


                    double StepRow = (bitmapImage.PixelHeight - startD - startU) / (rows - 1);
                    double StepCol = (bitmapImage.PixelWidth - startL - startR) / (cols - 1);

                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            if (RadioButtonCircle.IsChecked == true)
                            {
                                DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircleWord();
                                drawingVisualCircle.Attribute.Center = new Point(startL + StepCol * j, startU + StepRow * i);
                                drawingVisualCircle.Attribute.Radius = PoiParam.DeafultCircleRadius;
                                drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                                drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                drawingVisualCircle.Attribute.ID = start + i * cols + j + 1;
                                drawingVisualCircle.Render();
                                ImageShow.AddVisual(drawingVisualCircle);
                            }
                            else
                            {
                                DrawingVisualRectangle drawingVisualCircle = new DrawingVisualRectangle();
                                drawingVisualCircle.Attribute.Rect = new Rect(startL + StepCol * j - PoiParam.DeafultRectWidth/2, startU + StepRow * i - PoiParam.DeafultRectHeight/2, PoiParam.DeafultRectWidth, PoiParam.DeafultRectHeight);
                                drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                                drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                                drawingVisualCircle.Attribute.ID = start + i * cols + j + 1;
                                drawingVisualCircle.Render();
                                ImageShow.AddVisual(drawingVisualCircle);
                            }


                        }
                    }


                }
                else if (RadioButtonAreaCircle.IsChecked == true)
                {
                    if (!int.TryParse(TextBoxAreaCircleR.Text, out int CircleR))
                        CircleR = 0;


                    if (!int.TryParse(tbNum.Text, out int count))
                        count = 0;
                    if (!int.TryParse(tbAngle.Text, out int angle))
                        angle = 0;

                    if (count < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1");
                        return;
                    }

                    for (int i = 0; i < count; i++)
                    {

                        double x1 = PoiParam.DatumAreaPoints.CenterX + CircleR * Math.Cos(i * 2 * Math.PI / count + Math.PI / 180 * angle);
                        double y1 = PoiParam.DatumAreaPoints.CenterY + CircleR * Math.Sin(i * 2 * Math.PI / count + Math.PI / 180 * angle);

                        if (RadioButtonCircle.IsChecked == true)
                        {
                            DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircleWord();
                            drawingVisualCircle.Attribute.Center = new Point(x1, y1);
                            drawingVisualCircle.Attribute.Radius = PoiParam.DeafultCircleRadius;
                            drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                            drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                            drawingVisualCircle.Attribute.ID = start + i + 1;
                            drawingVisualCircle.Render();
                            ImageShow.AddVisual(drawingVisualCircle);
                        }
                        else
                        {
                            DrawingVisualRectangle drawingVisualCircle = new DrawingVisualRectangle();
                            drawingVisualCircle.Attribute.Rect = new Rect(x1 - PoiParam.DeafultRectWidth/2, y1 - PoiParam.DeafultRectHeight/2, PoiParam.DeafultRectWidth, PoiParam.DeafultRectHeight);
                            drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                            drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                            drawingVisualCircle.Attribute.ID = start + i + 1;
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
            
            foreach (var item in DrawingVisualLists.ToList())
            {
                if (item is Visual visual)
                    ImageShow.RemoveVisual(visual);
            }
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

        private void button4_Click(object sender, RoutedEventArgs e)
        {

        }

        private void button5_Click(object sender, RoutedEventArgs e)
        {

        }
        DrawingVisual drawingVisualDatum;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (drawingVisualDatum != null)
            {
                DefaultPoint.Remove(drawingVisualDatum);
                ImageShow.RemoveVisual(drawingVisualDatum);
            }

            if (RadioButtonAreaCircle.IsChecked ==true)
            {
                if (!double.TryParse(TextBoxAreaCircleR.Text, out double Radius))
                    Radius = 0;
                DrawingVisualDatumCircle drawingVisual = new DrawingVisualDatumCircle();
                drawingVisual.Attribute.Center = PoiParam.DatumAreaPoints.Center;
                drawingVisual.Attribute.Radius = Radius;
                drawingVisual.Attribute.Brush = Brushes.Transparent;
                drawingVisual.Attribute.Pen = new Pen(Brushes.Blue, 2);
                drawingVisual.Render();
                drawingVisualDatum = drawingVisual;
            }
            else
            {
                if (!double.TryParse(tbWidth.Text, out double Width))
                    Width = 0;
                if (!double.TryParse(tbHeight.Text, out double Height))
                    Height = 0;

                DrawingVisualDatumRectangle drawingVisualDatumRectangle = new DrawingVisualDatumRectangle();
                drawingVisualDatumRectangle.Attribute.Rect = new Rect(PoiParam.DatumAreaPoints.Center - new Vector((int)(Width/2), (int)(Height/2)), (PoiParam.DatumAreaPoints.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                drawingVisualDatumRectangle.Attribute.Brush = Brushes.Transparent;

                drawingVisualDatumRectangle.Attribute.Pen = new Pen(Brushes.Blue, 2);
                drawingVisualDatumRectangle.Render();
                drawingVisualDatum = drawingVisualDatumRectangle;
            }

            ImageShow.AddVisual(drawingVisualDatum);

        }

        private void button_save_Click(object sender, RoutedEventArgs e)
        {
            PoiParam.PoiPoints.Clear();
            foreach (var item in DrawingVisualLists)
            {
                DrawAttributeBase drawAttributeBase = item.GetAttribute();
                if (drawAttributeBase is CircleAttribute circle)
                {
                    PoiParamData poiParamData = new PoiParamData()
                    {
                        ID = circle.ID,
                        Name = circle.Name,
                        PointType = RiPointTypes.Circle,
                        PixX = circle.Center.X,
                        PixY = circle.Center.Y,
                        PixWidth = circle.Radius,
                        PixHeight = circle.Radius,
                    };
                    PoiParam.PoiPoints.Add(poiParamData);

                }
                else if (drawAttributeBase is RectangleAttribute rectangle)
                {
                    PoiParamData poiParamData = new PoiParamData()
                    {
                        ID = rectangle.ID,
                        Name = rectangle.Name,
                        PointType = RiPointTypes.Rect,
                        PixX = rectangle.Rect.X,
                        PixY = rectangle.Rect.Y,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                    };
                    PoiParam.PoiPoints.Add(poiParamData);
                }
            }

            TemplateControl.GetInstance().SavePOI2DB(PoiParam);
        }
    }

}
