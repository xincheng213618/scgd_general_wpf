using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Templates.Jsons.MTF2;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Process.MTF.VRPartition;

internal static class VRMtfPartitionCalculator
{
    public static VRMtfPartitionViewTestResult Calculate(IReadOnlyList<MTFItem>? source, VRMtfPartitionProcessConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Size <= 0 || !double.IsFinite(config.Center)
            || config.Center < 0 || config.Center > config.Size - 1)
            throw new ArgumentException("VR分区的网格边长或中心无效。");

        double[] radii = [config.RA, config.RB, config.RC, config.RD];
        if (radii.Any(radius => !double.IsFinite(radius) || radius < 0)
            || radii.Zip(radii.Skip(1), (inner, outer) => inner < outer).Any(increasing => !increasing))
            throw new ArgumentException("VR分区半径必须满足0≤RA<RB<RC<RD。");
        if (source == null || source.Count != (long)config.Size * config.Size)
            throw new ArgumentException($"VR分区需要{config.Size}×{config.Size}个点，实际为{source?.Count ?? 0}。");
        if (source.Any(point => point == null || string.IsNullOrWhiteSpace(point.name)
            || !point.mtfValue.HasValue || !double.IsFinite(point.mtfValue.Value)))
            throw new ArgumentException("VR分区包含缺少名称、测量值或非有限值的点。");
        if (source.Select(point => point.name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != source.Count)
            throw new ArgumentException("VR分区点名称重复，无法确定网格顺序。");

        MTFItem[] sorted = source.OrderBy(point => point.name, Comparer<string>.Create(Shlwapi.CompareLogical)).ToArray();
        var result = new VRMtfPartitionViewTestResult { PointCount = sorted.Length };
        List<double>[] regions = [new(), new(), new(), new()];
        for (int index = 0; index < sorted.Length; index++)
        {
            MTFItem point = sorted[index];
            double value = point.mtfValue!.Value;
            result.Items.Add(new ObjectiveTestItem
            {
                Name = point.name,
                Value = value,
                TestValue = value.ToString(config.ShowConfig),
                Unit = config.Unit
            });
            double row = index / config.Size - config.Center;
            double column = index % config.Size - config.Center;
            double distance = Math.Sqrt(row * row + column * column);
            int pointRegion = -1;
            for (int region = 0; region < radii.Length; region++)
            {
                if (distance > radii[region]) continue;
                regions[region].Add(value);
                pointRegion = region;
                break;
            }
            result.PointRegions.Add(pointRegion);
        }

        for (int region = 0; region < regions.Length; region++)
        {
            string regionName = ((char)('A' + region)).ToString();
            List<double> values = regions[region];
            if (values.Count == 0)
                throw new ArgumentException($"VR分区{regionName}没有测量点，请检查中心和半径。");
            result.RegionPointCounts[regionName] = values.Count;
            AddMetric(result, config, regionName, "Min", values.Min());
            AddMetric(result, config, regionName, "Max", values.Max());
            AddMetric(result, config, regionName, "Average", values.Average());
        }
        return result;
    }

    private static void AddMetric(VRMtfPartitionTestResult result, VRMtfPartitionProcessConfig config, string region, string statistic, double rawValue)
    {
        string name = $"Region_{region}_{statistic}";
        RecipeBase recipe = config.RecipeConfig.GetRecipe(name);
        double value = recipe.Apply(rawValue);
        if (!double.IsFinite(value))
            throw new ArgumentException($"VR分区{name}修正后不是有限数值。");
        result.Items.Add(new ObjectiveTestItem
        {
            Name = name,
            Value = value,
            TestValue = value.ToString(config.ShowConfig),
            Unit = config.Unit,
            LowLimit = recipe.Min,
            UpLimit = recipe.Max
        });
    }
}
