using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace System.ComponentModel
{
    public interface IPropertyEditor
    {
        DockPanel GenProperties(PropertyInfo property, object obj);
    }

    public class TextboxPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            Binding binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            binding.UpdateSourceTrigger = UpdateSourceTrigger.Default;

            var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (t == typeof(float) || property.PropertyType == typeof(double))
            {
                binding.StringFormat = "0.0################";
            }

            var textbox = PropertyEditorHelper.CreateSmallTextBox(binding);
            textbox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }
    }

    public class BoolPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
            => PropertyEditorHelper.GenBoolProperties(property, obj);
    }

    public class EnumPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
            => PropertyEditorHelper.GenEnumProperties(property, obj);
    }

    public class TextSelectFilePropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var textBinding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            var textbox = PropertyEditorHelper.CreateSmallTextBox(textBinding);

            var selectBtn = new Button { Content = "...", Margin = new Thickness(5, 0, 0, 0) };
            selectBtn.Click += (_, __) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                var path = property.GetValue(obj) as string;
#if NET8_0
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    ofd.DefaultDirectory = Directory.GetDirectoryRoot(path);
                }
#endif
                if (ofd.ShowDialog() == true)
                {
                    property.SetValue(obj, ofd.FileName);
                }
            };

            var openFolderBtn = new Button { Content = "🗁", Margin = new Thickness(5, 0, 0, 0), ToolTip = "打开所在文件夹" };
            openFolderBtn.Click += (_, __) =>
            {
                var path = property.GetValue(obj) as string;
                if (!string.IsNullOrWhiteSpace(path)) PlatformHelper.OpenFolder(path);
            };

            DockPanel.SetDock(selectBtn, Dock.Right);
            DockPanel.SetDock(openFolderBtn, Dock.Right);
            dockPanel.Children.Add(openFolderBtn);
            dockPanel.Children.Add(selectBtn);
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }
    }

    public class TextSelectFolderPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var textBinding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            var textbox = PropertyEditorHelper.CreateSmallTextBox(textBinding);

            var selectBtn = new Button { Content = "...", Margin = new Thickness(5, 0, 0, 0) };
            selectBtn.Click += (_, __) =>
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = property.GetValue(obj) as string ?? string.Empty };
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                    property.SetValue(obj, folderDialog.SelectedPath);
            };

            var openFolderBtn = new Button { Content = "🗁", Margin = new Thickness(5, 0, 0, 0), ToolTip = "打开文件夹" };
            openFolderBtn.Click += (_, __) =>
            {
                var path = property.GetValue(obj) as string;
                if (!string.IsNullOrWhiteSpace(path)) PlatformHelper.OpenFolder(path);
            };

            DockPanel.SetDock(selectBtn, Dock.Right);
            DockPanel.SetDock(openFolderBtn, Dock.Right);
            dockPanel.Children.Add(openFolderBtn);
            dockPanel.Children.Add(selectBtn);
            dockPanel.Children.Add(textbox);
            return dockPanel;
        }
    }

    public class CronExpressionPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var cronBtn = new Button { Content = "在线Cron表达式生成器", Margin = new Thickness(5, 0, 0, 0), ToolTip = "打开在线Cron表达式生成器" };
            cronBtn.Click += (_, __) => PlatformHelper.Open("https://cron.qqe2.com/");
            DockPanel.SetDock(cronBtn, Dock.Right);

            var cronTextBox = PropertyEditorHelper.CreateSmallTextBox(PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(cronBtn);
            dockPanel.Children.Add(cronTextBox);
            return dockPanel;
        }
    }

    public class TextSerialPortPropertiesEditor : IPropertyEditor
    {
        private static readonly List<string> SerialPorts = new() { "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM10", "COM11", "COM12", "COM13", "COM14", "COM15", "COM16" };
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true, ItemsSource = SerialPorts };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

    public class TextBaudRatePropertiesEditor : IPropertyEditor
    {
        private static readonly List<int> BaudRates = new() { 921600, 460800, 230400, 115200, 57600, 38400, 19200, 14400, 9600, 4800, 2400, 1200, 600, 300 };
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox { Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true, ItemsSource = BaudRates };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(combo);
            return dockPanel;
        }
    }

    public class FontFamilyPropertiesEditor : IPropertyEditor
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
                ItemsSource = Fonts.SystemFontFamilies
                    .Select(f => new KeyValuePair<FontFamily, string>(
                        f,
                        f.FamilyNames.TryGetValue(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.Name), out string fontName) ? fontName : f.Source
                    )).ToList()
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }

    public class FontWeightPropertiesEditor : IPropertyEditor
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
                ItemsSource = typeof(FontWeights).GetProperties()
                    .Select(p => new KeyValuePair<FontWeight, string>((FontWeight)p.GetValue(null), p.Name)).ToList()
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }

    public class FontStylePropertiesEditor : IPropertyEditor
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
                ItemsSource = typeof(FontStyles).GetProperties()
                    .Select(p => new KeyValuePair<FontStyle, string>((FontStyle)p.GetValue(null), p.Name)).ToList()
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }

    public class FontStretchPropertiesEditor : IPropertyEditor
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
                ItemsSource = typeof(FontStretches).GetProperties()
                    .Select(p => new KeyValuePair<FontStretch, string>((FontStretch)p.GetValue(null), p.Name)).ToList()
            };

            var binding = PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name);
            comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            DockPanel.SetDock(comboBox, Dock.Right);

            dockPanel.Children.Add(comboBox);
            dockPanel.Children.Add(textBlock);
            return dockPanel;
        }
    }
}
