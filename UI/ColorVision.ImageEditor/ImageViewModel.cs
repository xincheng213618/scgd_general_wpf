#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private readonly DefaultImageViewDisplayConfig _defaultDisplayConfig = DefaultImageViewDisplayConfig.Current;

        public DrawCanvas Image { get; set; }
        public Crosshair Crosshair { get; set; }

        public ImageViewConfig Config => EditorContext.Config;

        public IEditorToolFactory IEditorToolFactory => EditorContext.IEditorToolFactory;

        public EditorContext EditorContext { get; set; }

        [DisplayName("最大缩放")]
        public double MaxZoom
        {
            get => _defaultDisplayConfig.MaxZoom;
            set
            {
                if (_defaultDisplayConfig.MaxZoom == value)
                {
                    return;
                }

                _defaultDisplayConfig.MaxZoom = value;
                OnPropertyChanged();
            }
        }

        [DisplayName("最小缩放")]
        public double MinZoom
        {
            get => _defaultDisplayConfig.MinZoom;
            set
            {
                if (_defaultDisplayConfig.MinZoom == value)
                {
                    return;
                }

                _defaultDisplayConfig.MinZoom = value;
                OnPropertyChanged();
            }
        }


        public ImageViewModel(ImageView imageView)
        {
            _defaultDisplayConfig.PropertyChanged += DefaultDisplayConfig_PropertyChanged;

            EditorContext = new EditorContext()
            {
                ImageView = imageView,
                DrawCanvas = imageView.ImageShow,
                Zoombox = imageView.Zoombox1,
            };
            EditorContext.RegisterService<IImageMouseInfoProvider>(new ImageMouseInfoProvider(EditorContext));
            EditorContext.SelectionVisual = new SelectEditorVisual(EditorContext);
            EditorContext.IEditorToolFactory = new IEditorToolFactory(imageView, EditorContext);
            EditorContext.CompactInspectorPresenter = new CompactInspectorPresenter(EditorContext);

            Image = EditorContext.DrawCanvas;
            EditorContext.CompactInspectorPresenter.Refresh();

            EditorContext.DrawCanvas.PreviewKeyDown += HandleKeyDown;

            Crosshair = new Crosshair(EditorContext);

            Image.ContextMenuOpening += HandleContextMenuOpening;
            Image.ContextMenu = EditorContext.ContextMenu;
            EditorContext.Zoombox.ContextMenu = EditorContext.ContextMenu;
            EditorContext.Zoombox.ContentMatrixChanged += Zoombox1_ContentMatrixChanged;

        }

        private void DefaultDisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DefaultImageViewDisplayConfig.MaxZoom))
            {
                OnPropertyChanged(nameof(MaxZoom));
            }
            else if (e.PropertyName == nameof(DefaultImageViewDisplayConfig.MinZoom))
            {
                OnPropertyChanged(nameof(MinZoom));
            }
        }

        /// <summary>
        /// ���������¼�
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="e">�����¼�����</param>
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
        /// �ƶ���ͼλ��
        /// </summary>
        /// <param name="x">X�����ƶ���</param>
        /// <param name="y">Y�����ƶ���</param>
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
        /// ���������Ĳ˵����¼�
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
        /// ������׼�����Ĳ˵�
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

            // �ݹ鴴���˵��ṹ
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

            // ���������˵�
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
        private void Zoombox1_ContentMatrixChanged(object? sender, EventArgs e)
        {
            double zoomRatio = EditorContext.ZoomRatio;
            if (oldMax != zoomRatio)
            {
                oldMax = zoomRatio;
                double scale = double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
                EditorContext.DrawCanvas.Sacle = scale;
                if (EditorContext.Config.IsLayoutUpdated)
                {
                    DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender" + EditorContext.Id.ToString(), 20, () => ImageLayoutUpdatedRender(scale, EditorContext.DrawingVisualLists));
                }
            }

        }

        bool IsUpdatedRender;  
        public void ImageLayoutUpdatedRender(double scale, ObservableCollection<IDrawingVisual> DrawingVisualLists)
        {
            if (IsUpdatedRender) return;
            try
            {
                IsUpdatedRender = true;
                EditorContext.DrawCanvas.Sacle = scale;
                EditorContext.DrawCanvas.ApplyLayoutScaleToVisuals();
            }
            finally
            {
                IsUpdatedRender = false;
            }
        }

        public bool ImageEditMode
        {
            get => EditorContext.IsImageEditMode;
            set => SetImageEditModeCore(value, applyUiState: true, notifyPropertyChanged: true);
        }

        private void SetImageEditModeCore(bool value, bool applyUiState, bool notifyPropertyChanged)
        {
            if (EditorContext.IsImageEditMode == value)
            {
                return;
            }

            if (applyUiState)
            {
                Config.IsToolBarDrawVisible = value;
            }

            EditorContext.IsImageEditMode = value;

            if (applyUiState)
            {
                if (value)
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
            }

            if (notifyPropertyChanged)
            {
                OnPropertyChanged(nameof(ImageEditMode));
            }
        }
        

        public void Dispose()
        {
            _defaultDisplayConfig.PropertyChanged -= DefaultDisplayConfig_PropertyChanged;

            IEditorToolFactory.Dispose();

            if (EditorContext.TryGetService<IImageMouseInfoProvider>(out IImageMouseInfoProvider? mouseInfoProvider) && mouseInfoProvider is IDisposable disposableMouseInfoProvider)
            {
                disposableMouseInfoProvider.Dispose();
                EditorContext.UnregisterService<IImageMouseInfoProvider>();
            }

            EditorContext.CompactInspectorPresenter?.Dispose();

            EditorContext.DrawingVisualLists?.Clear();
            EditorContext.DrawingVisualLists = null;
            EditorContext.Zoombox.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;


            if (Image != null)
            {
                Image.PreviewKeyDown -= HandleKeyDown;
            }
            Image = null;

            GC.SuppressFinalize(this);
        }
    }
}
