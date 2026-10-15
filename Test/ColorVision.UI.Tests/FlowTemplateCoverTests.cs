using ColorVision.Engine.Templates.Flow;
using Microsoft.Data.Sqlite;
using ST.Library.UI.NodeEditor;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateCoverTests
{
    [Fact]
    public async Task CoverIsBoundedCachedAndChangesWhenSavedLayoutChangesWithoutLoadingRuntimeNodes()
    {
        string path = NewCachePath();
        var service = new FlowTemplateCoverService(path);
        var data = CreateCanvas();
        var first = await service.LoadAsync(data, CancellationToken.None);
        Assert.NotNull(first);
        Assert.True(first.IsFrozen);
        Assert.Equal(640, first.PixelWidth);
        Assert.Equal(360, first.PixelHeight);
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT generated_at FROM flow_template_covers";
        string generatedAt = (string)command.ExecuteScalar()!;
        await new FlowTemplateCoverService(path).LoadAsync(data, CancellationToken.None);
        Assert.Equal(generatedAt, command.ExecuteScalar());
        await service.LoadAsync(CreateCanvas(160), CancellationToken.None);
        command.CommandText = "SELECT COUNT(*) FROM flow_template_covers";
        Assert.Equal(2L, command.ExecuteScalar());
        command.CommandText = "SELECT COUNT(DISTINCT hex(png)) FROM flow_template_covers";
        Assert.Equal(2L, command.ExecuteScalar());
    }

    [Fact]
    public async Task BrokenCacheDoesNotBlockCoverAndCorruptPngIsRegenerated()
    {
        string path = NewCachePath();
        string data = CreateCanvas();
        var service = new FlowTemplateCoverService(path);
        await service.LoadAsync(data, CancellationToken.None);
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "UPDATE flow_template_covers SET png=x'0001'";
            command.ExecuteNonQuery();
        }
        Assert.NotNull(await service.LoadAsync(data, CancellationToken.None));
        // A file in the directory position makes cache creation fail deterministically.
        Assert.NotNull(await new FlowTemplateCoverService(Path.Combine(path, "unavailable.db")).LoadAsync(data, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyCorruptAndCancelledCoversCannotCreateMisleadingCachedImages()
    {
        string path = NewCachePath();
        var service = new FlowTemplateCoverService(path);
        Assert.Null(await service.LoadAsync("", CancellationToken.None));
        Assert.False(File.Exists(path));
        await Assert.ThrowsAsync<FormatException>(() => service.LoadAsync("invalid base64", CancellationToken.None));
        Assert.False(File.Exists(path));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.LoadAsync(CreateCanvas(), cancellation.Token));
        Assert.False(File.Exists(path));
    }

    internal static string CreateCanvas(int targetY = 60) => WpfTestHost.Invoke(() =>
    {
        using var editor = new STNodeEditor();
        var first = new FlowCoverTestNode();
        var second = new FlowCoverTestNode();
        first.Create(); second.Create();
        first.Title = "图像采集"; second.Title = "亮度检测";
        first.Left = 20; first.Top = 60;
        second.Left = 240; second.Top = targetY;
        editor.Nodes.AddRange([first, second]);
        Assert.Equal(ConnectionStatus.Connected, first.Output.ConnectOption(second.Input));
        return Convert.ToBase64String(editor.GetCanvasData());
    });

    private static string NewCachePath() => Path.Combine(Path.GetTempPath(), "ColorVision-flow-cover-tests", Guid.NewGuid().ToString("N"), "covers.db");
}

public sealed class FlowCoverTestNode : STNode
{
    public STNodeOption Input { get; private set; } = null!;
    public STNodeOption Output { get; private set; } = null!;
    protected override void OnCreate()
    {
        Input = InputOptions.Add("IN", typeof(string), true);
        Output = OutputOptions.Add("OUT", typeof(string), false);
    }
    public override void OnLoadNode(Dictionary<string, byte[]> properties) => throw new InvalidOperationException("Covers must not restore live node properties.");
    protected override void OnEditorLoadCompleted() => throw new InvalidOperationException("Covers must not load a runtime editor.");
}
