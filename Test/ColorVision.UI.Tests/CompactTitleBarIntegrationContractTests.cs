using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using ColorVision.Windowing;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

/// <summary>Checks the real main-window/configuration contracts without starting application services.</summary>
public sealed class CompactTitleBarIntegrationContractTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void FreshConfigurationKeepsTheNativeTitleBarAndTheSwitchRaisesOnlyItsOwnNotification()
    {
        var config = new MainWindowConfig();
        Assert.False(config.UseCompactTitleBar);
        List<string?> notifications = [];
        config.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        config.UseCompactTitleBar = true;

        Assert.True(config.UseCompactTitleBar);
        Assert.Equal([nameof(MainWindowConfig.UseCompactTitleBar)], notifications);
    }

    [Fact]
    public void CompactShellIsASeparateSubclassAndDirectMainWindowConstructionStaysNative()
    {
        Assert.Equal(typeof(MainWindow), typeof(CompactMainWindow).BaseType);
        Assert.True(typeof(CompactMainWindow).IsSealed);
        Assert.NotNull(typeof(MainWindow).GetConstructor(Type.EmptyTypes));
        Assert.NotNull(typeof(CompactMainWindow).GetConstructor(Type.EmptyTypes));

        string ordinarySource = ReadRepositoryText("ColorVision/MainWindow.xaml.cs");
        Assert.Contains("public MainWindow() : this(useStandardWindowAppearance: true)", ordinarySource, StringComparison.Ordinal);
        Assert.DoesNotContain("UseCompactTitleBar", ordinarySource, StringComparison.Ordinal);
        string baseConstructor = MethodBody(ordinarySource, "protected MainWindow(bool useStandardWindowAppearance)");
        Assert.Contains("if (useStandardWindowAppearance)", baseConstructor, StringComparison.Ordinal);
        Assert.Contains("this.ApplyCaption()", baseConstructor, StringComparison.Ordinal);
        Assert.Contains("this.SetWindowFull(Config)", baseConstructor, StringComparison.Ordinal);

        string compactSource = ReadRepositoryText("ColorVision/CompactMainWindow.cs");
        Assert.Contains("public CompactMainWindow() : base(useStandardWindowAppearance: false)", compactSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InitializeComponent()", compactSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new DockingManager", compactSource, StringComparison.Ordinal);
        string constructor = MethodBody(compactSource, "public CompactMainWindow()");
        Assert.True(constructor.IndexOf("PropertyChanged += CompactTitleBarConfigChanged", StringComparison.Ordinal)
            < constructor.IndexOf("this.SetWindowFull(Config)", StringComparison.Ordinal));
    }

    [Fact]
    public void StartupFactoryRoutesTheSettingWithoutChangingFeatureLaunchers()
    {
        MethodInfo factory = Assert.IsAssignableFrom<MethodInfo>(typeof(MainWindowFactory).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static));
        Assert.Equal(typeof(MainWindow), factory.ReturnType);
        Assert.Equal(typeof(bool), Assert.Single(factory.GetParameters()).ParameterType);
        string factorySource = ReadRepositoryText("ColorVision/MainWindowFactory.cs");
        Assert.Matches(@"useCompactTitleBar\s*\?\s*new CompactMainWindow\(\)\s*:\s*new MainWindow\(\)", factorySource);

        string startupBody = MethodBody(ReadRepositoryText("ColorVision/StartWindow.xaml.cs"), "private void ShowMainWindowAndClose()");
        Assert.Equal(2, Regex.Matches(startupBody, @"MainWindowFactory\.Create\(MainWindowConfig\.Instance\.UseCompactTitleBar\)").Count);
        Assert.DoesNotContain("new MainWindow()", startupBody, StringComparison.Ordinal);
        Assert.DoesNotContain("new CompactMainWindow()", startupBody, StringComparison.Ordinal);
        Assert.Contains("project1.Execute()", startupBody, StringComparison.Ordinal);
        Assert.Contains("project2.Execute()", startupBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zh-CN", "重启", "实验")]
    [InlineData("zh-Hant", "重新啟動", "實驗")]
    [InlineData("en", "restart", "experimental")]
    public void CompactSettingExplainsItsExperimentalAndRestartOnlyStatus(string cultureName, string restartText, string experimentalText)
    {
        PropertyInfo property = typeof(MainWindowConfig).GetProperty(nameof(MainWindowConfig.UseCompactTitleBar))!;
        string displayName = property.GetCustomAttribute<DisplayNameAttribute>()!.DisplayName;
        string description = property.GetCustomAttribute<DescriptionAttribute>()!.Description;
        var culture = new CultureInfo(cultureName);
        string? localizedName = global::ColorVision.Properties.Resources.ResourceManager.GetString(displayName, culture);
        string? localizedDescription = global::ColorVision.Properties.Resources.ResourceManager.GetString(description, culture);

        Assert.NotNull(localizedName);
        Assert.NotNull(localizedDescription);
        Assert.Contains(experimentalText, localizedName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(restartText, localizedName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(restartText, localizedDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindowRemainsAStandardWindowWithOneExistingWorkspace()
    {
        XDocument document = LoadMainWindow();
        XElement root = document.Root!;

        Assert.Equal(Presentation + "Window", root.Name);
        Assert.Equal("ColorVision.MainWindow", (string?)root.Attribute(Xaml + "Class"));
        Assert.NotEqual("True", (string?)root.Attribute(nameof(Window.AllowsTransparency)));
        Assert.NotEqual("None", (string?)root.Attribute(nameof(Window.WindowStyle)));
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == nameof(System.Windows.Shell.WindowChrome));
        Assert.Single(document.Descendants(), element => (string?)element.Attribute(Xaml + "Name") == "DockingManager1");
        Assert.Equal("{DynamicResource GlobalBackground}", (string?)Named(document, "DockingManager1").Attribute("Background"));
        Assert.Equal("{DynamicResource GlobalBackground}", (string?)Named(document, "StatusBarGrid").Attribute("Background"));
    }

    [Fact]
    public void OnlyInteractiveHeaderControlsOptIntoClientHitTesting()
    {
        XDocument document = LoadMainWindow();
        foreach (string name in new[] { "Menu1", "RightMenuItemPanel", "UpdateNotificationButton", "CompactActionsOverflowButton" })
            Assert.Equal("True", (string?)Named(document, name).Attribute("WindowChrome.IsHitTestVisibleInChrome"));

        foreach (string name in new[] { "Root", "TopBarGrid", "MainWindowTitleBar" })
            Assert.NotEqual("True", (string?)Named(document, name).Attribute("WindowChrome.IsHitTestVisibleInChrome"));

        XElement captionButtons = Named(document, "NativeCaptionButtonsPlaceholder");
        Assert.Equal("0", (string?)captionButtons.Attribute("Width"));
        Assert.Equal("False", (string?)captionButtons.Attribute("IsHitTestVisible"));
        Assert.Null(captionButtons.Attribute("Background"));
        Assert.Empty(captionButtons.Elements());
        Assert.Same(Named(document, "TopBarGrid"), captionButtons.Parent);
    }

    [Fact]
    public void CompactHeaderReservesARealDragRegionBeforeMeasuringInteractiveControls()
    {
        XDocument document = LoadMainWindow();
        XElement dragRegion = Named(document, "CompactDragRegion");
        Assert.Equal("120", (string?)dragRegion.Attribute("Width"));
        Assert.Equal("Collapsed", (string?)dragRegion.Attribute("Visibility"));
        Assert.NotEqual("False", (string?)dragRegion.Attribute("IsHitTestVisible"));
        Assert.Equal("Right", (string?)dragRegion.Attribute("DockPanel.Dock"));
        Assert.Equal("False", (string?)dragRegion.Attribute("WindowChrome.IsHitTestVisibleInChrome"));
        Assert.Equal("{DynamicResource GlobalBackground}", (string?)dragRegion.Attribute("Background"));
        Assert.Equal("1", (string?)dragRegion.Attribute("Panel.ZIndex"));
        XElement dock = dragRegion.Parent!;
        Assert.Equal(Presentation + "DockPanel", dock.Name);
        Assert.Equal(["CompactActionsOverflowButton", "RightMenuItemPanel", "CompactDragRegion"],
            dock.Elements().Take(3).Select(element => (string?)element.Attribute(Xaml + "Name")));
        Assert.Equal("True", (string?)dock.Attribute("ClipToBounds"));
        Assert.Equal("2", (string?)Named(document, "RightMenuItemPanel").Attribute("Panel.ZIndex"));
        Assert.Equal("{DynamicResource GlobalBackground}", (string?)Named(document, "RightMenuItemPanel").Attribute("Background"));
        XElement topBar = Named(document, "TopBarGrid");
        Assert.Equal(["*", "Auto"], topBar.Element(Presentation + "Grid.ColumnDefinitions")!.Elements()
            .Select(element => (string?)element.Attribute("Width")));
        Assert.Equal("1", (string?)Named(document, "NativeCaptionButtonsPlaceholder").Attribute("Grid.Column"));
        Assert.Equal(2, topBar.Elements().Count(element => element.Name.LocalName != "Grid.ColumnDefinitions"));

        string compactSource = ReadRepositoryText("ColorVision/CompactMainWindow.cs");
        Assert.Contains("CompactDragRegion.Visibility = Visibility.Visible", MethodBody(compactSource, "private void AttachCompactTitleBar("), StringComparison.Ordinal);
        Assert.Contains("CompactDragRegion.Visibility = Visibility.Collapsed", MethodBody(compactSource, "private void CompactTitleBarConfigChanged("), StringComparison.Ordinal);
        string overrideBody = MethodBody(compactSource, "protected override void UpdateRightMenuVisibility()");
        Assert.Contains("CompactTitleBarLayout.Update(", overrideBody, StringComparison.Ordinal);
        Assert.Contains("base.UpdateRightMenuVisibility()", overrideBody, StringComparison.Ordinal);
        string layoutBody = MethodBody(ReadRepositoryText("ColorVision/Windowing/CompactTitleBarLayout.cs"), "internal static void Update(");
        Assert.Contains("MeasureNaturalWidth(updateNotice)", layoutBody, StringComparison.Ordinal);
        Assert.Contains("hasPendingUpdate", layoutBody, StringComparison.Ordinal);
        Assert.DoesNotContain("updateNotice.Visibility == Visibility.Visible", layoutBody, StringComparison.Ordinal);
        Assert.Contains("MeasureNaturalWidth(dragRegion)", layoutBody, StringComparison.Ordinal);
    }

    [Fact]
    public void OverflowStaysOptInAndHiddenPendingUpdatesRemainAccessible()
    {
        XDocument document = LoadMainWindow();
        XElement overflow = Named(document, "CompactActionsOverflowButton");
        XElement badge = Named(document, "CompactUpdateBadge");
        Assert.Equal("Collapsed", (string?)overflow.Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)badge.Attribute("Visibility"));
        Assert.Equal("False", (string?)badge.Attribute("IsHitTestVisible"));
        Assert.Contains(overflow.Descendants(), element => ReferenceEquals(element, badge));
        Assert.Equal("3", (string?)overflow.Attribute("Panel.ZIndex"));
        Assert.Equal("{DynamicResource GlobalBackground}", (string?)overflow.Attribute("Background"));
        Assert.Null(overflow.Attribute("Click"));
        Assert.NotNull(overflow.Attribute("AutomationProperties.Name"));
        XElement actionStyle = document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Key") == "MainWindowActionButtonStyle");
        XElement focusBorder = actionStyle.Descendants().Single(element => element.Name.LocalName == "Trigger" && (string?)element.Attribute("Property") == "IsKeyboardFocused")
            .Elements().Single(element => (string?)element.Attribute("Property") == "BorderBrush");
        Assert.Equal("{DynamicResource PrimaryBrush}", (string?)focusBorder.Attribute("Value"));

        List<XElement> actionTriggers = actionStyle.Descendants(Presentation + "ControlTemplate.Triggers").Single().Elements().ToList();
        XElement inactiveTrigger = actionTriggers.Single(element => element.Name.LocalName == "DataTrigger");
        Assert.Contains("IsActive", (string?)inactiveTrigger.Attribute("Binding"), StringComparison.Ordinal);
        Assert.Contains("Window", (string?)inactiveTrigger.Attribute("Binding"), StringComparison.Ordinal);
        Assert.Equal("False", (string?)inactiveTrigger.Attribute("Value"));
        Assert.Equal("{DynamicResource TitleBarActionInactiveForeground}", (string?)inactiveTrigger.Elements().Single().Attribute("Value"));
        foreach (string state in new[] { "IsMouseOver", "IsPressed", "IsKeyboardFocused" })
        {
            XElement trigger = actionTriggers.Single(element => (string?)element.Attribute("Property") == state);
            XElement foreground = trigger.Elements().Single(element => (string?)element.Attribute("Property") == "Foreground");
            Assert.Equal("{DynamicResource GlobalTextBrush}", (string?)foreground.Attribute("Value"));
            Assert.True(actionTriggers.IndexOf(inactiveTrigger) < actionTriggers.IndexOf(trigger), "Direct interaction must take precedence over inactive-window dimming.");
        }

        string compactSource = ReadRepositoryText("ColorVision/CompactMainWindow.cs");
        Assert.Contains("CompactActionsOverflowButton.Click += CompactActionsOverflowButton_Click",
            MethodBody(compactSource, "public CompactMainWindow()"), StringComparison.Ordinal);
        string closeBody = MethodBody(compactSource, "private void CloseCompactTitleBar(");
        Assert.Contains("CompactActionsOverflowButton.Click -= CompactActionsOverflowButton_Click", closeBody, StringComparison.Ordinal);
        Assert.Contains("CompactActionsOverflowButton.ContextMenu = null", closeBody, StringComparison.Ordinal);
        string layoutBody = MethodBody(compactSource, "protected override void UpdateRightMenuVisibility()");
        Assert.Contains("CombinedUpdateCoordinator.HasPendingStartupUpdate", layoutBody, StringComparison.Ordinal);
        Assert.Contains("CompactUpdateBadge.Visibility", layoutBody, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(CompactActionsOverflowButton", layoutBody, StringComparison.Ordinal);
        Assert.Contains("UpdateNotificationButton.Content", layoutBody, StringComparison.Ordinal);
        int fallbackReturn = layoutBody.IndexOf("return;", StringComparison.Ordinal);
        Assert.Contains("inactiveMenu.IsOpen = false", layoutBody[..fallbackReturn], StringComparison.Ordinal);
        string fullScreenBody = MethodBody(compactSource, "private void CompactTitleBarConfigChanged(");
        int resumeBranch = fullScreenBody.IndexOf("else", StringComparison.Ordinal);
        Assert.Contains("UpdateRightMenuVisibility();", fullScreenBody[..resumeBranch], StringComparison.Ordinal);
        string clickBody = MethodBody(compactSource, "private void CompactActionsOverflowButton_Click(");
        Assert.Contains("CompactTitleBarActions.CreateMenu(", clickBody, StringComparison.Ordinal);
        Assert.Contains("CombinedUpdateCoordinator.HasPendingStartupUpdate", clickBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfigureButton(", ReadRepositoryText("ColorVision/MainWindow.xaml.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void CompactMenuAlignmentIsOptInAndRestoresTheOriginalLayoutOnFallbackAndFullScreen()
    {
        XElement menu = Named(LoadMainWindow(), "Menu1");
        Assert.Null(menu.Attribute("VerticalAlignment"));
        Assert.Null(menu.Attribute("Margin"));
        string source = ReadRepositoryText("ColorVision/CompactMainWindow.cs");
        string constructor = MethodBody(source, "public CompactMainWindow()");
        Assert.Contains("_ordinaryMenuMargin = Menu1.Margin", constructor, StringComparison.Ordinal);
        Assert.Contains("_ordinaryMenuVerticalAlignment = Menu1.VerticalAlignment", constructor, StringComparison.Ordinal);
        string alignBody = MethodBody(source, "private void SetCompactMenuAlignment(");
        Assert.Contains("compact ? VerticalAlignment.Center : _ordinaryMenuVerticalAlignment", alignBody, StringComparison.Ordinal);
        Assert.Contains("_ordinaryMenuMargin.Top + 4", alignBody, StringComparison.Ordinal);
        Assert.Contains(": _ordinaryMenuMargin", alignBody, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"DockingManager1\.Margin\s*=", alignBody);
        string attachBody = MethodBody(source, "private void AttachCompactTitleBar(");
        Assert.Contains("SetCompactMenuAlignment(true)", attachBody, StringComparison.Ordinal);
        Assert.Contains("SetCompactMenuAlignment(false)", attachBody[attachBody.IndexOf("catch", StringComparison.Ordinal)..], StringComparison.Ordinal);
        string fullBody = MethodBody(source, "private void CompactTitleBarConfigChanged(");
        Assert.Contains("SetCompactMenuAlignment(false)", fullBody, StringComparison.Ordinal);
        Assert.Contains("SetCompactMenuAlignment(_compactTitleBar?.IsAttached == true)", fullBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetCompactMenuAlignment", ReadRepositoryText("ColorVision/MainWindow.xaml.cs"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(40)]
    [InlineData(48)]
    public void CompactMenuCenterMovesDownTwoDipsWithoutMovingTheWorkspaceBoundary(double headerHeight)
    {
        WpfTestHost.Invoke(() =>
        {
            var fixture = CreateSyntheticHeader();
            fixture.Menu.Items.Add(new MenuItem { Header = "文件(_F)" });
            fixture.Header.Width = 1000;
            fixture.Header.Height = headerHeight;
            Thickness originalMargin = fixture.Menu.Margin;
            VerticalAlignment originalAlignment = fixture.Menu.VerticalAlignment;
            foreach (bool compact in new[] { true, false, true, false })
            {
                fixture.Menu.SetCurrentValue(FrameworkElement.VerticalAlignmentProperty, compact ? VerticalAlignment.Center : originalAlignment);
                fixture.Menu.SetCurrentValue(FrameworkElement.MarginProperty, compact
                    ? new Thickness(originalMargin.Left, originalMargin.Top + 4, originalMargin.Right, originalMargin.Bottom) : originalMargin);
                fixture.Header.Measure(new Size(1000, headerHeight));
                fixture.Header.Arrange(new Rect(0, 0, 1000, headerHeight));
                fixture.Header.UpdateLayout();

                Assert.Equal(headerHeight, fixture.Header.ActualHeight, 3);
                Assert.Equal(1000, fixture.Header.ActualWidth, 3);
                if (compact)
                {
                    Point menuTop = fixture.Menu.TranslatePoint(new Point(), fixture.Header);
                    Assert.Equal(headerHeight / 2 + 2, menuTop.Y + fixture.Menu.ActualHeight / 2, 3);
                    Assert.InRange(menuTop.Y, 0, headerHeight);
                    Assert.True(menuTop.Y + fixture.Menu.ActualHeight <= headerHeight);
                }
                else
                {
                    Assert.Equal(originalMargin, fixture.Menu.Margin);
                    Assert.Equal(originalAlignment, fixture.Menu.VerticalAlignment);
                }
            }
        });
    }

    [Theory]
    [InlineData(400)]
    [InlineData(640)]
    [InlineData(1000)]
    public void RealLayoutHelperStabilizesAcrossNarrowWideResizeAndUpdateNoticeChanges(double initialWidth)
    {
        WpfTestHost.Invoke(() =>
        {
            (Border header, Border drag, StackPanel tools, Image icon, Menu menu, Button update, Button overflow) = CreateSyntheticHeader();
            drag.Visibility = Visibility.Visible;
            icon.Visibility = Visibility.Visible;
            // Known natural widths make the visibility threshold deterministic across system fonts/themes.
            menu.Width = 300;
            menu.Margin = new Thickness(0, 4, 0, 0);
            menu.VerticalAlignment = VerticalAlignment.Center;
            update.Width = 180;
            update.Margin = new Thickness(0);
            overflow.Width = 32;
            overflow.Margin = new Thickness(0);
            tools.Children.Add(new Button { Content = "Tools", Width = 100, Margin = new Thickness(0) });
            foreach (string caption in new[] { "File", "Edit", "Templates", "Tools", "View", "Help", "Long customer menu" })
                menu.Items.Add(new MenuItem { Header = caption });

            header.Width = initialWidth;
            header.Height = 40;
            int layoutUpdates = 0;
            bool hasPendingUpdate = false;
            void UpdateHeaderLayout()
            {
                Assert.True(++layoutUpdates <= 256, "The real size-change/measurement path must not enter a layout feedback loop.");
                CompactTitleBarLayout.Update(header, menu, tools, update, icon, drag, overflow, hasPendingUpdate);
            }
            SizeChangedEventHandler sizeChanged = (_, _) => UpdateHeaderLayout();
            header.SizeChanged += sizeChanged;
            var window = new Window
            {
                Content = header,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                ShowInTaskbar = false,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            void SettleLayout()
            {
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            try
            {
                window.Show();
                SettleLayout();
                foreach (double width in new[] { initialWidth, 400, 640, 1000, 640, 400, initialWidth, 1000 })
                {
                    foreach (bool hasUpdate in new[] { false, true, false })
                    {
                        int updatesBefore = layoutUpdates;
                        header.Width = width;
                        hasPendingUpdate = hasUpdate;
                        // Match the production notification callback, including a newly visible notice before layout.
                        UpdateHeaderLayout();
                        SettleLayout();

                        double requiredWidth = 300 + 100 + (hasUpdate ? 180 : 0) + 30 + 120;
                        Visibility expected = header.ActualWidth >= requiredWidth ? Visibility.Visible : Visibility.Collapsed;
                        Visibility expectedOverflow = expected == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                        Visibility expectedNotice = hasUpdate && (expected == Visibility.Visible || header.ActualWidth >= 662)
                            ? Visibility.Visible : Visibility.Collapsed;
                        Assert.Equal(expected, tools.Visibility);
                        Assert.Equal(expectedOverflow, overflow.Visibility);
                        Assert.Equal(expectedNotice, update.Visibility);
                        Assert.Equal(width, header.ActualWidth, 3);
                        Assert.Equal(120, drag.ActualWidth, 3);
                        Assert.False(WindowChrome.GetIsHitTestVisibleInChrome(header));
                        Assert.False(WindowChrome.GetIsHitTestVisibleInChrome(drag));
                        Point center = drag.TranslatePoint(new Point(drag.ActualWidth / 2, drag.ActualHeight / 2), header);
                        // Use WPF's input filter, as WindowChrome does; raw visual tests can see collapsed high-Z panels.
                        Assert.Same(drag, header.InputHitTest(center));
                        AssertInteractiveOverflowIsReachable(header, overflow);
                        AssertActionsAdjoinCaptionBoundary(header, tools, overflow, drag);
                        Assert.InRange(layoutUpdates - updatesBefore, 1, 8);

                        int settledUpdates = layoutUpdates;
                        SettleLayout();
                        Assert.Equal(settledUpdates, layoutUpdates);
                        Assert.Equal(expected, tools.Visibility);
                        Assert.Equal(expectedOverflow, overflow.Visibility);
                        Assert.Equal(expectedNotice, update.Visibility);
                    }
                }

                // Pending update state survives a layout-hidden notice: widening alone must restore it.
                hasPendingUpdate = true;
                foreach (double width in new[] { 400d, 700, 1000, 400, 1000 })
                {
                    header.Width = width;
                    UpdateHeaderLayout();
                    SettleLayout();
                    Assert.Equal(width >= 730 ? Visibility.Visible : Visibility.Collapsed, tools.Visibility);
                    Assert.Equal(width >= 730 ? Visibility.Collapsed : Visibility.Visible, overflow.Visibility);
                    Assert.Equal(width >= 662 ? Visibility.Visible : Visibility.Collapsed, update.Visibility);
                    AssertInteractiveOverflowIsReachable(header, overflow);
                    AssertActionsAdjoinCaptionBoundary(header, tools, overflow, drag);
                }
            }
            finally
            {
                header.SizeChanged -= sizeChanged;
                window.Close();
            }
        });
    }

    [Fact]
    public void AutoSizedHeaderUsesTheRealLayoutHelperWithoutResizeOrNoticeFeedbackLoops()
    {
        WpfTestHost.Invoke(() =>
        {
            (Border header, Border drag, StackPanel tools, Image icon, Menu menu, Button update, Button overflow) = CreateSyntheticHeader();
            drag.Visibility = Visibility.Visible;
            icon.Visibility = Visibility.Visible;
            header.MinHeight = 32;
            header.Width = 1000;
            menu.Margin = new Thickness(0, 4, 0, 0);
            menu.VerticalAlignment = VerticalAlignment.Center;
            foreach (string caption in new[] { "文件(_F)", "编辑(_E)", "模板(_M)", "工具(_T)", "视图(_V)", "帮助(_H)" })
                menu.Items.Add(new MenuItem { Header = caption });
            foreach (string label in new[] { "下载", "工具", "用户" })
                tools.Children.Add(new Button { Content = label, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(6, 4, 6, 4) });
            update.Content = "发现可用更新";
            update.Padding = new Thickness(4, 0, 4, 0);
            Assert.True(double.IsNaN(menu.Width));
            Assert.True(double.IsNaN(header.Height));

            int layoutUpdates = 0;
            bool hasPendingUpdate = false;
            void UpdateHeaderLayout()
            {
                Assert.True(++layoutUpdates <= 256, "Auto-sized controls must not feed continuous size changes back into measurement.");
                CompactTitleBarLayout.Update(header, menu, tools, update, icon, drag, overflow, hasPendingUpdate);
            }
            SizeChangedEventHandler sizeChanged = (_, _) => UpdateHeaderLayout();
            header.SizeChanged += sizeChanged;
            var window = new Window
            {
                Content = header,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                ShowInTaskbar = false,
                Left = -10000,
                Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            void SettleLayout()
            {
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
            try
            {
                window.Show();
                SettleLayout();
                foreach (double width in new[] { 400d, 640, 1000, 640, 400, 1000, 400, 640, 1000 })
                {
                    foreach (bool hasUpdate in new[] { false, true, false })
                    {
                        int updatesBefore = layoutUpdates;
                        header.Width = width;
                        hasPendingUpdate = hasUpdate;
                        UpdateHeaderLayout();
                        SettleLayout();

                        Assert.Equal(width, header.ActualWidth, 3);
                        Assert.True(header.ActualHeight >= 32);
                        Assert.Equal(120, drag.ActualWidth, 3);
                        Assert.False(WindowChrome.GetIsHitTestVisibleInChrome(drag));
                        Point center = drag.TranslatePoint(new Point(drag.ActualWidth / 2, drag.ActualHeight / 2), header);
                        Assert.Same(drag, header.InputHitTest(center));
                        AssertInteractiveOverflowIsReachable(header, overflow);
                        AssertActionsAdjoinCaptionBoundary(header, tools, overflow, drag);
                        Assert.Equal(tools.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, overflow.Visibility);
                        if (!hasUpdate)
                            Assert.Equal(Visibility.Collapsed, update.Visibility);
                        Assert.InRange(layoutUpdates - updatesBefore, 1, 12);

                        int settledUpdates = layoutUpdates;
                        Visibility settledActionsVisibility = tools.Visibility;
                        Visibility settledNoticeVisibility = update.Visibility;
                        Visibility settledOverflowVisibility = overflow.Visibility;
                        double settledHeight = header.ActualHeight;
                        SettleLayout();
                        SettleLayout();
                        Assert.Equal(settledUpdates, layoutUpdates);
                        Assert.Equal(settledActionsVisibility, tools.Visibility);
                        Assert.Equal(settledNoticeVisibility, update.Visibility);
                        Assert.Equal(settledOverflowVisibility, overflow.Visibility);
                        Assert.Equal(settledHeight, header.ActualHeight, 3);
                    }
                }
            }
            finally
            {
                header.SizeChanged -= sizeChanged;
                window.Close();
            }
        });
    }

    [Fact]
    public void MetadataButtonsKeepTheirOriginalCommandsAndIdentityWhenConfiguredAsCompactActions()
    {
        WpfTestHost.Invoke(() =>
        {
            var fixture = CreateSyntheticHeader();
            Style style = Assert.IsType<Style>(fixture.Header.FindResource("CompactTitleBarActionButtonStyle"));
            foreach (string label in new[] { "下载", "第三方应用", "账户" })
            {
                var command = new RelayCommand(_ => throw new InvalidOperationException("Metadata construction must not execute commands."));
                var icon = new TextBlock { Text = label[..1] };
                var metadata = new MenuItemMetadata { Header = label, Icon = icon, Command = command };
                Button button = MainWindow.CreateRightMenuButton(metadata);
                object parameter = new();
                button.CommandParameter = parameter;

                Assert.Equal(20, button.Width);
                Assert.True(double.IsNaN(button.Height));
                Assert.Same(metadata, button.Tag);
                Assert.Same(icon, button.Content);
                Assert.Same(command, button.Command);
                Assert.Equal(label, button.ToolTip);
                Assert.Equal(label, AutomationProperties.GetName(button));

                CompactTitleBarActions.ConfigureButton(button, style);

                Assert.Equal(32, button.Width);
                Assert.Equal(28, button.Height);
                Assert.Same(style, button.Style);
                Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(button));
                Assert.Same(metadata, button.Tag);
                Assert.Same(icon, button.Content);
                Assert.Same(command, button.Command);
                Assert.Same(parameter, button.CommandParameter);
                Assert.Equal(label, button.ToolTip);
                Assert.Equal(label, AutomationProperties.GetName(button));
            }
        });
    }

    [Fact]
    public void OrdinaryAndCompactActionGlyphsFollowDefaultInactiveAndChangedThemeColors()
    {
        WpfTestHost.Invoke(() =>
        {
            // Keep detached defaults separate from the production path, which owns the header before its first layout.
            foreach (bool hostInWindow in new[] { false, true })
            {
                var fixture = CreateSyntheticHeader();
                foreach (string key in new[] { "GlobalBackground", "GlobalTextBrush", "TitleBarActionForeground", "TitleBarActionInactiveForeground" })
                    fixture.Header.Resources.Remove(key);

                Button ordinary = MainWindow.CreateRightMenuButton(new MenuItemMetadata { Header = "下载", Icon = new TextBlock { Text = "D", Foreground = Brushes.White } });
                Button compact = MainWindow.CreateRightMenuButton(new MenuItemMetadata { Header = "账户", Icon = new TextBlock { Text = "A", Foreground = Brushes.White } });
                Style commonStyle = Assert.IsType<Style>(fixture.Header.FindResource("MainWindowActionButtonStyle"));
                Style compactStyle = Assert.IsType<Style>(fixture.Header.FindResource("CompactTitleBarActionButtonStyle"));
                CompactTitleBarActions.ConfigureButton(compact, compactStyle);
                fixture.Tools.Children.Add(ordinary);
                fixture.Tools.Children.Add(compact);

                Window? window = hostInWindow ? new Window
                {
                    Content = fixture.Header, Width = 1000, Height = 80, Left = -10000, Top = -10000,
                    WindowStartupLocation = WindowStartupLocation.Manual, ShowActivated = false, ShowInTaskbar = false
                } : null;

                void ApplyTheme(bool dark)
                {
                    fixture.Header.Resources.MergedDictionaries.Clear();
                    fixture.Header.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri($"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml", UriKind.Relative)
                    });
                    fixture.Header.Measure(new Size(1000, 40));
                    fixture.Header.Arrange(new Rect(0, 0, 1000, 40));
                    fixture.Header.UpdateLayout();
                    PumpDispatcher();
                }

                Color AssertActionColor(string resourceKey)
                {
                    if (window != null)
                        Assert.False(window.IsActive);
                    Color expected = Assert.IsType<SolidColorBrush>(fixture.Header.FindResource(resourceKey)).Color;
                    foreach (Button button in new[] { ordinary, compact })
                    {
                        Assert.False(button.IsMouseOver);
                        Assert.False(button.IsKeyboardFocused);
                        Assert.Equal(expected, Assert.IsType<SolidColorBrush>(button.Foreground).Color);
                        Assert.Same(button.Foreground, Assert.IsType<TextBlock>(button.Content).Foreground);
                    }
                    return expected;
                }

                try
                {
                    window?.Show();
                    PumpDispatcher();
                    ApplyTheme(false);
                    Assert.Same(commonStyle, ordinary.Style.BasedOn);
                    Assert.Same(commonStyle, compactStyle.BasedOn);
                    string resourceKey = hostInWindow ? "TitleBarActionInactiveForeground" : "TitleBarActionForeground";
                    Color lightColor = AssertActionColor(resourceKey);
                    ApplyTheme(true);
                    Color darkColor = AssertActionColor(resourceKey);
                    if (!hostInWindow)
                        Assert.NotEqual(lightColor, darkColor);
                }
                finally
                {
                    window?.Close();
                }
            }
        });
    }

    [Fact]
    public void OverflowMenuKeepsLiveCommandParameterTargetAndEnabledBindings()
    {
        WpfTestHost.Invoke(() =>
        {
            List<object?> executed = [];
            var command = new RelayCommand(parameter => executed.Add(parameter));
            object originalParameter = new();
            var source = new Button { Command = command, CommandParameter = originalParameter, ToolTip = "下载", Visibility = Visibility.Collapsed };
            var denied = new Button { Command = new RelayCommand(_ => throw new InvalidOperationException("Denied command executed."), _ => false), ToolTip = "账户" };
            ContextMenu menu = CompactTitleBarActions.CreateMenu([source, denied], new Button(), hasPendingUpdate: false);
            PumpDispatcher();

            Assert.Equal(2, menu.Items.Count);
            MenuItem item = Assert.IsType<MenuItem>(menu.Items[0]);
            MenuItem deniedItem = Assert.IsType<MenuItem>(menu.Items[1]);
            Assert.Equal("下载", item.Header);
            Assert.Same(command, item.Command);
            Assert.Same(originalParameter, item.CommandParameter);
            Assert.Same(source, item.CommandTarget);
            Assert.True(item.IsEnabled);
            Assert.False(deniedItem.IsEnabled);
            Assert.False(deniedItem.Command!.CanExecute(deniedItem.CommandParameter));

            object changedParameter = new();
            var target = new Button();
            source.CommandParameter = changedParameter;
            source.CommandTarget = target;
            source.IsEnabled = false;
            PumpDispatcher();
            Assert.Same(changedParameter, item.CommandParameter);
            Assert.Same(target, item.CommandTarget);
            Assert.False(item.IsEnabled);
            Assert.Empty(executed);

            source.IsEnabled = true;
            source.CommandTarget = null;
            PumpDispatcher();
            Assert.True(item.IsEnabled);
            Assert.Same(source, item.CommandTarget);
            Assert.True(item.Command!.CanExecute(item.CommandParameter));
            item.Command.Execute(item.CommandParameter);
            Assert.Same(changedParameter, Assert.Single(executed));

            var replacement = new RelayCommand(_ => { });
            source.Command = replacement;
            PumpDispatcher();
            Assert.Same(replacement, item.Command);
        });
    }

    [Fact]
    public void OverflowRoutedCommandsStillUseTheOriginalButtonAsTheirDefaultTarget()
    {
        WpfTestHost.Invoke(() =>
        {
            int executions = 0;
            object parameter = new();
            var command = new RoutedCommand();
            var source = new Button { Command = command, CommandParameter = parameter, Visibility = Visibility.Collapsed };
            source.CommandBindings.Add(new CommandBinding(command, (_, args) =>
            {
                Assert.Same(parameter, args.Parameter);
                executions++;
            }, (_, args) => args.CanExecute = true));
            ContextMenu menu = CompactTitleBarActions.CreateMenu([source], new Button(), hasPendingUpdate: false);
            MenuItem item = Assert.IsType<MenuItem>(Assert.Single(menu.Items.Cast<object>()));
            PumpDispatcher();

            Assert.Same(source, item.CommandTarget);
            Assert.True(command.CanExecute(item.CommandParameter, item.CommandTarget));
            command.Execute(item.CommandParameter, item.CommandTarget);
            Assert.Equal(1, executions);
        });
    }

    [Fact]
    public void OverflowUpdateItemBridgesTheOriginalClickAndHonorsPendingAndDisabledState()
    {
        WpfTestHost.Invoke(() =>
        {
            int updates = 0;
            var update = new Button { Content = "发现更新", Visibility = Visibility.Collapsed };
            update.Click += (sender, args) =>
            {
                Assert.Same(update, sender);
                Assert.Same(update, args.Source);
                updates++;
            };
            ContextMenu menu = CompactTitleBarActions.CreateMenu([new Button { ToolTip = "账户" }], update, hasPendingUpdate: true);
            Assert.Equal(3, menu.Items.Count);
            Assert.IsType<Separator>(menu.Items[1]);
            MenuItem item = Assert.IsType<MenuItem>(menu.Items[2]);
            PumpDispatcher();
            Assert.Equal("发现更新", item.Header);
            Assert.True(item.IsEnabled);
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            Assert.Equal(1, updates);

            update.IsEnabled = false;
            update.Content = "正在更新";
            PumpDispatcher();
            Assert.False(item.IsEnabled);
            Assert.Equal("正在更新", item.Header);
            // Even a programmatically raised event must not bypass the original update-button guard.
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            Assert.Equal(1, updates);

            update.IsEnabled = true;
            PumpDispatcher();
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
            Assert.Equal(2, updates);
            Assert.Empty(CompactTitleBarActions.CreateMenu([], update, hasPendingUpdate: false).Items.Cast<object>());
            Assert.IsType<MenuItem>(Assert.Single(CompactTitleBarActions.CreateMenu([], update, hasPendingUpdate: true).Items.Cast<object>()));
        });
    }

    [Fact]
    public void RealActionAndUpdateTemplatesKeepTheWholeCompactClickAreaInteractive()
    {
        WpfTestHost.Invoke(() =>
        {
            var fixture = CreateSyntheticHeader();
            fixture.Header.Width = 1000;
            fixture.Header.Height = 40;
            fixture.Drag.Visibility = Visibility.Visible;
            fixture.Icon.Visibility = Visibility.Visible;
            fixture.Menu.Items.Add(new MenuItem { Header = "File" });
            var action = MainWindow.CreateRightMenuButton(new MenuItemMetadata { Header = "Account", Icon = new TextBlock { Text = "A" } });
            CompactTitleBarActions.ConfigureButton(action, (Style)fixture.Header.FindResource("CompactTitleBarActionButtonStyle"));
            fixture.Tools.Children.Add(action);
            fixture.Update.Width = 180;
            fixture.Header.Measure(new Size(1000, 40));
            fixture.Header.Arrange(new Rect(0, 0, 1000, 40));
            CompactTitleBarLayout.Update(fixture.Header, fixture.Menu, fixture.Tools, fixture.Update,
                fixture.Icon, fixture.Drag, fixture.Overflow, hasPendingUpdate: true);
            fixture.Header.Measure(new Size(1000, 40));
            fixture.Header.Arrange(new Rect(0, 0, 1000, 40));
            fixture.Header.UpdateLayout();

            foreach (Button button in new[] { action, fixture.Update })
            {
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Equal(28, button.ActualHeight, 3);
                Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(button));
                foreach (Point localPoint in new[] { new Point(button.ActualWidth / 2, 1), new Point(1, button.ActualHeight / 2), new Point(button.ActualWidth / 2, button.ActualHeight - 1) })
                {
                    Point point = button.TranslatePoint(localPoint, fixture.Header);
                    DependencyObject? hit = VisualTreeHelper.HitTest(fixture.Header, point)?.VisualHit;
                    Assert.NotNull(hit);
                    Assert.True(ReferenceEquals(button, hit) || button.IsAncestorOf(hit), "The title-bar click area must include the space outside its text glyphs.");
                }
            }
        });
    }

    [Fact]
    public void HeaderDoesNotPaintOverNativeButtonsOrRecreateSystemCaptionCommands()
    {
        XDocument document = LoadMainWindow();
        Assert.Null(Named(document, "Root").Attribute("Background"));
        Assert.Null(Named(document, "TopBarGrid").Attribute("Background"));
        Assert.Equal("Collapsed", (string?)Named(document, "CompactWindowIcon").Attribute("Visibility"));

        string[] nativeCommands = ["CloseWindowCommand", "MinimizeWindowCommand", "MaximizeWindowCommand", "RestoreWindowCommand"];
        Assert.DoesNotContain(Named(document, "TopBarGrid").DescendantsAndSelf().Attributes(),
            attribute => nativeCommands.Any(command => attribute.Value.Contains(command, StringComparison.Ordinal)));
    }

    [Fact]
    public void PackageIconLoaderIsSharedAndDoesNotApplyNativeCaptionStyling()
    {
        MethodInfo loader = Assert.IsAssignableFrom<MethodInfo>(typeof(ThemeManagerExtensions).GetMethod(
            nameof(ThemeManagerExtensions.TryLoadPackageIcon), BindingFlags.Public | BindingFlags.Static));
        Assert.Equal(typeof(BitmapImage), loader.ReturnType);
        Assert.Equal(typeof(Window), Assert.Single(loader.GetParameters()).ParameterType);

        string source = ReadRepositoryText("UI/ColorVision.Themes/ThemeManagerExtensions.cs");
        string loaderBody = MethodBody(source, "public static BitmapImage? TryLoadPackageIcon(");
        Assert.Contains("BitmapCacheOption.OnLoad", loaderBody, StringComparison.Ordinal);
        Assert.Contains("image.Freeze()", loaderBody, StringComparison.Ordinal);
        Assert.Contains("PackageIcon.png", loaderBody, StringComparison.Ordinal);
        Assert.DoesNotContain("DwmSetWindowAttribute", loaderBody, StringComparison.Ordinal);
        Assert.DoesNotContain("SetWindowTitleBarColor", loaderBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCaption", loaderBody, StringComparison.Ordinal);
        Assert.Contains("TryLoadPackageIcon(window)", MethodBody(source, "public static void ApplyCaption("), StringComparison.Ordinal);
    }

    [Fact]
    public void CompactWindowLoadsPackageIconOnceAndThemeChangesUseTheCachedImage()
    {
        string source = ReadRepositoryText("ColorVision/CompactMainWindow.cs");
        Assert.Equal(1, Regex.Matches(source, @"ThemeManagerExtensions\.TryLoadPackageIcon\(").Count);
        Assert.Contains("_compactTitleBarPackageIcon = ThemeManagerExtensions.TryLoadPackageIcon(this)",
            MethodBody(source, "private void AttachCompactTitleBar("), StringComparison.Ordinal);
        string themeBody = MethodBody(source, "private void ApplyCompactTitleBarTheme(");
        Assert.Contains("if (_compactTitleBarPackageIcon != null)", themeBody, StringComparison.Ordinal);
        Assert.Contains("Icon = _compactTitleBarPackageIcon", themeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("TryLoadPackageIcon", themeBody, StringComparison.Ordinal);
        Assert.DoesNotContain("File.", themeBody, StringComparison.Ordinal);
    }

    private static XElement Named(XDocument document, string name)
        => Assert.Single(document.Descendants(), element => (string?)element.Attribute(Xaml + "Name") == name);

    private static (Border Header, Border Drag, StackPanel Tools, Image Icon, Menu Menu, Button Update, Button Overflow) CreateSyntheticHeader()
    {
        // Use the real header and button templates without constructing MainWindow or its services.
        XDocument document = LoadMainWindow();
        XElement markup = new(Named(document, "MainWindowTitleBar"));
        markup.SetAttributeValue(XNamespace.Xmlns + "x", Xaml.NamespaceName);
        var resources = new XElement(Presentation + "Border.Resources");
        foreach (string key in new[] { "MainWindowActionButtonStyle", "CompactTitleBarActionButtonStyle", "UpdateNotificationButtonStyle" })
            resources.Add(new XElement(document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Key") == key)));
        markup.AddFirst(resources);
        XElement updateMarkup = markup.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "UpdateNotificationButton");
        updateMarkup.Attribute("Click")!.Remove();
        updateMarkup.SetAttributeValue("Content", "Update available");
        updateMarkup.Attribute("ToolTip")!.Remove();
        XElement overflowMarkup = markup.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "CompactActionsOverflowButton");
        overflowMarkup.SetAttributeValue("ToolTip", "More actions");
        overflowMarkup.SetAttributeValue("AutomationProperties.Name", "More actions");
        XElement iconMarkup = markup.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == "CompactWindowIcon");
        iconMarkup.Attribute("Source")!.Remove();
        var header = Assert.IsType<Border>(XamlReader.Parse(markup.ToString()));
        header.Resources["GlobalBackground"] = Brushes.White;
        header.Resources["BorderBrush"] = Brushes.Black;
        header.Resources["GlobalTextBrush"] = Brushes.Black;
        header.Resources["TitleBarActionForeground"] = Brushes.DimGray;
        header.Resources["TitleBarActionInactiveForeground"] = Brushes.Gray;
        Assert.IsType<DockPanel>(header.Child);
        return (header, Assert.IsType<Border>(header.FindName("CompactDragRegion")),
            Assert.IsType<StackPanel>(header.FindName("RightMenuItemPanel")), Assert.IsType<Image>(header.FindName("CompactWindowIcon")),
            Assert.IsType<Menu>(header.FindName("Menu1")), Assert.IsType<Button>(header.FindName("UpdateNotificationButton")),
            Assert.IsType<Button>(header.FindName("CompactActionsOverflowButton")));
    }

    private static void AssertInteractiveOverflowIsReachable(Border header, Button overflow)
    {
        if (overflow.Visibility != Visibility.Visible)
            return;

        Assert.True(WindowChrome.GetIsHitTestVisibleInChrome(overflow));
        Assert.True(overflow.ActualWidth >= 32);
        Point center = overflow.TranslatePoint(new Point(overflow.ActualWidth / 2, overflow.ActualHeight / 2), header);
        DependencyObject? hit = header.InputHitTest(center) as DependencyObject;
        Assert.NotNull(hit);
        Assert.True(ReferenceEquals(overflow, hit) || overflow.IsAncestorOf(hit),
            $"The visible overflow button must receive input instead of an overlapping {hit!.GetType().Name}.");
    }

    private static void AssertActionsAdjoinCaptionBoundary(Border header, StackPanel actions, Button overflow, Border drag)
    {
        FrameworkElement rightmost = actions.Visibility == Visibility.Visible ? actions : overflow;
        Assert.Equal(Visibility.Visible, rightmost.Visibility);
        Point actionsTop = rightmost.TranslatePoint(new Point(), header);
        Point dragTop = drag.TranslatePoint(new Point(), header);
        Assert.Equal(header.ActualWidth, actionsTop.X + rightmost.ActualWidth, 3);
        Assert.Equal(actionsTop.X, dragTop.X + drag.ActualWidth, 3);

        if (actions.Visibility != Visibility.Visible)
            return;
        foreach (Button button in actions.Children.OfType<Button>())
        {
            Point center = button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), header);
            DependencyObject? hit = header.InputHitTest(center) as DependencyObject;
            Assert.NotNull(hit);
            Assert.True(ReferenceEquals(button, hit) || button.IsAncestorOf(hit), "A right-edge action must not be covered by the menu or drag region.");
        }
    }

    private static void PumpDispatcher() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static XDocument LoadMainWindow([CallerFilePath] string sourcePath = "")
        => XDocument.Load(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", "ColorVision", "MainWindow.xaml")));

    private static string ReadRepositoryText(string repositoryPath, [CallerFilePath] string sourcePath = "")
        => File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", "..", repositoryPath)));

    private static string MethodBody(string source, string declaration)
    {
        int declarationIndex = source.IndexOf(declaration, StringComparison.Ordinal);
        Assert.True(declarationIndex >= 0, $"Method declaration not found: {declaration}");
        int openingBrace = source.IndexOf('{', declarationIndex);
        Assert.True(openingBrace >= 0);
        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0)
                return source[(openingBrace + 1)..index];
        }
        throw new InvalidOperationException($"Method body not found: {declaration}");
    }
}
