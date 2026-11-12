using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro.Process;

internal sealed class VidCameraPropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var cameras = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToArray();
        var first = cameras.FirstOrDefault();
        string defaultLabel = first == null || string.IsNullOrWhiteSpace(first.Name)
            ? "默认（第一台相机）" : $"默认（第一台：{first.Name}）";
        var options = new List<KeyValuePair<string, string>> { new(string.Empty, defaultLabel) };
        options.AddRange(cameras.Where(camera => !string.IsNullOrWhiteSpace(camera.Code))
            .Select(camera => new KeyValuePair<string, string>(camera.Code,
                string.IsNullOrWhiteSpace(camera.Name) || camera.Name == camera.Code ? camera.Code : $"{camera.Name} ({camera.Code})")));
        var picker = new ComboBox
        {
            Style = PropertyEditorHelper.ComboBoxSmallStyle,
            Margin = new Thickness(5, 0, 0, 0),
            ItemsSource = options,
            DisplayMemberPath = nameof(KeyValuePair<string, string>.Value),
            SelectedValuePath = nameof(KeyValuePair<string, string>.Key)
        };
        var row = new DockPanel();
        row.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));
        row.Children.Add(picker);
        return row;
    }
}
