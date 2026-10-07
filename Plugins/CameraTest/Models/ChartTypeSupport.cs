using ColorVision.Core;
using System.Reflection;
using System.Text.Json;

namespace CameraTest.Models;

/// <summary>The plugin also runs on released hosts whose shared Core only supports BMW.</summary>
public static class ChartTypeSupport
{
    private static readonly PropertyInfo? ChartType = typeof(BmwSfrRoiSettings).GetProperty("ChartType");
    public static bool SupportsCheckerboard => ChartType?.PropertyType.IsEnum == true;
    public static int Selection(BmwSfrRoiSettings settings) => ChartType?.GetValue(settings)?.ToString() switch { "Checkerboard" => 1, "Auto" => 2, _ => 0 };

    public static BmwSfrRoiSettings Select(BmwSfrRoiSettings settings, int index)
    {
        if (index is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(index));
        if (index != 0 && !SupportsCheckerboard) throw new InvalidOperationException("当前宿主仅支持 BMW，请更新包含棋盘格算法的 ColorVision 宿主及原生组件。");
        var edited = settings with { };
        if (ChartType != null) ChartType.SetValue(edited, Enum.Parse(ChartType.PropertyType, index == 1 ? "Checkerboard" : index == 2 ? "Auto" : "Bmw"));
        return edited;
    }

    public static void ValidateProfile(string json)
    {
        if (SupportsCheckerboard) return;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("MeasurementRoi", out var roi) && roi.TryGetProperty("ChartType", out var type)
            && type.ToString() is not ("Bmw" or "0"))
            throw new InvalidOperationException("此配置需要棋盘格算法，请先更新 ColorVision 宿主及原生组件。");
    }
}
