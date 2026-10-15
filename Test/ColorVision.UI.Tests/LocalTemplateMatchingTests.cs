using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2;
using ColorVision.Themes;
using FlowEngineLib.Base;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

// Protects the portable recipe, native/managed boundary and flow failure/ownership contract.
public sealed class LocalTemplateMatchingTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void PortableTemplatePreservesPixelsParametersAndPreview(int depth)
    {
        using Recipe recipe = new();
        TemplateMatchingDocument document = CreateDocument();
        document.Image.Depth = depth;
        document.Image.Pixels = Enumerable.Range(0, 13 * 11 * depth / 8).Select(i => (byte)(i % 247)).ToArray();
        if (depth == 32)
            Buffer.BlockCopy(Enumerable.Range(0, 143).Select(i => (float)(i * 0.125)).ToArray(), 0, document.Image.Pixels, 0, document.Image.Pixels.Length);
        document.Options.AngleMin = -7;
        TemplateMatchingFile.Save(recipe.Path, document);
        TemplateMatchingDocument loaded = TemplateMatchingFile.Load(recipe.Path);
        Assert.Equal(document.Fingerprint(), loaded.Fingerprint());
        Assert.Equal(document.Image.Pixels, loaded.Image.Pixels);
        Assert.Equal(-7, loaded.Options.AngleMin);
        StaTest.Run(() => { BitmapSource preview = loaded.Image.CreatePreview(); Assert.Equal(13, preview.PixelWidth); Assert.True(preview.IsFrozen); });
    }

    [Fact]
    public void InvalidSaveDoesNotReplaceExistingFileAndLoadRejectsIncompatibleOrIncompleteData()
    {
        using Recipe recipe = new();
        string original = File.ReadAllText(recipe.Path);
        TemplateMatchingDocument document = CreateDocument();
        document.Options.ScaleStep = 0;
        Assert.Throws<ArgumentException>(() => TemplateMatchingFile.Save(recipe.Path, document));
        Assert.Equal(original, File.ReadAllText(recipe.Path));
        foreach (string field in new[] { "schema", "image", "options" })
        {
            JObject json = JObject.Parse(original);
            json.Remove(field);
            File.WriteAllText(recipe.Path, json.ToString());
            Assert.Throws<System.Text.Json.JsonException>(() => TemplateMatchingFile.Load(recipe.Path));
        }
        File.WriteAllText(recipe.Path, original.Replace("matching/v1", "matching/v99"));
        Assert.Throws<InvalidDataException>(() => TemplateMatchingFile.Load(recipe.Path));
        File.WriteAllText(recipe.Path, original[..(original.Length / 2)]);
        Assert.Throws<System.Text.Json.JsonException>(() => TemplateMatchingFile.Load(recipe.Path));
    }

    [Fact]
    public void CaptureCopiesOnlyRoiAcrossPaddedRows()
    {
        byte[] source = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
        WithImage(source, 6, 4, 10, image =>
        {
            TemplateMatchingImage captured = TemplateMatchingImage.Capture(image, new(2, 1, 3, 2));
            Assert.Equal(new byte[] { 12, 13, 14, 22, 23, 24 }, captured.Pixels);
        });
    }

    [Fact]
    public void NativeRoundTripFindsMultipleTargetsInFullImageCoordinatesAndRejectsAbsentTarget()
    {
        using Recipe recipe = new();
        TemplateMatchingDocument template = TemplateMatchingFile.Load(recipe.Path);
        byte[] source = MakeSource(template.Image, (23, 19), (66, 48));
        WithImage(source, 100, 80, 100, image =>
        {
            TemplateMatchingResult result = TemplateMatching.Run(image, template, new(15, 10, 75, 60));
            Assert.True(result.Success, result.RawJson);
            Assert.Equal(2, result.Matches.Length);
            foreach ((int x, int y) in new[] { (23, 19), (66, 48) })
            {
                TemplateMatchingMatch match = Assert.Single(result.Matches, m => Math.Abs(m.Corners[0].X - x) < 0.001 && Math.Abs(m.Corners[0].Y - y) < 0.001);
                Assert.InRange(match.Score, 0.99, 1.000001);
                Assert.Equal(1, match.Scale);
                Assert.Equal(0, match.AngleDegrees);
            }
            TemplateMatchingResult absent = TemplateMatching.Run(image, template, new(0, 0, 20, 16));
            Assert.False(absent.Success);
            Assert.Empty(absent.Matches);
            Assert.Throws<ArgumentException>(() => TemplateMatching.Run(image, template, new(90, 0, 20, 20)));
        });
        template.Image.Pixels = new byte[143];
        WithImage(source, 100, 80, 100, image => Assert.Equal("invalid_template", TemplateMatching.Run(image, template, default).StatusCode));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmbeddedNodeRunsNativeWithoutTemplateFileOrDatabase(bool persistLegacyResults)
    {
        using Recipe recipe = new();
        TemplateMatchingDocument document = TemplateMatchingFile.Load(recipe.Path);
        LocalTemplateMatchingNode original = new() { ParameterJson = document.ToJson() };
        original.Create();
        LocalTemplateMatchingNode restored = new();
        restored.Create();
        restored.OnLoadNode(ParseState(original.GetSaveData()));
        File.Delete(recipe.Path);
        CVStartCFC action = CreateAction();
        action.PersistResults = persistLegacyResults;
        action.Data["MasterId"] = 123;
        action.TryGetCurrentFrame(out LocalFlowFrame? frame);
        using LocalFlowFrameLease lease = frame!.Acquire();
        byte[] pixels = MakeSource(document.Image, (23, 19), (66, 48));
        Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
        try
        {
            LocalTemplateMatchingNodeResult result = restored.ExecuteSynchronously(action);
            Assert.Equal(2, result.Result.Matches.Length);
            Assert.Equal(document.Fingerprint(), result.TemplateFingerprint);
            Assert.Equal(frame.FrameId.ToString("N"), result.FrameId);
            Assert.Same(result.Result, action.Data["LocalTemplateMatchingResult"]);
            Assert.Equal(123, action.Data["MasterId"]);
            Assert.False(action.Data.ContainsKey("MasterResultType"));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void NodeSnapshotsEmbeddedSettingsAndRetainsMemoryFrame()
    {
        CVStartCFC action = CreateAction();
        action.TryGetCurrentFrame(out LocalFlowFrame? frame);
        LocalTemplateMatchingNode? node = null;
        FakeServices services = new()
        {
            DetectHandler = (image, document, roi) =>
            {
                Assert.Equal("synthetic", document.Name);
                Assert.Equal(5, roi.X);
                Assert.Equal(100, image.cols);
                TemplateMatchingDocument next = CreateDocument();
                next.Name = "edited during execution";
                node!.ParameterJson = next.ToJson();
                return Detection();
            }
        };
        node = new(services) { ParameterJson = CreateDocument().ToJson(), SearchRegion = new(5, 5, 85, 65) };
        try
        {
            LocalTemplateMatchingNodeResult result = node.ExecuteSynchronously(action);
            Assert.Equal("synthetic", result.TemplateName);
            Assert.Equal("edited during execution", TemplateMatchingDocument.FromJson(node.ParameterJson).Name);
            action.TryGetCurrentFrame(out LocalFlowFrame? after);
            Assert.Same(frame, after);
            Assert.Equal(1.2, ((TemplateMatchingMatch)action.Data["LocalTemplateMatchingBestMatch"]).Scale);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedDetectionOrInvocationCannotExposeStaleOutput(bool invocationFailure)
    {
        CVStartCFC action = CreateAction();
        action.Data["MasterId"] = 12;
        action.Data["LocalTemplateMatchingResult"] = Detection();
        action.Data["LocalTemplateMatchingMatches"] = Detection().Matches;
        action.Data["LocalTemplateMatchingBestMatch"] = Detection().Matches[0];
        FakeServices services = new()
        {
            DetectHandler = (_, _, _) => invocationFailure ? throw new InvalidOperationException("Native unavailable") : new() { StatusCode = "no_matches", Message = "No target" }
        };
        try
        {
            Assert.Throws<InvalidOperationException>(() => new LocalTemplateMatchingNode(services) { ParameterJson = CreateDocument().ToJson() }.ExecuteSynchronously(action));
            Assert.False(action.Data.ContainsKey("LocalTemplateMatchingResult"));
            Assert.False(action.Data.ContainsKey("LocalTemplateMatchingMatches"));
            Assert.False(action.Data.ContainsKey("LocalTemplateMatchingBestMatch"));
            Assert.Equal(12, action.Data["MasterId"]);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void LegacyImageIdAloneDoesNotCauseDatabaseLookup()
    {
        CVStartCFC action = new("no-database-batch");
        action.Data["MasterId"] = 12;
        action.Data["MasterResultType"] = 100;
        try
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
                new LocalTemplateMatchingNode() { ParameterJson = CreateDocument().ToJson() }.ExecuteSynchronously(action));
            Assert.Contains("加载图片", error.Message);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void InvalidRoiStopsBeforeNativeAndPreservesUpstreamFrame()
    {
        CVStartCFC action = CreateAction();
        action.TryGetCurrentFrame(out LocalFlowFrame? frame);
        FakeServices services = new();
        LocalTemplateMatchingNode node = new(services) { ParameterJson = CreateDocument().ToJson(), SearchRegion = new(99, 0, 10, 10) };
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
            Assert.Equal(0, services.Detected);
            Assert.True(action.TryGetCurrentFrame(out LocalFlowFrame? current));
            Assert.Same(frame, current);
            using LocalFlowFrameLease lease = frame!.Acquire();
            Assert.Equal(100, lease.Metadata.Width);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void NodeStateAndWindowUseSavedTemplate()
    {
        using Recipe recipe = new();
        LocalTemplateMatchingNode node = new() { ParameterJson = TemplateMatchingFile.Load(recipe.Path).ToJson(), SearchRegion = new(1, 2, 30, 40) };
        node.Create();
        Assert.Equal("LocalTemplateMatching", node.NodeType);
        Assert.Equal(new[] { "IN" }, node.GetAllInputOptions().Select(option => option.Text));
        LocalTemplateMatchingNode restored = new();
        restored.Create();
        restored.OnLoadNode(ParseState(node.GetSaveData()));
        Assert.Equal(node.ParameterJson, restored.ParameterJson);
        Assert.Equal(node.SearchRegion, restored.SearchRegion);
        WpfTestHost.Invoke(() =>
        {
            var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
            TemplateMatchingWindow window = new(recipe.Path);
            try
            {
                window.Measure(new Size(1020, 760));
                window.Arrange(new Rect(0, 0, 1020, 760));
                Assert.Equal(recipe.Path, window.SelectedPath);
                Assert.Equal("synthetic", ((TextBox)window.FindName("NameText")).Text);
                Assert.NotNull(((Image)window.FindName("TemplatePreview")).Source);
                string? output = Environment.GetEnvironmentVariable("COLORVISION_MATCHING_DEMO");
                if (!string.IsNullOrEmpty(output))
                {
                    Directory.CreateDirectory(output);
                    FrameworkElement content = (FrameworkElement)window.Content;
                    ((Grid)content).Background = window.Background;
                    window.Content = null;
                    content.Measure(new Size(992, 732));
                    content.Arrange(new Rect(0, 0, 992, 732));
                    content.UpdateLayout();
                    RenderTargetBitmap render = new(1020, 760, 96, 96, PixelFormats.Pbgra32);
                    render.Render(content);
                    SavePng(System.IO.Path.Combine(output, "template-manager.png"), render);
                    TemplateMatchingDocument document = TemplateMatchingFile.Load(recipe.Path);
                    TemplateMatchingFile.Save(System.IO.Path.Combine(output, "synthetic.cvtm"), document);
                    SavePng(System.IO.Path.Combine(output, "source.png"), BitmapSource.Create(100, 80, 96, 96, PixelFormats.Gray8, null, MakeSource(document.Image, (23, 19), (66, 48)), 100));
                }
            }
            finally
            {
                window.Close();
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NodeEditorCommitsImportedReferenceAndEditedParametersOnlyOnApply(bool apply)
    {
        using Recipe recipe = new();
        TemplateMatchingDocument original = CreateDocument();
        original.Name = "node original";
        string originalJson = original.ToJson();
        WpfTestHost.Invoke(() =>
        {
            var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
            TemplateMatchingWindow window = TemplateMatchingWindow.ForNode(originalJson);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = window.Top = -16000;
            window.ShowInTaskbar = window.ShowActivated = false;
            Exception? failure = null;
            window.Loaded += (_, _) =>
            {
                try
                {
                    Assert.Equal("应用到节点", ((Button)window.FindName("ApplyButton")).Content);
                    window.LoadTemplate(recipe.Path);
                    File.Delete(recipe.Path);
                    ((TextBox)window.FindName("NameText")).Text = "node edited";
                    window.UpdateLayout();
                    TextBox threshold = Assert.Single(Descendants(window).OfType<TextBox>(),
                        text => text.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == nameof(TemplateMatchingOptions.ScoreThreshold));
                    threshold.Text = "0.95";
                    if (apply) ((Button)window.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    else window.Close();
                }
                catch (Exception ex) { failure = ex; window.Close(); }
            };
            try
            {
                bool? accepted = window.ShowDialog();
                Assert.Null(failure);
                Assert.Equal(apply, accepted == true);
                Assert.Equal("node original", TemplateMatchingDocument.FromJson(originalJson).Name);
                if (apply)
                {
                    TemplateMatchingDocument edited = TemplateMatchingDocument.FromJson(window.ResultJson);
                    Assert.Equal("node edited", edited.Name);
                    Assert.Equal(0.95, edited.Options.ScoreThreshold);
                    Assert.Equal(original.Image.Pixels, edited.Image.Pixels);
                }
                else Assert.Empty(window.ResultJson);
            }
            finally
            {
                window.Close();
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject nested in Descendants(child)) yield return nested;
        }
    }

    private static void SavePng(string path, BitmapSource source)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
    private static TemplateMatchingDocument CreateDocument()
    {
        byte[] pixels = new byte[143];
        new Random(721).NextBytes(pixels);
        return new() { Name = "synthetic", Image = new() { Width = 13, Height = 11, Depth = 8, Channels = 1, Pixels = pixels },
            Options = new() { AngleMin = 0, AngleMax = 0, ScaleMin = 1, ScaleMax = 1, FeatureMode = TemplateMatchingFeature.Intensity, OcclusionTolerance = 0, PyramidLevels = 1, Subpixel = false, ScoreThreshold = 0.99 } };
    }
    private static byte[] MakeSource(TemplateMatchingImage template, params (int X, int Y)[] locations)
    {
        byte[] source = Enumerable.Repeat((byte)20, 8000).ToArray();
        foreach (var point in locations)
            for (int y = 0; y < template.Height; y++) Buffer.BlockCopy(template.Pixels, y * template.Width, source, (point.Y + y) * 100 + point.X, template.Width);
        return source;
    }
    private static void WithImage(byte[] pixels, int width, int height, int stride, Action<HImage> action)
    {
        IntPtr pointer = Marshal.AllocCoTaskMem(pixels.Length);
        try { Marshal.Copy(pixels, 0, pointer, pixels.Length); action(new() { cols = width, rows = height, depth = 8, channels = 1, stride = stride, pData = pointer, isDispose = true }); }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }
    private static TemplateMatchingResult Detection() => new() { Success = true, StatusCode = "ok", Matches = [new() { Center = new(18, 20), AngleDegrees = 2, Scale = 1.2, Score = 1, VisibleFraction = 1, Corners = [new(12, 15), new(24, 15), new(24, 25), new(12, 25)] }] };
    private static LocalFlowFrame Frame() => LocalFlowFrame.Allocate(new LocalFrameMetadata { Width = 100, Height = 80, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw, FlipMode = CVImageFlipMode.None }, 8000, 0);
    private static CVStartCFC CreateAction() { CVStartCFC action = new("matching-test"); action.SetCurrentFrame(Frame()); return action; }
    private sealed class Recipe : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".cvtm");
        public Recipe() => TemplateMatchingFile.Save(Path, CreateDocument());
        public void Dispose() => File.Delete(Path);
    }
    private sealed class FakeServices : ILocalTemplateMatchingServices
    {
        public int Detected { get; private set; }
        public Func<HImage, TemplateMatchingDocument, RoiRect, TemplateMatchingResult> DetectHandler { get; init; } = (_, _, _) => Detection();
        public TemplateMatchingResult Detect(HImage image, TemplateMatchingDocument document, RoiRect roi) { Detected++; return DetectHandler(image, document, roi); }
    }
    private static Dictionary<string, byte[]> ParseState(byte[] data)
    {
        int position = 0;
        position += data[position] + 1;
        position += data[position] + 1;
        Dictionary<string, byte[]> state = new();
        while (position < data.Length)
        {
            int keyLength = BitConverter.ToInt32(data, position); position += 4;
            string key = Encoding.UTF8.GetString(data, position, keyLength); position += keyLength;
            int valueLength = BitConverter.ToInt32(data, position); position += 4;
            state.Add(key, data[position..(position + valueLength)]); position += valueLength;
        }
        return state;
    }
}
