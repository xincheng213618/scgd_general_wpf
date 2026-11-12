using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Themes;
using ColorVision.UI;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class CopilotLoadedBatchResultsWorkflowTests
{
    [Fact]
    public async Task AutomaticContextHandsOffTheExactPageWhenTwoLoadedPagesHaveTheSameBatchId()
    {
        var fixture = WpfTestHost.Invoke(() => new Pages());
        try
        {
            var provider = WpfTestHost.Invoke(() => Assert.Single(Assert.Single(
                CopilotAgentExtensionRegistry.Shared.GetSnapshot().Extensions.Where(extension =>
                    extension.SourceId == CopilotMeasurementResultAgentExtension.SourceId)).ContextProviders));
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
                LiveContextProvider = () => new CopilotLiveContext { SourceId = "unrelated-selection", Title = "Another view" },
            });
            var tool = new CopilotInspectLoadedBatchResultsTool(dispatcher);
            var request = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Diagnose, UserText = "当前这个批次有哪些结果记录？",
                ConversationId = "loaded-batch-context", TaskId = "loaded-batch-context-task",
            };
            var contextRequest = new CopilotContextRequest { Scope = CopilotContextScope.Diagnose, UserText = request.UserText };
            var otherContext = Assert.IsType<CopilotContextItem>(await provider.CaptureAsync(contextRequest, CancellationToken.None));
            var otherReference = Reference(otherContext);

            var other = await tool.ExecuteAsync(request, Input(otherReference), CancellationToken.None);

            Assert.True(other.Success, other.ErrorMessage);
            using var otherJson = JsonDocument.Parse(other.Content);
            Assert.Equal(42, otherJson.RootElement.GetProperty("batch_id").GetInt32());
            Assert.Single(otherJson.RootElement.GetProperty("results").EnumerateArray());
            WpfTestHost.Invoke(fixture.ActivateTarget);
            var targetContext = Assert.IsType<CopilotContextItem>(await provider.CaptureAsync(contextRequest, CancellationToken.None));
            var targetReference = Reference(targetContext);
            Assert.NotEqual(otherReference["source_ref"].GetString(), targetReference["source_ref"].GetString());
            Assert.Equal(otherReference["expected_source_instance_id"].GetString(), targetReference["expected_source_instance_id"].GetString());

            var target = await dispatcher.CallAsync("inspect_loaded_batch_results", targetReference, CancellationToken.None);

            Assert.True(target.Success, target.Text);
            using var targetJson = JsonDocument.Parse(target.Text);
            Assert.Equal(42, targetJson.RootElement.GetProperty("batch_id").GetInt32());
            Assert.Equal(7, targetJson.RootElement.GetProperty("results").GetArrayLength());
            Assert.Equal(targetReference["source_ref"].GetString(), targetJson.RootElement.GetProperty("source_ref").GetString());
            AssertWithheld(otherContext.Content);
            AssertWithheld(targetContext.Content);
            WpfTestHost.Invoke(fixture.AssertUnselected);

            WpfTestHost.Invoke(fixture.UnloadTarget);
            var staleAttachment = await dispatcher.CallAsync("inspect_loaded_batch_results", targetReference, CancellationToken.None);
            Assert.False(staleAttachment.Success);
            Assert.Equal(CopilotToolFailureKind.NotFound, staleAttachment.FailureKind);
            var currentContext = Assert.IsType<CopilotContextItem>(await provider.CaptureAsync(contextRequest, CancellationToken.None));
            var currentReference = Reference(currentContext);
            Assert.Equal(otherReference["source_ref"].GetString(), currentReference["source_ref"].GetString());
            var current = await tool.ExecuteAsync(request, Input(currentReference), CancellationToken.None);
            Assert.True(current.Success, current.ErrorMessage);
            using var currentJson = JsonDocument.Parse(current.Content);
            Assert.Single(currentJson.RootElement.GetProperty("results").EnumerateArray());
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    [Fact]
    public async Task LoadedRecordsCanBeDiscoveredAcrossPagesAndFocusedWithoutSelectingOrRebindingTheirDatabaseIds()
    {
        var fixture = WpfTestHost.Invoke(() => new Pages());
        try
        {
            var environment = new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
                LiveContextProvider = () => new CopilotLiveContext { SourceId = "unrelated-selection", Title = "Another view" },
            };
            var dispatcher = new CopilotMcpToolDispatcher(environment);
            var tool = new CopilotInspectLoadedBatchResultsTool(dispatcher);
            var request = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Diagnose, UserText = "Find the nonzero and unknown result codes in the loaded batch.",
                ConversationId = "loaded-batch-results", TaskId = "loaded-batch-results-task",
            };
            var sourceSnapshot = await new CopilotLoadedBatchResultsReader().CaptureAsync(CancellationToken.None);
            var source = Assert.Single(sourceSnapshot.Sources, item => item.Results.Count == 7);
            var directory = await tool.ExecuteAsync(request, Input(new { max_items = 100 }), CancellationToken.None);
            Assert.True(directory.Success, directory.ErrorMessage);
            using var directoryJson = JsonDocument.Parse(directory.Content);
            var owner = directoryJson.RootElement.GetProperty("source_instance_id").GetString();
            var sources = directoryJson.RootElement.GetProperty("sources").EnumerateArray().ToArray();
            Assert.Equal(2, sources.Count(item => item.GetProperty("batch_id").GetInt32() == 42));
            Assert.Contains(sources, item => item.GetProperty("source_ref").GetString() == source.SourceRef);
            AssertWithheld(directory.Content);

            var first = await tool.ExecuteAsync(request, Input(new
            {
                source_ref = source.SourceRef, expected_source_instance_id = owner, max_items = 2,
            }), CancellationToken.None);
            Assert.True(first.Success, first.ErrorMessage);
            Assert.NotEmpty(first.PartialResultMessage);
            using var firstJson = JsonDocument.Parse(first.Content);
            var firstCursor = firstJson.RootElement.GetProperty("next_cursor").GetString();
            var cursor = firstCursor;
            var rows = firstJson.RootElement.GetProperty("results").EnumerateArray().Select(row => row.Clone()).ToList();
            while (cursor != null)
            {
                var page = await dispatcher.CallAsync("inspect_loaded_batch_results", Arguments(new
                {
                    source_ref = source.SourceRef, expected_source_instance_id = owner, max_items = 3, cursor,
                }), CancellationToken.None);
                Assert.True(page.Success, page.Text);
                AssertWithheld(page.Text);
                using var json = JsonDocument.Parse(page.Text);
                rows.AddRange(json.RootElement.GetProperty("results").EnumerateArray().Select(row => row.Clone()));
                cursor = json.RootElement.GetProperty("next_cursor").GetString();
            }
            Assert.Equal(7, rows.Count);
            Assert.Equal(7, rows.Select(row => row.GetProperty("result_ref").GetString()).Distinct().Count());
            Assert.All(rows, row => Assert.Equal(17, row.GetProperty("internal_id").GetInt32()));
            var image = Assert.Single(rows, row => row.GetProperty("kind").GetString() == "image"
                && row.GetProperty("result_code").GetInt32() == 9);
            var algorithm = Assert.Single(rows, row => row.GetProperty("kind").GetString() == "algorithm"
                && row.GetProperty("result_code").ValueKind == JsonValueKind.Null);
            Assert.Equal(3_700_001, image.GetProperty("duration_ms").GetInt64());
            Assert.Equal(3_700_001, image.GetProperty("recorded_duration_ms").GetInt64());
            Assert.Equal(-17, algorithm.GetProperty("recorded_duration_ms").GetInt64());
            Assert.Equal(JsonValueKind.Null, algorithm.GetProperty("duration_ms").ValueKind);
            Assert.StartsWith("2026-10-27T01:02:03", algorithm.GetProperty("created_at").GetString());
            Assert.False(algorithm.GetProperty("created_at").GetString()!.EndsWith('Z'));
            Assert.All(rows, row => Assert.False(row.TryGetProperty("passed", out _)));
            WpfTestHost.Invoke(fixture.AssertUnselected);

            var resultRef = image.GetProperty("result_ref").GetString();
            var focusInput = new { source_ref = source.SourceRef, expected_source_instance_id = owner, result_ref = resultRef };
            var focus = await tool.ExecuteAsync(request, Input(focusInput), CancellationToken.None);
            Assert.True(focus.Success, focus.ErrorMessage);
            using (var json = JsonDocument.Parse(focus.Content))
                Assert.Equal(resultRef, Assert.Single(json.RootElement.GetProperty("results").EnumerateArray())
                    .GetProperty("result_ref").GetString());

            WpfTestHost.Invoke(fixture.ReplaceTargetWithAnEqualDatabaseId);
            var oldFocus = await dispatcher.CallAsync("inspect_loaded_batch_results", Arguments(focusInput), CancellationToken.None);
            Assert.False(oldFocus.Success);
            Assert.Equal(CopilotToolFailureKind.NotFound, oldFocus.FailureKind);
            var oldPage = await dispatcher.CallAsync("inspect_loaded_batch_results", Arguments(new
            {
                source_ref = source.SourceRef, expected_source_instance_id = owner, cursor = firstCursor,
            }), CancellationToken.None);
            Assert.False(oldPage.Success);
            Assert.Equal(CopilotToolFailureKind.Validation, oldPage.FailureKind);

            var refreshed = await dispatcher.CallAsync("inspect_loaded_batch_results", Arguments(new
            {
                source_ref = source.SourceRef, expected_source_instance_id = owner, max_items = 100,
            }), CancellationToken.None);
            Assert.True(refreshed.Success, refreshed.Text);
            using var refreshedJson = JsonDocument.Parse(refreshed.Text);
            var replacement = Assert.Single(refreshedJson.RootElement.GetProperty("results").EnumerateArray(), row =>
                row.GetProperty("kind").GetString() == "image" && row.GetProperty("result_code").GetInt32() == 9);
            Assert.NotEqual(resultRef, replacement.GetProperty("result_ref").GetString());
            Assert.Equal(17, replacement.GetProperty("internal_id").GetInt32());
            WpfTestHost.Invoke(fixture.UnloadTarget);
            var unloaded = await dispatcher.CallAsync("inspect_loaded_batch_results", Arguments(new
            {
                source_ref = source.SourceRef, expected_source_instance_id = owner,
                result_ref = replacement.GetProperty("result_ref").GetString(),
            }), CancellationToken.None);
            Assert.False(unloaded.Success);
            Assert.Equal(CopilotToolFailureKind.NotFound, unloaded.FailureKind);
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }
    }

    private static void AssertWithheld(string text)
    {
        Assert.DoesNotContain("withheld-path-sentinel", text, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-message-sentinel", text, StringComparison.Ordinal);
        Assert.DoesNotContain("barcode-sentinel", text, StringComparison.Ordinal);
        Assert.DoesNotContain("parameter-sentinel", text, StringComparison.Ordinal);
    }

    private static Dictionary<string, JsonElement> Arguments(object value)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(value))!;
    private static CopilotAgentToolInput Input(object value)
        => new() { Arguments = Arguments(value).ToDictionary(pair => pair.Key, pair => (object?)pair.Value) };

    private static Dictionary<string, JsonElement> Reference(CopilotContextItem context)
    {
        const string prefix = "Loaded batch result source: ";
        var line = Assert.Single(context.Content.Split(Environment.NewLine), value => value.StartsWith(prefix, StringComparison.Ordinal));
        var reference = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line[prefix.Length..])!;
        Assert.Equal(2, reference.Count);
        Assert.Equal(32, reference["source_ref"].GetString()!.Length);
        Assert.Equal(32, reference["expected_source_instance_id"].GetString()!.Length);
        return reference;
    }

    private sealed class Pages : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly CopilotLiveContext? _previousContext = CopilotLiveContextRegistry.Current;
        private readonly MeasureBatchPage _target;
        private readonly MeasureBatchPage _other;

        internal Pages()
        {
            foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
            _target = CreatePage();
            _other = CreatePage();
            for (var index = 0; index < 5; index++) _target.ViewResultImages.Add(Image(index == 4 ? 9 : 0));
            _target.ViewResultAlgs.Add(new ViewResultAlg
            {
                Id = 17, ResultCode = null, TotalTime = -17, POITemplateName = "Shared template",
                CreateTime = new DateTime(2026, 10, 27, 1, 2, 3, DateTimeKind.Unspecified),
                FilePath = "withheld-path-sentinel", ResultDesc = "raw-message-sentinel",
            });
            _target.ViewResultAlgs.Add(new ViewResultAlg { Id = 17, ResultCode = 0, POITemplateName = "Shared template" });
            _other.ViewResultImages.Add(Image(9));
            Register(_target);
            Register(_other);
            AssertUnselected();
        }

        private static MeasureBatchPage CreatePage()
        {
            var page = new MeasureBatchPage(new Frame(), new MeasureBatchModel { Id = 42, Code = "barcode-sentinel" });
            var load = typeof(MeasureBatchPage).GetMethod("Page_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
            page.Loaded -= (RoutedEventHandler)load.CreateDelegate(typeof(RoutedEventHandler), page);
            return page;
        }

        private static void Register(MeasureBatchPage page)
            => typeof(MeasureBatchPage).GetMethod("EnsureCopilotContextRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(page, null);

        private static ViewResultImage Image(int code)
        {
            var image = new ViewResultImage
            {
                Id = 17, ResultCode = code, FilePath = "withheld-path-sentinel", FileUrl = "withheld-path-sentinel",
                ResultDesc = "raw-message-sentinel", ResultMsg = "raw-message-sentinel", ReqParams = "parameter-sentinel",
            };
            typeof(ViewResultImage).GetField("_totalTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(image, 3_700_001L);
            return image;
        }

        internal void AssertUnselected()
        {
            foreach (var page in new[] { _target, _other })
            {
                Assert.Null(((ListView)page.FindName("listView1")).SelectedItem);
                Assert.Null(((ListView)page.FindName("listView2")).SelectedItem);
            }
        }

        internal void ReplaceTargetWithAnEqualDatabaseId() { AssertUnselected(); _target.ViewResultImages[4] = Image(9); AssertUnselected(); }
        internal void ActivateTarget() => typeof(MeasureBatchPage).GetMethod("CopilotHostWindow_Activated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(_target, [null, EventArgs.Empty]);
        internal void UnloadTarget() => _target.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        public void Dispose()
        {
            _target.Dispose();
            _other.Dispose();
            if (_previousContext != null) CopilotLiveContextRegistry.Publish(_previousContext);
            else CopilotLiveContextRegistry.Clear(CopilotMeasurementResultAgentExtension.SourceId);
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
