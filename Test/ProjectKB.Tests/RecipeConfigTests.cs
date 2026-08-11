using Xunit;

namespace ProjectKB.Tests;

public class RecipeConfigTests
{
    [Fact]
    public void CopyFromCopiesValuesAndPreservesTargetNotifications()
    {
        KBRecipeConfig source = new()
        {
            EnableKeyLvLimit = false,
            MinKeyLv = 1.1,
            MaxKeyLv = 2.2,
            EnableAvgLvLimit = false,
            MaxAvgLv = 3.3,
            MinAvgLv = 1.2,
            EnableUniformityLimit = false,
            MinUniformity = 85,
            EnableKeyLcLimit = false,
            MinKeyLc = 12,
            MaxKeyLc = 115,
            KeyLcNeighborhoodRadiusMm = 45,
            KeyLcPixelsPerMillimeter = 12,
            EnableBacklightAutotune = true,
            BacklightAutotuneSteepness = 7,
            BacklightAutotuneAvgLvQ1 = 1,
            BacklightAutotuneAvgLvQ3 = 2,
            BacklightAutotuneMinLvQ1 = 3,
            BacklightAutotuneMinLvQ3 = 4,
            BacklightAutotuneUniformityQ1 = 5,
            BacklightAutotuneUniformityQ3 = 6
        };
        KBRecipeConfig target = new();
        List<string?> changedProperties = [];
        target.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        target.CopyFrom(source);

        foreach (var property in typeof(KBRecipeConfig).GetProperties().Where(property => property.CanRead && property.CanWrite))
        {
            Assert.Equal(property.GetValue(source), property.GetValue(target));
        }
        Assert.Contains(nameof(KBRecipeConfig.MinKeyLv), changedProperties);

        int notificationCount = changedProperties.Count;
        target.MinKeyLv = 9.9;
        Assert.True(changedProperties.Count > notificationCount);
    }
}
