using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using ColorVision.ImageEditor;
using ColorVision.Solution.Themes;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class DockLayoutSerializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetKeepsTheOpenEditorAndItsUnsavedContent(bool floating)
    {
        WpfTestHost.Invoke(() =>
        {
            LayoutRoot previousRoot = WorkspaceManager.layoutRoot;
            LayoutDocumentPane previousPane = WorkspaceManager.LayoutDocumentPane;
            var docking = CreateDockingManager(new LayoutAnchorable { Content = new Border() });
            docking.Theme = new AvalonDockTheme(isDark: false);
            var manager = new DockLayoutManager(docking);
            manager.RegisterDocument("home", new Border(), "Home");
            WorkspaceManager.layoutRoot = docking.Layout;
            WorkspaceManager.LayoutDocumentPane = docking.Layout.Descendents().OfType<LayoutDocumentPane>().Single();
            var draft = new UnsavedEditor();
            string path = Path.Combine(Path.GetTempPath(), $"ColorVision-Draft-{Guid.NewGuid():N}.txt");
            int closes = 0;
            LayoutDocument? document = null;
            try
            {
                WithShownDockWindow(docking, () =>
                {
                    document = EditorDocumentService.Open(path, typeof(UnsavedEditor), "Draft", () => draft, _ => closes++);
                    LayoutRoot oldLayout = docking.Layout;
                    if (floating)
                    {
                        document.Float();
                        PumpDispatcher();
                        Assert.Single(docking.FloatingWindows);
                    }
                    else
                    {
                        DocumentTabPinManager.ToggleCommand.Execute(document);
                    }

                    manager.ResetLayout();
                    PumpDispatcher();

                    Assert.NotSame(oldLayout, docking.Layout);
                    Assert.Same(docking.Layout, WorkspaceManager.layoutRoot);
                    Assert.Contains(document, WorkspaceManager.LayoutDocumentPane.Children);
                    Assert.Same(draft, document.Content);
                    Assert.True(draft.IsDirty);
                    Assert.True(document.IsActive);
                    Assert.Same(Window.GetWindow(docking), Window.GetWindow(draft));
                    Assert.Equal(!floating, DocumentTabPinManager.IsPinned(document));
                    if (!floating) Assert.Same(document, WorkspaceManager.LayoutDocumentPane.Children[0]);
                    Assert.Empty(docking.FloatingWindows);
                    Assert.Equal(0, closes);
                    Assert.Same(document, EditorDocumentService.Open<UnsavedEditor>(path, typeof(UnsavedEditor), "Draft",
                        () => throw new InvalidOperationException("Reset must preserve the open editor instance.")));
                    Assert.False(EditorDocumentService.TryCloseAllDocuments(_ => MessageBoxResult.Cancel));
                    Assert.True(EditorDocumentService.TrySaveActiveDocument());
                    Assert.False(draft.IsDirty);
                    document.Close();
                    Assert.Equal(1, closes);
                });
            }
            finally
            {
                try
                {
                    draft.Save();
                    if (document != null && closes == 0)
                    {
                        if (!ReferenceEquals(document.Root, docking.Layout))
                        {
                            document.Parent?.RemoveChild(document);
                            WorkspaceManager.LayoutDocumentPane.Children.Add(document);
                        }
                        document.Close();
                    }
                }
                finally
                {
                    WorkspaceManager.layoutRoot = previousRoot;
                    WorkspaceManager.LayoutDocumentPane = previousPane;
                }
            }
        });
    }

    private sealed class UnsavedEditor : Border, IEditorDocumentContent
    {
        public bool IsDirty { get; private set; } = true;
        public bool CanSave => true;
        public event EventHandler? DocumentStateChanged;

        public bool Save()
        {
            IsDirty = false;
            DocumentStateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    [Fact]
    public void FailedLayoutSerializationPreservesThePreviousFileAndCanBeRetried()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-DockLayout-");
        string path = Path.Combine(directory.FullName, "MainWindowDockLayout.xml");
        const string originalLayout = "<LayoutRoot />";
        const string updatedLayout = "<LayoutRoot><RootPanel /></LayoutRoot>";
        try
        {
            DockLayoutManager.WriteLayoutAtomically(path, writer => writer.Write(originalLayout));
            var failure = new IOException("Layout serialization interrupted.");

            Assert.Same(failure, Assert.Throws<IOException>(() => DockLayoutManager.WriteLayoutAtomically(path, writer =>
            {
                writer.Write("<LayoutRoot>");
                writer.Flush();
                Assert.Equal(originalLayout, File.ReadAllText(path));
                throw failure;
            })));

            Assert.Equal(originalLayout, File.ReadAllText(path));
            Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));

            DockLayoutManager.WriteLayoutAtomically(path, writer => writer.Write(updatedLayout));
            Assert.Equal(updatedLayout, File.ReadAllText(path));
            Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }

    [Theory]
    [InlineData("document")]
    [InlineData("tool")]
    [InlineData("detached tool")]
    public void WorkspaceFileDropsFollowTheFloatingWindowAndRespectContentHandlers(string windowKind)
    {
        WpfTestHost.Invoke(() =>
        {
            bool document = windowKind == "document";
            bool detached = windowKind == "detached tool";
            var surface = new Border();
            var tool = new LayoutAnchorable { ContentId = "tool", Title = "Tool", Content = document ? new Border() : surface };
            var docking = CreateDockingManager(tool, new LayoutAnchorable { ContentId = "other-tool", Title = "Other tool", Content = new Border() });
            docking.Theme = new AvalonDockTheme(isDark: false);
            var documentPane = docking.Layout.Descendents().OfType<LayoutDocumentPane>().Single();
            var tab = new LayoutDocument { ContentId = "document", Title = "Document", Content = document ? surface : new Border() };
            documentPane.Children.Add(tab);
            documentPane.Children.Add(new LayoutDocument { ContentId = "other-document", Title = "Other document", Content = new Border() });
            LayoutContent content = document ? tab : tool;

            WithShownDockWindow(docking, () =>
            {
                Window main = Window.GetWindow(docking)!;
                var opened = new List<(string[] Paths, Window Owner)>();
                _ = new WorkspaceFileDropRouter(main, docking, (paths, owner) =>
                {
                    opened.Add((paths, owner));
                    return Task.CompletedTask;
                });
                string[] paths = ["preview.mp4", "notes.txt"];

                Assert.True(RaiseFileDrop(surface, paths));
                Assert.Same(main, Assert.Single(opened).Owner);
                opened.Clear();

                if (detached)
                    docking.DetachAnchorableToWindow(tool);
                else
                    content.Float();
                PumpDispatcher();
                AssertFloatingDrop(surface);

                if (!document && !detached)
                {
                    docking.Theme = null;
                    docking.Theme = new AvalonDockTheme(isDark: true);
                    PumpDispatcher();
                    AssertFloatingDrop(surface);
                }

                void HandleContentDrop(object sender, DragEventArgs e) => e.Handled = true;
                surface.Drop += HandleContentDrop;
                Assert.True(RaiseFileDrop(surface, ["image.png"]));
                Assert.Empty(opened);
                surface.Drop -= HandleContentDrop;

                var serializer = new XmlLayoutSerializer(docking);
                using var savedLayout = new StringWriter();
                serializer.Serialize(savedLayout);

                if (detached)
                    docking.ReattachAnchorable(tool);
                else
                    content.Dock();
                PumpDispatcher();
                Assert.Empty(docking.FloatingWindows);
                Assert.True(RaiseFileDrop(surface, paths));
                Assert.Same(main, Assert.Single(opened).Owner);
                opened.Clear();

                var restoredSurface = new Border();
                serializer.LayoutSerializationCallback += (_, args) =>
                    args.Content = args.Model.ContentId == content.ContentId ? restoredSurface : new Border();
                serializer.Deserialize(new StringReader(savedLayout.ToString()));
                PumpDispatcher();
                AssertFloatingDrop(restoredSurface);

                void AssertFloatingDrop(Border target)
                {
                    var source = PresentationSource.FromVisual(target);
                    Window floating = detached
                        ? Assert.IsType<DetachedAnchorableWindow>(source?.RootVisual)
                        : Assert.Single(docking.FloatingWindows);
                    var root = Assert.IsAssignableFrom<UIElement>(source?.RootVisual);
                    Assert.NotSame(main, root);
                    Assert.True(root.AllowDrop);
                    Assert.True(target.AllowDrop);
                    Assert.True(ImageView.GetForwardUnsupportedFileDrops(target));
                    Assert.True(RaiseFileDrop(target, paths));
                    var request = Assert.Single(opened);
                    Assert.Equal(paths, request.Paths);
                    Assert.Same(floating, request.Owner);
                    opened.Clear();
                }
            });
        });
    }

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

    private static bool RaiseFileDrop(UIElement target, string[] paths)
    {
        // WPF creates DragEventArgs internally. Raise its routed event without desktop mouse input.
        ConstructorInfo constructor = typeof(DragEventArgs).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)], null)!;
        var args = (DragEventArgs)constructor.Invoke(
            [new DataObject(DataFormats.FileDrop, paths), DragDropKeyStates.None, DragDropEffects.Copy, target, new Point()]);
        args.RoutedEvent = DragDrop.DropEvent;
        target.RaiseEvent(args);
        return args.Handled;
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
