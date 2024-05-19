using ColorVision.Properties;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using ColorVision.UI.Views;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision
{
    public class MainWindowConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resource.StartRecoverUILayout,
                                Description = Resource.StartRecoverUILayout,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(MainWindowConfig.IsRestoreWindow),
                                Source = MainWindowConfig.Instance
                            }
            };
        }
    }


    public class MainWindowConfig : IConfig
    {
        public static MainWindowConfig Instance => ConfigHandler.GetInstance().GetRequiredService<MainWindowConfig>();

        public bool IsRestoreWindow { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }

        public void SetWindow(Window window)
        {
            if (IsRestoreWindow && Height != 0 && Width != 0)
            {
                window.Top = Top;
                window.Left = Left;
                window.Height = Height;
                window.Width = Width;
                window.WindowState = (WindowState)WindowState;

                if (Width > SystemParameters.WorkArea.Width)
                {
                    window.Width = SystemParameters.WorkArea.Width;
                }
                if (Height > SystemParameters.WorkArea.Height)
                {
                    window.Height = SystemParameters.WorkArea.Height;
                }
            }
        }
        public void SetConfig(Window window)
        {
            Top = window.Top;
            Left = window.Left;
            Height = window.Height;
            Width = window.Width;
            WindowState = (int)window.WindowState;
        }
    }
}
