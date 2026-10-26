using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Themes;

namespace ColorVision.UI.Languages
{

    public class LanguagePropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            var comboBox = new ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 0,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = LanguageManager.Current.Languages.Distinct().OrderBy(code => code == CultureInfo.InstalledUICulture.Name ? 0 : 1)
                    .Select(code => new KeyValuePair<string, string>(code, DisplayName(code))).ToList()
            };
            comboBox.SetResourceReference(FrameworkElement.StyleProperty, "SettingsComboBoxStyle");
            ComboBoxSearch.SetIsEnabled(comboBox, true);
            ComboBoxSearch.SetWatermark(comboBox, Properties.Resources.ResourceManager.GetString("SearchLanguages", CultureInfo.CurrentUICulture) ?? "Search languages");

            string previousSelection = (string?)property.GetValue(obj) ?? CultureInfo.InstalledUICulture.Name;
            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);


            comboBox.SelectionChanged += (s, e)=>
            {
                if (comboBox.SelectedValue is string str && str != previousSelection)
                {
                    if (!LanguageManager.Current.LanguageChange(str))
                    {
                        property.SetValue(obj, previousSelection);
                        comboBox.SetCurrentValue(ComboBox.SelectedValueProperty, previousSelection);
                    }
                    else previousSelection = str;
                }

            };

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }

        private static string DisplayName(string code)
        {
            if (code == CultureInfo.InstalledUICulture.Name) return LanguageManager.AutoDetectDisplayName;
            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "English";
            if (code is "zh-Hant" or "zh-TW" or "zh-HK") return "繁體中文";
            if (code is "zh-Hans" or "zh-CN") return "简体中文";
            return LanguageManager.GetDisplayName(code);
        }
    }


    public class LanguageConfig:IConfig
    {
        public static LanguageConfig Instance => ConfigService.Instance.GetRequiredService<LanguageConfig>();

        /// <summary>
        /// 语言
        /// </summary>
        [ConfigSetting(Order = -30, Section = ConfigSettingConstants.SectionAppearance)]
        [DisplayName("Language"), Description("LanguageDescription"), PropertyEditorType(typeof(LanguagePropertiesEditor))]
        public string UICulture
        {
            get => LanguageManager.GetDefaultLanguages().Contains(_UICulture) ? _UICulture : CultureInfo.InstalledUICulture.Name;
            set { _UICulture = value; }
        }
        private string _UICulture = CultureInfo.InstalledUICulture.Name;
    }
}
