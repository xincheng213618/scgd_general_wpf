#pragma warning disable CA1707
using ColorVision.UI.Desktop.MenuItemManager;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public class MenuItemManagerServiceTests
{
    [Fact]
    public void CreateEditingSnapshot_DoesNotExposePersistedOverridesForMutation()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new() { GuidId = "Child", IsVisible = false }
            }
        };

        ObservableCollection<MenuItemSetting> snapshot = MenuItemManagerService.CreateEditingSnapshot(
            [new TestMenuItem("Child", "Parent", 7, "Child header")],
            config);

        MenuItemSetting setting = Assert.Single(snapshot);
        Assert.False(setting.IsVisible);
        Assert.Equal("Child header", setting.Header);
        Assert.Equal(7, setting.DefaultOrder);

        setting.IsVisible = true;
        Assert.False(Assert.Single(config.Overrides).IsVisible);
    }

    [Fact]
    public void CreateSparseOverrides_PersistsOnlyCustomizedItems()
    {
        MenuItemSetting[] settings =
        [
            new() { GuidId = "Default" },
            new() { GuidId = "Hidden", IsVisible = false },
            new() { GuidId = "Ordered", OrderOverride = 12 },
            new() { GuidId = "Moved", OwnerGuidOverride = "Tools" },
        ];

        ObservableCollection<MenuItemOverride> overrides = MenuItemManagerService.CreateSparseOverrides(settings);

        Assert.Equal(3, overrides.Count);
        Assert.DoesNotContain(overrides, item => item.GuidId == "Default");
        Assert.False(overrides.Single(item => item.GuidId == "Hidden").IsVisible);
        Assert.Equal(12, overrides.Single(item => item.GuidId == "Ordered").OrderOverride);
        Assert.Equal("Tools", overrides.Single(item => item.GuidId == "Moved").OwnerGuidOverride);
    }

    [Fact]
    public void CreateEditingSnapshot_MigratesLegacyFullSnapshotOnce()
    {
        var config = new MenuItemManagerConfig
        {
            Settings = new ObservableCollection<MenuItemSetting>
            {
                new() { GuidId = "Default", Header = "Old metadata" },
                new() { GuidId = "Hidden", IsVisible = false, Header = "Old hidden metadata" },
            }
        };
        IMenuItem[] menuItems =
        [
            new TestMenuItem("Default", MenuItemConstants.Menu, 1, "Default"),
            new TestMenuItem("Hidden", MenuItemConstants.Menu, 2, "Hidden"),
        ];

        ObservableCollection<MenuItemSetting> first = MenuItemManagerService.CreateEditingSnapshot(menuItems, config);
        ObservableCollection<MenuItemSetting> second = MenuItemManagerService.CreateEditingSnapshot(menuItems, config);

        Assert.Null(config.Settings);
        MenuItemOverride migrated = Assert.Single(config.Overrides);
        Assert.Equal("Hidden", migrated.GuidId);
        Assert.False(migrated.IsVisible);
        Assert.False(first.Single(item => item.GuidId == "Hidden").IsVisible);
        Assert.Equal(first.Count, second.Count);
        Assert.Single(config.Overrides);
    }

    [Fact]
    public void SparseConfigSerialization_OmitsLegacySnapshotAndDefaultFields()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new() { GuidId = "Ordered", OrderOverride = 42 }
            }
        };

        string json = JsonConvert.SerializeObject(config);

        Assert.DoesNotContain("Settings", json, StringComparison.Ordinal);
        Assert.DoesNotContain("IsVisible", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Header", json, StringComparison.Ordinal);
        Assert.Contains("OrderOverride", json, StringComparison.Ordinal);
    }

    [Fact]
    public void IsValidOwnerOverride_UsesEditingSnapshotAndRejectsCycles()
    {
        var parent = new MenuItemSetting { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "Parent", OwnerGuid = "Child" };
        var child = new MenuItemSetting { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "Child", OwnerGuid = MenuItemConstants.Menu };
        MenuItemSetting[] settings = [parent, child];

        Assert.False(MenuItemManagerService.IsValidOwnerOverride(child, "Parent", settings));
        Assert.True(MenuItemManagerService.IsValidOwnerOverride(child, MenuItemConstants.Menu, settings));
    }

    [Fact]
    public void CreateEditingSnapshot_DistinguishesEqualGuidsByTargetName()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new() { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "Shared", IsVisible = false }
            }
        };
        IMenuItem[] menuItems =
        [
            new TestMenuItem("Shared", MenuItemConstants.Menu, 1, "Main", MenuItemConstants.MainWindowTarget),
            new TestMenuItem("Shared", MenuItemConstants.Menu, 2, "Spectrum", "SpectrumWindow"),
        ];

        ObservableCollection<MenuItemSetting> snapshot = MenuItemManagerService.CreateEditingSnapshot(menuItems, config);

        Assert.Equal(2, snapshot.Count);
        Assert.False(snapshot.Single(item => item.TargetName == MenuItemConstants.MainWindowTarget).IsVisible);
        Assert.True(snapshot.Single(item => item.TargetName == "SpectrumWindow").IsVisible);
    }

    [Fact]
    public void LegacyUnscopedOverride_ExpandsToEveryMatchingScopeAndBecomesExplicit()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new() { GuidId = "Shared", OrderOverride = 42 }
            }
        };
        IMenuItem[] menuItems =
        [
            new TestMenuItem("Shared", MenuItemConstants.Menu, 1, "Main", MenuItemConstants.MainWindowTarget),
            new TestMenuItem("Shared", MenuItemConstants.Menu, 2, "Spectrum", "SpectrumWindow"),
        ];

        ObservableCollection<MenuItemSetting> snapshot = MenuItemManagerService.CreateEditingSnapshot(menuItems, config);
        ObservableCollection<MenuItemOverride> sparse = MenuItemManagerService.CreateSparseOverrides(snapshot);

        Assert.All(snapshot, item => Assert.Equal(42, item.OrderOverride));
        Assert.Equal(2, sparse.Count);
        Assert.Contains(sparse, item => item.TargetName == MenuItemConstants.MainWindowTarget && item.GuidId == "Shared");
        Assert.Contains(sparse, item => item.TargetName == "SpectrumWindow" && item.GuidId == "Shared");
        Assert.DoesNotContain(sparse, item => string.IsNullOrWhiteSpace(item.TargetName));
    }

    [Fact]
    public void OwnerScope_AllowsSameTargetOrGlobalButRejectsAnotherWindow()
    {
        var source = new MenuItemSetting { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "Source" };
        var sameTarget = new MenuItemSetting { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "Same" };
        var global = new MenuItemSetting { TargetName = MenuItemConstants.GlobalTarget, GuidId = "GlobalParent" };
        var anotherWindow = new MenuItemSetting { TargetName = "SpectrumWindow", GuidId = "Other" };
        MenuItemSetting[] settings = [source, sameTarget, global, anotherWindow];

        Assert.True(MenuItemManagerService.IsOwnerInAllowedScope(source, sameTarget));
        Assert.True(MenuItemManagerService.IsOwnerInAllowedScope(source, global));
        Assert.False(MenuItemManagerService.IsOwnerInAllowedScope(source, anotherWindow));
        Assert.False(MenuItemManagerService.IsValidOwnerOverride(source, anotherWindow.GuidId, settings));
    }

    [Fact]
    public void CreateEditingSnapshot_DropsRetiredMenuEntryOverrides()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new() { GuidId = "MenuMenuItemManager", IsVisible = false },
                new() { TargetName = MenuItemConstants.GlobalTarget, GuidId = "MenuMenuItemManager", OrderOverride = 1 },
                new() { GuidId = "ServiceManager", IsVisible = false },
                new() { TargetName = MenuItemConstants.MainWindowTarget, GuidId = "ServiceManager", OrderOverride = 2 },
                new() { TargetName = "SpectrumWindow", GuidId = "KeepMe", IsVisible = false },
            }
        };

        ObservableCollection<MenuItemSetting> snapshot = MenuItemManagerService.CreateEditingSnapshot([], config);

        MenuItemOverride remainingOverride = Assert.Single(config.Overrides);
        Assert.Equal("KeepMe", remainingOverride.GuidId);
        Assert.Equal("SpectrumWindow", remainingOverride.TargetName);
        Assert.Equal("KeepMe", Assert.Single(snapshot).GuidId);
    }

    [Fact]
    public void UnavailablePluginOwnerOverride_RoundTripsWithoutCatalogMetadata()
    {
        var config = new MenuItemManagerConfig
        {
            Overrides = new ObservableCollection<MenuItemOverride>
            {
                new()
                {
                    TargetName = "PluginWindow",
                    GuidId = "PluginChild",
                    OwnerGuidOverride = "PluginParent",
                }
            }
        };

        MenuItemSetting orphan = Assert.Single(MenuItemManagerService.CreateEditingSnapshot([], config));
        MenuItemOverride persisted = Assert.Single(MenuItemManagerService.CreateSparseOverrides([orphan]));

        Assert.Null(orphan.SourceType);
        Assert.Null(orphan.SourceAssembly);
        Assert.Equal("PluginWindow", persisted.TargetName);
        Assert.Equal("PluginChild", persisted.GuidId);
        Assert.Equal("PluginParent", persisted.OwnerGuidOverride);
    }

    private sealed class TestMenuItem : IMenuItem
    {
        public TestMenuItem(string guidId, string ownerGuid, int order, string header, string targetName = MenuItemConstants.MainWindowTarget)
        {
            GuidId = guidId;
            OwnerGuid = ownerGuid;
            Order = order;
            Header = header;
            TargetName = targetName;
        }

        public string TargetName { get; }
        public string? OwnerGuid { get; }
        public string? GuidId { get; }
        public int Order { get; }
        public string? Header { get; }
        public string? InputGestureText => null;
        public object? Icon => null;
        public ICommand? Command => null;
        public Visibility Visibility => Visibility.Visible;
        public bool? IsChecked => null;
    }
}
