#pragma warning disable CS8625,CS8604,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Sorts;
using ColorVision.Util.Draw.Rectangle;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace ColorVision.Engine.Templates.POI
{
    public class EditPoiParamConfig : Common.MVVM.ViewModelBase, IConfig
    {
        public static EditPoiParamConfig Instance => ConfigService.Instance.GetRequiredService<EditPoiParamConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    public partial class EditPoiParam : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EditPoiParam));
        private string TagName { get; set; } = "P_";
        public PoiParam PoiParam { get; set; }
        public PoiConfig PoiConfig => PoiParam.PoiConfig;

        public EditPoiParam(PoiParam poiParam) 
        {
            PoiParam = poiParam;
            InitializeComponent();
            this.ApplyCaption();

            this.DelayClearImage((Action)(() => Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                ImageViewModel?.Dispose();
            }))));
            this.Title = poiParam.Name + "-" + this.Title;
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => ImageViewModel.DrawingVisualLists;

        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

        public Zoombox Zoombox1 => ImageView.Zoombox1;

        public DrawCanvas ImageShow => ImageView.ImageShow;
        public ImageViewModel ImageViewModel => ImageView.ImageViewModel;

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = PoiParam;
            ListView1.ItemsSource = DrawingVisualLists;
            ListView1.ContextMenu = new ContextMenu();
            ImageView.ImageViewModel.ImageEditMode = true;
            ImageView.ImageViewModel.SelectEditorVisual.SelectVisualChanged += (s, e) =>
            {
                ListView1.SelectedItem = e;
                ListView1.ScrollIntoView(e);
            };

            MoveUpCommand = new RelayCommand(a => MoveUp(), a => ListView1?.SelectedIndex > 0); // 假设ListView1是ViewModel中的属性或可以通过绑定访问
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => ListView1?.SelectedIndex < DrawingVisualLists.Count - 1);
            MoveToTopCommand = new RelayCommand(a => MoveToTop(), a => ListView1?.SelectedIndex > 0);
            MoveToBottomCommand = new RelayCommand(a => MoveToBottom(), a => ListView1?.SelectedIndex < DrawingVisualLists.Count - 1);

            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(GraphicBorderType)).Cast<GraphicBorderType>()  select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(GraphicBorderType)).Cast<GraphicBorderType>() select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingGraphicPosition)).Cast<DrawingGraphicPosition>() select new KeyValuePair<DrawingGraphicPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            ImageView.ImageShow.ImageInitialized += (s, e) =>
            {
                PoiParam.Width = (int)ImageView.ImageShow.Source.Width;
                PoiParam.Height = (int)ImageView.ImageShow.Source.Height;
            };

            if (PoiParam.Height != 0 && PoiParam.Width != 0)
            {
                if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
                    PoiParam.LoadPoiDetailFromDB(PoiParam);

                if (PoiParam.PoiPoints.Count > 500)
                    PoiConfig.IsLayoutUpdated = false;


                if (File.Exists(PoiConfig.BackgroundFilePath))
                {
                    ImageView.OpenImage(PoiConfig.BackgroundFilePath);
                }
                else
                    CreateImage(PoiParam.Width, PoiParam.Height, Colors.White, false);

                RenderPoiConfig();
                PoiParamToDrawingVisual(PoiParam);
                log.Debug("Render Poi end");
            }
            else
            {
                PoiParam.Width = 400;
                PoiParam.Height = 300;
            }
            PreviewKeyDown += (s, e) =>
            {
                if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key ==Key.S)
                {
                    SavePoiParam();
                }
            };

            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                EditPoiParamConfig.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                EditPoiParamConfig.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            ImageView.ImageShow.VisualsAdd += (s, e) =>
            {
                if (PoiConfig.IsUserDraw)
                {
                    PoiConfig.IsUserDraw = false;
                    if (e.Visual is DVCircleText dVCircleText)
                    {

                        dVCircleText.Attribute.PropertyChanged += (s, e) =>
                        {
                            PoiConfig.CenterX = (int)dVCircleText.Attribute.Center.X;
                            PoiConfig.CenterY = (int)dVCircleText.Attribute.Center.Y;
                            PoiConfig.AreaCircleRadius = (int)dVCircleText.Attribute.Radius;
                            RenderPoiConfig();
                        };
                    }
                    if (e.Visual is DVRectangleText dVRectangleText)
                    {
                        dVRectangleText.Attribute.PropertyChanged += (s, e) =>
                        {
                            PoiConfig.CenterX = (int)(dVRectangleText.Attribute.Rect.Width / 2 + dVRectangleText.Attribute.Rect.X);
                            PoiConfig.CenterY = (int)(dVRectangleText.Attribute.Rect.Height / 2 + dVRectangleText.Attribute.Rect.Y);
                            PoiConfig.AreaRectWidth = (int)dVRectangleText.Attribute.Rect.Width;
                            PoiConfig.AreaRectHeight = (int)dVRectangleText.Attribute.Rect.Height;
                            RenderPoiConfig();
                        };
                    }
                }
            };
        }

        private ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                foreach (var item in GridViewColumnVisibilitys)
                {
                    if (item.ColumnName.ToString() == gridViewColumnHeader.Content.ToString())
                    {
                        string Name = item.ColumnName.ToString();
                        if (Name == Properties.Resources.SerialNumber1)
                        {
                            item.IsSortD = !item.IsSortD;
                            DrawingVisualLists.Sort((x, y) => item.IsSortD ? y.BaseAttribute.Id.CompareTo(x.BaseAttribute.Id) : x.BaseAttribute.Id.CompareTo(y.BaseAttribute.Id));
                        }
                    }
                }
            }
        }



        private void Button_UpdateVisualLayout_Click(object sender, RoutedEventArgs e)
        {
            UpdateVisualLayout();
        }
        private void UpdateVisualLayout()
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
        }


        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif,*.tiff,*.cvraw,*.cvcie) | *.jpg; *.jpeg; *.png;*.tif;*.tiff;*.cvraw;*.cvcie";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                ImageView.OpenImage(filePath);
                PoiConfig.BackgroundFilePath = filePath;

            }
        }

        private void CreateImage_Click(object sender, RoutedEventArgs e)
        {
            CreateImage(PoiParam.Width, PoiParam.Height, Colors.White,false);

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
        private void CreateImage(int width, int height, Color color,bool IsClear = true)
        {

            Thread thread = new(() => 
            {
                Application.Current.Dispatcher.Invoke((Action)(() =>
                {
                     ImageView.SetImageSource(CreateWhiteLayer(width, height));
                     ImageView.UpdateZoomAndScale();
                     InitPoiConfigValue((int)ImageView.ViewBitmapSource.Width, (int)ImageView.ViewBitmapSource.Height);
                    if (IsClear)
                    {
                        ImageShow.Clear();
                        DrawingVisualLists.Clear();
                    }
                    ImageShow.RaiseImageInitialized();

                }));
            });
            thread.Start();
            PoiConfig.BackgroundFilePath = null;
        }


        private void InitPoiConfigValue(int width,int height)
        {
            Application.Current.Dispatcher.Invoke(() => PoiConfig.IsShowPoiConfig = true);
            RenderPoiConfig();
        }

        private Dictionary<IDrawingVisual, int> DBIndex = new Dictionary<IDrawingVisual, int>();

        private int No;

        private async void PoiParamToDrawingVisual(PoiParam poiParam)
        {
            try
            {
                if (PoiConfig.IsPoiCIEFile)
                {
                    Init = true;
                    return;
                }

                foreach (var item in poiParam.PoiPoints)
                {
                    No++;
                    switch (item.PointType)
                    {
                        case GraphicTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixWidth/2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.Id = No;
                            Circle.Attribute.Text = item.Name;

                            Circle.Attribute.Name = item.Id.ToString();

                            Circle.Render();
                            ImageShow.AddVisualCommand(Circle);
                            DBIndex.Add(Circle,item.Id);
                            break;
                        case GraphicTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new System.Windows.Rect(item.PixX - item.PixWidth /2, item.PixY - item.PixHeight /2, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = No;
                            Rectangle.Attribute.Text = item.Name;
                            Rectangle.Attribute.Name = item.Id.ToString();

                            Rectangle.Render();
                            ImageShow.AddVisualCommand(Rectangle);
                            DBIndex.Add(Rectangle, item.Id);
                            break;
                        case GraphicTypes.Quadrilateral:
                            break;
                        case GraphicTypes.Point:
                            if (item.Name == "PointInt1")
                            {
                                PoiConfig.PointInt1.X = (int)item.PixX;
                                PoiConfig.PointInt1.Y = (int)item.PixY;
                            }
                            if (item.Name == "PointInt2")
                            {
                                PoiConfig.PointInt2.X = (int)item.PixX;
                                PoiConfig.PointInt2.Y = (int)item.PixY;
                            }
                            if (item.Name == "PointInt3")
                            {
                                PoiConfig.PointInt3.X = (int)item.PixX;
                                PoiConfig.PointInt3.Y = (int)item.PixY;
                            }
                            if (item.Name == "PointInt4")
                            {
                                PoiConfig.PointInt4.X = (int)item.PixX;
                                PoiConfig.PointInt4.Y = (int)item.PixY;
                            }
                            break;
                    }
                }
                ImageShow.ClearActionCommand();
                Init = true;
            }
            catch
            {

            }
        }



        private async void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapImage) return;

            int Num = 0;
            int start = DrawingVisualLists.Count;

            switch (PoiConfig.PointType)
            {
                case GraphicTypes.Circle:
                    if (PoiConfig.AreaCircleNum < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                        return;
                    }




                    for (int i = 0; i < PoiConfig.AreaCircleNum; i++)
                    {
                        Num++;

                        double x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                        double y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);

                        switch (PoiConfig.DefaultPointType)
                        {
                            case GraphicTypes.Circle:

                                if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition)
                                {
                                    switch (pOIPosition)
                                    {
                                        case DrawingGraphicPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.External:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }


                                DVCircleText Circle = new();
                                Circle.Attribute.Center = new Point(x1, y1);
                                Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                Circle.Attribute.Brush = Brushes.Transparent;
                                Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                Circle.Attribute.Id = start + i + 1;
                                Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                Circle.Render();
                                ImageShow.AddVisualCommand(Circle);
                                break;
                            case GraphicTypes.Rect:

                                if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition2)
                                {
                                    switch (pOIPosition2)
                                    {
                                        case DrawingGraphicPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingGraphicPosition.External:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius + PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                DVRectangleText Rectangle = new();
                                Rectangle.Attribute.Rect = new System.Windows.Rect(x1 - PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                Rectangle.Attribute.Brush = Brushes.Transparent;
                                Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                Rectangle.Attribute.Id = start + i + 1;
                                Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                Rectangle.Render();
                                ImageShow.AddVisualCommand(Rectangle);
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                            default:
                                break;
                        }
                    }
                    break;
                case GraphicTypes.Rect:

                    int cols = PoiConfig.AreaRectCol;
                    int rows = PoiConfig.AreaRectRow;

                    if (rows < 1 || cols < 1)
                    {
                        MessageBox.Show("点阵数的行列不能小于1", "ColorVision");
                        return;
                    }
                    double Width = PoiConfig.AreaRectWidth;
                    double Height = PoiConfig.AreaRectHeight;


                    double startU = PoiConfig.CenterY - Height / 2;
                    double startD = bitmapImage.PixelHeight - PoiConfig.CenterY - Height / 2;
                    double startL = PoiConfig.CenterX - Width / 2;
                    double startR = bitmapImage.PixelWidth - PoiConfig.CenterX - Width / 2;

                    if (ComboBoxBorderType2.SelectedValue is DrawingGraphicPosition pOIPosition1)
                    {
                        switch (PoiConfig.DefaultPointType)
                        {
                            case GraphicTypes.Circle:
                                switch (pOIPosition1)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        startU += PoiConfig.DefaultCircleRadius;
                                        startD += PoiConfig.DefaultCircleRadius;
                                        startL += PoiConfig.DefaultCircleRadius;
                                        startR += PoiConfig.DefaultCircleRadius;
                                        break;
                                    case DrawingGraphicPosition.External:
                                        startU -= PoiConfig.DefaultCircleRadius;
                                        startD -= PoiConfig.DefaultCircleRadius;
                                        startL -= PoiConfig.DefaultCircleRadius;
                                        startR -= PoiConfig.DefaultCircleRadius;
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case GraphicTypes.Rect:
                                switch (pOIPosition1)
                                {
                                    case DrawingGraphicPosition.LineOn:
                                        break;
                                    case DrawingGraphicPosition.Internal:
                                        startU += PoiConfig.DefaultRectHeight / 2;
                                        startD += PoiConfig.DefaultRectHeight / 2;
                                        startL += PoiConfig.DefaultRectWidth / 2;
                                        startR += PoiConfig.DefaultRectWidth / 2;
                                        break;
                                    case DrawingGraphicPosition.External:
                                        startU -= PoiConfig.DefaultRectHeight / 2;
                                        startD -= PoiConfig.DefaultRectHeight / 2;
                                        startL -= PoiConfig.DefaultRectWidth / 2;
                                        startR -= PoiConfig.DefaultRectWidth / 2;
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            case GraphicTypes.Quadrilateral:
                                break;
                            default:
                                break;
                        }
                    }


                    double StepRow = (rows > 1) ? (bitmapImage.PixelHeight - startD - startU) / (rows - 1) : 0;
                    double StepCol = (cols > 1) ? (bitmapImage.PixelWidth - startL - startR) / (cols - 1) : 0;


                    int all = rows * cols;


                    if (PoiConfig.IsPoiCIEFile)
                    {
                        PoiParam.PoiPoints.Clear();
                    }

                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < cols; j++)
                        {
                            Num++;


                            double x1 = startL + StepCol * j;
                            double y1 = startU + StepRow * i;

                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    if (PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = x1, PixY = y1, PixWidth = PoiConfig.DefaultCircleRadius, PixHeight = PoiConfig.DefaultCircleRadius });
                                    }
                                    else
                                    {
                                        DVCircleText Circle = new();
                                        Circle.Attribute.Center = new Point(x1, y1);
                                        Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                        Circle.Attribute.Id = start + i * cols + j + 1;
                                        Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                        Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                        Circle.Render();
                                        ImageShow.AddVisualCommand(Circle);
                                    }
                                    break;
                                case GraphicTypes.Rect:
                                    if (PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = x1, PixY = y1, PointType = GraphicTypes.Rect, PixWidth = PoiConfig.DefaultRectWidth, PixHeight = PoiConfig.DefaultRectHeight });
                                    }
                                    else
                                    {
                                        DVRectangleText Rectangle = new();
                                        Rectangle.Attribute.Rect = new System.Windows.Rect(x1 - (double)PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                        Rectangle.Attribute.Id = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                        Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                        Rectangle.Render();
                                        ImageShow.AddVisualCommand(Rectangle);
                                    }
                                    break;
                                case GraphicTypes.Quadrilateral:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    if (PoiConfig.IsPoiCIEFile)
                    {
                        Thread thread = new(() =>
                        {
                            SaveAsFile();

                            Application.Current.Dispatcher.Invoke(() =>
                            {
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
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();

                                            hImageProcessed.Dispose();

                                            ImageShow.Source = image;
                                        }
                                    });
                                }

                                else if (ImageShow.Source is BitmapImage bitmapSource)
                                {
                                    hImage = bitmapSource.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();
                                            hImageProcessed.Dispose();

                                            ImageShow.Source = image;
                                        }
                                    });
                                }
  
                            });



                        });
                        thread.Start();
                    }


                    break;
                case GraphicTypes.Quadrilateral:
                    List<Point> pts_src =
                    [
                        PoiConfig.Polygon1,
                        PoiConfig.Polygon2,
                        PoiConfig.Polygon3,
                        PoiConfig.Polygon4,
                    ];

                    List<Point> points = Helpers.SortPolyPoints(pts_src);

                    cols = PoiConfig.AreaPolygonCol;
                    rows = PoiConfig.AreaPolygonRow;

                    if (PoiConfig.IsPoiCIEFile)
                    {
                        PoiParam.PoiPoints.Clear();
                    }

                    double rowStep = (rows > 1) ? 1.0 / (rows - 1) : 0;
                    double columnStep = (rows > 1) ? 1.0 / (cols - 1) : 0;
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

                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:
                                    if (PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = point.X, PixY = point.Y, PixWidth = PoiConfig.DefaultCircleRadius, PixHeight = PoiConfig.DefaultCircleRadius });
                                    }
                                    else
                                    {
                                        DVCircleText Circle = new();
                                        Circle.Attribute.Center = new Point(point.X, point.Y);
                                        Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                        Circle.Attribute.Brush = Brushes.Transparent;
                                        Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                        Circle.Attribute.Id = start + i * cols + j + 1;
                                        Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                        Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                        Circle.Render();
                                        ImageShow.AddVisualCommand(Circle);
                                    }
                                    break;
                                case GraphicTypes.Rect:
                                    if (PoiConfig.IsPoiCIEFile)
                                    {
                                        PoiParam.PoiPoints.Add(new PoiPoint() { PixX = point.X, PixY = point.Y, PointType = GraphicTypes.Rect, PixWidth = PoiConfig.DefaultRectWidth, PixHeight = PoiConfig.DefaultRectHeight });
                                    }
                                    else
                                    {
                                        DVRectangleText Rectangle = new();
                                        Rectangle.Attribute.Rect = new System.Windows.Rect(point.X - PoiConfig.DefaultRectWidth / 2, point.Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                        Rectangle.Attribute.Brush = Brushes.Transparent;
                                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                        Rectangle.Attribute.Id = start + i * cols + j + 1;
                                        Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                        Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                        Rectangle.Render();
                                        ImageShow.AddVisualCommand(Rectangle);
                                    }

                                    break;
                                case GraphicTypes.Quadrilateral:
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    if (PoiConfig.IsPoiCIEFile)
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
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiConfig.DefaultCircleRadius, ints, ints.Length, PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (ret == 0)
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();

                                            hImageProcessed.Dispose();

                                            ImageShow.Source = image;

                                        }
                                    });
                                }

                                if (ImageShow.Source is WriteableBitmap writeable)
                                {
                                    hImage = writeable.ToHImage();
                                    int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, PoiConfig.DefaultCircleRadius, ints, ints.Length , PoiConfig.Thickness);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (!HImageExtension.UpdateWriteableBitmap(ImageShow.Source, hImageProcessed))
                                        {
                                            var image = hImageProcessed.ToWriteableBitmap();
                                            hImageProcessed.Dispose();

                                            ImageShow.Source = image;
                                        }
                                    });
                                }
                            });

                        });
                        thread.Start();
                    }
                    break;

                case GraphicTypes.Polygon:

                    int No = 0;
                    for (int i = 0; i < PoiConfig.Polygons.Count - 1; i++)
                    {
                        double dx = (PoiConfig.Polygons[i + 1].X - PoiConfig.Polygons[i].X) / (PoiConfig.Polygons[i].SplitNumber + 1);
                        double dy = (PoiConfig.Polygons[i + 1].Y - PoiConfig.Polygons[i].Y) / (PoiConfig.Polygons[i].SplitNumber + 1);

                        for (int j = 1; j < PoiConfig.Polygons[i].SplitNumber + 1; j++)
                        {
                            No++;
                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiConfig.Polygons[i].X + dx * j, PoiConfig.Polygons[i].Y + dy * j);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + No;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);
                                    Circle.Render();
                                    ImageShow.AddVisualCommand(Circle);
                                    break;
                                case GraphicTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Polygons[i].X + dx * j - PoiConfig.DefaultRectWidth / 2, PoiConfig.Polygons[i].Y + dy * j - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + No;
                                    Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                    Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                    Rectangle.Render();
                                    ImageShow.AddVisualCommand(Rectangle);
                                    break;
                                default:
                                    break;
                            }
                        }


                    }

                    for (int i = 0; i < PoiConfig.Polygons.Count; i++)
                    {
                        if (PoiConfig.AreaPolygonUsNode)
                        {
                            switch (PoiConfig.DefaultPointType)
                            {
                                case GraphicTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiConfig.Polygons[i].X, PoiConfig.Polygons[i].Y);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + i + 1;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);

                                    Circle.Render();
                                    ImageShow.AddVisualCommand(Circle);
                                    break;
                                case GraphicTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Polygons[i].X - PoiConfig.DefaultRectWidth / 2, PoiConfig.Polygons[i].Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i + 1;
                                    Rectangle.Attribute.Name = Rectangle.Attribute.Id.ToString();
                                    Rectangle.Attribute.Text = string.Format("{0}{1}", TagName, Rectangle.Attribute.Name);
                                    Rectangle.Render();
                                    ImageShow.AddVisualCommand(Rectangle);
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
        }


        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("清空关注点", "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
            ClearRender();
            //清空关注点的时候重置计数
            No = 0;
        }

        public void ClearRender()
        {
            foreach (var item in DrawingVisualLists.ToList())
                if (item is Visual visual)
                    ImageShow.RemoveVisualCommand(visual);
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[listView.SelectedIndex] is ISelectVisual drawingVisua)
            {
                ImageView.ImageViewModel.SelectEditorVisual.SetRender(drawingVisua);
            }
        }

        private void MenuItem_DrawingVisual_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Visual visual &&visual is IDrawingVisual drawing)
            {
                ImageShow.RemoveVisualCommand(visual);
                DrawingVisualLists.Remove(drawing);
            }
        }

        private void ListView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                // Check if the focused element is a TextBox
                if (Keyboard.FocusedElement is TextBox)
                {
                    // Let the TextBox handle the Delete key for editing
                    return;
                }

                if (sender is ListView listView && listView.SelectedItems.Count > 0)
                {
                    var visualsToRemove = new List<Visual>();

                    foreach (var selectedItem in listView.SelectedItems)
                    {
                        int index = listView.Items.IndexOf(selectedItem);
                        if (index >= 0 && DrawingVisualLists[index] is Visual visual)
                        {
                            visualsToRemove.Add(visual);
                        }
                    }
                    foreach (var visual in visualsToRemove)
                    {
                        ImageShow.RemoveVisualCommand(visual);
                    }
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
                ImageShow.RemoveVisualCommand(drawingVisualDatum);
            }
            if (PoiConfig.IsShowPoiConfig)
            {
                switch (PoiConfig.PointType)
                {
                    case GraphicTypes.Circle:
                        DVDatumCircle Circle = new();
                        Circle.Attribute.Center = PoiConfig.Center;
                        Circle.Attribute.Radius = PoiConfig.AreaCircleRadius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Render();
                        drawingVisualDatum = Circle;
                        ImageShow.AddVisualCommand(drawingVisualDatum);
                        break;
                    case GraphicTypes.Rect:
                        double Width = PoiConfig.AreaRectWidth;
                        double Height = PoiConfig.AreaRectHeight;
                        DVDatumRectangle Rectangle = new();
                        Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiConfig.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisualCommand(drawingVisualDatum);
                        break;
                    case GraphicTypes.Quadrilateral:

                        List<Point> pts_src = new();
                        pts_src.Add(PoiConfig.Polygon1);
                        pts_src.Add(PoiConfig.Polygon2);
                        pts_src.Add(PoiConfig.Polygon3);  
                        pts_src.Add(PoiConfig.Polygon4);

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
                        ImageShow.AddVisualCommand(drawingVisualDatum);
                        break;
                    case GraphicTypes.Polygon:
                        DVDatumPolygon Polygon1 = new() { IsComple = false };
                        Polygon1.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon1.Attribute.Brush = Brushes.Transparent;
                        foreach (var item in PoiConfig.Polygons)
                        {
                            Polygon1.Attribute.Points.Add(new Point(item.X, item.Y));
                        }
                        Polygon1.Render();
                        drawingVisualDatum = Polygon1;
                        ImageShow.AddVisualCommand(drawingVisualDatum);

                        break;
                    default:
                        break;
                }

            }
        }

        private void SavePoiParam()
        {
            PoiParam.PoiPoints.Clear();
            Rect rect = new Rect(0,0, PoiParam.Width, PoiParam.Height);
            foreach (var item in DrawingVisualLists)
            {
                int index = DBIndex.TryGetValue(item, out int value) ? value : 0;

                BaseProperties drawAttributeBase = item.BaseAttribute;
                if (drawAttributeBase is CircleTextProperties circle)
                {
                    Rect rect1 = new Rect(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius, circle.Radius * 2, circle.Radius * 2);
                    if (!rect.Contains(rect1))
                        continue;
                    PoiPoint poiParamData = new PoiPoint()
                    {
                        Id = index,
                        PointType = GraphicTypes.Circle,
                        PixX = circle.Center.X,
                        PixY = circle.Center.Y,
                        PixWidth = circle.Radius * 2,
                        PixHeight = circle.Radius * 2,
                        Name = circle.Text
                    };
                    PoiParam.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is RectangleTextProperties rectangle)
                {
                    Rect rect1 = new Rect(rectangle.Rect.X, rectangle.Rect.Y, rectangle.Rect.Width, rectangle.Rect.Height);
                    if (!rect.Contains(rect1))
                        continue;
                    PoiPoint poiParamData = new()
                    {
                        Id = index,
                        Name = rectangle.Text,
                        PointType = GraphicTypes.Rect,
                        PixX = rectangle.Rect.X + rectangle.Rect.Width/2,
                        PixY = rectangle.Rect.Y + rectangle.Rect.Height/2,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                    };
                    PoiParam.PoiPoints.Add(poiParamData);
                }
            }

            if (PoiConfig.IsPointInt)
            {
                PoiPoint PointInt1 = new() { Id = -1, Name = "PointInt1", PointType = GraphicTypes.Point, PixX = PoiConfig.PointInt1.X, PixY = PoiConfig.PointInt1.Y, PixWidth = 1, PixHeight = 1, };
                PoiPoint PointInt2 = new() { Id = -2, Name = "PointInt2", PointType = GraphicTypes.Point, PixX = PoiConfig.PointInt2.X, PixY = PoiConfig.PointInt2.Y, PixWidth = 1, PixHeight = 1, };
                PoiPoint PointInt3 = new() { Id = -3, Name = "PointInt3", PointType = GraphicTypes.Point, PixX = PoiConfig.PointInt3.X, PixY = PoiConfig.PointInt3.Y, PixWidth = 1, PixHeight = 1, };
                PoiPoint PointInt4 = new() { Id = -4, Name = "PointInt4", PointType = GraphicTypes.Point, PixX = PoiConfig.PointInt4.X, PixY = PoiConfig.PointInt4.Y, PixWidth = 1, PixHeight = 1, };

                PoiParam.PoiPoints.Add(PointInt1);
                PoiParam.PoiPoints.Add(PointInt2);
                PoiParam.PoiPoints.Add(PointInt3);
                PoiParam.PoiPoints.Add(PointInt4);
            }

            Thread thread = new(() =>
            {
                int ret = PoiParam.Save2DB();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string Msg = ret ==-1 ?"保存失败,具体报错信息请查看日志": "保存成功";
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), Msg, "ColorVision");
                });
            });
            thread.Start();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            SavePoiParam();
        }
        private void Button_Setting_Click(object sender, RoutedEventArgs e)
        {
        }
        private void Service_Click(object sender, RoutedEventArgs e)
        {
            var db = MySqlControl.GetInstance().DB;

            var recentItems = db.Queryable<MeasureResultImgModel>()
                   .OrderBy(it => it.CreateDate, OrderByType.Desc)
                   .Take(6)
                   .ToList();

            if (recentItems.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到刚拍摄的图像");
                return;
            }
            try
            {
                foreach (var item in recentItems)
                {
                    if (File.Exists(item.FileUrl))
                    {
                        ImageView.OpenImage(item.FileUrl);
                        return;
                    }
                }
                MessageBox.Show("打开最近服务拍摄的图像失败,找不到文件地址");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开最近服务拍摄的图像失败", ex.Message);
            }
        }


        private ObservableCollection<MeasureResultImgModel> MeasureImgResultModels = new();
        private void Button_RefreshImg_Click(object sender, RoutedEventArgs e)
        {
            MeasureImgResultModels.Clear();
            var imgs = MeasureImgResultDao.Instance.GetAll(100);
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
                    if (MeasureImgResultModels[ComboBoxImg.SelectedIndex] is MeasureResultImgModel model && model.FileUrl != null)
                    {
                        ImageView.OpenImage(model.FileUrl);
                        PoiConfig.BackgroundFilePath = model.FileUrl;
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
                double topMargin = ParseDoubleOrDefault(TextBoxUp1.Text);
                double bottomMargin = ParseDoubleOrDefault(TextBoxDown1.Text);
                double leftMargin = ParseDoubleOrDefault(TextBoxLeft1.Text);
                double rightMargin = ParseDoubleOrDefault(TextBoxRight1.Text);

                // 将当前多边形顶点存入一个列表中以便处理
                var polygonPoints = new List<Point>
        {
            new Point(PoiConfig.Polygon1X, PoiConfig.Polygon1Y),
            new Point(PoiConfig.Polygon2X, PoiConfig.Polygon2Y),
            new Point(PoiConfig.Polygon3X, PoiConfig.Polygon3Y),
            new Point(PoiConfig.Polygon4X, PoiConfig.Polygon4Y)
        };


                if (ComboBoxBorderType1.SelectedItem is KeyValuePair<GraphicBorderType, string> KeyValue && KeyValue.Key == GraphicBorderType.Relative)
                {
                    polygonPoints = ImageEditorHelper.SortRectanglePoints(polygonPoints);

                    double originalWidth = Math.Max(polygonPoints[1].X, polygonPoints[2].X) - Math.Min(polygonPoints[0].X, polygonPoints[3].X);
                    double originalHeight = Math.Max(polygonPoints[2].Y, polygonPoints[3].Y) - Math.Min(polygonPoints[0].Y, polygonPoints[1].Y);


                    // 将百分比边距转换为像素值
                    topMargin = originalHeight * topMargin / 100;
                    bottomMargin = originalHeight * bottomMargin / 100;
                    leftMargin = originalWidth * leftMargin / 100;
                    rightMargin = originalWidth * rightMargin / 100;
                }


                // 调用新的缩放方法
                var scaledPolygon = ImageEditorHelper.ScalePolygon(polygonPoints, topMargin, bottomMargin, leftMargin, rightMargin);

                // 更新 PoiConfig 中的顶点坐标
                PoiConfig.Polygon1X = (int)scaledPolygon[0].X;
                PoiConfig.Polygon1Y = (int)scaledPolygon[0].Y;
                PoiConfig.Polygon2X = (int)scaledPolygon[1].X;
                PoiConfig.Polygon2Y = (int)scaledPolygon[1].Y;
                PoiConfig.Polygon3X = (int)scaledPolygon[2].X;
                PoiConfig.Polygon3Y = (int)scaledPolygon[2].Y;
                PoiConfig.Polygon4X = (int)scaledPolygon[3].X;
                PoiConfig.Polygon4Y = (int)scaledPolygon[3].Y;

            }
            ImportMarinPopup.IsOpen =  false;
            RenderPoiConfig();
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

                if (ComboBoxBorderType11.SelectedItem is KeyValuePair<GraphicBorderType, string> KeyValue && KeyValue.Key == GraphicBorderType.Relative)
                {
                    startU = PoiConfig.AreaRectHeight * startU / 100;
                    startD = PoiConfig.AreaRectHeight * startD / 100;

                    startL = PoiConfig.AreaRectWidth * startL / 100;
                    startR = PoiConfig.AreaRectWidth * startR / 100;
                }

                PoiConfig.AreaRectWidth = PoiConfig.AreaRectWidth - (int)startR - (int)startL;
                PoiConfig.AreaRectHeight = PoiConfig.AreaRectHeight - (int)startD - (int)startD;
            }
            ImportMarinPopup1.IsOpen = false;
            RenderPoiConfig();
        }

        private void ButtonImportMarin1_Click(object sender, RoutedEventArgs e)
        {
            ImportMarinPopup1.IsOpen = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PolygonPoint polygonPoint)
            {
                PoiConfig.Polygons.Remove(polygonPoint);
                RenderPoiConfig();
            }
        }

        private void ReadFile_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(PoiConfig.PoiCIEFileName))
            {
                ClearRender();
                ViewHandleBuildPoiFile.CovertPoiParam(PoiParam, PoiConfig.PoiCIEFileName);
                PoiParamToDrawingVisual(PoiParam);
            }
        }

        public void SaveAsFile()
        {
            if (File.Exists(PoiConfig.PoiCIEFileName))
            {
                ViewHandleBuildPoiFile.CoverFile(PoiParam, PoiConfig.PoiCIEFileName);
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
                        PoiConfig.PoiCIEFileName = saveFileDialog.FileName;
                        ViewHandleBuildPoiFile.CoverFile(PoiParam, PoiConfig.PoiCIEFileName);
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

            if (!File.Exists(PoiConfig.PoiFixFilePath))
            {
                using System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.Filter = "csv Files (*.csv)|*.csv";
                saveFileDialog.Title = "Save File";
                saveFileDialog.FileName = "PoiFix.csv";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PoiConfig.PoiFixFilePath = saveFileDialog.FileName;
                }
                else
                {
                    return;
                }
            }

            using (StreamWriter writer = new StreamWriter(PoiConfig.PoiFixFilePath, false, Encoding.UTF8))
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
        }



        private void FindLuminousArea_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (ImageView.HImageCache != null)
                {
                    string FindLuminousAreajson = PoiConfig.FindLuminousArea.ToJsonN();
                    Task.Run(() =>
                    {
                        int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)ImageView.HImageCache, FindLuminousAreajson,out IntPtr resultPtr);
                        if (length > 0)
                        {
                            string result = Marshal.PtrToStringAnsi(resultPtr);
                            Console.WriteLine("Result: " + result);
                            OpenCVMediaHelper.FreeResult(resultPtr);
                            MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (rect.Width ==0)
                                {
                                    PoiConfig.AreaRectWidth = (int)ImageView.ViewBitmapSource.Width;
                                    PoiConfig.AreaRectHeight = (int)ImageView.ViewBitmapSource.Height;
                                    PoiConfig.CenterX = (int)ImageView.ViewBitmapSource.Width /2;
                                    PoiConfig.CenterY = (int)ImageView.ViewBitmapSource.Height /2;
                                }
                                else
                                {
                                    PoiConfig.AreaRectWidth = rect.Width;
                                    PoiConfig.AreaRectHeight = rect.Height;
                                    PoiConfig.CenterX = rect.X + rect.Width / 2;
                                    PoiConfig.CenterY = rect.Y + rect.Height / 2;
                                }

                                RenderPoiConfig();
                            });

                        }
                        else
                        {
                            Console.WriteLine("Error occurred, code: " + length);
                        }
                    });
                };
            }));

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            var lsit = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
            DeviceCamera deviceCamera = lsit.FirstOrDefault();
            

            MsgRecord msgRecord = deviceCamera?.DisplayCameraControlLazy.Value.TakePhoto();

            if (msgRecord != null)
            {
                msgRecord.MsgSucessed += (arg) =>
                {
                    int masterId = Convert.ToInt32(arg.Data.MasterId);
                    List<MeasureResultImgModel> resultMaster = null;
                    if (masterId > 0)
                    {
                        resultMaster = new List<MeasureResultImgModel>();
                        MeasureResultImgModel model = MeasureImgResultDao.Instance.GetById(masterId);
                        if (model != null)
                            resultMaster.Add(model);
                    }
                    if (resultMaster != null)
                    {
                        foreach (MeasureResultImgModel result in resultMaster)
                        {
                            try
                            {
                                if (result.FileUrl != null)
                                {
                                    ImageView.OpenImage(result.FileUrl);
                                    PoiConfig.BackgroundFilePath = result.FileUrl;
                                }
                                else
                                {
                                    MessageBox.Show("打开最近服务拍摄的图像失败,找不到文件地址");
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("打开最近服务拍摄的图像失败", ex.Message);
                            }
                        }
                    }



                };

            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            PoiConfig.DefaultRectWidth = PoiConfig.AreaRectWidth / PoiConfig.AreaRectRow;
            PoiConfig.DefaultRectHeight = PoiConfig.AreaRectHeight / PoiConfig.AreaRectCol;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            PoiParam.LeftBottomX = PoiEditRectCache.Instance.LeftBottomX;
            PoiParam.LeftBottomY = PoiEditRectCache.Instance.LeftBottomY;
            PoiParam.LeftTopX = PoiEditRectCache.Instance.LeftTopX;
            PoiParam.LeftTopY = PoiEditRectCache.Instance.LeftTopY;
            PoiParam.RightBottomX = PoiEditRectCache.Instance.RightBottomX;
            PoiParam.RightBottomY = PoiEditRectCache.Instance.RightBottomY;
            PoiParam.RightTopX = PoiEditRectCache.Instance.RightTopX;
            PoiParam.RightTopY = PoiEditRectCache.Instance.RightTopY;
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            PoiEditRectCache.Instance.LeftBottomX = PoiParam.LeftBottomX;
            PoiEditRectCache.Instance.LeftBottomY = PoiParam.LeftBottomY;
            PoiEditRectCache.Instance.LeftTopX = PoiParam.LeftTopX;
            PoiEditRectCache.Instance.LeftTopY = PoiParam.LeftTopY;
            PoiEditRectCache.Instance.RightBottomX = PoiParam.RightBottomX;
            PoiEditRectCache.Instance.RightBottomY = PoiParam.RightBottomY;
            PoiEditRectCache.Instance.RightTopX = PoiParam.RightTopX;
            PoiEditRectCache.Instance.RightTopY = PoiParam.RightTopY;
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            PoiEditRectCache.Instance.LeftTopX = PoiConfig.PointInt1.X;
            PoiEditRectCache.Instance.LeftTopY = PoiConfig.PointInt1.Y;
            PoiEditRectCache.Instance.RightTopX = PoiConfig.PointInt2.X;
            PoiEditRectCache.Instance.RightTopY = PoiConfig.PointInt2.Y;
            PoiEditRectCache.Instance.RightBottomX = PoiConfig.PointInt3.X;
            PoiEditRectCache.Instance.RightBottomY = PoiConfig.PointInt3.Y;
            PoiEditRectCache.Instance.LeftBottomX = PoiConfig.PointInt4.X;
            PoiEditRectCache.Instance.LeftBottomY = PoiConfig.PointInt4.Y;

        }

        private void FindLuminousAreaCorner_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if ( ImageView.HImageCache != null)
                {
                    string FindLuminousAreaCornerjson = PoiConfig.FindLuminousAreaCorner.ToJsonN();
                    Task.Run(() =>
                    {
                        int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)ImageView.HImageCache, FindLuminousAreaCornerjson, out IntPtr resultPtr);
                        if (length > 0)
                        {
                            string result = Marshal.PtrToStringAnsi(resultPtr);
                            log.Info(result);
                            OpenCVMediaHelper.FreeResult(resultPtr);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (PoiConfig.FindLuminousAreaCorner.UseRotatedRect)
                                {
                                    var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
                                    var corners = jObj["Corners"].ToObject<List<List<float>>>();
                                    if (corners.Count == 4)
                                    {
                                        PoiConfig.Polygon1X = (int)corners[0][0];
                                        PoiConfig.Polygon1Y = (int)corners[0][1];
                                        PoiConfig.Polygon2X = (int)corners[1][0];
                                        PoiConfig.Polygon2Y = (int)corners[1][1];
                                        PoiConfig.Polygon3X = (int)corners[2][0];
                                        PoiConfig.Polygon3Y = (int)corners[2][1];
                                        PoiConfig.Polygon4X = (int)corners[3][0];
                                        PoiConfig.Polygon4Y = (int)corners[3][1];
                                    }



                                }
                                else
                                {
                                    MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);
                                    PoiConfig.Polygon1X = rect.X;
                                    PoiConfig.Polygon1Y = rect.Y;
                                    PoiConfig.Polygon2X = rect.X + rect.Width;
                                    PoiConfig.Polygon2Y = rect.Y;
                                    PoiConfig.Polygon3X = rect.X + rect.Width;
                                    PoiConfig.Polygon3Y = rect.Y + rect.Height;
                                    PoiConfig.Polygon4X = rect.X;
                                    PoiConfig.Polygon4Y = rect.Y + rect.Height;
                                }
                                RenderPoiConfig();
                            });

                        }
                        else
                        {
                            Console.WriteLine("Error occurred, code: " + length);
                        }
                    });
                }
                ;
            }));
        }
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
        }

        private void Button_Click_41(object sender, RoutedEventArgs e)
        {
            PoiEditRectCache.Instance.RightTopX = PoiParam.PoiConfig.Polygon1X;
            PoiEditRectCache.Instance.RightTopY = PoiParam.PoiConfig.Polygon1Y;
            PoiEditRectCache.Instance.LeftTopX = PoiParam.PoiConfig.Polygon2X;
            PoiEditRectCache.Instance.LeftTopY = PoiParam.PoiConfig.Polygon2Y;
            PoiEditRectCache.Instance.LeftBottomX = PoiParam.PoiConfig.Polygon3X;
            PoiEditRectCache.Instance.LeftBottomY = PoiParam.PoiConfig.Polygon3Y;
            PoiEditRectCache.Instance.RightBottomX = PoiParam.PoiConfig.Polygon4X;
            PoiEditRectCache.Instance.RightBottomY = PoiParam.PoiConfig.Polygon4Y;
        }

        private void ListView1_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ListView1.ContextMenu.Items.Clear();

            Type type = DrawingVisualLists[ListView1.SelectedIndex].GetType();
            foreach (var provider in ImageView.ImageViewModel.IEditorToolFactory.ContextMenuProviders)
            {
                if (provider.ContextType.IsAssignableFrom(type))
                {
                    var items = provider.GetContextMenuItems(ImageView.ImageViewModel, DrawingVisualLists[ListView1.SelectedIndex]);
                    foreach (var item in items)
                        ListView1.ContextMenu.Items.Add(item);
                }
            }


            if(type == typeof(DVPolygon))
            {

                var itemss= new MenuItem { Header = "设置到折线布点" };
                itemss.Click += (s, e) =>
                {
                    PoiConfig.Polygons.Clear();
                    if (DrawingVisualLists[ListView1.SelectedIndex] is DVPolygon dVPolygon)
                    {
                        foreach (var item in dVPolygon.Attribute.Points)
                        {
                            PoiConfig.Polygons.Add(new PolygonPoint(item.X, item.Y));
                        }
                    }
                };
                ListView1.ContextMenu.Items.Add(itemss);
            }

            var moveUpItem = new MenuItem { Header = "上移", Command = MoveUpCommand };
            ListView1.ContextMenu.Items.Add(moveUpItem);

            var moveDownItem = new MenuItem { Header = "下移", Command = MoveDownCommand };
            ListView1.ContextMenu.Items.Add(moveDownItem);

            var moveToTopItem = new MenuItem { Header = "移动到首位", Command = MoveToTopCommand };
            ListView1.ContextMenu.Items.Add(moveToTopItem);

            var moveToBottomItem = new MenuItem { Header = "移动到末尾", Command = MoveToBottomCommand };
            ListView1.ContextMenu.Items.Add(moveToBottomItem);



        }
        RelayCommand MoveUpCommand { get; set; }
        RelayCommand MoveDownCommand { get; set; }
        RelayCommand MoveToTopCommand { get; set; }
        RelayCommand MoveToBottomCommand { get; set; }

        // 添加移动方法
        private void MoveUp()
        {
            int index = ListView1.SelectedIndex; // 假设ListView1是ViewModel中的属性
            if (index > 0)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.RemoveAt(index);
                DrawingVisualLists.Insert(index - 1, item);
                ListView1.SelectedIndex = index - 1;
            }
        }

        private void MoveDown()
        {
            int index = ListView1.SelectedIndex;
            if (index < DrawingVisualLists.Count - 1)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.RemoveAt(index);
                DrawingVisualLists.Insert(index + 1, item);
                ListView1.SelectedIndex = index + 1;
            }
        }

        private void MoveToTop()
        {
            int index = ListView1.SelectedIndex;
            if (index > 0)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.RemoveAt(index);
                DrawingVisualLists.Insert(0, item);
                ListView1.SelectedIndex = 0;
            }
        }

        private void MoveToBottom()
        {
            int index = ListView1.SelectedIndex;
            if (index < DrawingVisualLists.Count - 1)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.RemoveAt(index);
                DrawingVisualLists.Add(item);
                ListView1.SelectedIndex = DrawingVisualLists.Count - 1;
            }
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {

        }
    }

}
