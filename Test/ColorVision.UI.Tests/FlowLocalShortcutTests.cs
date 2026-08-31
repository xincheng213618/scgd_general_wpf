using ColorVision.Engine.FlowProcessing;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public sealed class FlowLocalShortcutTests
{
    [Fact]
    public void AutoAlignmentOnlyAcceptsExactControlL()
    {
        // Only evaluate the key predicate; never construct ViewFlow or arrange nodes.
        for (int mask = 0; mask < 16; mask++)
        {
            var modifiers = (ModifierKeys)mask;
            Assert.Equal(modifiers == ModifierKeys.Control, ViewFlow.IsAutoAlignmentShortcut(Key.L, modifiers));
        }
    }

    [Theory]
    [InlineData(Key.O)]
    [InlineData(Key.R)]
    [InlineData(Key.None)]
    [InlineData(Key.System)]
    [InlineData(Key.ImeProcessed)]
    public void OtherKeysNeverTriggerAutoAlignment(Key key)
    {
        Assert.False(ViewFlow.IsAutoAlignmentShortcut(key, ModifierKeys.Control));
    }
}
