using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Core;
using AvalonDock.Layout;
using AvalonDock.Serializer.Xml;
using ColorVision.ImageEditor;
using ColorVision.Solution.Terminal;
using ColorVision.Solution.Themes;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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

                    // Floating window activation depends on the desktop foreground. Set the layout state under test explicitly.
                    document.IsActive = true;
                    Assert.True(document.IsActive);
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
    public void DocumentStripTracksHiddenTabsAndReleasesItsRowWhenHidden()
    {
        WpfTestHost.Invoke(() =>
        {
            var pane = new LayoutDocumentPane { ShowHeader = false };
            var documents = new[] { "Device settings", "Calibration profile", "Measurement summary" }
                .Select(title => new LayoutDocument { Title = title, Content = new Border() }).ToArray();
            foreach (var document in documents)
                pane.Children.Add(document);
            pane.SelectedContentIndex = 0;
            var docking = new DockingManager
            {
                Layout = new LayoutRoot { RootPanel = new LayoutPanel(pane) },
                Theme = new AvalonDockTheme(isDark: false),
            };

            WithShownDockWindow(docking, () =>
            {
                var panel = Assert.Single(FindVisualChildren<ColorVision.Themes.StableDocumentPaneTabPanel>(docking));
                var strip = (Grid)VisualTreeHelper.GetParent(panel);
                var paneLayout = (Grid)VisualTreeHelper.GetParent(strip);
                var window = Window.GetWindow(docking)!;
                Assert.Equal(0, paneLayout.RowDefinitions[0].ActualHeight);
                Assert.True(((Border)documents[0].Content).IsVisible);

                pane.ShowHeader = true;
                PumpDispatcher();
                Assert.False(panel.HasOverflow);
                window.Width = 280;
                documents[^1].IsActive = true;
                PumpDispatcher();
                Assert.True(panel.HasOverflow);
                Assert.True(((TabItem)panel.Children[^1]).IsVisible);
                Assert.Equal(documents, pane.Children.OfType<LayoutDocument>());

                pane.ShowHeader = false;
                PumpDispatcher();
                Assert.Equal(0, paneLayout.RowDefinitions[0].ActualHeight);
                Assert.True(((Border)documents[^1].Content).IsVisible);

                pane.ShowHeader = true;
                window.Width = 800;
                PumpDispatcher();
                Assert.True(paneLayout.RowDefinitions[0].ActualHeight > 0);
                Assert.False(panel.HasOverflow);
            });
        });
    }

    [Fact]
    public void LongDocumentTabKeepsItsSuffixAndCommandsVisibleWhenThePaneNarrows()
    {
        WpfTestHost.Invoke(() =>
        {
            const string title = "20241211T132127.4740933_SV6100_Camera_Calibration_F5.6W.cvraw";
            var document = new LayoutDocument { Title = title, Content = new Border() };
            var pane = new LayoutDocumentPane(document);
            var docking = new DockingManager
            {
                Layout = new LayoutRoot { RootPanel = new LayoutPanel(pane) },
                Theme = new AvalonDockTheme(isDark: false),
            };
            WithShownDockWindow(docking, () =>
            {
                var panel = Assert.Single(FindVisualChildren<ColorVision.Themes.StableDocumentPaneTabPanel>(docking));
                var tab = Assert.Single(panel.Children.OfType<TabItem>());
                var header = Assert.Single(FindVisualChildren<ContentPresenter>(tab), p => p.Name == "DocumentHeader");
                string Caption() => Assert.Single(FindVisualChildren<TextBlock>(header)).Text;
                var close = Assert.Single(FindVisualChildren<Button>(tab), b => b.Name == "PART_CloseButton");

                Assert.StartsWith("2024", Caption());
                Assert.Contains("…", Caption());
                Assert.EndsWith("F5.6W.cvraw", Caption());
                Assert.Equal(title, document.Title);
                Assert.Equal(title, tab.ToolTip);
                double wideTab = tab.ActualWidth;

                Window.GetWindow(docking)!.Width = 195;
                PumpDispatcher();
                Assert.True(tab.ActualWidth < wideTab);
                Assert.EndsWith(".cvraw", Caption());
                Assert.True(close.TranslatePoint(new Point(close.ActualWidth, 0), panel).X <= panel.ActualWidth + 1);

                document.Title = "A.cs";
                document.ToolTip = @"D:\Capture\A.cs";
                PumpDispatcher();
                Assert.Equal("A.cs", Caption());
                Assert.Equal(document.ToolTip, tab.ToolTip);
            });
        });
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
    public void DetachedToolUsesThePaneTitleForWindowCommands(bool canClose)
    {
        WpfTestHost.Invoke(() =>
        {
            var surface = new Border();
            var tool = new LayoutAnchorable { ContentId = "detached-tool", Title = "Tool", Content = surface, CanClose = canClose, CanHide = !canClose };
            int closed = 0;
            tool.Closed += (_, _) => closed++;
            var docking = CreateDockingManager(tool);
            docking.Theme = new AvalonDockTheme(isDark: false);

            WithShownDockWindow(docking, () =>
            {
                docking.DetachAnchorableToWindow(tool);
                PumpDispatcher();
                var window = Assert.IsType<DetachedAnchorableWindow>(PresentationSource.FromVisual(surface)?.RootVisual);
                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                Assert.NotNull(chrome);
                Assert.False(chrome.UseAeroCaptionButtons);
                var caption = Assert.Single(FindVisualChildren<AnchorablePaneTitle>(window));
                Assert.Same(tool, caption.Model);

                var menu = Assert.Single(FindVisualChildren<DropDownButton>(caption));
                Assert.True(menu.IsVisible);
                Assert.True(System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(menu));
                var maximize = Assert.Single(FindVisualChildren<Button>(caption), button => button.Name == "DetachedMaximizeButton");
                var close = Assert.Single(FindVisualChildren<Button>(caption), button => button.Name == "DetachedCloseButton");
                Assert.True(maximize.IsVisible);
                Assert.True(close.IsVisible);
                Assert.Same(window, maximize.CommandParameter);
                Assert.True(close.Command.CanExecute(close.CommandParameter));
                Assert.Empty(FindVisualChildren<Button>(caption).Where(button => button.IsVisible && button.Name is "PART_AutoHidePin" or "PART_HidePin"));

                close.Command.Execute(close.CommandParameter);
                PumpDispatcher();
                Assert.False(tool.IsDetached);
                Assert.Equal(canClose ? 1 : 0, closed);
                if (!canClose)
                    Assert.Same(Window.GetWindow(docking), PresentationSource.FromVisual(surface)?.RootVisual);
            });
        });
    }

    [Fact]
    public void FloatingDocumentCaptionTracksTheSelectedTabAndPreservesItsWindowCommands()
    {
        WpfTestHost.Invoke(() =>
        {
            var draft = new LayoutDocument { Title = "Draft.cs", Content = new Border() };
            var docking = new DockingManager
            {
                Layout = new LayoutRoot { RootPanel = new LayoutPanel(new LayoutDocumentPane(draft)) },
                Theme = new AvalonDockTheme(isDark: false),
            };
            bool allowClose = false;
            draft.Closing += (_, e) => e.Cancel = !allowClose;
            WithShownDockWindow(docking, () =>
            {
                var host = Window.GetWindow(docking)!;
                host.Title = "Inspection workspace";
                host.Icon = System.Windows.Media.Imaging.BitmapSource.Create(16, 16, 96, 96,
                    PixelFormats.Bgra32, null, new byte[16 * 16 * 4], 16 * 4);
                draft.Float();
                PumpDispatcher();
                var window = Assert.IsType<LayoutDocumentFloatingWindowControl>(Assert.Single(docking.FloatingWindows));
                window.Width = 650;
                PumpDispatcher();
                var caption = Assert.IsAssignableFrom<FrameworkElement>(window.Template.FindName("Header", window));
                var header = Assert.IsType<ContentPresenter>(window.Template.FindName("HeaderContent", window));
                var hostName = Assert.IsType<TextBlock>(window.Template.FindName("HostName", window));
                Assert.True(hostName.IsVisible);
                Assert.Equal(host.Title, hostName.Text);
                Assert.Same(host.Icon, Assert.IsType<System.Windows.Controls.Image>(window.Template.FindName("CaptionIcon", window)).Source);
                Assert.Same(draft, header.Content);
                draft.Title = "Renamed.cs";
                window.Width = 290;
                PumpDispatcher();
                Assert.Equal(draft.Title, window.Title);
                Assert.Empty(hostName.Text);
                Assert.Contains(FindVisualChildren<TextBlock>(header), text => text.IsVisible && text.Text == draft.Title);
                Assert.True(header.ActualWidth > 0);

                var pane = Assert.IsType<LayoutDocumentPane>(draft.Parent);
                var second = new LayoutDocument { Title = "Settings.json", Content = new Border() };
                pane.Children.Add(second);
                second.IsActive = true;
                PumpDispatcher();
                Assert.Same(second, header.Content);
                Assert.Equal(second.Title, window.Title);
                var paneRoot = PresentationSource.FromVisual((Border)second.Content)!.RootVisual;
                var tab = Assert.Single(FindVisualChildren<TabItem>(paneRoot), item => item.IsSelected);
                Invoke(Assert.Single(FindVisualChildren<Button>(tab), button => button.Name == "PART_PinButton"));
                Assert.True(DocumentTabPinManager.IsPinned(second));

                Invoke(WindowButton("PART_PinMaximize"));
                Assert.Equal(WindowState.Maximized, window.WindowState);
                var workArea = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;
                var captionBounds = new Rect(caption.PointToScreen(new Point()),
                    caption.PointToScreen(new Point(caption.ActualWidth, caption.ActualHeight)));
                Assert.True(new Rect(workArea.X, workArea.Y, workArea.Width, workArea.Height).Contains(captionBounds));
                Invoke(WindowButton("PART_PinRestore"));
                Assert.Equal(WindowState.Normal, window.WindowState);

                Invoke(WindowButton("PART_PinClose"));
                Assert.Same(draft, Assert.Single(pane.Children));
                Assert.Same(draft, header.Content);
                Assert.True(window.IsVisible);
                allowClose = true;
                Invoke(WindowButton("PART_PinClose"));
                Assert.Empty(docking.FloatingWindows);

                Button WindowButton(string name) => Assert.IsType<Button>(window.Template.FindName(name, window));
                static void Invoke(Button button)
                {
                    Assert.True(button.IsVisible && button.IsEnabled);
                    var invoke = Assert.IsAssignableFrom<IInvokeProvider>(new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke));
                    invoke.Invoke();
                    PumpDispatcher();
                }
            });
        });
    }

    [Fact]
    public void SplitFloatingDocumentCaptionKeepsItsLastDocumentWhenTheMainWindowTakesFocus()
    {
        WpfTestHost.Invoke(() =>
        {
            var main = new LayoutDocument { Title = "Main", Content = new Border() };
            var first = new LayoutDocument { Title = "First", Content = new Border() };
            var mainPane = new LayoutDocumentPane(main);
            mainPane.Children.Add(first);
            var docking = new DockingManager
            {
                Layout = new LayoutRoot { RootPanel = new LayoutPanel(mainPane) },
                Theme = new AvalonDockTheme(isDark: true),
            };
            WithShownDockWindow(docking, () =>
            {
                first.Float();
                PumpDispatcher();
                var window = Assert.IsType<LayoutDocumentFloatingWindowControl>(Assert.Single(docking.FloatingWindows));
                var model = Assert.IsType<LayoutDocumentFloatingWindow>(window.Model);
                var second = new LayoutDocument { Title = "Second", Content = new Border() };
                model.RootPanel.Children.Add(new LayoutDocumentPane(second));
                var header = Assert.IsType<ContentPresenter>(window.Template.FindName("HeaderContent", window));
                foreach (var document in new[] { second, first })
                {
                    document.IsActive = true;
                    PumpDispatcher();
                    Assert.Same(document, header.Content);
                    main.IsActive = true;
                    PumpDispatcher();
                    Assert.Same(document, header.Content);
                }
            });
        });
    }

    [Fact]
    public void MaximizedFloatingToolKeepsItsCaptionAndTabsInsideTheMonitorWorkArea()
    {
        WpfTestHost.Invoke(() =>
        {
            var surface = new Border();
            var tool = new LayoutAnchorable { Title = "First tool", Content = surface };
            var docking = CreateDockingManager(tool);
            docking.Theme = new AvalonDockTheme(isDark: true);
            WithShownDockWindow(docking, () =>
            {
                tool.Float();
                PumpDispatcher();
                var window = Assert.IsType<LayoutAnchorableFloatingWindowControl>(Assert.Single(docking.FloatingWindows));
                var pane = Assert.IsType<LayoutAnchorablePane>(tool.Parent);
                pane.Children.Add(new LayoutAnchorable { Title = "Second tool", Content = new Border() });
                tool.IsSelected = true;
                PumpDispatcher();

                var maximize = Assert.IsType<Button>(window.Template.FindName("PART_PinMaximize", window));
                Assert.True(maximize.IsVisible);
                var command = Assert.IsAssignableFrom<System.Windows.Input.RoutedCommand>(maximize.Command);
                command.Execute(maximize.CommandParameter, maximize);
                PumpDispatcher();
                Assert.Equal(WindowState.Maximized, window.WindowState);

                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                var workArea = new Rect(area.X, area.Y, area.Width, area.Height);
                var caption = Assert.IsAssignableFrom<FrameworkElement>(window.Template.FindName("Header", window));
                AssertInsideWorkArea(caption);

                // AvalonDock hosts the pane in a child HWND with its own visual tree.
                var contentRoot = PresentationSource.FromVisual(surface)!.RootVisual;
                var tabs = FindVisualChildren<TabItem>(contentRoot).ToArray();
                Assert.Equal(2, tabs.Length);
                Assert.All(tabs, AssertInsideWorkArea);

                void AssertInsideWorkArea(FrameworkElement element)
                {
                    Assert.True(element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0);
                    var bounds = new Rect(element.PointToScreen(new Point()),
                        element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight)));
                    Assert.True(workArea.Contains(bounds), $"{element.GetType().Name} bounds {bounds} exceed work area {workArea}.");
                }
            });
        });
    }

    [Fact]
    public void TerminalSessionsOpenAsPeersAndCloseIndependently()
    {
        WpfTestHost.Invoke(() =>
        {
            var docking = CreateDockingManager(new LayoutAnchorable { Content = new Border() });
            docking.Theme = new AvalonDockTheme(isDark: false);
            var manager = new DockLayoutManager(docking);
            DockLayoutManager? previousManager = WorkspaceManager.LayoutManager;
            string scriptPath = Path.Combine(Path.GetTempPath(), $"ColorVision-Terminal-{Guid.NewGuid():N}.cmd");
            string scriptMarker = Path.ChangeExtension(scriptPath, ".done");
            try
            {
                File.WriteAllText(scriptPath, "@echo ColorVision terminal dock smoke > \"%~dpn0.done\"\r\n");
                WorkspaceManager.LayoutManager = manager;
                manager.RegisterPanel("LogPanel", new Border(), "Log", PanelPosition.Bottom);
                new TerminalPanelProvider().RegisterPanels();
                manager.ShowPanel("LogPanel");
                manager.ShowPanel(TerminalService.PanelId);
                var logPanel = GetPanel("LogPanel");
                var primaryPanel = GetPanel(TerminalService.PanelId);
                using var interactive = Assert.IsType<TerminalControl>(Assert.IsType<DeferredDockContent>(primaryPanel.Content).Content);
                var interactiveMenu = Assert.IsType<Grid>(interactive.FindName("TerminalRoot")).ContextMenu;
                var service = TerminalService.GetInstance();

                WithShownDockWindow(docking, () =>
                {
                    var pane = Assert.IsType<LayoutAnchorablePane>(logPanel.Parent);
                    Assert.Same(pane, primaryPanel.Parent);
                    Assert.Collection(pane.Children, panel => Assert.Same(logPanel, panel), panel => Assert.Same(primaryPanel, panel));
                    Assert.True(primaryPanel.IsSelected);
                    AssertMenuOpens(GetTitleButton(primaryPanel, 1), interactiveMenu);

                    InvokeTitleButton(GetTitleButton(primaryPanel, 0));
                    var secondPanel = Assert.Single(pane.Children, panel => panel.Content is TerminalControl);
                    using var second = Assert.IsType<TerminalControl>(secondPanel.Content);
                    Assert.NotSame(interactive, second);
                    Assert.True(secondPanel.IsSelected);

                    service.RunScript(scriptPath);
                    PumpDispatcher();
                    var runPanel = GetPanel(TerminalService.RunPanelId);
                    using var run = Assert.IsType<TerminalControl>(runPanel.Content);
                    var runMenu = Assert.IsType<Grid>(run.FindName("TerminalRoot")).ContextMenu;
                    Assert.Same(pane, runPanel.Parent);
                    Assert.True(runPanel.IsSelected);
                    Assert.NotSame(interactive, run);
                    Assert.NotSame(second, run);
                    WaitForScriptMarker(scriptMarker);
                    AssertMenuOpens(GetTitleButton(runPanel, 1), runMenu);
                    Assert.False(interactiveMenu.IsOpen);

                    manager.ShowPanel(TerminalService.PanelId);
                    Assert.True(primaryPanel.IsSelected);
                    File.Delete(scriptMarker);
                    service.RunScript(scriptPath);
                    Assert.Same(runPanel, GetPanel(TerminalService.RunPanelId));
                    Assert.Same(run, runPanel.Content);
                    Assert.True(runPanel.IsSelected);
                    WaitForScriptMarker(scriptMarker);

                    runPanel.Close();
                    secondPanel.Close();
                    Assert.True(run.IsDisposed);
                    Assert.True(second.IsDisposed);
                    Assert.False(interactive.IsDisposed);
                    Assert.DoesNotContain(runPanel, pane.Children);
                    Assert.DoesNotContain(secondPanel, pane.Children);

                    Button GetTitleButton(LayoutAnchorable panel, int buttonIndex)
                    {
                        var title = Assert.Single(FindVisualChildren<AnchorablePaneTitle>(docking),
                            candidate => candidate.IsVisible && ReferenceEquals(candidate.Model, panel));
                        var buttons = FindVisualChildren<Button>(title).Where(button => button.Name == "PanelTitleActionButton").ToArray();
                        Assert.Equal(2, buttons.Length);
                        return buttons[buttonIndex];
                    }

                    static void InvokeTitleButton(Button button)
                    {
                        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke));
                        invoke.Invoke();
                        PumpDispatcher();
                    }

                    static void AssertMenuOpens(Button gear, ContextMenu expected)
                    {
                        try
                        {
                            InvokeTitleButton(gear);
                            Assert.True(expected.IsOpen);
                            Assert.Same(gear, expected.PlacementTarget);
                            Assert.Equal(PlacementMode.Bottom, expected.Placement);

                            CloseContextMenuAndWait(expected);
                            Assert.Same(DependencyProperty.UnsetValue, expected.ReadLocalValue(ContextMenu.PlacementTargetProperty));
                            Assert.Same(DependencyProperty.UnsetValue, expected.ReadLocalValue(ContextMenu.PlacementProperty));
                        }
                        finally
                        {
                            expected.IsOpen = false;
                        }
                    }
                });

                LayoutAnchorable GetPanel(string contentId) => Assert.Single(docking.Layout.Descendents().OfType<LayoutAnchorable>(),
                    item => item.ContentId == contentId);
            }
            finally
            {
                WorkspaceManager.LayoutManager = previousManager;
                File.Delete(scriptPath);
                File.Delete(scriptMarker);
            }
        });
    }

    private static void WaitForScriptMarker(string path)
    {
        if (File.Exists(path))
            return;

        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
        watcher.Created += (_, _) => dispatcher.BeginInvoke(new Action(() => frame.Continue = false));
        watcher.EnableRaisingEvents = true;
        var watchdog = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        watchdog.Tick += (_, _) => frame.Continue = false;
        watchdog.Start();
        if (!File.Exists(path))
            Dispatcher.PushFrame(frame);
        watchdog.Stop();

        Assert.True(File.Exists(path), "The script did not write its completion marker.");
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

    private static void CloseContextMenuAndWait(ContextMenu menu)
    {
        // WPF raises Closed after the popup's closing animation completes.
        var frame = new DispatcherFrame();
        bool closed = false;
        var watchdog = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
        watchdog.Tick += (_, _) => frame.Continue = false;
        menu.Closed += OnClosed;
        try
        {
            watchdog.Start();
            menu.IsOpen = false;
            if (!closed)
                Dispatcher.PushFrame(frame);
        }
        finally
        {
            watchdog.Stop();
            menu.Closed -= OnClosed;
        }
        Assert.True(closed, "The context menu did not raise Closed.");

        void OnClosed(object sender, RoutedEventArgs args)
        {
            closed = true;
            frame.Continue = false;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typed)
                yield return typed;
            foreach (T descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
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
