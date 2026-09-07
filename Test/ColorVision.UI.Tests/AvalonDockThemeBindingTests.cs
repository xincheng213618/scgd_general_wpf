using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

/// <summary>Real AvalonDock controls with synthetic content; never starts MainWindow or loads a saved workspace.</summary>
public class AvalonDockThemeBindingTests
{
    [Fact]
    public void DockedCaption_RetainsActionsAcrossEmptyModelAndThemeReplacement()
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            var model = new LayoutAnchorable { Title = "Solution Explorer" };
            var title = new AnchorablePaneTitle { Model = model };
            foreach (bool dark in new[] { false, true, false })
            {
                var theme = new AvalonDockTheme(dark);
                ResourceDictionary globalPalette = LoadGlobalPalette(dark);
                title.Resources.MergedDictionaries.Clear();
                title.Resources.MergedDictionaries.Add(globalPalette);
                title.Resources.MergedDictionaries.Add(theme.ThemeResourceDictionary);
                title.Style = (Style)theme.ThemeResourceDictionary[typeof(AnchorablePaneTitle)];
                foreach (bool active in new[] { false, true, false })
                {
                    model.IsActive = active;
                    ArrangeCaption(title);
                    Part<DropDownButton>(title, "MenuDropDownButton");
                    Part<Button>(title, "PART_AutoHidePin");
                    Part<Button>(title, "PART_HidePin");
                }
                title.Model = null;
                ArrangeCaption(title);
                title.Model = model;
            }
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Fact]
    public void ThemeReplacement_PreservesCommandMenus()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            foreach (bool dark in new[] { false, true, false })
            {
                var theme = new AvalonDockTheme(dark);
                scene.ReplaceGlobalPalette(dark);
                scene.Manager.Theme = null;
                scene.Manager.Theme = theme;
                Arrange(scene.Manager);
                foreach (LayoutContent model in new LayoutContent[] { scene.Document, scene.Tool })
                {
                    bool document = model is LayoutDocument;
                    ContextMenu menu = Assert.IsAssignableFrom<ContextMenu>(document
                        ? scene.Manager.DocumentContextMenu : scene.Manager.AnchorableContextMenu);
                    FrameworkElement header = document
                        ? DocumentTab(scene.DocumentPaneControl, scene.Document) : scene.ToolTitle;
                    TextBlock caption = Assert.Single(Descendants<TextBlock>(header), text => text.Text == model.Title);
                    AssertRightClickOpensMenu(scene, caption, menu, scene.Manager.GetLayoutItemFromModel(model));
                }
            }
        });
    }

    [Fact]
    public void RealManager_SwitchesThemesWithoutReplacingContentOrLosingFocusState()
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(false);
            LayoutRoot originalLayout = scene.Manager.Layout;
            object originalDocumentContent = scene.Document.Content;
            object originalToolContent = scene.Tool.Content;
            foreach (bool dark in new[] { false, true, false })
            {
                scene.Document.IsActive = true;
                var theme = new AvalonDockTheme(dark);
                // Use the production replacement path on the same manager, model and content.
                scene.ReplaceGlobalPalette(dark);
                scene.Manager.Theme = null;
                scene.Manager.Theme = theme;
                Arrange(scene.Manager);
                Assert.Same(originalLayout, scene.Manager.Layout);
                Assert.Same(originalDocumentContent, scene.Document.Content);
                Assert.Same(originalToolContent, scene.Tool.Content);
                Assert.True(scene.Document.IsSelected);
                LayoutDocumentPaneControl documents = scene.DocumentPaneControl;
                TabItem selectedTab = DocumentTab(documents, scene.Document);
                Assert.True(selectedTab.IsSelected);

                scene.Tool.IsActive = true;
                Arrange(scene.Manager);
                Assert.True(scene.Tool.IsActive);
                Assert.True(scene.Document.IsSelected);
                Assert.False(scene.Document.IsActive);
                Assert.True(scene.Document.IsLastFocusedDocument);
                Assert.Same(scene.Document, documents.SelectedContent);

                scene.Document.IsActive = true;
                Arrange(scene.Manager);
                Assert.False(scene.Tool.IsActive);

                scene.SecondDocument.IsActive = true;
                Arrange(scene.Manager);
                Assert.False(selectedTab.IsSelected);
                Assert.False(scene.Document.IsLastFocusedDocument);
                TabItem otherTab = DocumentTab(documents, scene.SecondDocument);
                Assert.True(otherTab.IsSelected);
            }
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Fact]
    public void InitiallySingleTool_GeneratesSelectedContentAndMaterializesOnceAcrossTabCountAndThemeChanges()
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            int factoryCalls = 0;
            int materializedNotifications = 0;
            bool factoryRanWhileLoadedAndVisible = false;
            var lifecycle = new List<string>();
            var failures = new List<Exception>();
            var payload = new Border { Child = new TextBlock { Text = "Synthetic deferred tool content" } };
            DeferredDockContent? deferred = null;
            deferred = new DeferredDockContent(() =>
            {
                factoryCalls++;
                factoryRanWhileLoadedAndVisible = deferred!.IsLoaded && deferred.IsVisible;
                lifecycle.Add("factory");
                return payload;
            }, _ => materializedNotifications++, failures.Add);
            deferred.Loaded += (_, _) => lifecycle.Add("loaded");
            var tool = new LayoutAnchorable
            {
                ContentId = "synthetic-single-tool", Title = "Synthetic Chat Assistant", Content = deferred, IsSelected = true
            };
            var tools = new LayoutAnchorablePane(tool);
            var document = new LayoutDocument { Title = "Synthetic document", Content = new Border(), IsSelected = true };
            var rootPanel = new LayoutPanel();
            rootPanel.Children.Add(new LayoutDocumentPane(document));
            rootPanel.Children.Add(new LayoutAnchorablePaneGroup(tools) { DockWidth = new GridLength(300) });
            var manager = new DockingManager { Theme = new AvalonDockTheme(false), Layout = new LayoutRoot { RootPanel = rootPanel } };
            ResourceDictionary globalPalette = LoadGlobalPalette(false);
            manager.Resources.MergedDictionaries.Add(globalPalette);
            tool.IsSelected = true;
            var host = new Window
            {
                Content = manager, Width = 720, Height = 460, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            try
            {
                // The first layout must start with one selected model. Starting with
                // two tabs and removing one would miss the item-generation deadlock.
                Assert.Single(tools.Children);
                Assert.True(tool.IsSelected);
                Assert.False(deferred.IsLoaded);
                Assert.Equal(0, factoryCalls);
                host.Show();
                AssertToolContent(singleTool: true);
                Assert.False(host.IsActive);
                Assert.True(factoryRanWhileLoadedAndVisible);
                Assert.True(lifecycle.IndexOf("loaded") >= 0 && lifecycle.IndexOf("loaded") < lifecycle.IndexOf("factory"));

                foreach (bool dark in new[] { true, false })
                {
                    manager.Resources.MergedDictionaries.Remove(globalPalette);
                    globalPalette = LoadGlobalPalette(dark);
                    manager.Resources.MergedDictionaries.Add(globalPalette);
                    manager.Theme = null;
                    manager.Theme = new AvalonDockTheme(dark);
                    AssertToolContent(singleTool: true);

                    var secondTool = new LayoutAnchorable { Title = "Second synthetic tool", Content = new Border() };
                    tools.Children.Add(secondTool);
                    AssertToolContent(singleTool: false);
                    secondTool.IsSelected = true;
                    Arrange(manager, 720, 460);
                    Assert.Same(secondTool, Assert.Single(Descendants<LayoutAnchorablePaneControl>(manager)).SelectedContent);
                    tool.IsSelected = true;
                    AssertToolContent(singleTool: false);
                    tools.Children.Remove(secondTool);
                    AssertToolContent(singleTool: true);
                }
                Assert.DoesNotContain("BindingExpression path error", trace.Output);
            }
            finally
            {
                host.Content = null;
                host.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                manager.Layout = new LayoutRoot();
            }

            void AssertToolContent(bool singleTool)
            {
                Arrange(manager, 720, 460);
                Assert.True(manager.IsLoaded);
                LayoutAnchorablePaneControl pane = Assert.Single(Descendants<LayoutAnchorablePaneControl>(manager));
                Assert.Same(tools, pane.Model);
                Assert.True(tool.IsSelected);
                Assert.Equal(0, pane.SelectedIndex);
                Assert.Same(tool, pane.SelectedContent);
                Assert.Same(tool, Part<ContentPresenter>(pane, "PART_SelectedContentHost").Content);
                TabItem tab = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(tool));
                Assert.True(tab.IsSelected);
                Assert.Equal(singleTool ? Visibility.Collapsed : Visibility.Visible, tab.Visibility);
                Grid strip = Part<Grid>(pane, "ToolTabStrip");
                Assert.Equal(Visibility.Visible, strip.Visibility);
                if (singleTool)
                {
                    Assert.Equal(0, strip.Height);
                    Assert.Equal(0, strip.ActualHeight);
                }
                else
                {
                    Assert.True(double.IsNaN(strip.Height));
                    Assert.True(strip.ActualHeight > 0);
                }
                Assert.True(Part<AnchorablePaneTabPanel>(pane, "HeaderPanel").IsItemsHost);
                LayoutAnchorableControl content = Assert.Single(Descendants<LayoutAnchorableControl>(pane), candidate => ReferenceEquals(candidate.Model, tool));
                AnchorablePaneTitle title = Assert.Single(Descendants<AnchorablePaneTitle>(content), candidate => ReferenceEquals(candidate.Model, tool));
                Assert.True(title.IsVisible);
                Assert.True(Part<Border>(title, "CaptionBorder").ActualHeight > 0);
                Assert.True(deferred.IsLoaded && deferred.IsVisible);
                Assert.NotNull(PresentationSource.FromVisual(payload));
                Assert.Same(deferred, tool.Content);
                Assert.Same(payload, deferred.Content);
                Assert.Empty(failures);
                Assert.Equal(1, factoryCalls);
                Assert.Equal(1, materializedNotifications);
            }
        });
    }

    [Fact]
    public void FactoryTool_ClosedAndImmediatelyReopened_RetainsVisiblePayloadInTheSameHost()
    {
        WpfTestHost.Invoke(() =>
        {
            var rootPanel = new LayoutPanel();
            rootPanel.Children.Add(new LayoutDocumentPane(new LayoutDocument { Title = "Synthetic document", Content = new Border() }));
            var manager = new DockingManager { Theme = new AvalonDockTheme(false), Layout = new LayoutRoot { RootPanel = rootPanel } };
            manager.Resources.MergedDictionaries.Add(LoadGlobalPalette(false));
            var layout = new DockLayoutManager(manager);
            var payload = new Border { Child = new TextBlock { Text = "Synthetic reopened content" } };
            int factoryCalls = 0;
            layout.RegisterPanel("synthetic-reopened-tool", () => { factoryCalls++; return payload; }, "Synthetic Chat Assistant", PanelPosition.Right);
            var host = new Window
            {
                Content = manager, Width = 720, Height = 460, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            try
            {
                Assert.Equal(0, factoryCalls);
                host.Show();
                layout.ShowPanel("synthetic-reopened-tool");
                Arrange(manager, 720, 460);
                LayoutAnchorable original = Assert.Single(manager.Layout.Descendents().OfType<LayoutAnchorable>());
                DeferredDockContent contentHost = Assert.IsType<DeferredDockContent>(original.Content);
                AssertVisiblePayload(original);

                // No dispatcher drain between these calls: the old LayoutItem and
                // ContentPresenter cleanup must not retain or later detach the host.
                original.Close();
                layout.ShowPanel("synthetic-reopened-tool");
                Arrange(manager, 720, 460);

                LayoutAnchorable reopened = Assert.Single(manager.Layout.Descendents().OfType<LayoutAnchorable>());
                Assert.NotSame(original, reopened);
                Assert.Null(original.Root);
                Assert.Same(contentHost, reopened.Content);
                AssertVisiblePayload(reopened);
                Assert.False(host.IsActive);

                void AssertVisiblePayload(LayoutAnchorable model)
                {
                    Assert.True(layout.IsPanelVisible("synthetic-reopened-tool"));
                    Assert.NotNull(manager.GetLayoutItemFromModel(model));
                    LayoutAnchorablePaneControl pane = Assert.Single(Descendants<LayoutAnchorablePaneControl>(manager));
                    Assert.Same(model, pane.SelectedContent);
                    LayoutAnchorableControl content = Assert.Single(Descendants<LayoutAnchorableControl>(pane), candidate => ReferenceEquals(candidate.Model, model));
                    AnchorablePaneTitle title = Assert.Single(Descendants<AnchorablePaneTitle>(content), candidate => ReferenceEquals(candidate.Model, model));
                    Assert.Equal("Synthetic Chat Assistant", title.Model.Title);
                    Assert.True(title.IsVisible && Part<Border>(title, "CaptionBorder").ActualHeight > 0);
                    Assert.Same(payload, contentHost.Content);
                    Assert.Same(contentHost, LogicalTreeHelper.GetParent(payload));
                    Assert.Same(manager, LogicalTreeHelper.GetParent(contentHost));
                    Assert.True(content.IsAncestorOf(contentHost));
                    Assert.True(payload.IsLoaded && payload.IsVisible);
                    Assert.True(payload.ActualWidth > 0 && payload.ActualHeight > 0);
                    Assert.NotNull(PresentationSource.FromVisual(payload));
                    Assert.Same(PresentationSource.FromVisual(manager), PresentationSource.FromVisual(payload));
                    Assert.Same(host, Window.GetWindow(payload));
                    Assert.Equal(1, factoryCalls);
                }
            }
            finally
            {
                host.Content = null;
                host.Close();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                manager.Layout = new LayoutRoot();
            }
        });
    }

    [Fact]
    public void MultipleDocumentGroups_EmphasizeOnlyTheActiveDocumentWithoutStealingToolActivation()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            var otherDocument = new LayoutDocument { Title = "Second document group", Content = new Border() };
            var otherGroup = new LayoutDocumentPane(otherDocument);
            scene.Manager.Layout.RootPanel.Children.Add(otherGroup);
            Arrange(scene.Manager);
            foreach (bool focusOtherGroup in new[] { false, true, false })
            {
                LayoutDocument activeDocument = focusOtherGroup ? otherDocument : scene.Document;
                activeDocument.IsActive = true;
                Arrange(scene.Manager);
                AssertDocumentGroups(activeDocument);
                scene.Tool.IsActive = true;
                Arrange(scene.Manager);
                Assert.True(scene.Tool.IsActive);
                Assert.False(scene.Document.IsActive);
                Assert.False(otherDocument.IsActive);
                Assert.Equal(!focusOtherGroup, scene.Document.IsLastFocusedDocument);
                Assert.Equal(focusOtherGroup, otherDocument.IsLastFocusedDocument);
                AssertDocumentGroups(null);
            }

            void AssertDocumentGroups(LayoutDocument? activeDocument)
            {
                foreach ((LayoutDocumentPane model, LayoutDocument document) in new[]
                {
                    (scene.Documents, scene.Document),
                    (otherGroup, otherDocument)
                })
                {
                    LayoutDocumentPaneControl pane = Assert.Single(Descendants<LayoutDocumentPaneControl>(scene.Manager),
                        candidate => ReferenceEquals(candidate.Model, model));
                    TabItem tab = DocumentTab(pane, document);
                    Assert.True(document.IsSelected);
                    Assert.True(tab.IsSelected);
                    bool active = ReferenceEquals(document, activeDocument);
                    Assert.Equal(active, document.IsActive);
                }
            }
        });
    }

    [Fact]
    public void AdjacentToolTabs_HitTestTheirOwnLayoutItem()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            Arrange(scene.Manager);
            LayoutAnchorablePaneControl pane = scene.ToolPaneControl;
            TabItem first = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(scene.Tool));
            TabItem second = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(scene.SecondTool));

            AssertTabHit(first, second);
            AssertTabHit(second, first);

            void AssertTabHit(TabItem expected, TabItem adjacent)
            {
                Point center = expected.TranslatePoint(
                    new Point(expected.ActualWidth / 2, expected.ActualHeight / 2),
                    scene.Manager);
                UIElement hit = Assert.IsAssignableFrom<UIElement>(scene.Manager.InputHitTest(center));
                DependencyObject[] route = VisualAncestorsAndSelf(hit).ToArray();
                Assert.Contains(expected, route);
                Assert.DoesNotContain(adjacent, route);
            }
        });
    }

    [Fact]
    public void PaneTitle_LoadsActionsFromContentProviderAndLeavesBottomTabPlain()
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(false);
            scene.SecondTool.Content = new DisPlayControlPanel();
            scene.SecondTool.IsSelected = true;
            Arrange(scene.Manager);

            AnchorablePaneTitle title = Assert.Single(Descendants<AnchorablePaneTitle>(scene.Manager),
                candidate => ReferenceEquals(candidate.Model, scene.SecondTool));
            Border caption = Part<Border>(title, "CaptionBorder");
            Button action = Assert.Single(Descendants<Button>(title), button => button.Command == DisPlayManager.CreateGroupCommand);
            DropDownButton menu = Part<DropDownButton>(title, "MenuDropDownButton");
            TabItem secondTab = Assert.IsType<TabItem>(scene.ToolPaneControl.ItemContainerGenerator.ContainerFromItem(scene.SecondTool));
            TextBlock glyph = Assert.Single(Descendants<TextBlock>(action));

            Assert.Equal("PanelTitleActionButton", action.Name);
            Assert.Equal(24, action.ActualWidth);
            Assert.Equal(24, action.ActualHeight);
            Assert.Equal("新建分组", action.ToolTip);
            Assert.Equal("\uE710", glyph.Text);
            AssertInside(action, caption);
            Assert.True(action.TranslatePoint(new Point(), caption).X < menu.TranslatePoint(new Point(), caption).X);
            Assert.DoesNotContain(Descendants<Button>(secondTab), button => button.Command == DisPlayManager.CreateGroupCommand);

            scene.Tool.IsSelected = true;
            Arrange(scene.Manager);
            Assert.DoesNotContain(Descendants<Button>(scene.ToolTitle), button => button.Command == DisPlayManager.CreateGroupCommand);
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Theory]
    [InlineData("tool-title")]
    [InlineData("document-tab")]
    [InlineData("tool-tab")]
    public void HeaderRightClick_OnTextAndPadding_OpensTheActualLayoutItemsCommands(string surface)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(false);
            LayoutContent model = surface == "document-tab" ? scene.Document : scene.Tool;
            foreach (bool selectedOrActive in new[] { false, true })
            {
                if (selectedOrActive) model.IsActive = true;
                else if (surface == "document-tab") scene.SecondDocument.IsActive = true;
                else if (surface == "tool-tab") scene.SecondTool.IsActive = true;
                else scene.Document.IsActive = true;
                Arrange(scene.Manager);

                FrameworkElement header = surface switch
                {
                    "tool-title" => scene.ToolTitle,
                    "document-tab" => DocumentTab(scene.DocumentPaneControl, scene.Document),
                    _ => Assert.IsType<TabItem>(scene.ToolPaneControl.ItemContainerGenerator.ContainerFromItem(scene.Tool))
                };
                TextBlock caption = Assert.Single(Descendants<TextBlock>(header), text => text.Text == model.Title);
                var samples = new List<(string Name, Point Point, bool OnText)>
                {
                    ("text", caption.TranslatePoint(new Point(Math.Min(6, caption.ActualWidth / 2), caption.ActualHeight / 2), header), true),
                    ("left padding", new Point(2, header.ActualHeight / 2), false)
                };
                double rightPaddingX = surface == "tool-title"
                    ? Part<DropDownButton>((Control)header, "MenuDropDownButton").TranslatePoint(new Point(), header).X - 2
                    : header.ActualWidth - 2;
                samples.Add(("right padding", new Point(rightPaddingX, header.ActualHeight / 2), false));
                if (surface != "tool-title")
                    samples.Add(("bottom padding", new Point(header.ActualWidth / 2, header.ActualHeight - 2), false));

                foreach ((string name, Point point, bool onText) in samples)
                {
                    UIElement hit = Assert.IsAssignableFrom<UIElement>(scene.Manager.InputHitTest(header.TranslatePoint(point, scene.Manager)));
                    DependencyObject[] route = VisualAncestorsAndSelf(hit).ToArray();
                    if (onText) Assert.Contains(caption, route);
                    else Assert.DoesNotContain(caption, route);
                    Type nativeHeaderType = surface switch
                    {
                        "tool-title" => typeof(AnchorablePaneTitle),
                        "document-tab" => typeof(LayoutDocumentTabItem),
                        _ => typeof(LayoutAnchorableTabItem)
                    };
                    Assert.True(route.Any(nativeHeaderType.IsInstanceOfType), $"{surface} {name} bypassed the native AvalonDock header.");
                    DropDownControlArea area = Assert.Single(route.OfType<DropDownControlArea>());
                    string menuProperty = surface == "document-tab" ? nameof(DockingManager.DocumentContextMenu) : nameof(DockingManager.AnchorableContextMenu);
                    ContextMenu? menu = surface == "document-tab" ? scene.Manager.DocumentContextMenu : scene.Manager.AnchorableContextMenu;
                    // Identity alone accepts Same(null, null), hiding a missing production menu.
                    Assert.True(menu != null, $"{surface} ({name}): the production theme did not initialize DockingManager.{menuProperty}.");
                    Assert.NotNull(menu);
                    Assert.True(area.DropDownContextMenu != null, $"{surface} ({name}): the native DropDownControlArea has no bound {menuProperty}.");
                    Assert.NotNull(area.DropDownContextMenu);
                    LayoutItem layoutItem = scene.Manager.GetLayoutItemFromModel(model);
                    Assert.NotNull(layoutItem);
                    Assert.Same(menu, area.DropDownContextMenu);
                    Assert.Same(layoutItem, area.DropDownContextMenuDataContext);
                    AssertRightClickOpensMenu(scene, hit, menu, layoutItem);
                }
            }
            Assert.True(!trace.Output.Contains("BindingExpression path error"), trace.Output);
        });
    }

    [Fact]
    public void DocumentCloseButton_HonorsCancellationAndCanClose()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            scene.Document.IsActive = true;
            Arrange(scene.Manager);
            Button close = Part<Button>(DocumentHeader(scene.Manager, scene.Document), "PART_CloseButton");
            int confirmations = 0;
            bool cancel = true;
            scene.Document.Closing += (_, args) => { confirmations++; args.Cancel = cancel; };
            Execute(close);
            Assert.Equal(1, confirmations);
            Assert.Contains(scene.Document, scene.Documents.Children);

            scene.Document.CanClose = false;
            Arrange(scene.Manager);
            Assert.False(close.Command!.CanExecute(close.CommandParameter));
            Assert.True(close.Visibility != Visibility.Visible || !close.IsEnabled);
            Assert.Equal(1, confirmations);

            scene.Document.CanClose = true;
            cancel = false;
            Arrange(scene.Manager);
            Execute(close);
            Assert.Equal(2, confirmations);
            Assert.DoesNotContain(scene.Document, scene.Documents.Children);
            Assert.Contains(scene.SecondDocument, scene.Documents.Children);
        });
    }

    [Fact]
    public void FixedDocumentTab_MovesToFrontShowsPinnedGlyphAndCanBeUnpinnedFromTheContextMenu()
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(false);
            LayoutDocument document = scene.SecondDocument;
            document.IsActive = true;
            Arrange(scene.Manager);
            LayoutDocumentTabItem header = DocumentHeader(scene.Manager, document);
            Button pin = Part<Button>(header, "PART_PinButton");
            Button close = Part<Button>(header, "PART_CloseButton");
            TextBlock glyph = Part<TextBlock>(header, "PinGlyph");

            Assert.Equal(1, scene.Documents.IndexOf(document));
            Assert.False(DocumentTabPinManager.IsPinned(document));
            Assert.True(document.CanMove);
            Assert.Equal(Visibility.Visible, pin.Visibility);
            Assert.Equal(Visibility.Visible, close.Visibility);
            Assert.Equal("\uE718", glyph.Text);
            Assert.Equal(DocumentTabPinManager.GetToggleText(document), pin.ToolTip);

            Execute(pin);
            Arrange(scene.Manager);
            header = DocumentHeader(scene.Manager, document);
            pin = Part<Button>(header, "PART_PinButton");
            close = Part<Button>(header, "PART_CloseButton");
            glyph = Part<TextBlock>(header, "PinGlyph");
            Assert.Equal(0, scene.Documents.IndexOf(document));
            Assert.True(document.IsActive);
            Assert.True(DocumentTabPinManager.IsPinned(document));
            Assert.False(document.CanMove);
            Assert.Equal(Visibility.Visible, pin.Visibility);
            Assert.Equal(Visibility.Visible, close.Visibility);
            Assert.Equal("\uE841", glyph.Text);

            ContextMenu menu = Assert.IsAssignableFrom<ContextMenu>(scene.Manager.DocumentContextMenu);
            MainWindow.PrepareDocumentContextMenu(menu, document);
            MenuItem pinMenu = Assert.Single(menu.Items.OfType<MenuItem>(),
                item => item.Command == DocumentTabPinManager.ToggleCommand);
            Assert.Equal(DocumentTabPinManager.GetToggleText(document), pinMenu.Header);
            Assert.Same(document, pinMenu.CommandParameter);
            Assert.True(pinMenu.Command.CanExecute(pinMenu.CommandParameter));
            pinMenu.Command.Execute(pinMenu.CommandParameter);
            Arrange(scene.Manager);
            header = DocumentHeader(scene.Manager, document);
            pin = Part<Button>(header, "PART_PinButton");
            close = Part<Button>(header, "PART_CloseButton");
            glyph = Part<TextBlock>(header, "PinGlyph");

            Assert.Equal(0, scene.Documents.IndexOf(document));
            Assert.False(DocumentTabPinManager.IsPinned(document));
            Assert.True(document.CanMove);
            Assert.Equal(Visibility.Visible, close.Visibility);
            Assert.Equal("\uE718", glyph.Text);
            MainWindow.PrepareDocumentContextMenu(menu, document);
            Assert.Equal(DocumentTabPinManager.GetToggleText(document), pinMenu.Header);
            Assert.Single(menu.Items.OfType<MenuItem>(), item => item.Command == DocumentTabPinManager.ToggleCommand);

            document.CanMove = false;
            Execute(pin);
            Execute(pin);
            Assert.False(document.CanMove);
            Assert.False(DocumentTabPinManager.IsPinned(document));
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Fact]
    public void FixedDocumentTabs_FormAStablePrefixAndUnpinMovesBehindIt()
    {
        WpfTestHost.Invoke(() =>
        {
            var pane = new LayoutDocumentPane();
            var first = new LayoutDocument { Title = "First" };
            var second = new LayoutDocument { Title = "Second" };
            var third = new LayoutDocument { Title = "Third" };
            pane.Children.Add(first);
            pane.Children.Add(second);
            pane.Children.Add(third);

            DocumentTabPinManager.ToggleCommand.Execute(third);
            Assert.Equal(new[] { third, first, second }, pane.Children.OfType<LayoutDocument>());

            DocumentTabPinManager.ToggleCommand.Execute(second);
            Assert.Equal(new[] { third, second, first }, pane.Children.OfType<LayoutDocument>());

            DocumentTabPinManager.ToggleCommand.Execute(third);
            Assert.Equal(new[] { second, third, first }, pane.Children.OfType<LayoutDocument>());
            Assert.True(third.CanMove);
            Assert.False(second.CanMove);
        });
    }

    [Fact]
    public void ToolCaptionCommands_PreserveAutoHideAndCloseVersusHide()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            scene.Tool.IsActive = true;
            Arrange(scene.Manager);
            Button pin = Part<Button>(scene.ToolTitle, "PART_AutoHidePin");
            ICommand pinCommand = Assert.IsAssignableFrom<ICommand>(pin.Command);
            Assert.True(pinCommand.CanExecute(pin.CommandParameter));
            pinCommand.Execute(pin.CommandParameter);
            Assert.True(scene.Tool.IsAutoHidden);
            pinCommand.Execute(pin.CommandParameter);
            Assert.False(scene.Tool.IsAutoHidden);
            scene.Tool.IsActive = true;
            scene.Tool.CanClose = false;
            Arrange(scene.Manager);
            Execute(Part<Button>(scene.ToolTitle, "PART_HidePin"));
            Assert.True(scene.Tool.IsHidden);
            Assert.Contains(scene.Tool, scene.Manager.Layout.Hidden);
            Assert.Contains(scene.SecondTool, scene.Tools.Children);
        });

        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            scene.Tool.IsActive = true;
            Arrange(scene.Manager);
            int confirmations = 0;
            scene.Tool.Closing += (_, _) => confirmations++;
            Execute(Part<Button>(scene.ToolTitle, "PART_HidePin"));
            Assert.Equal(1, confirmations);
            Assert.DoesNotContain(scene.Tool, scene.Tools.Children);
            Assert.DoesNotContain(scene.Tool, scene.Manager.Layout.Hidden);
        });
    }

    [Fact]
    public void NarrowWorkspace_LongCaptionsLeaveTitleActionsAndDocumentOverflowAccessible()
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(false);
            scene.Tool.Title = "Solution Explorer — " + new string('W', 120);
            scene.Document.Title = "SV6100_Algorithm111_" + new string('W', 120);
            scene.Document.IsActive = true;
            Arrange(scene.Manager, 800, 540);
            AnchorablePaneTitle title = scene.ToolTitle;
            Border caption = Part<Border>(title, "CaptionBorder");
            AssertInside(Part<DropDownButton>(title, "MenuDropDownButton"), caption);
            AssertInside(Part<Button>(title, "PART_AutoHidePin"), caption);
            AssertInside(Part<Button>(title, "PART_HidePin"), caption);
            LayoutDocumentPaneControl documents = scene.DocumentPaneControl;
            AssertInside(Part<DropDownButton>(documents, "MenuDropDownButton"), documents);
            TabItem tab = DocumentTab(documents, scene.Document);
            Assert.True(tab.ActualWidth > 0 && tab.ActualWidth <= documents.ActualWidth);
            AssertInside(Part<Button>(DocumentHeader(scene.Manager, scene.Document), "PART_PinButton"), documents);
            AssertInside(Part<Button>(DocumentHeader(scene.Manager, scene.Document), "PART_CloseButton"), documents);
        });
    }

    [Fact]
    public void FloatingCaption_RetainsWindowControls()
    {
        WpfTestHost.Invoke(() =>
        {
            var manager = new DockingManager { Theme = new AvalonDockTheme(false) };
            ResourceDictionary globalPalette = LoadGlobalPalette(false);
            manager.Resources.MergedDictionaries.Add(globalPalette);
            var model = new LayoutAnchorable { Title = "Floating sample", Content = new Border() };
            var floatingModel = new LayoutAnchorableFloatingWindow
            {
                RootPanel = new LayoutAnchorablePaneGroup(new LayoutAnchorablePane(model))
            };
            manager.Layout.FloatingWindows.Add(floatingModel);
            // Float() shows a window. This exercises independent theme loading without displaying UI.
            var window = (LayoutAnchorableFloatingWindowControl)Activator.CreateInstance(
                typeof(LayoutAnchorableFloatingWindowControl), BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: new object[] { floatingModel }, culture: null)!;
            window.Resources.MergedDictionaries.Add(globalPalette);
            try
            {
                foreach (bool active in new[] { false, true, false })
                {
                    model.IsActive = active;
                    Arrange(window, 600, 400);
                    foreach (string name in new[] { "PART_PinClose", "PART_PinMaximize", "PART_PinRestore" })
                        Assert.NotNull(Part<Button>(window, name).Command);
                    Part<DropDownButton>(window, "SinglePaneContextMenu");
                }
            }
            finally { window.Close(); }
        });
    }

    private static void AssertRightClickOpensMenu(DockingScene scene, UIElement hit, ContextMenu menu, LayoutItem expectedItem)
    {
        Assert.NotNull(menu);
        Assert.NotNull(expectedItem);
        LayoutContent[] documents = scene.Documents.Children.ToArray();
        LayoutAnchorable[] tools = scene.Tools.Children.ToArray();
        Assert.False(menu.IsOpen);
        double opacity = menu.Opacity;
        bool staysOpen = menu.StaysOpen;
        bool focusable = menu.Focusable;
        bool hitTestVisible = menu.IsHitTestVisible;
        Visibility visibility = menu.Visibility;
        IInputElement? capturedBefore = Mouse.Captured;
        bool opened = false;
        RoutedEventHandler onOpened = (_, _) => opened = true;
        menu.Opened += onOpened;
        try
        {
            // Open the actual menu and bindings without painting at the user's mouse.
            // Hidden retains layout but prevents MenuBase from taking native mouse capture.
            menu.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Hidden);
            menu.SetCurrentValue(UIElement.OpacityProperty, 0d);
            menu.SetCurrentValue(ContextMenu.StaysOpenProperty, true);
            menu.SetCurrentValue(UIElement.FocusableProperty, false);
            menu.SetCurrentValue(UIElement.IsHitTestVisibleProperty, false);
            // Explicit null forces the handler to supply LayoutItem. ClearValue would
            // instead inherit the previous PlacementTarget's raw LayoutContent model.
            menu.SetCurrentValue(FrameworkElement.DataContextProperty, null);
            var preview = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = Mouse.PreviewMouseUpEvent
            };
            // WPF cracks this tunneling event into PreviewMouseRightButtonUp; the real
            // DropDownControlArea class handler must be reached through the hit visual.
            hit.RaiseEvent(preview);
            Assert.True(preview.Handled, "The hit route never reached AvalonDock's right-click menu handler.");
            hit.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right)
            {
                RoutedEvent = Mouse.MouseUpEvent,
                Handled = preview.Handled
            });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Assert.True(opened, "The original menu must complete its WPF opening lifecycle.");
            Assert.True(menu.IsOpen);
            Assert.Same(capturedBefore, Mouse.Captured);
            Assert.False(menu.IsKeyboardFocusWithin);
            Assert.Same(expectedItem, menu.DataContext);
            Assert.Same(expectedItem.LayoutElement, Assert.IsAssignableFrom<LayoutItem>(menu.DataContext).LayoutElement);
            Assert.Equal(FontWeights.Normal, menu.FontWeight);
            MenuItem[] entries = menu.Items.OfType<MenuItem>().ToArray();
            Assert.NotEmpty(entries);
            Assert.All(entries, entry =>
            {
                Assert.NotNull(entry.Command);
                Assert.Equal(FontWeights.Normal, entry.FontWeight);
            });
            Assert.Contains(entries, entry => ReferenceEquals(entry.Command, expectedItem.FloatCommand));
            Assert.Contains(entries, entry => ReferenceEquals(entry.Command, expectedItem.DockAsDocumentCommand));
            ICommand actionCommand = expectedItem is LayoutAnchorableItem anchorable ? anchorable.AutoHideCommand : expectedItem.CloseCommand;
            MenuItem action = Assert.Single(entries, entry => ReferenceEquals(entry.Command, actionCommand));
            Assert.True(actionCommand.CanExecute(action.CommandParameter));
            Assert.True(action.IsEnabled);
            // Opening a menu must not accidentally invoke its first command (e.g. Close).
            Assert.Equal(documents, scene.Documents.Children.ToArray());
            Assert.Equal(tools, scene.Tools.Children.ToArray());
        }
        finally
        {
            menu.IsOpen = false;
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            menu.Opened -= onOpened;
            menu.SetCurrentValue(UIElement.OpacityProperty, opacity);
            menu.SetCurrentValue(ContextMenu.StaysOpenProperty, staysOpen);
            menu.SetCurrentValue(UIElement.FocusableProperty, focusable);
            menu.SetCurrentValue(UIElement.IsHitTestVisibleProperty, hitTestVisible);
            menu.SetCurrentValue(UIElement.VisibilityProperty, visibility);
        }
    }

    private static IEnumerable<DependencyObject> VisualAncestorsAndSelf(DependencyObject element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            yield return current;
    }

    private static ResourceDictionary LoadGlobalPalette(bool isDark)
        => new() { Source = new Uri($"/ColorVision.Themes;component/Themes/{(isDark ? "Dark" : "White")}.xaml", UriKind.Relative) };

    private static void AssertInside(FrameworkElement child, FrameworkElement parent)
    {
        Assert.Equal(Visibility.Visible, child.Visibility);
        Assert.True(child.ActualWidth > 0 && child.ActualHeight > 0);
        Point origin = child.TranslatePoint(new Point(), parent);
        Assert.True(origin.X >= -1 && origin.Y >= -1 && origin.X + child.ActualWidth <= parent.ActualWidth + 1
            && origin.Y + child.ActualHeight <= parent.ActualHeight + 1, $"{child.Name} is outside the available pane bounds.");
    }

    private static void Execute(Button button)
    {
        ICommand command = Assert.IsAssignableFrom<ICommand>(button.Command);
        Assert.True(command.CanExecute(button.CommandParameter));
        command.Execute(button.CommandParameter);
    }

    private static T Part<T>(Control control, string name) where T : DependencyObject
        => Assert.IsAssignableFrom<T>(control.Template.FindName(name, control));

    private static TabItem DocumentTab(LayoutDocumentPaneControl pane, LayoutDocument model)
        => Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(model));

    private static LayoutDocumentTabItem DocumentHeader(DependencyObject root, LayoutDocument model)
        => Assert.Single(Descendants<LayoutDocumentTabItem>(root), header => ReferenceEquals(header.Model, model));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (T descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Arrange(FrameworkElement element, double width = 1280, double height = 640)
    {
        if (element is DockingManager && Window.GetWindow(element) is Window host)
        {
            host.Width = width;
            host.Height = height;
            host.UpdateLayout();
        }
        for (int pass = 0; pass < 2; pass++)
        {
            element.ApplyTemplate();
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
            CommandManager.InvalidateRequerySuggested();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }

    private static void ArrangeCaption(FrameworkElement element)
    {
        element.ApplyTemplate();
        element.Measure(new Size(600, double.PositiveInfinity));
        element.Arrange(new Rect(0, 0, 600, element.DesiredSize.Height));
        element.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static XDocument LoadShell([CallerFilePath] string testPath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "ColorVision", "MainWindow.xaml")));

    private static XElement? DocumentHeaderTemplate(XDocument shell)
        => shell.Descendants().SingleOrDefault(element => element.Name.LocalName == "DataTemplate"
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "DocumentHeaderTemplate");

    private sealed class DockingScene : IDisposable
    {
        private readonly Window _host;
        internal ResourceDictionary GlobalPalette { get; private set; }
        internal DockingManager Manager { get; }
        internal LayoutDocumentPane Documents { get; } = new();
        internal LayoutAnchorablePane Tools { get; } = new();
        internal LayoutDocument Document { get; }
        internal LayoutDocument SecondDocument { get; }
        internal LayoutAnchorable Tool { get; }
        internal LayoutAnchorable SecondTool { get; }

        internal DockingScene(bool isDark)
        {
            Tool = new LayoutAnchorable { Title = "Solution Explorer", CanClose = true, CanHide = true, CanAutoHide = true,
                Content = Content("GlobalBackground", "' Default '", "  ▾  Engine", "      ColorVision.Engine", "      FlowEngineLib", "  ▾  UI", "      ColorVision.ImageEditor", "  ▾  Projects", "      ProjectLUX", "  ▾  examples", "      inspection.cvflow", "      calibration.json") };
            SecondTool = new LayoutAnchorable { Title = "Device Control", Content = Content("GlobalBackground", "No device is connected in this isolated preview.") };
            Tools.Children.Add(Tool);
            Tools.Children.Add(SecondTool);
            Document = new LayoutDocument { Title = "Workflow", Content = Content("DockingSurfaceBackground", "Workflow", "", "This preview uses the real AvalonDock theme and synthetic content.", "", "Start  →  Local image  →  Luminous area  →  End") };
            SecondDocument = new LayoutDocument { Title = "SV6100_Camera", Content = Content("DockingSurfaceBackground", "Camera settings", "Synthetic preview — no device or service is started.") };
            Documents.Children.Add(Document);
            Documents.Children.Add(SecondDocument);
            Documents.Children.Add(new LayoutDocument { Title = "SV6100_Algorithm111", Content = Content("DockingSurfaceBackground", "Algorithm settings") });
            Documents.Children.Add(new LayoutDocument { Title = "SV6100_Calibration", Content = Content("DockingSurfaceBackground", "Calibration") });
            var rootPanel = new LayoutPanel { Orientation = Orientation.Horizontal };
            rootPanel.Children.Add(new LayoutAnchorablePaneGroup(Tools) { DockWidth = new GridLength(280) });
            rootPanel.Children.Add(Documents);
            Manager = new DockingManager
            {
                Theme = new AvalonDockTheme(isDark),
                Layout = new LayoutRoot { RootPanel = rootPanel }
            };
            GlobalPalette = LoadGlobalPalette(isDark);
            Manager.Resources.MergedDictionaries.Add(GlobalPalette);
            // Reuse actual shell caption markup without constructing the production window.
            XElement? sourceTemplate = DocumentHeaderTemplate(LoadShell());
            if (sourceTemplate != null)
            {
                var template = new XElement(sourceTemplate);
                template.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Remove();
                Manager.DocumentHeaderTemplate = (DataTemplate)XamlReader.Parse(template.ToString());
            }
            Tool.IsSelected = true;
            Document.IsActive = true;
            // AvalonDock creates LayoutRootPanel in Loaded, not OnApplyTemplate.
            // Use the real WPF lifecycle in an invisible, nonactivating synthetic host.
            _host = new Window
            {
                Content = Manager, Width = 1280, Height = 640, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Opacity = 0
            };
            try
            {
                _host.Show();
                Arrange(Manager);
                AssertRenderedLayout();
                Assert.False(_host.IsActive);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal LayoutDocumentPaneControl DocumentPaneControl
            => Assert.Single(Descendants<LayoutDocumentPaneControl>(Manager), pane => ReferenceEquals(pane.Model, Documents));

        internal LayoutAnchorablePaneControl ToolPaneControl
            => Assert.Single(Descendants<LayoutAnchorablePaneControl>(Manager), pane => ReferenceEquals(pane.Model, Tools));

        internal AnchorablePaneTitle ToolTitle
            => Assert.Single(Descendants<AnchorablePaneTitle>(Manager), title => ReferenceEquals(title.Model, Tool));

        internal void ReplaceGlobalPalette(bool isDark)
        {
            Manager.Resources.MergedDictionaries.Remove(GlobalPalette);
            GlobalPalette = LoadGlobalPalette(isDark);
            Manager.Resources.MergedDictionaries.Add(GlobalPalette);
        }

        internal void AssertRenderedLayout()
        {
            Assert.True(Manager.IsLoaded, "The synthetic manager must complete its real Loaded lifecycle.");
            Assert.NotNull(Manager.LayoutRootPanel);
            Assert.True(DocumentPaneControl.ActualWidth > 0 && DocumentPaneControl.ActualHeight > 0);
            Assert.True(ToolPaneControl.ActualWidth > 0 && ToolPaneControl.ActualHeight > 0);
            AnchorablePaneTitle selectedTitle = Assert.Single(Descendants<AnchorablePaneTitle>(Manager), title => ReferenceEquals(title.Model, Tools.SelectedContent));
            Part<Border>(selectedTitle, "CaptionBorder");
            Part<Border>(DocumentTab(DocumentPaneControl, Document), "DocumentTabBorder");
        }

        public void Dispose()
        {
            _host.Content = null;
            _host.Close();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Manager.Layout = new LayoutRoot();
        }

        private static Border Content(string backgroundKey, params string[] lines)
        {
            var stack = new StackPanel { Margin = new Thickness(12) };
            foreach (string line in lines)
            {
                var text = new TextBlock { Text = line, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
                text.SetResourceReference(TextBlock.ForegroundProperty, "DockingTextBrush");
                stack.Children.Add(text);
            }
            var border = new Border { Child = stack };
            border.SetResourceReference(Border.BackgroundProperty, backgroundKey);
            return border;
        }
    }

    private sealed class BindingTrace : IDisposable
    {
        private readonly StringWriter _writer = new();
        private readonly TextWriterTraceListener _listener;
        private readonly SourceLevels _previousLevel;

        public BindingTrace()
        {
            _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.Refresh();
            _listener = new TextWriterTraceListener(_writer);
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        }

        public string Output => _writer.ToString();

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
            _listener.Dispose();
            _writer.Dispose();
        }
    }
}
