using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Views;
using ColorVision.Util.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Themes
{
    public class ThemeConfigSeting : IConfigSetting
    {
        public string Name => Resource.Theme;
        public string Description => Resource.Theme;
        public string BindingName => "Theme";
        public object Source => ThemeConfig.Instance;
        public ConfigSettingType Type => ConfigSettingType.ComboBox;
        public UserControl UserControl => throw new System.NotImplementedException();
        public ComboBox ComboBox
        {
            get
            {
                ComboBox cmtheme = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
                Binding themeBinding = new Binding("Theme");
                cmtheme.SetBinding(ComboBox.SelectedValueProperty, themeBinding);

                cmtheme.ItemsSource = from e1 in Enum.GetValues(typeof(Theme)).Cast<Theme>()
                                      select new KeyValuePair<Theme, string>(e1, Resource.ResourceManager.GetString(e1.ToDescription(), CultureInfo.CurrentUICulture) ?? "");

                cmtheme.SelectionChanged += (s, e) => Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);
                cmtheme.DataContext = ThemeConfig.Instance;
                return cmtheme;
            }
        }
    }

    public class ThemeConfigTransparentWindow : IConfigSetting
    {
        public string Name => Resource.TransparentWindow;
        public string Description => Resource.TransparentWindow;
        public string BindingName => "TransparentWindow";
        public object Source => ThemeConfig.Instance;
        public ConfigSettingType Type => ConfigSettingType.Bool;
        public UserControl UserControl => throw new NotImplementedException();
        public ComboBox ComboBox => throw new NotImplementedException();
    }


    public class ThemeConfig: ViewModelBase,IConfig
    {
        public static ThemeConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        public Theme Theme { get; set; } = Theme.UseSystem;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; NotifyPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }
}
