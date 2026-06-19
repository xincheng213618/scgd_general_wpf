using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel
{
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
                var initialDirectory = PathSelectionHelper.GetExistingDirectory(path);
                if (!string.IsNullOrWhiteSpace(initialDirectory))
                {
                    ofd.InitialDirectory = initialDirectory;
                }

                var fileName = Directory.Exists(path) ? null : PathSelectionHelper.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    ofd.FileName = fileName;
                }

                if (ofd.ShowDialog() == true)
                {
                    property.SetValue(obj, ofd.FileName);
                }
            };

            var openFolderBtn = new Button { Content = "🗁", Margin = new Thickness(5, 0, 0, 0), ToolTip = ColorVision.UI.Properties.Resources.OpenContainingFolder };
            openFolderBtn.Click += (_, __) =>
            {
                var path = property.GetValue(obj) as string;
                if (string.IsNullOrWhiteSpace(path)) return;

                if (File.Exists(path))
                {
                    PlatformHelper.OpenFolderAndSelectFile(path);
                    return;
                }

                var folder = PathSelectionHelper.GetExistingDirectory(path);
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    PlatformHelper.OpenFolder(folder);
                }
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
