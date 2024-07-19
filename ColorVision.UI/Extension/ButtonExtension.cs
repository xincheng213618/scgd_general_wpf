using System.Windows.Controls;

namespace ColorVision.UI.Extension
{
    public static class ButtonExtension
    {
        public static async Task ChangeButtonContentAsync(this Button button, string newContent, Action action, int delayMilliseconds = 1000)
        {
            if (button.Content.ToString() != newContent)
            {
                action.Invoke();
                var originalContent = button.Content;
                button.Content = newContent;
                await Task.Delay(delayMilliseconds);
                button.Content = originalContent;
            }
        }
    }
}
