using ColorVision.Common.Utilities;
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

namespace ColorVision.UI.StatusBar
{
    /// <summary>
    /// VS Code 风格的可复用状态栏控件。
    /// 支持左右分区、TargetName 过滤、上下文菜单、动态文档感知等特性。
    /// </summary>
    public partial class StatusBarControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusBarControl));

        /// <summary>
        /// 目标窗口名称，用于过滤 IStatusBarProvider 提供的状态栏项。
        /// 匹配 TargetName 或 Global 的项会被加载。
        /// </summary>
        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(StatusBarControl),
                new PropertyMetadata(StatusBarConstants.GlobalTarget, OnTargetNameChanged));

        public string TargetName
        {
            get => (string)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        private static void OnTargetNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusBarControl control)
                control.LoadItems();
        }

        private readonly List<StatusBarMeta> _dynamicItems = new();

        public StatusBarControl()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadItems();
        }

        /// <summary>
        /// 加载状态栏项（从程序集扫描 IStatusBarProvider）
        /// </summary>
        public void LoadItems()
        {
            LeftPanel.Children.Clear();
            RightPanel.Children.Clear();

            var allMetas = CollectMetas();

            // 按 TargetName 过滤
            var filtered = allMetas.Where(m =>
                m.TargetName == TargetName ||
                m.TargetName == StatusBarConstants.GlobalTarget).ToList();

            // 追加动态项
            filtered.AddRange(_dynamicItems);

            var leftItems = filtered
                .Where(m => GetAlignment(m) == StatusBarAlignment.Left)
                .OrderBy(m => m.Order).ToList();

            var rightItems = filtered
                .Where(m => GetAlignment(m) == StatusBarAlignment.Right)
                .OrderBy(m => m.Order).ToList();

            foreach (var meta in leftItems)
                CreateStatusBarItem(meta, LeftPanel);

            foreach (var meta in rightItems)
                CreateStatusBarItem(meta, RightPanel);

            BuildContextMenu(StatusBarGrid, filtered);
        }

        /// <summary>
        /// 设置动态状态栏项（例如由活动文档提供的上下文状态栏项）
        /// </summary>
        public void SetDynamicItems(IEnumerable<StatusBarMeta> items)
        {
            _dynamicItems.Clear();
            if (items != null)
                _dynamicItems.AddRange(items);
            LoadItems();
        }

        /// <summary>
        /// 清除动态状态栏项
        /// </summary>
        public void ClearDynamicItems()
        {
            if (_dynamicItems.Count > 0)
            {
                _dynamicItems.Clear();
                LoadItems();
            }
        }

        private static List<StatusBarMeta> CollectMetas()
        {
            var metas = new List<StatusBarMeta>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IStatusBarProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IStatusBarProvider provider)
                        {
                            var items = provider.GetStatusBarIconMetadata();
                            if (items != null)
                                metas.AddRange(items);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to load StatusBarProvider {type.Name}: {ex.Message}");
                    }
                }
            }

            return metas;
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

        private static void CreateStatusBarItem(StatusBarMeta meta, StackPanel panel)
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

        private static void BuildContextMenu(Grid grid, List<StatusBarMeta> items)
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

            grid.ContextMenu = contextMenu;
        }
    }
}
