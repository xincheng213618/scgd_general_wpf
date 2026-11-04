using ColorVision.Algorithms;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.FindCross;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Tif;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LocalRgbCrossHistoryTests
{
    // Registry discovery order cannot decide which persisted contract owns a result.
    [Theory]
    [InlineData("1.0", false, true)]
    [InlineData("2.0", true, false)]
    [InlineData("3.0", false, false)]
    [InlineData(null, false, false)]
    public void RgbHistoryAndLegacySingleCrossHaveExclusiveOwnership(string? version, bool rgb, bool legacy)
    {
        ViewResultAlg result = new() { ResultType = ViewResultAlgType.FindCross, Version = version };
        Assert.Equal(rgb, new ViewHandleLocalRgbCross().CanHandle1(result));
        Assert.Equal(legacy, new ViewHandleFindCross().CanHandle1(result));
        result.ResultType = ViewResultAlgType.Distortion;
        Assert.False(new ViewHandleLocalRgbCross().CanHandle1(result));
    }

    [Fact]
    public void ParserPreservesSavedValuesAndSourceGeometryWithoutRecipeOrRemeasurement()
    {
        LocalRgbCrossHistory history = LocalRgbCrossHistory.Parse(Measurement().ToJsonString());
        LocalRgbCrossHistoryRow row = Assert.Single(history.Rows);
        Assert.Equal(1.25, row.RG);
        Assert.Equal(2.5, row.BG);
        Assert.Contains("saturated_samples", row.Diagnostic);
        AlgorithmGeometry horizontal = Assert.Single(history.Geometries, item => item.Id == "P1-R-horizontal");
        Assert.Equal(new AlgorithmPoint(20, 46), horizontal.Points[0]);
        Assert.Equal(new AlgorithmPoint(100, 51), horizontal.Points[1]);
        Assert.Contains(history.Overlays, item => item.GeometryId == "P1-region" && item.Style.Label!.Contains("R-G 1.250"));
        // The values above intentionally differ from differences between the saved edges.
        // Display must use the persisted comparison, rather than applying another measurement.
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("outside")]
    [InlineData("channel-outside-region")]
    [InlineData("invalid-channel-with-geometry")]
    [InlineData("invalid-pair-with-value")]
    [InlineData("non-finite")]
    [InlineData("coordinate-space")]
    [InlineData("unknown-schema")]
    [InlineData("duplicate")]
    public void MalformedGeometryAndContractsAreRejectedBeforeDrawing(string defect)
    {
        JsonObject json = Measurement();
        JsonNode point = json["points"]![0]!;
        switch (defect)
        {
            case "negative": point["region"]!["x"] = -1; break;
            case "outside": point["region"]!["width"] = 1000; break;
            case "channel-outside-region": point["channels"]!["R"]!["horizontalArm"]!["x"] = 0; break;
            case "invalid-channel-with-geometry": point["channels"]!["R"]!["status"] = "INVALID"; break;
            case "invalid-pair-with-value": point["comparisons"]!["R-G"]!["status"] = "INVALID"; break;
            case "non-finite": point["comparisons"]!["R-G"]!["maximumAbsoluteEdgeOffsetPx"] = "NaN"; break;
            case "coordinate-space": json["coordinates"]!["space"] = "roi"; break;
            case "unknown-schema": json["schemaVersion"] = "99.0.0"; break;
            case "duplicate": json["grid"]!["columns"] = 2; ((JsonArray)json["points"]!).Add(point.DeepClone()); break;
        }
        Assert.ThrowsAny<Exception>(() => LocalRgbCrossHistory.Parse(json.ToJsonString()));
    }

    [Fact]
    public void InvalidPointsRetainReasonsAndDoNotInventEdgesOrPairValues()
    {
        JsonObject json = Measurement();
        JsonNode point = json["points"]![0]!;
        point["status"] = "INVALID";
        point["reasonCodes"] = new JsonArray("G:arm_edges_not_found");
        point["channels"]!["G"] = new JsonObject
        {
            ["status"] = "INVALID", ["reasonCodes"] = new JsonArray("arm_edges_not_found"),
            ["horizontalArm"] = null, ["verticalArm"] = null
        };
        point["comparisons"] = new JsonObject { ["R-G"] = Pair(null), ["B-G"] = Pair(null) };
        LocalRgbCrossHistory history = LocalRgbCrossHistory.Parse(json.ToJsonString());
        LocalRgbCrossHistoryRow row = Assert.Single(history.Rows);
        Assert.Equal("INVALID", row.Status);
        Assert.Null(row.RG); Assert.Null(row.BG);
        Assert.Contains("arm_edges_not_found", row.Diagnostic);
        Assert.DoesNotContain(history.Geometries, item => item.Id.StartsWith("P1-G-"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("broken-reference")]
    [InlineData("broken-file")]
    [InlineData("wrong-owner")]
    public void LoadTurnsUnreadableHistoryIntoVisibleDiagnosticsAndDoesNotRetryDatabase(string failure)
    {
        string file = Path.Combine(Path.GetTempPath(), $"rgb-history-{Guid.NewGuid():N}.json");
        try
        {
            if (failure != "missing") File.WriteAllText(file, failure == "broken-file" ? "{" : Measurement().ToJsonString());
            int reads = 0;
            ViewHandleLocalRgbCross handler = new(_ =>
            {
                reads++;
                return [new DetailCommonModel { PId = failure == "wrong-owner" ? 8 : 7,
                    ResultJson = failure == "broken-reference" ? "{" : new JsonObject { ["ResultFileName"] = file }.ToJsonString() }];
            });
            ViewResultAlg result = new() { Id = 7 };
            handler.Load(null!, result);
            handler.Load(null!, result);
            LocalRgbCrossHistory history = Assert.IsType<LocalRgbCrossHistory>(Assert.Single(result.ViewResults));
            Assert.Contains("读取失败", history.Diagnostic);
            Assert.Empty(history.Geometries); Assert.Empty(history.Rows);
            Assert.Equal(1, reads);
        }
        finally { File.Delete(file); }
    }

    // Protects the source-ready and selection boundary: a late open must never restore an old result overlay.
    [Fact]
    public async Task AsyncSourceLoadingAndSelectionChangesKeepOnlyCurrentHistoryOverlays()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rgb-history-{Guid.NewGuid():N}.png");
        ImageView? view = null;
        ViewResultContext? context = null;
        DelayedPngOpen? opener = null;
        Task first = Task.CompletedTask, second = Task.CompletedTask;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                EnsureResources();
                using (FileStream stream = File.Create(path))
                {
                    PngBitmapEncoder png = new();
                    png.Frames.Add(BitmapFrame.Create(new WriteableBitmap(320, 320, 96, 96, PixelFormats.Gray8, null)));
                    png.Save(stream);
                }
                view = new ImageView { EnableEditorImageServices = false };
                view.Config.IsLayoutUpdated = false;
                context = new() { ImageView = view, ListView = new ListView { View = new GridView() }, LeftGridViewColumnVisibilitys = new(), SideTextBox = new TextBox() };
                opener = new(view.EditorContext);
                view.IEditorToolFactory.IImageOpens[".png"] = opener;
                ViewHandleLocalRgbCross handler = new(_ => throw new InvalidOperationException("Preloaded history must not query the database."));
                first = handler.PresentAsync(context, Result(LocalRgbCrossHistory.Parse(Measurement().ToJsonString()), path));
                Assert.False(first.IsCompleted);
                Assert.Empty(view.EditorContext.ProcessingContext.AlgorithmOverlays.Snapshot());
                // Mirrors AlgorithmView selection preparation; use the same source path intentionally.
                context.ListView.ItemsSource = null;
                view.ImageShow.Clear();
                second = handler.PresentAsync(context, Result(LocalRgbCrossHistory.Failure("broken JSON"), path));
                opener.Continue.SetResult();
            });
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));
            await WpfTestHost.Invoke(() =>
            {
                Assert.IsAssignableFrom<BitmapSource>(view!.ViewBitmapSource);
                Assert.Empty(view.EditorContext.ProcessingContext.AlgorithmOverlays.Snapshot());
                Assert.Contains("broken JSON", context!.SideTextBox.Text);
                return new ViewHandleLocalRgbCross().PresentAsync(context, Result(LocalRgbCrossHistory.Parse(Measurement().ToJsonString()), path));
            });
            WpfTestHost.Invoke(() =>
            {
                AlgorithmOverlayArtifact overlay = Assert.Single(view!.EditorContext.ProcessingContext.AlgorithmOverlays.Snapshot());
                Assert.Equal("rgb-cross-history", overlay.Name);
                Assert.Contains(overlay.Items, item => item.Style.Label?.Contains("R-G 1.250") == true);
                Assert.Single(context!.ListView.Items);
                view.ImageShow.Clear();
                Assert.Empty(view.EditorContext.ProcessingContext.AlgorithmOverlays.Snapshot());
            });
        }
        finally
        {
            opener?.Continue.TrySetResult();
            if (view != null) WpfTestHost.Invoke(view.Dispose);
            File.Delete(path);
        }
    }

    private static ViewResultAlg Result(LocalRgbCrossHistory history, string path) => new()
    {
        Version = "2.0", ResultType = ViewResultAlgType.FindCross, FilePath = path,
        ViewResults = new ObservableCollection<IViewResult> { history }
    };

    private sealed class DelayedPngOpen(EditorContext context) : IAsyncImageOpen
    {
        internal TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OpenImage(EditorContext owner, string? filePath) => throw new NotSupportedException();
        public async Task OpenImageAsync(EditorContext owner, string? filePath, CancellationToken cancellationToken = default)
        {
            await Continue.Task;
            await new CommonImageOpen(context).OpenImageAsync(owner, filePath, cancellationToken);
        }
    }

    private static JsonObject Measurement() => new()
    {
        ["schemaId"] = "colorvision.rgb-cross-measurement", ["schemaVersion"] = "1.2.0", ["referenceChannel"] = "G",
        ["algorithm"] = new JsonObject { ["id"] = "colorvision.display.rgb-cross-registration", ["version"] = "1.6.1" },
        ["source"] = new JsonObject { ["width"] = 320, ["height"] = 320 },
        ["coordinates"] = new JsonObject { ["space"] = "source-image", ["unit"] = "px", ["origin"] = "top-left", ["pixelCenters"] = "integer", ["axes"] = "x-right-y-down" },
        ["grid"] = new JsonObject { ["rows"] = 1, ["columns"] = 1 }, ["searchRegion"] = Box(0, 0, 320, 320),
        ["execution"] = new JsonObject { ["status"] = "SUCCEEDED", ["reasonCodes"] = new JsonArray() },
        ["points"] = new JsonArray(new JsonObject
        {
            ["id"] = "P1", ["row"] = 1, ["column"] = 1, ["status"] = "VALID", ["region"] = Box(10, 10, 100, 100),
            ["reasonCodes"] = new JsonArray(), ["warnings"] = new JsonArray("saturated_samples_threshold_edges_may_be_biased"),
            ["channels"] = new JsonObject { ["R"] = Channel(1), ["G"] = Channel(0), ["B"] = Channel(-2) },
            ["comparisons"] = new JsonObject { ["R-G"] = Pair(1.25), ["B-G"] = Pair(2.5) }
        })
    };
    private static JsonObject Channel(int shift) => new()
    {
        ["status"] = "VALID", ["reasonCodes"] = new JsonArray(),
        ["horizontalArm"] = Box(20, 45 + shift, 80, 5), ["verticalArm"] = Box(50 + shift, 20, 5, 80)
    };
    private static JsonObject Pair(double? value) => new() { ["status"] = value.HasValue ? "VALID" : "INVALID", ["maximumAbsoluteEdgeOffsetPx"] = value };
    private static JsonObject Box(double x, double y, double width, double height) => new() { ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height };
    private static void EnsureResources()
    {
        Application.Current.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        Application.Current.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        Application.Current.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        Application.Current.Resources["ToolBarImage"] = new Style(typeof(Image));
        Application.Current.Resources["BaseStyle"] = new Style(typeof(Control));
        Application.Current.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        Application.Current.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
