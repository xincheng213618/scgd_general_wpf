#pragma warning disable CS8625
using ColorVision.Common.Collections;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Draw;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIGenCali;
using ColorVision.Engine.Services.Templates.POI.ListViewAdorners;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingPOIPosition = ColorVision.Engine.Templates.POI.DrawingPOIPosition;

namespace ColorVision.Engine.Services.Templates.POI
{
    public partial class EditPoiParam : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EditPoiParam));
        private string TagName { get; set; } = "P_";

        public PoiParam PoiParam { get; set; }

        public EditPoiParam(PoiParam poiParam) 
        {
            PoiParam = poiParam;
            InitializeComponent();
            this.ApplyCaption();

            Task.Run(() => Test());
        }

        public async void Test()
        {
            await Task.Delay(100);

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    ToolBarTop?.ClearImage();
                    writeableBitmap = null;
                }));
            });
        }
        

        public BulkObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new BulkObservableCollection<IDrawingVisual>();
        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = PoiParam;

            ListView1.ItemsSource = DrawingVisualLists;
            ListViewDragDropManager<IDrawingVisual> listViewDragDropManager = new ListViewAdorners.ListViewDragDropManager<IDrawingVisual>(ListView1);
            listViewDragDropManager.EventHandler += (s, e) =>
            {
                if (!DBIndex.ContainsKey(e[0]))
                    DBIndex.Add(e[0], -1);
                if (!DBIndex.ContainsKey(e[1]))
                    DBIndex.Add(e[1], -1);

                int old = DBIndex[e[0]];
                DBIndex[e[0]] = DBIndex[e[1]];

                e[0].BaseAttribute.Name = DBIndex[e[1]].ToString();
                DBIndex[e[1]] = old;
                e[1].BaseAttribute.Name = old.ToString();
            };

            ComboBoxBorderType.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>() select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType.SelectedIndex = 0;

            ComboBoxValidate.ItemsSource = TemplateComplyParam.Params.GetValue("Comply.CIE.AVG")?.CreateEmpty();
            ComboBoxValidateCIE.ItemsSource = TemplateComplyParam.Params.GetValue("Comply.CIE")?.CreateEmpty();

            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()  select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>() select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingPOIPosition)).Cast<DrawingPOIPosition>() select new KeyValuePair<DrawingPOIPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            ToolBarTop = new ToolBarTop(ImageContentGrid, Zoombox1, ImageShow);
            ToolBarTop.ToolBarScaleRuler.IsShow = false;
            ToolBar1.DataContext = ToolBarTop;
            ToolBarTop.EditModeChanged += (s, e) =>
            {
                if (e.IsEditMode)
                {
                    PoiParam.PoiConfig.IsShowDatum = false;
                    PoiParam.PoiConfig.IsShowPoiConfig = false;
                    RenderPoiConfig();
                }
            };

            ImageShow.VisualsAdd += (s, e) =>
            {
                if (!PoiParam.PoiConfig.IsShowText)
                {
                    if (s is IDrawingVisual visual)
                    {
                        DrawingVisualLists.Add(visual);
                    }
                }
                else
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

                }


            };
            if (PoiParam.PoiConfig.IsShowText)
            {
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
            }
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
                    UpdateVisualLayout(PoiParam.PoiConfig.IsLayoutUpdated);
                }
            };

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;
                if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
                    PoiParam.LoadPoiDetailFromDB(PoiParam);


                WaitControlProgressBar.Value = 10;

                if (PoiParam.PoiPoints.Count > 500)
                    PoiParam.PoiConfig.IsLayoutUpdated = false;

                CreateImage(PoiParam.Width, PoiParam.Height, Colors.White, false);
                WaitControlProgressBar.Value = 20;
                RenderPoiConfig();
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
                if (item is DVDatumCircle visualDatumCircle)
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
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif,*.tiff,*.cvraw,*.cvcie) | *.jpg; *.jpeg; *.png;*.tif;*.tiff;*.cvraw;*.cvcie";
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

        private void CreateImage_Click(object sender, RoutedEventArgs e)
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
                    BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                    SetImageSource(bitmapImage.ToWriteableBitmap());
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
                InitPoiConfigValue(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
            }
            ImageShow.ImageInitialize();
            Zoombox1.ZoomUniform();
        }

        private bool Init;
        public static WriteableBitmap CreateWhiteLayer(int width, int height)
        {
            // 创建 WriteableBitmap
            var writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            // 计算每行的字节数
            int bytesPerPixel = (writeableBitmap.Format.BitsPerPixel + 7) / 8;
            int stride = width * bytesPerPixel;
            byte[] pixels = new byte[height * stride];

            // 填充白色
            for (int i = 0; i < pixels.Length; i += bytesPerPixel)
            {
                pixels[i] = 255;     // Blue
                pixels[i + 1] = 255; // Green
                pixels[i + 2] = 255; // Red
                pixels[i + 3] = 255; // Alpha
            }

            // 写入像素数据
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            return writeableBitmap;
        }
        WriteableBitmap writeableBitmap;
        private void CreateImage(int width, int height, Color color,bool IsClear = true)
        {
            Thread thread = new(() => 
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    writeableBitmap = CreateWhiteLayer(width, height);
                    if (ImageShow.Source == null)
                    {
                        ImageShow.Source = writeableBitmap;
                        Zoombox1.ZoomUniform();
                        if (IsClear|| !Init)
                            InitPoiConfigValue((int)writeableBitmap.Width,(int)writeableBitmap.Height);
                    }
                    else
                    {
                        if (ImageShow.Source is BitmapSource img && (img.PixelWidth != writeableBitmap.PixelWidth || img.PixelHeight != writeableBitmap.PixelHeight))
                        {
                            InitPoiConfigValue((int)writeableBitmap.Width, (int)writeableBitmap.Height);
                            ImageShow.Source = writeableBitmap;
                            Zoombox1.ZoomUniform();
                        }
                        else
                        {
                            ImageShow.Source = writeableBitmap;
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


        private void InitPoiConfigValue(int width,int height)
        {
            Application.Current.Dispatcher.Invoke(() => PoiParam.PoiConfig.IsShowPoiConfig = true);
            RenderPoiConfig();
            DatumSet();
        }

        private Dictionary<IDrawingVisual, int> DBIndex = new Dictionary<IDrawingVisual, int>();

        private int No;

        private async void PoiParamToDrawingVisual(PoiParam poiParam)
        {
            try
            {
                if (poiParam.PoiConfig.IsPoiCIEFile)
                {
                    Init = true;
                    return;
                }

                DrawingVisualLists.SuspendUpdate();
                int WaitNum = 50;
                if (!PoiParam.PoiConfig.IsShowText)
                    WaitNum = 1000;
                foreach (var item in poiParam.PoiPoints)
                {
                    No++;
                    if (No % WaitNum == 0)
                    {
                        WaitControlProgressBar.Value = 20 + No * 79 / poiParam.PoiPoints.Count;
                        await Task.Delay(10);
                    }
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.IsShowText = PoiParam.PoiConfig.IsShowText;
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.Id = No;
                            Circle.Attribute.Text = item.Name;

                            Circle.Attribute.Name = item.Id.ToString();
                            Circle.Attribute.Tag = item.Tag;
                            Circle.Render();
                            ImageShow.AddVisual(Circle);
                            DBIndex.Add(Circle,item.Id);
                            break;
                        case RiPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.IsShowText = PoiParam.PoiConfig.IsShowText;
                            Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth /2, item.PixY - item.PixHeight /2, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = No;
                            Rectangle.Attribute.Text = item.Name;
                            Rectangle.Attribute.Name = item.Id.ToString();

                            Rectangle.Attribute.Tag = item.Tag;
                            Rectangle.Render();
                            ImageShow.AddVisual(Rectangle);
                            DBIndex.Add(Rectangle, item.Id);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
                }
                WaitControlProgressBar.Value = 99;
                if (DrawingVisualLists.Count <= 1000000)
                {
                    DrawingVisualLists.ResumeUpdate();
                }

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

                    PoiParam.PoiConfig.X1X = (int)startL;
                    PoiParam.PoiConfig.X1Y = (int)startU;
                    PoiParam.PoiConfig.X2X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.PoiConfig.X2Y = (int)startU;
                    PoiParam.PoiConfig.X3X = bitmapImage.PixelWidth - (int)startR;
                    PoiParam.PoiConfig.X3Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.PoiConfig.X4X = (int)startR;
                    PoiParam.PoiConfig.X4Y = bitmapImage.PixelHeight - (int)startD;
                    PoiParam.PoiConfig.CenterX = (int)bitmapImage.PixelWidth / 2;
                    PoiParam.PoiConfig.CenterY = bitmapImage.PixelHeight / 2;
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
            if (PoiParam.PoiConfig.IsShowDatum)
            {
                List<Point> Points = new()
                {
                    new Point(PoiParam.PoiConfig.X1X, PoiParam.PoiConfig.X1Y),
                    new Point(PoiParam.PoiConfig.X2X, PoiParam.PoiConfig.X2Y),
                    new Point(PoiParam.PoiConfig.X3X, PoiParam.PoiConfig.X3Y),
                    new Point(PoiParam.PoiConfig.X4X, PoiParam.PoiConfig.X4Y),
                    new Point(PoiParam.PoiConfig.CenterX, PoiParam.PoiConfig.CenterY),
                };

                for (int i = 0; i < Points.Count; i++)
                {
                    DVDatumCircle drawingVisual = new();
                    drawingVisual.Attribute.Center = Points[i];
                    drawingVisual.Attribute.Radius = 5 / Zoombox1.ContentMatrix.M11;
                    drawingVisual.Attribute.Brush = Brushes.Blue;
                    drawingVisual.Attribute.Pen = new Pen(Brushes.Blue, 2);
                    drawingVisual.Attribute.Id = i + 1;
                    drawingVisual.Render();
                    DefaultPoint.Add(drawingVisual);
                    ImageShow.AddVisual(drawingVisual);
                }
            }
        }


        private async void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapImage) return;

            int Num = 0;
            int start = DrawingVisualLists.Count;

            switch (PoiParam.PoiConfig.PointType)
            {
                case RiPointTypes.Circle:
                    if (PoiParam.PoiConfig.AreaCircleNum < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                        return;
                    }

                    if (PoiParam.PoiConfig.AreaCircleNum > 1000)
                    {
                        WaitControl.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Value = 0;
                        PoiParam.PoiConfig.IsLayoutUpdated = false;
                    }


                    for (int i = 0; i < PoiParam.PoiConfig.AreaCircleNum; i++)
                    {
                        Num++;
                        if (Num % 100 == 0 && WaitControl.Visibility == Visibility.Visible)
                        {
                            WaitControlProgressBar.Value = Num * 1000 / PoiParam.PoiConfig.AreaCircleNum;
                            await Task.Delay(1);
                        }

                        double x1 = PoiParam.PoiConfig.CenterX + PoiParam.PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                        double y1 = PoiParam.PoiConfig.CenterY + PoiParam.PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);

                        switch (PoiParam.DefaultPointType)
                        {
                            case RiPointTypes.Circle:

                                if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition)
                                {
                                    switch (pOIPosition)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            x1 = PoiParam.PoiConfig.CenterX + PoiParam.PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + PoiParam.PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            x1 = PoiParam.PoiConfig.CenterX + (PoiParam.PoiConfig.AreaCircleRadius - PoiParam.PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + (PoiParam.PoiConfig.AreaCircleRadius - PoiParam.PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.External:
                                            x1 = PoiParam.PoiConfig.CenterX + (PoiParam.PoiConfig.AreaCircleRadius + PoiParam.PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + (PoiParam.PoiConfig.AreaCircleRadius + PoiParam.PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }


                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(x1, y1);
                                Circle.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultCircleRadius / 30);
                                Circle.Attribute.Id = start + i + 1;
                                Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                Circle.Render();
                                ImageShow.AddVisual(Circle);
                                break;
                            case RiPointTypes.Rect:

                                if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition2)
                                {
                                    switch (pOIPosition2)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            x1 = PoiParam.PoiConfig.CenterX + PoiParam.PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + PoiParam.PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            x1 = PoiParam.PoiConfig.CenterX + (PoiParam.PoiConfig.AreaCircleRadius - PoiParam.PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + (PoiParam.PoiConfig.AreaCircleRadius - PoiParam.PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.External:
                                            x1 = PoiParam.PoiConfig.CenterX + (PoiParam.PoiConfig.AreaCircleRadius + PoiParam.PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            y1 = PoiParam.PoiConfig.CenterY + (PoiParam.PoiConfig.AreaCircleRadius + PoiParam.PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiParam.PoiConfig.AreaCircleNum + Math.PI / 180 * PoiParam.PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new Rect(x1 - PoiParam.PoiConfig.DefaultRectWidth / 2, y1 - PoiParam.PoiConfig.DefaultRectHeight / 2, PoiParam.PoiConfig.DefaultRectWidth, PoiParam.PoiConfig.DefaultRectHeight);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultRectWidth / 30);
                                Rectangle.Attribute.Id = start + i + 1;
                                Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
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

                    int cols = PoiParam.PoiConfig.AreaRectCol;
                    int rows = PoiParam.PoiConfig.AreaRectRow;

                    if (rows < 1 || cols < 1)
                    {
                        MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                        return;
                    }
                    double Width = PoiParam.PoiConfig.AreaRectWidth;
                    double Height = PoiParam.PoiConfig.AreaRectHeight;


                    double startU = PoiParam.PoiConfig.CenterY - Height / 2;
                    double startD = bitmapImage.PixelHeight - PoiParam.PoiConfig.CenterY - Height / 2;
                    double startL = PoiParam.PoiConfig.CenterX - Width / 2;
                    double startR = bitmapImage.PixelWidth - PoiParam.PoiConfig.CenterX - Width / 2;

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
                                        startU += PoiParam.PoiConfig.DefaultCircleRadius;
                                        startD += PoiParam.PoiConfig.DefaultCircleRadius;
                                        startL += PoiParam.PoiConfig.DefaultCircleRadius;
                                        startR += PoiParam.PoiConfig.DefaultCircleRadius;
                                        break;
                                    case DrawingPOIPosition.External:
                                        startU -= PoiParam.PoiConfig.DefaultCircleRadius;
                                        startD -= PoiParam.PoiConfig.DefaultCircleRadius;
                                        startL -= PoiParam.PoiConfig.DefaultCircleRadius;
                                        startR -= PoiParam.PoiConfig.DefaultCircleRadius;
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
                                        startU += PoiParam.PoiConfig.DefaultRectWidth / 2;
                                        startD += PoiParam.PoiConfig.DefaultRectWidth / 2;
                                        startL += PoiParam.PoiConfig.DefaultRectHeight / 2;
                                        startR += PoiParam.PoiConfig.DefaultRectHeight / 2;
                                        break;
                                    case DrawingPOIPosition.External:
                                        startU -= PoiParam.PoiConfig.DefaultRectWidth / 2;
                                        startD -= PoiParam.PoiConfig.DefaultRectWidth / 2;
                                        startL -= PoiParam.PoiConfig.DefaultRectHeight / 2;
                                        startR -= PoiParam.PoiConfig.DefaultRectHeight / 2;
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
                        DrawingVisualLists.SuspendUpdate();
                        WaitControl.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Value = 0;
                        PoiParam.PoiConfig.IsLayoutUpdated = false;
                    }

                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                    {
                        WaitControl.Visibility = Visibility.Visible;
                        PoiParam.PoiPoints.Clear();
                    }



                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            Num++;
                            if (Num % 10000 == 0 && WaitControl.Visibility == Visibility.Visible)
                            {
                                WaitControlProgressBar.Value = Num * 10000 / all;
                                await Task.Delay(1);
                            }

                            double x1 = startL + StepCol * j;
                            double y1 = startU + StepRow * i;

                            switch (PoiParam.DefaultPointType)
                            {
                                case RiPointTypes.Circle:
                                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = x1, PixY = y1, PixWidth = PoiParam.PoiConfig.DefaultCircleRadius, PixHeight = PoiParam.PoiConfig.DefaultCircleRadius });
                                    }
                                    else
                                    {
                                        DVCircleText Circle = new();
                                        Circle.IsShowText = PoiParam.PoiConfig.IsShowText;
                                        Circle.Attribute.Center = new Point(x1, y1);
                                        Circle.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultCircleRadius / 30);
                                        Circle.Attribute.Id = start + i * cols + j + 1;
                                        Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                        Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                    }
                                    break;
                                case RiPointTypes.Rect:
                                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = x1, PixY = y1, PointType = RiPointTypes.Rect, PixWidth = PoiParam.PoiConfig.DefaultRectWidth, PixHeight = PoiParam.PoiConfig.DefaultRectHeight });
                                    }
                                    else
                                    {
                                        DVRectangleText Rectangle = new();
                                        Rectangle.IsShowText = PoiParam.PoiConfig.IsShowText;
                                        Rectangle.Attribute.Rect = new Rect(x1 - (double)PoiParam.PoiConfig.DefaultRectWidth / 2, y1 - PoiParam.PoiConfig.DefaultRectWidth / 2, PoiParam.PoiConfig.DefaultRectWidth, PoiParam.PoiConfig.DefaultRectWidth);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultRectWidth / 30);
                                        Rectangle.Attribute.Id = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                        Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                    }
                                    break;
                                case RiPointTypes.Mask:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                    {
                        Thread thread = new(() =>
                        {
                            WaitControlText.Text = "关注点强制启用文件保存";
                            SaveAsFile();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                WaitControlText.Text = "正在绘制关注点";
                                int[] ints = new int[PoiParam.PoiPoints.Count * 2];
                                for (int i = 0; i < PoiParam.PoiPoints.Count; i++)
                                {
                                    ints[2 * i] = (int)PoiParam.PoiPoints[i].PixX;
                                    ints[2 * i + 1] = (int)PoiParam.PoiPoints[i].PixY;
                                }
                                HImage hImage;
                                if (ImageShow.Source is WriteableBitmap writeable)
                                {
                                    hImage = writeable.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiParam.PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiParam.PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();

                                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                            hImageProcessed.pData = IntPtr.Zero;
                                            ImageShow.Source = image;
                                        }
                                    });
                                }

                                if (ImageShow.Source is BitmapImage bitmapSource)
                                {
                                    hImage = bitmapSource.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiParam.PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiParam.PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();
                                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                            hImageProcessed.pData = IntPtr.Zero;
                                            ImageShow.Source = image;
                                            WaitControl.Visibility = Visibility.Collapsed;
                                        }
                                    });
                                }
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    WaitControl.Visibility = Visibility.Collapsed;
                                });
                            });



                        });
                        thread.Start();
                    }

                    if (all <= 1000000)
                    {
                        DrawingVisualLists.ResumeUpdate();
                    }

                    break;
                case RiPointTypes.Mask:
                    List<Point> pts_src =
                    [
                        PoiParam.PoiConfig.Polygon1,
                        PoiParam.PoiConfig.Polygon2,
                        PoiParam.PoiConfig.Polygon3,
                        PoiParam.PoiConfig.Polygon4,
                    ];

                    List<Point> points = Helpers.SortPolyPoints(pts_src);

                    cols = PoiParam.PoiConfig.AreaPolygonCol;
                    rows = PoiParam.PoiConfig.AreaPolygonRow;

                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                    {
                        PoiParam.PoiPoints.Clear();
                    }


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
                                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = point.X, PixY = point.Y, PixWidth = PoiParam.PoiConfig.DefaultCircleRadius, PixHeight = PoiParam.PoiConfig.DefaultCircleRadius });
                                    }
                                    else
                                    {
                                        DVCircleText Circle = new();
                                        Circle.Attribute.Center = new Point(point.X, point.Y);
                                        Circle.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultCircleRadius / 30);
                                        Circle.Attribute.Id = start + i * cols + j + 1;
                                        Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                        Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                        Circle.Render();
                                        ImageShow.AddVisual(Circle);
                                    }
                                    break;
                                case RiPointTypes.Rect:
                                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = point.X, PixY = point.Y, PointType = RiPointTypes.Rect, PixWidth = PoiParam.PoiConfig.DefaultRectWidth, PixHeight = PoiParam.PoiConfig.DefaultRectHeight });
                                    }
                                    else
                                    {
                                        DVRectangleText Rectangle = new();
                                        Rectangle.Attribute.Rect = new Rect(point.X - PoiParam.PoiConfig.DefaultRectWidth / 2, point.Y - PoiParam.PoiConfig.DefaultRectHeight / 2, PoiParam.PoiConfig.DefaultRectWidth, PoiParam.PoiConfig.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultRectWidth / 30);
                                        Rectangle.Attribute.Id = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                        Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                        Rectangle.Render();
                                        ImageShow.AddVisual(Rectangle);
                                    }

                                    break;
                                case RiPointTypes.Mask:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (PoiParam.PoiConfig.IsPoiCIEFile)
                    {

                        Thread thread = new(() =>
                        {
                            log.Info("正在保存关注点");

                            log.Info("正在保存成csv文件");
                            SaveAsFile();

                            int[] ints = new int[PoiParam.PoiPoints.Count * 2];
                            for (int i = 0; i < PoiParam.PoiPoints.Count; i++)
                            {
                                ints[2 * i] = (int)PoiParam.PoiPoints[i].PixX;
                                ints[2 * i + 1] = (int)PoiParam.PoiPoints[i].PixY;
                            }
                            HImage hImage;
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (ImageShow.Source is BitmapImage bitmapSource)
                                {
                                    hImage = bitmapSource.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiParam.PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiParam.PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();

                                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                            hImageProcessed.pData = IntPtr.Zero;
                                            ImageShow.Source = image;

                                        }
                                    });
                                }

                                if (ImageShow.Source is WriteableBitmap writeable)
                                {
                                    hImage = writeable.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiParam.PoiConfig.DefaultCircleRadius, ints, ints.Length , PoiParam.PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (!HImageExtension.UpdateWriteableBitmap(ImageShow.Source, hImageProcessed))
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();
                                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                            hImageProcessed.pData = IntPtr.Zero;
                                            ImageShow.Source = image;
                                        }
                                    });
                                }
                            });

                        });
                        thread.Start();
                    }
                    break;

                case RiPointTypes.Polygon:

                    int No = 0;
                    for (int i = 0; i < PoiParam.PoiConfig.Polygons.Count - 1; i++)
                    {
                        double dx = (PoiParam.PoiConfig.Polygons[i + 1].X - PoiParam.PoiConfig.Polygons[i].X) / (PoiParam.PoiConfig.Polygons[i].SplitNumber + 1);
                        double dy = (PoiParam.PoiConfig.Polygons[i + 1].Y - PoiParam.PoiConfig.Polygons[i].Y) / (PoiParam.PoiConfig.Polygons[i].SplitNumber + 1);

                        for (int j = 1; j < PoiParam.PoiConfig.Polygons[i].SplitNumber + 1; j++)
                        {
                            No++;
                            switch (PoiParam.DefaultPointType)
                            {
                                case RiPointTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiParam.PoiConfig.Polygons[i].X + dx * j, PoiParam.PoiConfig.Polygons[i].Y + dy * j);
                                    Circle.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + No;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new Rect(PoiParam.PoiConfig.Polygons[i].X + dx * j - PoiParam.PoiConfig.DefaultRectWidth / 2, PoiParam.PoiConfig.Polygons[i].Y + dy * j - PoiParam.PoiConfig.DefaultRectHeight / 2, PoiParam.PoiConfig.DefaultRectWidth, PoiParam.PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + No;
                                    Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                    Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                    Rectangle.Render();
                                    ImageShow.AddVisual(Rectangle);
                                    break;
                                default:
                                    break;
                            }
                        }


                    }

                    for (int i = 0; i < PoiParam.PoiConfig.Polygons.Count; i++)
                    {
                        if (PoiParam.PoiConfig.AreaPolygonUsNode)
                        {
                            switch (PoiParam.DefaultPointType)
                            {
                                case RiPointTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiParam.PoiConfig.Polygons[i].X, PoiParam.PoiConfig.Polygons[i].Y);
                                    Circle.Attribute.Radius = PoiParam.PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + i + 1;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);

                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new Rect(PoiParam.PoiConfig.Polygons[i].X - PoiParam.PoiConfig.DefaultRectWidth / 2, PoiParam.PoiConfig.Polygons[i].Y - PoiParam.PoiConfig.DefaultRectHeight / 2, PoiParam.PoiConfig.DefaultRectWidth, PoiParam.PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiParam.PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i + 1;
                                    Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                    Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
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
            if (PoiParam.PoiConfig.IsShowText)
            {
                UpdateVisualLayout(true);
                ScrollViewer1.ScrollToEnd();
            }
            //这里我不推荐添加
            if (WaitControl.Visibility == Visibility.Visible)
            {
                WaitControl.Visibility = Visibility.Collapsed;
                WaitControlProgressBar.Visibility = Visibility.Collapsed;
                WaitControlProgressBar.Value = 0;
            }
        }


        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("清空关注点", "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            ClearRender();
        }

        public void ClearRender()
        {
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
        private void ShowPoiConfig_Click(object sender, RoutedEventArgs e)
        {
            RenderPoiConfig();
        }

        private void RadioButtonArea_Checked(object sender, RoutedEventArgs e)
        {
            RenderPoiConfig();
        }

        private void RenderPoiConfig()
        {
            if (drawingVisualDatum != null)
            {
                ImageShow.RemoveVisual(drawingVisualDatum);
            }
            if (PoiParam.PoiConfig.IsShowPoiConfig)
            {
                switch (PoiParam.PoiConfig.PointType)
                {
                    case RiPointTypes.Circle:
                        DVDatumCircle Circle = new();
                        Circle.Attribute.Center = PoiParam.PoiConfig.Center;
                        Circle.Attribute.Radius = PoiParam.PoiConfig.AreaCircleRadius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Render();
                        drawingVisualDatum = Circle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Rect:
                        double Width = PoiParam.PoiConfig.AreaRectWidth;
                        double Height = PoiParam.PoiConfig.AreaRectHeight;
                        DVDatumRectangle Rectangle = new();
                        Rectangle.Attribute.Rect = new Rect(PoiParam.PoiConfig.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiParam.PoiConfig.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Mask:

                        List<Point> pts_src = new();
                        pts_src.Add(PoiParam.PoiConfig.Polygon1);
                        pts_src.Add(PoiParam.PoiConfig.Polygon2);
                        pts_src.Add(PoiParam.PoiConfig.Polygon3);  
                        pts_src.Add(PoiParam.PoiConfig.Polygon4);

                        List<Point> result = Helpers.SortPolyPoints(pts_src);
                        DVDatumPolygon Polygon = new() { IsComple = true };
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
                        DVDatumPolygon Polygon1 = new() { IsComple = false };
                        Polygon1.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon1.Attribute.Brush = Brushes.Transparent;
                        foreach (var item in PoiParam.PoiConfig.Polygons)
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
                int index = DBIndex.TryGetValue(item, out int value) ? value : -1;

                BaseProperties drawAttributeBase = item.BaseAttribute;
                if (drawAttributeBase is CircleTextProperties circle)
                {
                    PoiPoint poiParamData = new PoiPoint()
                    {
                        Id = index,
                        PointType = RiPointTypes.Circle,
                        PixX = circle.Center.X,
                        PixY = circle.Center.Y,
                        PixWidth = circle.Radius * 2,
                        PixHeight = circle.Radius * 2,
                        Tag = circle.Tag,
                        Name = circle.Text
                    };


                    PoiParam.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is RectangleTextProperties rectangle)
                {
                    PoiPoint poiParamData = new()
                    {
                        Id = index,
                        Name = rectangle.Text,
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


        private void PoiConfigImport_Click(object sender, RoutedEventArgs e)
        {
            PoiParam.PoiConfig.Polygon1X = PoiParam.PoiConfig.X1X;
            PoiParam.PoiConfig.Polygon1Y = PoiParam.PoiConfig.X1Y;
            PoiParam.PoiConfig.Polygon2X = PoiParam.PoiConfig.X2X;
            PoiParam.PoiConfig.Polygon2Y = PoiParam.PoiConfig.X2Y;
            PoiParam.PoiConfig.Polygon3X = PoiParam.PoiConfig.X3X;
            PoiParam.PoiConfig.Polygon3Y = PoiParam.PoiConfig.X3Y;
            PoiParam.PoiConfig.Polygon4X = PoiParam.PoiConfig.X4X;
            PoiParam.PoiConfig.Polygon4Y = PoiParam.PoiConfig.X4Y;
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

                PoiParam.PoiConfig.LedLen1 = Math.Round(LengthResult[0], 2);
                PoiParam.PoiConfig.LedLen2 = Math.Round(LengthResult[1], 2);
                PoiParam.PoiConfig.LedLen3 = Math.Round(LengthResult[2], 2);
                PoiParam.PoiConfig.LedLen4 = Math.Round(LengthResult[3], 2);
                if (calresult != 3)
                {
                    MessageBox.Show("灯珠抓取异常，请检查参数设置", "ColorVision");
                    return;
                }

                for (int i = 0; i < testdata; i++)
                {
                    DVCircleText Circle = new();
                    Circle.Attribute.Center = new Point(zuobiaoX[i], zuobiaoY[i]);
                    Circle.Attribute.Radius = banjin[i];
                    Circle.Attribute.Brush = Brushes.Transparent;
                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)banjin[i] / 30);
                    Circle.Attribute.Id = i + 1;
                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);
                    Circle.Render();
                    ImageShow.AddVisual(Circle);
                }

        }

        private void Import_Draw_Click(object sender, RoutedEventArgs e)
        {
            EditPoiParamAdd windowFocusPointAd = new EditPoiParamAdd(PoiParam) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            windowFocusPointAd.Closed += (s, e) =>
            {
                if(windowFocusPointAd.IsSucess)
                    PoiParamToDrawingVisual(PoiParam);
            };
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
            if (fileInfo.FileExtType == FileExtType.Src)
            {
                if (fileInfo.data != null)
                {
                    var src = OpenCvSharp.Cv2.ImDecode(fileInfo.data, OpenCvSharp.ImreadModes.Unchanged);
                    SetImageSource(src.ToBitmapSource());
                }
            }
            else if (fileInfo.FileExtType == FileExtType.Raw)
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
                double startU = ParseDoubleOrDefault(TextBoxUp1.Text);
                double startD = ParseDoubleOrDefault(TextBoxDown1.Text);
                double startL = ParseDoubleOrDefault(TextBoxLeft1.Text);
                double startR = ParseDoubleOrDefault(TextBoxRight1.Text);

                if (ComboBoxBorderType1.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                {
                    startU = bitmapImage.PixelHeight * startU / 100;
                    startD = bitmapImage.PixelHeight * startD / 100;

                    startL = bitmapImage.PixelWidth * startL / 100;
                    startR = bitmapImage.PixelWidth * startR / 100;
                }
                PoiParam.PoiConfig.Polygon1X = (int)startL;
                PoiParam.PoiConfig.Polygon1Y = (int)startU;
                PoiParam.PoiConfig.Polygon2X = bitmapImage.PixelWidth - (int)startR;
                PoiParam.PoiConfig.Polygon2Y = (int)startU;
                PoiParam.PoiConfig.Polygon3X = bitmapImage.PixelWidth - (int)startR;
                PoiParam.PoiConfig.Polygon3Y = bitmapImage.PixelHeight - (int)startD;
                PoiParam.PoiConfig.Polygon4X = (int)startR;
                PoiParam.PoiConfig.Polygon4Y = bitmapImage.PixelHeight - (int)startD;
                
            }
            ImportMarinPopup.IsOpen =  false;

        }

        private static double ParseDoubleOrDefault(string input, double defaultValue = 0) => double.TryParse(input, out double result) ? result : defaultValue;

        private void ButtonImportMarinSetting2(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is BitmapSource bitmapImage)
            {
                double startU = ParseDoubleOrDefault(TextBoxUp2.Text);
                double startD = ParseDoubleOrDefault(TextBoxDown2.Text);
                double startL = ParseDoubleOrDefault(TextBoxLeft2.Text);
                double startR = ParseDoubleOrDefault(TextBoxRight2.Text);

                if (ComboBoxBorderType11.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
                {
                    startU = bitmapImage.PixelHeight * startU / 100;
                    startD = bitmapImage.PixelHeight * startD / 100;

                    startL = bitmapImage.PixelWidth * startL / 100;
                    startR = bitmapImage.PixelWidth * startR / 100;
                }

                PoiParam.PoiConfig.AreaRectWidth = bitmapImage.PixelWidth - (int)startR - (int)startL;
                PoiParam.PoiConfig.AreaRectHeight = bitmapImage.PixelHeight - (int)startD - (int)startD;
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
                PoiParam.PoiConfig.Polygons.Remove(polygonPoint);
                RenderPoiConfig();
            }
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            foreach (var item in DrawingVisualLists)
            {
                item.BaseAttribute.Tag = PoiParam.PoiConfig.DeafultValidateCIEId;
            }
        }

        private void ComboBoxValidateCIE_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = TemplateComplyParam.Params.GetValue("Comply.CIE");
            }
        }

        private void ReadFile_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(PoiParam.PoiConfig.PoiCIEFileName))
            {
                ClearRender();
                ViewHandleBuildPoiFile.CovertPoiParam(PoiParam, PoiParam.PoiConfig.PoiCIEFileName);
                PoiParamToDrawingVisual(PoiParam);
            }
        }

        public void SaveAsFile()
        {
            if (File.Exists(PoiParam.PoiConfig.PoiCIEFileName))
            {
                ViewHandleBuildPoiFile.CoverFile(PoiParam, PoiParam.PoiConfig.PoiCIEFileName);
            }
            else
            {
                using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                {
                    saveFileDialog.Filter = "csv Files (*.csv)|*.csv";
                    saveFileDialog.Title = "Save File";
                    saveFileDialog.FileName = "PoiCIE.csv";
                    saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        PoiParam.PoiConfig.PoiCIEFileName = saveFileDialog.FileName;
                        ViewHandleBuildPoiFile.CoverFile(PoiParam, PoiParam.PoiConfig.PoiCIEFileName);
                    }
                }
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SaveAsFile();
        }

        private void PoiFix_Create_Click(object sender, RoutedEventArgs e)
        {

            if (!File.Exists(PoiParam.PoiConfig.PoiFixFilePath))
            {
                using System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.Filter = "csv Files (*.csv)|*.csv";
                saveFileDialog.Title = "Save File";
                saveFileDialog.FileName = "PoiFix.csv";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PoiParam.PoiConfig.PoiFixFilePath = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            using (StreamWriter writer = new StreamWriter(PoiParam.PoiConfig.PoiFixFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("Id,Name,PixX,PixY,PixWidth,PixHeight,GenCalibrationType,M,N,P");
                foreach (var item in PoiParam.PoiPoints)
                {
                    writer.WriteLine($"{item.Id},{item.Name},{item.PixX},{item.PixY},{item.PixWidth},{item.PixHeight},{GenCalibrationType.BrightnessAndChroma},1,1,1");
                }
            };
        }


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            FocusPointGrid.Height = FocusPointRowDefinition.ActualHeight;
            PropertyGrid21.Height = FocusPointRowDefinition.ActualHeight;
        }
    }

}
