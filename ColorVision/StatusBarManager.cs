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

namespace ColorVision.UI
{
    public class StatusBarManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatusBarManager));
        private static StatusBarManager _instance;
        private static readonly object _locker = new();
        public static StatusBarManager GetInstance() { lock (_locker) { return _instance ??= new StatusBarManager(); } }

        public Grid StatusBarGrid { get; private set; }
        public StackPanel StatusBarTextDocker { get; private set; }

        private ContextMenu _contextMenu;

        private StatusBarManager() { }

        public void Init(Grid statusBarGrid, StackPanel statusBarTextDocker)
        {
            StatusBarGrid = statusBarGrid;
            StatusBarTextDocker = statusBarTextDocker;
            _contextMenu = new ContextMenu();
            StatusBarGrid.ContextMenu = _contextMenu;
            
            LoadStatusBarFromAssembly();
        }

        public void LoadStatusBarFromAssembly()
        {
            if (StatusBarGrid == null || StatusBarTextDocker == null) return;

            StatusBarTextDocker.Children.Clear();
            _contextMenu.Items.Clear();

            var allSettings = new List<StatusBarMeta>();

            // Scan assemblies for IStatusBarProvider
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => typeof(IStatusBarProvider).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        if (Activator.CreateInstance(type) is IStatusBarProvider configSetting)
                        {
                            var metas = configSetting.GetStatusBarIconMetadata();
                            if (metas != null)
                                allSettings.AddRange(metas);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to load StatusBarProvider {type.Name}: {ex.Message}");
                    }
                }
            }

            // Group by Type, then sort by Order within groups
            var sortedSettings = allSettings
                .GroupBy(setting => setting.Type)
                .SelectMany(group => group.OrderBy(setting => setting.Order));

            foreach (var item in sortedSettings)
            {
                AddStatusBarIconMetadata(item);
            }
        }

        private void AddStatusBarIconMetadata(StatusBarMeta meta)
        {
            if (meta.Type == StatusBarType.Icon)
            {
                CreateIconStatusBarItem(meta);
            }
            else if (meta.Type == StatusBarType.Text)
            {
                CreateTextStatusBarItem(meta);
            }
        }

        private void CreateIconStatusBarItem(StatusBarMeta meta)
        {
            // 1. Create StatusBarItem
            StatusBarItem statusBarItem = new StatusBarItem { ToolTip = meta.Description };
            statusBarItem.DataContext = meta.Source;

            if (meta.VisibilityBindingName != null)
            {
                var visibilityBinding = new Binding(meta.VisibilityBindingName)
                {
                    Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                };
                statusBarItem.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
            }

            if (meta.Command != null)
            {
                statusBarItem.MouseLeftButtonDown += (s, e) => meta.Command.Execute(e);
            }

            // 2. Create ToggleButton content
            ToggleButton toggleButton = new ToggleButton { IsEnabled = false };
            if (!string.IsNullOrEmpty(meta.ButtonStyleName) && Application.Current.TryFindResource(meta.ButtonStyleName) is Style styleResource)
                toggleButton.Style = styleResource;

            if (!string.IsNullOrEmpty(meta.BindingName))
            {
                var isCheckedBinding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                toggleButton.SetBinding(ToggleButton.IsCheckedProperty, isCheckedBinding);
            }
            
            toggleButton.DataContext = meta.Source;
            statusBarItem.Content = toggleButton;

            StatusBarTextDocker.Children.Add(statusBarItem);

            // 3. Add to ContextMenu
            AddContextMenuOption(meta);
        }

        private void CreateTextStatusBarItem(StatusBarMeta meta)
        {
            StatusBarItem statusBarItem = new StatusBarItem();
            statusBarItem.DataContext = meta.Source;

            if (!string.IsNullOrEmpty(meta.BindingName))
            {
                var binding = new Binding(meta.BindingName) { Mode = BindingMode.OneWay };
                statusBarItem.SetBinding(ContentControl.ContentProperty, binding);
            }

            if (meta.VisibilityBindingName != null)
            {
                var visibilityBinding = new Binding(meta.VisibilityBindingName)
                {
                    Converter = (IValueConverter)Application.Current.FindResource("bool2VisibilityConverter")
                };
                statusBarItem.SetBinding(UIElement.VisibilityProperty, visibilityBinding);
            }

            StatusBarTextDocker.Children.Add(statusBarItem);

            // Add to ContextMenu
            AddContextMenuOption(meta);
        }

        private void AddContextMenuOption(StatusBarMeta meta)
        {
            MenuItem menuItem = new MenuItem() { Header = meta.Name };
            menuItem.Click += (s, e) => menuItem.IsChecked = !menuItem.IsChecked;
            menuItem.DataContext = meta.Source;

            if (meta.VisibilityBindingName != null)
            {
                var isCheckedBinding = new Binding(meta.VisibilityBindingName)
                {
                    Mode = BindingMode.TwoWay,
                };
                menuItem.SetBinding(MenuItem.IsCheckedProperty, isCheckedBinding);
            }
            _contextMenu.Items.Add(menuItem);
        }
    }
}