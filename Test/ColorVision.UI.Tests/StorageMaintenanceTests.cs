using ColorVision.Settings.Maintenance;
using ColorVision.Themes;
using ColorVision.UI.Desktop.Settings;
using ColorVision.UI.Maintenance;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StorageMaintenanceTests
{
    [Fact]
    public async Task DiscoveredCategoriesUseTheirOwnScanAndCleanupAndPrepareFailureInvalidatesOldResults()
    {
        using Sandbox sandbox = new();
        var provider = new FixtureFileProvider(Path.GetDirectoryName(sandbox.Files["logs"])!);
        StorageMaintenanceViewModel model = new(new StorageMaintenanceConfig(), [provider]);
        Assert.Equal("fixture.files", Assert.Single(model.Items).Id);
        await model.ScanAsync();
        StorageCleanupItem item = Assert.Single(model.Items);
        Assert.True(item.CanClean);
        Assert.True(item.HasRetention);
        await model.ScanAsync();
        Assert.Equal(1, item.Snapshot!.ReusedFileCount);

        provider.FailPrepare = true;
        await model.ScanAsync();
        Assert.False(item.CanClean);
        Assert.Contains("fixture-prepare-error", model.Status);
        await model.CleanupAsync([item]);
        Assert.True(File.Exists(sandbox.Files["logs"]));

        provider.FailPrepare = false;
        await model.ScanAsync();
        await model.CleanupAsync([item]);
        Assert.False(File.Exists(sandbox.Files["logs"]));
        Assert.True(File.Exists(sandbox.Files["packages"]));
    }

    [Fact]
    public void FileWindowAutomaticallyScansInjectedSourcesAndKeepsFileActionsOutOfSettings()
    {
        WithPage(980, false, "en-US", (_, page, _, model, sandbox) =>
        {
            var window = new FileManagementWindow(model);
            try
            {
                Assert.Equal(0, sandbox.RuleFactoryCalls);
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                CompleteWithDispatcher(window.ScanTask);
                Assert.Equal(1, sandbox.RuleFactoryCalls);
                var filePage = Assert.IsType<FileManagementControl>(Element<ScrollViewer>(window, "FileContent").Content);
                Assert.True(Element<Button>(filePage, "CleanSelectedButton").IsEnabled);
                Assert.DoesNotContain(Descendants(filePage), element => element.Name is "FileManagementButton" or "MemoryManagementButton" or "ResetOptions");
                window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                CompleteWithDispatcher(window.ScanTask);
                Assert.Equal(1, Item(model, "logs").Snapshot!.ReusedFileCount);

                Grid content = Assert.IsType<Grid>(window.Content);
                content.Measure(new Size(720, 680));
                content.Arrange(new Rect(0, 0, 720, 680));
                content.UpdateLayout();
                foreach (TextBlock text in Descendants(content).OfType<TextBlock>().Where(IsEffectivelyVisible)) AssertTextFits(text);
                Assert.True(Element<Button>(page, "FileManagementButton").IsEnabled);
                Assert.True(Element<Button>(page, "MemoryManagementButton").IsEnabled);
            }
            finally { window.Close(); }
        });
    }

    private sealed class FixtureFileProvider(string root) : IFileMaintenanceProvider
    {
        public bool FailPrepare { get; set; }
        public void PrepareScan() { if (FailPrepare) throw new IOException("fixture-prepare-error"); }
        public IEnumerable<FileMaintenanceCategory> GetCategories() => [new FixtureFileCategory(root)];
    }

    private sealed class FixtureFileCategory(string root) : FileMaintenanceCategory
    {
        public override string Id => "fixture.files";
        public override string Title => "Fixture files";
        public override string Description => "Isolated provider";
        public override int? DefaultRetentionDays => 7;
        public override bool DefaultSelected => true;
        public override IReadOnlyList<string> SourceDirectories => [root];
        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken) =>
            FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan([new(Id, root, "*.dat", RetentionDays: retentionDays)], cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));
        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken) =>
            FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup((MaintenanceFileCleanupScanResult)confirmed.Token, cancellationToken));
    }

    [Fact]
    public async Task DefaultsSelectSafeCategoriesAndRequireAScanBeforeAnyCleanup()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceViewModel model = sandbox.CreateModel();

        Assert.Equal(["logs", "temp", "thumbnails", "cie-cache"], model.Items.Where(item => item.IsSelected).Select(item => item.Id));
        Assert.False(Item(model, "packages").IsSelected);
        Assert.Equal(30, Item(model, "logs").RetentionDays);
        Assert.Equal(7, Item(model, "temp").RetentionDays);
        Assert.Equal(30, Item(model, "packages").RetentionDays);
        Assert.All(model.Items, item => { Assert.False(item.IsScanned); Assert.False(item.CanClean); });
        Assert.False(model.CanCleanSelected);
        Assert.Equal(0, sandbox.RuleFactoryCalls);
        Assert.Equal(0, sandbox.ThumbnailScanCalls);

        await model.CleanupAsync([Item(model, "logs"), Item(model, "thumbnails")]);

        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(0, sandbox.ThumbnailClearCalls);
        Assert.Equal(MaintenanceText.ScanFirst, model.Status);
    }

    [Fact]
    public async Task ScanUsesOnlyInjectedRootsAndCapturesTheRequestedRetention()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceConfig config = new() { LogRetentionDays = 90, TemporaryRetentionDays = 14, PackageRetentionDays = 180 };
        StorageMaintenanceViewModel model = sandbox.CreateModel(config);

        await model.ScanAsync();

        Assert.Equal((90, 14, 180), sandbox.LastRetention);
        Assert.Equal(1, sandbox.RuleFactoryCalls);
        Assert.Equal(1, sandbox.ThumbnailScanCalls);
        Assert.Equal(0, sandbox.ThumbnailClearCalls);
        Assert.All(model.Items, item => { Assert.True(item.IsScanned); Assert.True(item.CanClean); });
        Assert.Equal(3, Item(model, "thumbnails").Count);
        Assert.All(model.Items.Where(item => item.FileScan != null).SelectMany(item => item.FileScan!.Files),
            file => Assert.True(file.FullPath.StartsWith(sandbox.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(MaintenanceText.ScanComplete, model.Status);
        Assert.False(model.IsBusy);
        Assert.Empty(model.Issues);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task ChangingRetentionInvalidatesTheOldProposalAndUpdatesOnlyItsSetting()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceConfig config = new();
        StorageMaintenanceViewModel model = sandbox.CreateModel(config);
        await model.ScanAsync();
        foreach (StorageCleanupItem item in model.Items)
            item.IsSelected = item.Id == "logs";
        StorageCleanupItem logs = Item(model, "logs");
        Assert.True(model.CanCleanSelected);

        logs.RetentionDays = 90;

        Assert.Equal(90, config.LogRetentionDays);
        Assert.Equal(7, config.TemporaryRetentionDays);
        Assert.False(logs.IsScanned);
        Assert.Null(logs.FileScan);
        Assert.Equal(0, logs.Count);
        Assert.Equal(0, logs.Bytes);
        Assert.False(model.CanCleanSelected);
        await model.CleanupAsync([logs]);
        Assert.True(File.Exists(sandbox.Files["logs"]));
        Assert.True(Item(model, "temp").IsScanned);

        logs.RetentionDays = -1;
        Assert.Equal(90, logs.RetentionDays);

        logs.RetentionDays = 0;
        Assert.Equal(0, config.LogRetentionDays);
        Assert.False(logs.HasRetentionPeriod);
        Assert.False(logs.CanClean);
        await model.ScanAsync();
        Assert.True(logs.CanClean);
        Assert.Equal(0, sandbox.LastRetention.Item1);
        Assert.Equal(0, new StorageCleanupItem("logs", "Logs", "", 0, false).RetentionDays);
    }

    [Fact]
    public async Task SourceLocationRemainsAvailableWhenRetentionLeavesNoCandidates()
    {
        using Sandbox sandbox = new();
        string root = Path.GetDirectoryName(sandbox.Files["logs"])!;
        File.SetLastWriteTimeUtc(sandbox.Files["logs"], DateTime.UtcNow);
        StorageMaintenanceViewModel model = new(new StorageMaintenanceConfig(), [new FixtureFileProvider(root)]);

        await model.ScanAsync();

        StorageCleanupItem item = Assert.Single(model.Items);
        Assert.Equal(0, item.Count);
        Assert.False(item.CanClean);
        Assert.True(item.CanOpenLocation);
        Assert.Equal(root, Assert.Single(item.SourceDirectories));
        item.RetentionDays = 0;
        await model.ScanAsync();
        Assert.Equal(1, item.Count);
        Assert.True(item.CanOpenLocation);
    }

    [Fact]
    public async Task PerItemCleanupTouchesOnlyTheConfirmedRowAndRequiresAnotherScan()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        await model.ScanAsync();
        StorageCleanupItem temporary = Item(model, "temp");
        long expectedBytes = temporary.Bytes;

        await model.CleanupAsync([temporary]);

        Assert.False(File.Exists(sandbox.Files["temp"]));
        Assert.All(sandbox.Files.Where(pair => pair.Key != "temp"), pair => Assert.True(File.Exists(pair.Value)));
        Assert.Equal(0, sandbox.ThumbnailClearCalls);
        Assert.False(temporary.CanClean);
        Assert.Null(temporary.FileScan);
        Assert.True(Item(model, "logs").CanClean);
        Assert.Equal(string.Format(MaintenanceText.Result, StorageCleanupItem.FormatBytes(expectedBytes), 1, 0, 0), model.Status);
    }

    [Fact]
    public async Task SelectedCleanupPreservesPackagesAndFilesCreatedAfterConfirmation()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        await model.ScanAsync();
        StorageCleanupItem[] confirmed = model.Items.Where(item => item.IsSelected && item.CanClean).ToArray();
        string laterFile = sandbox.CreateOldFile("logs", "after-scan.dat");

        await model.CleanupAsync(confirmed);

        Assert.False(File.Exists(sandbox.Files["logs"]));
        Assert.False(File.Exists(sandbox.Files["temp"]));
        Assert.False(File.Exists(sandbox.Files["cie-cache"]));
        Assert.True(File.Exists(sandbox.Files["packages"]));
        Assert.True(File.Exists(laterFile));
        Assert.Equal(1, sandbox.ThumbnailClearCalls);
        Assert.Same(sandbox.ThumbnailToken, sandbox.LastClearedSnapshot!.Token);
        Assert.All(confirmed, item => Assert.False(item.IsScanned));
        Assert.True(Item(model, "packages").IsScanned);
        Assert.False(model.CanCleanSelected);
    }

    [Fact]
    public async Task AForeignOrInvalidatedConfirmedRowRejectsTheWholeBatch()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        StorageMaintenanceViewModel other = sandbox.CreateModel();
        await model.ScanAsync();
        await other.ScanAsync();

        await model.CleanupAsync([Item(model, "logs"), Item(other, "temp")]);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));

        Item(model, "temp").RetentionDays = 14;
        await model.CleanupAsync([Item(model, "logs"), Item(model, "temp")]);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(0, sandbox.ThumbnailClearCalls);
    }

    [Fact]
    public async Task CancellingAnInFlightScanReportsCancellationAndNeverDeletes()
    {
        using Sandbox sandbox = new();
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        sandbox.ScanThumbnailOverride = () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The fake thumbnail scan was not released.");
            return sandbox.ThumbnailSnapshot;
        };
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        Task scan = model.ScanAsync();
        try
        {
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(5))));
            Assert.True(model.IsBusy);
            Assert.False(model.CanCleanSelected);
            model.Cancel();
        }
        finally { release.Set(); }
        await scan.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MaintenanceText.Cancelled, model.Status);
        Assert.False(model.IsBusy);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(0, sandbox.ThumbnailClearCalls);
    }

    [Fact]
    public async Task CancellingCleanupStopsBeforeTheNextConfirmedCategory()
    {
        using Sandbox sandbox = new();
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        sandbox.ClearThumbnailOverride = snapshot =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The fake thumbnail cleanup was not released.");
            return new(snapshot.Bytes, snapshot.Count, 0, null);
        };
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        await model.ScanAsync();
        Task cleanup = model.CleanupAsync([Item(model, "thumbnails"), Item(model, "logs")]);
        try
        {
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(5))));
            model.Cancel();
        }
        finally { release.Set(); }
        await cleanup.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Contains(MaintenanceText.Cancelled, model.Status);
        Assert.False(model.IsBusy);
        Assert.True(Item(model, "logs").IsScanned);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
        Assert.Equal(1, sandbox.ThumbnailClearCalls);
    }

    [Fact]
    public async Task RuleFactoryFailureIsVisibleAndLeavesNoDeletableRows()
    {
        using Sandbox sandbox = new();
        StorageMaintenanceViewModel model = new(new StorageMaintenanceConfig(),
            (_, _, _) => throw new IOException("isolated-rule-error"),
            () => throw new InvalidOperationException("Must not scan thumbnails after a rule factory failure."),
            _ => throw new InvalidOperationException("Must not clear thumbnails without a scan."));

        await model.ScanAsync();

        Assert.Contains("isolated-rule-error", model.Status);
        Assert.Contains("isolated-rule-error", Assert.Single(model.Issues));
        Assert.True(model.HasIssues);
        Assert.False(model.IsBusy);
        Assert.False(model.CanCleanSelected);
        Assert.All(model.Items, item => Assert.False(item.CanClean));
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task ThumbnailScanFailureKeepsItsRowUnavailableAndExposesDetails()
    {
        using Sandbox sandbox = new();
        sandbox.ScanThumbnailOverride = () => throw new IOException("isolated-thumbnail-scan-error");
        StorageMaintenanceViewModel model = sandbox.CreateModel();

        await model.ScanAsync();

        Assert.False(Item(model, "thumbnails").CanClean);
        Assert.True(Item(model, "logs").CanClean);
        Assert.True(model.HasIssues);
        Assert.Contains(model.Issues, issue => issue.Contains("isolated-thumbnail-scan-error", StringComparison.Ordinal));
        Assert.False(model.IsBusy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThumbnailCleanupFailuresAreReportedWithoutPretendingFilesWereDeleted(bool throwException)
    {
        using Sandbox sandbox = new();
        sandbox.ClearThumbnailOverride = _ => throwException
            ? throw new IOException("isolated-thumbnail-cleanup-error")
            : new StorageCacheCleanupResult(0, 0, 0, "isolated-thumbnail-cleanup-error");
        StorageMaintenanceViewModel model = sandbox.CreateModel();
        await model.ScanAsync();

        await model.CleanupAsync([Item(model, "thumbnails")]);

        Assert.True(model.HasIssues);
        Assert.Contains("isolated-thumbnail-cleanup-error", Assert.Single(model.Issues));
        Assert.Equal(string.Format(MaintenanceText.Result, StorageCleanupItem.FormatBytes(0), 0, 0, 1), model.Status);
        Assert.False(Item(model, "thumbnails").CanClean);
        Assert.All(sandbox.Files.Values, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task ConcurrentMaintenanceModelsDoNotStartAnotherScanWhileOneIsActive()
    {
        using Sandbox sandbox = new();
        using Sandbox secondSandbox = new();
        using ManualResetEventSlim entered = new();
        using ManualResetEventSlim release = new();
        sandbox.ScanThumbnailOverride = () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("The fake scan was not released.");
            return sandbox.ThumbnailSnapshot;
        };
        StorageMaintenanceViewModel first = sandbox.CreateModel();
        StorageMaintenanceViewModel second = secondSandbox.CreateModel();
        Task scan = first.ScanAsync();
        try
        {
            Assert.True(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(5))));
            await second.ScanAsync();
            Assert.Equal(0, secondSandbox.RuleFactoryCalls);
            Assert.Equal(0, secondSandbox.ThumbnailScanCalls);
            Assert.False(second.IsBusy);
        }
        finally { release.Set(); }
        await scan.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void IsolatedControlScansReadonlyUsageWithoutResolvingProductionStateOrPreparingCleanup()
    {
        WithPage(980, false, "zh-CN", (host, page, scroll, model, sandbox) =>
        {
            Assert.Same(model, page.ViewModel);
            Assert.Same(page, page.DataContext);
            Assert.Equal(0, sandbox.RuleFactoryCalls);
            Assert.Equal(0, sandbox.ThumbnailScanCalls);
            Assert.True(page.Overview.IsComplete);
            Assert.Equal(5, page.Overview.TotalCount);
            Assert.True(Element<Button>(page, "FileManagementButton").IsEnabled);
            Assert.True(Element<Button>(page, "MemoryManagementButton").IsEnabled);
            Assert.DoesNotContain(Descendants(page), element => element.Name == "ScanButton" || element.Name == "CleanSelectedButton");
            Assert.DoesNotContain(Descendants(page), element => element.Name is "ResetOptions" or "CancelResetButton");
        });
    }

    [Theory]
    [InlineData(980, false, "zh-CN")]
    [InlineData(980, true, "zh-CN")]
    [InlineData(1180, false, "zh-CN")]
    [InlineData(1180, true, "zh-CN")]
    [InlineData(980, false, "en-US")]
    [InlineData(980, true, "en-US")]
    [InlineData(1180, false, "en-US")]
    [InlineData(1180, true, "en-US")]
    public void SettingsWidthKeepsTextAndActionsUnclippedAndReachable(int width, bool dark, string culture)
    {
        WithPage(width, dark, culture, (host, page, scroll, model, sandbox) =>
        {
            CompleteWithDispatcher(model.ScanAsync());
            RefreshLayout(host, width);
            Assert.InRange(page.ActualWidth, width - 345, width - 295);
            Assert.True(Element<Button>(page, "FileManagementButton").IsEnabled);
            Assert.True(Element<Button>(page, "MemoryManagementButton").IsEnabled);

            foreach (FrameworkElement element in Descendants(page).Where(IsEffectivelyVisible).Where(element => element is TextBlock or Button or ComboBox or CheckBox))
            {
                Rect bounds = BoundsIn(element, page);
                Assert.True(bounds.Left >= -1 && bounds.Right <= page.ActualWidth + 1,
                    $"{element.GetType().Name} '{ElementLabel(element)}' exceeds the {page.ActualWidth:0.##}-wide settings content: {bounds}.");
                if (element is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))
                    AssertTextFits(text);
                if (element is Button button && button.Content is string label)
                {
                    double available = button.ActualWidth - button.Padding.Left - button.Padding.Right - button.BorderThickness.Left - button.BorderThickness.Right;
                    Assert.True(MeasureText(label, button).WidthIncludingTrailingWhitespace <= available + 2,
                        $"Button '{label}' must display its complete action label (available {available:0.##}).");
                }
            }

            foreach (Button button in Descendants(page).OfType<Button>().Where(IsEffectivelyVisible).ToArray())
            {
                button.BringIntoView();
                RefreshLayout(host, width);
                Rect bounds = BoundsIn(button, scroll);
                Assert.True(bounds.Top >= -1 && bounds.Bottom <= scroll.ActualHeight + 1,
                    $"Action '{button.Content}' must be reachable by vertical scrolling: {bounds}, viewport {scroll.RenderSize}.");
            }
        });
    }

    [Theory]
    [InlineData(980, false)]
    [InlineData(980, true)]
    [InlineData(1180, false)]
    [InlineData(1180, true)]
    public void RealSettingsShellRendersInjectedGeneralSectionsWithoutDiscoveringProductionSettings(int width, bool dark)
    {
        WithGeneralSettings(width, dark, (window, host) =>
        {
            Assert.Equal(ColorVision.UI.Properties.Resources.GeneralSettings, Element<TextBlock>(window, "CurrentGroupTitle").Text);
            StackPanel content = Element<StackPanel>(window, "SettingsContentPanel");
            string[] labels = Descendants(content).OfType<TextBlock>().Select(text => text.Text).ToArray();
            foreach (string key in new[] { "SettingsSectionAppearance", "SettingsSectionUpdates", "SettingsSectionDiagnostics" })
                Assert.Contains(ColorVision.UI.Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture), labels);
            Assert.Contains("主题", labels);
            Assert.Contains("用户界面语言", labels);
            Assert.Contains("日志级别", labels);
            Assert.Equal(3, Descendants(content).OfType<RadioButton>().Count());
            Assert.DoesNotContain(labels, label => label.Contains("加载失败", StringComparison.Ordinal));
            Assert.Equal(2, Element<ListBox>(window, "NavigationListBox").Items.Count);

            foreach (FrameworkElement element in Descendants(content).Where(IsEffectivelyVisible).Where(element => element is TextBlock or ComboBox or Button))
            {
                Rect bounds = BoundsIn(element, content);
                Assert.True(bounds.Left >= -1 && bounds.Right <= content.ActualWidth + 1,
                    $"The real settings shell clips {ElementLabel(element)} at width {width}: {bounds}, content {content.RenderSize}.");
                if (element is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) AssertTextFits(text);
                if (element is ComboBox combo)
                {
                    Geometry? clip = LayoutInformation.GetLayoutClip(combo);
                    Assert.True(clip == null || (clip.Bounds.Left <= 0 && clip.Bounds.Right >= combo.ActualWidth),
                        $"The settings editor clips its own border: {combo.Text}, rendered {combo.RenderSize}, clip {clip?.Bounds}.");
                }
            }

        });
    }

    [Fact]
    public void RealSettingsShellSearchAndGroupChangesResetScrollingAndKeepThePageDescription()
    {
        WithGeneralSettings(980, true, (window, host) =>
        {
            ScrollViewer scroll = Element<ScrollViewer>(window, "SettingsScrollViewer");
            scroll.ScrollToBottom();
            RefreshLayout(host, 980);
            Assert.True(scroll.VerticalOffset > 0);

            Element<ListBox>(window, "NavigationListBox").SelectedIndex = 1;
            RefreshLayout(host, 980);
            Assert.Equal("隔离工具", Element<TextBlock>(window, "CurrentGroupTitle").Text);
            Assert.Equal("仅用于离屏验证，不连接生产服务。", Element<TextBlock>(window, "CurrentGroupDescription").Text);
            Assert.Equal(Visibility.Visible, Element<TextBlock>(window, "CurrentGroupDescription").Visibility);
            Assert.Equal(0, scroll.VerticalOffset);

            scroll.ScrollToBottom();
            RefreshLayout(host, 980);
            Assert.True(scroll.VerticalOffset > 0);
            TextBox search = Element<TextBox>(window, "SearchTextBox");
            search.Text = "network-only-fixture";
            RefreshLayout(host, 980);
            Assert.Equal(0, scroll.VerticalOffset);
            Assert.Equal(ColorVision.UI.Properties.Resources.GeneralSettings, Element<TextBlock>(window, "CurrentGroupTitle").Text);
            string[] labels = Descendants(Element<StackPanel>(window, "SettingsContentPanel")).OfType<TextBlock>().Select(text => text.Text).ToArray();
            Assert.Contains("不使用系统代理", labels);
            Assert.DoesNotContain("主题", labels);

            search.Text = "there-is-no-such-isolated-setting";
            RefreshLayout(host, 980);
            Assert.Empty(Element<ListBox>(window, "NavigationListBox").Items);
            Assert.Equal(ColorVision.UI.Properties.Resources.Options, Element<TextBlock>(window, "CurrentGroupTitle").Text);
            // SettingResources has an explicit fallback for this existing, optional resource key.
            string expectedEmptyText = ColorVision.UI.Properties.Resources.ResourceManager.GetString("SettingsNoMatchingSettings", CultureInfo.CurrentUICulture)
                ?? "没有匹配的设置";
            TextBlock emptyText = Assert.Single(Descendants(Element<StackPanel>(window, "SettingsContentPanel")).OfType<TextBlock>());
            Assert.Equal(expectedEmptyText, emptyText.Text);
        });
    }

    [Fact]
    public void SettingsEditorsWriteToTheSourceAndKeepValuesAcrossNavigation()
    {
        WithGeneralSettings(980, false, (window, host) =>
        {
            StackPanel content = Element<StackPanel>(window, "SettingsContentPanel");
            ComboBox language = Descendants(content).OfType<ComboBox>().Single(combo => combo.SelectedValue is PreviewLanguage);
            var source = Assert.IsType<PreviewSettings>(BindingOperations.GetBinding(language, Selector.SelectedValueProperty)?.Source);
            language.SelectedValue = PreviewLanguage.English;
            Assert.Equal(PreviewLanguage.English, source.Language);

            ToggleButton snapshot = Descendants(content).OfType<ToggleButton>().Single(toggle =>
                BindingOperations.GetBinding(toggle, ToggleButton.IsCheckedProperty)?.Path.Path == nameof(PreviewSettings.CreateSnapshot));
            snapshot.IsChecked = true;
            Assert.True(source.CreateSnapshot);

            ListBox navigation = Element<ListBox>(window, "NavigationListBox");
            navigation.SelectedIndex = 1;
            navigation.SelectedIndex = 0;
            RefreshLayout(host, 980);
            language = Descendants(content).OfType<ComboBox>().Single(combo => combo.SelectedValue is PreviewLanguage);
            Assert.Equal(PreviewLanguage.English, language.SelectedValue);
            Assert.True(source.CreateSnapshot);
        });
    }

    [Theory]
    [InlineData(980, false)]
    [InlineData(980, true)]
    [InlineData(1180, false)]
    [InlineData(1180, true)]
    public void RealSettingsShellKeepsMaintenanceContentReadableAfterCustomPageSizing(int width, bool dark)
    {
        WithMaintenanceSettings(width, dark, 18, (window, host, page, model, sandbox) =>
        {
            Assert.Equal(0, sandbox.RuleFactoryCalls);
            Assert.Equal(0, sandbox.ThumbnailScanCalls);
            Assert.Equal(MaintenanceText.Title, Element<TextBlock>(window, "CurrentGroupTitle").Text);
            Assert.Equal(MaintenanceText.Description, Element<TextBlock>(window, "CurrentGroupDescription").Text);
            CompleteWithDispatcher(model.ScanAsync());
            RefreshLayout(host, width);
            Assert.True(page.ActualWidth >= 600, "The settings shell must leave enough room for the maintenance actions.");
            Rect titleBounds = BoundsIn(Element<TextBlock>(window, "CurrentGroupTitle"), host);
            Assert.InRange(Math.Abs(titleBounds.Left - BoundsIn(page, host).Left), 0, 1);
            ScrollViewer scroll = Element<ScrollViewer>(window, "SettingsScrollViewer");
            Assert.True(scroll.ScrollableHeight > 0);

            foreach (FrameworkElement element in Descendants(page).Where(IsEffectivelyVisible).Where(element => element is TextBlock or Button or ComboBox or CheckBox))
            {
                Rect bounds = BoundsIn(element, page);
                Assert.True(bounds.Left >= -1 && bounds.Right <= page.ActualWidth + 1,
                    $"The real shell's custom-content sizing clips {ElementLabel(element)}: {bounds}, page {page.RenderSize}.");
                if (element is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) AssertTextFits(text);
            }
            foreach (Button button in Descendants(page).OfType<Button>().Where(IsEffectivelyVisible).ToArray())
            {
                button.BringIntoView();
                RefreshLayout(host, width);
                Rect bounds = BoundsIn(button, scroll);
                Assert.True(bounds.Top >= -1 && bounds.Bottom <= scroll.ActualHeight + 1,
                    $"The real settings shell must scroll to maintenance action '{button.Content}': {bounds}, viewport {scroll.RenderSize}.");
            }
            scroll.ScrollToTop();
            RefreshLayout(host, width);
            CaptureStoragePreview(host, $"storage-overflow-{width}-{(dark ? "dark" : "light")}.png");
        });
        CaptureNormalStoragePreview(width, dark);
    }

    [Fact]
    public void RealSettingsShellReusesTheIsolatedMaintenancePageAcrossGroupChanges()
    {
        WithMaintenanceSettings(980, true, 18, (window, host, page, model, sandbox) =>
        {
            CompleteWithDispatcher(model.ScanAsync());
            Item(model, "packages").IsSelected = true;
            ScrollViewer scroll = Element<ScrollViewer>(window, "SettingsScrollViewer");
            scroll.ScrollToBottom();
            RefreshLayout(host, 980);
            Assert.True(scroll.VerticalOffset > 0);

            ListBox navigation = Element<ListBox>(window, "NavigationListBox");
            navigation.SelectedIndex = 0;
            RefreshLayout(host, 980);
            Assert.Equal(0, scroll.VerticalOffset);
            Assert.Equal(ColorVision.UI.Properties.Resources.GeneralSettings, Element<TextBlock>(window, "CurrentGroupTitle").Text);
            navigation.SelectedIndex = 1;
            RefreshLayout(host, 980);

            Assert.Equal(0, scroll.VerticalOffset);
            PreviewMaintenancePage wrapper = Assert.Single(Descendants(Element<StackPanel>(window, "SettingsContentPanel")).OfType<PreviewMaintenancePage>());
            Assert.Same(page, wrapper.Page);
            Assert.Same(model, wrapper.Page.ViewModel);
            Assert.True(Item(model, "packages").IsSelected);
            Assert.All(model.Items, item => Assert.True(item.IsScanned));
            Assert.Equal(1, sandbox.RuleFactoryCalls);
            Assert.Equal(1, sandbox.ThumbnailScanCalls);
            Assert.Equal(0, sandbox.ThumbnailClearCalls);
        });
    }

    private static void WithMaintenanceSettings(int width, bool dark, int categoryCount,
        Action<SettingWindow, Grid, StorageMaintenanceControl, StorageMaintenanceViewModel, Sandbox> action)
    {
        WithPage(width, dark, "zh-CN", (_, _, _, model, sandbox) =>
        {
            Func<StorageMaintenanceControl>? previousFactory = PreviewMaintenancePage.CreatePage;
            // The public parameterless view is only an activation adapter around this injected fake model.
            PreviewMaintenancePage.CreatePage = () => new StorageMaintenanceControl(model, null,
                new StorageOverviewViewModel([new PreviewUsageProvider(sandbox.Root, categoryCount)]));
            try
            {
                ConfigSettingMetadata[] settings =
                [
                    new() { Name = "隔离常规选项", Section = ConfigSettingConstants.SectionAppearance,
                        BindingName = nameof(PreviewSettings.EnableDiagnostics), Source = new PreviewSettings(), Order = 0 },
                    new() { Name = MaintenanceText.Title, Description = MaintenanceText.Description, Type = ConfigSettingType.TabItem,
                        ViewType = typeof(PreviewMaintenancePage), Order = 850 }
                ];
                ConstructorInfo constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                    [typeof(IEnumerable<ConfigSettingMetadata>)], null) ?? throw new InvalidOperationException("The isolated settings constructor must remain available.");
                SettingWindow window = (SettingWindow)constructor.Invoke([settings]);
                StorageMaintenanceControl? page = null;
                try
                {
                    Grid host = Assert.IsType<Grid>(window.Content);
                    Element<ListBox>(window, "NavigationListBox").SelectedIndex = 1;
                    RefreshLayout(host, width);
                    PreviewMaintenancePage wrapper = Assert.Single(Descendants(Element<StackPanel>(window, "SettingsContentPanel")).OfType<PreviewMaintenancePage>());
                    page = wrapper.Page;
                    page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    CompleteWithDispatcher(page.ScanTask);
                    action(window, host, page, model, sandbox);
                }
                finally
                {
                    page?.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    window.Close();
                }
            }
            finally { PreviewMaintenancePage.CreatePage = previousFactory; }
        });
    }

    private static void WithGeneralSettings(int width, bool dark, Action<SettingWindow, Grid> action)
    {
        // Reuse the isolated STA/theme lifetime, but exercise the real shell and its controller below.
        WithPage(width, dark, "zh-CN", (_, _, _, _, _) =>
        {
            PreviewSettings values = new();
            // ThemePropertiesEditor reads this independent instance. No option is clicked, so ApplyTheme is never invoked.
            ThemeConfig theme = new() { Theme = dark ? Theme.Dark : Theme.Light };
            List<ConfigSettingMetadata> settings =
            [
                new() { Name = "主题", Description = "选择应用界面的颜色主题。", Section = ConfigSettingConstants.SectionAppearance,
                    BindingName = nameof(ThemeConfig.Theme), Source = theme, Layout = ConfigSettingLayout.Wide, Order = -40 },
                new() { Name = "用户界面语言", Description = "选择界面显示语言，跟随系统时使用系统语言。", Section = ConfigSettingConstants.SectionAppearance,
                    BindingName = nameof(PreviewSettings.Language), Source = values, Order = -30 },
                new() { Name = "主程序更新", Section = ConfigSettingConstants.SectionUpdates, BindingName = "IsAutoUpdate", Source = new AutoUpdateConfig(), Order = 500 },
                new() { Name = "插件更新", Section = ConfigSettingConstants.SectionUpdates, BindingName = "IsAutoUpdate", Source = new MarketplaceWindowConfig(), Order = 510 },
                new() { Name = "更新前创建程序快照", Description = "在安装更新前保留当前程序文件，以便回退。", Section = ConfigSettingConstants.SectionUpdates,
                    BindingName = nameof(PreviewSettings.CreateSnapshot), Source = values, Order = 520 },
                new() { Name = "不使用系统代理", Description = "network-only-fixture：仅用于验证搜索不会访问网络。", Section = ConfigSettingConstants.SectionUpdates,
                    BindingName = nameof(PreviewSettings.NoProxy), Source = values, Order = 530 },
                new() { Name = "日志级别", Description = "控制应用写入日志的详细程度。", Section = ConfigSettingConstants.SectionDiagnostics,
                    BindingName = nameof(PreviewSettings.LogLevel), Source = values, Order = 600 },
                new() { Name = "隔离工具", Description = "仅用于离屏验证，不连接生产服务。", Type = ConfigSettingType.TabItem,
                    ViewType = typeof(PreviewToolsPage), Order = 850 }
            ];
            for (int index = 0; index < 6; index++)
                settings.Add(new() { Name = $"诊断选项 {index + 1}", Description = "合成设置用于验证长页面的滚动与分区，不修改运行参数。",
                    Section = ConfigSettingConstants.SectionDiagnostics, BindingName = nameof(PreviewSettings.EnableDiagnostics), Source = values, Order = 610 + index });
            ConstructorInfo constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                [typeof(IEnumerable<ConfigSettingMetadata>)], null) ?? throw new InvalidOperationException("The isolated settings constructor must remain available.");
            SettingWindow window = (SettingWindow)constructor.Invoke([settings]);
            try
            {
                Grid host = Assert.IsType<Grid>(window.Content);
                RefreshLayout(host, width);
                action(window, host);
            }
            finally { window.Close(); }
        });
    }

    private static StorageCleanupItem Item(StorageMaintenanceViewModel model, string id) => model.Items.Single(item => item.Id == id);

    private static void WithPage(int width, bool dark, string culture,
        Action<Grid, StorageMaintenanceControl, ScrollViewer, StorageMaintenanceViewModel, Sandbox> action)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
            ResourceDictionary resources = Application.Current.Resources;
            List<ResourceDictionary> dictionaries = [];
            Dictionary<object, object> previousLocalResources = [];
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                })
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(source, UriKind.Relative) };
                    resources.MergedDictionaries.Add(dictionary);
                    dictionaries.Add(dictionary);
                }
                foreach (object key in resources.Keys.Cast<object>().ToArray())
                {
                    if (!dictionaries.Any(dictionary => dictionary.Contains(key))) continue;
                    previousLocalResources[key] = resources[key];
                    resources.Remove(key);
                }
                // Production ThemeResourceDictionary also contributes these derived surface/font tokens.
                // Create only the in-memory resources; applying ThemeManager would start its OS monitor.
                ResourceDictionary appearance = new ThemeAppearance().CreateResources(dark ? Theme.Dark : Theme.Light, resources);
                foreach (object key in appearance.Keys.Cast<object>().ToArray())
                {
                    if (!resources.Keys.Cast<object>().Contains(key)) continue;
                    previousLocalResources[key] = resources[key];
                    resources.Remove(key);
                }
                resources.MergedDictionaries.Add(appearance);
                dictionaries.Add(appearance);

                using Sandbox sandbox = new();
                StorageMaintenanceViewModel model = sandbox.CreateModel();
                StorageMaintenanceControl page = new(model, null,
                    new StorageOverviewViewModel([new PreviewUsageProvider(sandbox.Root, 5)]));
                Grid host = new();
                host.SetResourceReference(Panel.BackgroundProperty, "GlobalBackground");
                host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
                host.ColumnDefinitions.Add(new ColumnDefinition());
                StackPanel navigation = new() { Margin = new Thickness(24, 28, 20, 0) };
                navigation.Children.Add(new TextBlock { Text = culture == "zh-CN" ? "选项" : "Options", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 28) });
                navigation.Children.Add(new TextBlock { Text = culture == "zh-CN" ? "常规" : "General", FontSize = 14, Margin = new Thickness(0, 0, 0, 22) });
                navigation.Children.Add(new TextBlock { Text = MaintenanceText.Title, FontSize = 14, FontWeight = FontWeights.SemiBold });
                foreach (TextBlock text in navigation.Children.OfType<TextBlock>()) text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                host.Children.Add(navigation);
                StackPanel content = new() { Margin = new Thickness(16, 14, 16, 20) };
                TextBlock title = new() { Text = MaintenanceText.Title, FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 18) };
                title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                content.Children.Add(title);
                content.Children.Add(page);
                ScrollViewer scroll = new() { Content = content, Margin = new Thickness(20), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                Grid.SetColumn(scroll, 1);
                host.Children.Add(scroll);
                // No Window.Show, production App startup, ConfigHandler or live cache/database provider is used.
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                CompleteWithDispatcher(page.ScanTask);
                try
                {
                    RefreshLayout(host, width);
                    action(host, page, scroll, model, sandbox);
                }
                finally { page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
            }
            finally
            {
                for (int index = dictionaries.Count - 1; index >= 0; index--)
                    resources.MergedDictionaries.Remove(dictionaries[index]);
                foreach ((object key, object value) in previousLocalResources) resources[key] = value;
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        });
    }

    private static void RefreshLayout(Grid host, int width)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            host.Measure(new Size(width, 760));
            host.Arrange(new Rect(0, 0, width, 760));
            host.UpdateLayout();
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }

    private static void CaptureStoragePreview(FrameworkElement content, string fileName)
    {
        string? directory = Environment.GetEnvironmentVariable("STORAGE_RENDER_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (DrawingContext drawing = visual.RenderOpen())
        {
            Rect bounds = new(0, 0, content.ActualWidth, content.ActualHeight);
            Brush background = content.TryFindResource("CV.Surface.Sidebar") as Brush
                ?? content.TryFindResource("GlobalBackground") as Brush ?? Brushes.White;
            drawing.DrawRectangle(background, null, bounds);
            drawing.DrawRectangle(new VisualBrush(content), null, bounds);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(directory, fileName));
        encoder.Save(stream);
    }

    private static void CaptureNormalStoragePreview(int width, bool dark)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STORAGE_RENDER_DIR"))) return;
        WithMaintenanceSettings(width, dark, 5, (_, host, page, _, _) =>
        {
            RefreshLayout(host, width);
            CompleteWithDispatcher(page.ScanTask);
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            RefreshLayout(host, width);
            Assert.False(page.Overview.IsBusy);
            Assert.True(page.Overview.IsComplete);
            Assert.Equal(5, page.Overview.Items.Count);
            Assert.Equal(15L * 1024 * 1024 * 1024, page.Overview.TotalBytes);
            ItemsControl legend = Element<ItemsControl>(page, "OverviewLegend");
            ItemsControl managedSources = Assert.Single(Descendants(page).OfType<ItemsControl>(), control =>
                !ReferenceEquals(control, legend) && control.ItemsSource is IEnumerable<StorageUsageItem>);
            Assert.Equal(5, managedSources.Items.Count);
            Assert.All(page.Overview.Items, item =>
            {
                Assert.True(item.IsScanned);
                Assert.Contains(Descendants(legend).OfType<TextBlock>(), text => text.Text == item.SizeText);
            });
            CaptureStoragePreview(host, $"storage-{width}-{(dark ? "dark" : "light")}.png");
        });
    }

    private static void CompleteWithDispatcher(Task task)
    {
        if (!task.IsCompleted)
        {
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherFrame frame = new();
            DispatcherTimer timeout = new(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(10) };
            timeout.Tick += (_, _) => frame.Continue = false;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            timeout.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timeout.Stop(); }
        }
        Assert.True(task.IsCompleted, "The injected maintenance operation must finish while the dispatcher remains responsive.");
        task.GetAwaiter().GetResult();
    }

    private static void AssertTextFits(TextBlock text)
    {
        FormattedText measured = MeasureText(text.Text, text);
        if (text.TextWrapping == TextWrapping.NoWrap)
            Assert.True(measured.WidthIncludingTrailingWhitespace <= text.ActualWidth + 2,
                $"Text '{text.Text}' is horizontally clipped ({measured.WidthIncludingTrailingWhitespace:0.##} > {text.ActualWidth:0.##}).");
        else
        {
            measured.MaxTextWidth = Math.Max(1, text.ActualWidth);
            Assert.True(measured.Height <= text.ActualHeight + 3,
                $"Wrapped text '{text.Text}' is vertically clipped ({measured.Height:0.##} > {text.ActualHeight:0.##}).");
        }
    }

    private static FormattedText MeasureText(string text, Control control) => new(text, CultureInfo.CurrentUICulture, control.FlowDirection,
        new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch), control.FontSize,
        control.Foreground, VisualTreeHelper.GetDpi(control).PixelsPerDip);

    private static FormattedText MeasureText(string text, TextBlock block) => new(text, CultureInfo.CurrentUICulture, block.FlowDirection,
        new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize,
        block.Foreground, VisualTreeHelper.GetDpi(block).PixelsPerDip);

    private static T Element<T>(FrameworkElement root, string name) where T : FrameworkElement
        => Assert.IsType<T>(root.FindName(name) ?? Descendants(root).FirstOrDefault(element => element.Name == name));

    private static Rect BoundsIn(FrameworkElement element, Visual ancestor) => element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
    private static string ElementLabel(FrameworkElement element) => element is TextBlock text ? text.Text : element is ContentControl content ? content.Content?.ToString() ?? element.Name : element.Name;

    private static bool IsEffectivelyVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        return element.ActualWidth > 0 && element.ActualHeight > 0;
    }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is FrameworkElement element) yield return element;
            foreach (FrameworkElement descendant in Descendants(child)) yield return descendant;
        }
    }

    public enum PreviewLanguage { System, Chinese, English }
    public enum PreviewLogLevel { Debug, Info, Warn, Error }
    public sealed class PreviewSettings
    {
        public PreviewLanguage Language { get; set; }
        public PreviewLogLevel LogLevel { get; set; } = PreviewLogLevel.Info;
        public bool CreateSnapshot { get; set; }
        public bool NoProxy { get; set; } = true;
        public bool EnableDiagnostics { get; set; }
    }
    public sealed class AutoUpdateConfig { public bool IsAutoUpdate { get; set; } = true; }
    public sealed class MarketplaceWindowConfig { public bool IsAutoUpdate { get; set; } = true; }
    public sealed class PreviewToolsPage : UserControl
    {
        public PreviewToolsPage() => Content = new Border { Height = 1600, Child = new TextBlock { Text = "Isolated UI fixture", VerticalAlignment = VerticalAlignment.Top } };
    }
    public sealed class PreviewMaintenancePage : UserControl
    {
        internal static Func<StorageMaintenanceControl>? CreatePage { get; set; }
        public StorageMaintenanceControl Page { get; }
        public PreviewMaintenancePage()
        {
            Page = CreatePage?.Invoke() ?? throw new InvalidOperationException("The maintenance test fixture must be explicitly injected.");
            Content = Page;
        }
    }

    private sealed class PreviewUsageProvider(string root, int count) : IStorageUsageProvider
    {
        public void PrepareStorageScan() { }
        public IEnumerable<StorageUsageCategory> GetStorageCategories() => Enumerable.Range(0, count).Select(index => new PreviewUsageCategory(root, index));
    }

    private sealed class PreviewUsageCategory(string root, int index) : StorageUsageCategory
    {
        public override string Id => $"fixture.usage.{index}";
        public override string Title => new[] { "相机图像", "算法结果", "相机资源", "预处理目录", "文件缓存" }[index % 5] + (index < 5 ? "" : $" {index + 1}");
        public override string Description => "仅用于隔离布局验证的存储来源。";
        public override bool IsProtected => index == 2;
        public override IReadOnlyList<string> SourceDirectories => [root];
        public override StorageUsageSnapshot Scan(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long bytes = (index + 1L) * 1024 * 1024 * 1024;
            return new(bytes, 1, [new FileMaintenanceEntry(Path.Combine(root, $"usage-{index}.dat"), bytes, DateTime.UnixEpoch)], []);
        }
    }

    private sealed class Sandbox : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"ColorVisionStorageMaintenanceTests-{Guid.NewGuid():N}");
        public Dictionary<string, string> Files { get; } = [];
        public object ThumbnailToken { get; } = new();
        public StorageCacheSnapshot ThumbnailSnapshot => new(64 * 1024, 3, ThumbnailToken);
        public Func<StorageCacheSnapshot>? ScanThumbnailOverride { get; set; }
        public Func<StorageCacheSnapshot, StorageCacheCleanupResult>? ClearThumbnailOverride { get; set; }
        public int RuleFactoryCalls { get; private set; }
        public int ThumbnailScanCalls { get; private set; }
        public int ThumbnailClearCalls { get; private set; }
        public StorageCacheSnapshot? LastClearedSnapshot { get; private set; }
        public (int, int, int) LastRetention { get; private set; }

        public Sandbox()
        {
            Directory.CreateDirectory(Root);
            foreach (string id in new[] { "logs", "temp", "cie-cache", "packages" }) Files[id] = CreateOldFile(id, "old.dat");
        }

        public string CreateOldFile(string id, string name)
        {
            string directory = Path.Combine(Root, id);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, "Isolated maintenance fixture. " + id);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-400));
            return path;
        }

        public StorageMaintenanceViewModel CreateModel(StorageMaintenanceConfig? config = null) => new(config ?? new(),
            (logs, temp, packages) =>
            {
                RuleFactoryCalls++;
                LastRetention = (logs, temp, packages);
                return new MaintenanceFileCleanupRule[]
                {
                    new("logs", Path.Combine(Root, "logs"), "*.dat", RetentionDays: logs),
                    new("temp", Path.Combine(Root, "temp"), "*.dat", RetentionDays: temp),
                    new("cie-cache", Path.Combine(Root, "cie-cache"), "*.dat", RetentionDays: 0),
                    new("packages", Path.Combine(Root, "packages"), "*.dat", RetentionDays: packages)
                };
            },
            () =>
            {
                ThumbnailScanCalls++;
                return ScanThumbnailOverride?.Invoke() ?? ThumbnailSnapshot;
            },
            snapshot =>
            {
                ThumbnailClearCalls++;
                LastClearedSnapshot = snapshot;
                return ClearThumbnailOverride?.Invoke(snapshot) ?? new(snapshot.Bytes, snapshot.Count, 0, null);
            });

        public void Dispose()
        {
            string fullPath = Path.GetFullPath(Root);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(fullPath).StartsWith("ColorVisionStorageMaintenanceTests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to delete a directory outside the isolated maintenance test scope.");
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, recursive: true);
        }
    }
}
