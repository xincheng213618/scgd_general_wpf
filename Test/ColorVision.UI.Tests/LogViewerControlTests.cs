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
}
