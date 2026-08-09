using ColorVision.Engine.Templates.Jsons.KB;
using Newtonsoft.Json;
using Xunit;

namespace ProjectKB.Tests;

public class LocalContrastTests
{
    [Fact]
    public void CalculateExcludesCurrentKeyFromLocalAverage()
    {
        KBItem target = CreateItem("Target", 100, 100, 100, 100, 120);
        KBItem left = CreateItem("Left", 0, 100, 100, 100, 100);
        KBItem right = CreateItem("Right", 200, 100, 100, 100, 100);

        ProjectKBWindow.CalCulLc(new[] { target, left, right }, 150);

        Assert.Equal(0.2, target.Lc, 10);
    }

    [Fact]
    public void CalculatePreservesSignedContrast()
    {
        KBItem target = CreateItem("Target", 100, 100, 100, 100, 80);
        KBItem left = CreateItem("Left", 0, 100, 100, 100, 100);
        KBItem right = CreateItem("Right", 200, 100, 100, 100, 100);

        ProjectKBWindow.CalCulLc(new[] { target, left, right }, 150);

        Assert.Equal(-0.2, target.Lc, 10);
    }

    [Fact]
    public void GetNeighborsUsesInclusiveCenterDistance()
    {
        KBItem target = CreateItem("Target", 0, 0, 100, 100, 100);
        KBItem onBoundary = CreateItem("Boundary", 345, 45, 10, 10, 100);
        KBItem outside = CreateItem("Outside", 346, 45, 10, 10, 100);
        KBItem[] items = [target, onBoundary, outside];

        IReadOnlyList<KBItem> neighbors = ProjectKBWindow.GetLcNeighbors(items, target, 300);

        Assert.Contains(onBoundary, neighbors);
        Assert.DoesNotContain(outside, neighbors);
        Assert.DoesNotContain(target, neighbors);
    }

    [Fact]
    public void GetNeighborsIgnoresRectangleOverlapWhenCenterIsOutside()
    {
        KBItem target = CreateItem("Target", 0, 0, 100, 100, 100);
        KBItem partialOverlap = CreateItem("J", 340, 0, 100, 100, 100);

        IReadOnlyList<KBItem> neighbors = ProjectKBWindow.GetLcNeighbors(new[] { target, partialOverlap }, target, 300);

        Assert.DoesNotContain(partialOverlap, neighbors);
    }

    [Fact]
    public void RadiusConversionUsesPhysicalCalibration()
    {
        Assert.Equal(300, ProjectKBWindow.GetLcNeighborhoodRadiusPixels(30, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectKBWindow.GetLcNeighborhoodRadiusPixels(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectKBWindow.GetLcNeighborhoodRadiusPixels(30, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectKBWindow.GetLcNeighborhoodRadiusPixels(double.MaxValue, 2));
    }

    [Fact]
    public void CalculateLeavesZeroWhenThereAreNoNeighbors()
    {
        KBItem target = CreateItem("Target", 0, 0, 100, 100, 100);

        ProjectKBWindow.CalCulLc(new[] { target }, 1);

        Assert.Equal(0, target.Lc);
    }

    [Fact]
    public void RecipeDefaultsMissingPhysicalCalibrationAndRejectsInvalidValues()
    {
        KBRecipeConfig recipe = JsonConvert.DeserializeObject<KBRecipeConfig>("{}")!;

        Assert.Equal(30, recipe.KeyLcNeighborhoodRadiusMm);
        Assert.Equal(10, recipe.KeyLcPixelsPerMillimeter);

        recipe.KeyLcNeighborhoodRadiusMm = -20;
        recipe.KeyLcPixelsPerMillimeter = 0;

        Assert.Equal(30, recipe.KeyLcNeighborhoodRadiusMm);
        Assert.Equal(10, recipe.KeyLcPixelsPerMillimeter);
    }

    private static KBItem CreateItem(string name, int x, int y, int width, int height, double lv)
    {
        return new KBItem
        {
            Name = name,
            Lv = lv,
            KBKeyRect = new KBKeyRect
            {
                Name = name,
                X = x,
                Y = y,
                Width = width,
                Height = height
            }
        };
    }
}
