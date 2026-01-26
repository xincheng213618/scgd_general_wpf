using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Properties;
using ColorVision.UI;
using log4net.Core;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Themes
{
    public class ThemeConfig: ViewModelBase,IConfig
    {
        public static ThemeConfig Instance => ConfigService.Instance.GetRequiredService<ThemeConfig>();

        /// <summary>
        /// 主题
        /// </summary>
        [PropertyEditorType(typeof(ThemePropertiesEditor))]
        public Theme Theme { get; set; } = Theme.UseSystem;

        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; OnPropertyChanged(); } }
        private bool _TransparentWindow = true;
    }


    public class ThemePropertiesEditor : IPropertyEditor
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
                ItemsSource = from e1 in Enum.GetValues(typeof(Theme)).Cast<Theme>()
                              select new KeyValuePair<Theme, string>(e1, Resources.ResourceManager.GetString(e1.ToDescription(), CultureInfo.CurrentUICulture) ?? "")
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            comboBox.SelectionChanged +=(s,e)=> Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
