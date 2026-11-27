using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class TimedButtonVisibilityTests
{
    [Fact]
    public void TimingUpdatesTheTooltipAndProgressWithoutAddingAContextMenuItem()
    {
        WpfTestHost.Invoke(() =>
        {
            const string label = "Restart";
            var menu = new ContextMenu();
            var existingItem = new MenuItem { Header = "Existing action" };
            menu.Items.Add(existingItem);
            var button = new Button { Content = label, ContextMenu = menu };
            var screen = new Grid();
            screen.Children.Add(button);
            using var operation = new TimedButtonOperation(button, new TimedButtonOperationOptions
            {
                OperationKey = $"timing-presentation-test:{Guid.NewGuid():N}",
                ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip(label, stats),
                TreatFirstSuccessAsWarmup = false,
                PersistStatsImmediately = false
            });

            var initialTooltip = Assert.IsType<string>(button.ToolTip);
            var progressHost = Assert.IsType<Grid>(button.Parent);
            using var scope = operation.Begin();
            Assert.Single(progressHost.Children.OfType<Border>());
            scope.Complete(true);

            Assert.Same(button, Assert.Single(progressHost.Children.Cast<UIElement>()));
            Assert.Equal(1, Assert.IsType<TimedButtonOperationStats>(operation.CurrentStats).SuccessCount);
            Assert.Equal(TimedButtonOperationTextFormatter.BuildTooltip(label, operation.CurrentStats), button.ToolTip);
            Assert.NotEqual(initialTooltip, button.ToolTip);
            Assert.Same(menu, button.ContextMenu);
            Assert.Same(existingItem, Assert.Single(menu.Items.Cast<object>()));
        });
    }

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
