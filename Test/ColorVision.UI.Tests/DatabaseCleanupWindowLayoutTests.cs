using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.DatabaseCleanup;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class DatabaseCleanupWindowLayoutTests
{
    [Fact]
    public void SocketEntryPointCreatesOnlyTheSocketSourceWithoutLoadingDatabaseData()
    {
        // Construct the same provider and scoped view model as the production launcher; never load the real database.
        SocketMessagesSqliteCleanupProvider provider = SocketDatabaseCleanupWindowLauncher.CreateSourceProvider();
        DatabaseCleanupWindowViewModel viewModel = DatabaseCleanupWindow.CreateViewModel(provider);

        DatabaseCleanupSourceViewModel source = Assert.Single(viewModel.Sources);
        Assert.Equal("socketmessages-sqlite", source.SourceId);
        Assert.Same(source, viewModel.SelectedSource);
        Assert.True(viewModel.IsSourceScoped);
        Assert.False(viewModel.ShowSourceNavigation);
        Assert.Contains(source.DisplayName, viewModel.WindowTitle);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ScopeDescription));
        Assert.Empty(source.Tables);
        Assert.False(source.SupportsTableCleanup);
        Assert.True(source.SupportsBackup);
        Assert.True(source.SupportsMigration);
    }

    [Fact]
    public void ScopedSourcesAreReadOnlyAndRejectSourcesOutsideTheWindow()
    {
        FakeSocketProvider socket = new();
        DatabaseCleanupWindowViewModel viewModel = new([socket], isSourceScoped: true);
        DatabaseCleanupSourceViewModel other = new(new FakeSourceProvider("unrelated", "Unrelated database", 0));

        Assert.True(((IList)viewModel.Sources).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((IList)viewModel.Sources).Add(other));
        Assert.Throws<ArgumentException>(() => viewModel.SelectedSource = other);
        Assert.Same(Assert.Single(viewModel.Sources), viewModel.SelectedSource);
        Assert.Throws<ArgumentException>(() => new DatabaseCleanupWindowViewModel([], isSourceScoped: true));
        Assert.Throws<ArgumentException>(() => new DatabaseCleanupWindowViewModel([socket, new FakeSelectionProvider()], isSourceScoped: true));
        Assert.Equal(0, socket.LoadCalls);
    }

    [Fact]
    public void ScopedRefreshLoadsOnlyItsInjectedProvider()
    {
        WithEnvironment(() =>
        {
            FakeSocketProvider socket = new();
            FakeSelectionProvider unrelated = new();
            DatabaseCleanupWindowViewModel viewModel = new([socket], isSourceScoped: true);

            CompleteWithDispatcher(viewModel.RefreshAllAsync());

            Assert.Equal(1, socket.LoadCalls);
            Assert.Equal(0, unrelated.LoadCalls);
            Assert.Equal("SocketMessage", Assert.Single(viewModel.SelectedSource!.Tables).TableName);
        });
    }

    [Fact]
    public void GlobalNavigationSortsSourcesAndRefreshesTheSelectedFakeOnly()
    {
        FakeSourceProvider zeta = new("zeta", "Zeta", 30);
        FakeSourceProvider beta = new("beta", "Beta", 10);
        FakeSourceProvider alpha = new("alpha", "Alpha", 10);
        WithWindow([zeta, beta, alpha], false, (window, viewModel) =>
        {
            Assert.False(viewModel.IsSourceScoped);
            Assert.True(viewModel.ShowSourceNavigation);
            Assert.Equal(["alpha", "beta", "zeta"], viewModel.Sources.Select(source => source.SourceId));
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "SourceNavigationPane").Visibility);
            Assert.Equal(0, alpha.LoadCalls + beta.LoadCalls + zeta.LoadCalls);

            CompleteWithDispatcher(viewModel.SelectedSource!.RefreshAsync());
            Assert.Equal(1, alpha.LoadCalls);
            Assert.Equal(0, beta.LoadCalls);
            Assert.Equal(0, zeta.LoadCalls);

            Element<ListBox>(window, "SourceListBox").SelectedItem = viewModel.Sources[2];
            RefreshLayout(window);
            Assert.Same(viewModel.Sources[2], viewModel.SelectedSource);
            Assert.Same(viewModel.SelectedSource, Element<ContentControl>(window, "SelectedSourceContent").Content);

            CompleteWithDispatcher(viewModel.SelectedSource!.RefreshAsync());
            Assert.Equal(1, alpha.LoadCalls);
            Assert.Equal(0, beta.LoadCalls);
            Assert.Equal(1, zeta.LoadCalls);
        });
    }

    [Fact]
    public void SocketWorkspaceHidesSelectionAndKeepsBackupMigrationAndDangerControlsScoped()
    {
        WithWindow([new FakeSocketProvider()], true, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            RefreshLayout(window);

            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "SourceNavigationPane").Visibility);
            Assert.Single(Element<ListBox>(window, "SourceListBox").Items);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "SelectionToolbar").Visibility);
            Assert.Equal(Visibility.Collapsed, Element<DataGrid>(window, "CleanupTablesGrid").Columns[0].Visibility);
            Assert.True(IsEffectivelyVisible(Element<Button>(window, "CreateBackupButton")));
            Assert.True(IsEffectivelyVisible(Element<Button>(window, "MigrationButton")));
            Assert.True(IsEffectivelyVisible(Element<CheckBox>(window, "BackupBeforeCleanupCheckBox")));
            Assert.False(Element<Expander>(window, "DangerZoneExpander").IsExpanded);

            Assert.DoesNotContain(VisualDescendants(Element<FrameworkElement>(window, "SourceWorkspace")).OfType<TextBlock>(),
                text => IsEffectivelyVisible(text) && text.Text == viewModel.SelectedSource!.SelectionSummary);
            Element<Expander>(window, "DangerZoneExpander").IsExpanded = true;
            RefreshLayout(window);
            Assert.False(IsEffectivelyVisible(Element<Button>(window, "CleanupSelectedButton")));
            Assert.True(IsEffectivelyVisible(Element<Button>(window, "CleanupAllButton")));
        });
    }

    [Fact]
    public void ChangingGlobalSourcesUpdatesTableSelectionAndOptionalCapabilities()
    {
        WithWindow([new FakeSelectionProvider(), new FakeSocketProvider()], false, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            RefreshLayout(window);
            Assert.Equal(Visibility.Visible, Element<DataGrid>(window, "CleanupTablesGrid").Columns[0].Visibility);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "SelectionToolbar").Visibility);
            Assert.False(IsEffectivelyVisible(Element<Button>(window, "MigrationButton")));
            Assert.False(Element<Expander>(window, "DangerZoneExpander").IsExpanded);

            Element<ListBox>(window, "SourceListBox").SelectedItem = viewModel.Sources.Single(source => source.SourceId == "socketmessages-sqlite");
            RefreshLayout(window);
            Assert.Equal(Visibility.Collapsed, Element<DataGrid>(window, "CleanupTablesGrid").Columns[0].Visibility);
            Assert.Equal(Visibility.Collapsed, Element<FrameworkElement>(window, "SelectionToolbar").Visibility);
            Assert.True(IsEffectivelyVisible(Element<Button>(window, "MigrationButton")));
            Assert.False(Element<Expander>(window, "DangerZoneExpander").IsExpanded);
        });
    }

    [Fact]
    public void EmptySourceShowsAnExplicitEmptyStateAndDisablesCleanup()
    {
        WithWindow([new FakeSourceProvider("empty", "Empty source", 0, empty: true)], true, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            RefreshLayout(window);

            Assert.Empty(Element<DataGrid>(window, "CleanupTablesGrid").Items);
            Assert.Equal(Visibility.Visible, Element<FrameworkElement>(window, "EmptyTablesPanel").Visibility);
            Assert.False(Element<Button>(window, "HistoryCleanupButton").IsEnabled);
            Assert.False(viewModel.SelectedSource!.CleanupAllCommand.CanExecute(null));
            Assert.False(viewModel.SelectedSource.CleanupSelectedCommand.CanExecute(null));
            Assert.False(string.IsNullOrWhiteSpace(Element<TextBlock>(window, "CleanupStatusText").Text));
            Assert.False(IsEffectivelyVisible(Element<Button>(window, "CreateBackupButton")));
            Assert.False(IsEffectivelyVisible(Element<Button>(window, "MigrationButton")));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BusyStateDisablesMaintenanceAndDangerActionsWithoutExecutingThem(bool socket)
    {
        IDatabaseCleanupSourceProvider provider = socket ? new FakeSocketProvider() : new FakeSelectionProvider();
        WithWindow([provider], true, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            DatabaseCleanupSourceViewModel source = viewModel.SelectedSource!;
            if (source.SupportsTableCleanup)
                source.SelectAllCommand.Execute(null);
            Element<Expander>(window, "DangerZoneExpander").IsExpanded = true;
            RefreshLayout(window);
            Assert.True(source.CleanupHistoryCommand.CanExecute(null));
            Assert.True(source.CleanupAllCommand.CanExecute(null));

            source.IsBusy = true;
            CommandManager.InvalidateRequerySuggested();
            RefreshLayout(window);

            foreach (string buttonName in new[] { "HistoryCleanupButton", "CleanupAllButton", "CreateBackupButton", "MigrationButton", "CleanupSelectedButton" })
            {
                Button button = Element<Button>(window, buttonName);
                Assert.False(button.Command.CanExecute(button.CommandParameter));
                if (IsEffectivelyVisible(button))
                    Assert.False(button.IsEnabled, $"{buttonName} must be disabled during maintenance.");
            }
            Assert.False(Element<TextBox>(window, "KeepMonthsTextBox").IsEnabled);
            Assert.False(Element<CheckBox>(window, "BackupBeforeCleanupCheckBox").IsEnabled);
            // No cleanup, backup, migration, network, or clipboard action is invoked by these tests.
        });
    }

    [Theory]
    [InlineData(false, "zh-CN", false)]
    [InlineData(false, "en-US", false)]
    [InlineData(false, "zh-CN", true)]
    [InlineData(false, "en-US", true)]
    [InlineData(true, "zh-CN", false)]
    [InlineData(true, "en-US", false)]
    [InlineData(true, "zh-CN", true)]
    [InlineData(true, "en-US", true)]
    public void MinimumSizeKeepsSharedWorkspaceReadableAndEveryActionReachable(bool scoped, string cultureName, bool dark)
    {
        IDatabaseCleanupSourceProvider[] providers = scoped ? [new FakeSocketProvider()] : [new FakeSelectionProvider(), new FakeSocketProvider()];
        WithWindow(providers, scoped, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            Grid root = RefreshLayout(window, minimum: true);
            FrameworkElement workspace = Element<FrameworkElement>(window, "SourceWorkspace");
            FrameworkElement overview = Element<FrameworkElement>(window, "SourceOverviewCard");
            FrameworkElement tables = Element<FrameworkElement>(window, "CleanupTablesCard");
            DataGrid tableGrid = Element<DataGrid>(window, "CleanupTablesGrid");
            ScrollViewer actions = Element<ScrollViewer>(window, "CleanupActionsScrollViewer");
            FrameworkElement status = Element<FrameworkElement>(window, "CleanupStatusBar");

            AssertInside(workspace, root);
            AssertInside(overview, workspace);
            AssertInside(tables, workspace);
            AssertInside(tableGrid, tables);
            AssertAllTableHeadersFit(tableGrid);
            AssertInside(actions, workspace);
            AssertInside(status, workspace);
            Assert.True(BoundsIn(overview, workspace).Bottom <= BoundsIn(tables, workspace).Top + 1,
                "The source overview must not overlap the data table.");
            Assert.True(BoundsIn(tables, workspace).Right <= BoundsIn(actions, workspace).Left + 1,
                "The table and maintenance panel must have separate columns.");
            Assert.True(BoundsIn(tables, workspace).Bottom <= BoundsIn(status, workspace).Top + 1,
                "The table and status bar must not overlap.");

            if (!scoped)
            {
                FrameworkElement navigation = Element<FrameworkElement>(window, "SourceNavigationPane");
                AssertInside(navigation, root);
                Assert.True(BoundsIn(navigation, root).Right <= BoundsIn(workspace, root).Left + 1,
                    "Global source navigation must not overlap the selected workspace.");
            }

            AssertReadableForeground(window, Element<TextBlock>(window, "CleanupStatusText"));
            AssertReadableForeground(window, Element<TextBlock>(window, "CleanupBackupStatusText"));
            TextBlock sourceTitle = Assert.Single(VisualDescendants(overview).OfType<TextBlock>()
                .Where(text => text.Text == viewModel.SelectedSource!.DisplayName));
            AssertReadableForeground(window, sourceTitle);
            TextBlock[] tableText = VisualDescendants(tableGrid).OfType<TextBlock>()
                .Where(text => !string.IsNullOrWhiteSpace(text.Text) && IsEffectivelyVisible(text)).ToArray();
            Assert.NotEmpty(tableText);
            foreach (TextBlock text in tableText)
                AssertReadableForeground(window, text);

            foreach (string buttonName in new[] { "CreateBackupButton", "MigrationButton" })
            {
                Button button = Element<Button>(window, buttonName);
                if (IsEffectivelyVisible(button))
                    AssertInside(button, overview);
            }
            Button cleanupSelected = Element<Button>(window, "CleanupSelectedButton");
            if (IsEffectivelyVisible(cleanupSelected))
            {
                AssertInside(cleanupSelected, Element<FrameworkElement>(window, "SelectionToolbar"));
                AssertInside(cleanupSelected, tables);
            }

            Element<Expander>(window, "DangerZoneExpander").IsExpanded = true;
            RefreshLayout(window, minimum: true);
            foreach (string buttonName in new[] { "HistoryCleanupButton", "CleanupAllButton" })
            {
                Button button = Element<Button>(window, buttonName);
                if (!IsEffectivelyVisible(button))
                    continue;
                button.BringIntoView();
                RefreshLayout(window, minimum: true);
                AssertInside(button, actions);
            }
        }, cultureName, dark);
    }

    [Fact]
    public void LayoutRefreshCompletesWhileBackgroundWorkRemainsQueued()
    {
        WithWindow([new FakeSelectionProvider()], true, (window, viewModel) =>
        {
            CompleteWithDispatcher(viewModel.RefreshAllAsync());
            int backgroundTicks = 0;
            bool timedOut = false;
            DispatcherTimer backgroundTimer = new(DispatcherPriority.Background, window.Dispatcher) { Interval = TimeSpan.Zero };
            backgroundTimer.Tick += (_, _) => backgroundTicks++;
            DispatcherTimer timeout = new(DispatcherPriority.Send, window.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            timeout.Tick += (_, _) =>
            {
                timedOut = true;
                backgroundTimer.Stop();
                timeout.Stop();
            };
            backgroundTimer.Start();
            timeout.Start();
            try
            {
                RefreshLayout(window, minimum: true);
                Assert.False(timedOut, "Layout refresh must not wait for unrelated dispatcher work to become idle.");
                Assert.True(backgroundTicks > 0, "The layout barrier must run alongside queued Background-priority work.");
                AssertAllTableHeadersFit(Element<DataGrid>(window, "CleanupTablesGrid"));
            }
            finally
            {
                backgroundTimer.Stop();
                timeout.Stop();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RequestedThemeOverridesAmbientPaletteAndRestoresApplicationResources(bool dark)
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary resources = Application.Current.Resources;
            HashSet<object> originalKeys = resources.Keys.Cast<object>().ToHashSet();
            string[] paletteKeys = ["GlobalBackground", "GlobalTextBrush", "SecondaryTextBrush"];
            Dictionary<string, object> originalPalette = paletteKeys.Where(key => originalKeys.Contains(key))
                .ToDictionary(key => key, key => resources[key]);
            ResourceDictionary[] originalMergedDictionaries = resources.MergedDictionaries.ToArray();
            resources["GlobalBackground"] = Brushes.White;
            resources["GlobalTextBrush"] = Brushes.Black;
            resources["SecondaryTextBrush"] = Brushes.Gray;
            try
            {
                WithWindow([new FakeSelectionProvider()], true, (window, viewModel) =>
                {
                    CompleteWithDispatcher(viewModel.RefreshAllAsync());
                    RefreshLayout(window, minimum: true);
                    foreach (TextBlock text in VisualDescendants(Element<DataGrid>(window, "CleanupTablesGrid")).OfType<TextBlock>()
                        .Where(text => !string.IsNullOrWhiteSpace(text.Text) && IsEffectivelyVisible(text)))
                        AssertReadableForeground(window, text);

                    Assert.Equal(dark ? Color.FromRgb(0x26, 0x26, 0x26) : Colors.White,
                        Assert.IsType<SolidColorBrush>(window.FindResource("GlobalBackground")).Color);
                    Assert.Equal(dark ? Color.FromRgb(0xC0, 0xC0, 0xC0) : Color.FromRgb(0x66, 0x66, 0x66),
                        Assert.IsType<SolidColorBrush>(window.FindResource("SecondaryTextBrush")).Color);
                }, dark: dark);

                Assert.Same(Brushes.White, resources["GlobalBackground"]);
                Assert.Same(Brushes.Black, resources["GlobalTextBrush"]);
                Assert.Same(Brushes.Gray, resources["SecondaryTextBrush"]);
                Assert.Equal(originalMergedDictionaries, resources.MergedDictionaries);
                Assert.True(originalKeys.Union(paletteKeys).ToHashSet().SetEquals(resources.Keys.Cast<object>()));
            }
            finally
            {
                foreach (string key in paletteKeys)
                {
                    if (originalPalette.TryGetValue(key, out object? value))
                        resources[key] = value;
                    else
                        resources.Remove(key);
                }
            }
        });
    }

    [Fact]
    public void RenderPreviewsOnlyWhenAnOutputDirectoryIsRequested()
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("COLORVISION_CLEANUP_PREVIEW_DIRECTORY");
        if (string.IsNullOrWhiteSpace(outputDirectory))
            return;

        Assert.True(Path.IsPathFullyQualified(outputDirectory), "The preview directory must be an absolute path.");
        Directory.CreateDirectory(outputDirectory);
        foreach ((string name, bool scoped, string culture, bool dark, bool minimum) in new[]
        {
            ("cleanup-global.png", false, "zh-CN", false, false),
            ("cleanup-socket.png", true, "zh-CN", false, false),
            ("cleanup-global-dark-minimum.png", false, "zh-CN", true, true),
            ("cleanup-socket-dark-minimum.png", true, "zh-CN", true, true),
            ("cleanup-global-english-minimum.png", false, "en-US", false, true),
            ("cleanup-socket-english-minimum.png", true, "en-US", false, true),
        })
        {
            IDatabaseCleanupSourceProvider[] providers = scoped ? [new FakeSocketProvider()] :
                [new FakeSelectionProvider(), new FakeSourceProvider("kb", "KB SQLite", 10),
                    new FakeSourceProvider("arvr", "ARVRPro SQLite", 15), new FakeSourceProvider("flow", "流程诊断 SQLite", 20), new FakeSocketProvider()];
            WithWindow(providers, scoped, (window, viewModel) =>
            {
                CompleteWithDispatcher(viewModel.RefreshAllAsync());
                Grid root = RefreshLayout(window, minimum);
                Size viewport = new(minimum ? window.MinWidth : window.Width, (minimum ? window.MinHeight : window.Height) - 40);
                Brush background = Assert.IsAssignableFrom<Brush>(window.FindResource("GlobalBackground"));
                VisualBrush content = new(root) { AutoLayoutContent = false, Stretch = Stretch.Fill };
                DrawingVisual preview = new();
                using (DrawingContext drawing = preview.RenderOpen())
                {
                    drawing.DrawRectangle(background, null, new Rect(viewport));
                    drawing.DrawRectangle(content, null, new Rect(new Point(root.Margin.Left, root.Margin.Top), root.RenderSize));
                }
                RenderTargetBitmap bitmap = new((int)Math.Ceiling(viewport.Width * 1.5),
                    (int)Math.Ceiling(viewport.Height * 1.5), 144, 144, PixelFormats.Pbgra32);
                bitmap.Render(preview);
                byte[] cornerPixel = new byte[4];
                bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth - 1, bitmap.PixelHeight - 1, 1, 1), cornerPixel, 4, 0);
                Assert.Equal((byte)255, cornerPixel[3]);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream output = File.Create(Path.Combine(outputDirectory, name));
                encoder.Save(output);
            }, culture, dark);
        }
    }

    private static void WithWindow(IEnumerable<IDatabaseCleanupSourceProvider> providers, bool scoped,
        Action<DatabaseCleanupWindow, DatabaseCleanupWindowViewModel> action, string cultureName = "zh-CN", bool dark = false)
    {
        WithEnvironment(() =>
        {
            DatabaseCleanupWindowViewModel viewModel = new(providers, scoped);
            DatabaseCleanupWindow window = new(viewModel, refreshOnLoad: false);
            try
            {
                // Exercise the real Loaded handler with refresh explicitly disabled; constructors and layout must not read databases.
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                RefreshLayout(window);
                Element<DataGrid>(window, "CleanupTablesGrid").RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                RefreshLayout(window);
                action(window, viewModel);
            }
            finally
            {
                window.Close();
            }
        }, cultureName, dark);
    }

    private static void WithEnvironment(Action action, string cultureName = "zh-CN", bool dark = false)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
            ResourceDictionary resources = Application.Current.Resources;
            List<ResourceDictionary> addedDictionaries = [];
            Dictionary<object, object> previousLocalResources = [];
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml",
                })
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(source, UriKind.Relative) };
                    resources.MergedDictionaries.Add(dictionary);
                    addedDictionaries.Add(dictionary);
                }
                // Top-level test stubs take precedence over merged theme dictionaries. Temporarily
                // remove only colliding local entries; merged-only values must not become local on restore.
                foreach (object key in resources.Keys.Cast<object>().ToArray())
                {
                    if (addedDictionaries.Any(dictionary => dictionary.Contains(key)))
                    {
                        previousLocalResources.Add(key, resources[key]);
                        resources.Remove(key);
                    }
                }
                action();
            }
            finally
            {
                for (int index = addedDictionaries.Count - 1; index >= 0; index--)
                    resources.MergedDictionaries.Remove(addedDictionaries[index]);
                foreach ((object key, object value) in previousLocalResources)
                    resources[key] = value;
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        });
    }

    private static void CompleteWithDispatcher(Task task)
    {
        if (!task.IsCompleted)
        {
            // RefreshAsync marshals snapshots back to the WPF host. Pump it instead of blocking its dispatcher with Wait/Result.
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherFrame frame = new();
            DispatcherTimer timeout = new(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(10) };
            timeout.Tick += (_, _) => frame.Continue = false;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeout.Start();
            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                timeout.Stop();
            }
        }
        Assert.True(task.IsCompleted, "The in-memory cleanup provider refresh must complete without blocking the WPF dispatcher.");
        task.GetAwaiter().GetResult();
    }

    private static Grid RefreshLayout(DatabaseCleanupWindow window, bool minimum = false)
    {
        // DataGrid viewport updates run at Loaded and command requery runs at Background.
        // A Background FIFO barrier drains both without waiting for unrelated timers to reach ContextIdle.
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        Grid root = Assert.IsType<Grid>(window.Content);
        Size size = new(minimum ? window.MinWidth : window.Width, (minimum ? window.MinHeight : window.Height) - 40);
        // DataGrid defers star-column sizing when its ScrollViewer viewport changes. Drain after
        // arranging, then lay out again so offscreen previews reflect a resize to the minimum width.
        for (int pass = 0; pass < 2; pass++)
        {
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
        root.UpdateLayout();
        return root;
    }

    private static void AssertAllTableHeadersFit(DataGrid grid)
    {
        ScrollContentPresenter viewport = Assert.Single(VisualDescendants(grid).OfType<ScrollContentPresenter>());
        Rect viewportBounds = BoundsIn(viewport, grid);
        DataGridColumnHeader[] headers = VisualDescendants(grid).OfType<DataGridColumnHeader>()
            .Where(header => header.Column?.Visibility == Visibility.Visible).ToArray();
        Assert.Equal(grid.Columns.Count(column => column.Visibility == Visibility.Visible), headers.Length);
        foreach (DataGridColumnHeader header in headers)
        {
            Rect bounds = BoundsIn(header, grid);
            Assert.True(bounds.Left >= viewportBounds.Left - 1 && bounds.Right <= viewportBounds.Right + 1,
                $"The '{header.Content}' column header must fit the table viewport: header {bounds}, viewport {viewportBounds}; "
                + $"column widths [{string.Join(", ", grid.Columns.Select(column => $"{column.Header}: {column.ActualWidth:0.##}"))}].");
        }
    }

    private static T Element<T>(DatabaseCleanupWindow window, string name) where T : FrameworkElement
    {
        if (window.FindName(name) is T direct)
            return direct;
        FrameworkElement root = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        T? visual = VisualDescendants(root).OfType<T>().FirstOrDefault(element => element.Name == name);
        if (visual != null)
            return visual;
        foreach (ContentPresenter presenter in VisualDescendants(root).OfType<ContentPresenter>())
        {
            if (presenter.ContentTemplate?.FindName(name, presenter) is T templated)
                return templated;
        }
        return Assert.IsAssignableFrom<T>(null);
    }

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor)
        => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));

    private static void AssertInside(FrameworkElement element, FrameworkElement ancestor)
    {
        Rect bounds = BoundsIn(element, ancestor);
        Assert.True(bounds.Width > 0 && bounds.Height > 0, $"{element.Name} must have usable layout space.");
        Assert.True(bounds.Left >= -1 && bounds.Top >= -1
            && bounds.Right <= ancestor.ActualWidth + 1 && bounds.Bottom <= ancestor.ActualHeight + 1,
            $"{element.Name} must fit inside {ancestor.Name}: {bounds}, parent {ancestor.RenderSize}.");
    }

    private static void AssertReadableForeground(DatabaseCleanupWindow window, TextBlock text)
    {
        SolidColorBrush foreground = Assert.IsType<SolidColorBrush>(text.Foreground);
        SolidColorBrush background = Assert.IsType<SolidColorBrush>(window.FindResource("GlobalBackground"));
        double first = Luminance(foreground.Color);
        double second = Luminance(background.Color);
        double contrast = (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
        Assert.True(contrast >= 4,
            $"'{text.Text}' must remain readable against the current theme; contrast was {contrast:0.##} (foreground {foreground.Color}, background {background.Color}).");
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte channel)
        {
            double value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }

    private static bool IsEffectivelyVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: not Visibility.Visible })
                return false;
        }
        return element.ActualWidth > 0 && element.ActualHeight > 0;
    }

    private static IEnumerable<FrameworkElement> VisualDescendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element)
                yield return element;
            foreach (FrameworkElement descendant in VisualDescendants(child))
                yield return descendant;
        }
    }

    private class FakeSourceProvider(string id, string displayName, int order, bool empty = false) : IDatabaseCleanupSourceProvider
    {
        private int _loadCalls;
        public string Id => id;
        public virtual string DisplayName => EngineLocalization.Get(displayName);
        public virtual string Description => EngineLocalization.Format($"数据库文件: {"C:\\Preview-only\\ColorVision\\Config\\InspectionHistory.db"}");
        public int Order => order;
        public int LoadCalls => _loadCalls;

        public virtual IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            Interlocked.Increment(ref _loadCalls);
            return empty ? [] : [new DatabaseCleanupTableInfo { TableName = "inspection_history", Exists = true, RowCount = 12840, SizeBytes = 14 * 1024 * 1024 }];
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths) => throw new InvalidOperationException("Layout tests must never clean a database.");
        public DatabaseCleanupExecutionResult CleanupAll() => throw new InvalidOperationException("Layout tests must never clean a database.");
    }

    private sealed class FakeSocketProvider() : FakeSourceProvider("socketmessages-sqlite", "", 22),
        IDatabaseCleanupBackupProvider, IDatabaseCleanupMigrationProvider
    {
        public override string DisplayName => EngineLocalization.Get("Socket 消息 SQLite");
        public override string Description => EngineLocalization.Format($"数据库文件: {"C:\\Preview-only\\ColorVision\\Config\\SocketMessages.db"}");
        public string MigrationActionName => EngineLocalization.Get("迁移并压缩历史消息");
        public string MigrationConfirmationMessage => "Preview only — no migration is allowed.";

        public override IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            base.LoadTables();
            return [new DatabaseCleanupTableInfo { TableName = "SocketMessage", Exists = true, RowCount = 28140, SizeBytes = 684 * 1024 }];
        }

        public DatabaseCleanupBackupResult CreateBackup() => throw new InvalidOperationException("Layout tests must never create a database backup.");
        public DatabaseCleanupExecutionResult ExecuteMigration() => throw new InvalidOperationException("Layout tests must never migrate a database.");
    }

    private sealed class FakeSelectionProvider() : FakeSourceProvider("mysql-results", "", 0),
        IDatabaseCleanupBackupProvider, IDatabaseCleanupSelectionProvider
    {
        public override string DisplayName => EngineLocalization.Get("MySQL 结果表");
        public override string Description => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.Ordinal)
            ? "数据源：本地检测结果库 · 以下统计均为界面预览示例"
            : "Source: local inspection results · All statistics are preview examples";

        public override IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            base.LoadTables();
            return Enumerable.Range(1, 14).Select(index => new DatabaseCleanupTableInfo
            {
                TableName = index == 1 ? "t_scgd_algorithm_result_master" : $"t_scgd_algorithm_result_detail_{index:00}",
                Exists = true,
                RowCount = 2400 * index,
                SizeBytes = 1024 * 1024 * index,
            }).ToArray();
        }

        public DatabaseCleanupBackupResult CreateBackup() => throw new InvalidOperationException("Layout tests must never create a database backup.");
        public DatabaseCleanupExecutionResult CleanupTables(IReadOnlyCollection<string> tableNames)
            => throw new InvalidOperationException("Layout tests must never clean database tables.");
    }
}
