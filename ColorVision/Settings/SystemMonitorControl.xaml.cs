using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ColorVision.Settings
{
    public class SystemMonitorProvider : IConfigSettingProvider,IStatusBarIconProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Properties.Resource.PerformanceTest,
                                Description = Properties.Resource.PerformanceTest,
                                Order =10,
                                Type = ConfigSettingType.TabItem,
                                Source = SystemMonitor.GetInstance(),
                                UserControl = new SystemMonitorControl(),

                            }
            };
        }
        public IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata()
        {
            return new List<StatusBarIconMetadata>
            {
                new StatusBarIconMetadata()
                {
                    Name = Properties.Resource.PerformanceTest,
                    Description = Properties.Resource.PerformanceTest,
                    Order =12,
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitor.Time),
                    VisibilityBindingName ="Config.IsShowTime",
                    Source = SystemMonitor.GetInstance()
                },
                new StatusBarIconMetadata()
                {
                    Name = Properties.Resource.PerformanceTest,
                    Description = Properties.Resource.PerformanceTest,
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
