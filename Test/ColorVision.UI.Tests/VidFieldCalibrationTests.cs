using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.UI.Authorizations;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class VidFieldCalibrationTests
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int ReadNativeVid(int position, [MarshalAs(UnmanagedType.LPStr)] string file, out double distance);

    [Theory]
    [InlineData(1, false)]
    [InlineData(1000, true)]
    public void ExportsAV4MapWithTheReciprocalCorrectionInEitherUnit(int scale, bool millimeters)
    {
        var original = new JObject { ["a"] = 1000, ["b"] = 0, ["c"] = 1000.0 / scale, ["d"] = 0, ["serial"] = "test" };
        double reference = 1.8 * scale;
        var result = VidFieldCalibration.Preview(original.ToString(), [new(1000, reference)], 0, 3000, millimeters);
        var exported = JObject.Parse(result.Json);

        Assert.Equal(VidCorrectionModel.Diopter, result.Model);
        Assert.Equal(2.0 * scale, result.Points[0].Measured, 8);
        Assert.Equal(reference, result.Points[0].Corrected, 8);
        Assert.Equal(1 / 1.8 - 1 / 2.0, result.Parameter, 12);
        Assert.Equal("test", exported.Value<string>("serial"));
        Assert.All(new[] { "a", "b", "c", "d" }, key => Assert.Equal(JTokenType.Float, exported[key]!.Type));
        Assert.Equal(scale / (1 / 2.9 + result.Parameter), VidMappingCoefficients.Evaluate(exported, 1900), 8);
    }

    [Fact]
    public void RetainsTheSplitBoundaryAndAppliesTheSameOffsetOnBothSides()
    {
        const string json = """
            {"label":"VID","VidNewVision":{"positionValue":1000,
                "shortSighted":{"a":1000,"b":0,"c":1,"d":0},
                "longSighted":{"a":1000,"b":0.0001,"c":2,"d":500,"label":"far"}}}
            """;
        var result = VidFieldCalibration.Preview(json, [new(1000, 1800)], 800, 2000, millimeters: true);
        var original = JObject.Parse(json);
        var exported = JObject.Parse(result.Json);
        Assert.Equal(1000, exported["VidNewVision"]!.Value<int>("positionValue"));
        Assert.Equal("far", exported["VidNewVision"]!["longSighted"]!.Value<string>("label"));
        foreach (int position in new[] { 800, 1000, 1001, 2000 })
        {
            double before = VidMappingCoefficients.Evaluate(original, position);
            Assert.Equal(1000 / (1000 / before + result.Parameter), VidMappingCoefficients.Evaluate(exported, position), 8);
        }
    }

    // Each model is recovered from exact references at points on both sides of a discontinuous split,
    // and the exported map reproduces them; a position shift also moves the split boundary.
    [Theory]
    [InlineData(nameof(VidCorrectionModel.Diopter), 0.1)]
    [InlineData(nameof(VidCorrectionModel.Distance), 50)]
    [InlineData(nameof(VidCorrectionModel.Position), 120.5)]
    public void MultiplePointsSelectTheModelThatExplainsThem(string model, double parameter)
    {
        var truth = Enum.Parse<VidCorrectionModel>(model);
        const string json = """
            {"VidNewVision":{"positionValue":1500,
                "shortSighted":{"a":1000,"b":0.0002,"c":1,"d":0},
                "longSighted":{"a":1000,"b":0.0002,"c":1,"d":30}}}
            """;
        var original = JObject.Parse(json);
        double Reference(int position) => truth switch
        {
            VidCorrectionModel.Diopter => 1 / (1 / VidMappingCoefficients.Evaluate(original, position) + parameter / 1000),
            VidCorrectionModel.Distance => VidMappingCoefficients.Evaluate(original, position) + parameter,
            _ => VidMappingCoefficients.Evaluate(original, position + parameter),
        };
        VidCalibrationPoint[] points = [.. new[] { 900, 1400, 1700, 2300 }.Select(position => new VidCalibrationPoint(position, Reference(position)))];

        var result = VidFieldCalibration.Preview(json, points, 800, 2500, millimeters: true);
        var exported = JObject.Parse(result.Json);

        Assert.Equal(truth, result.Model);
        Assert.Equal(parameter, result.Parameter, 6);
        Assert.All(result.Fits, fit => Assert.True(fit.Model == truth ? fit.RmsDiopters < 1e-9 : fit.RmsDiopters > 1e-4, $"{fit.Model}: {fit.RmsDiopters}"));
        foreach (var point in points)
            Assert.Equal(point.Reference, VidMappingCoefficients.Evaluate(exported, point.Position), 6);
        Assert.Equal(truth == VidCorrectionModel.Position ? 1379 : 1500, exported["VidNewVision"]!.Value<int>("positionValue"));
    }

    [Fact]
    public void RejectsAModelWithANonFiniteResidualAtAMeasurementPoint()
    {
        const string json = """{"a":0,"b":0,"c":1,"d":0}""";
        VidCalibrationPoint[] points = [new(1, -1), new(3, 3)];

        Assert.Throws<FormatException>(() => VidFieldCalibration.Preview(json, points, 1, 3,
            millimeters: false, model: VidCorrectionModel.Distance));
        var result = VidFieldCalibration.Preview(json, points, 1, 3, millimeters: false);
        Assert.NotNull(result.Fits.Single(fit => fit.Model == VidCorrectionModel.Distance).Error);
        Assert.All(result.Points, point => Assert.True(double.IsFinite(point.ResidualDiopters)));
    }

    [Fact]
    public void PreviewFlagsAnInfinityPointInTheTemplateRange()
    {
        const string json = """{"a":0,"b":0,"c":1,"d":0}""";
        var result = VidFieldCalibration.Preview(json, [new(1000, 2000)], 100, 3000, millimeters: true);
        Assert.True(result.HasInfinityInRange);
        Assert.Equal(2000, result.Points[0].Corrected, 8);
    }

    [Fact]
    public void ValidatesBothBranchesBeforeMeasuring()
    {
        const string json = """{"VidNewVision":{"positionValue":1000,"shortSighted":{"a":0,"b":0,"c":1,"d":0},"longSighted":{"a":0,"b":0,"c":1}}}""";
        Assert.Throws<FormatException>(() => VidFieldCalibration.ValidateSource(json));
    }

    [Theory]
    [InlineData("""{"a":1000,"b":0,"c":1,"d":0}""")]
    [InlineData("""{"VidNewVision":{"positionValue":1000,"shortSighted":{"a":1000,"b":0,"c":1,"d":0},"longSighted":{"a":1000,"b":0.0001,"c":2,"d":500}}}""")]
    public void DeliveredNativeReaderCanReadTheExport(string json)
    {
        string source = Path.GetTempFileName();
        string file = source + ".dat";
        string library = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cvCamera.dll");
        IntPtr handle = IntPtr.Zero;
        var previousAuthorization = Authorization.Instance;
        try
        {
            Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
            File.WriteAllText(source, json, Encoding.Unicode);
            var original = CalibrationJsonFileStore.Load(source);
            var result = VidFieldCalibration.Preview(original.Json, [new(1000, 1800)], 800, 2000, millimeters: true);
            result.SaveAs(file, original);
            Assert.Equal(original.Hash, CalibrationJsonFileStore.Load(source).Hash);
            Assert.Throws<IOException>(() => result.SaveAs(source, original));
            handle = NativeLibrary.Load(library);
            var read = Marshal.GetDelegateForFunctionPointer<ReadNativeVid>(NativeLibrary.GetExport(handle, "CalDistanceExV4"));
            foreach (int position in new[] { 1000, 1001 })
            {
                Assert.Equal(1, read(position, file, out double value));
                Assert.Equal(VidFieldCalibration.ReadCurrent(result.Json, position), value, 8);
            }
        }
        finally
        {
            Authorization.Instance = previousAuthorization;
            if (handle != IntPtr.Zero) NativeLibrary.Free(handle);
            File.Delete(file);
            File.Delete(source);
        }
    }
}
