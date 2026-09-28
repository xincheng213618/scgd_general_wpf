using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor;

public sealed class LocalMtfConfigurationEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        DockPanel panel = new();
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        Button button = new() { Content = "配置条纹 MTF…", Padding = new Thickness(10, 2, 10, 2) };
        panel.Children.Add(button);
        button.Click += (_, _) =>
        {
            try
            {
                StripeMtfParameters parameters = StripeMtfParameters.FromJson(property.GetValue(obj) as string ?? "{}");
                PropertyEditorWindow window = new(parameters, PropertyEditorEditMode.Transactional)
                { Title = "条纹 MTF 参数", Owner = Window.GetWindow(panel), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Submitted += (_, _) =>
                {
                    try { property.SetValue(obj, parameters.ToJson().ToString()); }
                    catch (Exception error) { MessageBox.Show(error.Message, "MTF 参数无效"); }
                };
                window.ShowDialog();
            }
            catch (Exception error) { MessageBox.Show(error.Message, "MTF 参数无效"); }
        };
        return panel;
    }
}
