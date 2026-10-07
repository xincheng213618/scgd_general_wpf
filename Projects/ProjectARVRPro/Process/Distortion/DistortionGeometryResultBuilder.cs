using ColorVision.Engine.Templates.Jsons.Distortion2;
using ProjectARVRPro.Recipe;
using System.IO;

namespace ProjectARVRPro.Process.Distortion;

internal static class DistortionGeometryResultBuilder
{
    internal static void Apply(DistortionReslut source, DistortionRecipeConfig recipe, DistortionTestResult target, string showConfig)
    {
        DistortionGeometry? geometry = source.Analysis?.Geometry;
        if (geometry?.MaximumTiltDegrees > 90)
            throw new InvalidDataException("最大倾斜角必须在0～90°之间。");
        target.MaximumTiltDegrees = Build(nameof(DistortionTestResult.MaximumTiltDegrees), geometry?.MaximumTiltDegrees, recipe.MaximumTiltDegrees, showConfig, "°");
        target.MaximumEdgeLengthDifferencePercent = Build(nameof(DistortionTestResult.MaximumEdgeLengthDifferencePercent), geometry?.MaximumEdgeLengthDifferencePercent, recipe.MaximumEdgeLengthDifferencePercent, showConfig, "%");
    }

    private static ObjectiveTestItem? Build(string name, double? rawValue, RecipeBase recipe, string showConfig, string unit)
    {
        if (!rawValue.HasValue) return null;
        if (!double.IsFinite(rawValue.Value) || rawValue.Value < 0)
            throw new InvalidDataException($"{name}必须是非负有限测量值。");
        double value = recipe.Apply(rawValue.Value);
        if (!double.IsFinite(value) || !double.IsFinite(recipe.Min) || !double.IsFinite(recipe.Max)
            || (recipe.Min != 0 && recipe.Max != 0 && recipe.Min > recipe.Max))
            throw new InvalidDataException($"{name}的修正值或配方限值无效。");
        return new ObjectiveTestItem
        {
            Name = name, Value = value, LowLimit = recipe.Min, UpLimit = recipe.Max,
            TestValue = value.ToString(showConfig) + unit, Unit = unit
        };
    }
}
