#pragma warning disable CS0169,CS8601,CS8602,CS8604,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Services;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Sorts;
using ColorVision.Util.Draw.Rectangle;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Engine.Templates.POI
{
    public class EditPoiParam1Config : Common.MVVM.ViewModelBase, IConfig
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
                saveFileDialog.Title = ColorVision.Engine.Properties.Resources.Engine_Dlg_SelectCalibrationFile;
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.RestoreDirectory = true;
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
                folderBrowserDialog.SelectedPath = SaveFolderPath;
                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SaveFolderPath = folderBrowserDialog.SelectedPath;
                }
            }
        }

        public bool DefaultDoKey { get => _DefaultDoKey; set { _DefaultDoKey = value; OnPropertyChanged(); } }
        private bool _DefaultDoKey = true;
        public bool DefaultDoHalo { get => _DefaultDoHalo; set { _DefaultDoHalo = value; OnPropertyChanged(); } }
        private bool _DefaultDoHalo;

        /// <summary>
        /// 校正文件
        /// </summary>
        public string LuminFile { get => _LuminFile; set { _LuminFile = value; OnPropertyChanged(); } }
        private string _LuminFile = string.Empty;

        public int SaveProcessData { get => _saveProcessData; set { _saveProcessData = value; OnPropertyChanged(); } }
        private int _saveProcessData;

        public float Exp { get => _Exp; set { _Exp = value; OnPropertyChanged(); } }
        private float _Exp = 600;

        public string SaveFolderPath { get => _SaveFolderPath; set { _SaveFolderPath = value; OnPropertyChanged(); } }
        private string _SaveFolderPath =Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));

    }

    public class KBPoiVMParam : Common.MVVM.ViewModelBase
    {
        /// <summary>
        /// 结果缩放
        /// </summary>
        public double KeyScale { get => _KeyScale; set { if (_KeyScale == value) return; _KeyScale = value; OnPropertyChanged(); } }
        private double _KeyScale = 1;
        /// <summary>
        /// 结果缩放
        /// </summary>
        public double HaloScale { get => _HaloScale; set { if (_HaloScale == value) return; _HaloScale = value; OnPropertyChanged(); } }
        private double _HaloScale = 1;

        public int HaloThreadV { get => _HaloThreadV; set { if (_HaloThreadV == value) return; _HaloThreadV = value; OnPropertyChanged(); } }
        private int _HaloThreadV = 500;

        public int KeyThreadV { get => _KeyThreadV; set { if (_KeyThreadV == value) return; _KeyThreadV = value; OnPropertyChanged(); } }
        private int _KeyThreadV = 3000;

        public int HaloOutMOVE { get => _HaloOutMOVE; set { if (_HaloOutMOVE == value) return; _HaloOutMOVE = value; OnPropertyChanged(); } }
        private int _HaloOutMOVE = 20;

        public int KeyOutMOVE { get => _KeyOutMOVE; set { if (_KeyOutMOVE == value) return; _KeyOutMOVE = value; OnPropertyChanged(); } }
        private int _KeyOutMOVE = 5;

        public int KeyOffsetX { get => _KeyOffsetX; set { if (_KeyOffsetX == value) return; _KeyOffsetX = value; OnPropertyChanged(); } }
        private int _KeyOffsetX;
        public int KeyOffsetY { get => _KeyOffsetY; set { if (_KeyOffsetY == value) return; _KeyOffsetY = value; OnPropertyChanged(); } }
        private int _KeyOffsetY;

        public int HaloOffsetX { get => _HaloOffsetX; set { if (_HaloOffsetX == value) return; _HaloOffsetX = value; OnPropertyChanged(); } }
        private int _HaloOffsetX;

        public int HaloSize { get => _HaloSize; set { if (_HaloSize == value) return; _HaloSize = value; OnPropertyChanged(); } }
        private int _HaloSize;


        public int HaloOffsetY { get => _HaloOffsetY; set { if (_HaloOffsetY == value) return; _HaloOffsetY = value; OnPropertyChanged(); } }
        private int _HaloOffsetY;

        /// <summary>
        /// 面积
        /// </summary>
        public double Area { get => _Area; set { if (_Area == value) return; _Area = value; OnPropertyChanged(); } }
        private double _Area = 1;

        /// <summary>
        /// 辉度
        /// </summary>
        public double Brightness { get => _Brightness; set { if (_Brightness == value) return; _Brightness = value; OnPropertyChanged(); } }
        private double _Brightness;
    }






    public partial class EditPoiParam1 : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EditPoiParam1));
        private string TagName { get; set; } = "P_";
        private readonly HashSet<RectangleTextProperties> _dirtyKeyboardKeys = new();
        private readonly DispatcherTimer _keyboardRecalculationTimer;
        private bool _isClosing;
        private bool _isApplyingKeyboardResults;
        private IntPtr _keyboardCalibrationHandle;
        private int _keyboardCalibrationResourceId = -1;
        private string _keyboardCalibrationCameraPath = string.Empty;

        public KBJson KBJson { get; set; }
        public KBPoiConfig PoiConfig => KBJson.PoiConfig;

        public TemplateJsonKBParam TemplateJsonKBParam { get; set; }

        public EditPoiParam1(TemplateJsonKBParam poiParam) 
        {
            TemplateJsonKBParam = poiParam;
            KBJson = TemplateJsonKBParam.KBJson;
            InitializeComponent();
            _keyboardRecalculationTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(100),
                DispatcherPriority.Background,
                (_, _) => FlushPendingKeyboardRecalculation(),
                Dispatcher);
            _keyboardRecalculationTimer.Stop();
            PoiImageViewComponent.SetIsTemplateSelectorEnabled(ImageView, false);
            this.ApplyCaption();
            this.DelayClearImage((Action)(() => Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                _isClosing = true;
                _keyboardRecalculationTimer.Stop();
                _dirtyKeyboardKeys.Clear();
                ReleaseKeyboardCalibration();
                ImageView?.Dispose();
            }))));
            this.Title = poiParam.Name + "-" + this.Title;
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => ImageView.EditorContext.DrawingVisualLists;
        public List<DrawingVisual> DefaultPoint { get; set; } = new List<DrawingVisual>();

        public Zoombox Zoombox1 => ImageView.Zoombox1;

        public DrawCanvas ImageShow => ImageView.ImageShow;

        private async void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = KBJson;
            ImageView.ImageEditMode = true;
            ImageView.EditorContext.SelectionVisual.SelectVisualChanged += (s, e) =>
            {
                ListView1.SelectedItem = e;
                ListView1.ScrollIntoView(e);
            };
            if (ImageView.IEditorToolFactory.GetIEditorTool<CircleManager>() is CircleManager circleManager)
                circleManager.Config.IsContinuous = true;
            if (ImageView.IEditorToolFactory.GetIEditorTool<RectangleManager>() is RectangleManager rectangleManager)
                rectangleManager.Config.IsContinuous = true;

            ListView1.ContextMenu = new ContextMenu();
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => GetSelectedDrawingVisualIndex() > 0);
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveSelectedDrawingVisualDown());
            MoveToTopCommand = new RelayCommand(a => MoveToTop(), a => GetSelectedDrawingVisualIndex() > 0);
            MoveToBottomCommand = new RelayCommand(a => MoveToBottom(), a => CanMoveSelectedDrawingVisualDown());


            ComboBoxBorderType1.ItemsSource = from e1 in Enum.GetValues<GraphicBorderType>().Cast<GraphicBorderType>()  select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType1.SelectedIndex = 0;

            ComboBoxBorderType11.ItemsSource = from e1 in Enum.GetValues<GraphicBorderType>().Cast<GraphicBorderType>() select new KeyValuePair<GraphicBorderType, string>(e1, e1.ToDescription());
            ComboBoxBorderType11.SelectedIndex = 0;

            ComboBoxBorderType2.ItemsSource = from e1 in Enum.GetValues<DrawingGraphicPosition>().Cast<DrawingGraphicPosition>() select new KeyValuePair<DrawingGraphicPosition, string>(e1, e1.ToDescription());
            ComboBoxBorderType2.SelectedIndex = 0;


            ListView1.ItemsSource = DrawingVisualLists;


            ImageShow.VisualsAdd += (s, e) =>
            {
                if (PoiConfig.IsUserDraw)
                {
                    PoiConfig.IsUserDraw = false;
                    if (e.Visual is DVCircleText dVCircleText)
                    {
                        PoiConfig.CenterX = (int)dVCircleText.Attribute.Center.X;
                        PoiConfig.CenterY = (int)dVCircleText.Attribute.Center.Y;
                        PoiConfig.AreaCircleRadius = (int)dVCircleText.Attribute.Radius;
                        RenderPoiConfig();
                        dVCircleText.Attribute.PropertyChanged += (s1, e1) =>
                        {
                            PoiConfig.CenterX = (int)dVCircleText.Attribute.Center.X;
                            PoiConfig.CenterY = (int)dVCircleText.Attribute.Center.Y;
                            PoiConfig.AreaCircleRadius = (int)dVCircleText.Attribute.Radius;
                            RenderPoiConfig();
                        };
                    }
                    if (e.Visual is DVRectangleText dVRectangleText)
                    {
                        UpdateAreaFromRect(dVRectangleText.Attribute.Rect);
                        dVRectangleText.Attribute.PropertyChanged += (s1, e1) =>
                        {
                            UpdateAreaFromRect(dVRectangleText.Attribute.Rect);
                        };
                    }
                    ImageShow.RemoveVisualCommand((System.Windows.Media.Visual)e.Visual);
                    return;
                }

                if (e.Visual is IDrawingVisual visual)
                {
                    if (visual.BaseAttribute.Param == null)
                    {
                        if (visual.BaseAttribute is RectangleTextProperties rectangle)
                        {
                            KBPoiVMParam poiPointParam = new KBPoiVMParam();
                            visual.BaseAttribute.Param = poiPointParam;
                            AttachKeyboardParamChangeHandler(rectangle, poiPointParam);

                        }

                    }
                }


            };

            bool loadExistingPoi = KBJson.Height != 0 && KBJson.Width != 0;
            if (loadExistingPoi)
            {
                if (File.Exists(PoiConfig.BackgroundFilePath))
                    ImageView.OpenImage(PoiConfig.BackgroundFilePath);
                else
                    CreateImage(KBJson.Width, KBJson.Height, Colors.White, false);

                RenderPoiConfig();
            }
            else
            {
                KBJson.Width = 400;
                KBJson.Height = 300;
                CreateImage(KBJson.Width, KBJson.Height, Colors.White, false);
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
                EditPoiParam1Config.Instance.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                EditPoiParam1Config.Instance.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }

            if (loadExistingPoi)
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                if (!_isClosing)
                {
                    PoiParamToDrawingVisual(KBJson);
                }
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
                            DrawingVisualLists.Sort((x, y) => item.IsSortD ? y.BaseAttribute.Id.CompareTo(x.BaseAttribute.Id) : x.BaseAttribute.Id.CompareTo(y.BaseAttribute.Id));
                        }
                    }
                }
            }
        }

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
            openFileDialog.Filter = ServicesHelper.ImageFileDialogFilter;
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
            CreateImage(KBJson.Width, KBJson.Height, Colors.White,false);

        }

        private bool Init;
        private void CreateImage(int width, int height, Color color,bool IsClear = true)
        {
            ImageView.SetLayerController(null);
            ImageView.SetImageSource(ImageUtils.CreateSolidColorDrawing(width, height, color), false, false);
            ImageView.UpdateZoomAndScale();
            InitPoiConfigValue(width, height);
            if (IsClear)
            {
                ImageShow.Clear();
                DrawingVisualLists.Clear();
            }
            PoiConfig.BackgroundFilePath = null;
        }


        private void InitPoiConfigValue(int width,int height)
        {
            Application.Current.Dispatcher.Invoke(() => PoiConfig.IsShowPoiConfig = true);
            RenderPoiConfig();
        }

        private Dictionary<IDrawingVisual, int> DBIndex = new Dictionary<IDrawingVisual, int>();

        private int No;

        private void PoiParamToDrawingVisual(KBJson poiParam)
        {
            try
            {
                if (poiParam.KBKeyRects.Count > 500)
                {
                    PoiConfig.IsLayoutUpdated = false;
                }

                List<Visual> visuals = new(poiParam.KBKeyRects.Count);
                foreach (var item in poiParam.KBKeyRects)
                {
                    No++;
                    DVRectangleText Rectangle = new();
                    Rectangle.Attribute.Rect = new System.Windows.Rect(item.X , item.Y, item.Width, item.Height);
                    Rectangle.Attribute.Brush = Brushes.Transparent;
                    Rectangle.Attribute.Pen = new Pen(Brushes.Red,  (double)item.Width / 30);
                    Rectangle.Attribute.Id = No;
                    Rectangle.Attribute.Text = item.Name;
                    Rectangle.Attribute.Name = No.ToString();

                    KBPoiVMParam poiPointParam = new KBPoiVMParam()
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
                    AttachKeyboardParamChangeHandler(Rectangle.Attribute, poiPointParam);

                    Rectangle.Attribute.Param = poiPointParam;




                    Rectangle.Render();
                    visuals.Add(Rectangle);
                    DBIndex.Add(Rectangle, No);
                }
                ImageShow.AddVisuals(visuals);
            }
            catch (Exception ex)
            {
                log.Error("加载键盘关注点视图失败", ex);
            }
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            if (!ImageUtils.TryGetImageSize(ImageShow.Source, out int imageWidth, out int imageHeight)) return;

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

                    if (PoiConfig.AreaCircleNum > 1000)
                    {
                        PoiConfig.IsLayoutUpdated = false;
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
                    double startD = imageHeight - PoiConfig.CenterY - Height / 2;
                    double startL = PoiConfig.CenterX - Width / 2;
                    double startR = imageWidth - PoiConfig.CenterX - Width / 2;

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
                                        startU += PoiConfig.DefaultRectWidth / 2;
                                        startD += PoiConfig.DefaultRectWidth / 2;
                                        startL += PoiConfig.DefaultRectHeight / 2;
                                        startR += PoiConfig.DefaultRectHeight / 2;
                                        break;
                                    case DrawingGraphicPosition.External:
                                        startU -= PoiConfig.DefaultRectWidth / 2;
                                        startD -= PoiConfig.DefaultRectWidth / 2;
                                        startL -= PoiConfig.DefaultRectHeight / 2;
                                        startR -= PoiConfig.DefaultRectHeight / 2;
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


                    double StepRow = (rows > 1) ? (imageHeight - startD - startU) / (rows - 1) : 0;
                    double StepCol = (cols > 1) ? (imageWidth - startL - startR) / (cols - 1) : 0;


                    int all = rows * cols;
                    if (all > 1000)
                    {
                        PoiConfig.IsLayoutUpdated = false;
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
                                    break;
                                case GraphicTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(x1 - (double)PoiConfig.DefaultRectWidth / 2, y1 - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i * cols + j + 1;
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
                                case GraphicTypes.Circle:
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
                                    break;
                                case GraphicTypes.Rect:
                                    DVRectangleText Rectangle = new();
                                    Rectangle.Attribute.Rect = new System.Windows.Rect(point.X - PoiConfig.DefaultRectWidth / 2, point.Y - PoiConfig.DefaultRectHeight / 2, PoiConfig.DefaultRectWidth, PoiConfig.DefaultRectHeight);
                                    Rectangle.Attribute.Brush = Brushes.Transparent;
                                    Rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)PoiConfig.DefaultRectWidth / 30);
                                    Rectangle.Attribute.Id = start + i * cols + j + 1;
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
            if (sender is ListView { SelectedItem: ISelectVisual drawingVisual })
            {
                ImageView.EditorContext.SelectionVisual.SetRender(drawingVisual);
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
                        if (selectedItem is Visual visual)
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
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Up)
            {
                MoveUp();
                e.Handled = true;
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Down)
            {
                MoveDown();
                e.Handled = true;
            }
            else if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.D)
            {
                BatchFillDown();
                e.Handled = true;
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
                ImageShow.RemoveOverlayVisual(drawingVisualDatum);
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
                        ImageShow.AddOverlayVisual(drawingVisualDatum);
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
                        ImageShow.AddOverlayVisual(drawingVisualDatum);
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
                        ImageShow.AddOverlayVisual(drawingVisualDatum);
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
                        ImageShow.AddOverlayVisual(drawingVisualDatum);

                        break;
                    default:
                        break;
                }

            }
        }

        private void SavePoiParam()
        {
            FlushPendingKeyboardRecalculation();
            KBJson.KBKeyRects.Clear();
            Rect rect = new Rect(0, 0, KBJson.Width, KBJson.Height);
            foreach (var item in DrawingVisualLists)
            {
                int index = DBIndex.TryGetValue(item, out int value) ? value : 0;

                BaseProperties drawAttributeBase = item.BaseAttribute;
               if (drawAttributeBase is RectangleTextProperties rectangle)
                {
                    if (rectangle.Rect.X <= 0)
                    {
                        MessageBox.Show($"{rectangle.Text} X为0{rectangle.Rect.X}");
                        return;
                    }
                    if (rectangle.Rect.Y <= 0)
                    {
                        MessageBox.Show($"{rectangle.Text} Y为0{rectangle.Rect.Y}");
                        return;
                    }
                    if (rectangle.Rect.Width <= 0)
                    {
                        MessageBox.Show($"{rectangle.Text} width为0{rectangle.Rect.Width}");
                        return;
                    }
                    if (rectangle.Rect.Height <= 0)
                    {
                        MessageBox.Show($"{rectangle.Text} Height为0{rectangle.Rect.Height}");
                        return;
                    }

                    Rect rect1 = new Rect(rectangle.Rect.X, rectangle.Rect.Y, rectangle.Rect.Width, rectangle.Rect.Height);
                    if (!rect.Contains(rect1))
                        continue;
                    PoiPoint poiParamData = new()
                    {
                        Id = index,
                        Name = rectangle.Text,
                        PointType = PoiShape.Rect,
                        PixX = rectangle.Rect.X + rectangle.Rect.Width / 2,
                        PixY = rectangle.Rect.Y + rectangle.Rect.Height / 2,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                    };
                    KBKeyRect kBKeyRect = new KBKeyRect();
                    if (rectangle.Param is not KBPoiVMParam param)
                    {
                        param = new KBPoiVMParam();
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
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            Db.Updateable(TemplateJsonKBParam.TemplateJsonModel).ExecuteCommand();

            MessageBox.Show(WindowHelpers.GetActiveWindow(), "保存成功", "ColorVision");
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            SavePoiParam();
        }
        private void Service_Click(object sender, RoutedEventArgs e)
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var recentItems = Db.Queryable<MeasureResultImgModel>().Where(x=>x.FileType == 2)
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
                        PoiConfig.BackgroundFilePath = item.FileUrl;
                        return;
                    }
                }
                    MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailedNoPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed, ex.Message);
            }


        }


        private ObservableCollection<MeasureResultImgModel> MeasureImgResultModels = new();
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
                    if (MeasureImgResultModels[ComboBoxImg.SelectedIndex] is MeasureResultImgModel model && model.FileUrl != null)
                    {
                        ImageView.OpenImage(model.FileUrl);
                        PoiConfig.BackgroundFilePath = model.FileUrl;
                    }
                    else
                    {
                        MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed, ex.Message);
                }
            }
           
        }

        private void ButtonImportMarin_Click(object sender, RoutedEventArgs e)
        {
            ImportMarinPopup.IsOpen = true;
        }

        private void ButtonImportMarinSetting(object sender, RoutedEventArgs e)
        {
            if (ImageUtils.TryGetImageSize(ImageShow.Source, out int imageWidth, out int imageHeight))
            {
                double startU = ParseDoubleOrDefault(TextBoxUp1.Text);
                double startD = ParseDoubleOrDefault(TextBoxDown1.Text);
                double startL = ParseDoubleOrDefault(TextBoxLeft1.Text);
                double startR = ParseDoubleOrDefault(TextBoxRight1.Text);

                if (ComboBoxBorderType1.SelectedItem is KeyValuePair<GraphicBorderType, string> KeyValue && KeyValue.Key == GraphicBorderType.Relative)
                {
                    startU = imageHeight * startU / 100;
                    startD = imageHeight * startD / 100;
                    startL = imageWidth * startL / 100;
                    startR = imageWidth * startR / 100;
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
            if (ImageShow.Source != null)
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


        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            FocusPointGrid.Height = FocusPointRowDefinition.ActualHeight;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            new EidtPoiDataGridForm((ObservableCollection<IDrawingVisual>)DrawingVisualLists).Show();           
        }

        private void DetectKeyRegions_Click(object sender, RoutedEventArgs e)
        {
            string configJson = PoiConfig.DetectKeyRegionsConfig.ToJsonN();
            ImageFrameLease? acquiredLease = ImageView.AcquireImageFrame();
            if (acquiredLease == null)
            {
                MessageBox.Show("请先加载图像", "ColorVision");
                return;
            }

            ImageFrameLease lease = acquiredLease;
            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                int length;
                IntPtr resultPtr;
                using (lease)
                {
                    length = OpenCVMediaHelper.M_DetectKeyRegions(lease.Image, new RoiRect(), configJson, out resultPtr);
                }
                if (length > 0)
                {
                    string result = OpenCVMediaHelper.PtrToStringAnsiAndFree(resultPtr);
                    log.Info("DetectKeyRegions result: " + result);

                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (!ImageView.IsCurrentImageRevision(revision))
                            return;

                        try
                        {
                            var jObj = Newtonsoft.Json.Linq.JObject.Parse(result);
                            var keyRegions = jObj["KeyRegions"].ToObject<List<MRect>>();
                            int count = jObj["Count"].ToObject<int>();

                            if (keyRegions == null || keyRegions.Count == 0)
                            {
                                MessageBox.Show("未检测到按键区域，请调整参数后重试", "ColorVision");
                                return;
                            }

                            int start = DrawingVisualLists.Count;
                            int idx = 0;
                            foreach (var region in keyRegions)
                            {
                                idx++;
                                DVRectangleText rectangle = new DVRectangleText();
                                rectangle.Attribute.Rect = new System.Windows.Rect(region.X, region.Y, region.Width, region.Height);
                                rectangle.Attribute.Brush = Brushes.Transparent;
                                rectangle.Attribute.Pen = new Pen(Brushes.Red, (double)Math.Max(region.Width, region.Height) / 30);
                                rectangle.Attribute.Id = start + idx;
                                rectangle.Attribute.Name = (start + idx).ToString();
                                rectangle.Attribute.Text = string.Format("{0}{1}", TagName, rectangle.Attribute.Name);

                                KBPoiVMParam poiPointParam = new KBPoiVMParam();
                                AttachKeyboardParamChangeHandler(rectangle.Attribute, poiPointParam);
                                rectangle.Attribute.Param = poiPointParam;

                                rectangle.Render();
                                ImageShow.AddVisualCommand(rectangle);
                            }

                            MessageBox.Show($"成功检测到 {count} 个按键区域", "ColorVision");
                        }
                        catch (Exception ex)
                        {
                            log.Error("DetectKeyRegions parse error", ex);
                            MessageBox.Show($"解析检测结果失败: {ex.Message}", "ColorVision");
                        }
                    });
                }
                else
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (!ImageView.IsCurrentImageRevision(revision))
                            return;

                        MessageBox.Show($"按键区域检测失败(错误码: {length})，请调整参数后重试", "ColorVision");
                    });
                }
            });
        }


        public ImageSource ViewBitmapSource => ImageView.ViewBitmapSource;


        private void FindLuminousArea_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            {
                string re = PoiConfig.FindLuminousArea.ToJsonN();
                ImageFrameLease? acquiredLease = ImageView.AcquireImageFrame();
                if (acquiredLease != null)
                {
                    ImageFrameLease lease = acquiredLease;
                    long revision = lease.Revision;
                    _ = Task.Run(() =>
                    {
                        int length;
                        IntPtr resultPtr;
                        using (lease)
                        {
                            length = OpenCVMediaHelper.M_FindLuminousArea(lease.Image, new RoiRect(), re, out resultPtr);
                        }
                        if (length > 0)
                        {
                            string result = OpenCVMediaHelper.PtrToStringAnsiAndFree(resultPtr);
                            Console.WriteLine("Result: " + result);
                            MRect rect = Newtonsoft.Json.JsonConvert.DeserializeObject<MRect>(result);

                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                if (!ImageView.IsCurrentImageRevision(revision))
                                    return;

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
                }
                else
                {
                    MessageBox.Show("请先加载实际图像", "ColorVision");
                }
            }));
        }

        private void SetDefault_Click(object sender, RoutedEventArgs e)
        {

        }

        private void UpdateAreaFromRect(Rect rect)
        {
            if (PoiConfig.PointType == GraphicTypes.Quadrilateral)
            {
                PoiConfig.Polygon1X = (int)rect.X;
                PoiConfig.Polygon1Y = (int)rect.Y;
                PoiConfig.Polygon2X = (int)(rect.X + rect.Width);
                PoiConfig.Polygon2Y = (int)rect.Y;
                PoiConfig.Polygon3X = (int)(rect.X + rect.Width);
                PoiConfig.Polygon3Y = (int)(rect.Y + rect.Height);
                PoiConfig.Polygon4X = (int)rect.X;
                PoiConfig.Polygon4Y = (int)(rect.Y + rect.Height);
            }
            else
            {
                PoiConfig.CenterX = (int)(rect.Width / 2 + rect.X);
                PoiConfig.CenterY = (int)(rect.Height / 2 + rect.Y);
                PoiConfig.AreaRectWidth = (int)rect.Width;
                PoiConfig.AreaRectHeight = (int)rect.Height;
            }
            RenderPoiConfig();
        }

        private async void DrawAreaOnImage_Click(object sender, RoutedEventArgs e)
        {
            SelectShapeType shapeType;
            switch (PoiConfig.PointType)
            {
                case GraphicTypes.Circle:
                    shapeType = SelectShapeType.Circle;
                    break;
                case GraphicTypes.Quadrilateral:
                    shapeType = SelectShapeType.Quadrilateral;
                    break;
                case GraphicTypes.Polygon:
                    shapeType = SelectShapeType.Polygon;
                    break;
                default:
                    shapeType = SelectShapeType.Rectangle;
                    break;
            }

            var result = await ImageView.BeginSelectAsync(shapeType);
            if (result == null) return;

            if (result.ShapeType == SelectShapeType.Circle)
            {
                PoiConfig.CenterX = (int)result.Center.X;
                PoiConfig.CenterY = (int)result.Center.Y;
                PoiConfig.AreaCircleRadius = (int)result.Radius;
            }
            else if ((result.ShapeType == SelectShapeType.Quadrilateral || result.ShapeType == SelectShapeType.Polygon) && result.Points != null)
            {
                if (PoiConfig.PointType == GraphicTypes.Quadrilateral && result.Points.Count >= 4)
                {
                    PoiConfig.Polygon1X = (int)result.Points[0].X;
                    PoiConfig.Polygon1Y = (int)result.Points[0].Y;
                    PoiConfig.Polygon2X = (int)result.Points[1].X;
                    PoiConfig.Polygon2Y = (int)result.Points[1].Y;
                    PoiConfig.Polygon3X = (int)result.Points[2].X;
                    PoiConfig.Polygon3Y = (int)result.Points[2].Y;
                    PoiConfig.Polygon4X = (int)result.Points[3].X;
                    PoiConfig.Polygon4Y = (int)result.Points[3].Y;
                }
                else
                {
                    PoiConfig.Polygons.Clear();
                    foreach (var pt in result.Points)
                        PoiConfig.Polygons.Add(new PolygonPoint(pt.X, pt.Y));
                }
            }
            else
            {
                UpdateAreaFromRect(result.Rect);
            }
            PoiConfig.IsShowPoiConfig = true;
            RenderPoiConfig();
        }

        private void Cal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CalculateKeyboardKeys();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetKBLocal_Click(object sender, RoutedEventArgs e)
        {

        }

        private bool _hasKeyboardResult;

        private void AttachKeyboardParamChangeHandler(RectangleTextProperties rectangle, KBPoiVMParam param)
        {
            rectangle.PropertyChanged += (_, e) =>
            {
                if (!_isApplyingKeyboardResults && _hasKeyboardResult &&
                    e.PropertyName == nameof(RectangleTextProperties.Rect))
                {
                    QueueKeyboardRecalculation(rectangle);
                }
            };
            param.PropertyChanged += (_, e) =>
            {
                if (_isApplyingKeyboardResults || !_hasKeyboardResult || !IsKeyboardCalculationProperty(e.PropertyName))
                    return;
                QueueKeyboardRecalculation(rectangle);
            };
        }

        private static bool IsKeyboardCalculationProperty(string propertyName)
        {
            return propertyName == nameof(KBPoiVMParam.Area) ||
                propertyName == nameof(KBPoiVMParam.KeyScale) ||
                propertyName == nameof(KBPoiVMParam.HaloScale) ||
                propertyName == nameof(KBPoiVMParam.KeyOutMOVE) ||
                propertyName == nameof(KBPoiVMParam.KeyThreadV) ||
                propertyName == nameof(KBPoiVMParam.KeyOffsetX) ||
                propertyName == nameof(KBPoiVMParam.KeyOffsetY) ||
                propertyName == nameof(KBPoiVMParam.HaloOutMOVE) ||
                propertyName == nameof(KBPoiVMParam.HaloThreadV) ||
                propertyName == nameof(KBPoiVMParam.HaloOffsetX) ||
                propertyName == nameof(KBPoiVMParam.HaloOffsetY) ||
                propertyName == nameof(KBPoiVMParam.HaloSize);
        }

        private void QueueKeyboardRecalculation(RectangleTextProperties rectangle)
        {
            if (!_hasKeyboardResult)
                return;
            _dirtyKeyboardKeys.Add(rectangle);
            _keyboardRecalculationTimer.Stop();
            _keyboardRecalculationTimer.Start();
        }

        private void FlushPendingKeyboardRecalculation()
        {
            _keyboardRecalculationTimer.Stop();
            if (_dirtyKeyboardKeys.Count == 0 || !_hasKeyboardResult)
                return;

            RectangleTextProperties[] dirtyKeys = _dirtyKeyboardKeys.ToArray();
            _dirtyKeyboardKeys.Clear();
            CalculateKeyboardKeys(false, dirtyKeys);
        }

        private void CalculateKeyboardKeys(bool show = true, IReadOnlyCollection<RectangleTextProperties> requestedKeys = null)
        {
            bool isIncremental = requestedKeys != null;
            if (!isIncremental)
            {
                _keyboardRecalculationTimer.Stop();
                _dirtyKeyboardKeys.Clear();
            }

            using ImageFrameLease? lease = ImageView.AcquireImageFrame();
            if (lease == null)
            {
                MessageBox.Show("请先加载图像", "ColorVision");
                return;
            }

            HImage image = lease.Image;
            if ((image.depth != 8 && image.depth != 16) ||
                (image.channels != 1 && image.channels != 3 && image.channels != 4))
            {
                MessageBox.Show($"KB 计算仅支持 8/16 位、1/3/4 通道图像，当前为 {image.depth} 位 {image.channels} 通道。", "ColorVision");
                return;
            }
            if (!PoiConfig.DefaultDoKey && !PoiConfig.DefaultDoHalo)
            {
                MessageBox.Show("请至少启用 CalKey 或 CalHalo。", "ColorVision");
                return;
            }

            List<(RectangleTextProperties Rectangle, KBPoiVMParam Param)> allKeyVisuals = new();
            foreach (IDrawingVisual drawingVisual in DrawingVisualLists)
            {
                if (drawingVisual.BaseAttribute is RectangleTextProperties rectangle &&
                    rectangle.Param is KBPoiVMParam param)
                {
                    allKeyVisuals.Add((rectangle, param));
                }
            }
            List<(RectangleTextProperties Rectangle, KBPoiVMParam Param)> keyVisuals = requestedKeys == null
                ? allKeyVisuals
                : allKeyVisuals.Where(item => requestedKeys.Contains(item.Rectangle)).ToList();
            if (keyVisuals.Count == 0)
            {
                if (!isIncremental)
                    MessageBox.Show("没有可计算的按键矩形。", "ColorVision");
                return;
            }

            JArray keys = new();
            for (int index = 0; index < keyVisuals.Count; index++)
            {
                (RectangleTextProperties rectangle, KBPoiVMParam param) = keyVisuals[index];
                int haloWidth = param.HaloSize > 0 ? param.HaloSize : Math.Max(1, param.HaloOutMOVE);
                int haloGap = Math.Max(0, param.HaloOutMOVE - haloWidth);
                keys.Add(new JObject
                {
                    ["id"] = index + 1,
                    ["name"] = rectangle.Text,
                    ["rect"] = new JObject
                    {
                        ["x"] = (int)rectangle.Rect.X,
                        ["y"] = (int)rectangle.Rect.Y,
                        ["width"] = (int)rectangle.Rect.Width,
                        ["height"] = (int)rectangle.Rect.Height
                    },
                    ["calculateKey"] = PoiConfig.DefaultDoKey,
                    ["calculateHalo"] = PoiConfig.DefaultDoHalo,
                    ["keyOffsetX"] = param.KeyOffsetX,
                    ["keyOffsetY"] = param.KeyOffsetY,
                    ["haloOffsetX"] = param.HaloOffsetX,
                    ["haloOffsetY"] = param.HaloOffsetY,
                    ["keyInsetPixels"] = Math.Max(0, param.KeyOutMOVE),
                    ["haloGapPixels"] = haloGap,
                    ["haloWidthPixels"] = Math.Max(1, haloWidth),
                    ["keyValidMin"] = NormalizeKeyboardThreshold(param.KeyThreadV),
                    ["haloValidMin"] = NormalizeKeyboardThreshold(param.HaloThreadV)
                });
            }

            JObject config = new()
            {
                ["gray"] = new JObject
                {
                    ["mode"] = "luminance",
                    ["validMin"] = 0.0,
                    ["validMax"] = 1.0
                },
                ["minimumValidPixels"] = 1,
                ["excludeKeyRectsFromHalo"] = true,
                ["keys"] = keys
            };
            if (isIncremental)
            {
                config["haloExclusionRects"] = new JArray(allKeyVisuals.Select(item => new JObject
                {
                    ["x"] = (int)item.Rectangle.Rect.X,
                    ["y"] = (int)item.Rectangle.Rect.Y,
                    ["width"] = (int)item.Rectangle.Rect.Width,
                    ["height"] = (int)item.Rectangle.Rect.Height
                }));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            int length = OpenCVMediaHelper.M_AnalyzeKeyboardHalo(
                image, new RoiRect(), config.ToString(Formatting.None), out IntPtr resultPtr);
            long nativeElapsed = stopwatch.ElapsedMilliseconds;
            if (length <= 0 || resultPtr == IntPtr.Zero)
            {
                MessageBox.Show($"KB 计算失败，错误码：{length}", "ColorVision");
                return;
            }

            string resultJson = OpenCVMediaHelper.PtrToStringUtf8AndFree(resultPtr);
            JObject output = JObject.Parse(resultJson);
            if (output["keys"] is not JArray measurements || measurements.Count != keyVisuals.Count)
                throw new InvalidDataException("KB 计算返回的按键数量与模板不一致。");

            IntPtr calibrationHandle = PoiConfig.DefaultDoKey
                ? GetOrCreateKeyboardCalibrationHandle()
                : IntPtr.Zero;
            float[] calibrationExposure = new[] { PoiConfig.Exp, PoiConfig.Exp, PoiConfig.Exp };
            if (calibrationHandle != IntPtr.Zero)
                calibrationExposure = GetKeyboardCalibrationExposure();

            List<string> csvRows = new()
            {
                "Name,Rect,HaloGray,HaloPixelCount,KeyGray,KeyPixelCount,Status"
            };
            double[] calibratedKeyValues = null;
            bool wasApplyingKeyboardResults = _isApplyingKeyboardResults;
            try
            {
                if (calibrationHandle != IntPtr.Zero &&
                    !TryCalibrateKeyboardMeans(calibrationHandle, measurements, calibrationExposure, out calibratedKeyValues))
                {
                    calibratedKeyValues = null;
                    log.Warn("KB 批量亮度校正失败，本次回退为未校正灰阶。");
                }
                _isApplyingKeyboardResults = true;
                for (int index = 0; index < measurements.Count; index++)
                {
                    JObject measurement = (JObject)measurements[index];
                    (RectangleTextProperties rectangle, KBPoiVMParam param) = keyVisuals[index];
                    bool keyValid = measurement.Value<bool>("keyValid");
                    bool haloValid = measurement.Value<bool>("haloValid");
                    int keyPixelCount = measurement.Value<int>("keyValidPixelCount");
                    int haloPixelCount = measurement.Value<int>("haloValidPixelCount");

                    double keyValue = -1.0;
                    if (PoiConfig.DefaultDoKey && keyValid)
                    {
                        double keyMean = measurement.Value<double>("keyMean");
                        double calibratedValue = calibratedKeyValues?[index] ?? double.NaN;
                        keyValue = double.IsFinite(calibratedValue)
                            ? calibratedValue
                            : keyMean * ushort.MaxValue;
                        keyValue *= param.KeyScale;
                        param.Brightness = param.Area != 0
                            ? keyValue / param.Area * keyPixelCount * KBJson.KBLVSacle
                            : keyValue * keyPixelCount * KBJson.KBLVSacle;
                    }

                    double haloValue = -1.0;
                    if (PoiConfig.DefaultDoHalo && haloValid)
                        haloValue = measurement.Value<double>("haloMean") * ushort.MaxValue * param.HaloScale;

                    measurement["keyValue"] = keyValue;
                    measurement["haloValue"] = haloValue;
                    csvRows.Add(string.Join(",",
                        EscapeCsv(rectangle.Text),
                        EscapeCsv(rectangle.Rect.ToString(CultureInfo.InvariantCulture)),
                        haloValue.ToString("G17", CultureInfo.InvariantCulture),
                        haloPixelCount.ToString(CultureInfo.InvariantCulture),
                        keyValue.ToString("G17", CultureInfo.InvariantCulture),
                        keyPixelCount.ToString(CultureInfo.InvariantCulture),
                        EscapeCsv(measurement.Value<string>("status") ?? string.Empty)));

                    if (!string.Equals(measurement.Value<string>("status"), "ok", StringComparison.Ordinal))
                    {
                        string warnings = string.Join("; ", measurement["warnings"]?.Values<string>() ?? Enumerable.Empty<string>());
                        log.Warn($"KB[{rectangle.Text}] {measurement.Value<string>("status")}: {warnings}");
                    }
                }
            }
            finally
            {
                _isApplyingKeyboardResults = wasApplyingKeyboardResults;
            }

            if (!isIncremental && PoiConfig.SaveProcessData != 0)
            {
                Directory.CreateDirectory(PoiConfig.SaveFolderPath);
                File.WriteAllLines(Path.Combine(PoiConfig.SaveFolderPath, "output.csv"), csvRows, new UTF8Encoding(true));
            }

            _hasKeyboardResult = true;
            stopwatch.Stop();
            log.Info($"KB 计算完成：{image.cols}x{image.rows}/{image.depth}bit/{image.channels}ch, " +
                $"Mode={(isIncremental ? "Incremental" : "Full")}, Keys={keyVisuals.Count}, " +
                $"Native={nativeElapsed}ms, Total={stopwatch.ElapsedMilliseconds}ms");
            if (show)
                ShowKeyboardResultPreview(measurements);
        }

        private static double NormalizeKeyboardThreshold(int threshold)
        {
            int value = Math.Max(0, threshold);
            double maximum = value > byte.MaxValue ? ushort.MaxValue : byte.MaxValue;
            return Math.Clamp(value / maximum, 0.0, 1.0);
        }

        private IntPtr GetOrCreateKeyboardCalibrationHandle()
        {
            if (PoiConfig.CalibrationParams == null ||
                PoiConfig.CalibrationTemplateIndex < 0 ||
                PoiConfig.CalibrationTemplateIndex >= PoiConfig.CalibrationParams.Count ||
                PoiConfig.CalibrationParams[PoiConfig.CalibrationTemplateIndex] is not TemplateModel<CalibrationParam> templateModel)
            {
                ReleaseKeyboardCalibration();
                log.Warn("KB 未配置亮度校正模板，将返回未校正灰阶。");
                return IntPtr.Zero;
            }
            if (string.IsNullOrEmpty(templateModel.Value.Color.Luminance.FilePath))
            {
                ReleaseKeyboardCalibration();
                log.Info("KB 四色校正不支持当前单通道快速校正，将返回未校正灰阶。");
                return IntPtr.Zero;
            }

            int resourceId = templateModel.Value.Color.Luminance.Id;
            if (PoiConfig.DeviceCamera?.PhyCamera is not PhyCamera phyCamera)
            {
                ReleaseKeyboardCalibration();
                return IntPtr.Zero;
            }
            string cameraPath = Path.Combine(phyCamera.Config.FileServerCfg.FileBasePath, phyCamera.Code);
            if (_keyboardCalibrationHandle != IntPtr.Zero &&
                _keyboardCalibrationResourceId == resourceId &&
                string.Equals(_keyboardCalibrationCameraPath, cameraPath, StringComparison.OrdinalIgnoreCase))
            {
                return _keyboardCalibrationHandle;
            }

            var resource = SysResourceDao.Instance.GetById(resourceId);
            if (resource == null)
            {
                ReleaseKeyboardCalibration();
                return IntPtr.Zero;
            }
            string luminFile = Path.Combine(
                cameraPath,
                "cfg",
                resource.Value);
            if (!File.Exists(luminFile))
            {
                ReleaseKeyboardCalibration();
                log.Warn($"找不到 KB 亮度校正文件：{luminFile}");
                return IntPtr.Zero;
            }

            ReleaseKeyboardCalibration();
            IntPtr handle = cvCameraCSLib.CreatCalibrationManage();
            if (handle == IntPtr.Zero ||
                cvCameraCSLib.CM_SetCalibParam(handle, CalibrationType.Luminance, true, luminFile) != 1)
            {
                if (handle != IntPtr.Zero)
                    _ = cvCameraCSLib.ReleaseCalibrationManage(handle);
                log.Warn($"KB 亮度校正加载失败，将返回未校正灰阶：{luminFile}");
                return IntPtr.Zero;
            }

            _keyboardCalibrationHandle = handle;
            _keyboardCalibrationResourceId = resourceId;
            _keyboardCalibrationCameraPath = cameraPath;
            log.Info($"KB 单通道校正：{luminFile}");
            return handle;
        }

        private void ReleaseKeyboardCalibration()
        {
            if (_keyboardCalibrationHandle != IntPtr.Zero)
            {
                _ = cvCameraCSLib.ReleaseCalibrationManage(_keyboardCalibrationHandle);
                _keyboardCalibrationHandle = IntPtr.Zero;
            }
            _keyboardCalibrationResourceId = -1;
            _keyboardCalibrationCameraPath = string.Empty;
        }

        private float[] GetKeyboardCalibrationExposure()
        {
            float fallback = float.IsFinite(PoiConfig.Exp) && PoiConfig.Exp > 0 ? PoiConfig.Exp : 1.0f;
            float[] result = new[] { fallback, fallback, fallback };
            float[] imageExposure = ImageView.Config.GetProperties<float[]>("Exp")
                ?? ImageView.Config.GetProperties<float[]>("exp");
            if (imageExposure == null || imageExposure.Length == 0)
            {
                log.Info($"KB 当前图像没有曝光元数据，使用模板曝光：{fallback:G9}");
                return result;
            }

            for (int index = 0; index < result.Length; index++)
            {
                float value = imageExposure[Math.Min(index, imageExposure.Length - 1)];
                result[index] = float.IsFinite(value) && value > 0 ? value : fallback;
            }
            log.Info($"KB 使用当前 CVRAW/CVCIE 曝光：{string.Join(",", result.Select(value => value.ToString("G9", CultureInfo.InvariantCulture)))}");
            return result;
        }

        private static bool TryCalibrateKeyboardMeans(
            IntPtr calibrationHandle,
            JArray measurements,
            float[] exposure,
            out double[] calibratedValues)
        {
            calibratedValues = new double[measurements.Count];
            Array.Fill(calibratedValues, double.NaN);
            List<int> measurementIndexes = new();
            List<ushort> rawValues = new();
            for (int index = 0; index < measurements.Count; index++)
            {
                if (measurements[index] is not JObject measurement || !measurement.Value<bool>("keyValid"))
                    continue;
                measurementIndexes.Add(index);
                rawValues.Add((ushort)Math.Clamp(
                    Math.Round(measurement.Value<double>("keyMean") * ushort.MaxValue),
                    ushort.MinValue,
                    ushort.MaxValue));
            }
            if (rawValues.Count == 0)
                return true;

            ushort[] rawValueArray = rawValues.ToArray();
            byte[] source = new byte[rawValueArray.Length * sizeof(ushort)];
            Buffer.BlockCopy(rawValueArray, 0, source, 0, source.Length);
            byte[] luminance = new byte[rawValueArray.Length * sizeof(float)];
            bool succeeded = cvCameraCSLib.CM_SCGD_SDP_Luminance(
                calibrationHandle,
                (uint)rawValueArray.Length,
                1,
                16,
                1,
                source,
                luminance,
                exposure);
            if (!succeeded)
                return false;

            for (int index = 0; index < measurementIndexes.Count; index++)
            {
                float value = BitConverter.ToSingle(luminance, index * sizeof(float));
                if (float.IsFinite(value))
                    calibratedValues[measurementIndexes[index]] = value;
            }
            return true;
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
                return value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private void ShowKeyboardResultPreview(JArray measurements)
        {
            if (ImageShow.Source is not ImageSource source)
                return;

            ImageView preview = new();
            preview.SetImageSource(source);
            double thickness = Math.Max(1.0, Math.Max(source.Width, source.Height) / 1500.0);
            foreach (JObject measurement in measurements.OfType<JObject>())
            {
                string name = measurement.Value<string>("name") ?? measurement.Value<int>("id").ToString(CultureInfo.InvariantCulture);
                string text = $"{name}  K:{measurement.Value<double>("keyValue"):F2}  H:{measurement.Value<double>("haloValue"):F2}";
                AddKeyboardPreviewRect(preview, measurement["inputRect"], Brushes.Red, thickness, text);
                AddKeyboardPreviewRect(preview, measurement["innerRect"], Brushes.Yellow, thickness, null);
                AddKeyboardPreviewRect(preview, measurement["haloBounds"], Brushes.Cyan, thickness, null);
            }

            Window window = new()
            {
                Title = Properties.Resources.QuickPreview,
                Content = preview,
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ContentRendered += (_, _) => preview.UpdateZoomAndScale();
            window.Show();
            window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(preview.Clear));
        }

        private static void AddKeyboardPreviewRect(
            ImageView preview,
            JToken rectToken,
            Brush brush,
            double thickness,
            string text)
        {
            if (rectToken is not JObject rectObject)
                return;
            Rect rect = new(
                rectObject.Value<double>("x"),
                rectObject.Value<double>("y"),
                rectObject.Value<double>("width"),
                rectObject.Value<double>("height"));
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            if (string.IsNullOrEmpty(text))
            {
                DVRectangle rectangle = new();
                rectangle.Attribute.Rect = rect;
                rectangle.Attribute.Brush = Brushes.Transparent;
                rectangle.Attribute.Pen = new Pen(brush, thickness);
                rectangle.Render();
                preview.ImageShow.AddVisualCommand(rectangle);
                return;
            }

            DVRectangleText rectangleText = new();
            rectangleText.Attribute.Rect = rect;
            rectangleText.Attribute.Brush = Brushes.Transparent;
            rectangleText.Attribute.Pen = new Pen(brush, thickness);
            rectangleText.Attribute.Text = text;
            rectangleText.Attribute.Foreground = brush;
            rectangleText.Attribute.FontSize = Math.Max(12.0, thickness * 5.0);
            rectangleText.Attribute.Position = RectangleTextPosition.Top;
            rectangleText.Render();
            preview.ImageShow.AddVisualCommand(rectangleText);
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

        private void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            var lsit = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList();
            DeviceCamera deviceCamera = lsit.FirstOrDefault();

            MsgRecord msgRecord = deviceCamera?.DisplayCameraControlLazy.Value.TakePhoto(PoiConfig.Exp);

            if (msgRecord != null)
            {
                msgRecord.MsgSucessed += (s,e) =>
                {
                    int masterId = Convert.ToInt32(e.Data.MasterId);
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
                                    MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailedNoPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(Properties.Resources.OpenLatestServiceImageFailed, ex.Message);
                            }
                        }
                    }



                };

            }
        }


        private void ListView1_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (ListView1.SelectedItem is not IDrawingVisual selectedVisual)
            {
                e.Handled = true;
                return;
            }

            ListView1.ContextMenu.Items.Clear();

            Type type = selectedVisual.GetType();
            foreach (var provider in ImageView.IEditorToolFactory.ContextMenuProviders)
            {
                if (provider.ContextType.IsAssignableFrom(type))
                {
                    var items = provider.GetContextMenuItems(selectedVisual);
                    foreach (var item in items)
                        ListView1.ContextMenu.Items.Add(item);
                }
            }
            var moveUpItem = new MenuItem { Header = "上移 (Alt+↑)", Command = MoveUpCommand };
            ListView1.ContextMenu.Items.Add(moveUpItem);

            var moveDownItem = new MenuItem { Header = "下移 (Alt+↓)", Command = MoveDownCommand };
            ListView1.ContextMenu.Items.Add(moveDownItem);

            var moveToTopItem = new MenuItem { Header = "移动到首位", Command = MoveToTopCommand };
            ListView1.ContextMenu.Items.Add(moveToTopItem);

            var moveToBottomItem = new MenuItem { Header = "移动到末尾", Command = MoveToBottomCommand };
            ListView1.ContextMenu.Items.Add(moveToBottomItem);

            ListView1.ContextMenu.Items.Add(new Separator());

            if (ListView1.SelectedItems.Count > 1)
            {
                var batchHeader = new MenuItem { Header = $"批量编辑 ({ListView1.SelectedItems.Count} 项)", IsEnabled = false };
                batchHeader.FontWeight = FontWeights.Bold;
                ListView1.ContextMenu.Items.Add(batchHeader);

                var batchTextItem = new MenuItem { Header = "批量设置名称..." };
                batchTextItem.Click += (s, ev) => BatchSetText();
                ListView1.ContextMenu.Items.Add(batchTextItem);

                bool hasCircles = ListView1.SelectedItems.Cast<IDrawingVisual>().Any(v => v is DVCircleText || v is DVCircle);
                if (hasCircles)
                {
                    var batchRadiusItem = new MenuItem { Header = "批量设置半径..." };
                    batchRadiusItem.Click += (s, ev) => BatchSetRadius();
                    ListView1.ContextMenu.Items.Add(batchRadiusItem);
                }

                bool hasRects = ListView1.SelectedItems.Cast<IDrawingVisual>().Any(v => v is DVRectangleText || v is DVRectangle);
                if (hasRects)
                {
                    var batchSizeItem = new MenuItem { Header = "批量设置尺寸..." };
                    batchSizeItem.Click += (s, ev) => BatchSetRectSize();
                    ListView1.ContextMenu.Items.Add(batchSizeItem);
                }

                var fillDownItem = new MenuItem { Header = "向下填充 (Ctrl+D)" };
                fillDownItem.Click += (s, ev) => BatchFillDown();
                ListView1.ContextMenu.Items.Add(fillDownItem);
            }


        }
        RelayCommand MoveUpCommand { get; set; }
        RelayCommand MoveDownCommand { get; set; }
        RelayCommand MoveToTopCommand { get; set; }
        RelayCommand MoveToBottomCommand { get; set; }

        private int GetSelectedDrawingVisualIndex()
        {
            return ListView1?.SelectedItem is IDrawingVisual selectedVisual ? DrawingVisualLists.IndexOf(selectedVisual) : -1;
        }

        private bool CanMoveSelectedDrawingVisualDown()
        {
            int index = GetSelectedDrawingVisualIndex();
            return index >= 0 && index < DrawingVisualLists.Count - 1;
        }

        private void MoveUp()
        {
            int index = GetSelectedDrawingVisualIndex();
            if (index > 0)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.Move(index, index - 1);
                ListView1.SelectedItem = item;
                UpdateDBIndex(item, index - 1);
            }
        }

        private void MoveDown()
        {
            int index = GetSelectedDrawingVisualIndex();
            if (index >= 0 && index < DrawingVisualLists.Count - 1)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.Move(index, index + 1);
                ListView1.SelectedItem = item;
                UpdateDBIndex(item, index + 1);
            }
        }

        private void MoveToTop()
        {
            int index = GetSelectedDrawingVisualIndex();
            if (index > 0)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.Move(index, 0);
                ListView1.SelectedItem = item;
                UpdateDBIndex(item, 0);
            }
        }

        private void MoveToBottom()
        {
            int index = GetSelectedDrawingVisualIndex();
            if (index >= 0 && index < DrawingVisualLists.Count - 1)
            {
                var item = DrawingVisualLists[index];
                DrawingVisualLists.Move(index, DrawingVisualLists.Count - 1);
                ListView1.SelectedItem = item;
                UpdateDBIndex(item, DrawingVisualLists.Count - 1);
            }
        }

        #region Drag-and-Drop Reordering

        private Point _dragStartPoint;
        private IDrawingVisual _draggedItem;

        private void ListViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;

            _dragStartPoint = e.GetPosition(null);
            if (sender is ListViewItem listViewItem)
                _draggedItem = listViewItem.Content as IDrawingVisual;
        }

        private void ListViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null)
                return;

            if (Keyboard.FocusedElement is TextBox)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                DataObject dragData = new DataObject("PoiDragItem", _draggedItem);
                DragDrop.DoDragDrop((DependencyObject)sender, dragData, DragDropEffects.Move);
                _draggedItem = null;
            }
        }

        private void ListView1_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("PoiDragItem"))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void ListView1_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("PoiDragItem"))
                return;

            var droppedData = e.Data.GetData("PoiDragItem") as IDrawingVisual;
            if (droppedData == null)
                return;

            IDrawingVisual target = null;

            var element = e.OriginalSource as DependencyObject;
            while (element != null && element is not ListViewItem)
                element = VisualTreeHelper.GetParent(element);

            if (element is ListViewItem listViewItem)
                target = listViewItem.Content as IDrawingVisual;

            if (target == null || ReferenceEquals(droppedData, target))
                return;

            int oldIndex = DrawingVisualLists.IndexOf(droppedData);
            int newIndex = DrawingVisualLists.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0)
                return;

            DrawingVisualLists.Move(oldIndex, newIndex);
            ListView1.SelectedItem = droppedData;
        }

        #endregion

        #region Batch Editing

        private void BatchFillDown()
        {
            if (ListView1.SelectedItems.Count < 2) return;

            var selectedItems = ListView1.SelectedItems.Cast<IDrawingVisual>().ToList();
            var first = selectedItems[0];

            if (first is DVCircleText firstCircle)
            {
                for (int i = 1; i < selectedItems.Count; i++)
                {
                    if (selectedItems[i] is DVCircleText circle)
                    {
                        circle.Attribute.Radius = firstCircle.Attribute.Radius;
                        circle.Render();
                    }
                }
            }
            else if (first is DVRectangleText firstRect)
            {
                for (int i = 1; i < selectedItems.Count; i++)
                {
                    if (selectedItems[i] is DVRectangleText rect)
                    {
                        var newRect = new Rect(rect.Attribute.Rect.X, rect.Attribute.Rect.Y,
                            firstRect.Attribute.Rect.Width, firstRect.Attribute.Rect.Height);
                        rect.Attribute.Rect = newRect;
                        rect.Render();
                    }
                }
            }
        }

        private void BatchSetText()
        {
            var selectedItems = ListView1.SelectedItems.Cast<IDrawingVisual>().ToList();
            if (selectedItems.Count == 0) return;

            string currentText = "";
            if (selectedItems[0] is DVCircleText ct)
                currentText = ct.Attribute.Text ?? "";
            else if (selectedItems[0] is DVRectangleText rt)
                currentText = rt.Attribute.Text ?? "";

            var dialog = new BatchEditDialog("批量设置名称", "请输入名称模板 (使用 {n} 表示序号):", currentText)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                string template = dialog.InputValue;
                int seq = 1;
                foreach (var item in selectedItems)
                {
                    string newText = template.Contains("{n}") ? template.Replace("{n}", seq.ToString()) : template;
                    if (item is DVCircleText circle)
                    {
                        circle.Attribute.Text = newText;
                        circle.Render();
                    }
                    else if (item is DVRectangleText rect)
                    {
                        rect.Attribute.Text = newText;
                        rect.Render();
                    }
                    seq++;
                }
            }
        }

        private void BatchSetRadius()
        {
            var selectedItems = ListView1.SelectedItems.Cast<IDrawingVisual>()
                .Where(v => v is DVCircleText || v is DVCircle).ToList();
            if (selectedItems.Count == 0) return;

            string currentValue = "";
            if (selectedItems[0] is DVCircleText ct)
                currentValue = ct.Attribute.Radius.ToString("F1");
            else if (selectedItems[0] is DVCircle c)
                currentValue = c.Attribute.Radius.ToString("F1");

            var dialog = new BatchEditDialog("批量设置半径", "请输入半径值:", currentValue)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                if (double.TryParse(dialog.InputValue, out double radius))
                {
                    foreach (var item in selectedItems)
                    {
                        if (item is DVCircleText circle)
                        {
                            circle.Attribute.Radius = radius;
                            circle.Render();
                        }
                        else if (item is DVCircle c)
                        {
                            c.Attribute.Radius = radius;
                            c.Render();
                        }
                    }
                }
            }
        }

        private void BatchSetRectSize()
        {
            var selectedItems = ListView1.SelectedItems.Cast<IDrawingVisual>()
                .Where(v => v is DVRectangleText || v is DVRectangle).ToList();
            if (selectedItems.Count == 0) return;

            string currentValue = "";
            if (selectedItems[0] is DVRectangleText rt)
                currentValue = $"{rt.Attribute.Rect.Width:F0},{rt.Attribute.Rect.Height:F0}";
            else if (selectedItems[0] is DVRectangle r)
                currentValue = $"{r.Attribute.Rect.Width:F0},{r.Attribute.Rect.Height:F0}";

            var dialog = new BatchEditDialog("批量设置尺寸", "请输入宽度,高度 (用逗号分隔):", currentValue)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() == true)
            {
                var parts = dialog.InputValue.Split(',');
                if (parts.Length != 2 || !double.TryParse(parts[0].Trim(), out double width) || !double.TryParse(parts[1].Trim(), out double height))
                {
                    MessageBox.Show("请输入正确的格式：宽度,高度 (例如: 100,50)", "格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var item in selectedItems)
                {
                    if (item is DVRectangleText rect)
                    {
                        rect.Attribute.Rect = new Rect(rect.Attribute.Rect.X, rect.Attribute.Rect.Y, width, height);
                        rect.Render();
                    }
                    else if (item is DVRectangle r)
                    {
                        r.Attribute.Rect = new Rect(r.Attribute.Rect.X, r.Attribute.Rect.Y, width, height);
                        r.Render();
                    }
                }
            }
        }

        #endregion


        private void UpdateDBIndex(IDrawingVisual movedItem, int newIndex)
        {
            // 找到被替换的项（假设newIndex是移动后的位置）
            var replacedItem = DrawingVisualLists[newIndex];
            // 交换位置
            if (!DBIndex.ContainsKey(movedItem))
                DBIndex.Add(movedItem, -1);
            if (!DBIndex.ContainsKey(replacedItem))
                DBIndex.Add(replacedItem, -1);

            int temp = DBIndex[movedItem];
            DBIndex[movedItem] = DBIndex[replacedItem];
            DBIndex[replacedItem] = temp;

            movedItem.BaseAttribute.Name = DBIndex[movedItem].ToString();
            replacedItem.BaseAttribute.Name = DBIndex[replacedItem].ToString();
        }
    }

}
