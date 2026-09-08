using WindowsServicePlugin.CVWinSMS;
using System.Reflection;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public class InstallToolAsyncCommandTests
{
    [Fact]
    public void AutomaticStartupDiscoveryExcludesBothLegacyToolCommands()
    {
        WithIsolatedDiscovery(handler =>
        {
            // Exercise the host's discovery implementation without running initializers
            // from unrelated assemblies or making any network/process calls.
            var types = handler.GetTypes(typeof(InstallTool).Assembly);
            Assert.Contains(typeof(InstallTool), types);
            Assert.Contains(typeof(CheckInstallToolUpdates), types);
            var initializers = handler.LoadImplementations<IMainWindowInitialized>();

            Assert.DoesNotContain(initializers, item => item.GetType() == typeof(InstallTool));
            Assert.DoesNotContain(initializers, item => item.GetType() == typeof(CheckInstallToolUpdates));
        });
    }

    [Fact]
    public void ManualUpdateMenuRetainsTheExistingOpenEntryAndServiceGroup()
    {
        WithIsolatedDiscovery(_ =>
        {
            IMenuItem open = new InstallTool();
            IMenuItem check = new CheckInstallToolUpdates();
            IMenuItem group = new WindowsServicePlugin.Menus.ServiceLog();

            Assert.Equal("Help", group.OwnerGuid);
            Assert.Equal("ServiceLog", group.GuidId);
            Assert.Equal(group.GuidId, open.OwnerGuid);
            Assert.Equal(group.GuidId, check.OwnerGuid);
            Assert.Equal("InstallTool", open.GuidId);
            Assert.Equal("CheckInstallToolUpdates", check.GuidId);
            Assert.Equal(1, open.Order);
            Assert.Equal(2, check.Order);
            Assert.Equal(MenuItemConstants.MainWindowTarget, open.TargetName);
            Assert.Equal(open.TargetName, check.TargetName);
            Assert.False(string.IsNullOrWhiteSpace(open.Header));
            Assert.Equal("检查旧服务管理工具更新", check.Header);
            Assert.NotNull(open.Command);
            Assert.NotNull(check.Command);
            Assert.Equal(typeof(Task), typeof(InstallTool).GetMethod(nameof(InstallTool.Initialize))!.ReturnType);
        });
    }

    [Fact]
    public void MenuManagerBuildsTheVisibleLegacyToolSubmenuUnderHelp()
    {
        WithIsolatedDiscovery(_ =>
        {
            AssemblyHandler discovery = AssemblyHandler.GetInstance();
            FieldInfo assembliesField = typeof(AssemblyHandler)
                .GetField("_assemblies", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object? previousAssemblies = assembliesField.GetValue(discovery);
            IMenuService? previousMenuService = MenuService.Instance;
            MenuManager? manager = null;
            var menu = new Menu();
            try
            {
                // Use real type discovery and tree construction, with only these
                // contributions instantiated. Keep global discovery state intact afterward.
                assembliesField.SetValue(discovery, new[] { typeof(MenuHelp).Assembly, typeof(InstallTool).Assembly });
                manager = (MenuManager)Activator.CreateInstance(typeof(MenuManager), nonPublic: true)!;
                Type[] contributions = [typeof(MenuHelp), typeof(WindowsServicePlugin.Menus.ServiceLog),
                    typeof(InstallTool), typeof(CheckInstallToolUpdates)];

                manager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, menu, contributions.Contains);

                MenuItem help = Assert.Single(menu.Items.OfType<MenuItem>());
                Assert.IsType<MenuHelp>(help.Tag);
                MenuItem service = Assert.Single(help.Items.OfType<MenuItem>());
                Assert.IsType<WindowsServicePlugin.Menus.ServiceLog>(service.Tag);
                Assert.True(service.HasItems);
                Assert.Equal(Visibility.Visible, service.Visibility);
                Assert.Equal(2, service.Items.Count);
                Assert.Collection(service.Items.OfType<MenuItem>(),
                    item => Assert.IsType<InstallTool>(item.Tag),
                    item => Assert.IsType<CheckInstallToolUpdates>(item.Tag));
                Assert.All(service.Items.OfType<MenuItem>(), item =>
                {
                    Assert.Equal(Visibility.Visible, item.Visibility);
                    Assert.NotNull(item.Command);
                    Assert.True(item.Command.CanExecute(null));
                });
            }
            finally
            {
                manager?.UnregisterMenu(menu);
                menu.Items.Clear();
                MenuService.SetInstance(previousMenuService!);
                assembliesField.SetValue(discovery, previousAssemblies);
            }
        });
    }

    [Fact]
    public async Task MissingInstalledToolReportsItsStateWithoutRequestingAVersion()
    {
        int requests = 0;
        var statuses = new List<string>();
        var offers = new List<Version>();

        await CheckForUpdatesAsync(() => null, () =>
        {
            requests++;
            throw new InvalidOperationException("A missing tool must not start an update request.");
        }, statuses, offers.Add);

        Assert.Equal(0, requests);
        Assert.Equal(["ToolMissing"], statuses);
        Assert.Empty(offers);
    }

    [Fact]
    public async Task UnreadableInstalledVersionDoesNotStartARequestOrOfferAnUpdate()
    {
        int requests = 0;
        var statuses = new List<string>();
        var offers = new List<Version>();

        await CheckForUpdatesAsync(
            () => throw new InvalidOperationException("Invalid local version metadata."),
            () =>
            {
                requests++;
                return Task.FromResult(new Version(2, 0));
            }, statuses, offers.Add);

        Assert.Equal(0, requests);
        Assert.Equal(["Unavailable"], statuses);
        Assert.Empty(offers);
    }

    [Fact]
    public async Task ManualRequestFailureAfterAwaitCannotReachDownloadOrProcessReplacement()
    {
        int requests = 0;
        int replacementOffers = 0;
        var statuses = new List<string>();

        await CheckForUpdatesAsync(() => new Version(1, 0), async () =>
        {
            requests++;
            await Task.Yield();
            throw new InvalidOperationException("Synthetic request failure.");
        }, statuses, _ => replacementOffers++);

        Assert.Equal(1, requests);
        Assert.Equal(["Unavailable"], statuses);
        // Download and process replacement live behind this callback in production.
        Assert.Equal(0, replacementOffers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0.0")]
    [InlineData("0.0.0")]
    [InlineData("0.0.0.0")]
    public async Task InvalidRemoteVersionCannotOfferAnUpdateOrBecomeAPackageVersion(string? versionText)
    {
        Version? version = versionText == null ? null : new Version(versionText);
        var statuses = new List<string>();
        var offers = new List<Version>();
        Func<Task<Version>> getLatest = () => Task.FromResult(version!);

        await CheckForUpdatesAsync(() => new Version(1, 0), getLatest, statuses, offers.Add);

        Assert.Equal(["Unavailable"], statuses);
        Assert.Empty(offers);
        await Assert.ThrowsAsync<InvalidOperationException>(() => GetRequiredLatestVersionAsync(getLatest));
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("2.0.0")]
    public async Task CurrentOrNewerInstalledToolDoesNotOfferReplacement(string installedVersion)
    {
        var statuses = new List<string>();
        var offers = new List<Version>();

        await CheckForUpdatesAsync(() => new Version(installedVersion),
            () => Task.FromResult(new Version(1, 2, 3)), statuses, offers.Add);

        Assert.Equal(["UpToDate"], statuses);
        Assert.Empty(offers);
    }

    [Fact]
    public async Task NewRemoteVersionIsRequestedOnceAndOfferedForConfirmation()
    {
        int requests = 0;
        var latest = new Version(2, 0, 0);
        var statuses = new List<string>();
        var offers = new List<Version>();

        await CheckForUpdatesAsync(() => new Version(1, 2, 3), () =>
        {
            requests++;
            return Task.FromResult(latest);
        }, statuses, offers.Add);

        Assert.Equal(1, requests);
        Assert.Empty(statuses);
        Assert.Same(latest, Assert.Single(offers));
    }

    [Fact]
    public async Task MenuBoundaryObservesFailureThrownAfterAwait()
    {
        var expected = new InvalidOperationException("invalid version");
        Exception? reported = null;

        MethodInfo boundary = typeof(InstallTool).GetMethod(
            "ExecuteMenuActionAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        Func<Task> failAfterAwait = async () =>
        {
            await Task.Yield();
            throw expected;
        };
        Action<Exception> reportFailure = ex => reported = ex;

        await (Task)boundary.Invoke(null, [failAfterAwait, reportFailure])!;

        Assert.Same(expected, reported);
        Assert.Equal(
            typeof(Task),
            typeof(InstallTool).GetMethod(nameof(InstallTool.Download))!.ReturnType);
    }

    private static Task CheckForUpdatesAsync(Func<Version?> getInstalledVersion,
        Func<Task<Version>> getLatestVersion, List<string> statuses, Action<Version> offerUpdate)
    {
        MethodInfo method = GetInternalMethod("CheckForUpdatesAsync");
        Type statusType = method.GetParameters()[2].ParameterType.GenericTypeArguments[0];
        object reportStatus = typeof(InstallToolAsyncCommandTests)
            .GetMethod(nameof(CreateStatusRecorder), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(statusType).Invoke(null, [statuses])!;
        return (Task)method.Invoke(null, [getInstalledVersion, getLatestVersion, reportStatus, offerUpdate])!;
    }

    private static Action<TStatus> CreateStatusRecorder<TStatus>(List<string> statuses) where TStatus : struct, Enum
        => status => statuses.Add(status.ToString());

    private static Task<Version> GetRequiredLatestVersionAsync(Func<Task<Version>> getLatestVersion)
        => (Task<Version>)GetInternalMethod("GetRequiredLatestVersionAsync").Invoke(null, [getLatestVersion])!;

    private static MethodInfo GetInternalMethod(string name)
        => typeof(InstallTool).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"InstallTool.{name} was not found.");

    private static void WithIsolatedDiscovery(Action<AssemblyHandler> action) => WpfTestHost.Invoke(() =>
    {
        IConfigService? previousConfig = ConfigService.Instance;
        IAssemblyService? previousAssemblyService = AssemblyService.Instance;
        try
        {
            // No Load/Save: the tool path is empty and configuration stays in memory.
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            var handler = (AssemblyHandler)Activator.CreateInstance(typeof(AssemblyHandler), nonPublic: true)!;
            typeof(AssemblyHandler).GetField("_assemblies", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(handler, new[] { typeof(InstallTool).Assembly });
            action(handler);
        }
        finally
        {
            AssemblyService.SetInstance(previousAssemblyService!);
            ConfigService.SetInstance(previousConfig!);
        }
    });
}
