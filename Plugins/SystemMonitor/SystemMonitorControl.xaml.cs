using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SystemMonitor.Properties;

namespace SystemMonitor
{
    public class SystemMonitorProvider : IConfigSettingProvider, IMenuItemProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                new ConfigSettingMetadata
                {
                    Name = Resources.PerformanceTest,
                    Description = Resources.PerformanceTest,
                    Order = 10,
                    Type = ConfigSettingType.TabItem,
                    Source = SystemMonitors.GetInstance(),
                    ViewType = typeof(SystemMonitorControl),
                }
            };
        }

        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata()
                {
                    OwnerGuid = "Tool",
                    GuidId = "SystemMonitor",
                    Header = Resources.PerformanceTest,
                    Order = 500,
                    Command = new ColorVision.Common.MVVM.RelayCommand(a =>
                    {
                        Window window = new Window()
                        {
                            Title = Resources.PerformanceTest,
                            Owner = Application.Current.GetActiveWindow(),
                            Width = 860,
                            Height = 720,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        };
                        window.Content = new SystemMonitorControl();
                        window.ApplyCaption();
                        window.Show();
                    })
                }
            };
        }
    }

    public partial class SystemMonitorControl : UserControl
    {
        private SystemMonitors? _monitor;
        private bool _isMonitoringActive;

        public SystemMonitorControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            _monitor = SystemMonitors.GetInstance();
            DataContext = _monitor;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isMonitoringActive) return;

            _monitor ??= SystemMonitors.GetInstance();
            _monitor.SetDetailViewActive(true);
            _isMonitoringActive = true;
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoringActive || _monitor == null) return;

            _monitor.SetDetailViewActive(false);
            _isMonitoringActive = false;
        }
    }
}
