using AvalonDock;
using AvalonDock.Layout;
using Conoscope.Presentation.Docking;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Conoscope.Tests;

public sealed class ConoscopeDocumentLayoutTests
{
    [Fact]
    public void MovedDocumentRemainsDiscoverableAndNewDocumentsUseItsSelectedPane()
    {
        RunOnStaThread(() =>
        {
            DockingManager manager = new();
            ConoscopeDocumentLayout layout = new(manager);
            LayoutDocument first = new() { Content = new object(), Title = "First" };
            LayoutDocument moved = new() { Content = new object(), Title = "Moved" };
            layout.Add(first);
            layout.Add(moved);
            LayoutDocumentPane originalPane = Assert.IsType<LayoutDocumentPane>(moved.Parent);
            LayoutDocument other = new() { Content = new object(), Title = "Other pane" };
            LayoutDocumentPane splitPane = new(other);
            manager.Layout.RootPanel.Children.Add(splitPane);
            originalPane.Children.Remove(moved);
            splitPane.Children.Add(moved);

            layout.Select(other);
            layout.Select(moved);

            Assert.Same(moved, layout.Find(moved.Content));
            Assert.Same(moved, layout.ActiveDocument);
            Assert.Same(moved, splitPane.SelectedContent);
            Assert.True(moved.IsActive);
            Assert.Equal(3, layout.Documents.Count());

            LayoutDocument added = new() { Content = new object(), Title = "Added after split" };
            layout.Add(added);
            Assert.Same(splitPane, added.Parent);
            Assert.Same(first, Assert.Single(originalPane.Children));
        });
    }

    [Fact]
    public void FloatingDocumentParticipatesInLookupSelectionAndNewDocumentPlacement()
    {
        RunOnStaThread(() =>
        {
            DockingManager manager = new();
            ConoscopeDocumentLayout layout = new(manager);
            LayoutDocument docked = new() { Content = new object(), Title = "Docked" };
            LayoutDocument floating = new() { Content = new object(), Title = "Floating" };
            layout.Add(docked);
            layout.Add(floating);
            Assert.IsType<LayoutDocumentPane>(floating.Parent).Children.Remove(floating);
            LayoutDocumentPane floatingPane = new(floating);
            LayoutDocumentFloatingWindow floatingWindow = new() { RootPanel = new LayoutDocumentPaneGroup(floatingPane) };
            manager.Layout.FloatingWindows.Add(floatingWindow);

            layout.Select(docked);
            layout.Select(floating);

            Assert.Same(floating, layout.Find(floating.Content));
            Assert.Same(floating, layout.ActiveDocument);
            Assert.Same(floating, floatingPane.SelectedContent);
            Assert.Contains(docked, layout.Documents);
            Assert.Contains(floating, layout.Documents);

            LayoutDocument added = new() { Content = new object(), Title = "Added while floating" };
            layout.Add(added);
            Assert.Same(floatingPane, added.Parent);
            Assert.Same(floatingWindow, floatingPane.Parent.Parent);
        });
    }

    [Fact]
    public void ReparentingAndCanceledCloseKeepContentAliveUntilTheFloatingDocumentCloses()
    {
        RunOnStaThread(() =>
        {
            DockingManager manager = new();
            ConoscopeDocumentLayout layout = new(manager);
            CountingDisposable content = new();
            LayoutDocument document = new() { Content = content, Title = "Lifetime" };
            int closedNotifications = 0;
            layout.TrackLifetime(document, () => closedNotifications++);
            layout.Add(document);
            Assert.IsType<LayoutDocumentPane>(document.Parent).Children.Remove(document);
            LayoutDocumentPane floatingPane = new(document);
            manager.Layout.FloatingWindows.Add(new LayoutDocumentFloatingWindow { RootPanel = new LayoutDocumentPaneGroup(floatingPane) });
            Assert.Equal(0, content.DisposeCount);

            EventHandler<CancelEventArgs> cancelClose = (_, args) => args.Cancel = true;
            document.Closing += cancelClose;
            document.Close();
            Assert.Equal(0, content.DisposeCount);
            Assert.Equal(0, closedNotifications);
            Assert.Same(document, layout.Find(content));

            document.Closing -= cancelClose;
            document.Close();
            Assert.Equal(1, content.DisposeCount);
            Assert.Equal(1, closedNotifications);
            Assert.Null(layout.Find(content));
            Assert.Empty(layout.Documents);
        });
    }

    [Fact]
    public void EmptyLayoutRecoversADocumentPane()
    {
        RunOnStaThread(() =>
        {
            DockingManager manager = new();
            manager.Layout.RootPanel.Children.Clear();
            ConoscopeDocumentLayout layout = new(manager);
            LayoutDocument document = new() { Content = new object(), Title = "Recovered" };

            layout.Add(document);
            layout.Select(document);

            Assert.IsType<LayoutDocumentPane>(document.Parent);
            Assert.Same(manager.Layout, document.Root);
            Assert.Same(document, layout.ActiveDocument);
        });
    }

    private sealed class CountingDisposable : IDisposable
    {
        internal int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA document-layout test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
