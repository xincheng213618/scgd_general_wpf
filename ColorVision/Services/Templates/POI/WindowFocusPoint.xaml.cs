﻿using ColorVision.Draw;
using ColorVision.Common.Extension;
using ColorVision.Media;
using ColorVision.Net;
using ColorVision.Services.Templates.POI.Dao;
using ColorVision.Common.Utilities;
using cvColorVision;
using cvColorVision.Util;
using log4net;
using NPOI.SS.UserModel;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MQTTMessageLib.FileServer;
using ColorVision.Services.Dao;
using ColorVision.Settings;

namespace ColorVision.Services.Templates.POI
{

    /// <summary>
    /// WindowFocusPoint.xaml 的交互逻辑
    /// </summary>
    public partial class WindowFocusPoint : Window, INotifyPropertyChanged
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowFocusPoint));


        private string pre_name = "P_";

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        DatumArea DatumAreaPoints { get; set; } = new DatumArea();
        SoftwareConfig SoftwareConfig { get; set; }
        public WindowFocusPoint()
        {
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            PoiParam = new PoiParam();
            DatumAreaPoints = PoiParam.DatumArea;
            StackPanelDatumAreaPoints.DataContext = PoiParam.DatumArea;
            this.DataContext = PoiParam;
        }

        PoiParam PoiParam { get; set; }
        public WindowFocusPoint(PoiParam poiParam)
        {
            PoiParam = poiParam;
            DatumAreaPoints = PoiParam.DatumArea; 
            InitializeComponent();
            ListView1.ItemsSource = DrawingVisualLists;
            StackPanelDatumAreaPoints.DataContext = DatumAreaPoints;
            this.DataContext = PoiParam;

        }


        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


        public bool IsLayoutUpdated { get => _IsLayoutUpdated; set { _IsLayoutUpdated = value; NotifyPropertyChanged(); if (value) UpdateVisualLayout(value);  } }
        private bool _IsLayoutUpdated;
        private void Button_Click_UpdateVisualLayout(object sender, RoutedEventArgs e)
        {
            UpdateVisualLayout(true);
        }
        public ToolBarTop ToolBarTop { get; set; }

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



        private async void Window_Initialized(object sender, EventArgs e)
        {
            SettingGroup.DataContext = this;

            ComboBoxBorderType.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()
                                             select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType.SelectedIndex = 0;
            ComboBoxBorderType.SelectionChanged += (s, e) =>
            {
                if (ComboBoxBorderType.SelectedItem is KeyValuePair<string, BorderType> KeyValue && KeyValue.Value is BorderType communicateType)
                {

                }
            };
            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()
                                             select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()
                                              select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingPOIPosition)).Cast<DrawingPOIPosition>()
                                               select new KeyValuePair<DrawingPOIPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            WindowState = WindowState.Maximized;
            ImageContentGrid.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            ToolBarTop = new ToolBarTop(ImageContentGrid, Zoombox1, ImageShow);

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
                if (DrawingVisualLists.Count ==0)
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
                    UpdateVisualLayout(IsLayoutUpdated);
                }
            };
            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;
                await Task.Delay(100);

                if(SoftwareConfig.IsUseMySql)
                    TemplateControl.GetInstance().LoadPoiDetailFromDB(PoiParam);

                WaitControlProgressBar.Value = 10;

                if (PoiParam.PoiPoints.Count > 500)
                    IsLayoutUpdated = false;

                CreateImage(PoiParam.Width, PoiParam.Height, Colors.White,false);
                WaitControlProgressBar.Value = 20;
                DatumSet();
                RenderDatumArea();
                PoiParamToDrawingVisual(PoiParam);
                log.Debug("Render Poi end");
            }
            else
            {
                PoiParam.Width = 400;
                PoiParam.Height = 300;
            }

            this.Closed += (s, e) =>
            {
                if (ImageShow.Source == null)
                {
                    PoiParam.Width = 0;
                    PoiParam.Height = 0;
                }
                PoiParam.PoiPoints.Clear();
                foreach (var item in DrawingVisualLists)
                {
                    DrawBaseAttribute drawAttributeBase = item.BaseAttribute;
                    if (drawAttributeBase is CircleAttribute circle)
                    {
                        PoiParamData poiParamData = new PoiParamData()
                        {
                            ID =circle.ID,
                            Name = circle.Name,
                            PointType = RiPointTypes.Circle,
                            PixX = circle.Center.X,
                            PixY = circle.Center.Y,
                            PixWidth = circle.Radius*2,
                            PixHeight = circle.Radius*2
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

                if (SoftwareConfig.IsUseMySql)
                {
                    new PoiMasterDao().Save(new PoiMasterModel(PoiParam) { Name = PoiParam.PoiName });
                }
            };

            this.PreviewKeyDown += (s, e) =>
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
            };


        }
        private LedPicData ledPicData;
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif,*.cvraw) | *.jpg; *.jpeg; *.png;*.tif;*.cvraw";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;

                if (Path.GetExtension(filePath).Contains("cvraw"))
                {
                    try
                    {
                        OpenImage(new NetFileUtil("1").OpenLocalCVCIEFile(filePath, FileExtType.Raw));
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
                    CVCIEFileInfo fileInfo = new CVCIEFileInfo();
                    fileInfo.fileType = FileExtType.Raw;
                    int ret = CVFileUtils.ReadCVFile_Raw(filePath, ref fileInfo);
                    if (ret == 0)
                    {
                        OpenCvSharp.Mat src;
                        if (fileInfo.bpp != 8)
                        {
                            OpenCvSharp.Mat temp = new OpenCvSharp.Mat(fileInfo.height, fileInfo.width, OpenCvSharp.MatType.MakeType(fileInfo.depth, fileInfo.channels), fileInfo.data);
                            src = new OpenCvSharp.Mat();
                            temp.ConvertTo(src, OpenCvSharp.MatType.CV_8U, 1.0 / 256.0);
                            temp.Dispose();
                        }
                        else
                        {
                             src = new OpenCvSharp.Mat(fileInfo.height, fileInfo.width, OpenCvSharp.MatType.MakeType(fileInfo.depth, fileInfo.channels), fileInfo.data);
                        }

                        BitmapSource bitmapSource = src.ToBitmapSource();
                        SetImageSource(bitmapSource);
                    }
                }
                else
                {
                    BitmapSource bitmapImage = new BitmapImage(new Uri(filePath));
                    SetImageSource(bitmapImage);
                    Zoombox1.ZoomUniform();
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
        }

        private bool Init; 

        private void CreateImage(int width, int height, Color color,bool IsClear = true)
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
                        WaitControl.Visibility = Visibility.Hidden;
                        WaitControlProgressBar.Visibility = Visibility.Hidden;
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
                            DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.ID = item.ID;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                            Rectangle.Attribute.Rect = new Rect(item.PixX, item.PixY, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.ID = item.ID;
                            Rectangle.Attribute.Name = item.Name;
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
                    WaitControl.Visibility = Visibility.Hidden;
                    WaitControlProgressBar.Visibility = Visibility.Hidden;
                }
                Init = true;
            }
            catch
            {

            }

        }





        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

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
                List<Point> Points = new List<Point>()
                {
                    new Point(DatumAreaPoints.X1.X, DatumAreaPoints.X1.Y),
                    new Point(DatumAreaPoints.X2.X, DatumAreaPoints.X2.Y),
                    new Point(DatumAreaPoints.X3.X, DatumAreaPoints.X3.Y),
                    new Point(DatumAreaPoints.X4.X, DatumAreaPoints.X4.Y),
                    new Point(DatumAreaPoints.Center.X, DatumAreaPoints.Center.Y),
                };

                for (int i = 0; i < Points.Count; i++)
                {
                    DrawingVisualDatumCircle drawingVisual = new DrawingVisualDatumCircle();
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
                    drawing.BaseAttribute.IsShow = false;
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

        private static void DrawSelectRect(DrawingVisual drawingVisual, Rect rect)
        {
            using DrawingContext dc = drawingVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), rect);
        }
        private DrawingVisual SelectRect = new DrawingVisual();

        private bool IsMouseDown;
        private Point MouseDownP;

        private DrawingVisual? SelectDrawingVisual;
        private DrawingVisualPolygon DrawingPolygonCache;
        private DrawingVisualCircle DrawCircleCache;
        private DrawingVisualRectangle DrawingRectangleCache;


        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {

        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {

        }
        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PoiParam.DatumArea.IsUserDraw)
            {
                if (PoiParam.DatumArea.IsAreaPolygon && ToolBarTop.DrawPolygon)
                {
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    PoiParam.DatumArea.Polygons.Clear();
                    foreach (var item in DrawingPolygonCache.Attribute.Points)
                    {
                        PoiParam.DatumArea.Polygons.Add(item);
                    }
                    DrawingPolygonCache = null;
                    RenderDatumArea();
                }

                if (PoiParam.DatumArea.IsAreaMask && ToolBarTop.DrawPolygon)
                {
                    ImageShow.RemoveVisual(DrawingPolygonCache);
                    if (DrawingPolygonCache!=null && DrawingPolygonCache.Attribute.Points.Count == 4)
                    {
                        PoiParam.DatumArea.Polygon1X = (int)DrawingPolygonCache.Attribute.Points[0].X;
                        PoiParam.DatumArea.Polygon1Y = (int)DrawingPolygonCache.Attribute.Points[0].Y;
                        PoiParam.DatumArea.Polygon2X = (int)DrawingPolygonCache.Attribute.Points[1].X;
                        PoiParam.DatumArea.Polygon2Y = (int)DrawingPolygonCache.Attribute.Points[1].Y;
                        PoiParam.DatumArea.Polygon3X = (int)DrawingPolygonCache.Attribute.Points[2].X;
                        PoiParam.DatumArea.Polygon3Y = (int)DrawingPolygonCache.Attribute.Points[2].Y;
                        PoiParam.DatumArea.Polygon4X = (int)DrawingPolygonCache.Attribute.Points[3].X;
                        PoiParam.DatumArea.Polygon4Y = (int)DrawingPolygonCache.Attribute.Points[3].Y;

                        RenderDatumArea();
                    }
                    else
                    {
                        MessageBox.Show("必须要是4个点");
                    }
                    DrawingPolygonCache = null;

                }
            }

            if (DrawingPolygonCache != null)
            {
                DrawingPolygonCache.MovePoints = null;
                DrawingPolygonCache.Render();
                DrawingPolygonCache = null;
            }
        }

        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;
                drawCanvas.CaptureMouse();


                Brush brush = PoiParam.DatumArea.IsUserDraw ? Brushes.Blue : Brushes.Red;

                if (PoiParam.DatumArea.IsUserDraw)
                {
                    if (PoiParam.DatumArea.IsAreaCircle)
                        ToolBarTop.DrawCircle = true;
                    if (PoiParam.DatumArea.IsAreaRect)
                        ToolBarTop.DrawRect = true;
                    if (PoiParam.DatumArea.IsAreaPolygon)
                        ToolBarTop.DrawPolygon = true;
                    if (PoiParam.DatumArea.IsAreaMask)
                        ToolBarTop.DrawPolygon = true;
                }


                if (ToolBarTop.EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);
                }
                else if (ToolBarTop.DrawCircle)
                {
                    DrawCircleCache = new DrawingVisualCircle() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                    DrawCircleCache.IsDrawing = true;
                    drawCanvas.AddVisual(DrawCircleCache);

                }
                else if (ToolBarTop.DrawRect)
                {
                    DrawingRectangleCache = new DrawingVisualRectangle() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + 30, MouseDownP.Y + 30));
                    DrawingRectangleCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);

                    drawCanvas.AddVisual(DrawingRectangleCache);

                }
                else if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingPolygonCache == null)
                    {
                        DrawingPolygonCache = new DrawingVisualPolygon();
                        DrawingPolygonCache.Attribute.Pen = new Pen(brush, 1 / Zoombox1.ContentMatrix.M11);
                        drawCanvas.AddVisual(DrawingPolygonCache);
                    }
                }
                else if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                {
                    PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;

                    ListView1.ScrollIntoView(drawingVisual);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                    if (ToolBarTop.Activate == true)
                    {
                        if (drawingVisual is DrawingVisual visual)
                            SelectDrawingVisual = visual;

                         if (SelectDrawingVisual is DrawingVisualCircle Circl)
                        {
                            Circl.IsDrawing = true;
                        }     
                    }
                }
            }
        }
        Point LastMouseMove;


        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;

                if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingPolygonCache != null)
                    {
                        DrawingPolygonCache.MovePoints = point;
                        DrawingPolygonCache.Render();
                    }
                }

                if (IsMouseDown)
                {

                    if (ToolBarTop.EraseVisual)
                    {
                        DrawSelectRect(SelectRect, new Rect(MouseDownP, point)); ;
                    }
                    else if (ToolBarTop.DrawCircle)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ToolBarTop.DrawRect)
                    {
                        DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                        DrawingRectangleCache.Render();
                    }
                    else if (ToolBarTop.DrawPolygon)
                    {

                    }
                    else if (SelectDrawingVisual !=null)
                    {
                        if (SelectDrawingVisual is IRectangle rectangle)
                        {
                            var OldRect = rectangle.Rect;
                            rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                        }
                        else if (SelectDrawingVisual is ICircle Circl)
                        {
                            Circl.Center += point - LastMouseMove;
                        }
                    }
                }
                LastMouseMove = point;
            }
        }
        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                IsMouseDown = false;
                var MouseUpP = e.GetPosition(drawCanvas);



                if (PoiParam.DatumArea.IsUserDraw)
                {
                    if (PoiParam.DatumArea.IsAreaCircle && ToolBarTop.DrawCircle)
                    {
                        PoiParam.DatumArea.CenterX = (int)DrawCircleCache.Attribute.Center.X;
                        PoiParam.DatumArea.CenterY = (int)DrawCircleCache.Attribute.Center.Y;
                        PoiParam.DatumArea.AreaCircleRadius = (int)DrawCircleCache.Attribute.Radius;
                        drawCanvas.RemoveVisual(DrawCircleCache);
                    }

                    if (PoiParam.DatumArea.IsAreaRect && ToolBarTop.DrawRect)
                    {
                        PoiParam.DatumArea.CenterX = (int)(DrawingRectangleCache.Attribute.Rect.Width/2 + DrawingRectangleCache.Attribute.Rect.X);
                        PoiParam.DatumArea.CenterY = (int)(DrawingRectangleCache.Attribute.Rect.Height / 2 + DrawingRectangleCache.Attribute.Rect.Y);
                        PoiParam.DatumArea.AreaRectWidth = (int)DrawingRectangleCache.Attribute.Rect.Width;
                        PoiParam.DatumArea.AreaRectHeight = (int)DrawingRectangleCache.Attribute.Rect.Height;
                        drawCanvas.RemoveVisual(DrawingRectangleCache);
                    }
                    RenderDatumArea();
                }


                if (ToolBarTop.EraseVisual)
                {
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                    foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                    {
                        drawCanvas.RemoveVisual(item);
                    }
                    drawCanvas.RemoveVisual(SelectRect);
                }
                else if (ToolBarTop.DrawPolygon && DrawingPolygonCache != null)
                {
                    DrawingPolygonCache.Points.Add(MouseUpP);
                    DrawingPolygonCache.MovePoints = null;
                    DrawingPolygonCache.Render();
                }
                else if (ToolBarTop.DrawCircle && DrawCircleCache!=null)
                {
                    DrawCircleCache.IsDrawing = false;
                    DrawCircleCache.Render();

                    PropertyGrid2.SelectedObject = DrawCircleCache.BaseAttribute;

                    DrawCircleCache.AutoAttributeChanged = true;

                    ListView1.ScrollIntoView(DrawCircleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                }
                else if (ToolBarTop.DrawRect)
                {
                    DrawingRectangleCache.Render();


                    PropertyGrid2.SelectedObject = DrawingRectangleCache.BaseAttribute;
                    DrawingRectangleCache.AutoAttributeChanged = true;
                    ListView1.ScrollIntoView(DrawingRectangleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

                }
                drawCanvas.ReleaseMouseCapture();

                if (SelectDrawingVisual is DrawingVisualCircle circle)
                {
                    circle.IsDrawing = false;
                    circle.Render();
                }
                SelectDrawingVisual = null;


            }
        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {

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
                            IsLayoutUpdated = false;
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
    

                                    DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                                    Circle.Attribute.Center = new Point(x1, y1);
                                    Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                    Circle.Attribute.ID = start + i + 1;
                                    Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
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

                                    DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                    Rectangle.Attribute.Rect = new Rect(x1 - PoiParam.DatumArea.DefaultRectWidth / 2, y1 - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                    Rectangle.Attribute.ID = start + i + 1;
                                    Rectangle.Attribute.Name = string.Format("{0}{1}", pre_name, Rectangle.Attribute.ID);
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
                            IsLayoutUpdated = false;
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

                                        DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(x1, y1);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:

                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(x1 - (double)PoiParam.DatumArea.DefaultRectWidth / 2, y1 - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectWidth);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", pre_name, Rectangle.Attribute.ID);
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
                        List<Point> pts_src = new List<Point>();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);
                        pts_src.Add(PoiParam.DatumArea.Polygon4);


                        List<Point> points = SortPolyPoints(pts_src);

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

                                Point point = new Point(x, y);

                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:
                                        DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(point.X, point.Y);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i * cols + j + 1;
                                        Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(point.X - PoiParam.DatumArea.DefaultRectWidth / 2, point.Y - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", pre_name, Rectangle.Attribute.ID);
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

                        for (int i = 0; i < PoiParam.DatumArea.Polygons.Count - 1; i++)
                        {
                            double dx = (PoiParam.DatumArea.Polygons[i+1].X - PoiParam.DatumArea.Polygons[i].X) / (PoiParam.DatumArea.AreaPolygonLenNum +1);
                            double dy = (PoiParam.DatumArea.Polygons[i + 1].Y - PoiParam.DatumArea.Polygons[i].Y) / (PoiParam.DatumArea.AreaPolygonLenNum +1);

                            for (int j = 1; j < PoiParam.DatumArea.AreaPolygonLenNum +1 ; j++)
                            {

                                switch (PoiParam.DefaultPointType)
                                {
                                    case RiPointTypes.Circle:

                                        DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(PoiParam.DatumArea.Polygons[i].X + dx*j, PoiParam.DatumArea.Polygons[i].Y + dy * j);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i + 1;
                                        Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Polygons[i].X + dx * j - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.Polygons[i].Y + dy * j - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", pre_name, Rectangle.Attribute.ID);
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

                                        DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                                        Circle.Attribute.Center = new Point(PoiParam.DatumArea.Polygons[i].X, PoiParam.DatumArea.Polygons[i].Y);
                                        Circle.Attribute.Radius = PoiParam.DatumArea.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultCircleRadius / 30);
                                        Circle.Attribute.ID = start + i + 1;
                                        Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                        break;
                                    case RiPointTypes.Rect:
                                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Polygons[i].X - PoiParam.DatumArea.DefaultRectWidth / 2, PoiParam.DatumArea.Polygons[i].Y - PoiParam.DatumArea.DefaultRectHeight / 2, PoiParam.DatumArea.DefaultRectWidth, PoiParam.DatumArea.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.DatumArea.DefaultRectWidth / 30);
                                        Rectangle.Attribute.ID = start + i + 1;
                                        Rectangle.Attribute.Name = string.Format("{0}{1}", pre_name, Rectangle.Attribute.ID);
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


        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = new Regex("[^0-9]+").IsMatch(e.Text);
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
                        DrawingVisualDatumCircle Circle = new DrawingVisualDatumCircle();
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
                        DrawingVisualDatumRectangle Rectangle = new DrawingVisualDatumRectangle();
                        Rectangle.Attribute.Rect = new Rect(PoiParam.DatumArea.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiParam.DatumArea.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Mask:

                        List<Point> pts_src = new List<Point>();
                        pts_src.Add(PoiParam.DatumArea.Polygon1);
                        pts_src.Add(PoiParam.DatumArea.Polygon2);
                        pts_src.Add(PoiParam.DatumArea.Polygon3);  
                        pts_src.Add(PoiParam.DatumArea.Polygon4);


                        List<Point> result = SortPolyPoints(pts_src);
                        DrawingVisualDatumPolygon Polygon = new DrawingVisualDatumPolygon() { IsComple = true };
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
                        DrawingVisualDatumPolygon Polygon1 = new DrawingVisualDatumPolygon() { IsComple = false };
                        Polygon1.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon1.Attribute.Brush = Brushes.Transparent;
                        foreach (var item in PoiParam.DatumArea.Polygons)
                        {
                            Polygon1.Attribute.Points.Add(item);
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

        public static List<Point> SortPolyPoints(List<Point> vPoints)
        {
            if (vPoints == null || vPoints.Count == 0) return new List<Point>();
            //计算重心
            double X = 0, Y = 0;
            for (int i = 0; i < vPoints.Count; i++)
            {
                X += vPoints[i].X;
                Y += vPoints[i].Y;
            }
            Point center = new Point((int)X / vPoints.Count, (int)Y / vPoints.Count);
            //冒泡排序
            for (int i = 0; i < vPoints.Count - 1; i++)
            {
                for (int j = 0; j < vPoints.Count - i - 1; j++)
                {
                    if (PointCmp(vPoints[j], vPoints[j + 1], center))
                    {
                        (vPoints[j + 1], vPoints[j]) = (vPoints[j], vPoints[j + 1]);
                    }
                }
            }
            return vPoints;
        }

        private static bool PointCmp(Point a, Point b, Point center)
        {
            if (a.X >= 0 && b.X < 0)
                return true;
            else if (a.X == 0 && b.X == 0)
                return a.Y > b.Y;
            //向量OA和向量OB的叉积
            double det = (a.X - center.X) * (b.Y - center.Y) - (b.X - center.X) * (a.Y - center.Y);
            if (det < 0)
                return true;
            if (det > 0)
                return false;
            //向量OA和向量OB共线，以距离判断大小
            double d1 = (a.X - center.X) * (a.X - center.X) + (a.Y - center.Y) * (a.Y - center.Y);
            double d2 = (b.X - center.X) * (b.X - center.X) + (b.Y - center.Y) * (b.Y - center.Y);
            return d1 > d2;
        }

        private void Button_save_Click(object sender, RoutedEventArgs e)
        {
            if (SoftwareConfig.IsUseMySql)
            {
                PoiParam.PoiPoints.Clear();
                foreach (var item in DrawingVisualLists)
                {
                    DrawBaseAttribute drawAttributeBase = item.BaseAttribute;
                    if (drawAttributeBase is CircleAttribute circle)
                    {
                        if (circle is CircleTextAttribute circleTextAttribute)
                        {
                            PoiParamData poiParamData = new PoiParamData()
                            {
                                ID = circle.ID,
                                Name = circleTextAttribute.Text,
                                PointType = RiPointTypes.Circle,
                                PixX = circle.Center.X,
                                PixY = circle.Center.Y,
                                PixWidth = circle.Radius*2,
                                PixHeight = circle.Radius*2,
                            };
                            PoiParam.PoiPoints.Add(poiParamData);
                        }
                        else
                        {
                            PoiParamData poiParamData = new PoiParamData()
                            {
                                ID = circle.ID,
                                PointType = RiPointTypes.Circle,
                                PixX = circle.Center.X,
                                PixY = circle.Center.Y,
                                PixWidth = circle.Radius,
                                PixHeight = circle.Radius,
                            };
                            PoiParam.PoiPoints.Add(poiParamData);
                        }
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
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Collapsed;
                WaitControlText.Text = "数据正在保存";
                Thread thread = new Thread(() =>
                {
                    TemplateControl.GetInstance().Save2DB(PoiParam);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        WaitControl.Visibility = Visibility.Collapsed;
                    });
                    MessageBox.Show("保存成功", "ColorVision");
                });
                thread.Start();
            }
        }

        public LedCheckCfg ledCheckCfg { get; set; } = new LedCheckCfg();


        private void RadioButtonMode2_Checked(object sender, RoutedEventArgs e)
        {
            PropertyGridAutoFocus.SelectedObject = ledCheckCfg;
          
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            int[] dstPointX = new int[4] { 990, 4420, 4430, 1000 };
            int[] dstPointY = new int[4] { 980, 590, 2700, 3180 };
            float[] PointX = new float[4];
            float[] PointY = new float[4];

            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                byte[] data;
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    data = ms.ToArray();
                }
                int  num = cvCameraCSLib.FindBrightArea((uint)PoiParam.Width, (UInt32)PoiParam.Height,8,3, Array.Empty<byte>());

            }


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

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private WindowStatus OldWindowStatus { get; set; }

        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var window = GetWindow(ImageContentGrid);

                if (toggleButton.IsChecked == true)
                {
                    if (ImageContentGrid.Parent is Grid p)
                    {
                        OldWindowStatus = new WindowStatus();
                        OldWindowStatus.Parent = p;
                        OldWindowStatus.WindowState = window.WindowState;
                        OldWindowStatus.WindowStyle = window.WindowStyle;
                        OldWindowStatus.ResizeMode = window.ResizeMode;
                        OldWindowStatus.Root = window.Content;
                        window.WindowStyle = WindowStyle.None;
                        window.WindowState = WindowState.Maximized;
                        OldWindowStatus.Parent.Children.Remove(ImageContentGrid);
                        window.Content = ImageContentGrid;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {

                    window.WindowStyle = OldWindowStatus.WindowStyle;
                    window.WindowState = OldWindowStatus.WindowState;
                    window.ResizeMode = OldWindowStatus.ResizeMode;

                    window.Content = OldWindowStatus.Root;
                    OldWindowStatus.Parent.Children.Add(ImageContentGrid);
                }
            }
        }


        private void Button_Click_4(object sender, RoutedEventArgs e)
        {

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

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {

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
                    DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                    Circle.Attribute.Center = new Point(zuobiaoX[i], zuobiaoY[i]);
                    Circle.Attribute.Radius = banjin[i];
                    Circle.Attribute.Brush = Brushes.Transparent;
                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)banjin[i] / 30);
                    Circle.Attribute.ID = i + 1;
                    Circle.Attribute.Text = string.Format("{0}{1}", pre_name, Circle.Attribute.ID);
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
            var ListConfigs = new ObservableCollection<TemplateModelBase>();
            foreach (var item in TemplateControl.GetInstance().PoiParams)
            {
                if (item.Value != PoiParam)
                {
                    TemplateModel<PoiParam> listConfig = new TemplateModel<PoiParam>(item.Key, item.Value);
                    ListConfigs.Add(listConfig);
                }
            }
            WindowFocusPointAdd windowFocusPointAd = new WindowFocusPointAdd(ListConfigs) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            windowFocusPointAd.Closed += (s, e) =>
            {
                if (windowFocusPointAd.SelectPoiParam != null)
                {
                    var SelectPoiParam = windowFocusPointAd.SelectPoiParam;

                    if (SoftwareConfig.IsUseMySql)
                        TemplateControl.GetInstance().LoadPoiDetailFromDB(SelectPoiParam);

                    foreach (var item in SelectPoiParam.PoiPoints)
                    {
                        PoiParam.PoiPoints.Add(item);
                    }
                    MessageBox.Show("导入成功", "ColorVision");
                }
            };
            windowFocusPointAd.ShowDialog();
        }

        MeasureImgResultDao MeasureImgResultDao = new MeasureImgResultDao();
        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (MeasureImgResultDao.GetLatestResult() is MeasureImgResultModel measureImgResultModel)
            {
                try
                {
                    OpenImage(new NetFileUtil("1").OpenLocalCVCIEFile(measureImgResultModel.FileUrl, FileExtType.Raw));
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


        public void OpenImage(CVCIEFileInfo fileInfo)
        {
            if (fileInfo.fileType == FileExtType.Src) OpenImage(fileInfo.data);
            else if (fileInfo.fileType == FileExtType.Raw)
            {
                ShowImage(fileInfo);
            }
        }

        private void ShowImage(CVCIEFileInfo fileInfo)
        {
            OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileInfo.height, fileInfo.width, OpenCvSharp.MatType.MakeType(fileInfo.depth, fileInfo.channels), fileInfo.data);
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

        private ObservableCollection<MeasureImgResultModel> MeasureImgResultModels = new ObservableCollection<MeasureImgResultModel>();
        private void Button_RefreshImg_Click(object sender, RoutedEventArgs e)
        {
            MeasureImgResultModels.Clear();
            var imgs = MeasureImgResultDao.GetAll();
            imgs.Reverse();
            foreach (var item in imgs)
            {
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
                    OpenImage(new NetFileUtil("1").OpenLocalCVCIEFile(MeasureImgResultModels[ComboBoxImg.SelectedIndex].FileUrl, FileExtType.Raw));
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

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }


    }

}