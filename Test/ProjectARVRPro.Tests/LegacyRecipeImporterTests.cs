using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Recipe;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class LegacyRecipeImporterTests
{
    [Fact]
    public void ImportsKnownRecipeAcrossAssemblyVersionAndReportsUnknownType()
    {
        string knownTypeName = typeof(BlackRecipeConfig).FullName!;
        var json = CreateLegacyRecipeJson(
            (knownTypeName, new JObject
            {
                ["$type"] = $"{knownTypeName}, ProjectARVRPro",
                [nameof(BlackRecipeConfig.FOFOContrast)] = RecipeValue(123.45, 456.78, 1.2, -3.4)
            }),
            ("Legacy.RemovedRecipeConfig", new JObject
            {
                ["$type"] = "Legacy.RemovedRecipeConfig, ProjectARVRPro",
                ["Value"] = RecipeValue(1, 2)
            }));

        bool success = LegacyRecipeImporter.TryParse(json, out var result, out string errorMessage);

        Assert.True(success, errorMessage);
        Assert.Equal(2, result.SourceCount);
        var imported = Assert.IsType<BlackRecipeConfig>(result.SharedConfigs[typeof(BlackRecipeConfig)]);
        Assert.Equal(123.45, imported.FOFOContrast.Min);
        Assert.Equal(456.78, imported.FOFOContrast.Max);
        Assert.Equal(1.2, imported.FOFOContrast.Fix);
        Assert.Equal(-3.4, imported.FOFOContrast.B);
        Assert.Contains("Legacy.RemovedRecipeConfig", result.UnsupportedTypeNames);
    }

    [Fact]
    public void MapsLegacyRgbAndW25RecipesToKeyedLuminanceConfigs()
    {
        var blueRecipe = new JObject
        {
            ["$type"] = "ProjectARVRPro.Process.RGB.Blue.BlueRecipeConfig, ProjectARVRPro",
            ["LuminanceUniformity"] = RecipeValue(0.61, 0.92),
            ["CenterLunimance"] = RecipeValue(210, 310)
        };
        var whiteRecipe = new JObject
        {
            ["$type"] = "ProjectARVRPro.Process.W25.W25RecipeConfig, ProjectARVRPro",
            ["CenterLunimance"] = RecipeValue(110, 190)
        };
        var json = CreateLegacyRecipeJson(
            ("ProjectARVRPro.Process.RGB.Blue.BlueRecipeConfig", blueRecipe),
            ("ProjectARVRPro.Process.W25.W25RecipeConfig", whiteRecipe));

        bool success = LegacyRecipeImporter.TryParse(json, out var result, out string errorMessage);

        Assert.True(success, errorMessage);
        Assert.Equal(2, result.LuminanceConfigs.Count);
        Assert.Equal(210, result.LuminanceConfigs["Blue"].CenterLuminance.Min);
        Assert.Equal(310, result.LuminanceConfigs["Blue"].CenterLuminance.Max);
        Assert.Equal(0.61, result.LuminanceConfigs["Blue"].LuminanceUniformity.Min);
        Assert.Equal(110, result.LuminanceConfigs["White"].CenterLuminance.Min);
        Assert.Equal(190, result.LuminanceConfigs["White"].CenterLuminance.Max);
        Assert.Equal(0.75, result.LuminanceConfigs["White"].LuminanceUniformity.Min);
    }

    [Fact]
    public void RejectsJsonWithoutLegacyConfigsList()
    {
        bool success = LegacyRecipeImporter.TryParse("{\"Recipe\":{}}", out _, out string errorMessage);

        Assert.False(success);
        Assert.Contains("Configs", errorMessage);
    }

    private static string CreateLegacyRecipeJson(params (string TypeName, JObject Value)[] entries)
    {
        var configs = new JObject
        {
            ["$type"] = "System.Collections.Generic.Dictionary`2[[System.Type, System.Private.CoreLib],[ProjectARVRPro.IRecipeConfig, ProjectARVRPro]], System.Private.CoreLib"
        };

        foreach (var (typeName, value) in entries)
            configs[$"{typeName}, ProjectARVRPro, Version=1.1.7.43, Culture=neutral, PublicKeyToken=fab52dd8ce4bdf58"] = value;

        return new JObject
        {
            ["$type"] = "ProjectARVRPro.RecipeConfig, ProjectARVRPro",
            [nameof(RecipeConfig.Configs)] = configs
        }.ToString();
    }

    private static JObject RecipeValue(double min, double max, double fix = 1, double b = 0)
    {
        return new JObject
        {
            ["$type"] = "ProjectARVRPro.Recipe.RecipeBase, ProjectARVRPro",
            ["Min"] = min,
            ["Max"] = max,
            ["Fix"] = fix,
            ["B"] = b
        };
    }
}
