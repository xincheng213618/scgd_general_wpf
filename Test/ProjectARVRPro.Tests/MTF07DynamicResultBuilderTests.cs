using ColorVision.Engine.Templates.Jsons.MTF2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Process.MTF.MTF07.MTFH;
using ProjectARVRPro.Process.MTF.MTF07.MTFV;
using ProjectARVRPro.Recipe;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class MTF07DynamicResultBuilderTests
{
    [Fact]
    public void ProcessConfigs_MapOnlyTheirConfiguredStripePoints()
    {
        var h = new MTFH07ProcessConfig();
        var v = new MTFV07ProcessConfig();

        Assert.True(h.TryGetItemName("Center_0F_H", out string hCenter));
        Assert.Equal(nameof(MTFH07TestResult.MTF_H_Center_0F), hCenter);
        Assert.True(v.TryGetItemName("LeftUp_0.7F_V", out string vLeftUp));
        Assert.Equal(nameof(MTFV07TestResult.MTF_V_LeftUp_0_7F), vLeftUp);
        Assert.False(h.TryGetItemName("Center_0F_V", out _));
        Assert.False(v.TryGetItemName("Center_0F_H", out _));
        Assert.False(h.TryGetItemName("Unconfigured_H", out _));
    }

    [Fact]
    public void PopulateItem_AppliesTheRecipeForThatPoint()
    {
        var item = new ObjectiveTestItem { Name = nameof(MTFH07TestResult.MTF_H_LeftUp_0_7F) };
        var source = new MTFItem { name = "LeftUp_0.7F_H", mtfValue = 0.3 };
        var recipe = new RecipeBase(0.5, 0.9, 2, 0.1);

        MTF07DynamicResultBuilder.PopulateItem(item, source, recipe, "F3", "%");

        Assert.Equal(0.7, item.Value, 10);
        Assert.Equal("0.700", item.TestValue);
        Assert.Equal(0.5, item.LowLimit);
        Assert.Equal(0.9, item.UpLimit);
        Assert.Equal("%", item.Unit);
    }

    [Fact]
    public void ProcessConfigs_HaveIndependentKeysAndPointRecipes()
    {
        var h = new MTFH07ProcessConfig { Key = " MTFH071 " };
        var v = new MTFV07ProcessConfig { Key = " MTFV071 " };
        h.RecipeConfig.MTF_H_Center_0F.Min = 0.6;

        Assert.Equal("MTFH071", h.GetOutputKey());
        Assert.Equal("MTFV071", v.GetOutputKey());
        Assert.Equal("MTFH07", new MTFH07ProcessConfig().GetOutputKey());
        Assert.Equal("MTFV07", new MTFV07ProcessConfig().GetOutputKey());
        Assert.Equal(0.6, h.RecipeConfig.MTF_H_Center_0F.Min);
        Assert.Equal(0.5, h.RecipeConfig.MTF_H_LeftUp_0_7F.Min);
        Assert.Equal(0.5, v.RecipeConfig.MTF_V_Center_0F.Min);
        Assert.NotSame(h.RecipeConfig.MTF_H_Center_0F, h.RecipeConfig.MTF_H_LeftUp_0_7F);
    }

    [Fact]
    public void PerPointRecipes_RoundTripWithoutUnifiedRecipe()
    {
        const string json = """
            {
              "RecipeConfig": {
                "MTF_H_Center_0F": { "Min": 0.65, "Max": 0.9, "Fix": 1, "B": 0 },
                "MTF_H_LeftUp_0_7F": { "Min": 0.7, "Max": 0.95, "Fix": 1, "B": 0 }
              }
            }
            """;

        var config = JsonConvert.DeserializeObject<MTFH07ProcessConfig>(json);
        string serialized = JsonConvert.SerializeObject(config);

        Assert.NotNull(config);
        Assert.Equal(0.65, config.RecipeConfig.MTF_H_Center_0F.Min);
        Assert.Equal(0.7, config.RecipeConfig.MTF_H_LeftUp_0_7F.Min);
        Assert.DoesNotContain("UnifiedRecipe", serialized);
    }

    [Fact]
    public void KeyedWriter_StoresStandardTypedTestResults()
    {
        var destination = new ObjectiveTestResult();
        var h = new MTFH07TestResult();
        var replacement = new MTFH07TestResult
        {
            MTF_H_Center_0F = new ObjectiveTestItem { Name = "MTF_H_Center_0F", Value = 0.8 }
        };
        var v = new MTFV07TestResult();

        KeyedTestResultWriter.Write(destination, "MTFH07", h);
        KeyedTestResultWriter.Write(destination, "mtfh07", replacement);
        KeyedTestResultWriter.Write(destination, "MTFV07", v);

        Assert.Single(destination.MTFH07TestResults);
        Assert.Same(replacement, destination.MTFH07TestResults["MTFH07"]);
        Assert.Same(v, destination.MTFV07TestResults["MTFV07"]);

        JObject json = JObject.Parse(JsonConvert.SerializeObject(destination));
        Assert.Equal(0.8, json["MTFH07TestResults"]?["MTFH07"]?["MTF_H_Center_0F"]?["Value"]?.Value<double>());
        Assert.Null(json["MTFH07TestResults"]?["MTFH07"]?["Items"]);
    }
}
