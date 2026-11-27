using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.Engine.Services.Devices.Camera;

internal sealed class CameraAcquisitionSettingsDraft
{
    internal static IReadOnlyList<string> EditableProperties { get; } =
    [
        nameof(DisplayCameraConfig.UseLocalCamera),
        nameof(DisplayCameraConfig.HikBayerQuality),
        nameof(DisplayCameraConfig.UseMotorReadbackForVid),
        nameof(DisplayCameraConfig.SaveLocalAutoFocusChart),
        nameof(DisplayCameraConfig.LocalAutoFocusChartDirectory)
    ];

    private static readonly PropertyInfo[] properties = EditableProperties
        .Select(name => typeof(DisplayCameraConfig).GetProperty(name)!).ToArray();
    private readonly object?[] initialValues;

    internal DisplayCameraConfig Settings { get; } = new();

    internal CameraAcquisitionSettingsDraft(DisplayCameraConfig source)
    {
        initialValues = properties.Select(property => property.GetValue(source)).ToArray();
        for (int i = 0; i < properties.Length; i++)
            properties[i].SetValue(Settings, initialValues[i]);
    }

    internal bool Apply(DisplayCameraConfig target, Action? persist = null)
    {
        var changes = new List<(PropertyInfo Property, object? PreviousValue)>();
        try
        {
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var value = property.GetValue(Settings);
                var currentValue = property.GetValue(target);
                if (Equals(value, initialValues[i]) || Equals(value, currentValue)) continue;
                changes.Add((property, currentValue));
                property.SetValue(target, value);
            }
            if (changes.Count == 0) return false;
            persist?.Invoke();
            return true;
        }
        catch
        {
            for (int i = changes.Count - 1; i >= 0; i--)
            {
                var (property, previousValue) = changes[i];
                property.SetValue(target, previousValue);
            }
            throw;
        }
    }
}
