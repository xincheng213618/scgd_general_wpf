using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Xunit.Abstractions;

namespace Conoscope.Tests;

public sealed class CalibratedRawDocumentTests
{
    private readonly ITestOutputHelper output;
    public CalibratedRawDocumentTests(ITestOutputHelper output) => this.output = output;
    private static readonly ConoscopePreprocessOptions NoPreprocess = new(false, 0.000001f, false,
        new DustRemovalOptions(DustRemovalMode.DarkSpot, 12, 1, 500, 3), new ImageFilterOptions(ImageFilterType.None, 1, 1, 1, 1, 1));

    [Theory]
    [InlineData(8, 0, true, 1)]
    [InlineData(16, 0, false, 2)]
    [InlineData(8, 1, false, 3)]
    [InlineData(16, 1, true, 2)]
    public async Task CalibratedRawMatchesEmbeddedXyzAnalysisAndExports(int bpp, int kind, bool interleaved, uint version)
    {
        string rawPath = TemporaryFile("CVRAW"), ciePath = TemporaryFile("cvcie"), exportPath = TemporaryFile("csv");
        try
        {
            using CVCIEFile raw = WriteRaw(rawPath, bpp, version);
            RawColorTransformV1 transform = Transform(kind, interleaved);
            SaveCalibration(rawPath, raw, transform);
            byte[] original = File.ReadAllBytes(rawPath);
            float[] expected = TransformAll(raw, transform);
            using CVCIEFile cie = new()
            {
                Version = 2, Rows = raw.Rows, Cols = raw.Cols, Bpp = 32, Channels = 3,
                Exp = [11, 22, 33], Data = expected.SelectMany(BitConverter.GetBytes).ToArray()
            };
            Assert.True(CVFileUtil.WriteCIEFile(ciePath, cie));
            using var document = NewDocument();
            using var reference = NewDocument();
            var states = new List<(ConoscopeDocumentChangeKind, bool)>();
            bool sourceLockedDuringLoad = false;
            document.Changed += (_, e) =>
            {
                states.Add((e.Kind, document.HasXyzData));
                // Record the result instead of throwing inside the isolated observer.
                try { using var write = File.Open(rawPath, FileMode.Open, FileAccess.Write, FileShare.Read); }
                catch (IOException) { sourceLockedDuringLoad = true; }
            };
            Assert.True(ConoscopeDocument.CanOpenFile(rawPath));
            Assert.True(ConoscopeDocument.CanOpenFile(ciePath));
            await document.OpenAsync(rawPath, null, NoPreprocess, false);
            await reference.OpenAsync(ciePath, null, NoPreprocess, false);
            Assert.Null(document.LoadError);
            Assert.Null(reference.LoadError);
            Assert.True(document.HasXyzData);
            Assert.Equal("11,22,33", document.ExposureSummary);
            Assert.Equal(new[] { (ConoscopeDocumentChangeKind.InitialDisplayReady, false), (ConoscopeDocumentChangeKind.DeferredChannelsReady, true) }, states);
            Assert.True(sourceLockedDuringLoad);
            AssertMatsEqual(document, reference);
            var curves = ConoscopeAngularAnalysis.Analyze(Context(document));
            var expectedCurves = ConoscopeAngularAnalysis.Analyze(Context(reference));
            for (int i = 0; i < curves.Count; i++) Assert.Equal(expectedCurves[i].Values, curves[i].Values);
            foreach (ExportChannel channel in new[] { ExportChannel.Y, ExportChannel.CieX, ExportChannel.CieY })
            {
                var options = new ConoscopeCrossSectionExportOptions { IncludeMetadata = false, DecimalPlaces = 8 };
                ConoscopeExportService.ExportAzimuthCrossSection(exportPath, channel, Context(document), 37, options);
                byte[] actual = File.ReadAllBytes(exportPath);
                ConoscopeExportService.ExportAzimuthCrossSection(exportPath, channel, Context(reference), 37, options);
                Assert.Equal(actual, File.ReadAllBytes(exportPath));
            }
            document.Reload(NoPreprocess);
            AssertMatsEqual(document, reference);
            Assert.Equal(original, File.ReadAllBytes(rawPath));
            using var writableAfterLoad = File.Open(rawPath, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally { File.Delete(rawPath); File.Delete(ciePath); File.Delete(exportPath); }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("not-replayable")]
    [InlineData("dimensions")]
    [InlineData("raw-depth")]
    [InlineData("coefficients")]
    [InlineData("version")]
    [InlineData("corrupt")]
    [InlineData("monochrome")]
    public async Task InvalidOrIncompleteCalibrationNeverPublishesRawRgbAsXyz(string invalid)
    {
        string path = TemporaryFile("cvraw");
        try
        {
            using CVCIEFile raw = WriteRaw(path, channels: invalid == "monochrome" ? 1 : 3);
            if (invalid != "missing")
                SaveCalibration(path, raw, Transform(invalid == "monochrome" ? 2 : 0, true), json =>
                {
                    if (invalid == "not-replayable") json["CanReplay"] = false;
                    if (invalid == "dimensions") json["Width"] = raw.Cols + 1;
                    if (invalid == "raw-depth") json["RawBpp"] = 8;
                    if (invalid == "coefficients") json["Coefficients"] = new JArray(1, 2);
                }, invalid == "version" ? 2u : 1u);
            if (invalid == "corrupt")
            {
                using var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
                file.Position = file.Length - 1;
                int value = file.ReadByte();
                file.Position--;
                file.WriteByte((byte)(value ^ 1));
            }
            using var document = NewDocument();
            Assert.False(ConoscopeDocument.CanOpenFile(path));
            await document.OpenAsync(path, null, NoPreprocess, false);
            Assert.NotNull(document.LoadError);
            Assert.False(document.HasDisplayData);
            Assert.False(document.HasXyzData);
            Assert.True(document.CanRetryLoad);
            // The failed reader releases its lease so repair and retry are possible.
            using CVCIEFile repaired = WriteRaw(path);
            SaveCalibration(path, repaired, Transform(0, true));
            await document.RetryLoadAsync();
            Assert.Null(document.LoadError);
            Assert.True(document.HasXyzData);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CanceledRawLoadCannotPublishDeferredDataOverANewerDocument()
    {
        string firstPath = TemporaryFile("cvraw"), secondPath = TemporaryFile("cvraw");
        try
        {
            using var first = WriteRaw(firstPath);
            using var second = WriteRaw(secondPath);
            SaveCalibration(firstPath, first, Transform(0, true));
            SaveCalibration(secondPath, second, Transform(1, false));
            using var document = NewDocument();
            Task? newer = null;
            var deferredFiles = new List<string>();
            document.Changed += (_, e) =>
            {
                if (e.Kind == ConoscopeDocumentChangeKind.InitialDisplayReady && document.FileName == firstPath)
                    newer = document.OpenAsync(secondPath, null, NoPreprocess, false);
                if (e.Kind == ConoscopeDocumentChangeKind.DeferredChannelsReady) deferredFiles.Add(document.FileName);
            };
            await document.OpenAsync(firstPath, null, NoPreprocess, false);
            Assert.NotNull(newer);
            await newer;
            Assert.Null(document.LoadError);
            Assert.Equal(new[] { secondPath }, deferredFiles);
            Assert.Equal(TransformAll(second, Transform(1, false))[second.Rows * second.Cols], document.Y!.At<float>(0, 0));
            using var reader = new CalibratedRawFileReader(firstPath);
            Assert.Throws<OperationCanceledException>(() => reader.ReadChannel(1, new CancellationToken(true)));
            reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => reader.ReadChannel(1));
            using var write = File.Open(firstPath, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally { File.Delete(firstPath); File.Delete(secondPath); }
    }

    private static ConoscopeDocument NewDocument() => new(LogManager.GetLogger(typeof(CalibratedRawDocumentTests)));
    private static string TemporaryFile(string extension) => Path.Combine(Path.GetTempPath(), $"conoscope-raw-{Guid.NewGuid():N}.{extension}");

    private static CVCIEFile WriteRaw(string path, int bpp = 16, uint version = 2, int channels = 3)
    {
        byte[] data = bpp == 8 ? Enumerable.Range(0, 21 * 21 * channels).Select(i => (byte)(i % 200 + 1)).ToArray()
            : Enumerable.Range(0, 21 * 21 * channels).SelectMany(i => BitConverter.GetBytes((ushort)(i * 17 % 60000 + 1))).ToArray();
        CVCIEFile raw = new() { Version = version, Rows = 21, Cols = 21, Bpp = bpp, Channels = channels, Exp = Enumerable.Repeat(1f, channels).ToArray(), Data = data };
        if (version == 3)
        {
            // The production writer emits the v1/v2 layout; construct a real v3 header here.
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(Encoding.ASCII.GetBytes("CVCIE")); writer.Write(3u); writer.Write(0); writer.Write(0);
            writer.Write(0f); writer.Write(channels);
            foreach (float exposure in raw.Exp) writer.Write(exposure);
            writer.Write(raw.Cols); writer.Write(raw.Rows); writer.Write(bpp); writer.Write(data.Length); writer.Write(data);
        }
        else Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        return raw;
    }

    private static RawColorTransformV1 Transform(int kind, bool interleaved)
    {
        var transform = RawColorTransformV1.Create();
        transform.Kind = kind; transform.Channels = kind == 2 ? 1 : 3; transform.InterleavedBgr = interleaved ? 1 : 0;
        transform.Coefficients = [0.123456789, -0.025, 0.007, 0.009, 0.22, -0.05, 0.03, 0.009, 0.32];
        return transform;
    }

    private static void SaveCalibration(string path, CVCIEFile raw, RawColorTransformV1 transform, Action<JObject>? edit = null, uint version = 1)
    {
        var json = JObject.FromObject(new
        {
            Width = raw.Cols, Height = raw.Rows, RawBpp = raw.Bpp, raw.Channels, transform.CalibrationType,
            TransformKind = transform.Kind, InterleavedBgr = transform.InterleavedBgr != 0, transform.Coefficients,
            Exposure = new float[] { 11, 22, 33 }, Template = "synthetic", CanReplay = true,
        });
        edit?.Invoke(json);
        CVFileMetadata.SetProperty(path, "colorvision.calibration.color", version, Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
    }

    private static float[] TransformAll(CVCIEFile raw, RawColorTransformV1 transform)
    {
        float[] result = new float[raw.Cols * raw.Rows * raw.Channels];
        GCHandle input = GCHandle.Alloc(raw.Data, GCHandleType.Pinned), output = GCHandle.Alloc(result, GCHandleType.Pinned);
        try
        {
            Assert.Equal(OpenCVCalibration.PoiOk, OpenCVMediaHelper.M_TransformRawColorV1(raw.Cols, raw.Rows, raw.Bpp,
                input.AddrOfPinnedObject(), (ulong)raw.Data.Length, in transform, -1, output.AddrOfPinnedObject(), (ulong)result.Length));
        }
        finally { input.Free(); output.Free(); }
        return result;
    }

    private static void AssertMatsEqual(ConoscopeDocument actual, ConoscopeDocument expected)
    {
        foreach (var pair in new[] { (actual.X!, expected.X!), (actual.Y!, expected.Y!), (actual.Z!, expected.Z!) })
            Assert.Equal(0, Cv2.Norm(pair.Item1, pair.Item2, NormTypes.INF));
    }

    private static ConoscopeExportContext Context(ConoscopeDocument source) => new()
    {
        ModelName = "Synthetic", ImageWidth = source.Y!.Cols, ImageHeight = source.Y.Rows,
        Center = new System.Windows.Point(10, 10), MaxAngle = 3, PixelsPerDegree = 2,
        ReadXyz = (x, y) => new(source.X!.At<float>(y, x), source.Y.At<float>(y, x), source.Z!.At<float>(y, x)),
    };

    [LocalRawSampleFact]
    public async Task RealCalibratedRawLoadsFullXyzAndMatchesIndependentPixelReplay()
    {
        string path = Environment.GetEnvironmentVariable("CONOSCOPE_RAW_SAMPLE")!;
        string before = HashFile(path);
        Assert.True(ConoscopeDocument.CanOpenFile(path));
        using var document = NewDocument();
        double firstDisplay = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        document.Changed += (_, e) => { if (e.Kind == ConoscopeDocumentChangeKind.InitialDisplayReady) firstDisplay = watch.Elapsed.TotalMilliseconds; };
        await document.OpenAsync(path, null, NoPreprocess, false);
        Assert.Null(document.LoadError);
        Assert.True(document.HasXyzData);
        double complete = watch.Elapsed.TotalMilliseconds;
        int headerEnd = CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header);
        using (header)
        using (var stream = File.OpenRead(path))
        using (var reader = new BinaryReader(stream))
        {
            var json = JObject.Parse(Encoding.UTF8.GetString(CVFileMetadata.Read(path)["colorvision.calibration.color"].Value));
            var transform = RawColorTransformV1.Create();
            transform.Kind = json["TransformKind"]!.Value<int>(); transform.Channels = 3;
            transform.CalibrationType = json["CalibrationType"]!.Value<int>();
            transform.InterleavedBgr = json["InterleavedBgr"]!.Value<bool>() ? 1 : 0;
            transform.Coefficients = json["Coefficients"]!.ToObject<double[]>()!;
            long payload = headerEnd + (header.Version == 2 ? 8 : 4);
            int bytes = header.Bpp / 8;
            var samples = new List<object>();
            foreach (int y in new[] { 0, header.Rows / 2, header.Rows - 1 })
                foreach (int x in new[] { 0, header.Cols / 2, header.Cols - 1 })
                {
                    long pixel = (long)y * header.Cols + x;
                    byte[] data = new byte[3 * bytes];
                    for (int c = 0; c < 3; c++)
                    {
                        long element = transform.InterleavedBgr != 0 ? pixel * 3 + c : c * (long)header.Rows * header.Cols + pixel;
                        stream.Position = payload + element * bytes;
                        reader.ReadBytes(bytes).CopyTo(data, c * bytes);
                    }
                    using CVCIEFile one = new() { Rows = 1, Cols = 1, Bpp = header.Bpp, Channels = 3, Data = data };
                    float[] expected = TransformAll(one, transform);
                    float[] actual = [document.X!.At<float>(y, x), document.Y!.At<float>(y, x), document.Z!.At<float>(y, x)];
                    Assert.Equal(expected, actual);
                    samples.Add(new { x, y, XYZ = actual });
                }
            Assert.Equal(before, HashFile(path));
            string evidence = JsonConvert.SerializeObject(new { Width = header.Cols, Height = header.Rows, Sha256 = before,
                YReadyMs = firstDisplay, XyzReadyMs = complete, Samples = samples }, Formatting.Indented);
            output.WriteLine(evidence);
            string? directory = Environment.GetEnvironmentVariable("CONOSCOPE_ANALYSIS_OUTPUT");
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "raw-validation.json"), evidence);
            }
        }
    }

    private static string HashFile(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    public sealed class LocalRawSampleFactAttribute : FactAttribute
    {
        public LocalRawSampleFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONOSCOPE_RAW_SAMPLE")))
                Skip = "Set CONOSCOPE_RAW_SAMPLE to run read-only calibrated CVRAW validation.";
        }
    }
}
