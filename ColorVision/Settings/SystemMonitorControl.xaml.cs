using ColorVision.Common.Utilities;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Settings
{
    public class SystemMonitorProvider : IConfigSettingProvider,IStatusBarIconProvider,IMenuItemProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Properties.Resources.PerformanceTest,
                                Description = Properties.Resources.PerformanceTest,
                                Order =10,
                                Type = ConfigSettingType.TabItem,
                                Source = SystemMonitor.GetInstance(),
                                UserControl = new SystemMonitorControl(),

                            }
            };
        }

        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata()
                {
                    OwnerGuid ="Tool",
                    GuidId ="SystemMonitor",
                    Header =Properties.Resources.PerformanceTest,
                    Order=500,
                    Command = new Common.MVVM.RelayCommand(A =>{ 
                        Window window = new Window(){ Title =Properties.Resources.PerformanceTest , Owner =Application.Current.GetActiveWindow()};
                        window.Content = new SystemMonitorControl();
                        window.Show();
                    }  )


                }
            };
        }

        public IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata()
        {
            return new List<StatusBarIconMetadata>
            {
                new StatusBarIconMetadata()
                {
                    Name = "Time",
                    Description = Properties.Resources.PerformanceTest,
                    Order =12,
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitor.Time),
                    VisibilityBindingName ="Config.IsShowTime",
                    Source = SystemMonitor.GetInstance()
                },
                new StatusBarIconMetadata()
                {
                    Name = "RAM",
                    Description = Properties.Resources.PerformanceTest,
                    Order =10,
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitor.MemoryThis),
                    VisibilityBindingName ="Config.IsShowRAM",
                    Source = SystemMonitor.GetInstance()
                }
            };
        }


    }

    /// <summary>
    /// SystemMonitorControl.xaml 的交互逻辑
    /// </summary>
    public partial class SystemMonitorControl : UserControl
    {
        public SystemMonitorControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SystemMonitor.GetInstance();
        }
    }
}
