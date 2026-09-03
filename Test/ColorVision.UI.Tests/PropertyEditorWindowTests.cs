using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Resources = ColorVision.UI.Properties.Resources;

namespace ColorVision.UI.Tests;

public sealed class PropertyEditorWindowTests
{
    [Fact]
    public void Title_UsesEditResourceAndTypeNameWhenNoDisplayNameExists()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());

            Assert.Equal($"{Resources.Edit} {nameof(SinglePropertyConfig)}", fixture.Window.Title);
        });
    }

    [Fact]
    public void Title_UsesTheObjectDisplayName()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new NamedConfig());

            Assert.Equal($"{Resources.Edit} Displayed configuration", fixture.Window.Title);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Title_ResolvesDisplayNameOrTypeNameThroughObjectResources(bool hasDisplayName)
    {
        WpfTestHost.Invoke(() =>
        {
            object config = hasDisplayName ? new LocalizedTitleConfig() : new LocalizedTypeNameConfig();
            Type type = config.GetType();
            string resourceKey = hasDisplayName ? "ConfigurationTitle" : type.Name;
            var resourceManager = new TestResourceManager(resourceKey, "Localized configuration");
            PropertyEditorHelper.GetResourceManager(type, resourceManager);
            try
            {
                using var fixture = new WindowFixture(config);

                Assert.Equal($"{Resources.Edit} Localized configuration", fixture.Window.Title);
            }
            finally
            {
                PropertyEditorHelper.ResourceManagerCache.TryRemove(type, out _);
            }
        });
    }

    [Fact]
    public void SingleDefaultCategory_HasNoDuplicateHeaderOrDecorationAndKeepsItsOnlyProperty()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());

            Border border = Assert.Single(fixture.RootBorders);
            AssertUnadornedRoot(border);
            Assert.Single(Assert.IsType<StackPanel>(border.Child).Children.OfType<DockPanel>());
            PropertyTreeNode node = Assert.Single(fixture.Window.TreeNodes);
            Assert.Equal(nameof(SinglePropertyConfig), node.Header);
            Assert.Same(border, node.AssociatedBorder);
            Assert.Equal(Visibility.Collapsed, fixture.Tree.Visibility);
        });
    }

    [Fact]
    public void ExplicitCategoryMatchingTypeName_StillHasItsHeaderAndDecoration()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new ExplicitCategoryConfig());

            AssertDecoratedRoot(Assert.Single(fixture.RootBorders), nameof(ExplicitCategoryConfig));
        });
    }

    [Fact]
    public void MultipleRootCategories_KeepBothHeadersAndTheNavigationTree()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new MultipleCategoryConfig());

            Assert.Equal(2, fixture.RootBorders.Count());
            Assert.All(fixture.RootBorders, border => AssertDecoratedRoot(border, Assert.IsType<string>(border.Tag)));
            Assert.Equal(2, fixture.Window.TreeNodes.Count);
            Assert.Equal(Visibility.Visible, fixture.Tree.Visibility);

            fixture.Search.Text = nameof(MultipleCategoryConfig.Name);

            Border visibleRoot = Assert.Single(fixture.RootBorders.Where(border => border.Visibility == Visibility.Visible));
            AssertDecoratedRoot(visibleRoot, nameof(MultipleCategoryConfig));
        });
    }

    [Fact]
    public void SingleDefaultRoot_KeepsNestedCategoryHeaderDecorationAndNavigation()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new NestedConfig());

            Border root = Assert.Single(fixture.RootBorders);
            AssertUnadornedRoot(root);
            PropertyTreeNode childNode = Assert.Single(Assert.Single(fixture.Window.TreeNodes).Children);
            Assert.Equal(nameof(NestedConfig.FileServerCfg), childNode.Header);
            Assert.Equal(Visibility.Visible, fixture.Tree.Visibility);
            Border nestedBorder = Assert.Single(Descendants<Border>(root).Where(border => Equals(border.Tag, nameof(FileServerCfg))));
            Assert.Equal(new Thickness(1), nestedBorder.BorderThickness);
            Assert.Contains(Descendants<TextBlock>(nestedBorder), text => text.Text == nameof(FileServerCfg));

            fixture.Search.Text = nameof(FileServerCfg.Endpoint);

            Assert.Equal(Visibility.Visible, root.Visibility);
            Assert.Equal(Visibility.Visible, nestedBorder.Visibility);
            Assert.True(childNode.IsVisible);
            Assert.Equal(Visibility.Visible, fixture.Tree.Visibility);
        });
    }

    [Theory]
    [InlineData(nameof(SinglePropertyConfig.Value))]
    [InlineData("Displayed value")]
    [InlineData("Searchable description")]
    [InlineData(nameof(SinglePropertyConfig))]
    public void Search_KeepsAHeaderlessSinglePropertyRootWhenItsMetadataMatches(string query)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            fixture.Search.Text = "no matching property";
            Assert.Equal(Visibility.Collapsed, Assert.Single(fixture.RootBorders).Visibility);
            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);

            fixture.Search.Text = query;

            Border root = Assert.Single(fixture.RootBorders);
            AssertUnadornedRoot(root);
            Assert.Equal(Visibility.Visible, root.Visibility);
            Assert.Equal(Visibility.Visible, fixture.ValueEditor.Visibility);
            Assert.True(Assert.Single(fixture.Window.TreeNodes).IsVisible);
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
        });
    }

    [Fact]
    public void Search_ExposesAnAccessibleClearButtonAndKeepsEmptyStateOutsideGeneratedProperties()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            var clearButton = Assert.IsType<Button>(fixture.Window.FindName("SearchClearButton"));
            Assert.Equal(Resources.PropEditor_ClearSearch, System.Windows.Automation.AutomationProperties.GetName(clearButton));
            Assert.Equal(Visibility.Collapsed, clearButton.Visibility);
            Assert.DoesNotContain(fixture.EmptyState, Descendants<FrameworkElement>(fixture.PropertyPanel));
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);

            fixture.Search.Text = "no matching property";

            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);
            Assert.Equal(Visibility.Visible, clearButton.Visibility);
            Assert.All(fixture.RootBorders, border => Assert.Equal(Visibility.Collapsed, border.Visibility));
            Assert.Equal(new Thickness(0), fixture.EditorSurface.Margin);
            fixture.Search.Clear();
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
            Assert.Equal(Visibility.Collapsed, clearButton.Visibility);
            Assert.Equal(Visibility.Visible, Assert.Single(fixture.RootBorders).Visibility);
        });
    }

    [Fact]
    public void Search_HasReadableSizingAndANoninteractivePlaceholderThatTracksInput()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            var placeholder = Assert.IsType<TextBlock>(fixture.Window.FindName("SearchPlaceholder"));
            var searchBar = Assert.IsType<Grid>(fixture.Search.Parent);
            Viewbox icon = Assert.Single(searchBar.Children.OfType<Viewbox>());
            Assert.Equal(34, fixture.Search.Height);
            Assert.Equal(34, fixture.Search.MinHeight);
            Assert.Equal(14, fixture.Search.FontSize);
            Assert.Equal(14, icon.Width);
            Assert.Equal(14, icon.Height);
            Assert.False(icon.IsHitTestVisible);
            Assert.Equal(Resources.PropEditor_SearchPlaceholder, placeholder.Text);
            Assert.Same(fixture.Window.FindResource("SecondaryTextBrush"), placeholder.Foreground);
            Assert.False(placeholder.IsHitTestVisible);
            Assert.Equal(DependencyProperty.UnsetValue, fixture.Search.ReadLocalValue(HandyControl.Controls.InfoElement.PlaceholderProperty));
            Assert.Equal(Visibility.Visible, placeholder.Visibility);

            fixture.Search.Text = nameof(SinglePropertyConfig.Value);

            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            fixture.Search.Text = " ";
            Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
            fixture.Search.Clear();
            Assert.Equal(Visibility.Visible, placeholder.Visibility);
        });
    }

    [Fact]
    public void SearchTemplate_RendersItsFullHeightRoundedBorderAndEditableContentHost()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            fixture.ShowOffscreenForFocus();
            TextBox search = Assert.IsType<TextBox>(fixture.Search);
            Assert.Same(fixture.Window.FindResource("PropertyEditorSearchBox"), search.Style);
            Assert.Same(fixture.Window.FindResource("TextBoxBaseStyle"), search.Style.BasedOn);
            Border border = Assert.IsType<Border>(search.Template.FindName("SearchBorder", search));
            ScrollViewer contentHost = Assert.IsType<ScrollViewer>(search.Template.FindName("PART_ContentHost", search));

            Assert.Equal(34, search.ActualHeight);
            Assert.Equal(search.ActualHeight, border.ActualHeight);
            Assert.Equal(new CornerRadius(5), border.CornerRadius);
            Assert.Equal(new Thickness(1), border.BorderThickness);
            Assert.True(contentHost.ActualWidth > 0 && contentHost.ActualHeight > 0);
            Assert.Equal(new Thickness(0), contentHost.Margin);
            Assert.Equal(new Thickness(0), fixture.EditorSurface.Margin);
            var placeholder = Assert.IsType<TextBlock>(fixture.Window.FindName("SearchPlaceholder"));
            double placeholderLeft = placeholder.TranslatePoint(new Point(), search).X;
            search.Text = nameof(SinglePropertyConfig.Value);
            fixture.Window.UpdateLayout();
            Rect firstCharacter = search.GetRectFromCharacterIndex(0);
            Assert.False(firstCharacter.IsEmpty);
            Assert.InRange(Math.Abs(firstCharacter.X - (search.Padding.Left + search.BorderThickness.Left)), 0, 2);
            Assert.InRange(Math.Abs(firstCharacter.X - placeholderLeft), 0, search.BorderThickness.Left + 0.1);
            Assert.Equal(Visibility.Visible, Assert.Single(fixture.RootBorders).Visibility);
        });
    }

    [Fact]
    public void NavigationTemplate_PreservesBindingsAndUsesQuietSelectionWithAKeyboardFocusOutline()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new NestedConfig());
            fixture.ShowOffscreenForFocus();
            PropertyTreeNode node = Assert.Single(fixture.Window.TreeNodes);
            TreeViewItem item = Assert.IsType<TreeViewItem>(fixture.Tree.ItemContainerGenerator.ContainerFromItem(node));
            Border header = Assert.IsType<Border>(item.Template.FindName("Bd", item));
            Border indicator = Assert.IsType<Border>(item.Template.FindName("SelectionIndicator", item));
            Assert.Equal(new Thickness(8, 0, 0, 0), fixture.EditorSurface.Margin);

            Assert.Equal(nameof(PropertyTreeNode.IsSelected), BindingOperations.GetBinding(item, TreeViewItem.IsSelectedProperty)?.Path.Path);
            Assert.Equal(BindingMode.TwoWay, BindingOperations.GetBinding(item, TreeViewItem.IsSelectedProperty)?.Mode);
            Assert.Equal(nameof(PropertyTreeNode.IsExpanded), BindingOperations.GetBinding(item, TreeViewItem.IsExpandedProperty)?.Path.Path);
            Assert.Equal(BindingMode.TwoWay, BindingOperations.GetBinding(item, TreeViewItem.IsExpandedProperty)?.Mode);
            Assert.Equal(nameof(PropertyTreeNode.ContextMenu), BindingOperations.GetBinding(item, FrameworkElement.ContextMenuProperty)?.Path.Path);
            Assert.Same(node.ContextMenu, item.ContextMenu);
            Assert.Equal(26, item.MinHeight);
            Assert.Equal(new Thickness(4, 0, 4, 0), header.Padding);
            Assert.Equal(new Thickness(1), header.BorderThickness);
            Assert.Equal(new CornerRadius(4), header.CornerRadius);
            Assert.Equal(Visibility.Collapsed, indicator.Visibility);
            Assert.Equal(3, indicator.Width);
            Assert.Null(item.FocusVisualStyle);

            node.IsSelected = true;
            fixture.Window.UpdateLayout();

            Assert.True(item.IsSelected);
            Assert.Same(fixture.Window.FindResource("SecondaryRegionBrush"), header.Background);
            Assert.Same(fixture.Window.FindResource("GlobalTextBrush"), item.Foreground);
            Assert.Same(fixture.Window.FindResource("PrimaryBrush"), indicator.Background);
            Assert.Equal(Visibility.Visible, indicator.Visibility);
            Assert.False(fixture.Window.IsActive);
            Assert.Equal(0.55, indicator.Opacity);
            node.IsExpanded = false;
            Assert.False(item.IsExpanded);
            item.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
            Assert.True(node.IsExpanded);

            // Verify the keyboard-only trigger without activating the hidden host.
            Trigger focus = Assert.Single(item.Template.Triggers.OfType<Trigger>(), trigger =>
                trigger.Property == UIElement.IsKeyboardFocusedProperty && Equals(trigger.Value, true));
            Setter outline = Assert.Single(focus.Setters.OfType<Setter>(), setter =>
                setter.TargetName == "Bd" && setter.Property == Border.BorderBrushProperty);
            Assert.Equal("PrimaryBrush", Assert.IsType<DynamicResourceExtension>(outline.Value).ResourceKey);
            item.SetCurrentValue(TreeViewItem.IsSelectedProperty, false);
            Assert.False(node.IsSelected);
            Assert.Equal(Visibility.Collapsed, indicator.Visibility);
            fixture.Search.Text = "no matching property";
            fixture.Window.UpdateLayout();
            Assert.Equal(new Thickness(0), fixture.EditorSurface.Margin);
            fixture.Search.Clear();
            fixture.Window.UpdateLayout();
            Assert.Equal(new Thickness(8, 0, 0, 0), fixture.EditorSurface.Margin);
        });
    }

    [Fact]
    public void Search_EmptyObjectStillShowsAnEmptyResultForANonemptyQuery()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new EmptyConfig());
            Assert.Empty(fixture.RootBorders);
            Assert.Empty(fixture.Window.TreeNodes);
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);

            fixture.Search.Text = "anything";

            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);
            fixture.Search.Text = "   ";
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
        });
    }

    [Fact]
    public void DisplayProperties_ReappliesTheSearchAndEmptyStateWhenRebuilt()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            fixture.Search.Text = "no matching property";

            fixture.Window.DisplayProperties(fixture.Window.EditConfig);

            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);
            Assert.Equal(Visibility.Collapsed, Assert.Single(fixture.RootBorders).Visibility);
            fixture.Search.Text = nameof(SinglePropertyConfig.Value);
            fixture.Window.DisplayProperties(fixture.Window.EditConfig);
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
            Assert.Equal(Visibility.Visible, Assert.Single(fixture.RootBorders).Visibility);
        });
    }

    [Theory]
    [InlineData("EmptySearchClearButton")]
    [InlineData("SearchClearButton")]
    public void SearchClearButtons_ClearTheQueryAndReturnFocusToSearch(string buttonName)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            fixture.ShowOffscreenForFocus();
            fixture.Search.Text = "no matching property";
            var clearButton = Assert.IsType<Button>(fixture.Window.FindName(buttonName));
            FocusManager.SetFocusedElement(fixture.Window, clearButton);

            clearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Empty(fixture.Search.Text);
            Assert.Same(fixture.Search, FocusManager.GetFocusedElement(fixture.Window));
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
            Assert.Equal(Visibility.Visible, Assert.Single(fixture.RootBorders).Visibility);
        });
    }

    [Theory]
    [InlineData(PropertySortMode.Default)]
    [InlineData(PropertySortMode.NameAscending)]
    [InlineData(PropertySortMode.NameDescending)]
    [InlineData(PropertySortMode.CategoryAscending)]
    [InlineData(PropertySortMode.CategoryDescending)]
    public void Sorting_PreservesTheHeaderlessSinglePropertyRootAndActiveFilter(PropertySortMode sortMode)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            // Move away from Default first so that selecting Default also rebuilds.
            fixture.Sort.SelectedIndex = 1;
            fixture.Search.Text = "no matching property";

            fixture.Sort.SelectedItem = fixture.Sort.Items.Cast<ComboBoxItem>().Single(item => Equals(item.Tag, sortMode));

            Assert.Equal(Visibility.Collapsed, Assert.Single(fixture.RootBorders).Visibility);
            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);
            fixture.Search.Clear();
            Border root = Assert.Single(fixture.RootBorders);
            AssertUnadornedRoot(root);
            Assert.Equal(Visibility.Visible, root.Visibility);
            Assert.Single(Assert.IsType<StackPanel>(root.Child).Children.OfType<DockPanel>());
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetActions_ClearSearchAndKeepTheOnlyProperty(bool restoreDefaults)
    {
        WpfTestHost.Invoke(() =>
        {
            var config = new SinglePropertyConfig { Value = "Opened value" };
            using var fixture = new WindowFixture(config);
            fixture.EditValue("Edited value");
            fixture.Search.Text = "no matching property";
            Assert.Equal(Visibility.Visible, fixture.EmptyState.Visibility);

            fixture.Click(restoreDefaults ? Resources.PropEditor_ResetToDefault : Resources.Reset);

            Assert.Empty(fixture.Search.Text);
            Assert.Equal(restoreDefaults ? "Default value" : "Opened value", config.Value);
            Border root = Assert.Single(fixture.RootBorders);
            AssertUnadornedRoot(root);
            Assert.Equal(Visibility.Visible, root.Visibility);
            Assert.Equal(config.Value, fixture.ValueEditor.Text);
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
        });
    }

    [Fact]
    public void FindCommand_HasControlFBindingAndFocusesAndSelectsTheSearchText()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            Assert.Contains(fixture.Window.InputBindings.OfType<KeyBinding>(), binding =>
                binding.Key == Key.F && binding.Modifiers == ModifierKeys.Control && binding.Command == ApplicationCommands.Find);
            Assert.Equal(Visibility.Visible, fixture.Search.Visibility);
            fixture.ShowOffscreenForFocus();
            fixture.Search.Text = "find this value";
            fixture.Search.Select(3, 0);
            FocusManager.SetFocusedElement(fixture.Window, fixture.ValueEditor);

            Assert.True(ApplicationCommands.Find.CanExecute(null, fixture.ValueEditor));
            ApplicationCommands.Find.Execute(null, fixture.ValueEditor);

            // Focus() requires a visible presentation source. This transparent,
            // off-screen host is shown without activating a foreground window.
            Assert.Same(fixture.Search, FocusManager.GetFocusedElement(fixture.Window));
            Assert.Equal(fixture.Search.Text, fixture.Search.SelectedText);
        });
    }

    [Theory]
    [InlineData("no matching property")]
    [InlineData("")]
    public void EscapeInSearch_ClearsTheFilterAndIsHandledWithoutClosing(string query)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            bool closed = false;
            fixture.Window.Closed += (_, _) => closed = true;
            fixture.Search.Text = query;

            KeyEventArgs key = fixture.RaisePreviewKeyDown(fixture.Search, Key.Escape);

            Assert.True(key.Handled);
            Assert.Empty(fixture.Search.Text);
            Assert.Equal(Visibility.Visible, Assert.Single(fixture.RootBorders).Visibility);
            Assert.Equal(Visibility.Collapsed, fixture.EmptyState.Visibility);
            Assert.False(closed);
        });
    }

    [Fact]
    public void EscapeInAPropertyEditor_IsNotConsumedBySearch()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            fixture.Search.Text = nameof(SinglePropertyConfig.Value);

            KeyEventArgs key = fixture.RaisePreviewKeyDown(fixture.ValueEditor, Key.Escape);

            Assert.False(key.Handled);
            Assert.Equal(nameof(SinglePropertyConfig.Value), fixture.Search.Text);
        });
    }

    [Fact]
    public void Footer_EmphasizesOnlyConfirmAndKeepsCommonActionSizingWithoutDefaultKeys()
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(new SinglePropertyConfig());
            var actionStyle = Assert.IsType<Style>(fixture.Window.FindResource("PropertyEditorActionButton"));
            Assert.Same(fixture.Window.FindResource("ButtonDefault"), actionStyle.BasedOn);
            Button confirm = Assert.IsType<Button>(fixture.Window.FindName("ConfirmButton"));
            Brush primaryBrush = Assert.IsAssignableFrom<Brush>(fixture.Window.FindResource("PrimaryBrush"));
            Assert.Same(primaryBrush, confirm.Background);

            Button[] secondaryButtons = [
                Assert.IsType<Button>(fixture.Window.FindName("ResetButton")),
                Assert.IsType<Button>(fixture.Window.FindName("ResetDefaultsButton")),
                Assert.IsType<Button>(fixture.Window.FindName("CancelButton")),
            ];
            Assert.All(secondaryButtons, button =>
            {
                Assert.Same(actionStyle, button.Style);
                Assert.NotSame(primaryBrush, button.Background);
                Assert.Equal(confirm.MinHeight, button.MinHeight);
                Assert.Equal(confirm.Padding, button.Padding);
                Assert.Equal(confirm.Margin, button.Margin);
                Assert.False(button.IsDefault);
                Assert.False(button.IsCancel);
            });
            Assert.True(double.IsFinite(confirm.MinHeight) && confirm.MinHeight > 0);
            Assert.False(confirm.IsDefault);
            Assert.False(confirm.IsCancel);
        });
    }

    [Theory]
    [InlineData(typeof(DirectRowOnlyConfig), 0)]
    [InlineData(typeof(MixedRowsConfig), 6)]
    [InlineData(typeof(ExplicitMixedRowsConfig), 0)]
    [InlineData(typeof(MultipleMixedRowsConfig), 0)]
    public void DirectRowInset_OnlyPadsHeaderlessRootsContainingNestedCards(Type configType, double expectedInset)
    {
        WpfTestHost.Invoke(() =>
        {
            using var fixture = new WindowFixture(Activator.CreateInstance(configType, nonPublic: true)!);
            // The shared row generator owns the baseline (0, 0, 0, 5) margin.
            Thickness expectedMargin = new(expectedInset, 0, expectedInset, 5);

            Assert.Equal(expectedMargin, FindRow().Margin);
            fixture.Sort.SelectedIndex = 1;
            Assert.Equal(expectedMargin, FindRow().Margin);
            fixture.Window.DisplayProperties(fixture.Window.EditConfig);
            Assert.Equal(expectedMargin, FindRow().Margin);

            DockPanel FindRow() => fixture.RootBorders
                .SelectMany(border => Assert.IsType<StackPanel>(border.Child).Children.OfType<DockPanel>())
                .Single(panel => panel.Tag is PropertyInfo property && property.Name == nameof(DirectRowOnlyConfig.Value));
        });
    }

    [Theory]
    [InlineData(PropertyEditorEditMode.Immediate)]
    [InlineData(PropertyEditorEditMode.Transactional)]
    public void Confirm_CommitsBeforeSubmittedAndCloses(PropertyEditorEditMode mode)
    {
        WpfTestHost.Invoke(() =>
        {
            var config = new SinglePropertyConfig();
            using var fixture = new WindowFixture(config, mode);
            int submitted = 0;
            bool closed = false;
            fixture.Window.Closed += (_, _) => closed = true;
            fixture.Window.Submitted += (_, _) =>
            {
                submitted++;
                Assert.Equal("Edited value", config.Value);
                Assert.False(closed);
            };
            fixture.EditValue("Edited value");
            Assert.Equal(mode == PropertyEditorEditMode.Immediate ? "Edited value" : "Default value", config.Value);

            fixture.Click(Resources.OK);

            Assert.Equal(1, submitted);
            Assert.True(closed);
            Assert.Equal("Edited value", config.Value);
        });
    }

    [Theory]
    [InlineData(PropertyEditorEditMode.Immediate)]
    [InlineData(PropertyEditorEditMode.Transactional)]
    public void Close_DoesNotSubmitOrChangeTheExistingEditModeSemantics(PropertyEditorEditMode mode)
    {
        WpfTestHost.Invoke(() =>
        {
            var config = new SinglePropertyConfig();
            using var fixture = new WindowFixture(config, mode);
            bool submitted = false;
            bool closed = false;
            fixture.Window.Submitted += (_, _) => submitted = true;
            fixture.Window.Closed += (_, _) => closed = true;
            fixture.EditValue("Edited value");
            Button cancel = Assert.IsType<Button>(fixture.Window.FindName("CancelButton"));
            Assert.Equal(mode == PropertyEditorEditMode.Immediate ? Resources.Close : Resources.Cancel, cancel.Content);

            cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.False(submitted);
            Assert.True(closed);
            Assert.Equal(mode == PropertyEditorEditMode.Immediate ? "Edited value" : "Default value", config.Value);
        });
    }

    private static void AssertUnadornedRoot(Border border)
    {
        Assert.Equal(new Thickness(0), border.BorderThickness);
        Assert.True(border.Background == null || border.Background is SolidColorBrush { Color.A: 0 });
        Assert.Empty(Assert.IsType<StackPanel>(border.Child).Children.OfType<TextBlock>());
    }

    private static void AssertDecoratedRoot(Border border, string category)
    {
        Assert.Equal(new Thickness(1), border.BorderThickness);
        Assert.NotNull(border.Background);
        Assert.Equal(category, Assert.Single(Assert.IsType<StackPanel>(border.Child).Children.OfType<TextBlock>()).Text);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (DependencyObject child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (T descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class WindowFixture : IDisposable
    {
        private readonly Dictionary<string, object?> _originalResources = new();
        public PropertyEditorWindow Window { get; }
        public TextBox Search => Assert.IsAssignableFrom<TextBox>(Window.FindName("SearchBox"));
        public ComboBox Sort => Assert.IsType<ComboBox>(Window.FindName("SortComboBox"));
        public TreeView Tree => Assert.IsType<TreeView>(Window.FindName("treeView"));
        public StackPanel PropertyPanel => Assert.IsType<StackPanel>(Window.FindName("PropertyPanel"));
        public FrameworkElement EmptyState => Assert.IsAssignableFrom<FrameworkElement>(Window.FindName("SearchEmptyState"));
        public Border EditorSurface => Assert.IsType<Border>(Window.FindName("EditorSurface"));
        public IEnumerable<Border> RootBorders => PropertyPanel.Children.OfType<Border>();
        public TextBox ValueEditor => Assert.Single(Descendants<DockPanel>(Assert.Single(RootBorders))
            .Single(panel => panel.Tag is PropertyInfo property && property.Name == nameof(SinglePropertyConfig.Value))
            .Children.OfType<TextBox>());

        public WindowFixture(object config, PropertyEditorEditMode mode = PropertyEditorEditMode.Immediate)
        {
            ResourceDictionary resources = Application.Current.Resources;
            foreach ((string key, object value) in new Dictionary<string, object>
            {
                ["GlobalTextBrush"] = Brushes.Black,
                ["GlobalBackground"] = Brushes.White,
                ["SecondaryTextBrush"] = Brushes.DimGray,
                ["SecondaryRegionBrush"] = Brushes.Gainsboro,
                ["GlobalBorderBrush"] = Brushes.LightGray,
                ["BorderBrush"] = Brushes.Gray,
                ["PrimaryBrush"] = Brushes.DodgerBlue,
                ["ButtonDefault"] = new Style(typeof(Button)),
                ["ButtonCommand"] = new Style(typeof(Button)),
                ["TreeViewItemBaseStyle"] = new Style(typeof(TreeViewItem)),
                ["ComboBox.Small"] = new Style(typeof(ComboBox)),
                ["TextBox.Small"] = new Style(typeof(TextBox)),
                ["TextBoxBaseStyle"] = new Style(typeof(TextBox)),
                ["bool2VisibilityConverter"] = new BooleanToVisibilityConverter(),
            })
            {
                _originalResources[key] = resources.Keys.Cast<object>().Contains(key) ? resources[key] : null;
                resources[key] = value;
            }
            try
            {
                Window = new PropertyEditorWindow(config, mode) { ShowInTaskbar = false, ShowActivated = false };
            }
            catch
            {
                RestoreResources();
                throw;
            }
        }

        public void Click(string content) => Descendants<Button>(Window).Single(button => Equals(button.Content, content))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        public void ShowOffscreenForFocus()
        {
            Window.Left = -10000;
            Window.Top = -10000;
            Window.Opacity = 0;
            Window.WindowStyle = WindowStyle.None;
            Window.Show();
            Window.UpdateLayout();
        }

        public void EditValue(string value)
        {
            TextBox editor = ValueEditor;
            editor.Text = value;
            // Text editors normally flush on LostFocus. Flush explicitly because
            // this isolated window never receives real foreground keyboard focus.
            editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        }

        public KeyEventArgs RaisePreviewKeyDown(UIElement target, Key key)
        {
            IntPtr handle = new WindowInteropHelper(Window).EnsureHandle();
            var arguments = new KeyEventArgs(Keyboard.PrimaryDevice, HwndSource.FromHwnd(handle), Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            };
            target.RaiseEvent(arguments);
            return arguments;
        }

        public void Dispose()
        {
            Window.Close();
            RestoreResources();
        }

        private void RestoreResources()
        {
            foreach ((string key, object? value) in _originalResources)
            {
                if (value == null) Application.Current.Resources.Remove(key);
                else Application.Current.Resources[key] = value;
            }
        }
    }

    private sealed class TestResourceManager(string key, string value) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => name == key ? value : null;
    }

    private sealed class SinglePropertyConfig
    {
        [DisplayName("Displayed value")]
        [Description("Searchable description")]
        public string Value { get; set; } = "Default value";
    }

    [DisplayName("Displayed configuration")]
    private sealed class NamedConfig
    {
        public string Value { get; set; } = "Value";
    }

    [DisplayName("ConfigurationTitle")]
    private sealed class LocalizedTitleConfig
    {
        public string Value { get; set; } = "Value";
    }

    private sealed class LocalizedTypeNameConfig
    {
        public string Value { get; set; } = "Value";
    }

    private sealed class ExplicitCategoryConfig
    {
        [Category(nameof(ExplicitCategoryConfig))]
        public string Value { get; set; } = "Value";
    }

    private sealed class MultipleCategoryConfig
    {
        public string Name { get; set; } = "Name";

        [Category("Network")]
        public string Address { get; set; } = "Address";
    }

    private sealed class NestedConfig
    {
        public FileServerCfg FileServerCfg { get; set; } = new();
    }

    private sealed class FileServerCfg
    {
        public string Endpoint { get; set; } = "Test endpoint";
    }

    private sealed class EmptyConfig { }

    private class DirectRowOnlyConfig
    {
        public string Value { get; set; } = "Value";
    }

    private class MixedRowsConfig : DirectRowOnlyConfig
    {
        public FileServerCfg FileServerCfg { get; set; } = new();
    }

    private sealed class MultipleMixedRowsConfig : MixedRowsConfig
    {
        [Category("Other")]
        public string OtherValue { get; set; } = "Other value";
    }

    private sealed class ExplicitMixedRowsConfig
    {
        [Category("Network")]
        public string Value { get; set; } = "Value";

        [Category("Network")]
        public FileServerCfg FileServerCfg { get; set; } = new();
    }
}
