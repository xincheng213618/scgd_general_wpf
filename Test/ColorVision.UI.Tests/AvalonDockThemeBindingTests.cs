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
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

/// <summary>Real AvalonDock controls with synthetic content; never starts MainWindow or loads a saved workspace.</summary>
public class AvalonDockThemeBindingTests
{
    private readonly ITestOutputHelper _output;

    public AvalonDockThemeBindingTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DockedCaption_IsFlatAndRetainsActionsAcrossEmptyModelAndThemeReplacement(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            var model = new LayoutAnchorable { Title = "Solution Explorer" };
            var title = new AnchorablePaneTitle { Model = model };
            foreach (bool dark in new[] { isDark, !isDark, isDark })
            {
                var theme = new AvalonDockTheme(dark);
                ResourceDictionary globalPalette = LoadGlobalPalette(dark);
                title.Resources.MergedDictionaries.Clear();
                title.Resources.MergedDictionaries.Add(globalPalette);
                title.Resources.MergedDictionaries.Add(theme.ThemeResourceDictionary);
                title.Style = (Style)theme.ThemeResourceDictionary[typeof(AnchorablePaneTitle)];
                AssertPalette(theme.ThemeResourceDictionary);
                foreach (bool active in new[] { false, true, false })
                {
                    model.IsActive = active;
                    ArrangeCaption(title);
                    Assert.InRange(Part<Border>(title, "CaptionBorder").ActualHeight, 28, 30);
                    Assert.Same(globalPalette["GlobalBackground"], Part<Border>(title, "CaptionBorder").Background);
                    AssertNoVisibleGrip(title);
                    Part<DropDownButton>(title, "MenuDropDownButton");
                    Part<Button>(title, "PART_AutoHidePin");
                    Part<Button>(title, "PART_HidePin");
                }
                title.Model = null;
                ArrangeCaption(title);
                title.Model = model;
            }
            Assert.DoesNotContain("GeometryDrawing", trace.Output);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThemeManagerStyle_InheritsRealCommandMenusAcrossThemeReplacement(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            var stockResources = new ResourceDictionary
            {
                Source = new Uri("/AvalonDock.Themes.VS2013;component/Themes/Generic.xaml", UriKind.Relative)
            };
            _output.WriteLine(DescribeStyleChain("Stock Generic.xaml", Assert.IsType<Style>(stockResources[typeof(DockingManager)])));
            using var scene = new DockingScene(isDark);
            foreach (bool dark in new[] { isDark, !isDark, isDark })
            {
                var theme = new AvalonDockTheme(dark);
                Style style = Assert.IsType<Style>(theme.ThemeResourceDictionary[typeof(DockingManager)]);
                scene.ReplaceGlobalPalette(dark);
                scene.Manager.Theme = null;
                scene.Manager.Theme = theme;
                Arrange(scene.Manager);
                _output.WriteLine(DescribeStyleChain($"Modern {(dark ? "dark" : "light")} theme", style));
                _output.WriteLine(DescribeStyleChain("Actual manager Style", scene.Manager.Style));
                _output.WriteLine($"Actual menus: Document={scene.Manager.DocumentContextMenu?.GetType().Name ?? "<null>"}; Anchorable={scene.Manager.AnchorableContextMenu?.GetType().Name ?? "<null>"}");
                Assert.NotNull(style.BasedOn);
                foreach (DependencyProperty property in new[] { DockingManager.DocumentContextMenuProperty, DockingManager.AnchorableContextMenuProperty })
                {
                    Setter? setter = StyleChain(style).Select(current => current.Setters.OfType<Setter>()
                        .LastOrDefault(candidate => candidate.Property == property)).FirstOrDefault(candidate => candidate != null);
                    Assert.True(setter != null, $"The modern style inheritance chain lost the upstream {property.Name} setter.");
                    Assert.NotNull(setter);
                    ContextMenu menu = Assert.IsAssignableFrom<ContextMenu>(setter.Value);
                    Assert.NotNull(scene.Manager.GetValue(property));
                    Assert.Same(menu, scene.Manager.GetValue(property));
                }
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealManager_UsesModernPaneFramesAndSwitchesThemesWithoutReplacingContent(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(isDark);
            LayoutRoot originalLayout = scene.Manager.Layout;
            object originalDocumentContent = scene.Document.Content;
            object originalToolContent = scene.Tool.Content;
            foreach (bool dark in new[] { isDark, !isDark, isDark })
            {
                scene.Document.IsActive = true;
                var theme = new AvalonDockTheme(dark);
                // Use the production replacement path on the same manager, model and content.
                scene.ReplaceGlobalPalette(dark);
                scene.Manager.Theme = null;
                scene.Manager.Theme = theme;
                Arrange(scene.Manager);
                ResourceDictionary resources = theme.ThemeResourceDictionary;
                AssertPalette(resources);
                Assert.Same(originalLayout, scene.Manager.Layout);
                Assert.Same(originalDocumentContent, scene.Document.Content);
                Assert.Same(originalToolContent, scene.Tool.Content);
                Assert.True(scene.Document.IsSelected);
                LayoutDocumentPaneControl documents = scene.DocumentPaneControl;
                Part<Border>(documents, "DocumentPaneBorder");
                Brush globalBackground = Assert.IsAssignableFrom<Brush>(scene.GlobalPalette["GlobalBackground"]);
                Assert.Same(globalBackground, scene.Manager.Background);
                Assert.Same(globalBackground, Part<Grid>(documents, "TabStrip").Background);
                Border toolFrame = Part<Border>(scene.ToolPaneControl, "ToolPaneBorder");
                Assert.Same(globalBackground, toolFrame.Background);
                Assert.Same(globalBackground, Assert.IsType<Grid>(VisualTreeHelper.GetParent(toolFrame)).Background);
                Assert.Same(globalBackground, Part<Border>(scene.ToolTitle, "CaptionBorder").Background);
                TabItem selectedToolTab = Assert.IsType<TabItem>(scene.ToolPaneControl.ItemContainerGenerator.ContainerFromItem(scene.Tool));
                Assert.Same(globalBackground, Part<Border>(selectedToolTab, "ToolTabBorder").Background);
                Assert.Same(globalBackground, Assert.IsType<Border>(scene.Tool.Content).Background);
                AssertNoVisibleGrip(scene.ToolTitle);

                TabItem selectedTab = DocumentTab(documents, scene.Document);
                Border selectedBorder = Part<Border>(selectedTab, "DocumentTabBorder");
                Assert.Equal(new CornerRadius(3), selectedBorder.CornerRadius);
                Assert.Equal(FontWeights.SemiBold, selectedTab.FontWeight);
                Assert.Same(resources["DockingAccentBrush"], selectedBorder.BorderBrush);
                Assert.Same(resources["DockingSurfaceBackground"], selectedBorder.Background);
                Assert.Same(resources["DockingSurfaceBackground"], Assert.IsType<Border>(scene.Document.Content).Background);
                Assert.Same(resources["DockingTextBrush"], selectedTab.Foreground);
                TextBlock caption = Assert.Single(Descendants<TextBlock>(selectedTab), text => text.Text == scene.Document.Title);
                Assert.Same(selectedTab.Foreground, caption.Foreground);

                scene.Tool.IsActive = true;
                Arrange(scene.Manager);
                Assert.True(scene.Tool.IsActive);
                Assert.True(scene.Document.IsSelected);
                Assert.False(scene.Document.IsActive);
                Assert.True(scene.Document.IsLastFocusedDocument);
                Assert.Equal(FontWeights.SemiBold, selectedTab.FontWeight);
                Assert.Same(resources["DockingAccentBrush"], Part<Border>(selectedTab, "DocumentTabBorder").BorderBrush);
                Assert.Same(resources["DockingAccentBrush"], Part<Border>(documents, "TabStripLine").BorderBrush);
                Assert.Same(resources["DockingAccentBrush"], Part<Border>(documents, "DocumentPaneBorder").BorderBrush);

                scene.SecondDocument.IsActive = true;
                Arrange(scene.Manager);
                Assert.False(selectedTab.IsSelected);
                Assert.False(scene.Document.IsLastFocusedDocument);
                Assert.Equal(FontWeights.Normal, selectedTab.FontWeight);
                Assert.NotSame(resources["DockingAccentBrush"], Part<Border>(selectedTab, "DocumentTabBorder").BorderBrush);
                TabItem otherTab = DocumentTab(documents, scene.SecondDocument);
                Assert.True(otherTab.IsSelected);
                Assert.Equal(FontWeights.SemiBold, otherTab.FontWeight);
                Assert.Same(resources["DockingAccentBrush"], Part<Border>(otherTab, "DocumentTabBorder").BorderBrush);
            }
            Assert.DoesNotContain("GeometryDrawing", trace.Output);
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitiallySingleTool_GeneratesSelectedContentAndMaterializesOnceAcrossTabCountAndThemeChanges(bool isDark)
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
            var manager = new DockingManager { Theme = new AvalonDockTheme(isDark), Layout = new LayoutRoot { RootPanel = rootPanel } };
            ResourceDictionary globalPalette = LoadGlobalPalette(isDark);
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

                foreach (bool dark in new[] { !isDark, isDark })
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactoryTool_ClosedAndImmediatelyReopened_RetainsVisiblePayloadInTheSameHost(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            var rootPanel = new LayoutPanel();
            rootPanel.Children.Add(new LayoutDocumentPane(new LayoutDocument { Title = "Synthetic document", Content = new Border() }));
            var manager = new DockingManager { Theme = new AvalonDockTheme(isDark), Layout = new LayoutRoot { RootPanel = rootPanel } };
            manager.Resources.MergedDictionaries.Add(LoadGlobalPalette(isDark));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleDocumentGroups_EmphasizeOnlyTheLastFocusedGroupWithoutStealingToolActivation(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
            var otherDocument = new LayoutDocument { Title = "Second document group", Content = new Border() };
            var otherGroup = new LayoutDocumentPane(otherDocument);
            scene.Manager.Layout.RootPanel.Children.Add(otherGroup);
            Arrange(scene.Manager);
            ResourceDictionary resources = ((AvalonDockTheme)scene.Manager.Theme).ThemeResourceDictionary;
            foreach (bool focusOtherGroup in new[] { false, true, false })
            {
                (focusOtherGroup ? otherDocument : scene.Document).IsActive = true;
                Arrange(scene.Manager);
                scene.Tool.IsActive = true;
                Arrange(scene.Manager);
                Assert.True(scene.Tool.IsActive);
                Assert.False(scene.Document.IsActive);
                Assert.False(otherDocument.IsActive);
                Assert.Equal(!focusOtherGroup, scene.Document.IsLastFocusedDocument);
                Assert.Equal(focusOtherGroup, otherDocument.IsLastFocusedDocument);
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
                    Assert.Equal(FontWeights.SemiBold, tab.FontWeight);
                    object stroke = resources[document.IsLastFocusedDocument ? "DockingAccentBrush" : "DockingBorderBrush"];
                    Assert.Same(stroke, Part<Border>(tab, "DocumentTabBorder").BorderBrush);
                    Assert.Same(stroke, Part<Border>(pane, "TabStripLine").BorderBrush);
                    Assert.Same(stroke, Part<Border>(pane, "DocumentPaneBorder").BorderBrush);
                }
            }
        });
    }

    [Theory]
    [InlineData(false, "tool-title")]
    [InlineData(true, "tool-title")]
    [InlineData(false, "document-tab")]
    [InlineData(true, "document-tab")]
    [InlineData(false, "tool-tab")]
    [InlineData(true, "tool-tab")]
    public void HeaderRightClick_OnTextAndPadding_OpensTheActualLayoutItemsCommands(bool isDark, string surface)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(isDark);
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
            Assert.DoesNotContain("GeometryDrawing", trace.Output);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DocumentCloseButton_HonorsCancellationAndCanClose(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToolCaptionCommands_PreserveAutoHideAndCloseVersusHide(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
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
            using var scene = new DockingScene(isDark);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NarrowWorkspace_LongCaptionsLeaveTitleActionsAndDocumentOverflowAccessible(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
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
            AssertInside(Part<Button>(DocumentHeader(scene.Manager, scene.Document), "PART_CloseButton"), documents);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FloatingCaption_LoadsTheSameThemeWithoutDotsAndRetainsWindowControls(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            var manager = new DockingManager { Theme = new AvalonDockTheme(isDark) };
            ResourceDictionary globalPalette = LoadGlobalPalette(isDark);
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
                    Assert.Same(globalPalette["GlobalBackground"], window.Background);
                    Assert.Same(globalPalette["GlobalBackground"], Part<Border>(window, "Header").Background);
                    Assert.Same(globalPalette["GlobalBackground"], Part<Border>(window, "WindowBorderForResize").Background);
                    AssertNoVisibleGrip(window);
                    foreach (string name in new[] { "PART_PinClose", "PART_PinMaximize", "PART_PinRestore" })
                        Assert.NotNull(Part<Button>(window, name).Command);
                    Part<DropDownButton>(window, "SinglePaneContextMenu");
                }
                Assert.DoesNotContain("GeometryDrawing", trace.Output);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void MainWindow_PreservesDocumentCaptionForeground()
    {
        XDocument document = LoadShell();
        XElement? header = DocumentHeaderTemplate(document);
        if (header != null)
            Assert.DoesNotContain(header.DescendantsAndSelf().Attributes(), attribute => attribute.Name.LocalName.EndsWith("Foreground", StringComparison.Ordinal)
                && attribute.Value.Contains("GlobalTextBrush", StringComparison.Ordinal));
    }

    [Fact]
    public void DeviceScrollViewer_UsesTheSameGlobalBackgroundAsToolChrome()
    {
        XElement viewer = Assert.Single(LoadShell().Descendants(), element => element.Name.LocalName == "ScrollViewer"
            && element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ScrollViewerDisplay");
        Assert.Equal("{DynamicResource GlobalBackground}", viewer.Attribute("Background")?.Value);
    }

    [Theory]
    [InlineData(96d)]
    [InlineData(144d)]
    public void RoundedSurface_ActuallyClipsSquareChildPixelsAndUpdatesAfterRadiusAndSizeChanges(double dpi)
    {
        WpfTestHost.Invoke(() =>
        {
            var surface = new DockingSurfaceBorder
            {
                Width = 80, Height = 60, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(2),
                Background = Brushes.White, BorderBrush = Brushes.Black, Child = new Border { Background = Brushes.Magenta }
            };
            var canvas = new Canvas { Background = Brushes.Lime };
            Canvas.SetLeft(surface, 16);
            Canvas.SetTop(surface, 16);
            canvas.Children.Add(surface);
            Arrange(canvas, 112, 92);
            AssertSurfacePixels(RenderVisual(canvas, dpi), surface, true);

            surface.CornerRadius = new CornerRadius();
            Arrange(canvas, 112, 92);
            AssertSurfacePixels(RenderVisual(canvas, dpi), surface, false);

            surface.CornerRadius = new CornerRadius(12);
            surface.Width = 106;
            surface.Height = 74;
            Arrange(canvas, 138, 106);
            AssertSurfacePixels(RenderVisual(canvas, dpi), surface, true);
        });
    }

    [Theory]
    [InlineData(Dock.Top)]
    [InlineData(Dock.Bottom)]
    public void SelectedTab_RendersOutwardConcaveShouldersAndRoundedFarCorners(Dock placement)
    {
        WpfTestHost.Invoke(() =>
        {
            var tab = new DockingTabBorder
            {
                Width = 96, Height = 28, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
                Background = Brushes.White, BorderBrush = Brushes.Black, IsSelected = true, Placement = placement
            };
            var canvas = new Canvas { Background = Brushes.Magenta };
            Canvas.SetLeft(tab, 16);
            Canvas.SetTop(tab, 16);
            canvas.Children.Add(tab);
            Arrange(canvas, 128, 60);
            RenderTargetBitmap bitmap = RenderVisual(canvas, 384);
            double joinY = placement == Dock.Bottom ? 0.5 : tab.ActualHeight - 0.5;
            double bodyY = placement == Dock.Bottom ? 6 : tab.ActualHeight - 6;
            double farY = placement == Dock.Bottom ? tab.ActualHeight - 0.5 : 0.5;
            // The shape extends outside its own layout slot only at the joining shoulders.
            // A normal rounded rectangle, or an underline, cannot satisfy these pixel samples.
            AssertPixel(bitmap, new Point(16 - 0.5, 16 + joinY), Colors.White);
            AssertPixel(bitmap, new Point(16 + tab.ActualWidth + 0.5, 16 + joinY), Colors.White);
            AssertPixel(bitmap, new Point(16 - 0.5, 16 + bodyY), Colors.Magenta);
            AssertPixel(bitmap, new Point(16 + tab.ActualWidth + 0.5, 16 + bodyY), Colors.Magenta);
            AssertPixel(bitmap, new Point(16 + 0.5, 16 + farY), Colors.Magenta);
            AssertPixel(bitmap, new Point(16 + tab.ActualWidth - 0.5, 16 + farY), Colors.Magenta);
            AssertPixel(bitmap, new Point(64, 30), Colors.White);

            tab.IsSelected = false;
            Arrange(canvas, 128, 60);
            bitmap = RenderVisual(canvas, 384);
            AssertPixel(bitmap, new Point(16 - 0.5, 16 + joinY), Colors.Magenta);
            AssertPixel(bitmap, new Point(16 + tab.ActualWidth + 0.5, 16 + joinY), Colors.Magenta);
        });
    }

    [Theory]
    [InlineData(Dock.Top, false, 96d)]
    [InlineData(Dock.Top, false, 144d)]
    [InlineData(Dock.Top, true, 96d)]
    [InlineData(Dock.Top, true, 144d)]
    [InlineData(Dock.Bottom, false, 96d)]
    [InlineData(Dock.Bottom, false, 144d)]
    [InlineData(Dock.Bottom, true, 96d)]
    [InlineData(Dock.Bottom, true, 144d)]
    public void UnselectedHover_KeepsPaneJoinLineVisibleWhenSelectionChanges(Dock placement, bool isDark, double renderDpi)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
            LayoutContent first = placement == Dock.Top ? scene.Document : scene.Tool;
            LayoutContent second = placement == Dock.Top ? scene.SecondDocument : scene.SecondTool;
            ResourceDictionary resources = ((AvalonDockTheme)scene.Manager.Theme).ThemeResourceDictionary;
            Brush hoverBrush = Assert.IsAssignableFrom<Brush>(resources["DockingHoverBrush"]);
            foreach (bool selectSecond in new[] { false, true, false })
            {
                LayoutContent selectedModel = selectSecond ? second : first;
                LayoutContent hoveredModel = selectSecond ? first : second;
                selectedModel.IsActive = true;
                Arrange(scene.Manager);
                TabControl pane = placement == Dock.Top ? scene.DocumentPaneControl : scene.ToolPaneControl;
                string chromeName = placement == Dock.Top ? "DocumentTabBorder" : "ToolTabBorder";
                Border line = Part<Border>(pane, placement == Dock.Top ? "TabStripLine" : "ToolTabStripLine");
                TabItem selectedTab = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(selectedModel));
                TabItem hoveredTab = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(hoveredModel));
                DockingTabBorder selected = Part<DockingTabBorder>(selectedTab, chromeName);
                DockingTabBorder hovered = Part<DockingTabBorder>(hoveredTab, chromeName);
                Assert.True(selected.IsSelected);
                Assert.False(hovered.IsSelected);
                Brush originalBackground = hovered.Background;
                Size originalSize = hovered.RenderSize;
                RenderTargetBitmap baseline = RenderVisual(scene.Manager, renderDpi);
                Point lineOrigin = line.TranslatePoint(new Point(), scene.Manager);
                Point hoverOrigin = hovered.TranslatePoint(new Point(), scene.Manager);
                double joinEdge = lineOrigin.Y + (placement == Dock.Top ? line.ActualHeight : 0);
                Color stroke = BrushColor(line.BorderBrush);
                // Take the real pane's already-rendered stroke pixels, avoiding assumptions
                // about fractional device-pixel placement at either output DPI.
                Point[] strokePixels = PixelsWithColor(baseline,
                    new Rect(hoverOrigin.X + 8, joinEdge - 2, hovered.ActualWidth - 16, 4), stroke).ToArray();
                Assert.NotEmpty(strokePixels);
                try
                {
                    // Exercise the actual chrome renderer using its production hover brush;
                    // do not inject global mouse input or replace the real pane/template.
                    hovered.SetCurrentValue(Border.BackgroundProperty, hoverBrush);
                    Arrange(scene.Manager);
                    Assert.Equal(originalSize, hovered.RenderSize);
                    Assert.False(hovered.IsSelected);
                    RenderTargetBitmap bitmap = RenderVisual(scene.Manager, renderDpi);
                    AssertPixel(bitmap, new Point(hoverOrigin.X + 5, hoverOrigin.Y + hovered.ActualHeight / 2), BrushColor(hoverBrush));
                    foreach (Point point in strokePixels)
                        AssertPixel(bitmap, point, PixelAt(baseline, point));

                    Point selectedOrigin = selected.TranslatePoint(new Point(), scene.Manager);
                    double selectedCenter = selectedOrigin.X + selected.ActualWidth / 2;
                    foreach (double y in strokePixels.Select(point => point.Y).Distinct())
                        AssertPixel(bitmap, new Point(selectedCenter, y), BrushColor(selected.Background));
                }
                finally
                {
                    hovered.SetCurrentValue(Border.BackgroundProperty, originalBackground);
                }
            }
        });
    }

    [Fact]
    public void PaintedTabShoulder_DoesNotTakeTheNeighbouringTabsHitArea()
    {
        WpfTestHost.Invoke(() =>
        {
            var neighbour = new Border { Width = 16, Height = 28, Background = Brushes.Magenta };
            var tab = new DockingTabBorder
            {
                Width = 96, Height = 28, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1),
                Background = Brushes.White, BorderBrush = Brushes.Black, IsSelected = true, Placement = Dock.Bottom
            };
            var canvas = new Canvas { Background = Brushes.Magenta };
            Canvas.SetTop(neighbour, 16);
            Canvas.SetLeft(tab, 16);
            Canvas.SetTop(tab, 16);
            canvas.Children.Add(neighbour);
            canvas.Children.Add(tab);
            Arrange(canvas, 128, 60);
            RenderTargetBitmap bitmap = RenderVisual(canvas, 384);
            var shoulder = new Point(15.5, 16.5);
            AssertPixel(bitmap, shoulder, Colors.White);
            Assert.Same(neighbour, VisualTreeHelper.HitTest(canvas, shoulder)?.VisualHit);
            Assert.Same(tab, VisualTreeHelper.HitTest(canvas, new Point(64, 30))?.VisualHit);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedMiddleToolTab_JoinsThePaneOutlineWithoutASeamAcrossResizeAndActivation(bool isDark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var trace = new BindingTrace();
            using var scene = new DockingScene(isDark);
            SelectMiddleTool(scene);
            foreach (int width in new[] { 1280, 800, 1024 })
            {
                foreach (bool active in new[] { true, false })
                {
                    scene.SecondTool.IsActive = true;
                    if (!active) scene.Document.IsActive = true;
                    Arrange(scene.Manager, width, 640);
                    LayoutAnchorablePaneControl pane = scene.ToolPaneControl;
                    Part<DockingSurfaceBorder>(pane, "ToolPaneBorder");
                    Border stripLine = Part<Border>(pane, "ToolTabStripLine");
                    TabItem selected = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(scene.SecondTool));
                    DockingTabBorder chrome = Part<DockingTabBorder>(selected, "ToolTabBorder");
                    Assert.True(selected.IsSelected);
                    Assert.True(chrome.IsSelected);
                    Assert.Equal(Dock.Bottom, chrome.Placement);
                    Assert.Equal(active, scene.SecondTool.IsActive);
                    ResourceDictionary resources = ((AvalonDockTheme)scene.Manager.Theme).ThemeResourceDictionary;
                    Color stroke = BrushColor(resources[active ? "DockingAccentBrush" : "DockingBorderBrush"]);
                    Color surface = BrushColor(scene.GlobalPalette["GlobalBackground"]);
                    Assert.Equal(stroke, BrushColor(chrome.BorderBrush));
                    RenderTargetBitmap bitmap = RenderVisual(scene.Manager, 192);
                    Point tabOrigin = chrome.TranslatePoint(new Point(), scene.Manager);
                    Point lineOrigin = stripLine.TranslatePoint(new Point(), scene.Manager);
                    double centerX = tabOrigin.X + chrome.ActualWidth / 2;
                    // Content continues through the selected tab's open joining edge.
                    AssertPixel(bitmap, new Point(centerX, lineOrigin.Y - 1.5), surface);
                    AssertPixel(bitmap, new Point(centerX, lineOrigin.Y + 0.5), surface);
                    AssertPixel(bitmap, new Point(centerX, lineOrigin.Y + 2), surface);

                    TabItem unselected = Assert.IsType<TabItem>(pane.ItemContainerGenerator.ContainerFromItem(scene.Tool));
                    Point otherOrigin = unselected.TranslatePoint(new Point(), scene.Manager);
                    double otherCenterX = otherOrigin.X + unselected.ActualWidth / 2;
                    AssertContainsColor(bitmap, new Rect(otherCenterX - 1, lineOrigin.Y, 2, 2), stroke);
                    AssertContainsColor(bitmap, new Rect(centerX - 1, tabOrigin.Y + chrome.ActualHeight - 2, 2, 3), stroke);
                }
            }

            // Removing the left neighbour moves the selected tab to the pane edge without
            // changing its RenderSize. Its former concave shoulder must repaint as a straight join.
            LayoutAnchorablePaneControl finalPane = scene.ToolPaneControl;
            TabItem previousTab = Assert.IsType<TabItem>(finalPane.ItemContainerGenerator.ContainerFromItem(scene.SecondTool));
            Size previousSize = Part<DockingTabBorder>(previousTab, "ToolTabBorder").RenderSize;
            scene.Tools.Children.Remove(scene.Tool);
            Arrange(scene.Manager, 1024, 640);
            finalPane = scene.ToolPaneControl;
            TabItem firstTab = Assert.IsType<TabItem>(finalPane.ItemContainerGenerator.ContainerFromItem(scene.SecondTool));
            DockingTabBorder firstChrome = Part<DockingTabBorder>(firstTab, "ToolTabBorder");
            Assert.Equal(previousSize, firstChrome.RenderSize);
            Assert.True(firstChrome.IsSelected);
            Point firstOrigin = firstChrome.TranslatePoint(new Point(), scene.Manager);
            Point frameOrigin = Part<DockingSurfaceBorder>(finalPane, "ToolPaneBorder").TranslatePoint(new Point(), scene.Manager);
            Point joinOrigin = Part<Border>(finalPane, "ToolTabStripLine").TranslatePoint(new Point(), scene.Manager);
            Assert.InRange(Math.Abs(firstOrigin.X - frameOrigin.X), 0, 1);
            Color firstStroke = BrushColor(firstChrome.BorderBrush);
            RenderTargetBitmap firstBitmap = RenderVisual(scene.Manager, 192);
            AssertContainsColor(firstBitmap, new Rect(firstOrigin.X, joinOrigin.Y - 2, 2, 1), firstStroke);
            AssertContainsColor(firstBitmap, new Rect(firstOrigin.X, joinOrigin.Y + 1, 2, 1), firstStroke);
            Assert.DoesNotContain("GeometryDrawing", trace.Output);
            Assert.DoesNotContain("BindingExpression path error", trace.Output);
        });
    }

    [Theory]
    [InlineData(false, 1280)]
    [InlineData(false, 800)]
    [InlineData(true, 1280)]
    [InlineData(true, 800)]
    public void RenderSyntheticWorkspace_WhenPreviewDirectoryIsRequested(bool isDark, int width)
    {
        string? directory = Environment.GetEnvironmentVariable("COLORVISION_DOCKING_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Assert.True(Path.IsPathFullyQualified(directory));
        WpfTestHost.Invoke(() =>
        {
            using var scene = new DockingScene(isDark);
            scene.Document.IsActive = true;
            Arrange(scene.Manager, width, 640);
            scene.AssertRenderedLayout();
            SavePreview(scene, directory, $"docking-{(isDark ? "dark" : "light")}-{width}.png");
            scene.SecondDocument.IsActive = true;
            Arrange(scene.Manager, width, 640);
            DockingTabBorder hoveredDocument = Part<DockingTabBorder>(DocumentTab(scene.DocumentPaneControl, scene.Document), "DocumentTabBorder");
            Brush originalBackground = hoveredDocument.Background;
            hoveredDocument.SetCurrentValue(Border.BackgroundProperty,
                ((AvalonDockTheme)scene.Manager.Theme).ThemeResourceDictionary["DockingHoverBrush"]);
            Arrange(scene.Manager, width, 640);
            SavePreview(scene, directory, $"docking-{(isDark ? "dark" : "light")}-{width}-second-document-hover.png");
            hoveredDocument.SetCurrentValue(Border.BackgroundProperty, originalBackground);
            scene.Tool.IsActive = true;
            Arrange(scene.Manager, width, 640);
            SavePreview(scene, directory, $"docking-{(isDark ? "dark" : "light")}-{width}-tool-focused.png");
            SelectMiddleTool(scene);
            Arrange(scene.Manager, width, 640);
            scene.AssertRenderedLayout();
            SavePreview(scene, directory, $"docking-{(isDark ? "dark" : "light")}-{width}-middle-tool.png");
        });
    }

    private static void SelectMiddleTool(DockingScene scene)
    {
        scene.Tool.Title = "Explorer";
        scene.SecondTool.Title = "Devices";
        scene.Tools.Children.Add(new LayoutAnchorable { Title = "Properties", Content = new Border() });
        scene.SecondTool.IsActive = true;
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
            MenuItem[] entries = menu.Items.OfType<MenuItem>().ToArray();
            Assert.NotEmpty(entries);
            Assert.All(entries, entry => Assert.NotNull(entry.Command));
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

    private static IEnumerable<Style> StyleChain(Style? style)
    {
        var visited = new HashSet<Style>();
        for (Style? current = style; current != null && visited.Add(current); current = current.BasedOn)
            yield return current;
    }

    private static string DescribeStyleChain(string label, Style? style)
    {
        string[] entries = StyleChain(style).Select((current, index) =>
            $"  [{index}] {current.TargetType.Name}: " + string.Join(", ", current.Setters.OfType<Setter>()
                .Select(setter => $"{setter.Property.Name}={setter.Value?.GetType().Name ?? "<null>"}"))).ToArray();
        return label + Environment.NewLine + (entries.Length == 0 ? "  <null>" : string.Join(Environment.NewLine, entries));
    }

    private static void AssertSurfacePixels(RenderTargetBitmap bitmap, DockingSurfaceBorder surface, bool rounded)
    {
        Color corner = rounded ? Colors.Lime : Colors.Magenta;
        foreach (Point point in new[]
        {
            new Point(2.5, 2.5), new Point(surface.ActualWidth - 2.5, 2.5),
            new Point(2.5, surface.ActualHeight - 2.5), new Point(surface.ActualWidth - 2.5, surface.ActualHeight - 2.5)
        })
            AssertPixel(bitmap, new Point(16 + point.X, 16 + point.Y), corner);
        AssertPixel(bitmap, new Point(16 + surface.ActualWidth / 2, 16 + surface.ActualHeight / 2), Colors.Magenta);
        AssertPixel(bitmap, new Point(17, 16 + surface.ActualHeight / 2), Colors.Black);
    }

    private static RenderTargetBitmap RenderVisual(FrameworkElement element, double dpi)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi / 96),
            (int)Math.Ceiling(element.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static void SavePreview(DockingScene scene, string directory, string name)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(RenderVisual(scene.Manager, 144)));
        Directory.CreateDirectory(directory);
        using FileStream output = File.Create(Path.Combine(directory, name));
        encoder.Save(output);
    }

    private static Color BrushColor(object brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static void AssertPixel(RenderTargetBitmap bitmap, Point point, Color expected)
    {
        Color actual = PixelAt(bitmap, point);
        Assert.True(ColorsMatch(expected, actual), $"Pixel at {point}: expected {expected}, actual {actual}.");
    }

    private static void AssertContainsColor(RenderTargetBitmap bitmap, Rect bounds, Color expected)
    {
        double step = 96 / bitmap.DpiX;
        for (double y = Math.Max(0, bounds.Top); y < Math.Min(bitmap.PixelHeight * step, bounds.Bottom); y += step)
            for (double x = Math.Max(0, bounds.Left); x < Math.Min(bitmap.PixelWidth * step, bounds.Right); x += step)
                if (ColorsMatch(expected, PixelAt(bitmap, new Point(x, y)))) return;
        Assert.Fail($"No {expected} outline pixel was rendered in {bounds}.");
    }

    private static Color PixelAt(RenderTargetBitmap bitmap, Point point)
    {
        int x = (int)Math.Floor(point.X * bitmap.DpiX / 96);
        int y = (int)Math.Floor(point.Y * bitmap.DpiY / 96);
        Assert.InRange(x, 0, bitmap.PixelWidth - 1);
        Assert.InRange(y, 0, bitmap.PixelHeight - 1);
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static IEnumerable<Point> PixelsWithColor(RenderTargetBitmap bitmap, Rect bounds, Color expected)
    {
        double scale = bitmap.DpiX / 96;
        int left = Math.Max(0, (int)Math.Ceiling(bounds.Left * scale));
        int right = Math.Min(bitmap.PixelWidth, (int)Math.Floor(bounds.Right * scale));
        int top = Math.Max(0, (int)Math.Ceiling(bounds.Top * scale));
        int bottom = Math.Min(bitmap.PixelHeight, (int)Math.Floor(bounds.Bottom * scale));
        for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
            {
                var point = new Point((x + 0.5) / scale, (y + 0.5) / scale);
                if (ColorsMatch(expected, PixelAt(bitmap, point))) yield return point;
            }
    }

    private static bool ColorsMatch(Color expected, Color actual)
        => Math.Abs(expected.A - actual.A) <= 8 && Math.Abs(expected.R - actual.R) <= 8
            && Math.Abs(expected.G - actual.G) <= 8 && Math.Abs(expected.B - actual.B) <= 8;

    private static void AssertPalette(ResourceDictionary resources)
    {
        foreach (string key in new[] { "DockingChromeBackground", "DockingSurfaceBackground", "DockingBorderBrush", "DockingAccentBrush",
            "DockingTextBrush", "DockingMutedTextBrush", "DockingHoverBrush", "DockingPressedBrush" })
            Assert.IsAssignableFrom<Brush>(resources[key]);
    }

    private static void AssertNoVisibleGrip(Control control)
    {
        if (control.Template.FindName("DragHandleTexture", control) is UIElement grip)
            Assert.True(grip.Visibility != Visibility.Visible || grip.Opacity == 0, "The old dotted drag grip must not be visible.");
    }

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
