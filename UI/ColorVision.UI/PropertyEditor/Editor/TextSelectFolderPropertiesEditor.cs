using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel
{
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

            var openFolderBtn = new Button { Content = "🗁", Margin = new Thickness(5, 0, 0, 0), ToolTip = ColorVision.UI.Properties.Resources.OpenFolder };
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
}
