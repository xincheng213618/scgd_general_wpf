using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class PreProcessManagerTests
{
    [Fact]
    public void FreshConfigurationPersistsOneEnabledCacheCleanup()
    {
        WpfTestHost.Invoke(() =>
        {
            using var config = new IsolatedConfig();
            var manager = config.Load();
            var action = Assert.Single(manager.Processes);
            var cleanup = Assert.IsType<FolderSizePreProcessor>(action.Process);

            Assert.True(action.IsEnabled);
            Assert.Same(action, manager.SelectedProcess);
            Assert.Equal(100L * 1024 * 1024 * 1024, cleanup.Config.TriggerSizeBytes);
            Assert.Equal(50L * 1024 * 1024 * 1024, cleanup.Config.TargetSizeBytes);

            var restored = config.Load();
            Assert.True(Assert.Single(restored.Processes).IsEnabled);
            Assert.IsType<FolderSizePreProcessor>(Assert.Single(restored.GetEnabledProcesses("Any flow")));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavedDisabledOrRemovedCleanupIsNotReenabled(bool remove)
    {
        WpfTestHost.Invoke(() =>
        {
            using var config = new IsolatedConfig();
            var manager = config.Load();
            var action = Assert.Single(manager.Processes);
            if (remove)
            {
                manager.Processes.Remove(action);
            }
            else
            {
                var cleanup = Assert.IsType<FolderSizePreProcessor>(action.Process);
                cleanup.Config.FolderPaths = [@"C:\custom-cache"];
                cleanup.Config.TriggerSizeBytes = 20L * 1024 * 1024 * 1024;
                action.IsEnabled = false;
            }

            var restored = config.Load();
            Assert.Empty(restored.GetEnabledProcesses("Any flow"));
            if (remove)
            {
                Assert.Empty(restored.Processes);
            }
            else
            {
                var cleanup = Assert.IsType<FolderSizePreProcessor>(Assert.Single(restored.Processes).Process);
                Assert.Equal([@"C:\custom-cache"], cleanup.Config.FolderPaths);
                Assert.Equal(20L * 1024 * 1024 * 1024, cleanup.Config.TriggerSizeBytes);
            }
        });
    }

    [Fact]
    public void LegacyTemplateRestrictionsAreIgnoredWhileEnabledOrderIsPreserved()
    {
        WpfTestHost.Invoke(() =>
        {
            using var config = new IsolatedConfig();
            Directory.CreateDirectory(Path.GetDirectoryName(config.Path)!);
            File.WriteAllText(config.Path, JsonConvert.SerializeObject(new[]
            {
                new { ProcessTypeFullName = typeof(ScriptPreProcessor).FullName, ConfigJson = "{}", ActionName = "First", IsEnabled = true, TemplateNames = "Old flow" },
                new { ProcessTypeFullName = typeof(FolderSizePreProcessor).FullName, ConfigJson = "{}", ActionName = "Disabled", IsEnabled = false, TemplateNames = "" },
                new { ProcessTypeFullName = typeof(FolderSizePreProcessor).FullName, ConfigJson = "{}", ActionName = "Last", IsEnabled = true, TemplateNames = "Other flow" }
            }));

            var manager = config.Load();
            var enabled = manager.GetEnabledProcesses("Unrelated flow");
            Assert.Equal(2, enabled.Count);
            Assert.Same(manager.Processes[0].Process, enabled[0]);
            Assert.Same(manager.Processes[2].Process, enabled[1]);

            manager.SavePersisted();
            var saved = JArray.Parse(File.ReadAllText(config.Path));
            Assert.All(saved.Cast<JObject>(), item => Assert.Null(item.Property("TemplateNames")));
            Assert.Equal(2, config.Load().GetEnabledProcesses("Another flow").Count);
        });
    }

    private sealed class IsolatedConfig : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ColorVision_PreProcess_{Guid.NewGuid():N}", "PreProcessConfig.json");

        public PreProcessManager Load()
        {
            AssemblyHandler.GetInstance().RegisterAssembly(typeof(PreProcessManager).Assembly);
            return new PreProcessManager(Path);
        }

        public void Dispose()
        {
            if (File.Exists(Path)) File.Delete(Path);
            string directory = System.IO.Path.GetDirectoryName(Path)!;
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
}
