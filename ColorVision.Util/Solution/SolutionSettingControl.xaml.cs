﻿using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Util.Properties;

namespace ColorVision.Solution
{
    public class SystemMonitorProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resources.ProjectSettings,
                                Description = Resources.ProjectSettings,
                                Order = 2,
                                Type = ConfigSettingType.TabItem,
                                Source = SolutionSetting.Instance,
                                UserControl = new SolutionSettingControl(),

                            }
            };
        }
    }

    /// <summary>
    /// SolutionSettingControl.xaml 的交互逻辑
    /// </summary>
    public partial class SolutionSettingControl : UserControl
    {
        public SolutionSettingControl()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SolutionSetting.Instance;
        }

        private void SetProjectDefault__Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SolutionSetting.Instance.DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
                ButtonContentChange(button, ColorVision.Util.Properties.Resources.Reseted);
            }

        }

        private void SetProjectDefaultCreatName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                SolutionSetting.Instance.DefaultCreatName = "新建工程";
                ButtonContentChange(button, ColorVision.Util.Properties.Resources.Reseted);
            }
        }

        private static async void ButtonContentChange(Button button, string Content)
        {
            if (button.Content.ToString() != Content)
            {
                var temp = button.Content;
                button.Content = Content;
                await Task.Delay(1000);
                button.Content = temp;
            }
        }
    }
}