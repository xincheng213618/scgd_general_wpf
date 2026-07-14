using ColorVision.Copilot;
using ColorVision.Themes;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptCaretThemeTests
{
    [Fact]
    public void PromptCaret_UsesContrastingColorForLightAndDarkThemes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var prompt = new TextBox();
                var apply = typeof(CopilotChatPanel).GetMethod("ApplyPromptCaretBrush", BindingFlags.Static | BindingFlags.NonPublic);

                Assert.NotNull(apply);
                apply.Invoke(null, [prompt, Theme.Light]);
                Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(prompt.CaretBrush).Color);

                apply.Invoke(null, [prompt, Theme.Dark]);
                Assert.Equal(Colors.White, Assert.IsType<SolidColorBrush>(prompt.CaretBrush).Color);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
