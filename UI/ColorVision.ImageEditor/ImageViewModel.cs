#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor
{
    public class ImageViewModel : ViewModelBase, IDisposable
    {
        public DrawCanvas Image { get; set; }
        public Crosshair Crosshair { get; set; }

        public SelectEditorVisual SelectEditorVisual { get; set; }

        public StackPanel SlectStackPanel { get; set; } = new StackPanel();

        public ImageViewConfig Config => EditorContext.Config;

        public MouseMagnifierManager MouseMagnifier { get; set; }

        public IEditorToolFactory IEditorToolFactory => EditorContext.IEditorToolFactory;

        public EditorContext EditorContext { get; set; }

        public double MaxZoom { get => _MaxZoom; set { _MaxZoom = value; OnPropertyChanged(); } }
        private double _MaxZoom = 20;
        public double MinZoom { get => _MinZoom; set { _MinZoom = value; OnPropertyChanged(); } }
        private double _MinZoom = 0.005;


        public ImageViewModel(ImageView imageView)
        {
            EditorContext = new EditorContext()
            {
                ImageView = imageView,
                ImageViewModel = this,
                DrawCanvas = imageView.ImageShow,
                Zoombox = imageView.Zoombox1,
            };
            SelectEditorVisual = new SelectEditorVisual(EditorContext);
            EditorContext.IEditorToolFactory = new IEditorToolFactory(imageView, EditorContext);


            MouseMagnifier = IEditorToolFactory.IEditorTools.OfType<MouseMagnifierManager>().FirstOrDefault();

            Image = EditorContext.DrawCanvas;

            imageView.AdvancedStackPanel.Children.Insert(0,SlectStackPanel);

            EditorContext.DrawCanvas.PreviewKeyDown += HandleKeyDown;

            Crosshair = new Crosshair(EditorContext);

            Image.ContextMenuOpening += HandleContextMenuOpening;
            Image.ContextMenu = EditorContext.ContextMenu;
            EditorContext.Zoombox.ContextMenu = EditorContext.ContextMenu;
            EditorContext.Zoombox.LayoutUpdated += Zoombox1_LayoutUpdated;

        }

        /// <summary>
        /// 处理键盘事件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">键盘事件参数</param>
        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (!ImageEditMode)
            {
                if (e.Key == Key.Left)
                {
                    MoveView(-10, 0);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    MoveView(10, 0);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    MoveView(0, -10);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    MoveView(0, 10);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 移动视图位置
        /// </summary>
        /// <param name="x">X方向移动量</param>
        /// <param name="y">Y方向移动量</param>
        private void MoveView(double x, double y)
        {
            TranslateTransform translateTransform = new();
            Vector vector = new(x, y);
            translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
            translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
            EditorContext.Zoombox.SetCurrentValue(Zoombox.ContentMatrixProperty,
                Matrix.Multiply(EditorContext.Zoombox.ContentMatrix, translateTransform.Value));
        }

        /// <summary>
        /// 处理上下文菜单打开事件
        /// </summary>
        public void HandleContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            EditorContext.ContextMenu.Items.Clear();

            if (ImageEditMode)
            {
                Point MouseDownP = Mouse.GetPosition(Image);
                var MouseVisual = Image.GetVisual<Visual>(MouseDownP);
                Type type = MouseVisual.GetType();

                if (MouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(MouseDownP) is ISelectVisual selectVisual)
                {
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(EditorContext, selectVisual);
                            foreach (var item in items)
                                EditorContext.ContextMenu.Items.Add(item);
                        }
                    }
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(EditorContext, selectEditorVisual);
                            foreach (var item in items)
                                EditorContext.ContextMenu.Items.Add(item);
                        }
                    }
                }
                else
                {
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(type))
                        {
                            var items = provider.GetContextMenuItems(EditorContext, MouseVisual);
                            foreach (var item in items)
                                EditorContext.ContextMenu.Items.Add(item);
                        }
                    }
                }

                if (EditorContext.ContextMenu.Items.Count == 0)
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
            if (EditorContext.IImageOpen is IIEditorToolContextMenu contentMenuProvider)
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
                    EditorContext.ContextMenu.Items.Add(new Separator());

                EditorContext.ContextMenu.Items.Add(menuItem);
            }
        }





        double oldMax;
        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e)
        {
            if (oldMax != EditorContext.ZoomRatio)
            {
                if (EditorContext.Config.IsLayoutUpdated)
                {
                    oldMax = EditorContext.ZoomRatio;
                    double scale = 1 / EditorContext.ZoomRatio;
                    DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender" + EditorContext.Id.ToString(), 20, () => ImageLayoutUpdatedRender(scale, EditorContext.DrawingVisualLists));
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



        private bool _ImageEditMode;
        public bool ImageEditMode
        {
            get => _ImageEditMode;
            set
            {
                if (_ImageEditMode == value) return;
                Config.IsToolBarDrawVisible = value;
                _ImageEditMode = value;

                if (_ImageEditMode)
                {
                    EditorContext.Zoombox.ActivateOn = ModifierKeys.Control;
                    EditorContext.Zoombox.Cursor = Cursors.Cross;
                }
                else
                {
                    EditorContext.Zoombox.ActivateOn = ModifierKeys.None;
                    EditorContext.Zoombox.Cursor = Cursors.Arrow;
                }
                EditorContext.DrawEditorManager.SetCurrentDrawEditor(null); 
                OnPropertyChanged();
            }
        }
        

        public void Dispose()
        {
            foreach (var item in IEditorToolFactory.IEditorTools.OfType<IDisposable>())
            {
                item.Dispose();
            }

            EditorContext.DrawingVisualLists?.Clear();
            EditorContext.DrawingVisualLists = null;
            EditorContext.Zoombox.LayoutUpdated -= Zoombox1_LayoutUpdated;


            if (Image != null)
            {
                Image.PreviewKeyDown -= HandleKeyDown;
            }
            Image = null;

            GC.SuppressFinalize(this);
        }
    }
}
