using ColorVision.Engine.Templates.Flow;
using Microsoft.Data.Sqlite;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateCoverTests
{
    [Fact]
    public async Task ThemeSpecificCoversKeepSavedControlValuesAndPortGeometryWithoutLoadingRuntimeNodes()
    {
        string data = WpfTestHost.Invoke(() =>
        {
            using var editor = new STNodeEditor();
            var first = new FlowCoverVisualTestNode();
            var second = new FlowCoverVisualTestNode();
            first.Create(); second.Create();
            first.Left = 20; first.Top = 160;
            second.Left = 330; second.Top = 20;
            editor.Nodes.AddRange([first, second]);
            Assert.Equal(ConnectionStatus.Connected, first.Output.ConnectOption(second.Inputs[1]));
            var appearance = first.CapturePreviewAppearance(STNodeSavedState.Parse(first.GetSaveData()).Properties);
            Assert.Equal("400", Assert.Single(appearance.Texts).Value);
            Assert.True(appearance.Inputs[0].IsSingle);
            Assert.False(appearance.Outputs[0].IsSingle);
            Assert.Equal(System.Drawing.Color.Goldenrod, appearance.TitleColor);
            return Convert.ToBase64String(editor.GetCanvasData());
        });
        var service = new FlowTemplateCoverService(NewCachePath());
        var darkStyle = new FlowTemplateCoverStyle(STNodeVisualTheme.Dark, true, 10, 0.3f);
        var lightStyle = darkStyle with { Theme = STNodeVisualTheme.Light };
        var dark = await service.LoadAsync(data, CancellationToken.None, darkStyle);
        var light = await service.LoadAsync(data, CancellationToken.None, lightStyle);
        Assert.NotSame(dark, light);
        Assert.True(service.TryGetCached(data, out var darkCached, darkStyle));
        Assert.True(service.TryGetCached(data, out var lightCached, lightStyle));
        Assert.Same(dark, darkCached);
        Assert.Same(light, lightCached);
        using var db = new SqliteConnection($"Data Source={service.DatabasePath};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(DISTINCT hex(png)) FROM flow_template_covers";
        Assert.Equal(2L, command.ExecuteScalar());
    }

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
        var repeated = service.LoadAsync(data, CancellationToken.None);
        Assert.True(repeated.IsCompletedSuccessfully);
        Assert.Same(first, await repeated);
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT generated_at FROM flow_template_covers";
        string generatedAt = (string)command.ExecuteScalar()!;
        await new FlowTemplateCoverService(path).LoadAsync(data, CancellationToken.None);
        Assert.Equal(generatedAt, command.ExecuteScalar());
        string changedData = CreateCanvas(160);
        Assert.False(service.TryGetCached(changedData, out _));
        await service.LoadAsync(changedData, CancellationToken.None);
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
        Assert.NotNull(await new FlowTemplateCoverService(path).LoadAsync(data, CancellationToken.None));
        // A file in the directory position makes cache creation fail deterministically.
        Assert.NotNull(await new FlowTemplateCoverService(Path.Combine(path, "unavailable.db")).LoadAsync(data, CancellationToken.None));
    }

    [Fact]
    public async Task RecreatedTilesShowCachedCoverImmediatelyAndKeepItWhenParametersReload()
    {
        var service = new FlowTemplateCoverService(NewCachePath());
        string data = CreateCanvas();
        var cached = await service.LoadAsync(data, CancellationToken.None);
        await WpfTestHost.Invoke<Task>(async () =>
        {
            var viewport = new Grid();
            var host = new Window { Content = viewport, Width = 320, Height = 180, Left = -10000, ShowActivated = false, ShowInTaskbar = false };
            try
            {
                host.Show();
                // Reopening the browser or switching back to tiles creates a new cover control.
                var cover = new FlowTemplateCover { Flow = new FlowParam { DataBase64 = data } };
                var loaded = new TaskCompletionSource();
                cover.Loaded += (_, _) => loaded.TrySetResult();
                viewport.Children.Add(cover);
                host.UpdateLayout();
                await loaded.Task;
                cover.Refresh(viewport, service);
                Assert.Same(cached, Assert.IsType<Image>(cover.Children[1]).Source);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<TextBlock>(cover.Children[0]).Visibility);
                cover.Flow = new FlowParam { DataBase64 = data };
                Assert.Same(cached, Assert.IsType<Image>(cover.Children[1]).Source);
                cover.Flow = new FlowParam { DataBase64 = "" };
                cover.Refresh(viewport, service);
                Assert.Null(Assert.IsType<Image>(cover.Children[1]).Source);
                Assert.Equal(Visibility.Visible, Assert.IsType<TextBlock>(cover.Children[0]).Visibility);
            }
            finally { host.Close(); }
        });
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

public sealed class FlowCoverVisualTestNode : STNode
{
    public STNodeOption[] Inputs { get; private set; } = [];
    public STNodeOption Output { get; private set; } = null!;
    [STNodeProperty("曝光", "曝光时间", true)]
    public float ExpTime { get => 400; set => throw new InvalidOperationException("Preview must not call saved property setters."); }
    protected override void OnCreate()
    {
        Title = "L/BV相机";
        TitleColor = System.Drawing.Color.Goldenrod;
        SetFixedSize(160, 90);
        Inputs = [InputOptions.Add("IN_IMG", typeof(FlowEngineLib.Base.CVStartCFC), true), InputOptions.Add("IN_ROI", typeof(FlowEngineLib.Base.CVStartCFC), true)];
        Output = OutputOptions.Add("OUT", typeof(FlowEngineLib.Base.CVStartCFC), false);
        Controls.Add(new FlowEngineLib.STNodeEditText<float> { Text = "曝光:", Value = 100, DisplayRectangle = new System.Drawing.Rectangle(5, 42, 150, 18) });
    }
    public override void OnLoadNode(Dictionary<string, byte[]> properties) => throw new InvalidOperationException("Covers must not restore live node properties.");
    protected override void OnEditorLoadCompleted() => throw new InvalidOperationException("Covers must not load a runtime editor.");
}
