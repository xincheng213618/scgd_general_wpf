#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.Rotate;
using ColorVision.ImageEditor.EditorTools.FullScreen;
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

        public RelayCommand ClearImageCommand { get; set; }
        public RelayCommand PrintImageCommand { get; set; }
        public RelayCommand PropertyCommand { get; set; }
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
        public TextManager TextManager { get; set; } // 新增
        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();
        public SelectEditorVisual SelectEditorVisual { get; set; }
        public StackPanel SlectStackPanel { get; set; } = new StackPanel();
        public ImageFullScreenMode ImageFullScreenMode { get; set; }

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
        private ImageContextMenuManager _contextMenuManager;
        private ImageKeyboardHandler _keyboardHandler;
        #endregion

        public IEditorToolFactory IEditorToolFactory { get; set; }

        public ImageViewModel(ImageView imageView, ZoomboxSub zoombox, DrawCanvas drawCanvas)
        {

            var context = new EditorContext()
            {
                ImageViewModel = this,
                DrawCanvas = drawCanvas,
                ZoomboxSub = zoombox
            };
            IEditorToolFactory = new IEditorToolFactory(imageView, context);


            this.ImageView = imageView;
            ZoomboxSub = zoombox;
            Image = drawCanvas;
            Config = new ImageViewConfig();
            ContextMenu = new ContextMenu();

            _transformOperations = new ImageTransformOperations(drawCanvas);
            _fileOperations = new ImageFileOperations(drawCanvas, imageView);
            ImageFullScreenMode = new ImageFullScreenMode(imageView);

            RegisterContextMenuProviders();

            imageView.AdvancedStackPanel.Children.Add(SlectStackPanel);


            SelectEditorVisual = new SelectEditorVisual(this, drawCanvas, zoombox);

            SetupCommandBindings(drawCanvas);
 
            CreateCommands();

            _keyboardHandler = new ImageKeyboardHandler(imageView, this, ZoomboxSub, Config);

            drawCanvas.PreviewMouseDown += (s, e) =>
            {
                Keyboard.ClearFocus();
                drawCanvas.Focus();
            };

            drawCanvas.PreviewKeyDown += (s, e) =>
            {
                Keyboard.ClearFocus();
                drawCanvas.Focus();
            };

            drawCanvas.PreviewKeyDown += _keyboardHandler.HandleKeyDown;


            InitializeTools(zoombox, drawCanvas);


            _contextMenuManager = new ImageContextMenuManager(this, drawCanvas, ContextMenu, ContextMenuProviders);
            Image.ContextMenuOpening += _contextMenuManager.HandleContextMenuOpening;
            Image.ContextMenu = ContextMenu;
            ZoomboxSub.ContextMenu = ContextMenu;
            ZoomboxSub.LayoutUpdated += Zoombox1_LayoutUpdated;
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
                ApplicationCommands.Close, 
                (s, e) => ClearImage(), 
                (s, e) => { e.CanExecute = Image.Source != null; }));
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
            TextManager = new TextManager(this, zoombox, drawCanvas); // 初始化 TextManager
        }

        private void CreateCommands()
        {
            PrintImageCommand = new RelayCommand(a => Print(), a => Image != null && Image.Source != null);
            ClearImageCommand = new RelayCommand(a => ClearImage(), a => Image != null && Image.Source != null);
            
            PropertyCommand = new RelayCommand(a => new DrawProperties(Config) { Owner = Window.GetWindow(ImageView), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());
        }

        #region Public Methods
                
        public void Print() => _fileOperations.Print();

        public void Save(string file) => ImageView.Save(file);

        public void ClearImage() => _fileOperations.ClearImage(ToolBarScaleRuler, ClearImageEventHandler);
        
        #endregion

        #region Properties with change notification
      
        /// <summary>
        /// ?????????????
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
            TextManager?.Dispose();
            
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
