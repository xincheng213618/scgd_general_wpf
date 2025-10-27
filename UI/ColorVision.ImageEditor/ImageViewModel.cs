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
        public ToolBarScaleRuler ToolBarScaleRuler { get; set; }
        public SelectEditorVisual SelectEditorVisual { get; set; }

        public StackPanel SlectStackPanel { get; set; } = new StackPanel();

        public ImageViewConfig Config => EditorContext.Config;

        public MouseMagnifierManager MouseMagnifier { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public IImageOpen? IImageOpen { get; set; }

        private ImageTransformOperations _transformOperations;

        public IEditorToolFactory IEditorToolFactory { get; set; }

        public EditorContext EditorContext { get; set; }

        public ImageViewModel(ImageView imageView, Zoombox zoombox, DrawCanvas drawCanvas)
        {
            EditorContext = new EditorContext()
            {
                ImageView = imageView,
                ImageViewModel = this,
                DrawCanvas = drawCanvas,
                Zoombox = zoombox,
            };
            SelectEditorVisual = new SelectEditorVisual(EditorContext);
            IEditorToolFactory = new IEditorToolFactory(imageView, EditorContext);

            MouseMagnifier = IEditorToolFactory.IEditorTools.OfType<MouseMagnifierManager>().FirstOrDefault();
            Image = drawCanvas;

            ContextMenu = new ContextMenu();

            _transformOperations = new ImageTransformOperations(drawCanvas);

            imageView.AdvancedStackPanel.Children.Insert(0,SlectStackPanel);

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

            drawCanvas.PreviewKeyDown += HandleKeyDown;

            Crosshair = new Crosshair(zoombox, drawCanvas);
            ToolBarScaleRuler = new ToolBarScaleRuler(imageView, zoombox, drawCanvas);

            Image.ContextMenuOpening += HandleContextMenuOpening;
            Image.ContextMenu = ContextMenu;
            zoombox.ContextMenu = ContextMenu;
            zoombox.LayoutUpdated += Zoombox1_LayoutUpdated;

        }

        /// <summary>
        /// ��������¼�
        /// </summary>
        /// <param name="sender">�¼�������</param>
        /// <param name="e">�����¼�����</param>
        public void HandleKeyDown(object sender, KeyEventArgs e)
        {
            HandleBrowseModeKeyDown(e);
        }


        /// <summary>
        /// �������ģʽ�µļ��̲���
        /// </summary>
        private void HandleBrowseModeKeyDown(KeyEventArgs e)
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
                // �л�����һ���ļ�
                string? previousFile = GetAdjacentImageFile(EditorContext.Config.FilePath, false);
                if (!string.IsNullOrEmpty(previousFile))
                {
                    EditorContext.ImageView.OpenImage(previousFile);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // �л�����һ���ļ�
                string? nextFile = GetAdjacentImageFile(EditorContext.Config.FilePath, true);
                if (!string.IsNullOrEmpty(nextFile))
                {
                    EditorContext.ImageView.OpenImage(nextFile);
                }
                e.Handled = true;
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
        /// ��ȡ���ڵ�ͼ���ļ�
        /// </summary>
        /// <param name="currentFilePath">��ǰ�ļ�·��</param>
        /// <param name="moveNext">�Ƿ��ȡ��һ���ļ�</param>
        /// <returns>�����ļ���·��</returns>
        private string? GetAdjacentImageFile(string currentFilePath, bool moveNext)
        {
            var supportedExtensions = IEditorToolFactory.IImageOpens.Keys.ToList();
            try
            {
                // ��ȡ��ǰ�ļ����ڵ�Ŀ¼
                string? directory = Path.GetDirectoryName(currentFilePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    return null;
                }

                // ��ȡĿ¼������֧�ֵ�ͼƬ�ļ���������������
                var imageFiles = Directory.GetFiles(directory)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();

                if (imageFiles.Count <= 1)
                {
                    return null; // �ļ�����û������ͼƬ
                }

                // ���б����ҵ���ǰ�ļ�������
                int currentIndex = imageFiles.FindIndex(
                    f => string.Equals(f, currentFilePath, StringComparison.OrdinalIgnoreCase));

                if (currentIndex == -1)
                {
                    return null; // ��ǰ�ļ������б��У���������������ɾ����
                }

                // ������һ������һ���ļ�������
                int newIndex;
                if (moveNext) // ��ȡ��һ��
                {
                    newIndex = (currentIndex + 1) % imageFiles.Count;
                }
                else // ��ȡ��һ��
                {
                    newIndex = (currentIndex - 1 + imageFiles.Count) % imageFiles.Count;
                }

                // �����µ��ļ�·��
                return imageFiles[newIndex];
            }
            catch (Exception ex)
            {
                // ���������־��¼
                Console.WriteLine($"Error finding adjacent image file: {ex.Message}");
                return null;
            }
        }



        /// <summary>
        /// ���������Ĳ˵����¼�
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
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(EditorContext, selectVisual);
                            foreach (var item in items)
                                ContextMenu.Items.Add(item);
                        }
                    }
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(EditorContext, selectEditorVisual);
                            foreach (var item in items)
                                ContextMenu.Items.Add(item);
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
        /// ������׼�����Ĳ˵�
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
                    ContextMenu.Items.Add(new Separator());

                ContextMenu.Items.Add(menuItem);
            }
        }





        double oldMax;
        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e)
        {
            if (oldMax != EditorContext.Zoomratio)
            {
                if (EditorContext.Config.IsLayoutUpdated)
                {
                    oldMax = EditorContext.Zoomratio;
                    double scale = 1 / EditorContext.Zoomratio;
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
