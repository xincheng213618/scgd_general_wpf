using System.ComponentModel;
using Xunit;

namespace ProjectKB.Tests;

public sealed class EditRecipeWindowLifecycleTests
{
    [Fact]
    public void ReplaceRecipeConfigSubscriptionIsIdempotentAndDetachesPreviousConfig()
    {
        KBRecipeConfig first = new();
        KBRecipeConfig second = new();
        KBRecipeConfig? observed = null;
        int notifications = 0;
        PropertyChangedEventHandler handler = (_, _) => notifications++;

        EditRecipeWindow.ReplaceRecipeConfigSubscription(ref observed, first, handler);
        EditRecipeWindow.ReplaceRecipeConfigSubscription(ref observed, first, handler);
        first.MinKeyLv = 1;

        Assert.Equal(1, notifications);

        EditRecipeWindow.ReplaceRecipeConfigSubscription(ref observed, second, handler);
        first.MinKeyLv = 2;
        second.MinKeyLv = 1;

        Assert.Equal(2, notifications);

        EditRecipeWindow.ReplaceRecipeConfigSubscription(ref observed, null, handler);
        EditRecipeWindow.ReplaceRecipeConfigSubscription(ref observed, null, handler);
        second.MinKeyLv = 2;

        Assert.Equal(2, notifications);
        Assert.Null(observed);
    }
}
