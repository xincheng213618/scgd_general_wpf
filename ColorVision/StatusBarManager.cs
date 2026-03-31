using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using log4net;
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

namespace ColorVision.UI
{
    public class StatusBarManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusBarManager));
        private static StatusBarManager _instance;
        private static readonly object _locker = new();
        public static StatusBarManager GetInstance() { lock (_locker) { return _instance ??= new StatusBarManager(); } }

        private readonly Dictionary<string, StatusBarWindowContext> _windows = new();
        private List<StatusBarMeta> _allMetas;

        private StatusBarManager() { }

        /// <summary>
        /// 初始化状态栏，为指定窗口注册状态栏容器
        /// </summary>
        /// <param name="targetName">目标窗口名称</param>
        /// <param name="container">状态栏容器Grid</param>
        public void Init(string targetName, Grid container)
        {
            var context = new StatusBarWindowContext(container);
            _windows[targetName] = context;
            LoadStatusBarForWindow(targetName);
        }

        /// <summary>
        /// 兼容旧接口：使用MainWindowTarget初始化
        /// </summary>
        [Obsolete("Use Init(string targetName, Grid container) instead")]
        public void Init(Grid statusBarGrid, StackPanel statusBarTextDocker)
        {
            var parent = statusBarGrid.Parent as Grid;
            if (parent == null)
            {
                log.Warn("StatusBarManager.Init: statusBarGrid has no Grid parent, falling back to direct container.");
                return;
            }

            parent.Children.Clear();
            parent.ColumnDefinitions.Clear();
            parent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
            parent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            parent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            Init(StatusBarConstants.MainWindowTarget, parent);
        }

        private void CollectAllMetas()
        {
            _allMetas = new List<StatusBarMeta>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IStatusBarProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IStatusBarProvider provider)
                        {
                            var metas = provider.GetStatusBarIconMetadata();
                            if (metas != null)
                                _allMetas.AddRange(metas);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to load StatusBarProvider {type.Name}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 为指定窗口加载状态栏
        /// </summary>
        public void LoadStatusBarForWindow(string targetName)
        {
            if (!_windows.TryGetValue(targetName, out var context)) return;

            // 重新收集元数据
            CollectAllMetas();

            // 按TargetName过滤：匹配当前窗口或Global
            var filtered = _allMetas.Where(m =>
                m.TargetName == targetName ||
                m.TargetName == StatusBarConstants.GlobalTarget).ToList();

            // 分为左右两组，按Order排序
            var leftItems = filtered
                .Where(m => GetAlignment(m) == StatusBarAlignment.Left)
                .OrderBy(m => m.Order).ToList();

            var rightItems = filtered
                .Where(m => GetAlignment(m) == StatusBarAlignment.Right)
                .OrderBy(m => m.Order).ToList();

            context.Clear();

            foreach (var meta in leftItems)
                CreateStatusBarItem(meta, context.LeftPanel);

            foreach (var meta in rightItems)
                CreateStatusBarItem(meta, context.RightPanel);

            BuildContextMenu(context, filtered);
        }

        /// <summary>
        /// 重新加载所有已注册窗口的状态栏
        /// </summary>
        public void RefreshAll()
        {
            foreach (var targetName in _windows.Keys.ToList())
                LoadStatusBarForWindow(targetName);
        }

        private static StatusBarAlignment GetAlignment(StatusBarMeta meta)
        {
            if (meta.Alignment.HasValue)
                return meta.Alignment.Value;

            return meta.Type switch
            {
                StatusBarType.Text => StatusBarAlignment.Right,
                _ => StatusBarAlignment.Left
            };
        }

        private void CreateStatusBarItem(StatusBarMeta meta, StackPanel panel)
        {
            switch (meta.Type)
            {
                case StatusBarType.Icon:
                    CreateIconItem(meta, panel);
                    break;
                case StatusBarType.Text:
                    CreateTextItem(meta, panel);
                    break;
                case StatusBarType.IconText:
                    CreateIconTextItem(meta, panel);
                    break;
            }
        }

        private static Border CreateItemContainer(StatusBarMeta meta)
        {
            var border = new Border
            {
                Padding = new Thickness(6, 0, 6, 0),
                Background = Brushes.Transparent,
                Cursor = meta.Command != null ? Cursors.Hand : Cursors.Arrow,
                VerticalAlignment = VerticalAlignment.Stretch,
                ToolTip = meta.Description,
            };

            border.DataContext = meta.Source;

            // 鼠标悬停效果
            border.MouseEnter += (s, e) =>
            {
                if (s is Border b) b.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            };
            border.MouseLeave += (s, e) =>
            {
                if (s is Border b) b.Background = Brushes.Transparent;
            };

            // 左键点击命令
            if (meta.Command != null)
            {
                border.MouseLeftButtonDown += (s, e) =>
                {
                    meta.Command.Execute(e);
                    e.Handled = true;
                };
            }

            // 右键点击命令
            if (meta.RightClickCommand != null)
            {
                border.MouseRightButtonDown += (s, e) =>
                {
                    meta.RightClickCommand.Execute(e);
                    e.Handled = true;
                };
            }

            // 可见性绑定
            if (meta.VisibilityBindingName != null)
            {
                var visibilityBinding = new Binding(meta.VisibilityBindingName)
                {
                    Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                };
                border.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
            }

            return border;
        }

        private static void CreateIconItem(StatusBarMeta meta, StackPanel panel)
        {
            var container = CreateItemContainer(meta);

            ToggleButton toggleButton = new ToggleButton { IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(meta.ButtonStyleName) && Application.Current.TryFindResource(meta.ButtonStyleName) is Style styleResource)
                toggleButton.Style = styleResource;

            if (!string.IsNullOrEmpty(meta.BindingName))
            {
                var isCheckedBinding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                toggleButton.SetBinding(ToggleButton.IsCheckedProperty, isCheckedBinding);
            }

            toggleButton.DataContext = meta.Source;
            container.Child = toggleButton;
            panel.Children.Add(container);
        }

        private static void CreateTextItem(StatusBarMeta meta, StackPanel panel)
        {
            var container = CreateItemContainer(meta);

            var textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");

            if (!string.IsNullOrEmpty(meta.BindingName))
            {
                var binding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                textBlock.SetBinding(TextBlock.TextProperty, binding);
            }

            textBlock.DataContext = meta.Source;
            container.Child = textBlock;
            panel.Children.Add(container);
        }

        private static void CreateIconTextItem(StatusBarMeta meta, StackPanel panel)
        {
            var container = CreateItemContainer(meta);

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };

            // 图标部分
            if (!string.IsNullOrEmpty(meta.IconResourceKey))
            {
                if (Application.Current.TryFindResource(meta.IconResourceKey) is Brush)
                {
                    var rect = new Rectangle { Width = 14, Height = 14 };
                    rect.SetResourceReference(Rectangle.FillProperty, meta.IconResourceKey);
                    var viewbox = new Viewbox { Width = 14, Height = 14, Child = rect, Margin = new Thickness(0, 0, 4, 0) };
                    stackPanel.Children.Add(viewbox);
                }
            }
            else if (!string.IsNullOrEmpty(meta.ButtonStyleName))
            {
                ToggleButton toggleButton = new ToggleButton { IsEnabled = false, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
                if (Application.Current.TryFindResource(meta.ButtonStyleName) is Style styleResource)
                    toggleButton.Style = styleResource;

                if (!string.IsNullOrEmpty(meta.BindingName))
                {
                    var isCheckedBinding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                    toggleButton.SetBinding(ToggleButton.IsCheckedProperty, isCheckedBinding);
                }
                toggleButton.DataContext = meta.Source;
                stackPanel.Children.Add(toggleButton);
            }

            // 文本部分
            var textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
            };

            var textBindingName = meta.TextBindingName ?? meta.BindingName;
            if (!string.IsNullOrEmpty(textBindingName))
            {
                var binding = new Binding(textBindingName) { Mode = BindingMode.OneWay };
                textBlock.SetBinding(TextBlock.TextProperty, binding);
            }

            textBlock.DataContext = meta.Source;
            stackPanel.Children.Add(textBlock);

            container.Child = stackPanel;
            panel.Children.Add(container);
        }

        private void BuildContextMenu(StatusBarWindowContext context, List<StatusBarMeta> items)
        {
            var contextMenu = new ContextMenu();

            foreach (var meta in items.Where(m => !string.IsNullOrEmpty(m.Name)))
            {
                var menuItem = new MenuItem { Header = meta.Name };
                menuItem.DataContext = meta.Source;

                if (meta.VisibilityBindingName != null)
                {
                    var isCheckedBinding = new Binding(meta.VisibilityBindingName)
                    {
                        Mode = BindingMode.TwoWay,
                    };
                    menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding);
                }
                else
                {
                    menuItem.IsChecked = true;
                    menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
                }

                contextMenu.Items.Add(menuItem);
            }

            context.Container.ContextMenu = contextMenu;
        }

        /// <summary>
        /// 状态栏窗口上下文，管理单个窗口的状态栏布局
        /// </summary>
        private class StatusBarWindowContext
        {
            public Grid Container { get; }
            public StackPanel LeftPanel { get; }
            public StackPanel RightPanel { get; }

            public StatusBarWindowContext(Grid container)
            {
                Container = container;

                LeftPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                RightPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Stretch,
                };

                Grid.SetColumn(LeftPanel, 0);
                Grid.SetColumn(RightPanel, 2);

                container.Children.Add(LeftPanel);
                container.Children.Add(RightPanel);
            }

            public void Clear()
            {
                LeftPanel.Children.Clear();
                RightPanel.Children.Clear();
            }
        }
    }
}