using ColorVision.UI;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace System.ComponentModel
{
    public sealed class CrashDumpTypePropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var resourceManager = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            dockPanel.Children.Add(PropertyEditorHelper.CreateLabel(property, resourceManager));

            var options = new[]
            {
                new CrashDumpTypeOption(ColorVision.UI.Desktop.Diagnostics.CrashDumpType.Mini, "小型转储（推荐）"),
                new CrashDumpTypeOption(ColorVision.UI.Desktop.Diagnostics.CrashDumpType.Full, "完整内存转储"),
                new CrashDumpTypeOption(ColorVision.UI.Desktop.Diagnostics.CrashDumpType.Custom, "自定义转储")
            };
            var comboBox = new ComboBox
            {
                Margin = new System.Windows.Thickness(5, 0, 0, 0),
                MinWidth = PropertyEditorHelper.ControlMinWidth,
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                ItemsSource = options,
                DisplayMemberPath = nameof(CrashDumpTypeOption.DisplayName),
                SelectedValuePath = nameof(CrashDumpTypeOption.Value)
            };
            comboBox.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            DockPanel.SetDock(comboBox, Dock.Right);
            dockPanel.Children.Add(comboBox);
            return dockPanel;
        }

        private sealed record CrashDumpTypeOption(
            ColorVision.UI.Desktop.Diagnostics.CrashDumpType Value,
            string DisplayName);
    }
}
