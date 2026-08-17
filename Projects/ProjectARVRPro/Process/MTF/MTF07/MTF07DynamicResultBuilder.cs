using ColorVision.Engine.Templates.Jsons.MTF2;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Process.MTF.MTF07
{
    internal static class MTF07DynamicResultBuilder
    {
        public static ObjectiveTestItem? CreateItem(string axis, MTFItem mtf, RecipeBase recipe, string showConfig, string unit)
        {
            ArgumentNullException.ThrowIfNull(mtf);
            ArgumentNullException.ThrowIfNull(recipe);
            if (string.IsNullOrWhiteSpace(mtf.name) || !MatchesAxis(axis, mtf.name))
                return null;

            double value = recipe.Apply(mtf.mtfValue ?? 0);
            return new ObjectiveTestItem
            {
                Name = BuildItemName(axis, mtf.name),
                Unit = unit,
                Value = value,
                TestValue = value.ToString(showConfig),
                LowLimit = recipe.Min,
                UpLimit = recipe.Max
            };
        }

        public static bool MatchesAxis(string axis, string? sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return false;

            string normalizedAxis = NormalizeAxis(axis);
            string oppositeAxis = normalizedAxis == "H" ? "V" : "H";
            bool matchesAxis = HasAxisMarker(sourceName, normalizedAxis);
            bool matchesOppositeAxis = HasAxisMarker(sourceName, oppositeAxis);
            return matchesAxis || !matchesOppositeAxis;
        }

        public static string BuildItemName(string axis, string sourceName)
        {
            string normalizedAxis = NormalizeAxis(axis);
            string name = sourceName.Trim();
            string marker = $"_MTF_{normalizedAxis}_";
            int markerIndex = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex > 0 && markerIndex + marker.Length < name.Length)
            {
                string frequency = name[..markerIndex];
                string position = name[(markerIndex + marker.Length)..];
                name = $"{position}_{frequency}";
            }

            foreach (string prefix in new[] { $"MTF07_{normalizedAxis}_", $"MTF_07_{normalizedAxis}_", $"MTF_{normalizedAxis}_", $"{normalizedAxis}_" })
            {
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                name = name[prefix.Length..];
                break;
            }

            string suffix = $"_{normalizedAxis}";
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                name = name[..^suffix.Length];

            name = name.Replace('.', '_').Trim('_');
            return $"MTF_{normalizedAxis}_{name}";
        }

        private static bool HasAxisMarker(string sourceName, string axis)
        {
            return sourceName.StartsWith($"{axis}_", StringComparison.OrdinalIgnoreCase) ||
                   sourceName.StartsWith($"MTF_{axis}_", StringComparison.OrdinalIgnoreCase) ||
                   sourceName.StartsWith($"MTF07_{axis}_", StringComparison.OrdinalIgnoreCase) ||
                   sourceName.StartsWith($"MTF_07_{axis}_", StringComparison.OrdinalIgnoreCase) ||
                   sourceName.Contains($"_MTF_{axis}_", StringComparison.OrdinalIgnoreCase) ||
                   sourceName.EndsWith($"_{axis}", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeAxis(string axis)
        {
            string normalizedAxis = axis?.Trim().ToUpperInvariant() ?? string.Empty;
            return normalizedAxis is "H" or "V"
                ? normalizedAxis
                : throw new ArgumentOutOfRangeException(nameof(axis), axis, "MTF07方向只能是H或V。");
        }
    }
}
