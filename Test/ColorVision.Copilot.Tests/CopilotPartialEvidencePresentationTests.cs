using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotPartialEvidencePresentationTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotPartialEvidence-");

    [Theory]
    [InlineData("unreadable-search")]
    [InlineData("search-page")]
    [InlineData("file-search-page")]
    [InlineData("directory-page")]
    [InlineData("truncated-read")]
    [InlineData("mixed-read")]
    public async Task RealPartialResultsRemainVisibleInCollapsedAndExpandedActivity(string kind)
    {
        var result = await CreatePartialResult(kind);
        Assert.True(result.Success, result.ErrorMessage);
        var entry = CreateEntry(result);

        Assert.Contains("结果不完整", entry.ActivityLabel);
        Assert.NotEqual("✓", entry.ActivityGlyph);
        Assert.NotEmpty(entry.ActivityDescription);
        Assert.Equal(result.PartialResultMessage, entry.ActivityDescription);
        Assert.DoesNotContain("已获得文件搜索结果。", entry.ActivityDescription);
        Assert.False(entry.IsFailure);
        var group = Assert.Single(CopilotAgentTraceGroup.Create([entry, CreateEntry(new()
        {
            ToolName = result.ToolName, Success = true, Summary = "Completed.",
        })]));
        Assert.Contains("结果不完整", group.ActivityLabel);
        Assert.NotEqual("✓", group.ActivityGlyph);
        Assert.Contains("1 次结果不完整", group.ActivityDescription);
    }

    [Fact]
    public async Task PartialCoverageSurvivesSnapshotsAndConversationRecovery()
    {
        var result = await CreatePartialResult("mixed-read");
        var captured = CopilotToolResultContract.Capture(result.ToolName, result);
        var observation = CopilotToolObservation.FromResult(captured);
        var run = new CopilotAgentRunResult
        {
            StepRecords = [new() { Observation = observation, ModelObservation = observation }],
        };
        Assert.Equal(result.PartialResultMessage, Assert.Single(run.StepRecords).Observation.PartialResultMessage);
        Assert.Equal(result.PartialResultMessage, run.StepRecords[0].ModelObservation!.PartialResultMessage);
        var entry = CreateEntry(captured);
        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(JsonConvert.SerializeObject(entry))!;
        restored.EnsureValid(DateTimeOffset.UtcNow);
        Assert.Equal(entry.PartialResultMessage, restored.PartialResultMessage);
        Assert.Equal(entry.ActivityLabel, restored.ActivityLabel);
        Assert.Contains(entry.PartialResultMessage, restored.DiagnosticDetails);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureOrNullAdvisoriesViolateTheResultContract(bool nullAdvisory)
    {
        var captured = CopilotToolResultContract.Capture("ReadLocalFile", new()
        {
            ToolName = "ReadLocalFile", Success = nullAdvisory,
            FailureKind = nullAdvisory ? CopilotToolFailureKind.None : CopilotToolFailureKind.Validation,
            PartialResultMessage = nullAdvisory ? null! : "Some file content remains unread.",
        });
        Assert.False(captured.Success);
        Assert.Equal(CopilotToolResultContract.InvalidOutputFailureCode, captured.FailureCode);
        Assert.Empty(captured.PartialResultMessage);
    }

    [Fact]
    public void AdvisoryIsRedactedAndBoundedOnRecoveryAndDoesNotOverrideFailure()
    {
        var secret = Guid.NewGuid().ToString("N");
        var captured = CopilotToolResultContract.Capture("ReadLocalFile", new()
        {
            ToolName = "ReadLocalFile", Success = true,
            PartialResultMessage = $"api_key={secret} " + new string('x', 1200),
        });
        Assert.DoesNotContain(secret, captured.PartialResultMessage);
        var restored = new CopilotAgentTraceEntry
        {
            ToolName = "ReadLocalFile", State = CopilotToolExecutionState.Completed,
            PartialResultMessage = $"api_key={secret} " + new string('x', 1200),
        };
        Assert.True(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.DoesNotContain(secret, restored.PartialResultMessage);
        // Trace summaries retain an 800-character prefix followed by the existing ellipsis marker.
        Assert.Equal(803, restored.PartialResultMessage.Length);
        Assert.EndsWith("...", restored.PartialResultMessage);
        restored.State = CopilotToolExecutionState.Failed;
        restored.EnsureValid(DateTimeOffset.UtcNow);
        Assert.False(restored.HasPartialResult);
        Assert.Empty(restored.PartialResultMessage);
        Assert.Contains("失败", restored.ActivityLabel);
    }

    [Fact]
    public void CompleteEmptySearchAndLegacyTraceDoNotAcquirePartialWarnings()
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "capture.log"), "No matching result");
        var result = CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None)
            .ToCapabilityResult().ToToolResult("GrepText");
        Assert.True(result.Success);
        Assert.Empty(result.PartialResultMessage);
        Assert.Equal("✓", CreateEntry(result).ActivityGlyph);
        var legacy = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>("{\"SchemaVersion\":18,\"ToolName\":\"ReadLocalFile\",\"State\":\"Completed\"}")!;
        legacy.EnsureValid(DateTimeOffset.UtcNow);
        Assert.False(legacy.HasPartialResult);
        Assert.DoesNotContain("PartialResultMessage", JsonConvert.SerializeObject(legacy));
    }

    [Fact]
    public async Task AttachmentBatchKeepsOmittedFilesAndReadScopesVisible()
    {
        var attachments = Enumerable.Range(0, 4).Select(index =>
        {
            var path = Path.Combine(_directory.FullName, $"attachment-{index}.log");
            File.WriteAllText(path, new string('x', 21_000));
            return new CopilotAttachmentItem { Type = CopilotAttachmentType.File, Value = path };
        }).ToArray();
        var tool = new CopilotReadAttachedFileTool();
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Auto, Attachments = attachments };
        var result = CopilotToolResultContract.Capture(tool.Name, await tool.ExecuteAsync(request, new(), CancellationToken.None));
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(3, result.LocalFileReadScopes.Count);
        Assert.All(result.LocalFileReadScopes, scope => Assert.True(scope.WasTruncated));
        Assert.Contains("1 个附件未读取", CreateEntry(result).ActivityDescription);
        var selected = await tool.ExecuteAsync(request, new() { Path = attachments[3].Value, StartLine = 1, StartColumn = 20_001 }, CancellationToken.None);
        Assert.True(selected.Success);
        Assert.Empty(selected.PartialResultMessage);
        Assert.Single(selected.LocalFileReadScopes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachmentFailuresRetainTheirCauseInsteadOfInvalidOutput(bool empty)
    {
        var path = Path.Combine(_directory.FullName, "binary.log");
        File.WriteAllText(path, "bad\0data");
        var tool = new CopilotReadAttachedFileTool();
        var result = CopilotToolResultContract.Capture(tool.Name, await tool.ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto,
            Attachments = empty ? [] : [new() { Type = CopilotAttachmentType.File, Value = path }],
        }, new(), CancellationToken.None));
        Assert.False(result.Success);
        Assert.NotEqual(CopilotToolResultContract.InvalidOutputFailureCode, result.FailureCode);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Empty(result.PartialResultMessage);
    }

    [Theory]
    [InlineData("mixed-fetch")]
    [InlineData("mixed-search")]
    [InlineData("all-failed-fetch")]
    [InlineData("omitted-fetch")]
    [InlineData("unavailable-deep-read")]
    public async Task WebPartialEvidenceSurvivesResultCaptureAndActivityProjection(string kind)
    {
        const string pageUrl = "https://public.example/page";
        const string failedUrl = "https://public.example/unavailable.json";
        const string omittedUrl = "https://public.example/omitted";
        var loadedUrls = new ConcurrentQueue<string>();
        Task<CopilotFetchedWebPageContent> LoadPage(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            loadedUrls.Enqueue(url);
            if (url == failedUrl || kind is "all-failed-fetch" or "unavailable-deep-read")
                throw new IOException("Controlled web fetch failure.");
            return Task.FromResult(new CopilotFetchedWebPageContent(url, "Captured page", string.Empty,
                "Captured web evidence for " + url,
                kind == "mixed-search" && url == pageUrl ? [failedUrl] : []));
        }

        var fetch = new CopilotFetchUrlTool(LoadPage);
        var isSearch = kind is "mixed-search" or "unavailable-deep-read";
        ICopilotTool tool = isSearch ? new CopilotWebSearchTool(
            (searchQuery, _) => Task.FromResult(new CopilotWebSearchResult
            {
                Success = true, Query = searchQuery, Provider = "controlled-search", Summary = "Found a search lead.",
                Content = "Search leads for " + pageUrl,
                Hits = [new() { Rank = 1, Title = "Selected page", Url = pageUrl, Snippet = "Search lead." }],
            }),
            (request, url, token) => fetch.ExecuteAsync(request, new() { Query = url }, token)) : fetch;
        var query = kind switch
        {
            "mixed-fetch" => pageUrl + " " + failedUrl,
            "omitted-fetch" => pageUrl + " https://public.example/second https://public.example/third " + omittedUrl,
            _ => pageUrl,
        };
        var rawResult = await tool.ExecuteAsync(new() { Mode = CopilotAgentMode.Web, UserText = query },
            new() { Query = query }, CancellationToken.None);
        var captured = CopilotToolResultContract.Capture(tool.Name, rawResult);
        Assert.NotEqual(CopilotToolResultContract.InvalidOutputFailureCode, captured.FailureCode);
        Assert.Equal(rawResult.Content, captured.Content);
        var observation = CopilotToolObservation.FromResult(captured);
        Assert.Equal(captured.Content, observation.Content);
        Assert.Equal(captured.PartialResultMessage, observation.PartialResultMessage);
        var expectedSources = kind switch
        {
            "all-failed-fetch" => Array.Empty<string>(),
            "omitted-fetch" => [pageUrl, "https://public.example/second", "https://public.example/third"],
            "mixed-search" => [pageUrl, pageUrl],
            _ => new[] { pageUrl },
        };
        Assert.NotNull(captured.WebEvidenceSourceUrls);
        Assert.Equal(expectedSources, captured.WebEvidenceSourceUrls);
        Assert.Equal(expectedSources, observation.WebEvidenceSourceUrls);
        var entry = CopilotAgentTraceEntry.FromResult(new()
        {
            ToolName = tool.Name, CallId = "web-partial-evidence", Access = CopilotToolAccess.ReadOnly,
            State = captured.Success ? CopilotToolExecutionState.Completed : CopilotToolExecutionState.Failed,
        }, captured);
        if (kind == "all-failed-fetch")
        {
            Assert.False(captured.Success);
            Assert.Equal(rawResult.FailureKind, captured.FailureKind);
            Assert.Contains("Controlled web fetch failure.", captured.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(captured.PartialResultMessage);
            Assert.False(observation.Success);
            Assert.True(entry.IsFailure);
            Assert.Contains("[Web Page Fetch Failed] " + pageUrl, captured.Content, StringComparison.Ordinal);
            Assert.Equal(new[] { pageUrl }, loadedUrls);
            return;
        }

        Assert.True(captured.Success, captured.ErrorMessage);
        Assert.Empty(captured.ErrorMessage);
        Assert.NotEmpty(captured.PartialResultMessage);
        Assert.True(observation.Success);
        Assert.True(entry.HasPartialResult);
        Assert.True(entry.IsVisibleInActivity);
        Assert.False(entry.IsFailure);
        Assert.Equal(captured.PartialResultMessage, entry.PartialResultMessage);
        Assert.Contains("结果不完整", entry.ActivityLabel);
        Assert.NotEmpty(entry.ActivityDescription);
        if (isSearch)
            Assert.Contains("Search leads for " + pageUrl, captured.Content, StringComparison.Ordinal);
        if (kind == "unavailable-deep-read")
        {
            Assert.Contains("[Selected Search Result Deep Read Unavailable] " + pageUrl, captured.Content, StringComparison.Ordinal);
            Assert.Equal(new[] { pageUrl }, loadedUrls);
        }
        else
        {
            Assert.Contains("Captured web evidence for " + pageUrl, captured.Content, StringComparison.Ordinal);
            if (kind == "omitted-fetch")
            {
                Assert.Contains("input_set_complete: false", captured.Content, StringComparison.Ordinal);
                Assert.Contains("omitted_input_url: " + omittedUrl, captured.Content, StringComparison.Ordinal);
                Assert.Equal(new[] { pageUrl, "https://public.example/second", "https://public.example/third" }, loadedUrls);
            }
            else
            {
                Assert.Contains("[Web Page Fetch Failed] " + failedUrl, captured.Content, StringComparison.Ordinal);
                Assert.Contains("all_attempts_succeeded: false", captured.Content, StringComparison.Ordinal);
                Assert.Equal(new[] { pageUrl, failedUrl }, loadedUrls);
            }
        }
    }

    [Theory]
    [InlineData(false, false, 220)]
    [InlineData(true, false, 220)]
    [InlineData(false, false, 420)]
    [InlineData(true, false, 420)]
    [InlineData(true, true, 220)]
    public async Task RealActivityTemplatesShowCoverageAndRespectNarrowWidths(bool grouped, bool includeFailure, double width)
    {
        var partial = CreateEntry(await CreatePartialResult("mixed-read"));
        StaTest.Run(() =>
        {
            try
            {
                var panel = new CopilotChatPanel();
                var other = CreateEntry(new() { ToolName = "ReadLocalFile", Success = true });
                if (includeFailure)
                {
                    other.State = CopilotToolExecutionState.Failed;
                    other.FailureKind = CopilotToolFailureKind.Validation;
                    other.ErrorMessage = "所选行范围超出文件内容。";
                }
                var content = grouped ? (object)Assert.Single(CopilotAgentTraceGroup.Create([partial, other])) : partial;
                var presenter = new ContentPresenter
                {
                    Content = content,
                    ContentTemplate = (DataTemplate)panel.Resources[grouped ? "AgentTraceGroupTemplate" : "AgentTraceEntryTemplate"],
                };
                var host = new Border { Width = width, Padding = new Thickness(10), Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Child = presenter };
                host.Resources.MergedDictionaries.Add(panel.Resources);
                host.Resources["GlobalTextBrush"] = Brushes.WhiteSmoke;
                host.Resources["GlobalBorderBrush1"] = Brushes.DimGray;
                Layout(host, width);
                var expander = Assert.Single(Visuals<Expander>(presenter));
                var header = Assert.IsType<Grid>(expander.Header);
                var glyph = header.Children.OfType<TextBlock>().First();
                var label = header.Children.OfType<TextBlock>().ElementAt(1);
                var expectedBrush = (SolidColorBrush)panel.Resources[includeFailure ? "CopilotControlDangerBrush" : "CopilotControlWarningBrush"];
                Assert.Equal(expectedBrush.Color, Assert.IsType<SolidColorBrush>(glyph.Foreground).Color);
                Assert.Equal(expectedBrush.Color, Assert.IsType<SolidColorBrush>(label.Foreground).Color);
                Assert.Contains(includeFailure ? "部分失败" : "结果不完整", label.Text);
                Assert.InRange(label.ActualWidth, 1, width - 20);
                Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
                foreach (var text in header.Children.OfType<TextBlock>())
                {
                    var bounds = text.TransformToAncestor(host).TransformBounds(new Rect(text.RenderSize));
                    Assert.InRange(bounds.Right, 0, width - 10);
                }
                expander.IsExpanded = true;
                Layout(host, width);
                if (grouped)
                {
                    foreach (var child in Visuals<Expander>(presenter).Skip(1))
                        child.IsExpanded = true;
                    Layout(host, width);
                }
                // Offscreen visuals have no PresentationSource, so IsVisible is always false.
                var description = Assert.Single(Visuals<TextBlock>(presenter), text => text.Text == partial.ActivityDescription && text.ActualHeight > 0);
                Assert.Equal(Visibility.Visible, description.Visibility);
                var descriptionBounds = description.TransformToAncestor(host).TransformBounds(new Rect(description.RenderSize));
                Assert.InRange(descriptionBounds.Bottom, 1, host.ActualHeight);
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(host.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var evidenceDirectory = Environment.GetEnvironmentVariable("COLORVISION_COPILOT_UI_EVIDENCE_DIRECTORY");
                if (!string.IsNullOrWhiteSpace(evidenceDirectory))
                {
                    Directory.CreateDirectory(evidenceDirectory);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(evidenceDirectory, $"activity-{(grouped ? "group" : "entry")}-{(includeFailure ? "failure" : "partial")}-{width:0}.png"));
                    encoder.Save(stream);
                }
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private static void Layout(FrameworkElement element, double width)
    {
        element.Measure(new Size(width, double.PositiveInfinity));
        element.Arrange(new Rect(new Point(), element.DesiredSize));
        element.UpdateLayout();
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;
            foreach (var descendant in Visuals<T>(child))
                yield return descendant;
        }
    }

    private async Task<CopilotToolResult> CreatePartialResult(string kind)
    {
        var path = Path.Combine(_directory.FullName, "capture.log");
        if (kind == "unreadable-search")
        {
            File.WriteAllText(path, "SUMMARY\0binary");
            return CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None).ToCapabilityResult().ToToolResult("GrepText");
        }
        if (kind == "search-page")
        {
            File.WriteAllLines(path, Enumerable.Repeat("SUMMARY result=OK", 45));
            return CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None).ToCapabilityResult().ToToolResult("GrepText");
        }
        if (kind is "directory-page" or "file-search-page")
        {
            for (var index = 0; index < 65; index++)
                File.WriteAllText(Path.Combine(_directory.FullName, $"record-{index:D2}.log"), "record");
            return kind == "directory-page"
                ? CopilotListDirectoryCapability.List([_directory.FullName], null, CancellationToken.None).ToToolResult("ListDirectory")
                : CopilotSearchFilesCapability.Search([_directory.FullName], "record-", null, true, CancellationToken.None).ToCapabilityResult().ToToolResult("SearchFiles");
        }
        File.WriteAllText(path, kind == "truncated-read" ? new string('x', 21_000) : "SUMMARY result=OK");
        var files = new List<string> { path };
        if (kind == "mixed-read")
        {
            var bad = Path.Combine(_directory.FullName, "damaged.log");
            File.WriteAllText(bad, "bad\0data");
            files.Add(bad);
        }
        return await new CopilotReadLocalFileTool().ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto, UserText = "读取日志", ReadableLocalFilePaths = files,
            SearchRootPaths = [_directory.FullName],
        }, new(), CancellationToken.None);
    }

    private static CopilotAgentTraceEntry CreateEntry(CopilotToolResult result) => CopilotAgentTraceEntry.FromResult(new()
    {
        ToolName = result.ToolName, CallId = Guid.NewGuid().ToString("N"), State = CopilotToolExecutionState.Completed,
        Access = CopilotToolAccess.ReadOnly, StartedAtUtc = DateTimeOffset.UtcNow, CompletedAtUtc = DateTimeOffset.UtcNow,
    }, CopilotToolResultContract.Capture(result.ToolName, result));

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotPartialEvidence-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
