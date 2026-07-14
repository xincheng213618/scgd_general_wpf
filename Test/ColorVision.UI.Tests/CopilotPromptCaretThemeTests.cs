using ColorVision.Copilot;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptCaretThemeTests
{
    [Fact]
    public void PromptCaret_FollowsGlobalTextBrushWhenThemeResourcesChange()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var prompt = new TextBox();
                prompt.Resources["GlobalTextBrush"] = Brushes.Black;
                var bind = typeof(CopilotChatPanel).GetMethod("BindPromptCaretToThemeResource", BindingFlags.Static | BindingFlags.NonPublic);

                Assert.NotNull(bind);
                bind.Invoke(null, [prompt]);
                Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(prompt.CaretBrush).Color);

                prompt.Resources["GlobalTextBrush"] = Brushes.White;
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
