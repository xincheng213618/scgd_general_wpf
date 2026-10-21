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
    public void AutomaticStartupDiscoveryExcludesLegacyToolCommand()
    {
        WithIsolatedDiscovery(handler =>
        {
            // Exercise the host's discovery implementation without running initializers
            // from unrelated assemblies or making any network/process calls.
            var types = handler.GetTypes(typeof(InstallTool).Assembly);
            Assert.Contains(typeof(InstallTool), types);
            var initializers = handler.LoadImplementations<IMainWindowInitialized>();

            Assert.DoesNotContain(initializers, item => item.GetType() == typeof(InstallTool));
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
                Type[] contributions = [typeof(MenuHelp), typeof(WindowsServicePlugin.Menus.ServiceLog), typeof(InstallTool)];

                manager.LoadMenuForWindow(MenuItemConstants.MainWindowTarget, menu, contributions.Contains);

                MenuItem help = Assert.Single(menu.Items.OfType<MenuItem>());
                Assert.IsType<MenuHelp>(help.Tag);
                MenuItem service = Assert.Single(help.Items.OfType<MenuItem>());
                Assert.IsType<WindowsServicePlugin.Menus.ServiceLog>(service.Tag);
                Assert.True(service.HasItems);
                Assert.Equal(Visibility.Visible, service.Visibility);
                Assert.IsType<InstallTool>(Assert.Single(service.Items.OfType<MenuItem>()).Tag);
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

    [Theory]
    [InlineData(null)]
    [InlineData("0.0")]
    [InlineData("0.0.0")]
    [InlineData("0.0.0.0")]
    public async Task InvalidRemoteVersionCannotBecomeADownloadPackageVersion(string? versionText)
    {
        Version? version = versionText == null ? null : new Version(versionText);
        Func<Task<Version>> getLatest = () => Task.FromResult(version!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => GetRequiredLatestVersionAsync(getLatest));
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
