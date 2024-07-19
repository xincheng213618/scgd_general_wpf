using ColorVision.Common.Utilities;
using ColorVision.UI.Draw;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.Net;
using ColorVision.Themes;
using ColorVision.Util.Draw.Rectangle;
using cvColorVision;
using cvColorVision.Util;
using log4net;
using MQTTMessageLib.FileServer;
using NPOI.SS.UserModel;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Templates.POI
{
    public partial class WindowFocusPoint : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowFocusPoint));
        private string TagName { get; set; } = "P_";

        public PoiParam PoiParam { get; set; }
        public WindowFocusPoint(PoiParam poiParam) 
        {
            PoiParam = poiParam;
            InitializeComponent();
            this.ApplyCaption();
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();
        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

        private async void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = PoiParam;

            ListView1.ItemsSource = DrawingVisualLists;

            ComboBoxBorderType.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>() select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType.SelectedIndex = 0;

            ComboBoxValidate.ItemsSource = TemplateComplyParam.Params["Comply.CIE.AVG"]?.CreateEmpty();
            ComboBoxValidateCIE.ItemsSource = TemplateComplyParam.Params["Comply.CIE"]?.CreateEmpty();


            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()  select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>() select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingPOIPosition)).Cast<DrawingPOIPosition>() select new KeyValuePair<DrawingPOIPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            ComboBoxXYZType.ItemsSource = from e1 in Enum.GetValues(typeof(XYZType)).Cast<XYZType>() select new KeyValuePair<XYZType, string>(e1, e1.ToString());
            ComboBoxXYZType.SelectedIndex = 0;

            ImageContentGrid.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            ToolBarTop = new ToolBarTop(ImageContentGrid, Zoombox1, ImageShow);
            ToolBarTop.ToolBarScaleRuler.IsShow = false;
            ToolBarTop.ImageEditMode = true;
            ToolBar1.DataContext = ToolBarTop;


            ImageShow.VisualsAdd += (s, e) =>
            {
                if (s is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && s is Visual visual1)
                {
                    DrawingVisualLists.Add(visual);
                    visual.BaseAttribute.PropertyChanged += (s1, e1) =>
                    {
                        if (e1.PropertyName == "IsShow")
                        {
                            ListView1.ScrollIntoView(visual);
                            ListView1.SelectedIndex = DrawingVisualLists.IndexOf(visual);
                            if (visual.BaseAttribute.IsShow == true)
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
            DrawingVisualLists.CollectionChanged += (s, e) =>
            {
                if (DrawingVisualLists.Count == 0)
                {
                    FocusPointGrid.Visibility = Visibility.Collapsed;
                    PropertyGrid21.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FocusPointGrid.Visibility = Visibility.Visible;
                    PropertyGrid21.Visibility = Visibility.Visible;
                }
            };

            //如果是不显示
            ImageShow.VisualsRemove += (s, e) =>
            {
                if (s is IDrawingVisual visual)
                {
                    if (visual.BaseAttribute.IsShow)
                        DrawingVisualLists.Remove(visual);
                }
            };

            double oldMax = Zoombox1.ContentMatrix.M11;
            Zoombox1.LayoutUpdated += (s, e) =>
            {
                if (oldMax != Zoombox1.ContentMatrix.M11)
                {
                    oldMax = Zoombox1.ContentMatrix.M11;
                    UpdateVisualLayout(PoiParam.DatumArea.IsLayoutUpdated);
                }
            };

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;
                await Task.Delay(100);

                if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
                    PoiParam.LoadPoiDetailFromDB(PoiParam);

                WaitControlProgressBar.Value = 10;

                if (PoiParam.PoiPoints.Count > 500)
                    PoiParam.DatumArea.IsLayoutUpdated = false;

                CreateImage(PoiParam.Width, PoiParam.Height, Colors.White, false);
                WaitControlProgressBar.Value = 20;
                DatumSet();
                RenderDatumArea();
                PoiParamToDrawingVisual(PoiParam);
                WaitControl.Visibility = Visibility.Collapsed;
                WaitControlProgressBar.Visibility = Visibility.Collapsed;
                log.Debug("Render Poi end");
            }
            else
            {
                PoiParam.Width = 400;
                PoiParam.Height = 300;
            }

            Closed += (s, e) =>
            {
                if (ImageShow.Source == null)
                {
                    PoiParam.Width = 0;
                    PoiParam.Height = 0;
                }
                PoiParam.PoiPoints.Clear();
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    if (DrawingPolygonCache != null)
                    {
                        ImageShow.RemoveVisual(DrawingPolygonCache);
                        DrawingPolygonCache.Render();
                        DrawingPolygonCache = null;
                    }
                }
                if (e.Key == Key.Back)
                {
                    if (DrawingPolygonCache != null && DrawingPolygonCache.Attribute.Points.Count > 0)
                    {
                        DrawingPolygonCache.Attribute.Points.Remove(DrawingPolygonCache.Attribute.Points.Last());
                        DrawingPolygonCache.Render();
                    }
                }
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key ==Key.S)
                {
                    SavePoiParam();
                }
            };
        }

        public ToolBarTop ToolBarTop { get; set; }

        private void Button_UpdateVisualLayout_Click(object sender, RoutedEventArgs e)
        {
            UpdateVisualLayout(true);
        }


        private void UpdateVisualLayout(bool IsLayoutUpdated)
        {
            foreach (var item in DefaultPoint)
            {
                if (item is DrawingVisualDatumCircle visualDatumCircle)
                {
                    visualDatumCircle.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                }
            }

            if (drawingVisualDatum != null && drawingVisualDatum is IDrawingVisualDatum Datum)
            {
                Datum.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                Datum.Render();
            }

            if (IsLayoutUpdated)
            {
                foreach (var item in DrawingVisualLists)
                {
                    item.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    item.Render();
                }
            }
        }



        private LedPicData ledPicData;
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif,*.cvraw,*.cvcie) | *.jpg; *.jpeg; *.png;*.tif;*.cvraw;*.cvcie";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;

                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                if (ext.Contains("cvraw")|| ext.Contains("cvsrc") || ext.Contains("cvcie"))
                {
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;
                    try
                    {
                        OpenImage(new NetFileUtil().OpenLocalCVFile(filePath, fileExtType));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    ledPicData ??= new LedPicData();
                    ledPicData.picUrl = filePath;
                    OpenImage(filePath);
                }
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
                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);

                if (ext == ".cvraw")
                {
                    if (CVFileUtil.ReadCVRaw(filePath, out CVCIEFile fileInfo))
                    {
                        OpenCvSharp.Mat src;
                        if (fileInfo.bpp != 8)
                        {
                            OpenCvSharp.Mat temp = OpenCvSharp.Mat.FromPixelData(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                            src = new OpenCvSharp.Mat();
                            temp.ConvertTo(src, OpenCvSharp.MatType.CV_8U, 1.0 / 256.0);
                            temp.Dispose();
                        }
                        else
                        {
                             src = OpenCvSharp.Mat.FromPixelData(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                        }

                        BitmapSource bitmapSource = src.ToBitmapSource();
                        SetImageSource(bitmapSource);
                    }
                }
                else
                {
                    BitmapSource bitmapImage = new BitmapImage(new Uri(filePath));
                    SetImageSource(bitmapImage);
                }
            }
        }

        public void SetImageSource(ImageSource imageSource)
        {
            ImageShow.Source = imageSource;
            if (imageSource is BitmapSource bitmapSource)
            {
                PoiParam.Width = bitmapSource.PixelWidth;
                PoiParam.Height = bitmapSource.PixelHeight;
                InitDatumAreaValue(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
            }
            ImageShow.ImageInitialize();
            Zoombox1.ZoomUniform();
        }

        private bool Init; 

        private void CreateImage(int width, int height, Color color,bool IsClear = true)
        {
            Thread thread = new(() => 
            {
                BitmapImage bitmapImage = ImageUtils.CreateSolidColorBitmap(width, height, color);
                bitmapImage.Freeze();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ImageShow.Source == null)
                    {
                        ImageShow.Source = bitmapImage;
                        Zoombox1.ZoomUniform();
                        if (IsClear|| !Init)
                            InitDatumAreaValue((int)bitmapImage.Width,(int)bitmapImage.Height);
                    }
                    else
                    {
                        if (ImageShow.Source is BitmapImage img && (img.PixelWidth != bitmapImage.PixelWidth || img.PixelHeight != bitmapImage.PixelHeight))
                        {
                            InitDatumAreaValue((int)bitmapImage.Width, (int)bitmapImage.Height);
                            ImageShow.Source = bitmapImage;
                            Zoombox1.ZoomUniform();
                        }
                        else
                        {
                            ImageShow.Source = bitmapImage;
                        }

                    }
                    if (IsClear)
                    {
                        ImageShow.Clear();
                        DrawingVisualLists.Clear();
                        PropertyGrid2.SelectedObject = null;
                    }
                    if (Init)
                    {
                        WaitControl.Visibility = Visibility.Collapsed;
                        WaitControlProgressBar.Visibility = Visibility.Collapsed;
                    }
                    Init = true;
                    ImageShow.ImageInitialize();

                });
            });
            thread.Start();
        }


        private void InitDatumAreaValue(int width,int height)
        {
            PoiParam.DatumArea.X1X = 0;
            PoiParam.DatumArea.X1Y = 0;
            PoiParam.DatumArea.X2X = width;
            PoiParam.DatumArea.X2Y = 0;
            PoiParam.DatumArea.X3X = width;
            PoiParam.DatumArea.X3Y = height;
            PoiParam.DatumArea.X4X = 0;
            PoiParam.DatumArea.X4Y = height;
            PoiParam.DatumArea.CenterX = width / 2;
            PoiParam.DatumArea.CenterY = height / 2;

            PoiParam.DatumArea.AreaCircleRadius = width> height? height / 2: width/2;
            PoiParam.DatumArea.AreaRectWidth = width;
            PoiParam.DatumArea.AreaRectHeight = height;

            PoiParam.DatumArea.Polygon1X = 0;
            PoiParam.DatumArea.Polygon1Y = 0;
            PoiParam.DatumArea.Polygon2X = width;
            PoiParam.DatumArea.Polygon2Y = 0;
            PoiParam.DatumArea.Polygon3X = width;
            PoiParam.DatumArea.Polygon3Y = height;
            PoiParam.DatumArea.Polygon4X = 0;
            PoiParam.DatumArea.Polygon4Y = height;
            Application.Current.Dispatcher.Invoke(() => PoiParam.DatumArea.IsShowDatumArea = true);
            RenderDatumArea();
            DatumSet();
        }

        private async void PoiParamToDrawingVisual(PoiParam poiParam)
        {
            try
            {
                int i = 0;

                foreach (var item in poiParam.PoiPoints)
                {
                    i++;
                    if (i % 50 == 0)
                    {
                        WaitControlProgressBar.Value = 20 + i * 79 / poiParam.PoiPoints.Count;
                        await Task.Delay(1);
                    }
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DrawingVisualCircleWord Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.ID = i;
                            Circle.Attribute.Text = i.ToString();
                            Circle.Attribute.Name = item.Name;
                            Circle.Attribute.Tag = item.Tag;
                            Circle.Attribute.Tag1 = item.Id;

                            Circle.Render();
                            ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DrawingVisualRectangleWord Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth /2, item.PixY - item.PixHeight /2, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.ID = item.Id;
                            Rectangle.Attribute.Name = item.Name;
                            Rectangle.Attribute.Tag = item.Tag;
                            Rectangle.Attribute.Tag1 = item.Id;
                            Rectangle.Render();
                            ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
                }
                WaitControlProgressBar.Value = 99;

                if (Init)
                {
                    WaitControl.Visibility = Visibility.Collapsed;
                    WaitControlProgressBar.Visibility = Visibility.Collapsed;
                }
                Init = true;
            }
            catch
            {

            }
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (RadioButtonBuildMode2.IsChecked == true)
            {
                if (ImageShow.Source is BitmapSource bitmapImage)
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

                    PoiParam.DatumArea.X1X = (int)startL;
                    PoiParam.DatumArea.X1Y = (int)startU;
                    PoiParam.DatumArea.X2X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumArea.X2Y = (int)startU;
                    PoiParam.DatumArea.X3X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.DatumArea.X3Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumArea.X4X = (int)startR;
                    PoiParam.DatumArea.X4Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.DatumArea.CenterX = (int)bitmapImage.PixelWidth / 2;
                    PoiParam.DatumArea.CenterY = bitmapImage.PixelHeight / 2;
                }
            }
            DatumSet();
        }

        private void DatumSet()
        {
            foreach (var item in DefaultPoint)
            {
                ImageShow.RemoveVisual(item);
            }
            DefaultPoint.Clear();
            if (PoiParam.DatumArea.IsShowDatum)
            {
                List<Point> Points = new()
                {
                    new Point(PoiParam.DatumArea.X1X, PoiParam.DatumArea.X1Y),
                    new Point(PoiParam.DatumArea.X2X, PoiParam.DatumArea.X2Y),
                    new Point(PoiParam.DatumArea.X3X, PoiParam.DatumArea.X3Y),
                    new Point(PoiParam.DatumArea.X4X, PoiParam.DatumArea.X4Y),
                    new Point(PoiParam.DatumArea.CenterX, PoiParam.DatumArea.CenterY),
                };

                for (int i = 0; i < Points.Count; i++)
                {
                    DrawingVisualDatumCircle drawingVisual = new();
                    drawingVisual.Attribute.Center = Points[i];
                    drawingVisual.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                    drawingVisual.Attribute.Brush = Brushes.Blue;
                    drawingVisual.Attribute.Pen = new Pen(Brushes.Blue, 2);
                    drawingVisual.Attribute.ID = i + 1;
                    drawingVisual.Render();
                    DefaultPoint.Add(drawingVisual);
                    ImageShow.AddVisual(drawingVisual);
                }
            }
        }




        private async void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is BitmapSource bitmapImage)
            {
                int Num =0;

                int start = DrawingVisualLists.Count;
                switch (PoiParam.DatumArea.PointType)
                {
                    case RiPointTypes.Circle:
                        if (PoiParam.DatumArea.AreaCircleNum < 1)
                        {
                            MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                            return;
                        }

                        if (PoiParam.DatumArea.AreaCircleNum > 1000)
                        {
                            WaitControl.Visibility = Visibility.Visible;
                            WaitControlProgressBar.Visibility = Visibility.Visible;
                            WaitControlProgressBar.Value = 0;
                            PoiParam.DatumArea.IsLayoutUpdated = false;
                        }


                        for (int i = 0; i < PoiParam.DatumArea.AreaCircleNum; i++)
                        {
                            Num++;
                            if (Num % 100 == 0 && WaitControl.Visibility == Visibility.Visible)
                            {
                                WaitControlProgressBar.Value = Num * 100 / PoiParam.DatumArea.AreaCircleNum;
                                await Task.Delay(1);
                            }

                            double x1 = PoiParam.DatumArea.CenterX + PoiParam.DatumArea.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                            double y1 = PoiParam.DatumArea.CenterY + PoiParam.DatumArea.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);

                            switch (PoiParam.DefaultPointType)
                            {
                                case RiPointTypes.Circle:

                                    if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition)
                                    {
                                        switch (pOIPosition)
                                        {
                                            case DrawingPOIPosition.LineOn:
                                                x1 = PoiParam.DatumArea.CenterX + PoiParam.DatumArea.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + PoiParam.DatumArea.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            case DrawingPOIPosition.Internal:
                                                x1 = PoiParam.DatumArea.CenterX + (PoiParam.DatumArea.AreaCircleRadius- PoiParam.DatumArea.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + (PoiParam.DatumArea.AreaCircleRadius - PoiParam.DatumArea.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            case DrawingPOIPosition.External:
                                                x1 = PoiParam.DatumArea.CenterX + (PoiParam.DatumArea.AreaCircleRadius + PoiParam.DatumArea.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + (PoiParam.DatumArea.AreaCircleRadius + PoiParam.DatumArea.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            default:
                                                break;
                                        }
                                    }
    

                                    DrawingVisualCircleWord Circle = new();
                                    Circle.Attribute.Center = new Point(x1, y1);
                                    Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                    Circle.Attribute.ID = start + i + 1;
                                    Circle.Attribute.Name = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:

                                    if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition2)
                                    {
                                        switch (pOIPosition2)
                                        {
                                            case DrawingPOIPosition.LineOn:
                                                x1 = PoiParam.DatumArea.CenterX + PoiParam.DatumArea.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + PoiParam.DatumArea.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            case DrawingPOIPosition.Internal:
                                                x1 = PoiParam.DatumArea.CenterX + (PoiParam.DatumArea.AreaCircleRadius - PoiParam.DatumArea.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + (PoiParam.DatumArea.AreaCircleRadius - PoiParam.DatumArea.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            case DrawingPOIPosition.External:
                                                x1 = PoiParam.DatumArea.CenterX + (PoiParam.DatumArea.AreaCircleRadius + PoiParam.DatumArea.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                y1 = PoiParam.DatumArea.CenterY + (PoiParam.DatumArea.AreaCircleRadius + PoiParam.DatumArea.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiParam.DatumArea.AreaCircleNum + Math.PI / 180 * PoiParam.DatumArea.AreaCircleAngle);
                                                break;
                                            default:
                                                break;
                                        }
                                    }

                                    DrawingVisualRectangleWord Rectangle = new();
                                    Rectangle.Attribute.Rect = new Rect(x1 - PoiParam.DatumArea.DefaultRectWidth / 2, y1 - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                    Rectangle.Attribute.ID = start + i + 1;
                                    Rectangle.Attribute.Name = string.Format("{0}{1}", TagName, Rectangle.Attribute.ID);
                                    Rectangle.Render();
                                    ImageShow.AddVisual(Rectangle);
                                    break;
                                case RiPointTypes.Mask:
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    case RiPointTypes.Rect:

                        int cols = PoiParam.DatumArea.AreaRectCol;
                        int rows = PoiParam.DatumArea.AreaRectRow;

                        if (rows < 1 || cols < 1)
                        {
                            MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                            return;
                        }
                        double Width = PoiParam.DatumArea.AreaRectWidth;
                        double Height = PoiParam.DatumArea.AreaRectHeight;


                        double startU = PoiParam.DatumArea.CenterY - Height / 2;
                        double startD = bitmapImage.PixelHeight - PoiParam.DatumArea.CenterY - Height / 2;
                        double startL = PoiParam.DatumArea.CenterX - Width / 2;
                        double startR = bitmapImage.PixelWidth - PoiParam.DatumArea.CenterX - Width / 2;

                        if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition1)
                        {
                            switch (PoiParam.DefaultPointType)
                            {
                                case RiPointTypes.Circle:
                                    switch (pOIPosition1)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            startU += PoiParam.DatumArea.DefaultCircleRadius;
                                            startD += PoiParam.DatumArea.DefaultCircleRadius;
                                            startL += PoiParam.DatumArea.DefaultCircleRadius;
                                            startR += PoiParam.DatumArea.DefaultCircleRadius;
                                            break;
                                        case DrawingPOIPosition.External:
                                            startU -= PoiParam.DatumArea.DefaultCircleRadius;
                                            startD -= PoiParam.DatumArea.DefaultCircleRadius;
                                            startL -= PoiParam.DatumArea.DefaultCircleRadius;
                                            startR -= PoiParam.DatumArea.DefaultCircleRadius; 
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                case RiPointTypes.Rect:
                                    switch (pOIPosition1)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            startU += PoiParam.DatumArea.DefaultRectWidth / 2;
                                            startD += PoiParam.DatumArea.DefaultRectWidth / 2;
                                            startL += PoiParam.DatumArea.DefaultRectHeight / 2;
                                            startR += PoiParam.DatumArea.DefaultRectHeight / 2;
                                            break;
                                        case DrawingPOIPosition.External:
                                            startU -= PoiParam.DatumArea.DefaultRectWidth / 2;
                                            startD -= PoiParam.DatumArea.DefaultRectWidth / 2;
                                            startL -= PoiParam.DatumArea.DefaultRectHeight / 2;
                                            startR -= PoiParam.DatumArea.DefaultRectHeight / 2;
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                case RiPointTypes.Mask:
                                    break;
                                default:
                                    break;
                            }
                        }


                        double StepRow = (bitmapImage.PixelHeight - startD - startU) / (rows - 1);
                        double StepCol = (bitmapImage.PixelWidth - startL - startR) / (cols - 1);


                        int all = rows * cols;
                        if (all > 1000)
                        {
                            WaitControl.Visibility = Visibility.Visible;
                            WaitControlProgressBar.Visibility = Visibility.Visible;
                            WaitControlProgressBar.Value = 0;
                            PoiParam.DatumArea.IsLayoutUpdated = false;
                        }

                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                Num++;
                                if (Num % 100 == 0 && WaitControl.Visibility == Visibility.Visible)
                                {
                                    WaitControlProgressBar.Value = Num * 100 / all;
                                    await Task.Delay(1);
                                }

                                double x1 = startL + StepCol * j;
                                double y1 = startU + StepRow * i;

                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:

                                        DrawingVisualCircleWord Circle = new();
                                        Circle.Attribute.Center = new Point(x1, y1);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Attribute.Name = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:

                                        DrawingVisualRectangleWord Rectangle = new();
                                        Rectangle.Attribute.Rect = new Rect(x1 - (double)PoiParam.DatumArea.DefaultRectWidth / 2, y1 - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectWidth);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", TagName, Rectangle.Attribute.ID);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    case RiPointTypes.Mask:
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;
                    case RiPointTypes.Mask:
                        List<Point> pts_src = new();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);
                        pts_src.Add(PoiParam.DatumArea.Polygon4);


                        List<Point> points = Helpers.SortPolyPoints(pts_src);

                        cols = PoiParam.DatumArea.AreaPolygonCol;
                        rows = PoiParam.DatumArea.AreaPolygonRow;


                        double rowStep = 1.0 / (rows - 1);
                        double columnStep = 1.0 / (cols - 1);
                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                // Calculate the position of the point within the quadrilateral
                                double x = (1 - i * rowStep) * (1 - j * columnStep) * points[0].X +
                                           (1 - i * rowStep) * (j * columnStep) * points[1].X +
                                           (i * rowStep) * (1 - j * columnStep) * points[3].X +
                                           (i * rowStep) * (j * columnStep) * points[2].X;

                                double y = (1 - i * rowStep) * (1 - j * columnStep) * points[0].Y +
                                           (1 - i * rowStep) * (j * columnStep) * points[1].Y +
                                           (i * rowStep) * (1 - j * columnStep) * points[3].Y +
                                           (i * rowStep) * (j * columnStep) * points[2].Y;

                                Point point = new(x, y);

                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:
                                        DrawingVisualCircleWord Circle = new();
                                        Circle.Attribute.Center = new Point(point.X, point.Y);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Attribute.Name = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new();
                                        Rectangle.Attribute.Rect = new Rect(point.X - PoiParam.DatumArea.DefaultRectWidth / 2, point.Y - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", TagName, Rectangle.Attribute.ID);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    case RiPointTypes.Mask:
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                        break;

                    case RiPointTypes.Polygon:
                        
                        int No = 0;
                        for (int i = 0; i < PoiParam.DatumArea.Polygons.Count - 1; i++)
                        {
                            double dx = (PoiParam.DatumArea.Polygons[i+1].X - PoiParam.DatumArea.Polygons[i].X) / (PoiParam.DatumArea.Polygons[i].SplitNumber + 1);
                            double dy = (PoiParam.DatumArea.Polygons[i + 1].Y - PoiParam.DatumArea.Polygons[i].Y) / (PoiParam.DatumArea.Polygons[i].SplitNumber + 1);

                            for (int j = 1; j < PoiParam.DatumArea.Polygons[i].SplitNumber +1 ; j++)
                            {
                                No++;
                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:

                                        DrawingVisualCircleWord Circle = new();
                                        Circle.Attribute.Center = new Point(PoiParam.DatumArea.Polygons[i].X + dx*j, PoiParam.DatumArea.Polygons[i].Y + dy * j);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + No;
                                        Circle.Attribute.Name = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new();
                                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Polygons[i].X + dx * j - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.Polygons[i].Y + dy * j - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + No;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", TagName, Rectangle.Attribute.ID);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    default:
                                        break;
                                }
                            }


                        }

                        for (int i = 0; i < PoiParam.DatumArea.Polygons.Count; i++)
                        {
                            if (PoiParam.DatumArea.AreaPolygonUsNode)
                            {
                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:

                                        DrawingVisualCircleWord Circle = new();
                                        Circle.Attribute.Center = new Point(PoiParam.DatumArea.Polygons[i].X, PoiParam.DatumArea.Polygons[i].Y);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i + 1;
                                        Circle.Attribute.Name = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new();
                                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Polygons[i].X - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.Polygons[i].Y - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", TagName, Rectangle.Attribute.ID);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                        break;
                                    default:
                                        break;
                                }
                            }

                        }

                        break;
                    default:
                        break;
                }
                //这里我不推荐添加
                UpdateVisualLayout(true);
                if (WaitControl.Visibility == Visibility.Visible)
                {
                    WaitControl.Visibility = Visibility.Collapsed;
                    WaitControlProgressBar.Visibility = Visibility.Collapsed;
                    WaitControlProgressBar.Value = 0;
                }
                ScrollViewer1.ScrollToEnd();
            }
        }




        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("清空关注点", "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            foreach (var item in DrawingVisualLists.ToList())
                if (item is Visual visual)
                    ImageShow.RemoveVisual(visual);
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
                PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;
                ImageShow.TopVisual(visual);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
        }
        private void MenuItem_DrawingVisual_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Visual visual &&visual is IDrawingVisual drawing)
            {
                PropertyGrid2.SelectedObject = null;
                ImageShow.RemoveVisual(visual);
                DrawingVisualLists.Remove(drawing);
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

        DrawingVisual drawingVisualDatum;
        private void ShowDatumArea_Click(object sender, RoutedEventArgs e)
        {
            RenderDatumArea();
        }

        private void RadioButtonArea_Checked(object sender, RoutedEventArgs e)
        {
            RenderDatumArea();
        }

        private void RenderDatumArea()
        {
            if (drawingVisualDatum != null)
            {
                ImageShow.RemoveVisual(drawingVisualDatum);
            }
            if (PoiParam.DatumArea.IsShowDatumArea)
            {
                switch (PoiParam.DatumArea.PointType)
                {
                    case RiPointTypes.Circle:
                        DrawingVisualDatumCircle Circle = new();
                        Circle.Attribute.Center = PoiParam.DatumArea.Center;
                        Circle.Attribute.Radius = PoiParam.DatumArea.AreaCircleRadius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Render();
                        drawingVisualDatum = Circle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Rect:
                        double Width = PoiParam.DatumArea.AreaRectWidth;
                        double Height = PoiParam.DatumArea.AreaRectHeight;
                        DrawingVisualDatumRectangle Rectangle = new();
                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiParam.DatumArea.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Mask:

                        List<Point> pts_src = new();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);  
                        pts_src.Add(PoiParam.DatumArea.Polygon4);

                        List<Point> result = Helpers.SortPolyPoints(pts_src);
                        DrawingVisualDatumPolygon Polygon = new() { IsComple = true };
                        Polygon.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon.Attribute.Brush = Brushes.Transparent;
                        Polygon.Attribute.Points.Add(result[0]);
                        Polygon.Attribute.Points.Add(result[1]);
                        Polygon.Attribute.Points.Add(result[2]);
                        Polygon.Attribute.Points.Add(result[3]);
                        Polygon.Render();
                        drawingVisualDatum = Polygon;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Polygon:
                        DrawingVisualDatumPolygon Polygon1 = new() { IsComple = false };
                        Polygon1.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon1.Attribute.Brush = Brushes.Transparent;
                        foreach (var item in PoiParam.DatumArea.Polygons)
                        {
                            Polygon1.Attribute.Points.Add(new Point(item.X, item.Y));
                        }
                        Polygon1.Render();
                        drawingVisualDatum = Polygon1;
                        ImageShow.AddVisual(drawingVisualDatum);

                        break;
                    default:
                        break;
                }

            }
        }

        private void SavePoiParam()
        {
            PoiParam.PoiPoints.Clear();
            foreach (var item in DrawingVisualLists)
            {
                DrawBaseAttribute drawAttributeBase = item.BaseAttribute;
                if (drawAttributeBase is CircleAttribute circle)
                {
                    PoiPoint poiParamData = new()
                    {
                        Id = circle.Tag1 ?? -1,
                        PointType = RiPointTypes.Circle,
                        PixX = circle.Center.X,
                        PixY = circle.Center.Y,
                        PixWidth = circle.Radius * 2,
                        PixHeight = circle.Radius * 2,
                        Tag = circle.Tag,
                        Name = circle.Name
                    };
                    PoiParam.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is RectangleAttribute rectangle)
                {
                    PoiPoint poiParamData = new()
                    {
                        Id = rectangle.Tag1 ??-1,
                        Name = rectangle.Name,
                        PointType = RiPointTypes.Rect,
                        PixX = rectangle.Rect.X + rectangle.Rect.Width/2,
                        PixY = rectangle.Rect.Y + rectangle.Rect.Height/2,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                        Tag = rectangle.Tag,
                    };
                    PoiParam.PoiPoints.Add(poiParamData);
                }
            }
            WaitControl.Visibility = Visibility.Visible;
            WaitControlProgressBar.Visibility = Visibility.Collapsed;
            WaitControlText.Text = "数据正在保存";
            Thread thread = new(() =>
            {
                PoiParam.Save2DB(PoiParam);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    WaitControl.Visibility = Visibility.Collapsed;
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "保存成功", "ColorVision");
                });
            });
            thread.Start();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            SavePoiParam();
        }

        public LedCheckCfg ledCheckCfg { get; set; } = new LedCheckCfg();


        private void RadioButtonMode2_Checked(object sender, RoutedEventArgs e)
        {
            PropertyGridAutoFocus.SelectedObject = ledCheckCfg;
          
        }


        //读取本地的灯珠检测配置文件
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "LedCfg files (*.cfg) | *.cfg";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                var Cfg = CfgFile.Load<LedCheckCfg>(filePath);
                if (Cfg == null)
                {
                    MessageBox.Show("读取配置文件失败", "ColorVision");
                    ledCheckCfg = new LedCheckCfg();
                }
                else
                {
                    ledCheckCfg = Cfg;
                }
                PropertyGridAutoFocus.SelectedObject = ledCheckCfg;
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "LedCfg files (*.cfg) | *.cfg";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                bool result=CfgFile.Save(filePath, ledCheckCfg);
                if (result)
                {
                    MessageBox.Show("保存成功", "ColorVision");
                }
                else
                {
                    MessageBox.Show("保存失败", "ColorVision");
                }
            }

        }


        private void DatumAreaImport_Click(object sender, RoutedEventArgs e)
        {
            PoiParam.DatumArea.Polygon1X = PoiParam.DatumArea.X1X;
            PoiParam.DatumArea.Polygon1Y = PoiParam.DatumArea.X1Y;
            PoiParam.DatumArea.Polygon2X = PoiParam.DatumArea.X2X;
            PoiParam.DatumArea.Polygon2Y = PoiParam.DatumArea.X2Y;
            PoiParam.DatumArea.Polygon3X = PoiParam.DatumArea.X3X;
            PoiParam.DatumArea.Polygon3Y = PoiParam.DatumArea.X3Y;
            PoiParam.DatumArea.Polygon4X = PoiParam.DatumArea.X4X;
            PoiParam.DatumArea.Polygon4Y = PoiParam.DatumArea.X4Y;
        }

        private void Button_Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingPopup.IsOpen = true;
        }

        private void Light_Draw_Click(object sender, RoutedEventArgs e)
        {
                byte[] pdata;
                OpenCvSharp.Mat mat;
                if (ledPicData == null)
                {
                    MessageBox.Show("请先载入图片", "ColorVision");
                    return;
                }
                else
                {
                    try
                    {
                        mat = OpenCvSharp.Cv2.ImRead(ledPicData.picUrl, OpenCvSharp.ImreadModes.Unchanged);

                        pdata = new byte[(ulong)mat.DataEnd - (ulong)mat.DataStart];
                        Marshal.Copy(mat.Data, pdata, 0, pdata.Length);
                    }
                    catch (Exception)
                    {

                        throw;
                    }
                }

                double[] LengthResult = new double[4];
                int testdata = 0;

                int channelType = ledCheckCfg.计算图像格式 % 10;

                testdata = ledCheckCfg.灯珠宽方向数量 * ledCheckCfg.灯珠高方向数量;

                double[] banjin = new double[testdata * channelType];
                double[] mianji = new double[testdata * channelType];
                double[] xiangsuhe = new double[testdata * channelType];
                double[] xiangsupingjun = new double[testdata * channelType];

                int[] zuobiaoX = new int[testdata * channelType];
                int[] zuobiaoY = new int[testdata * channelType];

                //////////////////
                float[] localRDMark = new float[8];
                string[] RDdata = new string[1];//RD编号
                double[] PointX = new double[1];//坐标X
                double[] PointY = new double[1];//坐标Y
                if (ledCheckCfg.是否使用本地点位信息计算)
                {
                    try
                    {
                        //读取本地的点位数据
                        IWorkbook workbook = WorkbookFactory.Create("cfg\\" + ledCheckCfg.本地点位信息坐标);
                        ISheet sheet = workbook.GetSheetAt(0);//获取第一个工作薄
                        testdata = sheet.LastRowNum;
                        //MessageBox.IsShow(testdata.ToString());
                        IRow row;
                        RDdata = new string[testdata];
                        PointX = new double[testdata];
                        PointY = new double[testdata];

                        banjin = new double[testdata];
                        mianji = new double[testdata];
                        xiangsuhe = new double[testdata * channelType];
                        xiangsupingjun = new double[testdata * channelType];

                        zuobiaoX = new int[testdata];
                        zuobiaoY = new int[testdata];

                        for (int m = 0; m < testdata; m++)
                        {
                            row = (IRow)sheet.GetRow(m + 1);
                            RDdata[m] = row.GetCell(2).ToString() ?? string.Empty;
                            _ = double.TryParse(row.GetCell(3).ToString(), out PointX[m]);
                            _ = double.TryParse(row.GetCell(4).ToString(), out PointY[m]);
                        }
                        for (int m = 0; m < 4; m++)
                        {
                            _ = float.TryParse(sheet.GetRow(m + 1).GetCell(6).ToString(), out localRDMark[2 * m + 0]);
                            _ = float.TryParse(sheet.GetRow(m + 1).GetCell(7).ToString(), out localRDMark[2 * m + 1]);
                        }
                        workbook.Close();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("本地点位数据文件读取失败，请检查设置与文件！", "ColorVision");
                        return;
                    }
                }
                /////////////

                double calresult = cvCameraCSLib.LedCheckYaQi(ledCheckCfg.isdebug, ledCheckCfg.灯珠抓取通道, (uint)mat.Cols, (uint)mat.Rows, (uint)(int)(mat.Step(1) * 8 / mat.Channels()), (uint)mat.Channels(), pdata
                    , ledCheckCfg.是否启用固定半径计算, ledCheckCfg.灯珠固定半径, ledCheckCfg.轮廓最小面积,
                    testdata, ledCheckCfg.轮廓范围系数, ledCheckCfg.图像二值化补正,
                    banjin, zuobiaoX, zuobiaoY,
                    ledCheckCfg.灯珠宽方向数量, ledCheckCfg.灯珠高方向数量, ledCheckCfg.关注范围, ledCheckCfg.关注区域二值化, ledCheckCfg.boundry,
                    ledCheckCfg.LengthCheck, ledCheckCfg.LengthRange, LengthResult, ledCheckCfg.是否使用本地点位信息计算, localRDMark, PointX, PointY);

                PoiParam.DatumArea.LedLen1 = Math.Round(LengthResult[0], 2);
                PoiParam.DatumArea.LedLen2 = Math.Round(LengthResult[1], 2);
                PoiParam.DatumArea.LedLen3 = Math.Round(LengthResult[2], 2);
                PoiParam.DatumArea.LedLen4 = Math.Round(LengthResult[3], 2);
                if (calresult != 3)
                {
                    MessageBox.Show("灯珠抓取异常，请检查参数设置", "ColorVision");
                    return;
                }

                for (int i = 0; i < testdata; i++)
                {
                    DrawingVisualCircleWord Circle = new();
                    Circle.Attribute.Center = new Point(zuobiaoX[i], zuobiaoY[i]);
                    Circle.Attribute.Radius = banjin[i];
                    Circle.Attribute.Brush = Brushes.Transparent;
                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)banjin[i] / 30);
                    Circle.Attribute.ID = i + 1;
                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.ID);
                    Circle.Render();
                    ImageShow.AddVisual(Circle);
                }

                //for (int i = 0; i < 4; i++)
                //{
                //    //LAPoints[i].Y = PointY[i];
                //    dataGridViewLightArea.Rows[i].Cells[0].Value = LedLengthResult[i];
                //    //dataGridViewLightArea.Rows[i].Cells[1].Value = LAPoints[i].Y;
                //}
        }

        private void Import_Draw_Click(object sender, RoutedEventArgs e)
        {
            WindowFocusPointAdd windowFocusPointAd = new WindowFocusPointAdd(PoiParam) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            windowFocusPointAd.ShowDialog();
        }

        MeasureImgResultDao MeasureImgResultDao = new();
        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (MeasureImgResultDao.GetLatestResult() is MeasureImgResultModel measureImgResultModel)
            {
                try
                {
                    if (measureImgResultModel.FileUrl != null)
                    {
                        OpenImage(new NetFileUtil().OpenLocalCVFile(measureImgResultModel.FileUrl));
                    }
                    else
                    {
                        MessageBox.Show("打开最近服务拍摄的图像失败,找不到文件地址" );
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show("打开最近服务拍摄的图像失败",ex.Message);
                }
            }
            else
            {
                MessageBox.Show(this, "找不到刚拍摄的图像");
            }
        }


        public void OpenImage(CVCIEFile fileInfo)
        {
            if (fileInfo.FileExtType == FileExtType.Src) OpenImage(fileInfo.data);
            else if (fileInfo.FileExtType == FileExtType.Raw)
            {
                ShowImage(fileInfo);
            }
        }

        private void ShowImage(CVCIEFile fileInfo)
        {
            OpenCvSharp.Mat src = OpenCvSharp.Mat.FromPixelData(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
            OpenCvSharp.Mat dst = null;
            if (fileInfo.bpp == 32)
            {
                OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                dst = new OpenCvSharp.Mat();
                src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
            }
            else
            {
                dst = src;
            }
            SetImageSource(dst.ToBitmapSource());

        }



        public void OpenImage(byte[] data)
        {
            if (data != null)
            {
                var src = OpenCvSharp.Cv2.ImDecode(data, OpenCvSharp.ImreadModes.Unchanged);
                SetImageSource(src.ToBitmapSource());
            }
        }

        private ObservableCollection<MeasureImgResultModel> MeasureImgResultModels = new();
        private void Button_RefreshImg_Click(object sender, RoutedEventArgs e)
        {
            MeasureImgResultModels.Clear();
            var imgs = MeasureImgResultDao.GetAll();
            imgs.Reverse();
            foreach (var item in imgs)
            {
                if (!string.IsNullOrWhiteSpace(item.RawFile) &&!item.RawFile.Contains(".cvcie",StringComparison.OrdinalIgnoreCase))
                    MeasureImgResultModels.Add(item);
            }
            ComboBoxImg.ItemsSource = MeasureImgResultModels;
            ComboBoxImg.DisplayMemberPath = "RawFile";
        }

        private void Button_Service_Click(object sender, RoutedEventArgs e)
        {
            if (ComboBoxImg.SelectedIndex > -1)
            {
                try
                {
                    if (MeasureImgResultModels[ComboBoxImg.SelectedIndex] is MeasureImgResultModel model && model.FileUrl != null)
                    {
                        OpenImage(new NetFileUtil().OpenLocalCVFile(model.FileUrl, FileExtType.Raw));
                    }
                    else
                    {
                        MessageBox.Show("打开最近服务拍摄的图像失败");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开最近服务拍摄的图像失败", ex.Message);
                }
            }
           
        }

        private void ButtonImportMarin_Click(object sender, RoutedEventArgs e)
        {
            ImportMarinPopup.IsOpen = true;
        }

        private void ButtonImportMarinSetting(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is BitmapSource bitmapImage)
            {
                if (!double.TryParse(TextBoxUp1.Text, out double startU))
                    startU = 0;

                if (!double.TryParse(TextBoxDown1.Text, out double startD))
                    startD = 0;

                if (!double.TryParse(TextBoxLeft1.Text, out double startL))
                    startL = 0;
                if (!double.TryParse(TextBoxRight1.Text, out double startR))
                    startR = 0;

                if (ComboBoxBorderType1.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                {
                    startU = bitmapImage.PixelHeight * startU / 100;
                    startD = bitmapImage.PixelHeight * startD / 100;

                    startL = bitmapImage.PixelWidth * startL / 100;
                    startR = bitmapImage.PixelWidth * startR / 100;
                }
                PoiParam.DatumArea.Polygon1X = (int)startL;
                PoiParam.DatumArea.Polygon1Y = (int)startU;
                PoiParam.DatumArea.Polygon2X = bitmapImage.PixelWidth - (int)startR;
                PoiParam.DatumArea.Polygon2Y = (int)startU;
                PoiParam.DatumArea.Polygon3X = bitmapImage.PixelWidth - (int)startR;
                PoiParam.DatumArea.Polygon3Y = bitmapImage.PixelHeight - (int)startD;
                PoiParam.DatumArea.Polygon4X = (int)startR;
                PoiParam.DatumArea.Polygon4Y = bitmapImage.PixelHeight - (int)startD;
                
            }
            ImportMarinPopup.IsOpen =  false;

        }

        private void ButtonImportMarinSetting2(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is BitmapSource bitmapImage)
            {
                if (!double.TryParse(TextBoxUp2.Text, out double startU))
                    startU = 0;

                if (!double.TryParse(TextBoxDown2.Text, out double startD))
                    startD = 0;

                if (!double.TryParse(TextBoxLeft2.Text, out double startL))
                    startL = 0;
                if (!double.TryParse(TextBoxRight2.Text, out double startR))
                    startR = 0;

                if (ComboBoxBorderType11.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                {
                    startU = bitmapImage.PixelHeight * startU / 100;
                    startD = bitmapImage.PixelHeight * startD / 100;

                    startL = bitmapImage.PixelWidth * startL / 100;
                    startR = bitmapImage.PixelWidth * startR / 100;
                }

                PoiParam.DatumArea.AreaRectWidth = bitmapImage.PixelWidth - (int)startR - (int)startL;
                PoiParam.DatumArea.AreaRectHeight = bitmapImage.PixelHeight - (int)startD - (int)startD;

            }
            ImportMarinPopup1.IsOpen = false;
        }

        private void ButtonImportMarin1_Click(object sender, RoutedEventArgs e)
        {
            ImportMarinPopup1.IsOpen = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PolygonPoint polygonPoint)
            {
                PoiParam.DatumArea.Polygons.Remove(polygonPoint);
                RenderDatumArea();
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            foreach (var item in DrawingVisualLists)
            {
                item.BaseAttribute.Tag = PoiParam.DatumArea.DeafultValidateCIEId;
            }
        }

        private void ComboBoxValidateCIE_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = TemplateComplyParam.Params["Comply.CIE"];
            }
        }
    }

}
