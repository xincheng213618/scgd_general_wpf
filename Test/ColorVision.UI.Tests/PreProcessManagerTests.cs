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

    [Fact]
    public async Task EnabledUnreadableActionBlocksEveryPreprocessorAndPreservesItsRawConfiguration()
    {
        using var config = new IsolatedConfig();
        const string brokenJson = """{"Marker":7,"Broken":@}""";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(config.Path)!);
        File.WriteAllText(config.Path, JsonConvert.SerializeObject(new[]
        {
            new { ProcessTypeFullName = typeof(CountingPreProcessor).FullName, ConfigJson = "{}", ActionName = "Healthy first", IsEnabled = true },
            new { ProcessTypeFullName = typeof(PartiallyConfiguredPreProcessor).FullName, ConfigJson = brokenJson, ActionName = "Unreadable action", IsEnabled = true }
        }));

        var manager = WpfTestHost.Invoke(() =>
        {
            AssemblyHandler.GetInstance().RegisterAssembly(typeof(PartiallyConfiguredPreProcessor).Assembly);
            return config.Load();
        });
        var healthy = Assert.IsType<CountingPreProcessor>(manager.Processes[0].Process);
        var partial = Assert.IsType<PartiallyConfiguredPreProcessor>(manager.Processes[1].Process);
        Assert.Equal(7, partial.Marker);

        Assert.False(await WpfTestHost.Invoke(() => manager.ExecuteAsync("Flow", "SN")));
        Assert.Equal(0, healthy.Executions);
        Assert.Equal(0, partial.Executions);

        WpfTestHost.Invoke(() =>
        {
            manager.Processes[1].ActionName = "Renamed unreadable action";
            var saved = JArray.Parse(File.ReadAllText(config.Path));
            Assert.Equal(brokenJson, saved[1]!["ConfigJson"]!.Value<string>());
            Assert.True(saved[1]!["IsEnabled"]!.Value<bool>());

            manager.Processes[1].IsEnabled = false;
            Assert.Same(healthy, Assert.Single(manager.GetEnabledProcesses("Flow")));
        });
        Assert.True(await WpfTestHost.Invoke(() => manager.ExecuteAsync("Flow", "SN")));
        Assert.Equal(1, healthy.Executions);
        Assert.Equal(0, partial.Executions);

        var restored = WpfTestHost.Invoke(() => config.Load());
        Assert.False(restored.Processes[1].IsEnabled);
        Assert.Equal(brokenJson, JArray.Parse(File.ReadAllText(config.Path))[1]!["ConfigJson"]!.Value<string>());
    }

    [Fact]
    public void MalformedCacheCleanupConfigurationDoesNotExposeTheDefaultProcessorForExecution()
    {
        WpfTestHost.Invoke(() =>
        {
            using var config = new IsolatedConfig();
            const string brokenJson = """{"FolderPaths":"not-an-array"}""";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(config.Path)!);
            File.WriteAllText(config.Path, JsonConvert.SerializeObject(new[]
            {
                new { ProcessTypeFullName = typeof(FolderSizePreProcessor).FullName, ConfigJson = brokenJson, ActionName = "Configured cleanup", IsEnabled = true }
            }));

            var manager = config.Load();
            Assert.True(Assert.Single(manager.Processes).IsEnabled);
            var failure = Assert.Throws<InvalidOperationException>(() => manager.GetEnabledProcesses("Flow"));
            Assert.Contains("Configured cleanup", failure.Message);
            manager.SavePersisted();
            Assert.Equal(brokenJson, JArray.Parse(File.ReadAllText(config.Path))[0]!["ConfigJson"]!.Value<string>());
        });
    }

    [PreProcess("Counting test action")]
    public class CountingPreProcessor : IPreProcessor
    {
        public int Executions { get; private set; }

        public Task<bool> PreProcess(PreProcessContext ctx)
        {
            Executions++;
            return Task.FromResult(true);
        }
    }

    [PreProcess("Partially configured test action")]
    public sealed class PartiallyConfiguredPreProcessor : CountingPreProcessor, IPreProcessor
    {
        public int Marker { get; private set; }

        public object GetConfig() => new { Marker };

        public void SetConfig(string configJson)
        {
            using var reader = new JsonTextReader(new StringReader(configJson));
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName && Equals(reader.Value, nameof(Marker)))
                    Marker = reader.ReadAsInt32() ?? 0;
            }
        }
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
