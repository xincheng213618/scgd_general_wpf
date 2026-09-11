using ColorVision.Engine.Media;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using ColorVision.Engine.Services.PhyCameras.Group;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LumFourColorFileCompatibilityTests
{
    // Matrix from calibration_pro/matlab/测试用标定文件.dat. Expected single-point
    // output was obtained by executing calibration_pro/main.py run_sg_adv (NumPy).
    private static readonly double[] SourceMatrix =
    [
        1.4289631086138899, 0.034884891878598577, 0.092724492365807987,
        0.026743159303587245, 1.2484298273177825, 0.0086786768406476311,
        -0.00026791999675675242, -0.0031857374309407324, 3.0880869190132545,
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SinglePointMatchesExecutedPythonForBothFileFormats(bool pa)
    {
        WithSource(CreateSource(pa), (path, _) =>
        {
            var source = LumFourColorSourceSnapshot.Load(path);
            var result = LumFourColorCorrectionCalculator.CorrectSinglePoint(source.Config, new(
                new(1237.00537109375, 0.194746896624565, 0.875731885433197), new(1324, 0.218552, 0.742315)));
            AssertMatrix(result,
            [
                2.0583802328874237, 0.03727646647171107, -0.07046040802772784,
                0.03852275131781405, 1.3340173953311067, -0.0065948391382977785,
                -0.0003859310446819755, -0.0034041393892062077, -2.3466061532087505,
            ]);
            Assert.Equal(pa ? CalibrationType.LumMultiColor : CalibrationType.LumFourColor, source.CalibrationFile.CalibrationType);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BothModesSaveOriginalFormatAndMetadataWithoutChangingSource(bool pa, bool rgbw)
    {
        JObject original = CreateSource(pa);
        WithSource(original, (path, destination) =>
        {
            string originalText = File.ReadAllText(path);
            var snapshot = LumFourColorSourceSnapshot.Load(path);
            var result = rgbw
                ? LumFourColorCorrectionCalculator.CorrectFourColor(snapshot.Config, Measurements())
                : LumFourColorCorrectionCalculator.CorrectSinglePoint(snapshot.Config, new(new(2, .2, .3), new(4, .2, .3)));
            snapshot.SaveCopy(destination, result);
            var loaded = LumFourColorSourceSnapshot.Load(destination);
            AssertMatrix(loaded.Config, Matrix(result));
            Assert.Equal(snapshot.CalibrationFile.CalibrationType, loaded.CalibrationFile.CalibrationType);
            Assert.Equal(originalText, File.ReadAllText(path));

            JObject saved = JObject.Parse(File.ReadAllText(destination));
            Assert.Equal(original.Properties().Select(p => p.Name), saved.Properties().Select(p => p.Name));
            foreach (var property in original.Properties().Where(p => pa ? p.Name != "pa" : !"abcdefghi".Contains(p.Name, StringComparison.Ordinal)))
                Assert.True(JToken.DeepEquals(property.Value, saved[property.Name]), property.Name);
            Assert.Equal(pa, saved.ContainsKey("pa"));
            Assert.Equal(!pa, saved.ContainsKey("a"));

            // Repeated saves use the original document; ratios never accumulate.
            string firstSaved = File.ReadAllText(destination);
            snapshot.SaveCopy(destination, result);
            Assert.Equal(firstSaved, File.ReadAllText(destination));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RgbwMatchesExecutedMatlabAndUsesOriginalMatrixAndWhiteLuminance(bool multiColor)
    {
        WithSource(CreateSource(multiColor), (path, _) =>
        {
            var pa = LumFourColorSourceSnapshot.Load(path);
            LumFourColorCorrectionMeasurements measurements = Measurements();
            var result = LumFourColorCorrectionCalculator.CorrectFourColor(pa.Config, measurements);
            // Executed the supplied MATLAB solveFourColorCal with the same RGBW measurements.
            AssertMatrix(result,
            [
                1.0117830453313152, -.0792292563629565, .15349540225513708,
                -.0016949652932371332, .893269111147705, .022592983843453044,
                .10415761828633169, -.21017934427856971, 2.6246770080677657,
            ]);
            var doubleWhite = LumFourColorCorrectionCalculator.CorrectFourColor(pa.Config, measurements with
            {
                White = measurements.White with { Reference = measurements.White.Reference with { Y = measurements.White.Reference.Y * 2 } },
            });
            AssertMatrix(doubleWhite, Matrix(result).Select(value => value * 2).ToArray());
            var doubleRed = LumFourColorCorrectionCalculator.CorrectFourColor(pa.Config, measurements with
            {
                Red = measurements.Red with { Reference = measurements.Red.Reference with { Y = measurements.Red.Reference.Y * 2 } },
            });
            AssertMatrix(doubleRed, Matrix(result));

            // Each sample must meet its reference chromaticity, and W sets brightness.
            foreach (var sample in new[] { measurements.Red, measurements.Green, measurements.Blue, measurements.White })
            {
                // Recover the response independently with Cramer's rule.
                double[] raw = InvertResponse(SourceMatrix, Xyz(sample.Camera));
                double[] matrix = Matrix(result);
                double[] xyz = Enumerable.Range(0, 3).Select(row => Enumerable.Range(0, 3).Sum(col => matrix[row * 3 + col] * raw[col])).ToArray();
                double sum = xyz.Sum();
                Assert.Equal(sample.Reference.CieX, xyz[0] / sum, 10);
                Assert.Equal(sample.Reference.CieY, xyz[1] / sum, 10);
                if (sample == measurements.White) Assert.Equal(sample.Reference.Y, xyz[1], 9);
            }
        });
    }

    [Theory]
    [InlineData("short-pa")]
    [InlineData("long-pa")]
    [InlineData("nested-pa")]
    [InlineData("nonfinite-pa")]
    [InlineData("string-pa")]
    [InlineData("missing-gain")]
    [InlineData("short-gain")]
    [InlineData("zero-gain")]
    [InlineData("nonfinite-gain")]
    [InlineData("mixed-matrices")]
    public void InvalidPaFilesCannotBecomeUsableSnapshots(string fault)
    {
        JObject source = CreateSource(true);
        switch (fault)
        {
            case "short-pa": ((JArray)source["pa"]!).RemoveAt(8); break;
            case "long-pa": ((JArray)source["pa"]!).Add(1); break;
            case "nested-pa": source["pa"]![0] = new JArray(1, 2, 3); break;
            case "nonfinite-pa": source["pa"]![0] = double.NaN; break;
            case "string-pa": source["pa"]![0] = "1,234"; break;
            case "missing-gain": source.Remove("Gain"); break;
            case "short-gain": source["Gain"] = new JArray(1, 1); break;
            case "zero-gain": source["Gain"]![0] = 0; break;
            case "nonfinite-gain": source["Gain"]![0] = double.PositiveInfinity; break;
            case "mixed-matrices": source["a"] = 1; break;
        }
        WithSource(source, (path, _) => Assert.Throws<InvalidOperationException>(() => LumFourColorSourceSnapshot.Load(path)));
    }

    [Theory]
    [InlineData("{\"a\":1,\"a\":2}")]
    [InlineData("a : , a , 1")]
    public void DuplicateKeysAndUnverifiedLegacyTextAreRejected(string text)
    {
        WithSource(CreateSource(false), (path, _) =>
        {
            File.WriteAllText(path, text);
            Assert.Throws<InvalidOperationException>(() => LumFourColorSourceSnapshot.Load(path));
        });
    }

    [Fact]
    public void PaSnapshotPreservesSaveGuardsAndDoesNotChangeManualRawContract()
    {
        WithSource(CreateSource(true), (path, destination) =>
        {
            var source = LumFourColorSourceSnapshot.Load(path);
            Assert.False(CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(path, out _, out _));
            Assert.Throws<InvalidOperationException>(() => source.SaveCopy(path, source.Config));
            File.WriteAllText(destination, "existing destination");
            source.Config.A = double.PositiveInfinity;
            Assert.Throws<InvalidOperationException>(() => source.SaveCopy(destination, source.Config));
            Assert.Equal("existing destination", File.ReadAllText(destination));
            source.Config.A = SourceMatrix[0];
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidOperationException>(() => source.SaveCopy(destination, source.Config));
            Assert.Equal("existing destination", File.ReadAllText(destination));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveTemplateWithMissingFileNeverFallsBackToInactiveMatrix(bool multi)
    {
        var template = new CalibrationParam();
        var active = multi ? template.Color.LumMultiColor : template.Color.LumFourColor;
        var inactive = multi ? template.Color.LumFourColor : template.Color.LumMultiColor;
        inactive.FilePath = "inactive-matrix.dat";
        active.IsSelected = true;
        Assert.Null(LumFourColorCalibrationWorkflowWindow.ResolveTemplateSource(null!, template));
    }

    private static JObject CreateSource(bool pa)
    {
        JObject source = new() { ["bpp"] = 16, ["device"] = "format-test", ["extra"] = new JObject { ["revision"] = 3 } };
        if (pa)
        {
            source["Gain"] = new JArray(1.25, 2.5, 3.75, 4, 5, 6, 7, 8, 9);
            source["pa"] = new JArray(SourceMatrix);
        }
        else
        {
            source["Gain_x"] = 1.25; source["Gain_y"] = 2.5; source["Gain_z"] = 3.75;
            source["Texp_x"] = 10; source["Texp_y"] = 20; source["Texp_z"] = 30;
            for (int index = 0; index < 9; index++) source[((char)('a' + index)).ToString()] = SourceMatrix[index];
        }
        return source;
    }

    private static LumFourColorCorrectionMeasurements Measurements() => new(
        new(new(126.78367, .6933417, .30602154), new(89.2225, .6887435, .3085066)),
        new(new(720.7425, .18320304, .62499464), new(494.32455, .13895464, .7421502)),
        new(new(46.2292, .1433485, .03912296), new(49.398224, .14334978, .036007576)),
        new(new(300.52304, .3194248, .3445272), new(212.77502, .29537222, .34703013)));

    private static double[] Matrix(CVRawManualCieConfig c) => [c.A, c.B, c.C, c.D, c.E, c.F, c.G, c.H, c.I];
    private static double[] Xyz(ColorCorrectionYxy c) => [c.Y * c.CieX / c.CieY, c.Y, c.Y * (1 - c.CieX - c.CieY) / c.CieY];
    private static double Determinant(double[] m) => m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
    private static double[] InvertResponse(double[] matrix, double[] xyz) => Enumerable.Range(0, 3).Select(column =>
    {
        double[] replaced = (double[])matrix.Clone();
        for (int row = 0; row < 3; row++) replaced[row * 3 + column] = xyz[row];
        return Determinant(replaced) / Determinant(matrix);
    }).ToArray();

    private static void AssertMatrix(CVRawManualCieConfig actual, double[] expected)
    {
        double[] matrix = Matrix(actual);
        for (int index = 0; index < 9; index++) Assert.Equal(expected[index], matrix[index], 10);
    }

    private static void WithSource(JObject source, Action<string, string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), $"lum-format-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "source.dat"), destination = Path.Combine(root, "corrected.dat");
        File.WriteAllText(path, source.ToString());
        try { test(path, destination); }
        finally { Directory.Delete(root, true); }
    }
}
