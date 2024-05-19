using ColorVision.Properties;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Properties;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision
{
    public class StartRecoverUILayout : IConfigSetting
    {
        public string Name => Resource.StartRecoverUILayout;

        public string Description => Resource.StartRecoverUILayout;

        public string BindingName => "IsRestoreWindow";

        public object Source => MainWindowConfig.Instance;

        public ConfigSettingType Type => ConfigSettingType.Bool;

        public UserControl UserControl => throw new System.NotImplementedException();

        public ComboBox ComboBox => throw new System.NotImplementedException();
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
