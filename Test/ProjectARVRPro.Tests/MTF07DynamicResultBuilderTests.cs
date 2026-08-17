using ColorVision.Engine.Templates.Jsons.MTF2;
using Newtonsoft.Json;
using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Process.MTF.MTFH;
using ProjectARVRPro.Process.MTF.MTFV;
using ProjectARVRPro.Recipe;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class MTF07DynamicResultBuilderTests
{
    [Theory]
    [InlineData("H", "Center_0F_H", "MTF_H_Center_0F")]
    [InlineData("V", "LeftUp_0.7F_V", "MTF_V_LeftUp_0_7F")]
    [InlineData("H", "MTF_H_Custom.12", "MTF_H_Custom_12")]
    [InlineData("V", "0.7F_MTF_V_RightDown", "MTF_V_RightDown_0_7F")]
    public void BuildItemName_NormalizesDynamicPointNames(string axis, string sourceName, string expected)
    {
        Assert.Equal(expected, MTF07DynamicResultBuilder.BuildItemName(axis, sourceName));
    }

    [Fact]
    public void CreateItem_KeepsHAndVResultsSeparated()
    {
        var recipe = new RecipeBase(0.5, 0, 2, 0.1);
        var hSource = new MTFItem { name = "Point1_H", mtfValue = 0.3 };
        var vSource = new MTFItem { name = "Point1_V", mtfValue = 0.4 };

        ObjectiveTestItem? hItem = MTF07DynamicResultBuilder.CreateItem("H", hSource, recipe, "F3", "%");

        Assert.NotNull(hItem);
        Assert.Equal("MTF_H_Point1", hItem.Name);
        Assert.Equal(0.7, hItem.Value, 10);
        Assert.Null(MTF07DynamicResultBuilder.CreateItem("H", vSource, recipe, "F3", "%"));
        Assert.Null(MTF07DynamicResultBuilder.CreateItem("V", hSource, recipe, "F3", "%"));
    }

    [Fact]
    public void ProcessConfigs_HaveIndependentKeysAndRecipes()
    {
        var h = new MTFHProcessConfig { Key = " MTFH1 " };
        var v = new MTFVProcessConfig { Key = " MTFV1 " };
        h.RecipeConfig.UnifiedRecipe.Min = 0.6;

        Assert.Equal("MTFH1", h.GetOutputKey());
        Assert.Equal("MTFV1", v.GetOutputKey());
        Assert.Equal(0.6, h.RecipeConfig.UnifiedRecipe.Min);
        Assert.Equal(0.5, v.RecipeConfig.UnifiedRecipe.Min);
    }

    [Fact]
    public void LegacyCenterRecipe_MigratesToUnifiedRecipe()
    {
        const string json = "{\"RecipeConfig\":{\"MTF_H_Center_0F\":{\"Min\":0.65,\"Max\":0.9,\"Fix\":1,\"B\":0}}}";

        var config = JsonConvert.DeserializeObject<MTFHProcessConfig>(json);

        Assert.NotNull(config);
        Assert.Equal(0.65, config.RecipeConfig.UnifiedRecipe.Min);
        Assert.Equal(0.9, config.RecipeConfig.UnifiedRecipe.Max);
        Assert.DoesNotContain("MTF_H_Center_0F", JsonConvert.SerializeObject(config));
    }
}
