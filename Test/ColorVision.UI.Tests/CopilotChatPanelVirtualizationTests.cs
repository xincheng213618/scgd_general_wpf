using ColorVision.Copilot;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class CopilotChatPanelVirtualizationTests
{
    [Fact]
    public void MessageList_RealizesOnlyVisibleItems()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var messages = new ObservableCollection<CopilotChatMessage>(
                    Enumerable.Range(1, 200)
                        .Select(index => new CopilotChatMessage(CopilotChatRole.User, $"Message {index}")));
                var panel = new CopilotChatPanel
                {
                    DataContext = new MessageListContext(messages),
                };

                panel.Measure(new Size(900, 640));
                panel.Arrange(new Rect(0, 0, 900, 640));
                panel.UpdateLayout();

                var messageList = Assert.IsType<ListBox>(panel.FindName("MessagesListBox"));
                Assert.True(VirtualizingPanel.GetIsVirtualizing(messageList));
                Assert.Equal(VirtualizationMode.Recycling, VirtualizingPanel.GetVirtualizationMode(messageList));
                Assert.Equal(ScrollUnit.Pixel, VirtualizingPanel.GetScrollUnit(messageList));
                Assert.NotNull(FindVisualChild<VirtualizingStackPanel>(messageList));

                var realizedItemCount = Enumerable.Range(0, messages.Count)
                    .Count(index => messageList.ItemContainerGenerator.ContainerFromIndex(index) != null);

                Assert.InRange(realizedItemCount, 1, 40);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;

            var nestedMatch = FindVisualChild<T>(child);
            if (nestedMatch != null)
                return nestedMatch;
        }

        return null;
    }

    private sealed class MessageListContext(ObservableCollection<CopilotChatMessage> messages)
    {
        public ObservableCollection<CopilotChatMessage> Messages { get; } = messages;
    }
}
