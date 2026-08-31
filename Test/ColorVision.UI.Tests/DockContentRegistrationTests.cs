using AvalonDock;
using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class DockContentRegistrationTests
{
    [Fact]
    public void Factory_IsDeferredAndInvokedOnlyOnce()
    {
        int invocationCount = 0;
        object content = new();
        DockContentRegistration registration = DockContentRegistration.FromFactory("panel", () =>
        {
            invocationCount++;
            return content;
        });

        Assert.False(registration.IsValueCreated);
        Assert.Equal(0, invocationCount);

        object first = registration.GetOrCreate();
        object second = registration.GetOrCreate();

        Assert.Same(content, first);
        Assert.Same(first, second);
        Assert.True(registration.IsValueCreated);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void Factory_NullResultIsRejectedWhenContentIsRequested()
    {
        DockContentRegistration registration = DockContentRegistration.FromFactory("panel", () => null!);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => registration.GetOrCreate());

        Assert.Contains("panel", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_LayoutContentRemainsDeferredUntilMaterialized()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                int invocationCount = 0;
                object content = new Border();
                DockContentRegistration registration = DockContentRegistration.FromFactory("panel", () =>
                {
                    invocationCount++;
                    return content;
                });

                var deferredContent = Assert.IsType<DeferredDockContent>(
                    registration.GetForLayout(_ => { }, ex => ExceptionDispatchInfo.Capture(ex).Throw()));
                Assert.Same(deferredContent, registration.GetForLayout(_ => { }, _ => { }));
                Assert.Equal(0, invocationCount);

                object? first = deferredContent.Materialize();
                object? second = deferredContent.Materialize();

                Assert.Same(content, first);
                Assert.Same(first, second);
                Assert.Same(deferredContent, registration.GetForLayout(_ => { }, _ => { }));
                Assert.Same(deferredContent, LogicalTreeHelper.GetParent((Border)content));
                Assert.Equal(1, invocationCount);
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

    [Fact]
    public void EagerContent_IsRestoredWithoutADeferredHost()
    {
        object content = new();
        DockContentRegistration registration = DockContentRegistration.FromContent(content);

        object layoutContent = registration.GetForLayout(_ => { }, _ => { });

        Assert.Same(content, layoutContent);
    }

    [Fact]
    public void ShowPanel_MaterializesDeferredContentOnlyOnFirstShow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dockingManager = new DockingManager
                {
                    Layout = new LayoutRoot
                    {
                        RootPanel = new LayoutPanel()
                    }
                };
                var layoutManager = new DockLayoutManager(dockingManager);
                int invocationCount = 0;
                var content = new Border();
                layoutManager.RegisterPanel(
                    "lazy-panel",
                    () =>
                    {
                        invocationCount++;
                        return content;
                    },
                    "Lazy panel",
                    PanelPosition.Right,
                    isDefaultVisible: false);

                Assert.Equal(0, invocationCount);

                layoutManager.ShowPanel("lazy-panel");
                layoutManager.ShowPanel("lazy-panel");

                Assert.Equal(1, invocationCount);
                Assert.True(layoutManager.IsPanelVisible("lazy-panel"));
                LayoutAnchorable anchorable = Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>());
                var deferredContent = Assert.IsType<DeferredDockContent>(anchorable.Content);
                Assert.Same(content, deferredContent.Content);
                Assert.Same(deferredContent, LogicalTreeHelper.GetParent(content));
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

    [Fact]
    public void ShowPanel_MaterializesContentAlreadyRestoredAsDeferred()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                int invocationCount = 0;
                var content = new Border();
                var deferredContent = new DeferredDockContent(
                    () =>
                    {
                        invocationCount++;
                        return content;
                    },
                    _ => { },
                    ex => ExceptionDispatchInfo.Capture(ex).Throw());
                var anchorable = new LayoutAnchorable
                {
                    ContentId = "lazy-panel",
                    Content = deferredContent,
                };
                var pane = new LayoutAnchorablePane();
                pane.Children.Add(anchorable);
                var paneGroup = new LayoutAnchorablePaneGroup();
                paneGroup.Children.Add(pane);
                var rootPanel = new LayoutPanel();
                rootPanel.Children.Add(paneGroup);
                var dockingManager = new DockingManager
                {
                    Layout = new LayoutRoot { RootPanel = rootPanel }
                };
                var layoutManager = new DockLayoutManager(dockingManager);

                layoutManager.ShowPanel("lazy-panel");
                layoutManager.ShowPanel("lazy-panel");

                Assert.Equal(1, invocationCount);
                Assert.Same(content, deferredContent.Content);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ClosedFactoryPanel_ReopensWithItsOriginalHost(bool drainBeforeReopen, bool useToggle)
    {
        RunOnSta(() =>
        {
            var dockingManager = CreateDockingManager();
            var layoutManager = new DockLayoutManager(dockingManager);
            var content = new Border();
            int invocationCount = 0;
            layoutManager.RegisterPanel("lazy-panel", () => { invocationCount++; return content; }, "Lazy panel", PanelPosition.Right);
            var host = GetRegisteredLayoutContent(layoutManager);
            LayoutAnchorable original = ReplaceLayout(dockingManager, host);
            Assert.Same(content, host.Materialize());
            Assert.Same(host, LogicalTreeHelper.GetParent(content));

            original.Close();
            Assert.Null(original.Root);
            Assert.False(layoutManager.IsPanelVisible("lazy-panel"));
            if (drainBeforeReopen)
                DrainDispatcher();

            if (useToggle)
                layoutManager.TogglePanel("lazy-panel");
            else
                layoutManager.ShowPanel("lazy-panel");

            LayoutAnchorable reopened = Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>());
            Assert.NotSame(original, reopened);
            Assert.Same(host, reopened.Content);
            Assert.True(layoutManager.IsPanelVisible("lazy-panel"));
            Assert.Same(content, host.Content);
            Assert.Same(host, LogicalTreeHelper.GetParent(content));
            Assert.Same(dockingManager, LogicalTreeHelper.GetParent(host));
            Assert.Equal(1, invocationCount);

            // AvalonDock removes obsolete LayoutItems through a queued callback.
            // That cleanup must not detach the reopened host or its payload.
            DrainDispatcher();
            Assert.NotNull(dockingManager.GetLayoutItemFromModel(reopened));
            Assert.Same(host, reopened.Content);
            Assert.Same(host, LogicalTreeHelper.GetParent(content));
            Assert.Same(dockingManager, LogicalTreeHelper.GetParent(host));
            Assert.Equal(1, invocationCount);
        });
    }

    [Fact]
    public void ClosedUnmaterializedFactoryPanel_MaterializesSynchronouslyWhenReopened()
    {
        RunOnSta(() =>
        {
            var dockingManager = CreateDockingManager();
            var layoutManager = new DockLayoutManager(dockingManager);
            var content = new Border();
            int invocationCount = 0;
            layoutManager.RegisterPanel("lazy-panel", () => { invocationCount++; return content; }, "Lazy panel", PanelPosition.Right);
            var host = GetRegisteredLayoutContent(layoutManager);
            LayoutAnchorable original = ReplaceLayout(dockingManager, host);
            Assert.Equal(0, invocationCount);
            original.Close();

            layoutManager.ShowPanel("lazy-panel");

            LayoutAnchorable reopened = Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>());
            Assert.Same(host, reopened.Content);
            Assert.Same(content, host.Content);
            Assert.Same(host, LogicalTreeHelper.GetParent(content));
            Assert.Equal(1, invocationCount);
            DrainDispatcher();
            Assert.Equal(1, invocationCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenFactoryPanel_ReusesItsAnchorableAndHost(bool useToggle)
    {
        RunOnSta(() =>
        {
            var dockingManager = CreateDockingManager();
            var layoutManager = new DockLayoutManager(dockingManager);
            var content = new Border();
            int invocationCount = 0;
            layoutManager.RegisterPanel("lazy-panel", () => { invocationCount++; return content; }, "Lazy panel", PanelPosition.Right);
            layoutManager.ShowPanel("lazy-panel");
            LayoutAnchorable original = Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>());
            var host = Assert.IsType<DeferredDockContent>(original.Content);

            layoutManager.TogglePanel("lazy-panel");
            Assert.True(original.IsHidden);
            Assert.Contains(original, dockingManager.Layout.Hidden);
            Assert.False(layoutManager.IsPanelVisible("lazy-panel"));
            DrainDispatcher();

            if (useToggle)
                layoutManager.TogglePanel("lazy-panel");
            else
                layoutManager.ShowPanel("lazy-panel");

            Assert.False(original.IsHidden);
            Assert.Same(original, Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>()));
            Assert.Same(host, original.Content);
            Assert.Same(content, host.Content);
            Assert.Same(host, LogicalTreeHelper.GetParent(content));
            Assert.Equal(1, invocationCount);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedInMemoryLayoutReplacement_ReusesTheRegisteredHost(bool drainBetweenLayouts)
    {
        RunOnSta(() =>
        {
            var dockingManager = CreateDockingManager();
            var layoutManager = new DockLayoutManager(dockingManager);
            var content = new Border();
            int invocationCount = 0;
            layoutManager.RegisterPanel("lazy-panel", () => { invocationCount++; return content; }, "Lazy panel", PanelPosition.Right);
            var host = GetRegisteredLayoutContent(layoutManager);

            for (int index = 0; index < 3; index++)
            {
                Assert.Same(host, GetRegisteredLayoutContent(layoutManager));
                LayoutAnchorable anchorable = ReplaceLayout(dockingManager, host);
                layoutManager.ShowPanel("lazy-panel");
                if (drainBetweenLayouts)
                    DrainDispatcher();

                Assert.Same(anchorable, Assert.Single(dockingManager.Layout.Descendents().OfType<LayoutAnchorable>()));
                Assert.Same(host, anchorable.Content);
                Assert.Same(content, host.Content);
                Assert.Same(host, LogicalTreeHelper.GetParent(content));
                Assert.Same(dockingManager, LogicalTreeHelper.GetParent(host));
                Assert.Equal(1, invocationCount);
            }

            DrainDispatcher();
            Assert.Same(host, LogicalTreeHelper.GetParent(content));
            Assert.Same(dockingManager, LogicalTreeHelper.GetParent(host));
        });
    }

    private static DockingManager CreateDockingManager() => new()
    {
        Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPane()) }
    };

    private static LayoutAnchorable ReplaceLayout(DockingManager dockingManager, object content)
    {
        var anchorable = new LayoutAnchorable
        {
            ContentId = "lazy-panel",
            Title = "Lazy panel",
            Content = content,
            CanClose = true,
            CanHide = true
        };
        dockingManager.Layout = new LayoutRoot
        {
            RootPanel = new LayoutPanel
            {
                Children = { new LayoutDocumentPane(), new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(anchorable)) }
            }
        };
        return anchorable;
    }

    private static DeferredDockContent GetRegisteredLayoutContent(DockLayoutManager layoutManager)
    {
        // Use the real restoration callback path without touching the user's XML,
        // WorkspaceManager singleton, or ResetLayout's filesystem side effects.
        object?[] arguments = ["lazy-panel", null];
        var method = typeof(DockLayoutManager).GetMethod("TryGetRegisteredLayoutContent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(true, method.Invoke(layoutManager, arguments));
        return Assert.IsType<DeferredDockContent>(arguments[1]);
    }

    private static void DrainDispatcher() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
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
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
