using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Configs;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Microsoft.DwayneNeed.Win32.Gdi32;
using Microsoft.VisualBasic.Logging;
using NPOI.SS.Formula.Functions;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Settings
{
    public class SoftwareSettingProvider : ViewModelBase, IConfigSettingProvider
    {
        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "ColorVisionAutoRun";
        public bool IsAutoRun { get => Tool.IsAutoRun(AutoRunName, AutoRunRegPath); set { Tool.SetAutoRun(value, AutoRunName, AutoRunRegPath); NotifyPropertyChanged(); } }

        public static readonly List<string> LogLevels = new() { "all", "debug", "info", "warning", "error", "none" };
        public static IEnumerable<Level> GetAllLevels()
        {
            return new List<Level> { Level.All, Level.Trace, Level.Debug, Level.Info, Level.Warn, Level.Error, Level.Critical, Level.Alert, Level.Fatal, Level.Off};
        }
        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            ComboBox cmlog = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            cmlog.SetBinding(ComboBox.SelectedValueProperty, new Binding(nameof(SoftwareSetting.LogLevel)));

            cmlog.ItemsSource = GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));

            cmlog.SelectionChanged += (s, e) => {
                var selectedLevel = (KeyValuePair<Level, string>)cmlog.SelectedItem;
                var hierarchy = (Hierarchy)LogManager.GetRepository();
                if (selectedLevel.Key != hierarchy.Root.Level)
                {
                    hierarchy.Root.Level = selectedLevel.Key;
                    log4net.Config.BasicConfigurator.Configure(hierarchy);
                    log.Info("更新Log4Net 日志级别：" + selectedLevel.Value);
                }
            };
            cmlog.DataContext = SoftwareSetting.Instance;


            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.TbSettingsStartBoot,
                    Description =  Properties.Resources.TbSettingsStartBoot,
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(IsAutoRun),
                    Source = this,
                },
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.LogLevel,
                    Description =  Properties.Resources.LogLevel,
                    Order = 15,
                    Type = ConfigSettingType.ComboBox,
                    ComboBox = cmlog,
                },
            };
        }
    }


    public partial class SoftwareSetting :ViewModelBase,IConfig
    {
        public static SoftwareSetting Instance => ConfigHandler.GetInstance().GetRequiredService<SoftwareSetting>();

        private Level _LogLevel = Level.All;
        public Level LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                LogLevelName = value.Name;
            }
        }
        public string LogLevelName { get => LogLevel.Name; 
            set 
            {
                if (value != LogLevel.Name)
                {
                    LogLevel = SoftwareSettingProvider.GetAllLevels().FirstOrDefault(level => level.Name == value) ?? Level.All;
                }
            }
        }

        public string? Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string? _Version = string.Empty;
    }
}
