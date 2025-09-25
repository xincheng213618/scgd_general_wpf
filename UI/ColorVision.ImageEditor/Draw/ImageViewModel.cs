#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.ImageEditor
{
    public class ImageViewModel : ViewModelBase, IDisposable
    {
        public DrawEditorManager DrawEditorManager { get; set; } = new DrawEditorManager();
        private readonly Guid _guid = Guid.NewGuid();

        #region Commands
        public RelayCommand ZoomUniformToFill { get; set; }
        public RelayCommand ZoomUniformCommand { get; set; }
        public RelayCommand ZoomInCommand { get; set; }
        public RelayCommand ZoomOutCommand { get; set; }
        public RelayCommand ZoomNoneCommand { get; set; }

        public RelayCommand FullCommand { get; set; }
        public RelayCommand RotateLeftCommand { get; set; }
        public RelayCommand RotateRightCommand { get; set; }
        public RelayCommand FlipHorizontalCommand { get; set; }
        public RelayCommand FlipVerticalCommand { get; set; }
        public RelayCommand SaveAsImageCommand { get; set; }
        public RelayCommand ClearImageCommand { get; set; }
        public RelayCommand PrintImageCommand { get; set; }
        public RelayCommand PropertyCommand { get; set; }
        public RelayCommand OpenImageCommand { get; set; }
        #endregion

        #region Events
        public event EventHandler ClearImageEventHandler;
        #endregion

        #region Components
        public ZoomboxSub ZoomboxSub { get; set; }
        public DrawCanvas Image { get; set; }
        public BezierCurveManager BezierCurveManager { get; set; }
        public CircleManager CircleManager { get; set; }
        public RectangleManager RectangleManager { get; set; }
        public EraseManager EraseManager { get; set; }
        public PolygonManager PolygonManager { get; set; }
        public MouseMagnifier MouseMagnifier { get; set; }
        public MeasureManager MeasureManager { get; set; }
        public LineManager LineManager { get; set; }
        public Crosshair Crosshair { get; set; }
        public Gridline Gridline { get; set; }
        public ToolBarScaleRuler ToolBarScaleRuler { get; set; }
        public ToolReferenceLine ToolConcentricCircle { get; set; }
        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();
        public SelectEditorVisual SelectEditorVisual { get; set; }
        public StackPanel SlectStackPanel { get; set; } = new StackPanel();
        #endregion

        #region Properties
        public ImageView ImageView { get; set; }
        public ImageViewConfig Config { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public IImageOpen? IImageOpen { get; set; }
        public List<IDVContextMenu> ContextMenuProviders { get; set; } = new List<IDVContextMenu>();
        public bool IsMax { get; set; }
        #endregion

        #region Helper Classes
        private ImageTransformOperations _transformOperations;
        private ImageFileOperations _fileOperations;
        private ImageFullScreenMode _fullScreenMode;
        private ImageContextMenuManager _contextMenuManager;
        private ImageKeyboardHandler _keyboardHandler;
        #endregion

        public ImageViewModel(ImageView Parent, ZoomboxSub zoombox, DrawCanvas drawCanvas)
        {
            // 初始化属性
            this.ImageView = Parent;
            ZoomboxSub = zoombox;
            Image = drawCanvas;
            Config = new ImageViewConfig();
            ContextMenu = new ContextMenu();

            // 初始化辅助类
            _transformOperations = new ImageTransformOperations(drawCanvas);
            _fileOperations = new ImageFileOperations(drawCanvas, Parent);
            _fullScreenMode = new ImageFullScreenMode(Parent);

            // 注册上下文菜单提供程序
            RegisterContextMenuProviders();
            
            // 初始化选择编辑器
            SelectEditorVisual = new SelectEditorVisual(this, drawCanvas, zoombox);

            // 配置命令绑定
            SetupCommandBindings(drawCanvas);
            // 创建命令
            CreateCommands();

            _keyboardHandler = new ImageKeyboardHandler(Parent, this, ZoomboxSub, Config, ZoomInCommand, ZoomOutCommand);

            // 设置鼠标和键盘处理
            SetupInputHandling(drawCanvas);

            // 初始化各种工具和管理器
            InitializeTools(zoombox, drawCanvas);



            // 设置上下文菜单
            _contextMenuManager = new ImageContextMenuManager(this, drawCanvas, ContextMenu, ContextMenuProviders);
            Image.ContextMenuOpening += _contextMenuManager.HandleContextMenuOpening;
            Image.ContextMenu = ContextMenu;
            ZoomboxSub.ContextMenu = ContextMenu;

            // 设置布局更新处理
            ZoomboxSub.LayoutUpdated += Zoombox1_LayoutUpdated;

            // 初始化键盘处理器
        }

        double oldMax;
        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e)
        {
            if (oldMax != ZoomboxSub.ContentMatrix.M11)
            {
                if (Config.IsLayoutUpdated)
                {
                    oldMax = ZoomboxSub.ContentMatrix.M11;
                    double scale = 1 / ZoomboxSub.ContentMatrix.M11;
                    DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender" + _guid.ToString(), 20, () => ImageLayoutUpdatedRender(scale, DrawingVisualLists));
                }
            }

        }
        bool IsUpdatedRender;
        public void ImageLayoutUpdatedRender(double scale, ObservableCollection<IDrawingVisual> DrawingVisualLists)
        {
            if (IsUpdatedRender) return;
            IsUpdatedRender = true;
            if (DrawingVisualLists != null)
            {
                foreach (var item in DrawingVisualLists)
                {
                    if (item.BaseAttribute is ITextProperties textProperties)
                    {
                        textProperties.TextAttribute.FontSize = 10 * scale;
                    }
                    item.Pen.Thickness = scale;
                    item.Render();
                }
            }
            IsUpdatedRender = false;
        }
        private void RegisterContextMenuProviders()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IDVContextMenu).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (Activator.CreateInstance(type) is IDVContextMenu instance)
                        {
                            ContextMenuProviders.Add(instance);
                        }
                    }
                }
            }
        }

        private void SetupCommandBindings(DrawCanvas drawCanvas)
        {
            drawCanvas.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Print, 
                (s, e) => Print(), 
                (s, e) => { e.CanExecute = Image != null && Image.Source != null; }));
                
            drawCanvas.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.SaveAs, 
                (s, e) => SaveAs(), 
                (s, e) => { e.CanExecute = Image != null && Image.Source != null; }));
                
            drawCanvas.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Open, 
                (s, e) => OpenImage(), 
                (s, e) => { e.CanExecute = true; }));
                
            drawCanvas.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Close, 
                (s, e) => ClearImage(), 
                (s, e) => { e.CanExecute = Image.Source != null; }));
        }

        private void SetupInputHandling(DrawCanvas drawCanvas)
        {
            drawCanvas.PreviewMouseDown += (s, e) =>
            {
                Keyboard.ClearFocus(); // 清除当前焦点
                drawCanvas.Focus();
            };
            
            drawCanvas.PreviewKeyDown += (s, e) =>
            {
                Keyboard.ClearFocus(); // 清除当前焦点
                drawCanvas.Focus();
            };
            
            drawCanvas.PreviewKeyDown += _keyboardHandler.HandleKeyDown;
        }

        private void InitializeTools(ZoomboxSub zoombox, DrawCanvas drawCanvas)
        {
            MouseMagnifier = new MouseMagnifier(zoombox, drawCanvas);
            Crosshair = new Crosshair(zoombox, drawCanvas);
            Gridline = new Gridline(zoombox, drawCanvas);
            ToolBarScaleRuler = new ToolBarScaleRuler(ImageView, zoombox, drawCanvas);
            ToolConcentricCircle = new ToolReferenceLine(this, zoombox, drawCanvas);

            MeasureManager = new MeasureManager(this, zoombox, drawCanvas);
            PolygonManager = new PolygonManager(this, zoombox, drawCanvas);
            BezierCurveManager = new BezierCurveManager(this, zoombox, drawCanvas);
            LineManager = new LineManager(this, zoombox, drawCanvas);
            CircleManager = new CircleManager(this, zoombox, drawCanvas);
            RectangleManager = new RectangleManager(this, zoombox, drawCanvas);
            EraseManager = new EraseManager(this, zoombox, drawCanvas);
        }

        private void CreateCommands()
        {
            // 缩放命令
            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill(), a => Image != null && Image.Source != null);
            ZoomUniformCommand = new RelayCommand(a => ZoomboxSub.ZoomUniform(), a => Image != null && Image.Source != null);
            ZoomInCommand = new RelayCommand(a => ZoomboxSub.Zoom(1.25), a => Image != null && Image.Source != null);
            ZoomOutCommand = new RelayCommand(a => ZoomboxSub.Zoom(0.8), a => Image != null && Image.Source != null);
            ZoomNoneCommand = new RelayCommand(a => ZoomboxSub.ZoomNone(), a => Image != null && Image.Source != null);

            // 图像操作命令
            FlipHorizontalCommand = new RelayCommand(a => _transformOperations.FlipHorizontal(), a => Image != null && Image.Source != null);
            FlipVerticalCommand = new RelayCommand(a => _transformOperations.FlipVertical(), a => Image != null && Image.Source != null);
            RotateLeftCommand = new RelayCommand(a => _transformOperations.RotateLeft());
            RotateRightCommand = new RelayCommand(a => _transformOperations.RotateRight());
            
            // 文件操作命令
            OpenImageCommand = new RelayCommand(a => OpenImage());
            SaveAsImageCommand = new RelayCommand(a => SaveAs(), a => Image != null && Image.Source != null);
            PrintImageCommand = new RelayCommand(a => Print(), a => Image != null && Image.Source != null);
            ClearImageCommand = new RelayCommand(a => ClearImage(), a => Image != null && Image.Source != null);
            
            // 其他命令
            PropertyCommand = new RelayCommand(a => new DrawProperties(Config) { Owner = Window.GetWindow(ImageView), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());
            FullCommand = new RelayCommand(a => MaxImage());
        }

        #region Public Methods
        
        public void OpenImage() => _fileOperations.OpenImage();
        
        public void Print() => _fileOperations.Print();
        
        public void SaveAs() => _fileOperations.SaveAs();
        
        public void Save(string fileName) => _fileOperations.Save(fileName);
        
        public void FlipHorizontal() => _transformOperations.FlipHorizontal();
        
        public void FlipVertical() => _transformOperations.FlipVertical();
        
        public void RotateRight() => _transformOperations.RotateRight();
        
        public void RotateLeft() => _transformOperations.RotateLeft();
        
        public void MaxImage() => _fullScreenMode.ToggleFullScreen();
        
        public void ClearImage() => _fileOperations.ClearImage(ToolBarScaleRuler, ClearImageEventHandler);
        
        #endregion

        #region Properties with change notification
      
        /// <summary>
        /// 当前的缩放分辨率
        /// </summary>
        public double ZoomRatio
        {
            get => ZoomboxSub.ContentMatrix.M11;
            set => ZoomboxSub.Zoom(value);
        }


        public EventHandler<bool> EditModeChanged { get; set; }

        private bool _ImageEditMode;
        public bool ImageEditMode
        {
            get => _ImageEditMode;
            set
            {
                if (_ImageEditMode == value) return;
                _ImageEditMode = value;

                EditModeChanged?.Invoke(this, _ImageEditMode);

                if (_ImageEditMode)
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.Control;
                    ZoomboxSub.Cursor = Cursors.Cross;
                }
                else
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.None;
                    ZoomboxSub.Cursor = Cursors.Arrow;
                }
                DrawEditorManager.SetCurrentDrawEditor(null); 
                OnPropertyChanged();
            }
        }
        
        #endregion

        public void Dispose()
        {
            LineManager?.Dispose();
            SelectEditorVisual?.Dispose();
            CircleManager?.Dispose();
            EraseManager?.Dispose();
            RectangleManager?.Dispose();
            PolygonManager?.Dispose();
            BezierCurveManager?.Dispose();
            
            if (DrawingVisualLists != null)
            {
                DrawingVisualLists.Clear();
                DrawingVisualLists = null;
            }

            if (ZoomboxSub != null)
            {
                ZoomboxSub.LayoutUpdated -= Zoombox1_LayoutUpdated;
                ZoomboxSub = null;
            }

            if (Image != null)
            {
                Image.ContextMenuOpening -= _contextMenuManager.HandleContextMenuOpening;
                Image.PreviewKeyDown -= _keyboardHandler.HandleKeyDown;
            }

            ImageView = null;
            Image = null;

            GC.SuppressFinalize(this);
        }
    }
}
