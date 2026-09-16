using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.UI.Desktop.Feedback;
using ColorVision.UI.LogImp;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FeedbackConfigurationSnapshotTests
{
    [Fact]
    public void JsonStringsAndArraysAreRedactedWithoutChangingSourceOrTimingSettings()
    {
        JArray source = JArray.Parse("""
            [{
              "$type": "Example.Process, Missing.Assembly",
              "IsEnabled": true,
              "ConfigJson": "{\"ApiKey\":\"embedded-secret\",\"ExposureTimeMs\":50,\"SuccessDelayMs\":500,\"Nested\":[{\"Password\":\"nested-secret\"}],\"RecipeJson\":\"{\\\"Token\\\":\\\"deep-secret\\\",\\\"Lv\\\":123.4}\"}",
              "Caption": "{placeholder}",
              "Text": "2026-09-16T12:00:00+08:00"
            }]
            """);
        string original = source.ToString();
        var snapshot = (JArray)FeedbackConfigurationSnapshot.CreateRedactedSnapshot(source);
        JObject config = JObject.Parse(snapshot[0].Value<string>("ConfigJson")!);
        Assert.Equal("[REDACTED]", config.Value<string>("ApiKey"));
        Assert.Equal("[REDACTED]", config["Nested"]![0]!.Value<string>("Password"));
        JObject recipe = JObject.Parse(config.Value<string>("RecipeJson")!);
        Assert.Equal("[REDACTED]", recipe.Value<string>("Token"));
        Assert.Equal(123.4, recipe.Value<double>("Lv"));
        Assert.Equal(50, config.Value<int>("ExposureTimeMs"));
        Assert.Equal(500, config.Value<int>("SuccessDelayMs"));
        Assert.Equal("{placeholder}", snapshot[0].Value<string>("Caption"));
        Assert.Equal("Example.Process, Missing.Assembly", snapshot[0].Value<string>("$type"));
        Assert.Equal(original, source.ToString());
        Assert.DoesNotContain("-secret", snapshot.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionIncludesOldCurrentConfigsButExcludesUnlistedFilesAndRawInvalidJson()
    {
        string directory = Directory.CreateTempSubdirectory("ColorVision-FeedbackConfig-").FullName;
        IReadOnlyList<(string EntryPath, string FilePath)> files = [];
        try
        {
            string pre = Path.Combine(directory, "PreProcessConfig.json");
            string post = Path.Combine(directory, "PostProcessConfig.json");
            string malformed = Path.Combine(directory, "BrokenConfig.json");
            const string text = "[{\"IsEnabled\":true,\"ConfigJson\":\"{\\\"Password\\\":\\\"do-not-include\\\",\\\"Delay\\\":100}\"}]";
            File.WriteAllText(pre, text);
            File.SetLastWriteTimeUtc(pre, DateTime.UtcNow.AddYears(-1));
            File.WriteAllText(post, "{\"ConfigJson\":\"{\\\"Token\\\":\\\"broken-secret\",\"Date\":\"2026-09-16T12:00:00+08:00\"}");
            File.WriteAllText(malformed, "{\"Password\":raw-secret-invalid}");
            File.WriteAllText(Path.Combine(directory, "kb.auth.json"), "{\"Password\":\"unlisted-secret\"}");
            files = FeedbackConfigurationSnapshot.CollectFiles(
            [
                ("Config/PreProcessConfig.json", pre),
                ("Config/PostProcessConfig.json", post),
                ("Config/BrokenConfig.json", malformed),
                ("Config/Missing.json", Path.Combine(directory, "Missing.json")),
            ]);
            Assert.Equal(3, files.Count);
            string preText = File.ReadAllText(files.Single(file => file.EntryPath == "Config/PreProcessConfig.json").FilePath);
            Assert.True(JArray.Parse(preText)[0].Value<bool>("IsEnabled"));
            Assert.DoesNotContain("do-not-include", preText, StringComparison.Ordinal);
            string postText = File.ReadAllText(files.Single(file => file.EntryPath == "Config/PostProcessConfig.json").FilePath);
            JObject postJson = JObject.Parse(postText);
            Assert.Equal("[REDACTED]", postJson.Value<string>("ConfigJson"));
            Assert.Contains("2026-09-16T12:00:00+08:00", postText, StringComparison.Ordinal);
            string error = File.ReadAllText(files.Single(file => file.EntryPath == "Config/BrokenConfig.json.collection-error.txt").FilePath);
            Assert.Contains("JsonReaderException", error, StringComparison.Ordinal);
            Assert.DoesNotContain("raw-secret", error, StringComparison.Ordinal);
            Assert.Equal(text, File.ReadAllText(pre));
        }
        finally
        {
            foreach (var file in files) File.Delete(file.FilePath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ConfigurationCollectorIsSelectedWithoutATimeFilterOrCleanupAction()
    {
        IFeedbackLogCollector collector = new FlowProcessingConfigurationFeedbackCollector();
        var item = new CollectorItem(collector);
        Assert.True(item.IsChecked);
        Assert.False(collector is IFeedbackLogTimeRangeCollector);
        Assert.False(collector is IFeedbackDiagnosticCleanupSource);
    }
}
