using ColorVision.Recovery;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Languages;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupThemeBootstrapTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{")]
    [InlineData("[]")]
    public void MissingOrInvalidConfigurationBuildsKeyDefaultsWithoutThemeResources(string? invalidJson)
    {
        Run((config, manager) =>
        {
            if (invalidJson != null)
                File.WriteAllText(config.ConfigFilePath, invalidJson);

            config.LoadConfigs();

            // Default discovery catches individual constructor failures. Assert that these
            // objects were actually created, before GetRequiredService can create replacements.
            Assert.True(config.Configs.ContainsKey(typeof(ThemeConfig)));
            Assert.True(config.Configs.ContainsKey(typeof(LanguageConfig)));
            Assert.True(config.Configs.ContainsKey(typeof(LogConfig)));
            Assert.True(config.Configs.ContainsKey(typeof(Authorization)));
            Assert.Equal(Theme.UseSystem, config.GetRequiredService<ThemeConfig>().Theme);
            Assert.NotNull(config.GetRequiredService<LogConfig>().LogLevel);
            Assert.False(string.IsNullOrWhiteSpace(config.GetRequiredService<LanguageConfig>().UICulture));
            Authorization.Instance = config.GetRequiredService<Authorization>();
            Assert.Equal(PermissionMode.Administrator, Authorization.Instance.PermissionMode);
            AssertNoThemeResources();

            manager.AppsTheme = Theme.Dark;
            Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);
            Assert.Equal(Theme.UseSystem, manager.CurrentTheme);
            Assert.Equal(Theme.Dark, manager.CurrentUITheme);
            AssertStartupWindowsCanBeConstructed();

            // Neither default recovery nor first theme application should silently rewrite
            // the missing/damaged file or manufacture a persisted backup.
            if (invalidJson == null)
                Assert.False(File.Exists(config.ConfigFilePath));
            else
                Assert.Equal(invalidJson, File.ReadAllText(config.ConfigFilePath));
            Assert.Empty(Directory.EnumerateFileSystemEntries(config.BackupFolderPath));
        });
    }

    [Theory]
    [InlineData(Theme.Light)]
    [InlineData(Theme.Dark)]
    public void PendingThemeResetCanApplyBeforeResourcesAndUsesTheResolvedSystemTheme(Theme systemTheme)
    {
        Run((config, manager) =>
        {
            var original = new JObject
            {
                [nameof(ThemeConfig)] = new JObject { [nameof(ThemeConfig.Theme)] = (int)Theme.Dark },
                [nameof(LanguageConfig)] = new JObject { [nameof(LanguageConfig.UICulture)] = "zh-Hans" },
            };
            File.WriteAllText(config.ConfigFilePath, original.ToString());
            byte[] originalBytes = File.ReadAllBytes(config.ConfigFilePath);
            var reset = new ConfigMaintenanceResetService(config.ConfigFilePath, [nameof(ThemeConfig)]);
            Assert.True(reset.Schedule(reset.Prepare([nameof(ThemeConfig)])).Succeeded);

            ConfigMaintenanceResetResult result = reset.ApplyPending(() => true);

            Assert.Equal(ConfigMaintenanceResetStatus.Applied, result.Status);
            Assert.Equal(originalBytes, File.ReadAllBytes(result.BackupPath!));
            var saved = JObject.Parse(File.ReadAllText(config.ConfigFilePath));
            Assert.Null(saved[nameof(ThemeConfig)]);
            Assert.True(JToken.DeepEquals(original[nameof(LanguageConfig)], saved[nameof(LanguageConfig)]));
            AssertNoThemeResources();

            config.LoadConfigs();
            Assert.Equal(Theme.UseSystem, ThemeConfig.Instance.Theme);
            Assert.Equal("zh-Hans", LanguageConfig.Instance.UICulture);
            AssertNoThemeResources();

            manager.AppsTheme = systemTheme;
            Application.Current.ApplyTheme(ThemeConfig.Instance.Theme);
            Assert.Equal(Theme.UseSystem, manager.CurrentTheme);
            Assert.Equal(systemTheme, manager.CurrentUITheme);
            AssertStartupWindowsCanBeConstructed();
        });
    }

    private static void AssertNoThemeResources()
    {
        Assert.Empty(Application.Current.Resources.Keys.Cast<object>());
        Assert.Empty(Application.Current.Resources.MergedDictionaries);
    }

    private static void AssertStartupWindowsCanBeConstructed()
    {
        Assert.IsType<ThemeResourceDictionary>(Assert.Single(Application.Current.Resources.MergedDictionaries));
        Assert.IsType<SolidColorBrush>(Application.Current.FindResource("GlobalBackground"));
        Assert.IsType<SolidColorBrush>(Application.Current.FindResource("GlobalTextBrush"));

        StartWindow? startup = null;
        SingleInstanceStartupWindow? singleInstance = null;
        StartupRecoveryWindow? recovery = null;
        int replacementCalls = 0;
        try
        {
            startup = new StartWindow();
            singleInstance = new SingleInstanceStartupWindow((_, _) =>
            {
                replacementCalls++;
                throw new InvalidOperationException("A construction test must not inspect or terminate processes.");
            }, "synthetic process");
            recovery = new StartupRecoveryWindow(null, manualRequest: true);

            Assert.NotNull(startup.Content);
            Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<TextBlock>(startup.FindName("labelVersion")).Text));
            Assert.NotNull(singleInstance.Content);
            Assert.NotNull(Assert.IsType<Button>(singleInstance.FindName("ForceButton")).Style);
            Assert.NotNull(recovery.Content);
            Assert.IsType<Style>(recovery.FindResource("UpdateDialogPrimaryButtonStyle"));
            Assert.Equal(0, replacementCalls);
            Assert.False(startup.IsLoaded);
            Assert.False(singleInstance.IsLoaded);
            Assert.False(recovery.IsLoaded);
        }
        finally
        {
            recovery?.Close();
            recovery?.Dispose();
            singleInstance?.Close();
            startup?.Close();
        }
    }

    private static void Run(Action<ConfigHandler, ThemeManager> action) => WpfTestHost.Invoke(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), $"ColorVisionStartupTheme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        ResourceDictionary previousResources = Application.Current.Resources;
        Window? previousMainWindow = Application.Current.MainWindow;
        IConfigService? previousConfig = ConfigService.Instance;
        Authorization previousAuthorization = Authorization.Instance;
        ThemeManager previousManager = ThemeManager.Current;
        using var manager = new ThemeManager();
        try
        {
            Application.Current.Resources = new ResourceDictionary();
            ThemeManager.Current = manager;
            var config = new ConfigHandler
            {
                ConfigDIFileName = "ColorVisionConfig",
                ConfigFilePath = Path.Combine(root, "ColorVisionConfig.json"),
                BackupFolderPath = Path.Combine(root, "Backup"),
                IsAutoSave = false,
            };
            Directory.CreateDirectory(config.BackupFolderPath);
            ConfigService.SetInstance(config);
            // Ensure the assemblies owning the four key configuration types participate
            // in discovery without constructing App or invoking its startup initializers.
            AssemblyHandler.GetInstance().RegisterAssembly(typeof(ThemeConfig).Assembly);
            AssemblyHandler.GetInstance().RegisterAssembly(typeof(Authorization).Assembly);
            action(config, manager);
        }
        finally
        {
            Application.Current.MainWindow = previousMainWindow;
            Application.Current.Resources = previousResources;
            ThemeManager.Current = previousManager;
            ConfigService.SetInstance(previousConfig!);
            Authorization.Instance = previousAuthorization;

            string cleanupRoot = Path.GetFullPath(root);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!cleanupRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Startup theme test cleanup must stay inside the temporary directory.");
            Directory.Delete(cleanupRoot, recursive: true);
        }
    });
}
