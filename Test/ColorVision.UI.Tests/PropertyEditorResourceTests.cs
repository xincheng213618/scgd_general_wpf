using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class PropertyEditorResourceTests
{
    [Fact]
    public void NumericEditorUsesTheCurrentStyleAfterAnEarlyResourceLookupFails()
    {
        WpfTestHost.Invoke(() =>
        {
            Application app = Application.Current;
            ResourceDictionary previousResources = app.Resources;
            try
            {
                app.Resources = new ResourceDictionary();
                Assert.Throws<ResourceReferenceKeyNotFoundException>(() => PropertyEditorHelper.TextBoxSmallStyle);

                Style first = new(typeof(TextBox));
                app.Resources["TextBox.Small"] = first;
                var property = typeof(Settings).GetProperty(nameof(Settings.MaximumEntries))!;
                Settings settings = new();
                Assert.Same(first, FindTextBox().Style);

                Style replacement = new(typeof(TextBox));
                app.Resources["TextBox.Small"] = replacement;
                Assert.Same(replacement, FindTextBox().Style);

                TextBox FindTextBox() => Assert.Single(PropertyEditorHelper.GenProperties(property, settings).Children.OfType<TextBox>());
            }
            finally { app.Resources = previousResources; }
        });
    }

    private sealed class Settings
    {
        public int MaximumEntries { get; set; } = 1;
    }
}
