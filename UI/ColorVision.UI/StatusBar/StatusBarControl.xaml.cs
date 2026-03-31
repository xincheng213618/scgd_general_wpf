using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ColorVision.UI
{
    public partial class StatusBarControl : UserControl
    {
        public string TargetName
        {
            get => (string)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(StatusBarControl),
                new PropertyMetadata("Global"));

        private static readonly Brush HoverBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));

        private readonly Dictionary<string, FrameworkElement> _itemContainers = new();
        private readonly List<StatusBarMeta> _loadedItems = new();
        private ContextMenu _contextMenu;

        static StatusBarControl()
        {
            HoverBrush.Freeze();
        }

        public StatusBarControl()
        {
            InitializeComponent();
            _contextMenu = new ContextMenu();
            RootGrid.ContextMenu = _contextMenu;
        }

        /// <summary>
        /// 加载状态栏项目
        /// </summary>
        public void LoadItems(IEnumerable<StatusBarMeta> items)
        {
            LeftItemsPanel.Children.Clear();
            RightItemsPanel.Children.Clear();
            _contextMenu.Items.Clear();
            _itemContainers.Clear();
            _loadedItems.Clear();

            var itemList = items.ToList();
            _loadedItems.AddRange(itemList);

            var leftItems = itemList
                .Where(i => i.Alignment == StatusBarAlignment.Left)
                .OrderBy(i => i.Order);

            var rightItems = itemList
                .Where(i => i.Alignment == StatusBarAlignment.Right)
                .OrderBy(i => i.Order);

            foreach (var meta in leftItems)
                AddItem(meta, LeftItemsPanel);

            foreach (var meta in rightItems)
                AddItem(meta, RightItemsPanel);

            // 添加分隔线 + 隐藏状态栏选项
            if (_contextMenu.Items.Count > 0)
                _contextMenu.Items.Add(new Separator());

            var hideMenuItem = new MenuItem { Header = "Hide Status Bar" };
            hideMenuItem.Click += (s, e) => this.Visibility = Visibility.Collapsed;
            _contextMenu.Items.Add(hideMenuItem);
        }

        /// <summary>
        /// 动态添加单个状态栏项
        /// </summary>
        public void AddStatusBarItem(StatusBarMeta meta)
        {
            _loadedItems.Add(meta);
            var panel = meta.Alignment == StatusBarAlignment.Left ? LeftItemsPanel : RightItemsPanel;

            // 找到正确的插入位置(按Order)
            int insertIndex = 0;
            foreach (FrameworkElement child in panel.Children)
            {
                if (child.Tag is StatusBarMeta existingMeta && existingMeta.Order <= meta.Order)
                    insertIndex++;
                else
                    break;
            }

            var container = CreateItemContainer(meta);
            container.Tag = meta;
            var id = GetItemId(meta);
            _itemContainers[id] = container;
            panel.Children.Insert(insertIndex, container);

            // 在分隔线之前插入右键菜单项
            int menuInsertIndex = _contextMenu.Items.Count > 0
                ? Math.Max(0, _contextMenu.Items.Count - 2) // 分隔线和隐藏按钮之前
                : 0;
            AddContextMenuItem(meta, container, menuInsertIndex);
        }

        /// <summary>
        /// 动态移除状态栏项
        /// </summary>
        public void RemoveStatusBarItem(string id)
        {
            if (_itemContainers.TryGetValue(id, out var container))
            {
                if (container.Parent is StackPanel panel)
                    panel.Children.Remove(container);
                _itemContainers.Remove(id);
            }
            _loadedItems.RemoveAll(m => GetItemId(m) == id);

            // 移除对应的右键菜单项
            for (int i = _contextMenu.Items.Count - 1; i >= 0; i--)
            {
                if (_contextMenu.Items[i] is MenuItem menuItem && menuItem.Tag is string menuId && menuId == id)
                {
                    _contextMenu.Items.RemoveAt(i);
                    break;
                }
            }
        }

        private static string GetItemId(StatusBarMeta meta)
        {
            return meta.Id ?? meta.Name ?? meta.GetHashCode().ToString();
        }

        private void AddItem(StatusBarMeta meta, StackPanel panel)
        {
            var container = CreateItemContainer(meta);
            container.Tag = meta;
            var id = GetItemId(meta);
            _itemContainers[id] = container;
            panel.Children.Add(container);
            AddContextMenuItem(meta, container);
        }

        private FrameworkElement CreateItemContainer(StatusBarMeta meta)
        {
            var border = new Border
            {
                Padding = new Thickness(6, 0, 6, 0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = Cursors.Hand,
            };

            if (!string.IsNullOrEmpty(meta.Description))
                border.ToolTip = meta.Description;

            border.DataContext = meta.Source;

            // 鼠标悬停高亮效果 (类似 VS Code)
            if (meta.ActionType == StatusBarActionType.Popup && meta.PopupContentFactory != null)
            {
                // 有 Popup 的项：悬停高亮 + 悬停2秒弹出 + 点击弹出
                DispatcherTimer hoverTimer = null;
                DispatcherTimer closeTimer = null;
                Popup activePopup = null;
                bool openedByClick = false;

                border.MouseEnter += (s, e) =>
                {
                    border.Background = HoverBrush;
                    // 禁用默认 ToolTip，因为有 Popup
                    border.ToolTip = null;

                    if (activePopup?.IsOpen == true) return;

                    // 悬停2秒后自动弹出
                    hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    hoverTimer.Tick += (t, te) =>
                    {
                        hoverTimer.Stop();
                        hoverTimer = null;
                        openedByClick = false;
                        activePopup = CreateAndShowPopup(meta, border, staysOpen: true);
                        AttachPopupHoverHandlers(activePopup, border, ref closeTimer, ref activePopup);
                    };
                    hoverTimer.Start();
                };

                border.MouseLeave += (s, e) =>
                {
                    border.Background = Brushes.Transparent;
                    hoverTimer?.Stop();
                    hoverTimer = null;

                    // 悬停打开的 Popup：鼠标离开 anchor 后延迟关闭（给用户时间移到 Popup 上）
                    if (activePopup?.IsOpen == true && !openedByClick)
                    {
                        closeTimer?.Stop();
                        closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                        closeTimer.Tick += (t, te) =>
                        {
                            closeTimer.Stop();
                            closeTimer = null;
                            if (activePopup != null) { activePopup.IsOpen = false; activePopup = null; }
                        };
                        closeTimer.Start();
                    }
                };

                // 左键点击：弹出并保持，点击其他区域消失
                border.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    hoverTimer?.Stop();
                    hoverTimer = null;
                    closeTimer?.Stop();
                    closeTimer = null;

                    if (activePopup?.IsOpen == true)
                    {
                        activePopup.IsOpen = false;
                        activePopup = null;
                        return;
                    }

                    openedByClick = true;
                    activePopup = CreateAndShowPopup(meta, border, staysOpen: false);
                    if (activePopup != null)
                    {
                        var popup = activePopup;
                        popup.Closed += (ps, pe) => { if (activePopup == popup) activePopup = null; };
                    }
                };
            }
            else
            {
                border.MouseEnter += (s, e) => border.Background = HoverBrush;
                border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

                if (meta.Command != null)
                {
                    border.MouseLeftButtonDown += (s, e) =>
                    {
                        e.Handled = true;
                        if (meta.Command.CanExecute(e))
                            meta.Command.Execute(e);
                    };
                }
            }

            // 创建内容
            border.Child = CreateContent(meta);

            // 可见性由用户通过右键菜单控制
            if (!meta.IsVisible)
            {
                border.Visibility = Visibility.Collapsed;
            }

            return border;
        }

        private UIElement CreateContent(StatusBarMeta meta)
        {
            return meta.Type switch
            {
                StatusBarType.Icon => CreateIconContent(meta),
                StatusBarType.Text => CreateTextContent(meta),
                StatusBarType.IconText => CreateIconTextContent(meta),
                _ => CreateTextContent(meta),
            };
        }

        private UIElement CreateIconContent(StatusBarMeta meta)
        {
            // 直接内容
            if (meta.IconContent != null)
            {
                return new ContentControl
                {
                    Content = meta.IconContent,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }

            // 资源键
            if (!string.IsNullOrEmpty(meta.IconResourceKey))
            {
                return CreateIconFromResourceKey(meta.IconResourceKey, 16);
            }

            // 兜底: 显示文字
            return CreateTextContent(meta);
        }

        private UIElement CreateTextContent(StatusBarMeta meta)
        {
            var textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            if (!string.IsNullOrEmpty(meta.BindingName))
            {
                var binding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                textBlock.SetBinding(TextBlock.TextProperty, binding);
            }
            else if (!string.IsNullOrEmpty(meta.Description))
            {
                textBlock.Text = meta.Description;
            }

            return textBlock;
        }

        private UIElement CreateIconTextContent(StatusBarMeta meta)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // 图标部分
            if (!string.IsNullOrEmpty(meta.IconResourceKey))
            {
                var icon = CreateIconFromResourceKey(meta.IconResourceKey, 14);
                icon.Margin = new Thickness(0, 0, 4, 0);
                panel.Children.Add(icon);
            }
            else if (meta.IconContent != null)
            {
                var content = new ContentControl
                {
                    Content = meta.IconContent,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0),
                };
                panel.Children.Add(content);
            }

            // 文字部分
            var textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");

            var textBindingName = meta.TextBindingName ?? meta.BindingName;
            if (!string.IsNullOrEmpty(textBindingName))
            {
                var binding = new Binding(textBindingName) { Mode = BindingMode.OneWay };
                textBlock.SetBinding(TextBlock.TextProperty, binding);
            }

            panel.Children.Add(textBlock);
            return panel;
        }

        private static FrameworkElement CreateIconFromResourceKey(string resourceKey, double size)
        {
            // 尝试获取资源，判断类型
            var resource = Application.Current.TryFindResource(resourceKey);
            if (resource is ImageSource)
            {
                // DrawingImage / BitmapImage 等 ImageSource 资源
                var image = new Image
                {
                    Width = size,
                    Height = size,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                image.SetResourceReference(Image.SourceProperty, resourceKey);
                return image;
            }

            // DrawingBrush 等作为 Fill 的资源
            var viewbox = new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var rect = new Rectangle { Width = size, Height = size };
            rect.SetResourceReference(Shape.FillProperty, resourceKey);
            viewbox.Child = rect;
            return viewbox;
        }

        private Popup CreateAndShowPopup(StatusBarMeta meta, FrameworkElement anchor, bool staysOpen)
        {
            var popupContent = meta.PopupContentFactory?.Invoke();
            if (popupContent == null) return null;

            var popupBorder = new Border
            {
                Child = popupContent,
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
            };
            popupBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            popupBorder.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");

            var popup = new Popup
            {
                Child = popupBorder,
                PlacementTarget = anchor,
                Placement = PlacementMode.Top,
                StaysOpen = staysOpen,
                AllowsTransparency = true,
            };

            popup.IsOpen = true;
            return popup;
        }

        /// <summary>
        /// 为悬停打开的 Popup 附加鼠标进出处理，实现"鼠标在 Popup 上时保持打开，离开后关闭"。
        /// </summary>
        private void AttachPopupHoverHandlers(Popup popup, FrameworkElement anchor,
            ref DispatcherTimer closeTimerRef, ref Popup activePopupRef)
        {
            if (popup?.Child is not FrameworkElement popupChild) return;

            var closeTimerCapture = closeTimerRef;
            var activePopupCapture = activePopupRef;

            // 使用包装对象在闭包间共享引用
            var state = new PopupHoverState { Popup = popup };

            popupChild.MouseEnter += (s, e) =>
            {
                // 鼠标进入 Popup，取消关闭计时
                state.CloseTimer?.Stop();
                state.CloseTimer = null;
            };

            popupChild.MouseLeave += (s, e) =>
            {
                // 鼠标离开 Popup，延迟关闭
                state.CloseTimer?.Stop();
                state.CloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                state.CloseTimer.Tick += (t, te) =>
                {
                    state.CloseTimer.Stop();
                    state.CloseTimer = null;
                    if (state.Popup != null) { state.Popup.IsOpen = false; state.Popup = null; }
                };
                state.CloseTimer.Start();
            };
        }

        private class PopupHoverState
        {
            public Popup Popup;
            public DispatcherTimer CloseTimer;
        }

        private void AddContextMenuItem(StatusBarMeta meta, FrameworkElement container, int insertIndex = -1)
        {
            if (string.IsNullOrEmpty(meta.Name)) return;

            var menuItem = new MenuItem
            {
                Header = meta.Name,
                IsCheckable = true,
                Tag = GetItemId(meta),
            };

            // 可见性纯用户控制，通过右键菜单切换
            menuItem.IsChecked = meta.IsVisible;
            menuItem.Checked += (s, e) => container.Visibility = Visibility.Visible;
            menuItem.Unchecked += (s, e) => container.Visibility = Visibility.Collapsed;

            if (insertIndex >= 0 && insertIndex < _contextMenu.Items.Count)
                _contextMenu.Items.Insert(insertIndex, menuItem);
            else
                _contextMenu.Items.Add(menuItem);
        }
    }
}
