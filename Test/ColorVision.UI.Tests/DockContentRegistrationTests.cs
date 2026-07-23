using AvalonDock;
using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;

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
                Assert.Equal(0, invocationCount);

                object? first = deferredContent.Materialize();
                object? second = deferredContent.Materialize();

                Assert.Same(content, first);
                Assert.Same(first, second);
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
                Assert.Same(content, anchorable.Content);
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
}
