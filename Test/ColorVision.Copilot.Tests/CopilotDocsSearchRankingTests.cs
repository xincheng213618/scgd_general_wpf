using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

// A search that always answers would hand the model unrelated pages as documentation evidence.
public sealed class CopilotDocsSearchRankingTests
{
    private static readonly string Index = JsonSerializer.Serialize(new
    {
        entries = new object[]
        {
            new { kind = "page", status = "current", title = "设置与流程的导入导出", text = "按对象选择入口。", aliases = new[] { "如何导出CSV" }, url = "exports.html", relativePath = "exports.md" },
            new { kind = "page", status = "current", title = "相机配置", text = "在设备页设置曝光时间和增益。", aliases = Array.Empty<string>(), url = "camera.html", relativePath = "camera.md" },
            new { kind = "page", status = "planned", title = "相机配置路线图", text = "以后设置曝光时间的计划。", aliases = Array.Empty<string>(), url = "roadmap.html", relativePath = "roadmap.md" },
        },
    });

    [Theory]
    [InlineData("the quick brown fox")]
    [InlineData("量子纠缠隐形传态")]
    public void UnrelatedQueryReturnsNothing(string query)
    {
        Assert.Empty(CopilotDocsToolSupport.SearchIndexJson(Index, query).Hits);
    }

    [Theory]
    [InlineData("如何导出CSV", "exports.md")]
    [InlineData("相机怎么设置曝光时间", "camera.md")]
    public void QuestionFindsTheTopicThatAnswersIt(string query, string expected)
    {
        Assert.Equal(expected, CopilotDocsToolSupport.SearchIndexJson(Index, query).Hits[0].RelativePath);
    }

    [Fact]
    public async Task SavedIndexServesRestartsAndAnswersWhenARefreshFails()
    {
        var directory = Directory.CreateTempSubdirectory("CopilotDocsCache-");
        try
        {
            var cachePath = Path.Combine(directory.FullName, "nested", "index.json");
            var downloads = 0;
            Task<string> Download(CancellationToken _) { downloads++; return Task.FromResult(Index); }
            Task<string> Offline(CancellationToken _) => throw new InvalidOperationException("offline");

            var first = await CopilotDocsToolSupport.SearchThroughDiskCacheAsync(cachePath, Download, "相机配置");
            var restarted = await CopilotDocsToolSupport.SearchThroughDiskCacheAsync(cachePath, Download, "相机配置");
            await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotDocsToolSupport.SearchThroughDiskCacheAsync(cachePath + ".missing", Offline, "相机配置"));
            File.SetLastWriteTimeUtc(cachePath, DateTime.UtcNow.AddDays(-2));
            var offline = await CopilotDocsToolSupport.SearchThroughDiskCacheAsync(cachePath, Offline, "相机配置");

            Assert.Equal(1, downloads);
            Assert.False(first.IndexIsStale);
            Assert.False(restarted.IndexIsStale);
            Assert.True(offline.IndexIsStale);
            Assert.NotEmpty(offline.Hits);
            Assert.Contains("saved copy", CopilotDocsToolSupport.BuildContextBlock(offline));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void PlannedTopicRanksBelowTheCurrentOne()
    {
        var hits = CopilotDocsToolSupport.SearchIndexJson(Index, "相机 曝光时间").Hits;

        Assert.Equal(["camera.md", "roadmap.md"], hits.Select(hit => hit.RelativePath));
    }
}
