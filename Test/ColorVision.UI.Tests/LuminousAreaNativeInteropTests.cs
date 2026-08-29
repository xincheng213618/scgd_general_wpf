using ColorVision.Core;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class LuminousAreaNativeInteropCollection
{
    public const string CollectionName = "Luminous area native interop";
}

public sealed class NativeV2FactAttribute : FactAttribute
{
    public const string OptInVariable = "COLORVISION_RUN_LUMINOUS_NATIVE_V2_TESTS";

    public NativeV2FactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal))
        {
            Skip = $"Set {OptInVariable}=1 to exercise the real M_FindLuminousAreaV2 export with synthetic HImage data.";
        }
    }
}

[Collection(LuminousAreaNativeInteropCollection.CollectionName)]
[Trait("Category", "NativeIntegration")]
public sealed class LuminousAreaNativeInteropTests
{
    [Fact]
    public void V2BindingUsesTheExpectedCdeclUtf8Abi()
    {
        MethodInfo method = typeof(OpenCVMediaHelper).GetMethod(
            nameof(OpenCVMediaHelper.M_FindLuminousAreaV2),
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(OpenCVMediaHelper), nameof(OpenCVMediaHelper.M_FindLuminousAreaV2));
        DllImportAttribute import = method.GetCustomAttribute<DllImportAttribute>()
            ?? throw new InvalidOperationException("M_FindLuminousAreaV2 must remain a native binding.");
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(CallingConvention.Cdecl, import.CallingConvention);
        Assert.EndsWith("opencv_helper.dll", import.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(typeof(HImage), parameters[0].ParameterType);
        Assert.Equal(typeof(RoiRect), parameters[1].ParameterType);
        Assert.Equal(UnmanagedType.LPUTF8Str, parameters[2].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.Equal(typeof(IntPtr).MakeByRefType(), parameters[3].ParameterType);
        Assert.True(parameters[3].IsOut);
    }

    [NativeV2Fact]
    public void RealV2ExportReturnsRoiLocalCornersAndItsBufferCanBeFreed()
    {
        using LuminousFixture fixture = LuminousFixture.Create();
        IntPtr resultPointer = IntPtr.Zero;
        string json;
        try
        {
            int returnCode = OpenCVMediaHelper.M_FindLuminousAreaV2(
                fixture.Image,
                fixture.Region,
                "{\"MinConfidence\":0.2}",
                out resultPointer);

            Assert.True(returnCode > 0, $"Native return code: {returnCode}");
            Assert.NotEqual(IntPtr.Zero, resultPointer);
            json = Marshal.PtrToStringUTF8(resultPointer) ?? string.Empty;
            int freeResult = OpenCVMediaHelper.FreeResult(resultPointer);
            resultPointer = IntPtr.Zero;
            Assert.Equal(0, freeResult);
        }
        finally
        {
            if (resultPointer != IntPtr.Zero)
            {
                _ = OpenCVMediaHelper.FreeResult(resultPointer);
            }
        }

        Assert.True(LuminousAreaResultParser.TryParseV2(
            json,
            out LuminousAreaDetectionResult parsed,
            out string parseError), $"{parseError} JSON={json}");
        Assert.True(parsed.Success, Describe(parsed));
        AssertCornersNear(parsed.Corners, fixture.ExpectedLocalCorners, tolerance: 14);
    }

    [NativeV2Fact]
    public void ManagedV2WrapperOffsetsRoiAndPreservesRejectedDiagnosticCorners()
    {
        using LuminousFixture fixture = LuminousFixture.Create();

        LuminousAreaDetectionResult accepted = LuminousAreaNative.DetectV2(
            fixture.Image, fixture.Region, minConfidence: 0.2);
        Assert.True(accepted.Success, Describe(accepted));
        Assert.True(accepted.NativeReturnCode > 0);
        AssertCornersNear(accepted.Corners, fixture.ExpectedFullImageCorners, tolerance: 14);

        LuminousAreaDetectionResult rejected = LuminousAreaNative.DetectV2(
            fixture.Image, fixture.Region, minConfidence: 1.0);
        Assert.False(rejected.Success);
        Assert.Equal("LowConfidence", rejected.FailureReason);
        Assert.Equal(4, rejected.Corners.Count);
        AssertCornersNear(rejected.Corners, fixture.ExpectedFullImageCorners, tolerance: 14);
    }

    [NativeV2Fact]
    public void UniformImageFailureIsConsistentAcrossImageEditorAndPoiDefaults()
    {
        using PinnedUShortImage image = PinnedUShortImage.CreateUniform(480, 360, 2400);
        GraphicEditingConfig editor = new();
        PoiConfig poi = new();
        FindLuminousAreaCorner[] configurations =
        [
            editor.FindLuminousArea,
            editor.FindLuminousAreaCorner,
            poi.FindLuminousArea,
            poi.FindLuminousAreaCorner
        ];

        LuminousAreaDetectionResult[] results = configurations
            .Select(config => LuminousAreaDetector.Detect(image.Image, default, config))
            .ToArray();

        Assert.All(results, result =>
        {
            Assert.False(result.Success);
            Assert.False(result.HasValidCorners);
            Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
            Assert.False(string.IsNullOrWhiteSpace(result.RawJson));
            Assert.True(result.NativeReturnCode > 0);
        });
        Assert.Single(results.Select(result => result.FailureReason).Distinct(StringComparer.Ordinal));
        Assert.Single(results.Select(LuminousAreaDetector.GetFailureMessage).Distinct(StringComparer.Ordinal));
    }

    private static void AssertCornersNear(
        IReadOnlyList<LuminousAreaPoint> actual,
        IReadOnlyList<LuminousAreaPoint> expected,
        double tolerance)
    {
        Assert.Equal(4, actual.Count);
        Assert.Equal(4, expected.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            double distance = Distance(actual[index].X - expected[index].X, actual[index].Y - expected[index].Y);
            Assert.True(distance <= tolerance,
                $"Corner {index} was ({actual[index].X:F2},{actual[index].Y:F2}); " +
                $"expected ({expected[index].X:F2},{expected[index].Y:F2}) within {tolerance:F2}px.");
        }
    }

    private static string Describe(LuminousAreaDetectionResult result) =>
        $"FailureReason={result.FailureReason}; Confidence={result.Confidence}; " +
        $"Diagnostic={result.Diagnostic}; NativeReturnCode={result.NativeReturnCode}; JSON={result.RawJson}";

    private sealed class LuminousFixture : IDisposable
    {
        private const int LocalWidth = 920;
        private const int LocalHeight = 680;
        private const int OffsetX = 87;
        private const int OffsetY = 61;
        private readonly PinnedUShortImage image;

        private LuminousFixture(PinnedUShortImage image)
        {
            this.image = image;
        }

        public HImage Image => image.Image;

        public RoiRect Region { get; } = new(OffsetX, OffsetY, LocalWidth, LocalHeight);

        public IReadOnlyList<LuminousAreaPoint> ExpectedLocalCorners { get; } =
        [
            new(153, 126),
            new(746, 96),
            new(779, 526),
            new(119, 558)
        ];

        public IReadOnlyList<LuminousAreaPoint> ExpectedFullImageCorners => ExpectedLocalCorners
            .Select(point => new LuminousAreaPoint(point.X + OffsetX, point.Y + OffsetY))
            .ToArray();

        public static LuminousFixture Create()
        {
            const int canvasWidth = 1120;
            const int canvasHeight = 820;
            LuminousAreaPoint[] corners =
            [
                new(153, 126),
                new(746, 96),
                new(779, 526),
                new(119, 558)
            ];
            ushort[] pixels = new ushort[canvasWidth * canvasHeight];
            Array.Fill(pixels, (ushort)700);
            double diagonal = Distance(LocalWidth, LocalHeight);

            for (int y = 0; y < LocalHeight; y++)
            {
                for (int x = 0; x < LocalWidth; x++)
                {
                    double value = 900 + 0.18 * x + 0.11 * y;
                    if (IsInsideConvexPolygon(x + 0.5, y + 0.5, corners))
                    {
                        double cornerDistance = Distance(x - corners[0].X, y - corners[0].Y) / diagonal;
                        double vignette = 0.62 + 0.38 * Math.Clamp(cornerDistance / 0.28, 0, 1);
                        double vertical = 0.90 + 0.10 * Math.Cos((y - LocalHeight * 0.5) / LocalHeight * Math.PI);
                        value = 900 + 44000 * vignette * vertical;
                    }

                    uint noiseHash = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663));
                    value += (int)(noiseHash & 0xff) - 128;
                    pixels[(y + OffsetY) * canvasWidth + x + OffsetX] =
                        (ushort)Math.Clamp((int)Math.Round(value), ushort.MinValue, ushort.MaxValue);
                }
            }

            return new LuminousFixture(new PinnedUShortImage(canvasWidth, canvasHeight, pixels));
        }

        public void Dispose() => image.Dispose();

        private static bool IsInsideConvexPolygon(
            double x,
            double y,
            IReadOnlyList<LuminousAreaPoint> corners)
        {
            for (int index = 0; index < corners.Count; index++)
            {
                LuminousAreaPoint start = corners[index];
                LuminousAreaPoint end = corners[(index + 1) % corners.Count];
                double cross = (end.X - start.X) * (y - start.Y) - (end.Y - start.Y) * (x - start.X);
                if (cross < 0)
                {
                    return false;
                }
            }
            return true;
        }
    }

    private sealed class PinnedUShortImage : IDisposable
    {
        private GCHandle handle;

        public PinnedUShortImage(int width, int height, ushort[] pixels)
        {
            if (pixels.Length != checked(width * height))
            {
                throw new ArgumentException("Pixel count does not match image dimensions.", nameof(pixels));
            }
            handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            Image = new HImage
            {
                rows = height,
                cols = width,
                channels = 1,
                depth = 16,
                stride = checked(width * sizeof(ushort)),
                isDispose = true,
                pData = handle.AddrOfPinnedObject()
            };
        }

        public HImage Image { get; }

        public static PinnedUShortImage CreateUniform(int width, int height, ushort value)
        {
            ushort[] pixels = new ushort[checked(width * height)];
            Array.Fill(pixels, value);
            return new PinnedUShortImage(width, height, pixels);
        }

        public void Dispose()
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    private static double Distance(double x, double y) => Math.Sqrt(x * x + y * y);
}
