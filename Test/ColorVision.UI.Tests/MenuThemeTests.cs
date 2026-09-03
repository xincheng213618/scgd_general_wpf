using ColorVision.Common.MVVM;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class MenuThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealMenus_LoadAllRolesWithRoundedSurfacesAndSeparateShadows(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(dark);
            Assert.Equal(MenuItemRole.TopLevelHeader, fixture.Root.Role);
            Assert.Equal(MenuItemRole.TopLevelItem, fixture.TopLevelAction.Role);
            Assert.NotNull(fixture.TopLevelAction.Template);
            Assert.True(fixture.TopLevelAction.ActualHeight > 0);
            Assert.True(fixture.TopLevelAction.ActualHeight < 30, "Top-level menu items should keep their compact height.");

            fixture.OpenRoot();
            Assert.Equal(MenuItemRole.SubmenuItem, fixture.Action.Role);
            Assert.Equal(MenuItemRole.SubmenuHeader, fixture.Nested.Role);
            Assert.InRange(fixture.Action.ActualHeight, 26, 27);
            Assert.InRange(fixture.Nested.ActualHeight, 26, 27);
            AssertMenuPopup(fixture.Root);

            fixture.OpenNested();
            AssertMenuPopup(fixture.Nested);

            fixture.OpenContextMenu();
            AssertRoundedSurface(fixture.ContextMenu);
            Assert.InRange(fixture.ContextAction.ActualHeight, 26, 27);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommandsCheckMarksAndDisabledItems_RetainNativeMenuBehavior(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(dark);
            fixture.OpenRoot();

            var togglePeer = new MenuItemAutomationPeer(fixture.Checkable);
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(togglePeer.GetPattern(PatternInterface.Toggle));
            toggle.Toggle();
            PumpDispatcher();
            fixture.OpenRoot();
            Assert.True(fixture.Checkable.IsChecked);
            Assert.Equal(ToggleState.On, toggle.ToggleState);
            Assert.True(TemplatePart<Border>(fixture.Checkable, "GlyphPanel").IsVisible);

            toggle.Toggle();
            PumpDispatcher();
            fixture.OpenRoot();
            Assert.False(fixture.Checkable.IsChecked);
            Assert.False(TemplatePart<Border>(fixture.Checkable, "GlyphPanel").IsVisible);

            Assert.False(fixture.Disabled.IsEnabled);
            var disabledPeer = new MenuItemAutomationPeer(fixture.Disabled);
            var disabledInvoke = Assert.IsAssignableFrom<IInvokeProvider>(disabledPeer.GetPattern(PatternInterface.Invoke));
            Assert.Throws<ElementNotEnabledException>(() => disabledInvoke.Invoke());
            Assert.Equal(0, fixture.CommandExecutions);

            var actionPeer = new MenuItemAutomationPeer(fixture.Action);
            var invoke = Assert.IsAssignableFrom<IInvokeProvider>(actionPeer.GetPattern(PatternInterface.Invoke));
            invoke.Invoke();
            PumpDispatcher();
            Assert.Equal(1, fixture.CommandExecutions);
            Assert.Same(fixture.CommandParameter, fixture.LastCommandParameter);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShortcutLabels_StayAlignedAndFollowChangedGestureText(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(dark);
            fixture.OpenRoot();
            Border surface = TemplatePart<Border>(fixture.Root, "SubMenuBorder");

            AssertGestureAlignment(fixture.Action, fixture.Checkable, surface);
            fixture.Action.InputGestureText = "Ctrl+Shift+Alt+F12";
            fixture.Checkable.InputGestureText = "F2";
            fixture.Root.UpdateLayout();
            PumpDispatcher();

            AssertGestureAlignment(fixture.Action, fixture.Checkable, surface);
            Assert.Equal("Ctrl+Shift+Alt+F12", GestureLabel(fixture.Action).Text);
            Assert.Equal("F2", GestureLabel(fixture.Checkable).Text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionalColumns_ExpandAndCollapseWithContentsWhileSiblingHeadersStayAligned(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(dark);
            var first = new MenuItem { Header = "布局条目", InputGestureText = null! };
            var second = new MenuItem { Header = "布局条目", InputGestureText = string.Empty };
            fixture.Root.Items.Clear();
            fixture.Root.Items.Add(first);
            fixture.Root.Items.Add(second);
            fixture.OpenRoot();
            Border surface = TemplatePart<Border>(fixture.Root, "SubMenuBorder");

            void RefreshLayout()
            {
                surface.UpdateLayout();
                PumpDispatcher();
                surface.UpdateLayout();
                PumpDispatcher();
            }

            RefreshLayout();
            AssertPlainMenuRow(first);
            AssertPlainMenuRow(second);
            double plainWidth = surface.ActualWidth;
            double plainHeaderLeft = HeaderBounds(first, surface).Left;

            first.Icon = new Border { Width = 16, Height = 16, Background = Brushes.Gray };
            RefreshLayout();
            AssertHeaderAlignment(first, second, surface);
            Assert.True(HeaderBounds(first, surface).Left > plainHeaderLeft + 1);
            Assert.True(surface.ActualWidth > plainWidth + 1);
            double iconWidth = surface.ActualWidth;

            second.Items.Add(new MenuItem { Header = "子菜单条目" });
            RefreshLayout();
            Assert.Equal(MenuItemRole.SubmenuHeader, second.Role);
            AssertHeaderAlignment(first, second, surface);
            Assert.True(surface.ActualWidth > iconWidth + 1);

            // An unchecked checkable entry reserves its column; toggling it must not move either row.
            second.IsCheckable = true;
            RefreshLayout();
            Assert.Equal(Visibility.Hidden, TemplatePart<Border>(second, "GlyphPanel").Visibility);
            double checkableWidth = surface.ActualWidth;
            second.IsChecked = true;
            RefreshLayout();
            Assert.True(TemplatePart<Border>(second, "GlyphPanel").IsVisible);
            AssertHeaderAlignment(first, second, surface);
            Assert.InRange(Math.Abs(surface.ActualWidth - checkableWidth), 0, 1);

            first.Icon = null;
            second.IsChecked = false;
            second.IsCheckable = false;
            RefreshLayout();
            AssertHeaderAlignment(first, second, surface);
            Assert.InRange(Math.Abs(HeaderBounds(first, surface).Left - plainHeaderLeft), 0, 1);
            double arrowOnlyWidth = surface.ActualWidth;
            Assert.True(arrowOnlyWidth > plainWidth + 1);
            Assert.True(arrowOnlyWidth < checkableWidth - 1);

            first.InputGestureText = "Ctrl+O";
            second.InputGestureText = "Ctrl+Shift+O";
            RefreshLayout();
            AssertHeaderAlignment(first, second, surface);
            AssertGestureAlignment(first, second, surface);
            Assert.True(surface.ActualWidth > arrowOnlyWidth + 1);
            TextBlock gesture = GestureLabel(second);
            Rect gestureBounds = gesture.TransformToAncestor(surface).TransformBounds(new Rect(gesture.RenderSize));
            Assert.True(gestureBounds.Left > HeaderBounds(second, surface).Right + 1);

            first.InputGestureText = null!;
            RefreshLayout();
            AssertHeaderAlignment(first, second, surface);
            second.InputGestureText = string.Empty;
            RefreshLayout();
            AssertHeaderAlignment(first, second, surface);
            Assert.InRange(Math.Abs(surface.ActualWidth - arrowOnlyWidth), 0, 1);

            second.Items.Clear();
            RefreshLayout();
            Assert.Equal(MenuItemRole.SubmenuItem, second.Role);
            AssertPlainMenuRow(first);
            AssertPlainMenuRow(second);
            Assert.InRange(Math.Abs(surface.ActualWidth - plainWidth), 0, 1);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongDropdownAndContextMenus_CanScrollToTheirLastEntry(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(dark);
            for (int index = 0; index < 40; index++)
            {
                fixture.Root.Items.Add(new MenuItem { Header = $"Dropdown entry {index + 1}" });
                fixture.ContextMenu.Items.Add(new MenuItem { Header = $"Context entry {index + 1}" });
            }

            fixture.OpenRoot();
            ScrollViewer dropdownScroll = TemplatePart<ScrollViewer>(fixture.Root, "SubMenuScrollViewer");
            AssertCanScrollToEnd(dropdownScroll);

            fixture.OpenContextMenu();
            ScrollViewer contextScroll = Assert.Single(Descendants<ScrollViewer>(fixture.ContextMenu));
            AssertCanScrollToEnd(contextScroll);
        });
    }

    [Fact]
    public void ReplacingThemeResources_UpdatesExistingDropdownNestedAndContextMenus()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new MenuFixture(false);
            Color light = AssertPopupColors(fixture);

            fixture.ReplaceTheme(true);
            Color dark = AssertPopupColors(fixture);
            Assert.NotEqual(light, dark);

            fixture.ReplaceTheme(false);
            Assert.Equal(light, AssertPopupColors(fixture));
        });
    }

    private static void AssertMenuPopup(MenuItem owner)
    {
        Popup popup = TemplatePart<Popup>(owner, "PART_Popup");
        Assert.True(owner.IsSubmenuOpen);
        Assert.True(popup.IsOpen);
        Assert.True(popup.AllowsTransparency);
        Assert.True(TemplatePart<ItemsPresenter>(owner, "ItemsPresenter").IsVisible);
        AssertRoundedSurface(owner);
    }

    private static void AssertRoundedSurface(Control owner)
    {
        Border surface = TemplatePart<Border>(owner, "SubMenuBorder");
        Border shadow = TemplatePart<Border>(owner, "Shadow");
        Assert.True(surface.IsVisible);
        Assert.True(surface.ActualWidth > 0);
        Assert.True(surface.ActualHeight > 0);
        Assert.Equal(new CornerRadius(8), surface.CornerRadius);
        Assert.Equal(surface.CornerRadius, shadow.CornerRadius);
        Assert.Same(VisualTreeHelper.GetParent(surface), VisualTreeHelper.GetParent(shadow));
        Assert.Null(surface.Effect);
        Assert.IsType<DropShadowEffect>(shadow.Effect);
        Assert.False(shadow.IsHitTestVisible);
    }

    private static void AssertGestureAlignment(MenuItem first, MenuItem second, Border surface)
    {
        TextBlock firstLabel = GestureLabel(first);
        TextBlock secondLabel = GestureLabel(second);
        Rect firstBounds = firstLabel.TransformToAncestor(surface).TransformBounds(new Rect(firstLabel.RenderSize));
        Rect secondBounds = secondLabel.TransformToAncestor(surface).TransformBounds(new Rect(secondLabel.RenderSize));
        Assert.True(firstBounds.Width > 0);
        Assert.True(secondBounds.Width > 0);
        Assert.InRange(Math.Abs(firstBounds.Right - secondBounds.Right), 0, 1);
        Assert.InRange(firstBounds.Right, 0, surface.ActualWidth);
    }

    private static TextBlock GestureLabel(MenuItem item)
        => Assert.Single(Descendants<TextBlock>(item), label => label.Text == item.InputGestureText);

    private static Rect HeaderBounds(MenuItem item, Visual ancestor)
    {
        ContentPresenter header = TemplatePart<ContentPresenter>(item, "HeaderPresenter");
        return header.TransformToAncestor(ancestor).TransformBounds(new Rect(header.RenderSize));
    }

    private static void AssertHeaderAlignment(MenuItem first, MenuItem second, Border surface)
    {
        Rect firstBounds = HeaderBounds(first, surface);
        Rect secondBounds = HeaderBounds(second, surface);
        Assert.InRange(Math.Abs(firstBounds.Left - secondBounds.Left), 0, 1);
        Assert.InRange(Math.Abs(firstBounds.Right - secondBounds.Right), 0, 1);
    }

    private static void AssertPlainMenuRow(MenuItem item)
    {
        Rect header = HeaderBounds(item, item);
        Assert.True(header.Width > 0);
        Assert.InRange(Math.Abs(header.Left - item.Padding.Left), 0, 1);
        Assert.InRange(Math.Abs(item.ActualWidth - item.Padding.Right - header.Right), 0, 1);
    }

    private static void AssertCanScrollToEnd(ScrollViewer scroll)
    {
        // Constrain the actual themed viewport so the check is independent of monitor resolution.
        scroll.MaxHeight = 180;
        scroll.UpdateLayout();
        PumpDispatcher();
        Assert.True(scroll.ViewportHeight > 0);
        Assert.True(scroll.ScrollableHeight > 0);
        scroll.ScrollToBottom();
        PumpDispatcher();
        scroll.UpdateLayout();
        PumpDispatcher();
        Assert.True(scroll.VerticalOffset > 0);
        Assert.InRange(Math.Abs(scroll.ScrollableHeight - scroll.VerticalOffset), 0, 1);
    }

    private static Color AssertPopupColors(MenuFixture fixture)
    {
        Color expected = Assert.IsType<SolidColorBrush>(Application.Current.FindResource("ContextMenuBackground")).Color;
        fixture.OpenRoot();
        Assert.Equal(expected, SurfaceColor(fixture.Root));
        fixture.OpenNested();
        Assert.Equal(expected, SurfaceColor(fixture.Nested));
        fixture.OpenContextMenu();
        Assert.Equal(expected, SurfaceColor(fixture.ContextMenu));
        return expected;
    }

    private static Color SurfaceColor(Control control)
        => Assert.IsType<SolidColorBrush>(TemplatePart<Border>(control, "SubMenuBorder").Background).Color;

    private static T TemplatePart<T>(Control owner, string name) where T : DependencyObject
        => Assert.IsType<T>(owner.Template.FindName(name, owner));

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (T descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    private static void PumpDispatcher()
        => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private sealed class MenuFixture : IDisposable
    {
        private readonly ResourceDictionary[] _previousResources;
        private readonly ResourceDictionary _theme = new();
        private readonly Window _window;

        internal MenuItem Root { get; } = new() { Header = "文件(_F)" };
        internal MenuItem TopLevelAction { get; } = new() { Header = "帮助(_H)" };
        internal MenuItem Action { get; } = new() { Header = "打开文件(_O)", InputGestureText = "Ctrl+O" };
        internal MenuItem Checkable { get; } = new() { Header = "显示较长名称的状态栏(_S)", IsCheckable = true, InputGestureText = "Ctrl+Shift+S" };
        internal MenuItem Disabled { get; } = new() { Header = "不可用的操作" };
        internal MenuItem Nested { get; } = new() { Header = "最近的文件(_R)" };
        internal ContextMenu ContextMenu { get; } = new();
        internal MenuItem ContextAction { get; } = new() { Header = "复制(_C)", InputGestureText = "Ctrl+C" };
        internal object CommandParameter { get; } = new();
        internal int CommandExecutions { get; private set; }
        internal object? LastCommandParameter { get; private set; }

        internal MenuFixture(bool dark)
        {
            _previousResources = Application.Current.Resources.MergedDictionaries.ToArray();
            _window = new Window
            {
                Width = 420, Height = 220, Left = -10000, Top = -10000,
                WindowStartupLocation = WindowStartupLocation.Manual,
                ShowActivated = false, ShowInTaskbar = false,
            };
            try
            {
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(_theme);
                ReplaceTheme(dark);
                var menu = new Menu { VerticalAlignment = VerticalAlignment.Top };
                menu.Items.Add(Root);
                menu.Items.Add(TopLevelAction);
                Action.Command = new RelayCommand(parameter =>
                {
                    CommandExecutions++;
                    LastCommandParameter = parameter;
                });
                Action.CommandParameter = CommandParameter;
                Disabled.Command = new RelayCommand(_ => CommandExecutions++, _ => false);
                Root.Items.Add(Action);
                Root.Items.Add(Checkable);
                Root.Items.Add(new Separator());
                Root.Items.Add(Disabled);
                Root.Items.Add(Nested);
                Nested.Items.Add(new MenuItem { Header = "最近检查项目.cvproj" });
                ContextMenu.Items.Add(ContextAction);
                ContextMenu.PlacementTarget = menu;
                menu.ContextMenu = ContextMenu;
                _window.Content = menu;
                _window.Show();
                PumpDispatcher();
                _window.UpdateLayout();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void ReplaceTheme(bool dark)
        {
            CloseMenus();
            _theme.MergedDictionaries.Clear();
            foreach (string path in new[]
            {
                $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                "/HandyControl;component/Themes/Theme.xaml",
                $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                "/ColorVision.Themes;component/Themes/Base.xaml",
                "/ColorVision.Themes;component/Themes/Menu.xaml",
                "/ColorVision.Themes;component/Themes/GroupBox.xaml",
                "/ColorVision.Themes;component/Themes/Icons.xaml",
                "/ColorVision.Themes;component/Themes/Window/BaseWindow.xaml",
            })
                _theme.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
            PumpDispatcher();
            _window.UpdateLayout();
        }

        internal void OpenRoot()
        {
            ContextMenu.IsOpen = false;
            Root.IsSubmenuOpen = true;
            PumpDispatcher();
            Root.UpdateLayout();
        }

        internal void OpenNested()
        {
            OpenRoot();
            Nested.IsSubmenuOpen = true;
            PumpDispatcher();
            Nested.UpdateLayout();
        }

        internal void OpenContextMenu()
        {
            CloseMenus();
            ContextMenu.IsOpen = true;
            PumpDispatcher();
            ContextMenu.UpdateLayout();
        }

        private void CloseMenus()
        {
            ContextMenu.IsOpen = false;
            Nested.IsSubmenuOpen = false;
            Root.IsSubmenuOpen = false;
            PumpDispatcher();
        }

        public void Dispose()
        {
            CloseMenus();
            _window.Content = null;
            _window.Close();
            PumpDispatcher();
            Application.Current.Resources.MergedDictionaries.Clear();
            foreach (ResourceDictionary resources in _previousResources)
                Application.Current.Resources.MergedDictionaries.Add(resources);
        }
    }
}
