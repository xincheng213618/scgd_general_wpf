using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Controls;
using ColorVision.UI.LogImp.Models;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class LogViewerControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedVirtualizedViewerKeepsVisibleEntriesWithoutAPlainTextMirror(bool latestAtTop)
    {
        WpfTestHost.Invoke(() =>
        {
            var viewer = new LogViewerControl { ViewerMode = LogViewerMode.Virtualized, MaxEntries = 2 };
            var old = new LogEntry("old warning", LogEntryLevel.Warning);
            var retained = new LogEntry("retained info", LogEntryLevel.Info);
            var incoming = new LogEntry("new warning", LogEntryLevel.Warning);
            viewer.SetEntries([old, retained], latestAtTop);

            // A mode change before Loaded still sees the same text.
            viewer.ViewerMode = LogViewerMode.TextBox;
            var textBox = Assert.IsType<TextBox>(viewer.FindName("PlainTextBox"));
            Assert.Contains(old.Text, textBox.Text);
            viewer.ViewerMode = LogViewerMode.Virtualized;
            viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            viewer.AppendEntries([incoming], latestAtTop, autoScroll: false);
            Assert.Equal(latestAtTop ? new[] { incoming, retained } : new[] { retained, incoming }, viewer.VisibleEntries);
            Assert.True(viewer.ApplySearchFilter("warning"));
            Assert.Same(incoming, Assert.Single(viewer.VisibleEntries));
            Assert.True(viewer.ApplySearchFilter(string.Empty));
            viewer.MaxEntries = 1;
            Assert.Same(incoming, Assert.Single(viewer.VisibleEntries));

            Assert.Empty(textBox.Text);
            var mirror = typeof(LogViewerControl).GetField("_plainText", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.Equal(string.Empty, mirror.GetValue(viewer));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RollingKeywordFilterKeepsMatchingRowsInOrderAcrossTrimming(bool latestAtTop)
    {
        WpfTestHost.Invoke(() =>
        {
            var viewer = new LogViewerControl { ViewerMode = LogViewerMode.Virtualized, MaxEntries = 4 };
            viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            ListBox list = Assert.IsType<ListBox>(viewer.FindName("EntriesListBox"));
            list.SetCurrentValue(ItemsControl.ItemsSourceProperty, viewer.VisibleEntries);
            var duplicate = new LogEntry("keep duplicate", LogEntryLevel.Info);
            var hidden = new LogEntry("hide this", LogEntryLevel.Warning);
            var retained = new LogEntry("keep selected", LogEntryLevel.Info);
            var incoming = new LogEntry("keep new", LogEntryLevel.Info);

            viewer.SetEntries([duplicate, hidden, duplicate, retained], latestAtTop);
            Assert.True(viewer.ApplySearchFilter("keep"));
            list.SelectedItem = retained;
            Assert.Same(retained, list.SelectedItem);
            viewer.AppendEntries([hidden, incoming], latestAtTop, autoScroll: false);
            Assert.Null(list.SelectedItem);
            AssertVisible([duplicate, retained, incoming]);

            var another = new LogEntry("keep another", LogEntryLevel.Info);
            viewer.AppendEntries([retained, incoming, hidden, duplicate, another], latestAtTop, autoScroll: false);
            AssertVisible([incoming, duplicate, another]);

            Assert.False(viewer.ApplySearchFilter("["));
            viewer.AppendEntries([hidden, retained], latestAtTop, autoScroll: false);
            AssertVisible([incoming, duplicate, another]);

            void AssertVisible(LogEntry[] chronological)
            {
                if (latestAtTop) Array.Reverse(chronological);
                Assert.Equal(chronological, viewer.VisibleEntries);
                Assert.Equal(chronological, list.Items.Cast<LogEntry>());
            }
        });
    }

    [Fact]
    public void RangeSelectionKeepsOrderAndPrimaryAfterExternalSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            var viewer = new LogViewerControl { ViewerMode = LogViewerMode.Virtualized };
            var first = new LogEntry("first", LogEntryLevel.Info);
            var repeated = new LogEntry("repeated", LogEntryLevel.Info);
            var fourth = new LogEntry("fourth", LogEntryLevel.Info);
            var last = new LogEntry("last", LogEntryLevel.Info);
            viewer.SetEntries([first, repeated, repeated, fourth, last], latestAtTop: false);
            viewer.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            ListBox list = Assert.IsType<ListBox>(viewer.FindName("EntriesListBox"));
            list.SetCurrentValue(ItemsControl.ItemsSourceProperty, viewer.VisibleEntries);
            var selectRange = typeof(LogViewerControl).GetMethod("SelectEntryRange", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Action<int, int>>(viewer);
            int changes = 0;
            list.SelectionChanged += (_, _) => changes++;

            selectRange(1, 2);
            list.SelectedItems.Clear();
            list.SelectedIndex = 2;
            Assert.Equal(2, list.SelectedIndex);
            selectRange(1, 3);
            AssertSelection(1, repeated, repeated, fourth);

            changes = 0;
            selectRange(1, 3);
            Assert.Equal(0, changes);
            selectRange(1, 4);
            AssertSelection(1, repeated, repeated, fourth, last);
            Assert.Equal(1, changes);
            selectRange(1, 2);
            AssertSelection(1, repeated, repeated);
            selectRange(1, 1);
            AssertSelection(1, repeated);

            list.SelectedItems.Add(last);
            selectRange(1, 0);
            AssertSelection(0, first, repeated);

            void AssertSelection(int index, params LogEntry[] entries)
            {
                Assert.Equal(index, list.SelectedIndex);
                Assert.Same(entries[0], list.SelectedItem);
                Assert.Equal(entries, list.SelectedItems.Cast<LogEntry>());
            }
        });
    }
}
