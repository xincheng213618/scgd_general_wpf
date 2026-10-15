using AvalonDock;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class DockLayoutSerializationTests
{
    [Fact]
    public void DeferredTabWaitsForSelectionAndReusesItsHostAcrossLayoutRestoration()
    {
        WpfTestHost.Invoke(() =>
        {
            int factoryCalls = 0;
            var failures = new List<Exception>();
            var registration = DockContentRegistration.FromFactory("deferred", () =>
            {
                factoryCalls++;
                return new UserControl();
            });
            var host = Assert.IsType<DeferredDockContent>(registration.GetForLayout(_ => { }, failures.Add));
            var visibleContent = new UserControl();
            var visible = new LayoutAnchorable { ContentId = "visible", Content = visibleContent };
            var deferred = new LayoutAnchorable { ContentId = "deferred", Content = host };
            var docking = CreateDockingManager(visible, deferred);
            var serializer = new XmlLayoutSerializer(docking);
            serializer.LayoutSerializationCallback += (_, args) => args.Content = args.Model.ContentId switch
            {
                "visible" => visibleContent,
                "deferred" => registration.GetForLayout(_ => { }, failures.Add),
                _ => null,
            };

            WithShownDockWindow(docking, () =>
            {
                Assert.False(deferred.IsSelected);
                Assert.False(host.IsVisible);
                Assert.False(registration.IsValueCreated);
                Assert.Equal(0, factoryCalls);

                deferred.IsSelected = true;
                PumpDispatcher();

                Assert.True(host.IsVisible);
                var content = Assert.IsType<UserControl>(host.Content);
                Assert.Equal(1, factoryCalls);

                using var serialized = new StringWriter();
                serializer.Serialize(serialized);
                serializer.Deserialize(new StringReader(serialized.ToString()));
                PumpDispatcher();

                var restored = Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorable>(),
                    item => item.ContentId == "deferred");
                Assert.Same(host, restored.Content);
                Assert.Same(content, host.Content);
                Assert.Equal(1, factoryCalls);
                Assert.Empty(failures);
            });
        });
    }

    [Fact]
    public void DeferredHiddenPanelCreatesOnceAndKeepsItsHostWhenShownHiddenAndReopened()
    {
        WpfTestHost.Invoke(() =>
        {
            int factoryCalls = 0;
            var visible = new LayoutAnchorable { ContentId = "visible", Content = new UserControl() };
            var docking = CreateDockingManager(visible);
            var manager = new DockLayoutManager(docking);
            manager.RegisterPanel("deferred", () =>
            {
                factoryCalls++;
                return new UserControl();
            }, "Deferred", PanelPosition.Left);
            var registrations = Assert.IsType<Dictionary<string, DockContentRegistration>>(
                typeof(DockLayoutManager).GetField("_contentRegistry", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(manager));
            var failures = new List<Exception>();
            var host = Assert.IsType<DeferredDockContent>(
                registrations["deferred"].GetForLayout(_ => { }, failures.Add));
            var deferred = new LayoutAnchorable
            {
                ContentId = "deferred",
                Content = host,
                CanClose = true,
                CanHide = true,
            };
            Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorablePane>()).Children.Add(deferred);
            deferred.Hide();

            WithShownDockWindow(docking, () =>
            {
                Assert.True(deferred.IsHidden);
                Assert.False(host.IsVisible);
                Assert.Equal(0, factoryCalls);

                manager.ShowPanel("deferred");
                PumpDispatcher();
                var content = Assert.IsType<UserControl>(host.Content);
                Assert.True(host.IsVisible);
                Assert.Same(host, deferred.Content);
                Assert.Equal(1, factoryCalls);

                deferred.Hide();
                PumpDispatcher();
                manager.ShowPanel("deferred");
                PumpDispatcher();
                Assert.Same(host, deferred.Content);
                Assert.Same(content, host.Content);
                Assert.Equal(1, factoryCalls);

                deferred.Close();
                PumpDispatcher();
                Assert.DoesNotContain(docking.Layout.Descendents().OfType<LayoutAnchorable>(),
                    item => item.ContentId == "deferred");
                manager.ShowPanel("deferred");
                PumpDispatcher();
                var reopened = Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorable>(),
                    item => item.ContentId == "deferred");
                Assert.NotSame(deferred, reopened);
                Assert.Same(host, reopened.Content);
                Assert.Same(content, host.Content);
                Assert.True(host.IsVisible);
                Assert.Equal(1, factoryCalls);
                Assert.Empty(failures);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyLayoutRestoresRegisteredContentAndPreservesUnresolvedContentPolicy(bool hideUnresolvedTools)
    {
        WpfTestHost.Invoke(() =>
        {
            // AvalonDock v4 persisted capitalized booleans and the layout model directly.
            const string legacyLayout = """
                <LayoutRoot>
                  <RootPanel Orientation="Horizontal">
                    <LayoutAnchorablePane DockWidth="250">
                      <LayoutAnchorable Title="Registered tool" ContentId="tool" CanClose="False" CanHide="True" />
                      <LayoutAnchorable Title="Unresolved tool" ContentId="missing-tool" />
                    </LayoutAnchorablePane>
                    <LayoutDocumentPane>
                      <LayoutDocument Title="Registered document" ContentId="document" CanClose="False" />
                      <LayoutDocument Title="Unresolved document" ContentId="missing-document" />
                    </LayoutDocumentPane>
                  </RootPanel>
                </LayoutRoot>
                """;
            var toolContent = new UserControl();
            var documentContent = new UserControl();
            var docking = new DockingManager();
            var serializer = new XmlLayoutSerializer(docking)
            {
                UnresolvedContentHandling = hideUnresolvedTools ? UnresolvedContentHandling.Hide : UnresolvedContentHandling.Remove
            };
            serializer.LayoutSerializationCallback += (_, args) =>
            {
                args.Content = args.Model.ContentId switch
                {
                    "tool" => toolContent,
                    "document" => documentContent,
                    _ => null
                };
                if (args.Content == null && !hideUnresolvedTools)
                    args.Cancel = true;
            };

            serializer.Deserialize(new StringReader(legacyLayout));
            AssertRestoredContent();

            using var serialized = new StringWriter();
            serializer.Serialize(serialized);
            serializer.Deserialize(new StringReader(serialized.ToString()));
            AssertRestoredContent();

            void AssertRestoredContent()
            {
                var tool = Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorable>(), item => item.ContentId == "tool");
                Assert.Same(toolContent, tool.Content);
                Assert.False(tool.CanClose);
                var document = Assert.Single(docking.Layout.Descendents().OfType<LayoutDocument>());
                Assert.Equal("document", document.ContentId);
                Assert.Same(documentContent, document.Content);
                Assert.False(document.CanClose);
                Assert.Equal(hideUnresolvedTools ? 1 : 0, docking.Layout.Hidden.Count);
                if (hideUnresolvedTools)
                    Assert.Equal("missing-tool", Assert.Single(docking.Layout.Hidden).ContentId);
                else
                    Assert.DoesNotContain(docking.Layout.Descendents().OfType<LayoutContent>(), item => item.ContentId == "missing-tool");
            }
        });
    }

    private static DockingManager CreateDockingManager(params LayoutAnchorable[] panels)
    {
        var pane = new LayoutAnchorablePane();
        foreach (LayoutAnchorable panel in panels)
            pane.Children.Add(panel);
        pane.SelectedContentIndex = 0;
        var rootPanel = new LayoutPanel { Orientation = Orientation.Horizontal };
        rootPanel.Children.Add(pane);
        rootPanel.Children.Add(new LayoutDocumentPane());
        return new DockingManager { Layout = new LayoutRoot { RootPanel = rootPanel } };
    }

    private static void WithShownDockWindow(DockingManager docking, Action assertions)
    {
        Window? previousMainWindow = Application.Current.MainWindow;
        var window = new Window
        {
            Content = docking,
            Width = 800,
            Height = 500,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
        };
        try
        {
            window.Show();
            PumpDispatcher();
            assertions();
        }
        finally
        {
            window.Close();
            PumpDispatcher();
            Application.Current.MainWindow = previousMainWindow;
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.SystemIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
