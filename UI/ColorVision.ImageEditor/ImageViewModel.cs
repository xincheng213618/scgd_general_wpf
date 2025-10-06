#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.ImageEditor.EditorTools.Rotate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    public class ImageViewModel : ViewModelBase, IDisposable
    {
        public DrawEditorManager DrawEditorManager { get; set; } = new DrawEditorManager();

        public Guid Id { get; set; } = Guid.NewGuid();



        #region Components
        public Zoombox ZoomboxSub { get; set; }
        public DrawCanvas Image { get; set; }

        public Crosshair Crosshair { get; set; }
        public ToolBarScaleRuler ToolBarScaleRuler { get; set; }
        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();
        public SelectEditorVisual SelectEditorVisual { get; set; }
        public StackPanel SlectStackPanel { get; set; } = new StackPanel();
        public ImageFullScreenMode ImageFullScreenMode { get; set; }

        public MouseMagnifierManager MouseMagnifier { get; set; }


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
        private ImageKeyboardHandler _keyboardHandler;
        #endregion

        public IEditorToolFactory IEditorToolFactory { get; set; }

        public ImageViewModel(ImageView imageView, Zoombox zoombox, DrawCanvas drawCanvas)
        {
            Config = new ImageViewConfig();

            var context = new EditorContext()
            {
                ImageView = imageView,
                ImageViewModel = this,
                DrawCanvas = drawCanvas,
                Zoombox = zoombox,
                Config = Config
            };

            IEditorToolFactory = new IEditorToolFactory(imageView, context);

            MouseMagnifier = IEditorToolFactory.IEditorTools.OfType<MouseMagnifierManager>().FirstOrDefault();

            this.ImageView = imageView;
            ZoomboxSub = zoombox;
            Image = drawCanvas;

            ContextMenu = new ContextMenu();

            _transformOperations = new ImageTransformOperations(drawCanvas);
            ImageFullScreenMode = new ImageFullScreenMode(imageView);

            RegisterContextMenuProviders();

            imageView.AdvancedStackPanel.Children.Add(SlectStackPanel);


            SelectEditorVisual = new SelectEditorVisual(this, drawCanvas, zoombox);


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

            Crosshair = new Crosshair(zoombox, drawCanvas);
            ToolBarScaleRuler = new ToolBarScaleRuler(ImageView, zoombox, drawCanvas);

            Image.ContextMenuOpening += HandleContextMenuOpening;
            Image.ContextMenu = ContextMenu;
            ZoomboxSub.ContextMenu = ContextMenu;
            ZoomboxSub.LayoutUpdated += Zoombox1_LayoutUpdated;
        }


        /// <summary>
        /// 处理上下文菜单打开事件
        /// </summary>
        public void HandleContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ContextMenu.Items.Clear();

            if (ImageEditMode)
            {
                Point MouseDownP = Mouse.GetPosition(Image);
                var MouseVisual = Image.GetVisual<Visual>(MouseDownP);
                Type type = MouseVisual.GetType();

                if (MouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(MouseDownP) is ISelectVisual selectVisual)
                {
                    foreach (var provider in ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(this, selectVisual);
                            foreach (var item in items)
                                ContextMenu.Items.Add(item);
                        }
                    }
                    foreach (var provider in ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(this, selectEditorVisual);
                            foreach (var item in items)
                                ContextMenu.Items.Add(item);
                        }
                    }
                }
                else
                {
                    foreach (var provider in ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(type))
                        {
                            var items = provider.GetContextMenuItems(this, MouseVisual);
                            foreach (var item in items)
                                ContextMenu.Items.Add(item);
                        }
                    }
                }

                if (ContextMenu.Items.Count == 0)
                    CreateStandardContextMenu();
            }
            else
            {
                CreateStandardContextMenu();
            }
        }

        /// <summary>
        /// 创建标准上下文菜单
        /// </summary>
        public void CreateStandardContextMenu()
        {
            List<MenuItemMetadata> MenuItemMetadatas = new List<MenuItemMetadata>();
            if (IImageOpen is IIEditorToolContextMenu contentMenuProvider)
            {
                MenuItemMetadatas.AddRange(contentMenuProvider.GetContextMenuItems());
            }

            foreach (var item in IEditorToolFactory.IIEditorToolContextMenus)
            {
                if (item is IImageOpen) continue;
                MenuItemMetadatas.AddRange(item.GetContextMenuItems());
            }


            var iMenuItems = MenuItemMetadatas.OrderBy(item => item.Order).ToList();

            // 递归创建菜单结构
            void CreateMenu(MenuItem parentMenuItem, string OwnerGuid)
            {
                var iMenuItems1 = iMenuItems.FindAll(a => a.OwnerGuid == OwnerGuid).OrderBy(a => a.Order).ToList();
                for (int i = 0; i < iMenuItems1.Count; i++)
                {
                    var iMenuItem = iMenuItems1[i];
                    string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem = new()
                    {
                        Header = iMenuItem.Header,
                        Icon = iMenuItem.Icon,
                        InputGestureText = iMenuItem.InputGestureText,
                        Command = iMenuItem.Command,
                        Tag = iMenuItem,
                        IsChecked = iMenuItem.IsChecked ?? false,
                        Visibility = iMenuItem.Visibility,
                    };

                    CreateMenu(menuItem, GuidId);
                    if (i > 0 && iMenuItem.Order - iMenuItems1[i - 1].Order > 4 && iMenuItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }
                    parentMenuItem.Items.Add(menuItem);
                }
                foreach (var item in iMenuItems1)
                {
                    iMenuItems.Remove(item);
                }
            }

            // 创建顶级菜单
            var iMenuItemMetas = MenuItemMetadatas
                .Where(item => item.OwnerGuid == MenuItemConstants.Menu && item.Visibility == Visibility.Visible)
                .OrderBy(item => item.Order)
                .ToList();

            for (int i = 0; i < iMenuItemMetas.Count; i++)
            {
                MenuItemMetadata menuItemMeta = iMenuItemMetas[i];
                MenuItem menuItem = new()
                {
                    Header = menuItemMeta.Header,
                    Command = menuItemMeta.Command,
                    Icon = menuItemMeta.Icon,
                    InputGestureText = menuItemMeta.InputGestureText,
                    IsChecked = menuItemMeta.IsChecked ?? false,
                };
                if (menuItemMeta.GuidId != null)
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                if (i > 0 && menuItemMeta.Order - iMenuItemMetas[i - 1].Order > 4)
                    ContextMenu.Items.Add(new Separator());

                ContextMenu.Items.Add(menuItem);
            }
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
                    DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender" + Id.ToString(), 20, () => ImageLayoutUpdatedRender(scale, DrawingVisualLists));
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

        #region Public Methods
                
        public void Save(string file) => ImageView.Save(file);
        public void ClearImage() => ImageView.Clear();

        
        #endregion

        #region Properties with change notification


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
            foreach (var item in IEditorToolFactory.IEditorTools.Cast<IDisposable>())
            {
                item.Dispose();
            }

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
                Image.PreviewKeyDown -= _keyboardHandler.HandleKeyDown;
            }

            ImageView = null;
            Image = null;

            GC.SuppressFinalize(this);
        }
    }
}
