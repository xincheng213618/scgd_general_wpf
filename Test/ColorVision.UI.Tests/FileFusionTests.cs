using ColorVision.Core;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class FileFusionTests
{
    [Fact]
    public void CpuNativeIntegrationReturnsOwnedManagedImageForTwoFiles()
    {
        using var fixture = new FusionFixture();
        FileFusionResult result = FileFusion.Default.Execute([fixture.Add("图像甲.png", value: 123), fixture.Add("图像乙.png", value: 123)], FileFusionMode.CPU);
        Assert.Equal(FileFusionMode.CPU, result.ActualMode);
        Assert.Equal(4, result.Image.PixelWidth);
        Assert.Equal(4, result.Image.PixelHeight);
        Assert.Equal(PixelFormats.Gray8, result.Image.Format);
        Assert.True(result.Image.IsFrozen);
        byte[] pixels = new byte[16];
        result.Image.CopyPixels(pixels, 4, 0);
        Assert.All(pixels, pixel => Assert.Equal(123, pixel));
    }

    [Fact]
    public void FolderUsesNaturalOrderAndDoesNotRecurse()
    {
        using var fixture = new FusionFixture();
        string ten = fixture.Add("plane10.png"), two = fixture.Add("plane2.png"), one = fixture.Add("plane1.png");
        Directory.CreateDirectory(Path.Combine(fixture.Directory, "nested"));
        fixture.Add("nested/plane0.png");
        File.WriteAllText(Path.Combine(fixture.Directory, "note.txt"), "ignored");
        Assert.Equal(new[] { one, two, ten }, FileFusion.GetFolderFiles(fixture.Directory));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void AutoRecordsActualBackendAndKeepsExplicitOrder(int count)
    {
        using var fixture = new FusionFixture();
        string[] files = Enumerable.Range(0, count).Select(i => fixture.Add($"plane{i}.png")).Reverse().ToArray();
        var executor = new FileFusion((string json, FileFusionMode mode, out HImage image) =>
        {
            Assert.Equal(files, JsonDocument.Parse(json).RootElement.GetProperty("files").Deserialize<string[]>());
            Assert.Equal(count < 5 ? FileFusionMode.CPU : FileFusionMode.GPU, mode);
            // The validated file must not be replaceable before native decoding finishes.
            Assert.Throws<IOException>(() => File.Open(files[0], FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
            image = CreateOutput();
            return 0;
        }, () => true);
        FileFusionResult result = executor.Execute(files);
        Assert.True(result.Image.IsFrozen);
        Assert.Equal(files, result.Files);
        Assert.Equal(count < 5 ? FileFusionMode.CPU : FileFusionMode.GPU, result.ActualMode);
        byte[] pixels = new byte[16];
        result.Image.CopyPixels(pixels, 4, 0);
        Assert.All(pixels, pixel => Assert.Equal(42, pixel));
        using FileStream unlocked = File.Open(files[0], FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Theory]
    [InlineData(FileFusionMode.GPU)]
    [InlineData(FileFusionMode.GPUAsync)]
    public void ExplicitGpuRejectsSmallStackBeforeNative(FileFusionMode mode)
    {
        using var fixture = new FusionFixture();
        var executor = NeverExecute();
        Assert.Throws<NotSupportedException>(() => executor.Execute([fixture.Add("a.png"), fixture.Add("b.png")], mode));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("size")]
    [InlineData("depth")]
    [InlineData("channels")]
    [InlineData("corrupt")]
    public void InvalidFilesNeverReachNative(string kind)
    {
        using var fixture = new FusionFixture();
        string first = fixture.Add("a.png");
        string second = kind switch
        {
            "duplicate" => first,
            "missing" => Path.Combine(fixture.Directory, "missing.png"),
            "size" => fixture.Add("b.png", width: 8),
            "depth" => fixture.Add("b.png", PixelFormats.Gray16),
            "channels" => fixture.Add("b.png", PixelFormats.Bgr24),
            _ => Path.Combine(fixture.Directory, "bad.png"),
        };
        if (kind == "corrupt") File.WriteAllText(second, "not an image");
        int calls = 0;
        var executor = new FileFusion((string json, FileFusionMode mode, out HImage image) => { calls++; image = default; return -1; }, () => true);
        Assert.ThrowsAny<Exception>(() => executor.Execute([first, second], FileFusionMode.CPU));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void CancellationAfterNativeReturnsDiscardsResultAndReleasesInputLocks()
    {
        using var fixture = new FusionFixture();
        string[] files = [fixture.Add("a.png"), fixture.Add("b.png")];
        using var cancellation = new CancellationTokenSource();
        var executor = new FileFusion((string json, FileFusionMode mode, out HImage image) =>
        {
            image = CreateOutput();
            cancellation.Cancel();
            return 0;
        }, () => false);
        Assert.Throws<OperationCanceledException>(() => executor.Execute(files, FileFusionMode.CPU, cancellation.Token));
        using FileStream unlocked = File.Open(files[0], FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [Fact]
    public async Task AsyncExecutionSnapshotsListBeforeScheduling()
    {
        using var fixture = new FusionFixture();
        var files = new List<string> { fixture.Add("a.png"), fixture.Add("b.png") };
        var options = new FileFusionOptions { FocusWindow = 7, Threshold = 16 };
        var executor = new FileFusion((string json, FileFusionMode mode, out HImage image) =>
        {
            var settings = JsonDocument.Parse(json).RootElement.GetProperty("options");
            Assert.Equal(7, settings.GetProperty("focusWindow").GetInt32());
            Assert.Equal(16, settings.GetProperty("threshold").GetDouble());
            image = CreateOutput(); return 0;
        }, () => false);
        Task<FileFusionResult> pending = executor.ExecuteAsync(files, FileFusionMode.CPU, options: options);
        files.Clear();
        options.FocusWindow = 9;
        var result = await pending;
        Assert.Equal(2, result.Files.Count);
        Assert.Equal(7, result.Options.FocusWindow);
    }

    [Theory]
    [InlineData(4, 3, 2, 0.2, 13)]
    [InlineData(5, 33, 2, 0.2, 13)]
    [InlineData(5, 3, 0, 0.2, 13)]
    [InlineData(5, 3, 2, double.NaN, 13)]
    [InlineData(5, 3, 2, 0.2, double.PositiveInfinity)]
    public void InvalidParametersNeverReachNative(int focus, int consistency, int step, double alpha, double threshold)
    {
        var options = new FileFusionOptions { FocusWindow = focus, ConsistencyWindow = consistency, FocusStep = step, Alpha = alpha, Threshold = threshold };
        Assert.Throws<ArgumentOutOfRangeException>(() => NeverExecute().Execute(["a.png", "b.png"], options: options));
    }

    [Fact]
    public void NativeFailureDoesNotRetryAndReleasesFiles()
    {
        using var fixture = new FusionFixture();
        string[] files = Enumerable.Range(0, 5).Select(i => fixture.Add($"{i}.png")).ToArray();
        int calls = 0;
        var executor = new FileFusion((string json, FileFusionMode mode, out HImage image) => { calls++; image = CreateOutput(); return -7; }, () => true);
        Assert.Contains("-7", Assert.Throws<InvalidOperationException>(() => executor.Execute(files)).Message);
        Assert.Equal(1, calls);
        using FileStream unlocked = File.Open(files[0], FileMode.Open, FileAccess.Write, FileShare.None);
    }

    private static FileFusion NeverExecute() => new((string json, FileFusionMode mode, out HImage image) =>
    {
        image = default;
        Assert.Fail("Invalid input reached native fusion.");
        return -1;
    }, () => true);

    private static HImage CreateOutput()
    {
        IntPtr data = Marshal.AllocCoTaskMem(16);
        Marshal.Copy(Enumerable.Repeat((byte)42, 16).ToArray(), 0, data, 16);
        return new HImage { cols = 4, rows = 4, channels = 1, depth = 8, stride = 4, pData = data };
    }
}

internal sealed class FusionFixture : IDisposable
{
    public string Directory { get; } = Path.Combine(Path.GetTempPath(), "ColorVisionFusionTests", Guid.NewGuid().ToString("N"));
    public FusionFixture() => System.IO.Directory.CreateDirectory(Directory);

    public string Add(string name, PixelFormat? format = null, int width = 4, byte value = 0)
    {
        string path = Path.Combine(Directory, name);
        PixelFormat pixelFormat = format ?? PixelFormats.Gray8;
        int stride = width * pixelFormat.BitsPerPixel / 8;
        BitmapSource image = BitmapSource.Create(width, 4, 96, 96, pixelFormat, null, Enumerable.Repeat(value, stride * 4).ToArray(), stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionFusionTests")) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Directory);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid fixture path.");
        System.IO.Directory.Delete(target, recursive: true);
    }
}
