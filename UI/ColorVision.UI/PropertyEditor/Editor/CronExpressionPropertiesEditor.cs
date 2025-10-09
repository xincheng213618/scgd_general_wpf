using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace System.ComponentModel
{
    public class CronExpressionPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();
            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var cronBtn = new Button { Content = ColorVision.UI.Properties.Resources.OnlineCronGenerator, Margin = new Thickness(5, 0, 0, 0), ToolTip = ColorVision.UI.Properties.Resources.OnlineCronGenerator };
            cronBtn.Click += (_, __) => PlatformHelper.Open("https://cron.qqe2.com/");
            DockPanel.SetDock(cronBtn, Dock.Right);

            var cronTextBox = PropertyEditorHelper.CreateSmallTextBox(PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            dockPanel.Children.Add(cronBtn);
            dockPanel.Children.Add(cronTextBox);
            return dockPanel;
        }
    }
}
