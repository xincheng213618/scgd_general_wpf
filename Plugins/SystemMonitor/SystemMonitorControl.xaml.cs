using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SystemMonitor.Properties;

namespace ColorVision.Settings
{


    public class SystemMonitorProvider : IConfigSettingProvider,IMenuItemProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resources.PerformanceTest,
                                Description = Resources.PerformanceTest,
                                Order =10,
                                Type = ConfigSettingType.TabItem,
                                Source = SystemMonitors.GetInstance(),
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
                    Header =Resources.PerformanceTest,
                    Order=500,
                    Command = new Common.MVVM.RelayCommand(A =>{ 
                        Window window = new Window(){ Title =Resources.PerformanceTest , Owner =Application.Current.GetActiveWindow()};
                        window.Content = new SystemMonitorControl();
                        window.Show();
                    }  )


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
            this.DataContext = SystemMonitors.GetInstance();
        }
    }
}
