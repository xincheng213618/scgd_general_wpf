#pragma warning disable CS8625,CS8604,CS8602
using ColorVision.Common.Adorners.ListViewAdorners;
using ColorVision.Common.Collections;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Tif;
using ColorVision.Net;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Sorts;
using ColorVision.Util.Draw.Rectangle;
using cvColorVision;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
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

namespace ColorVision.Engine.Templates.POI
{
    public class EditPoiParam1Config : ViewModelBase, IConfig
    {
        public static EditPoiParam1Config Instance => ConfigService.Instance.GetRequiredService<EditPoiParam1Config>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    public class KBPoiConfig : PoiConfig
    {
        public RelayCommand SelectLuminFileCommand { get; set; }
        public RelayCommand SelcetSaveFilePathCommand { get; set; }

        public KBPoiConfig() : base()
        {
            SelectLuminFileCommand = new RelayCommand(a => SelectLuminFile());
            SelcetSaveFilePathCommand = new RelayCommand(a => SelcetSaveFilePath());
        }
        public void SelectLuminFile()
        {
            using (System.Windows.Forms.OpenFileDialog saveFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                saveFileDialog.Filter = "标定文件 (*.dat)|*.dat";
                saveFileDialog.Title = "选择标定文件";
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    LuminFile = saveFileDialog.FileName;
                }
            }
        }
        public void SelcetSaveFilePath()
        {
            using (System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                folderBrowserDialog.Description = "Select Folder";
                folderBrowserDialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SaveFolderPath = folderBrowserDialog.SelectedPath;
                }
            }
        }

        public bool DefaultDoKey { get => _DefaultDoKey; set { _DefaultDoKey = value; NotifyPropertyChanged(); } }
        private bool _DefaultDoKey = true;
        public bool DefaultDoHalo { get => _DefaultDoHalo; set { _DefaultDoHalo = value; NotifyPropertyChanged(); } }
        private bool _DefaultDoHalo;

        /// <summary>
        /// 校正文件
        /// </summary>
        public string LuminFile { get => _LuminFile; set { _LuminFile = value; NotifyPropertyChanged(); } }
        private string _LuminFile = string.Empty;

        public int SaveProcessData { get => _saveProcessData; set { _saveProcessData = value; NotifyPropertyChanged(); } }
        private int _saveProcessData;

        public float Exp { get => _Exp; set { _Exp = value; NotifyPropertyChanged(); } }
        private float _Exp = 600;

        public string SaveFolderPath { get => _SaveFolderPath; set { _SaveFolderPath = value; NotifyPropertyChanged(); } }
        private string _SaveFolderPath =Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

    }


    public partial class EditPoiParam1 : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EditPoiParam1));
        private string TagName { get; set; } = "P_";

        public KBJson KBJson { get; set; }
        public KBPoiConfig PoiConfig => KBJson.PoiConfig;

        public TemplateJsonKBParam TemplateJsonKBParam { get; set; }

        public EditPoiParam1(TemplateJsonKBParam poiParam) 
        {
            TemplateJsonKBParam = poiParam;
            KBJson = TemplateJsonKBParam.KBJson;
            InitializeComponent();
            this.ApplyCaption();
            Task.Run(() => DelayClose());
            this.Title = poiParam.Name + "-" + this.Title;
        }

        public async void DelayClose()
        {
            await Task.Delay(100);
            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                this.DelayClearImage((Action)(() => Application.Current.Dispatcher.Invoke((Action)(() =>
                {
                    ImageViewModel?.ClearImage();
                    if (HImageCache != null)
                    {
                        HImageCache?.Dispose();
                        HImageCache = null;
                    };
                    this.ViewBitmapSource = null;
                }))));
            }));
        }
        

        public BulkObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new BulkObservableCollection<IDrawingVisual>();
        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = KBJson;

            ListView1.ItemsSource = DrawingVisualLists;
            if (AlgorithmKBConfig.Instance.KBCanDrag)
            {
                ListViewDragDropManager<IDrawingVisual> listViewDragDropManager = new Common.Adorners.ListViewAdorners.ListViewDragDropManager<IDrawingVisual>(ListView1);
                listViewDragDropManager.ShowDragAdorner = true;
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
            }
           
            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>()  select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues(typeof(BorderType)).Cast<BorderType>() select new KeyValuePair<BorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues(typeof(DrawingPOIPosition)).Cast<DrawingPOIPosition>() select new KeyValuePair<DrawingPOIPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;

            ImageViewModel = new ImageViewModel(ImageContentGrid, Zoombox1, ImageShow);

            ImageViewModel.ToolBarScaleRuler.IsShow = false;
            ToolBar1.DataContext = ImageViewModel;
            ToolBarRight.DataContext = ImageViewModel;
            ImageViewModel.EditModeChanged += (s, e) =>
            {
                if (e)
                {
                    PoiConfig.IsShowDatum = false;
                    PoiConfig.IsShowPoiConfig = false;
                    RenderPoiConfig();
                }
            };

            ImageShow.VisualsAdd += (s, e) =>
            {
                if (!PoiConfig.IsShowText)
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
                        if (visual.BaseAttribute.Param ==null)
                        {
                            if (visual.BaseAttribute is RectangleTextProperties rectangle)
                            {
                                PoiPointParam poiPointParam = new PoiPointParam();
                                visual.BaseAttribute.Param = poiPointParam;
                                poiPointParam.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == "Area")
                                    {
                                        CalPoiPointParamB(rectangle);
                                    }
                                };

                            }

                        }

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
            if (PoiConfig.IsShowText)
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
                    UpdateVisualLayout(PoiConfig.IsLayoutUpdated);
                }
            };

            if (KBJson.Height != 0 && KBJson.Width != 0)
            {
                WaitControl.Visibility = Visibility.Visible;
                WaitControlProgressBar.Visibility = Visibility.Visible;
                WaitControlProgressBar.Value = 0;

                if (File.Exists(PoiConfig.BackgroundFilePath))
                    OpenImage(PoiConfig.BackgroundFilePath);
                else
                    CreateImage(KBJson.Width, KBJson.Height, Colors.White, false);

                WaitControlProgressBar.Value = 20;
                RenderPoiConfig();
                PoiParamToDrawingVisual(KBJson);
                WaitControl.Visibility = Visibility.Collapsed;
                WaitControlProgressBar.Visibility = Visibility.Collapsed;
                log.Debug("Render Poi end");
            }
            else
            {
                KBJson.Width = 400;
                KBJson.Height = 300;
                CreateImage(KBJson.Width, KBJson.Height, Colors.White, false);
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

            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                EditPoiParam1Config.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                EditPoiParam1Config.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
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
                            var sortedItems = DrawingVisualLists.ToList();
                            sortedItems.Sort((x, y) => item.IsSortD ? y.BaseAttribute.Id.CompareTo(x.BaseAttribute.Id) : x.BaseAttribute.Id.CompareTo(y.BaseAttribute.Id));
                            DrawingVisualLists.UpdateCollection(sortedItems);
                        }
                    }
                }
            }
        }

        public ImageViewModel ImageViewModel { get; set; }

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
                        OpenImage(new NetFileUtil().OpenLocalCVFile(filePath, (CVType)fileExtType));
                        PoiConfig.BackgroundFilePath = filePath;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    OpenImage(filePath);
                    PoiConfig.BackgroundFilePath = filePath;
                }
            }
        }

        private void CreateImage_Click(object sender, RoutedEventArgs e)
        {
            CreateImage(KBJson.Width, KBJson.Height, Colors.White,false);

        }

        public void OpenImage(string filePath)
        {
            if (CVFileUtil.IsCIEFile(filePath))
            {
                OpenImage(new NetFileUtil().OpenLocalCVFile(filePath));
            }
            else if (Path.GetExtension(filePath).Contains(".tif"))
            {
                SetImageSource(new WriteableBitmap(TiffReader.ReadTiff(filePath)));
            }
            else 
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                SetImageSource(bitmapImage.ToWriteableBitmap());
            }
        }

        public HImage? HImageCache { get; set; }

        public void SetImageSource(BitmapSource imageSource)
        {
            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            };
            if (imageSource is WriteableBitmap writeableBitmap)
            {
                Task.Run(() => Application.Current.Dispatcher.Invoke((() =>
                {
                    HImageCache = writeableBitmap.ToHImage();
                })));
            }
            KBJson.Width = imageSource.PixelWidth;
            KBJson.Height = imageSource.PixelHeight;
            InitPoiConfigValue(imageSource.PixelWidth, imageSource.PixelHeight);

            ImageShow.RaiseImageInitialized();
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
        private void CreateImage(int width, int height, Color color,bool IsClear = true)
        {
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }

            Thread thread = new(() => 
            {
                Application.Current.Dispatcher.Invoke((Action)(() =>
                {
                    this.ViewBitmapSource = CreateWhiteLayer(width, height);
                    if (ImageShow.Source == null)
                    {
                        ImageShow.Source = this.ViewBitmapSource;
                        Zoombox1.ZoomUniform();
                        if (IsClear || !Init)
                            InitPoiConfigValue((int)this.ViewBitmapSource.Width,(int)this.ViewBitmapSource.Height);
                    }
                    else
                    {
                        if (ImageShow.Source is BitmapSource img && (img.PixelWidth != this.ViewBitmapSource.Width || img.PixelHeight != this.ViewBitmapSource.Height))
                        {
                            InitPoiConfigValue((int)this.ViewBitmapSource.Width, (int)this.ViewBitmapSource.Height);
                            ImageShow.Source = this.ViewBitmapSource;
                            Zoombox1.ZoomUniform();
                        }
                        else
                        {
                            ImageShow.Source = this.ViewBitmapSource;
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

        private async void PoiParamToDrawingVisual(KBJson poiParam)
        {
            try
            {
                int WaitNum = 50;
                if (!PoiConfig.IsShowText)
                    WaitNum = 1000;
                foreach (var item in poiParam.KBKeyRects)
                {
                    No++;

                    if (No % WaitNum == 0)
                    {
                        WaitControlProgressBar.Value = 20 + No * 79 / poiParam.KBKeyRects.Count;
                        await Task.Delay(10);
                    }
                    DVRectangleText Rectangle = new();
                    Rectangle.IsShowText = PoiConfig.IsShowText;
                    Rectangle.Attribute.Rect = new System.Windows.Rect(item.X , item.Y, item.Width, item.Height);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red,  (double)item.Width / 30);
                    Rectangle.Attribute.Id = No;
                    Rectangle.Attribute.Text = item.Name;
                    Rectangle.Attribute.Name = No.ToString();

                    PoiPointParam poiPointParam = new PoiPointParam()
                    {
                        HaloScale = item.KBHalo.HaloScale,
                        HaloOffsetX = item.KBHalo.OffsetX,
                        HaloOffsetY = item.KBHalo.OffsetY,
                        HaloSize = item.KBHalo.HaloSize,
                        HaloThreadV = item.KBHalo.ThresholdV,
                        HaloOutMOVE = item.KBHalo.Move,
                        KeyScale = item.KBKey.KeyScale,
                        KeyOffsetX = item.KBKey.OffsetX,
                        KeyOffsetY = item.KBKey.OffsetY,
                        KeyThreadV = item.KBKey.ThresholdV,
                        KeyOutMOVE = item.KBKey.Move,
                        Area = item.KBKey.Area,
                    };
                    poiPointParam.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == "Area")
                        {
                            CalPoiPointParamB(Rectangle.Attribute);
                        }
                    };

                    Rectangle.Attribute.Param = poiPointParam;




                    Rectangle.Render();
                    ImageShow.AddVisual(Rectangle);
                    DBIndex.Add(Rectangle, No);
                }
                WaitControlProgressBar.Value = 99;
                ImageShow.ClearActionCommand();
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
                case RiPointTypes.Circle:
                    if (PoiConfig.AreaCircleNum < 1)
                    {
                        MessageBox.Show("绘制的个数不能小于1", "ColorVision");
                        return;
                    }

                    if (PoiConfig.AreaCircleNum > 1000)
                    {
                        WaitControl.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Value = 0;
                        PoiConfig.IsLayoutUpdated = false;
                    }


                    for (int i = 0; i < PoiConfig.AreaCircleNum; i++)
                    {
                        Num++;
                        if (Num % 100 == 0 && WaitControl.Visibility == Visibility.Visible)
                        {
                            WaitControlProgressBar.Value = Num * 1000 / PoiConfig.AreaCircleNum;
                            await Task.Delay(1);
                        }

                        double x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                        double y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);

                        switch (PoiConfig.DefaultPointType)
                        {
                            case RiPointTypes.Circle:

                                if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition)
                                {
                                    switch (pOIPosition)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultCircleRadius) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.External:
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
                                ImageShow.AddVisual(Circle);
                                break;
                            case RiPointTypes.Rect:

                                if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition2)
                                {
                                    switch (pOIPosition2)
                                    {
                                        case DrawingPOIPosition.LineOn:
                                            x1 = PoiConfig.CenterX + PoiConfig.AreaCircleRadius * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + PoiConfig.AreaCircleRadius * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.Internal:
                                            x1 = PoiConfig.CenterX + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectWidth / 2) * Math.Cos(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            y1 = PoiConfig.CenterY + (PoiConfig.AreaCircleRadius - PoiConfig.DefaultRectHeight / 2) * Math.Sin(i * 2 * Math.PI / PoiConfig.AreaCircleNum + Math.PI / 180 * PoiConfig.AreaCircleAngle);
                                            break;
                                        case DrawingPOIPosition.External:
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

                    if (ComboBoxBorderType2.SelectedValue is DrawingPOIPosition pOIPosition1)
                    {
                        switch (PoiConfig.DefaultPointType)
                        {
                            case RiPointTypes.Circle:
                                switch (pOIPosition1)
                                {
                                    case DrawingPOIPosition.LineOn:
                                        break;
                                    case DrawingPOIPosition.Internal:
                                        startU += PoiConfig.DefaultCircleRadius;
                                        startD += PoiConfig.DefaultCircleRadius;
                                        startL += PoiConfig.DefaultCircleRadius;
                                        startR += PoiConfig.DefaultCircleRadius;
                                        break;
                                    case DrawingPOIPosition.External:
                                        startU -= PoiConfig.DefaultCircleRadius;
                                        startD -= PoiConfig.DefaultCircleRadius;
                                        startL -= PoiConfig.DefaultCircleRadius;
                                        startR -= PoiConfig.DefaultCircleRadius;
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
                                        startU += PoiConfig.DefaultRectWidth / 2;
                                        startD += PoiConfig.DefaultRectWidth / 2;
                                        startL += PoiConfig.DefaultRectHeight / 2;
                                        startR += PoiConfig.DefaultRectHeight / 2;
                                        break;
                                    case DrawingPOIPosition.External:
                                        startU -= PoiConfig.DefaultRectWidth / 2;
                                        startD -= PoiConfig.DefaultRectWidth / 2;
                                        startL -= PoiConfig.DefaultRectHeight / 2;
                                        startR -= PoiConfig.DefaultRectHeight / 2;
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


                    double StepRow = (rows > 1) ? (bitmapImage.PixelHeight - startD - startU) / (rows - 1) : 0;
                    double StepCol = (cols > 1) ? (bitmapImage.PixelWidth - startL - startR) / (cols - 1) : 0;


                    int all = rows * cols;
                    if (all > 1000)
                    {
                        DrawingVisualLists.SuspendUpdate();
                        WaitControl.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Visibility = Visibility.Visible;
                        WaitControlProgressBar.Value = 0;
                        PoiConfig.IsLayoutUpdated = false;
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

                            switch (PoiConfig.DefaultPointType)
                            {
                                case RiPointTypes.Circle:
                                    DVCircleText Circle = new();
                                    Circle.IsShowText = PoiConfig.IsShowText;
                                    Circle.Attribute.Center = new Point(x1, y1);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + i * cols + j + 1;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.IsShowText = PoiConfig.IsShowText;
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(x1 - (double)PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i * cols + j + 1;
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
                    }
                    if (all <= 1000000)
                    {
                        DrawingVisualLists.ResumeUpdate();
                    }

                    break;
                case RiPointTypes.Mask:
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

                    double rowStep = (rows > 1) ? 1.0 / (rows - 1) : 0;
                    double columnStep = (cols > 1) ? 1.0 / (cols - 1) : 0;

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
                                case RiPointTypes.Circle:
                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(point.X, point.Y);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + i * cols + j + 1;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Name);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(point.X - PoiConfig.DefaultRectWidth / 2, point.Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i * cols + j + 1;
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
                    }

                    break;

                case RiPointTypes.Polygon:

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
                                case RiPointTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiConfig.Polygons[i].X + dx * j, PoiConfig.Polygons[i].Y + dy * j);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + No;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);
                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Polygons[i].X + dx * j - PoiConfig.DefaultRectWidth / 2, PoiConfig.Polygons[i].Y + dy * j - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
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

                    for (int i = 0; i < PoiConfig.Polygons.Count; i++)
                    {
                        if (PoiConfig.AreaPolygonUsNode)
                        {
                            switch (PoiConfig.DefaultPointType)
                            {
                                case RiPointTypes.Circle:

                                    DVCircleText Circle = new();
                                    Circle.Attribute.Center = new Point(PoiConfig.Polygons[i].X, PoiConfig.Polygons[i].Y);
                                    Circle.Attribute.Radius = PoiConfig.DefaultCircleRadius;
                                    Circle.Attribute.Brush = Brushes.Transparent;
                                    Circle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultCircleRadius / 30);
                                    Circle.Attribute.Id = start + i + 1;
                                    Circle.Attribute.Name = Circle.Attribute.Id.ToString();
                                    Circle.Attribute.Text = string.Format("{0}{1}", TagName, Circle.Attribute.Id);

                                    Circle.Render();
                                    ImageShow.AddVisual(Circle);
                                    break;
                                case RiPointTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Polygons[i].X - PoiConfig.DefaultRectWidth / 2, PoiConfig.Polygons[i].Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
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
            if (PoiConfig.IsShowText)
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
                        ImageShow.RemoveVisual(visual);
                    }

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
            if (PoiConfig.IsShowPoiConfig)
            {
                switch (PoiConfig.PointType)
                {
                    case RiPointTypes.Circle:
                        DVDatumCircle Circle = new();
                        Circle.Attribute.Center = PoiConfig.Center;
                        Circle.Attribute.Radius = PoiConfig.AreaCircleRadius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Render();
                        drawingVisualDatum = Circle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Rect:
                        double Width = PoiConfig.AreaRectWidth;
                        double Height = PoiConfig.AreaRectHeight;
                        DVDatumRectangle Rectangle = new();
                        Rectangle.Attribute.Rect = new System.Windows.Rect(PoiConfig.Center - new Vector((int)(Width / 2), (int)(Height / 2)), (PoiConfig.Center + new Vector((int)(Width / 2), (int)(Height / 2))));
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Render();
                        drawingVisualDatum = Rectangle;
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Mask:

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
                        ImageShow.AddVisual(drawingVisualDatum);
                        break;
                    case RiPointTypes.Polygon:
                        DVDatumPolygon Polygon1 = new() { IsComple = false };
                        Polygon1.Attribute.Pen = new Pen(Brushes.Blue, 1 / Zoombox1.ContentMatrix.M11);
                        Polygon1.Attribute.Brush = Brushes.Transparent;
                        foreach (var item in PoiConfig.Polygons)
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
            KBJson.KBKeyRects.Clear();
            Rect rect = new Rect(0, 0, KBJson.Width, KBJson.Height);
            foreach (var item in DrawingVisualLists)
            {
                int index = DBIndex.TryGetValue(item, out int value) ? value : -1;

                BaseProperties drawAttributeBase = item.BaseAttribute;
               if (drawAttributeBase is RectangleTextProperties rectangle)
                {
                    Rect rect1 = new Rect(rectangle.Rect.X, rectangle.Rect.Y, rectangle.Rect.Width, rectangle.Rect.Height);
                    if (!rect.Contains(rect1))
                        continue;
                    PoiPoint poiParamData = new()
                    {
                        Id = index,
                        Name = rectangle.Text,
                        PointType = RiPointTypes.Rect,
                        PixX = rectangle.Rect.X + rectangle.Rect.Width / 2,
                        PixY = rectangle.Rect.Y + rectangle.Rect.Height / 2,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                    };
                    KBKeyRect kBKeyRect = new KBKeyRect();
                    if (rectangle.Param is not PoiPointParam param)
                    {
                        param = new PoiPointParam();
                    }
                    kBKeyRect.DoHalo = PoiConfig.DefaultDoHalo;
                    kBKeyRect.DoKey = PoiConfig.DefaultDoKey ;

                    KBHalo kBHalo = new KBHalo();
                    kBHalo.HaloScale = param.HaloScale;
                    kBHalo.OffsetX = param.HaloOffsetX;
                    kBHalo.OffsetY = param.HaloOffsetY;
                    kBHalo.HaloSize = param.HaloSize;
                    kBHalo.ThresholdV = param.HaloThreadV;
                    kBHalo.Move = param.HaloOutMOVE;
                    kBKeyRect.KBHalo = kBHalo;


                    KBKey kBKey = new KBKey();
                    kBKey.KeyScale = param.KeyScale;
                    kBKey.OffsetX = param.KeyOffsetX;
                    kBKey.OffsetY = param.KeyOffsetY;
                    kBKey.ThresholdV = param.KeyThreadV;
                    kBKey.Move = param.KeyOutMOVE;
                    kBKey.Area = param.Area;
                    kBKeyRect.KBKey = kBKey;

                    kBKeyRect.Height = (int)rectangle.Rect.Height;
                    kBKeyRect.Width = (int)rectangle.Rect.Width;
                    kBKeyRect.X = (int)rectangle.Rect.X;
                    kBKeyRect.Y = (int)rectangle.Rect.Y;
                    kBKeyRect.Name = rectangle.Text;

                    kBKeyRect.DoKey = true;
                    KBJson.KBKeyRects.Add(kBKeyRect);
                }
            }
            TemplateJsonKBParam.JsonValue = JsonConvert.SerializeObject(KBJson);
            TemplateJsonDao.Instance.Save(TemplateJsonKBParam.TemplateJsonModel);

            MessageBox.Show(WindowHelpers.GetActiveWindow(), "保存成功", "ColorVision");
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            SavePoiParam();
        }
        private void Service_Click(object sender, RoutedEventArgs e)
        {
            if (MeasureImgResultDao.Instance.GetLatestResult() is MeasureImgResultModel measureImgResultModel)
            {
                try
                {
                    if (measureImgResultModel.FileUrl != null)
                    {
                        foreach (var item in MeasureImgResultDao.Instance.GetByCreateDate(6))
                        {
                            if (!item.FileUrl.Contains("result"))
                            {
                                OpenImage(new NetFileUtil().OpenLocalCVFile(item.FileUrl));
                                PoiConfig.BackgroundFilePath = item.FileUrl;
                                return;
                            }
                        }
                        MessageBox.Show("打开最近服务拍摄的图像失败,找不到文件地址");
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
            if (fileInfo.FileExtType == CVType.Src)
            {
                if (fileInfo.data != null)
                {
                    var src = OpenCvSharp.Cv2.ImDecode(fileInfo.data, OpenCvSharp.ImreadModes.Unchanged);
                    SetImageSource(src.ToWriteableBitmap());
                }
            }
            else if (fileInfo.FileExtType == CVType.Raw)
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
                SetImageSource(dst.ToWriteableBitmap());
            }
        }



        private ObservableCollection<MeasureImgResultModel> MeasureImgResultModels = new();
        private void Button_RefreshImg_Click(object sender, RoutedEventArgs e)
        {
            MeasureImgResultModels.Clear();
            var imgs = MeasureImgResultDao.Instance.GetAll();
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
                        OpenImage(new NetFileUtil().OpenLocalCVFile(model.FileUrl, CVType.Raw));
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

                PoiConfig.Polygon1X += (int)startL;
                PoiConfig.Polygon1Y += (int)startU;
                PoiConfig.Polygon2X -= (int)startR;
                PoiConfig.Polygon2Y += (int)startU;
                PoiConfig.Polygon3X -= (int)startR;
                PoiConfig.Polygon3Y -= (int)startD;
                PoiConfig.Polygon4X += (int)startL;
                PoiConfig.Polygon4Y -= (int)startD;

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

                if (ComboBoxBorderType11.SelectedItem is KeyValuePair<BorderType, string> KeyValue && KeyValue.Key == BorderType.Relative)
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


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            FocusPointGrid.Height = FocusPointRowDefinition.ActualHeight;
            PropertyGrid21.Height = FocusPointRowDefinition.ActualHeight;
        }

        private void reference_Click(object sender, RoutedEventArgs e)
        {
            menuPop1.IsOpen = true;
        }

        private void reference1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                menuPop1.IsOpen = false;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            new EidtPoiDataGridForm((ObservableCollection<IDrawingVisual>)DrawingVisualLists).Show();           
        }


        public ImageSource PseudoImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RenderPseudo();
        }
        private void Pseudo_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            PseudoColor pseudoColor = new PseudoColor(new ImageViewConfig());
            pseudoColor.ShowDialog();
            var Colormapes = PseudoColor.GetColormapDictionary().First(x => x.Key == ColormapTypes.COLORMAP_JET);
            string valuepath = Colormapes.Value;
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative));
            RenderPseudo();
        }
        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, (e) =>
            {
                RenderPseudo();
            }, e.NewValue);
        }
        public void RenderPseudo()
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (Pseudo.IsChecked == false)
                {
                    ImageShow.Source = this.ViewBitmapSource;
                    PseudoImage = null;
                    return;
                }

                if (HImageCache != null)
                {
                    // 首先获取滑动条的值，这需要在UI线程中执行

                    uint min = (uint)PseudoSlider.ValueStart;
                    uint max = (uint)PseudoSlider.ValueEnd;

                    log.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        int ret = OpenCVMediaHelper.M_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, ColormapTypes.COLORMAP_JET, -1);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                if (!HImageExtension.UpdateWriteableBitmap(PseudoImage, hImageProcessed))
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                    hImageProcessed.pData = nint.Zero;
                                    PseudoImage = image;
                                }
                                if (Pseudo.IsChecked == true)
                                {
                                    ImageShow.Source = PseudoImage;
                                }
                            }
                        });
                    });
                };
            }));
        }



        private void FindLuminousArea_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (HImageCache != null)
                {
                    string re = PoiConfig.FindLuminousArea.ToJsonN();
                    Task.Run(() =>
                    {
                        int length = OpenCVMediaHelper.M_FindLuminousArea((HImage)HImageCache, re,out IntPtr resultPtr);
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
                                    PoiConfig.AreaRectWidth = (int)ViewBitmapSource.Width;
                                    PoiConfig.AreaRectHeight = (int)ViewBitmapSource.Height;
                                    PoiConfig.CenterX = (int)ViewBitmapSource.Width /2;
                                    PoiConfig.CenterY = (int)ViewBitmapSource.Height /2;
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

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Cal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InitialKBKey();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetKBLocal_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CalPoiPointParamB(RectangleTextProperties rectangle)
        {
            if (!IsInitialKB) return;
            if (rectangle.Param is PoiPointParam poiPointParam)
            {
                IRECT rect = new IRECT((int)rectangle.Rect.X, (int)rectangle.Rect.Y, (int)rectangle.Rect.Width, (int)rectangle.Rect.Height);
                if (PoiConfig.DefaultDoKey)
                {
                    ushort[] keygray1 = new ushort[256];
                    uint Keygraynum = 0;
                    float keyGray = KeyBoardDLL.CM_CalculateKey(rect, poiPointParam.KeyOutMOVE, poiPointParam.KeyThreadV, PoiConfig.SaveFolderPath + $"\\{rectangle.Text}", keygray1, ref Keygraynum);
                    if (Calibratiohandle != IntPtr.Zero)
                    {
                        byte[] byteArray = BitConverter.GetBytes(keygray1[0]);
                        byte[] byteArray1 = new byte[4];
                        cvCameraCSLib.CM_SCGD_SDP_Luminance(Calibratiohandle, 1, 1, 16, 1, byteArray, byteArray1, new float[] { PoiConfig.Exp, PoiConfig.Exp, PoiConfig.Exp });
                        keyGray = (float)BitConverter.ToSingle(byteArray1);
                    }

                    keyGray = (float)(keyGray * poiPointParam.KeyScale);
                    if (poiPointParam.Area != 0)
                    {
                        poiPointParam.Brightness = keyGray / poiPointParam.Area;
                        poiPointParam.Brightness = poiPointParam.Brightness * Keygraynum * AlgorithmKBConfig.Instance.KBLVSacle;
                    }
                    else
                    {
                        poiPointParam.Brightness = keyGray;
                        poiPointParam.Brightness = poiPointParam.Brightness * Keygraynum * AlgorithmKBConfig.Instance.KBLVSacle;
                    }

                }

            }


        }
        nint Calibratiohandle = IntPtr.Zero;

        bool IsInitialKB ;
        private void InitialKBKey()
        {
           if (PoiConfig.CalibrationParams == null)
            {
                MessageBox.Show("请先设置校准模板");
                return;
            }
            string luminFile;
            if (PoiConfig.CalibrationTemplateIndex > -1 && PoiConfig.CalibrationParams[PoiConfig.CalibrationTemplateIndex] is TemplateModel<CalibrationParam> templateModel)
            {
                string path = templateModel.Value.Color.Luminance.FilePath;


                if (string.IsNullOrEmpty(path))
                {
                    path = templateModel.Value.Color.LumFourColor.FilePath;
                    if (string.IsNullOrEmpty(path))
                    {
                        MessageBox.Show("找不到亮度校正模板");
                        return;
                    }
                    else
                    {
                        log.Info("执行四色校正");
                        var resource = SysResourceDao.Instance.GetById(templateModel.Value.Color.LumFourColor.Id);

                        PhyCamera phyCamera1 = PoiConfig.DeviceCamera.PhyCamera;
                        string filepath = Path.Combine(phyCamera1.Config.FileServerCfg.FileBasePath, phyCamera1.Code, "cfg", resource.Value);
                        log.Info($"Lum:{filepath}");

                        if (File.Exists(filepath))
                        {
                            luminFile = filepath;
                        }
                        else
                        {
                            MessageBox.Show("找不到亮度校正模板");
                            return;
                        }
                    }
                }
                else
                {
                    log.Info("执行单通道校正");

                    var resource = SysResourceDao.Instance.GetById(templateModel.Value.Color.Luminance.Id);

                    PhyCamera phyCamera1 = PoiConfig.DeviceCamera.PhyCamera;
                    string filepath = Path.Combine(phyCamera1.Config.FileServerCfg.FileBasePath, phyCamera1.Code, "cfg", resource.Value);
                    log.Info($"Lum:{filepath}");

                    if (File.Exists(filepath))
                    {
                        luminFile = filepath;

                        Calibratiohandle = cvCameraCSLib.CreatCalibrationManage();
                        int ret = cvCameraCSLib.CM_SetCalibParam(Calibratiohandle, CalibrationType.Luminance,true, luminFile);
                        if (ret != 1)
                        {
                            log.Error("read luminance file ERROR!");
                        }
                    }
                    else
                    {
                        MessageBox.Show("找不到亮度校正模板");
                        return;
                    }
                }


            }
            else
            {
                MessageBox.Show("请先设置校准模板");
                return;
            }


            if (luminFile ==null || !File.Exists(luminFile))
            {
                MessageBox.Show("找不到亮度校正模板");
                return;
            }
            string imgFileName = PoiConfig.BackgroundFilePath;
            if (imgFileName == null || !File.Exists(imgFileName))
            {
                MessageBox.Show("背景图片");
                return;
            }

            OpenCvSharp.Mat image;
            if (CVFileUtil.IsCIEFile(imgFileName))
            {
                int index = CVFileUtil.ReadCIEFileHeader(imgFileName, out CVCIEFile cvcie);
                if (cvcie.FileExtType == CVType.CIE)
                {
                    string path = Path.GetDirectoryName(imgFileName);
                    if (File.Exists(Path.Combine(path, cvcie.srcFileName)))
                    {
                        int index1 = CVFileUtil.ReadCIEFileHeader(Path.Combine(path, cvcie.srcFileName), out CVCIEFile cvcie1);

                        if (index1 > 0)
                        {
                            CVFileUtil.ReadCIEFileData(Path.Combine(path, cvcie.srcFileName), ref cvcie1, index1);
                            if (cvcie1.bpp == 16)
                            {
                                image = OpenCvSharp.Mat.FromPixelData(cvcie1.cols, cvcie1.rows, OpenCvSharp.MatType.MakeType(cvcie1.Depth, cvcie1.channels), cvcie1.data);
                            }
                            else if (cvcie1.bpp == 32)
                            {
                                OpenCvSharp.Mat src = OpenCvSharp.Mat.FromPixelData(cvcie1.cols, cvcie1.rows, OpenCvSharp.MatType.MakeType(cvcie1.Depth, cvcie1.channels), cvcie1.data);
                                OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                                image = new OpenCvSharp.Mat();
                                src.ConvertTo(image, OpenCvSharp.MatType.CV_8U);
                            }
                            else
                            {
                                image = OpenCvSharp.Mat.FromPixelData(cvcie1.cols, cvcie1.rows, OpenCvSharp.MatType.CV_8UC(cvcie1.channels), cvcie1.data);
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                   
                }
                else
                {
                    if (index > 0)
                    {
                        CVFileUtil.ReadCIEFileData(imgFileName, ref cvcie, index);
                        if (cvcie.bpp == 16)
                        {
                            image = OpenCvSharp.Mat.FromPixelData(cvcie.cols, cvcie.rows, OpenCvSharp.MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);
                        }
                        else if (cvcie.bpp == 32)
                        {
                            OpenCvSharp.Mat src = OpenCvSharp.Mat.FromPixelData(cvcie.cols, cvcie.rows, OpenCvSharp.MatType.MakeType(cvcie.Depth, cvcie.channels), cvcie.data);
                            OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                            image = new OpenCvSharp.Mat();
                            src.ConvertTo(image, OpenCvSharp.MatType.CV_8U);
                        }
                        else
                        {
                            image = OpenCvSharp.Mat.FromPixelData(cvcie.cols, cvcie.rows, OpenCvSharp.MatType.CV_8UC(cvcie.channels), cvcie.data);
                        }
                    }
                    else
                    {
                        return;
                    }
                }

            }
            else
            {
                image = OpenCvSharp.Cv2.ImRead(imgFileName, OpenCvSharp.ImreadModes.Unchanged);
            }

            int width = image.Width;
            int height = image.Height;
            int channels = image.Channels();
            int bpp = image.ElemSize() * 8 / channels;
            IntPtr imgData = image.Data;
            KeyBoardDLL.CM_InitialKeyBoardSrc(width, height, bpp, channels, imgData, PoiConfig.SaveProcessData, PoiConfig.SaveFolderPath, PoiConfig.Exp, luminFile, 0);

            string csvFilePath = PoiConfig.SaveFolderPath + "\\output.csv";
            using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                writer.WriteLine("Name,rect,HaloGray,haloGraynum,KeyGray,Keygraynum");
                foreach (var drawingVisual in DrawingVisualLists)
                {
                    BaseProperties drawAttributeBase = drawingVisual.BaseAttribute;
                    if (drawAttributeBase is RectangleTextProperties rectangle && rectangle.Param is PoiPointParam poiPointParam)
                    { 
                        try
                        {
                            IRECT rect = new IRECT((int)rectangle.Rect.X, (int)rectangle.Rect.Y, (int)rectangle.Rect.Width, (int)rectangle.Rect.Height);
                            float haloGray = -1;
                            uint haloGraynum = 0;
                            ushort[] haloGray1 = new ushort[256];
                            uint Keygraynum = 0;
                            ushort[] keygray1 = new ushort[256];

                            if (PoiConfig.DefaultDoHalo)
                            {
                                haloGray = KeyBoardDLL.CM_CalculateHalo(rect, poiPointParam.HaloOutMOVE, poiPointParam.HaloThreadV, 15, PoiConfig.SaveFolderPath + $"\\{rectangle.Text}",  haloGray1,ref haloGraynum);
                                haloGray = (float)(haloGray * poiPointParam.HaloScale);
                            }
                            float keyGray = -1;
                            if (PoiConfig.DefaultDoKey)
                            {
                                keyGray = KeyBoardDLL.CM_CalculateKey(rect, poiPointParam.KeyOutMOVE, poiPointParam.KeyThreadV, PoiConfig.SaveFolderPath + $"\\{rectangle.Text}",  keygray1 ,ref Keygraynum);
                                
                                if (Calibratiohandle != IntPtr.Zero)
                                {
                                    byte[] byteArray = BitConverter.GetBytes(keygray1[0]);
                                    byte[] byteArray1 = new byte[4];
                                    cvCameraCSLib.CM_SCGD_SDP_Luminance(Calibratiohandle, 1, 1, 16, 1, byteArray, byteArray1, new float[] { PoiConfig.Exp , PoiConfig.Exp , PoiConfig.Exp });
                                    keyGray =(float) BitConverter.ToSingle(byteArray1);
                                }




                                keyGray = (float)(keyGray * poiPointParam.KeyScale);
                                if (poiPointParam.Area != 0)
                                {
                                    poiPointParam.Brightness = keyGray / poiPointParam.Area;
                                    poiPointParam.Brightness = poiPointParam.Brightness * Keygraynum * AlgorithmKBConfig.Instance.KBLVSacle;
                                }
                                else
                                {
                                    poiPointParam.Brightness = keyGray;
                                    poiPointParam.Brightness = poiPointParam.Brightness * Keygraynum * AlgorithmKBConfig.Instance.KBLVSacle;
                                }

                            }
                            string name = rectangle.Text;
                            if (name.Contains(',') || name.Contains('\"'))
                            {
                                name = $"\"{name.Replace("\"", "\"\"")}\"";
                            }
                            writer.WriteLine($"{name},{rect},{haloGray},{haloGraynum},{keyGray},{Keygraynum},{keygray1[0]},{keygray1[1]},{keygray1[2]}");
                        }
                        catch
                        {

                        }

                    }
                }
            }

            IntPtr pData = Marshal.AllocHGlobal(width * height * channels);

            int rw = 0; int rh = 0; int rBpp = 0; int rChannel = 0;

            byte[] pDst1 = new byte[image.Cols * image.Rows * bpp *channels];

            int result = KeyBoardDLL.CM_GetKeyBoardResult(ref rw, ref rh, ref rBpp, ref rChannel, pDst1);
            OpenCvSharp.Mat mat;
            if (rBpp == 8)
            {
                mat = OpenCvSharp.Mat.FromPixelData(rh, rw, OpenCvSharp.MatType.CV_8UC(rChannel), pDst1);

            }
            else
            {
                mat = OpenCvSharp.Mat.FromPixelData(rh, rw, OpenCvSharp.MatType.CV_16UC(rChannel), pDst1);
            }
            IsInitialKB = true;
            string Imageresult = $"{PoiConfig.SaveFolderPath}\\{Path.GetFileName(imgFileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.tif";
            mat.SaveImage(Imageresult);

            ImageView imageView = new();
            Window window = new() { Title = Properties.Resources.QuickPreview };
            if (Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.GetActiveWindow();
            }
            window.Content = imageView;
            imageView.OpenImage(Imageresult);
            window.Show();
            if (Application.Current.MainWindow != window)
            {
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ImageViewModel.ClearImage();
                }));
            }

        }

        private void CreateCopy_Click(object sender, RoutedEventArgs e)
        {
            int index = TemplateKB.Params.IndexOf(TemplateKB.Params.First(x=>x.Value == TemplateJsonKBParam));

            int oldindex = TemplateKB.Params.Count;
            TemplateKB templateKB = new TemplateKB();
            if (templateKB.CopyTo(index))
            {
                templateKB.OpenCreate();
            }
            int newindex = TemplateKB.Params.Count;
            if (newindex!= oldindex)
            {
                this.Close();
                new EditPoiParam1(TemplateKB.Params[newindex - 1].Value).ShowDialog();
            }
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
                    List<MeasureImgResultModel> resultMaster = null;
                    if (masterId > 0)
                    {
                        resultMaster = new List<MeasureImgResultModel>();
                        MeasureImgResultModel model = MeasureImgResultDao.Instance.GetById(masterId);
                        if (model != null)
                            resultMaster.Add(model);
                    }
                    if (resultMaster != null)
                    {
                        foreach (MeasureImgResultModel result in resultMaster)
                        {
                            try
                            {
                                if (result.FileUrl != null)
                                {
                                    OpenImage(new NetFileUtil().OpenLocalCVFile(result.FileUrl));
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
    }

}
