using ColorVision.UI;
using ColorVision.UI.Desktop.MenuItemManager;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class MenuCustomizationPersistenceTests
{
    [Fact]
    public void FailedApplyKeepsTheSavedMenuAndCanBeRetried()
    {
        WpfTestHost.Invoke(() =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-MenuSave-");
            string path = Path.Combine(directory.FullName, "config.json");
            var handler = ConfigHandler.GetInstance();
            var previousService = ConfigService.Instance;
            string previousPath = handler.ConfigFilePath;
            var config = handler.GetRequiredService<MenuItemManagerConfig>();
            var previousOverrides = config.Overrides;
            var previousLegacySettings = config.Settings;
            var key = new MenuItemScopeKey("MenuPersistenceTest", "test.menu-item");
            try
            {
                TestEnvironment.SetConfig(handler);
                handler.ConfigFilePath = path;
                config.Settings = null;
                config.Overrides = [new() { TargetName = key.TargetName, GuidId = key.GuidId, OrderOverride = 20 }];
                Assert.True(handler.TrySave(config, out string error), error);
                MenuItemManagerService.RebuildMenu();
                byte[] before = File.ReadAllBytes(path);
                var draft = new MenuItemSetting { TargetName = key.TargetName, GuidId = key.GuidId, IsVisible = false };
                var menu = MenuManager.GetInstance();

                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<IOException>(() => MenuItemManagerService.CommitEditingSnapshot([draft]));
                    Assert.Equal(20, menu.ScopedOrderOverrides[key]);
                    Assert.DoesNotContain(key, menu.ScopedFilteredItems);
                    Assert.Equal(20, Assert.Single(config.Overrides).OrderOverride);
                }

                Assert.Equal(before, File.ReadAllBytes(path));
                Assert.True(MenuItemManagerService.CommitEditingSnapshot([draft]));
                Assert.Contains(key, menu.ScopedFilteredItems);
                Assert.DoesNotContain(key, menu.ScopedOrderOverrides.Keys);
                var reopened = new ConfigHandler { ConfigFilePath = path };
                reopened.LoadConfigs();
                Assert.False(Assert.Single(reopened.GetRequiredService<MenuItemManagerConfig>().Overrides).IsVisible);
            }
            finally
            {
                handler.ConfigFilePath = previousPath;
                config.Overrides = previousOverrides;
                config.Settings = previousLegacySettings;
                try { MenuItemManagerService.RebuildMenu(); }
                finally
                {
                    ConfigService.SetInstance(previousService);
                    foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                    directory.Delete();
                }
            }
        });
    }
}
