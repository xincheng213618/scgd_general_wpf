using ColorVision.Themes;
using ColorVision.UI.Properties;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
                MinWidth = PropertyEditorHelper.ControlMinWidth,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                DisplayMemberPath = "Value",
                SelectedValuePath = "Key",
                ItemsSource = from e1 in LanguageManager.Current.Languages
                              select new KeyValuePair<string, string>(e1, LanguageManager.keyValuePairs.TryGetValue(e1, out string value) ? value : e1)
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);


            string temp = Thread.CurrentThread.CurrentUICulture.Name;
            comboBox.SelectionChanged += (s, e)=>
            {
                if (comboBox.SelectedValue is string str)
                {
                    if (!LanguageManager.Current.LanguageChange(str))
                    {
                        property.SetValue(obj, temp);
                    }
                }

            };

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }


    public class LanguageConfig:IConfig, IConfigSettingProvider
    {
        public static LanguageConfig Instance => ConfigService.Instance.GetRequiredService<LanguageConfig>();

        /// <summary>
        /// 语言
        /// </summary>
        [DisplayName("Language"),PropertyEditorType(typeof(LanguagePropertiesEditor))]
        public string UICulture
        {
            get => LanguageManager.GetDefaultLanguages().Contains(_UICulture) ? _UICulture : CultureInfo.InstalledUICulture.Name;
            set { _UICulture = value; }
        }
        private string _UICulture = CultureInfo.InstalledUICulture.Name;



        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    BindingName = nameof(UICulture),
                    Source = Instance,
                }
            };
        }
    }
}
