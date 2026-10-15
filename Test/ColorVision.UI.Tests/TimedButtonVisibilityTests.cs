using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class TimedButtonVisibilityTests
{
    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void OperationAndCompletionRespectVisibilityChangedByTheScreen(Visibility visibility)
    {
        WpfTestHost.Invoke(() =>
        {
            Grid screen = new();
            Button button = new() { Content = "打开" };
            screen.Children.Add(button);
            using var operation = new TimedButtonOperation(button, new TimedButtonOperationOptions { OperationKey = "visibility-boundary-test" });
            using var scope = operation.Begin();
            Assert.Equal(Visibility.Visible, button.Visibility);
            button.Visibility = visibility;
            Assert.Equal(visibility, ((FrameworkElement)button.Parent).Visibility);
            scope.Complete(false);
            Assert.Equal(visibility, button.Visibility);
            Assert.Equal(visibility, ((FrameworkElement)button.Parent).Visibility);
            button.Visibility = Visibility.Visible;
            Assert.Equal(Visibility.Visible, ((FrameworkElement)button.Parent).Visibility);
        });
    }
}
