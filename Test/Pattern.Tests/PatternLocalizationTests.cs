using ColorVision.Common.Utilities;
using ColorVision.UI;
using ImageProjector;
using Pattern.Noise;
using Pattern.QuadrantGrating;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Pattern.Tests;

public sealed class PatternLocalizationTests
{
    private static readonly CultureInfo[] SupportedCultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en"),
        CultureInfo.GetCultureInfo("zh-Hant")
    ];

    [Fact]
    public void PatternResourceSetsHaveTheSameCompleteKeySet()
    {
        AssertMatchingResourceSets(Pattern.Properties.Resources.ResourceManager);
    }

    [Fact]
    public void ProjectionResourceSetsHaveTheSameCompleteKeySet()
    {
        AssertMatchingResourceSets(ImageProjector.Properties.Resources.ResourceManager);
    }

    [Fact]
    public void PatternTextUsesTheCurrentUiCulture()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            Assert.Equal("Chart Generation Tool", PatternText.ChartGenerationTool);
            Assert.Equal("Generate Pattern", PatternText.GeneratePattern);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-Hant");
            Assert.Equal("圖卡產生工具", PatternText.ChartGenerationTool);
            Assert.Equal("產生圖卡", PatternText.GeneratePattern);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void PropertyMetadataAndEnumsResolveThroughPatternResources()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        CultureInfo culture = CultureInfo.GetCultureInfo("en");
        try
        {
            CultureInfo.CurrentUICulture = culture;
            ResourceManager resources = Pattern.Properties.Resources.ResourceManager;
            PropertyInfo mainBrush = typeof(PatternQuadrantGratingConfig).GetProperty(nameof(PatternQuadrantGratingConfig.MainBrush))!;
            PropertyInfo imageItems = typeof(ImageProjectorConfig).GetProperty(nameof(ImageProjectorConfig.ImageItems))!;

            Assert.Equal("Line Color", PropertyEditorHelper.GetLocalizedString(resources,
                mainBrush.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()!.DisplayName));
            Assert.Equal("By Cell Size", resources.GetString(nameof(GratingLayoutMode.ByCellSize), culture));
            Assert.Equal("Image List", PropertyEditorHelper.GetDisplayName(
                PropertyEditorHelper.GetResourceManager(typeof(ImageProjectorConfig)), imageItems));

            Assert.Null(resources.GetString(nameof(NoiseType.Uniform), culture));
            Assert.Equal("Uniform Noise", PropertyEditorHelper.GetLocalizedString(resources, NoiseType.Uniform.ToDescription()));
            Assert.Equal("Fit", PropertyEditorHelper.GetLocalizedString(resources, ImageStretchMode.Uniform.ToDescription()));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void EveryPatternConfigPropertyAndEnumHasAResourceKey()
    {
        ResourceManager resources = Pattern.Properties.Resources.ResourceManager;
        Type[] configTypes = typeof(PatternWindow).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IConfig).IsAssignableFrom(type))
            .Where(type => type.Namespace?.StartsWith("Pattern", StringComparison.Ordinal) == true || type.Namespace == "ImageProjector")
            .ToArray();

        foreach (Type configType in configTypes)
        {
            AssertResourceKey(resources, configType.Name, configType.FullName!);
            foreach (PropertyInfo property in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(property => property.CanRead && property.CanWrite)
                         .Where(property => property.GetCustomAttribute<System.ComponentModel.BrowsableAttribute>()?.Browsable ?? true))
            {
                string displayKey = property.GetCustomAttribute<System.ComponentModel.DisplayNameAttribute>()?.DisplayName ?? property.Name;
                AssertResourceKey(resources, displayKey, $"{configType.FullName}.{property.Name}");

                if (property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() is { } description)
                    AssertResourceKey(resources, description.Description, $"{configType.FullName}.{property.Name} description");

                Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (!propertyType.IsEnum) continue;
                foreach (Enum value in Enum.GetValues(propertyType))
                {
                    string enumName = value.ToString();
                    string? descriptionKey = propertyType.GetField(enumName)?.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
                    bool hasEnumName = resources.GetString(enumName, CultureInfo.InvariantCulture) != null;
                    bool hasDescription = descriptionKey != null && resources.GetString(descriptionKey, CultureInfo.InvariantCulture) != null;
                    Assert.True(hasEnumName || hasDescription, $"Missing enum translation for {propertyType.FullName}.{enumName}.");
                }
            }
        }
    }

    private static void AssertMatchingResourceSets(ResourceManager resourceManager)
    {
        HashSet<string>? baseline = null;
        foreach (CultureInfo culture in SupportedCultures)
        {
            ResourceSet resourceSet = Assert.IsAssignableFrom<ResourceSet>(resourceManager.GetResourceSet(culture, true, false));
            HashSet<string> keys = resourceSet.Cast<DictionaryEntry>()
                .Select(entry => Assert.IsType<string>(entry.Key))
                .ToHashSet(StringComparer.Ordinal);

            Assert.All(resourceSet.Cast<DictionaryEntry>(), entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value as string)));
            if (baseline == null)
            {
                baseline = keys;
            }
            else
            {
                Assert.True(baseline.SetEquals(keys), $"Resource keys differ for culture '{culture.Name}'.");
            }
        }
    }

    private static void AssertResourceKey(ResourceManager resourceManager, string key, string owner)
    {
        Assert.True(resourceManager.GetString(key, CultureInfo.InvariantCulture) != null, $"Missing resource key '{key}' for {owner}.");
    }
}
