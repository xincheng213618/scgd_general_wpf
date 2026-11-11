using ColorVision.Engine.FlowProcessing;
using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Exports.Lux;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.MTF.MTFHV058;
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.Uniformity;
using ProjectARVRPro.Process.W255;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class LuxCustomerCsvExporterTests
{
    [Theory]
    [InlineData(LuxCsvFormat.Current, "%", false, "P1")]
    [InlineData(LuxCsvFormat.Current, "%", false, "P_1")]
    [InlineData(LuxCsvFormat.Legacy, "", true, "P1")]
    [InlineData(LuxCsvFormat.Legacy, "", true, "P_1")]
    public void WhiteProjectionMatchesTheArchivedNamesUnitsAndFieldSelectionWithoutChangingMeasurements(LuxCsvFormat format, string uniformityUnit, bool includesDiagonal, string pointName)
    {
        var white = new W255TestResult
        {
            PoixyuvDatas = [new() { Name = pointName, Y = 123, x = 0.3, y = 0.4, u = 0.2, v = 0.5 }],
            LuminanceUniformity = Item("new-uniformity-name", 0.8, 0.9, 1),
            CenterLunimance = Item("new-center-name", 123),
            DiagonalFieldOfViewAngle = Item("diagonal", 42),
            CenterCIE1976ChromaticCoordinatesu = Item("u", 0.2)
        };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(new() { W255TestResult = white }, format);
        ObjectiveTestCsvRow uniformity = Assert.Single(rows, row => row.TestItem == "Luminance_Uniformity(min_max_100)");
        Assert.Equal(("80", "90", "100", "fail", uniformityUnit),
            (uniformity.TestValue, uniformity.LowerLimit, uniformity.UpperLimit, uniformity.TestResult, uniformity.Unit));
        Assert.Equal(includesDiagonal, rows.Any(row => row.TestItem == "Diagonal_Field_of_View_Angle"));
        ObjectiveTestCsvRow point = Assert.Single(rows, row => row.TestItem == "P1(Lv)");
        Assert.Equal(("123", "cd/m^2", "pass", "0", "0"), (point.TestValue, point.Unit, point.TestResult, point.LowerLimit, point.UpperLimit));
        ObjectiveTestCsvRow average = Assert.Single(rows, row => row.TestItem == "Average_Luminance");
        Assert.Equal(("123", "pass"), (average.TestValue, average.TestResult));
        foreach (var (name, value) in new[] { ("P1(Cx)", "0.3"), ("P1(Cy)", "0.4"), ("P1(u')", "0.2"), ("P1(v')", "0.5") })
            Assert.Equal(value, Assert.Single(rows, row => row.TestItem == name).TestValue);
        Assert.Contains(rows, row => row.TestItem == "Center_CIE_1976Chromatic_Coordinates_u'" && row.TestValue == "0.2");
        Assert.DoesNotContain(rows, row => row.TestItem == "Center CIE_1976Chromatic_Coordinates_u'");
        Assert.Equal(includesDiagonal ? 57 : 56, rows.Count);
        Assert.DoesNotContain(rows, row => row.TestResult == "None");
        Assert.Equal(pointName, white.PoixyuvDatas[0].Name);
        Assert.Equal(0.8, white.LuminanceUniformity.Value);
        Assert.Equal(0.9, white.LuminanceUniformity.LowLimit);
        Assert.Equal("new-uniformity-name", white.LuminanceUniformity.Name);
    }

    [Fact]
    public void WhiteAverageUsesMeasuredPoiAndDoesNotHideInvalidLuminance()
    {
        var white = new W255TestResult
        {
            PoixyuvDatas = Enumerable.Range(1, 9).Select(index => new PoixyuvData { Name = "P_" + index, Y = index * 100 }).ToList()
        };
        var result = new ObjectiveTestResult { W255TestResult = white };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Legacy);
        Assert.Equal("500", Assert.Single(rows, row => row.TestItem == "Average_Luminance").TestValue);
        for (int index = 1; index <= 9; index++)
            Assert.Equal((index * 100).ToString(), Assert.Single(rows, row => row.TestItem == $"P{index}(Lv)").TestValue);

        white.PoixyuvDatas[8].Y = double.NaN;
        ObjectiveTestCsvRow invalid = Assert.Single(LuxCsvProjector.CollectRows(result, LuxCsvFormat.Legacy), row => row.TestItem == "Average_Luminance");
        Assert.Equal(("", "fail"), (invalid.TestValue, invalid.TestResult));
    }

    [Theory]
    [InlineData(LuxCsvFormat.Current, "P2_horizontal", "")]
    [InlineData(LuxCsvFormat.Legacy, "0.4F_LeftUp_horizontal", "%")]
    public void ArMtfProjectionChangesTheLegacyLabelsWithoutMultiplyingRawMtf(LuxCsvFormat format, string itemName, string unit)
    {
        var result = new ObjectiveTestResult
        {
            MTFHV048TestResults = [new MTFHV048TestResult { MTF_HV_H_LeftUp_0_4F = Item("new-name", 0.53, 0.4, 0) }]
        };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, format, mtfPointFormat: LuxMtfPointFormat.Mtf048);
        Assert.Equal(54, rows.Count);
        ObjectiveTestCsvRow row = Assert.Single(rows, item => item.TestItem == itemName);
        Assert.Equal(("MTF", "0.53", "0.4", unit, "pass"), (row.TestScreen, row.TestValue, row.LowerLimit, row.Unit, row.TestResult));
        Assert.Contains(rows, item => item.TestItem.EndsWith("H1", StringComparison.Ordinal) && item.TestValue == "0" && item.TestResult == "pass");
        Assert.DoesNotContain(rows, item => item.TestResult == "None");
    }

    [Theory]
    [InlineData(true, LuxCsvFormat.Current, LuxMtfPointFormat.Mtf058, "P2", "")]
    [InlineData(true, LuxCsvFormat.Legacy, LuxMtfPointFormat.Mtf058, "0.5F_LeftUp", "%")]
    [InlineData(true, LuxCsvFormat.Legacy, LuxMtfPointFormat.Mtf048, "0.4F_LeftUp", "%")]
    [InlineData(false, LuxCsvFormat.Current, LuxMtfPointFormat.Mtf048, "P2", "")]
    [InlineData(false, LuxCsvFormat.Legacy, LuxMtfPointFormat.Mtf048, "0.4F_LeftUp", "%")]
    [InlineData(false, LuxCsvFormat.Legacy, LuxMtfPointFormat.Mtf058, "0.5F_LeftUp", "%")]
    public void MtfUsesItsOwnProcessPointKeysAndExportsOnlyTheSelectedLuxFields(bool is058, LuxCsvFormat format, LuxMtfPointFormat pointFormat, string prefix, string unit)
    {
        var horizontal = Item("horizontal", 0.58, 0.6, 0);
        var result = new ObjectiveTestResult();
        if (is058)
            result.DynamicMTFHV058TestResults["MTF-A"] = new() { MTF_HV_H_LeftUp_0_5F = horizontal };
        else
            result.MTFHV048TestResults.Add(new() { MTF_HV_H_LeftUp_0_4F = horizontal });
        var snapshot = MtfSnapshot(1, "{\"Name\":\" MTF-A \",\"Key_LeftUp_0_5F\":\"custom-inner\",\"Key_LeftUp_0_4F\":\"custom-inner\"}", new
        {
            MTFDetailViewReslut = new { MTFResult = new { resultChild = new[]
            {
                new { name = "custom-inner", childRects = new[]
                {
                    new { mtfValue = 0.61 }, new { mtfValue = 0.52 }, new { mtfValue = 0.63 }, new { mtfValue = 0.54 }
                } }
            } } }
        });
        if (!is058)
            snapshot.ProcessTypeFullName = typeof(MTFHV048Process).FullName;
        IReadOnlyList<ObjectiveTestCsvRow> rows;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            rows = LuxCsvProjector.CollectRows(result, format, [snapshot], pointFormat);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
        Assert.Equal(54, rows.Count);
        Assert.All(rows, row => Assert.Equal(is058 ? "MTF-A" : "MTF", row.TestScreen));
        foreach (var (component, value) in new[] { ("H1", "0.52"), ("V1", "0.61"), ("H2", "0.54"), ("V2", "0.63") })
        {
            ObjectiveTestCsvRow raw = Assert.Single(rows, row => row.TestItem == prefix + "_" + component);
            Assert.Equal((value, unit, "0", "0", "pass"), (raw.TestValue, raw.Unit, raw.LowerLimit, raw.UpperLimit, raw.TestResult));
        }
        ObjectiveTestCsvRow average = Assert.Single(rows, row => row.TestItem == prefix + "_horizontal");
        Assert.Equal(("0.58", "0.6", "fail"), (average.TestValue, average.LowerLimit, average.TestResult));
        Assert.DoesNotContain(rows, row => row.TestItem.StartsWith("MTF_HV_", StringComparison.Ordinal));
        Assert.Equal(0.58, horizontal.Value);
    }

    [Fact]
    public void Mtf058DoesNotReuseRawComponentsFromAnotherKeyOrAnOlderSnapshot()
    {
        var result = new ObjectiveTestResult();
        result.DynamicMTFHV058TestResults["MTF-A"] = new();
        result.MTFHV058TestResults.Add(new());
        var rawData = new
        {
            MTFDetailViewReslut = new { MTFResult = new { resultChild = new[]
            {
                new { name = "Center_0F", childRects = new[] { new { mtfValue = 0.7 }, new { mtfValue = 0.8 } } }
            } } }
        };
        var older = MtfSnapshot(1, "{\"Name\":\"MTF-A\"}", rawData);
        var other = MtfSnapshot(2, "{\"Name\":\"MTF-B\"}", rawData);
        var unnamed = MtfSnapshot(3, "{}", rawData);
        var latest = MtfSnapshot(4, "{\"Name\":\"MTF-A\"}", new { });
        latest.ViewResultJson = "{";
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Legacy, [older, other, unnamed, latest]);
        Assert.Equal("0", Assert.Single(rows, row => row.TestScreen == "MTF-A" && row.TestItem == "0F_Center_H1").TestValue);
        Assert.Equal("0.8", Assert.Single(rows, row => row.TestScreen == "MTF0581" && row.TestItem == "0F_Center_H1").TestValue);
        Assert.Equal("0", Assert.Single(rows, row => row.TestScreen == "MTF0581" && row.TestItem == "0F_Center_H2").TestValue);
        Assert.DoesNotContain(rows, row => row.TestScreen == "MTF-B");
    }

    [Theory]
    [InlineData("MTF0581", "MTF0581", true)]
    [InlineData("MTF0581", "MTF0581", false)]
    [InlineData("VRMTFH", "MTF", true)]
    [InlineData("VRMTFV", "MTF", true)]
    public void Mtf058MatchesEachSnapshotByItsSourceKeyOrPosition(string key, string namedScreen, bool namedAfterUnnamed)
    {
        var result = new ObjectiveTestResult();
        result.MTFHV058TestResults.Add(new());
        result.DynamicMTFHV058TestResults[key] = new();

        ProjectARVRReuslt unnamed = MtfSnapshot(namedAfterUnnamed ? 1 : 2, "{}", RawComponents(0.2));
        ProjectARVRReuslt named = MtfSnapshot(namedAfterUnnamed ? 2 : 1, JsonConvert.SerializeObject(new { Name = key }), RawComponents(0.8));
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Legacy, [unnamed, named]);

        Assert.Equal(
            [("MTF0581", "0.2"), (namedScreen, "0.8")],
            rows.Where(row => row.TestItem == "0F_Center_H1").Select(row => (row.TestScreen, row.TestValue)));

        static object RawComponents(double h1) => new
        {
            MTFDetailViewReslut = new { MTFResult = new { resultChild = new[]
            {
                new { name = "Center_0F", childRects = new[] { new { mtfValue = 0.1 }, new { mtfValue = h1 } } }
            } } }
        };
    }

    [Fact]
    public void KeyedWhiteAndFovReplaceCompatibilityCopiesInsteadOfExportingThemTwice()
    {
        var result = new ObjectiveTestResult
        {
            W255TestResult = new W255TestResult { PoixyuvDatas = [new() { Name = "P1", Y = 99 }] }
        };
        result.LuminanceChromaticityTestResults["White"] = new()
        {
            PoixyuvDatas = [new() { Name = "P1", Y = 200 }],
            LuminanceUniformity = new() { Name = "Luminance_uniformity(%)", TestValue = "85.0000", Unit = "%", Value = 0.85, LowLimit = 0.8 }
        };
        result.FieldOfViewTestResults["White"] = new() { HorizontalFieldOfViewAngle = Item("Fov", 45) };
        result.LuminanceChromaticityTestResults["Red"] = new() { PoixyuvDatas = [new() { Name = "P1", Y = 300 }] };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Current, luminanceUniformityFormat: UniformityDisplayFormat.Decimal);
        Assert.Equal("200", Assert.Single(rows, row => row.TestScreen == "W255" && row.TestItem == "P1(Lv)").TestValue);
        Assert.Equal("W255,Luminance_Uniformity(min_max_100),0.85,%,0.8,0,pass",
            Assert.Single(rows, row => row.TestScreen == "W255" && row.TestItem == "Luminance_Uniformity(min_max_100)").ToCsvLine());
        Assert.Equal("45", Assert.Single(rows, row => row.TestItem == "Horizontal_Field_Of_View_Angle").TestValue);
        Assert.Contains(rows, row => row.TestScreen == "Red" && row.TestItem == "P1(Lv)" && row.TestValue == "300");
        Assert.DoesNotContain(rows, row => row.TestScreen is "R255" or "G255" or "B255");
    }

    [Theory]
    [InlineData(CustomTestResultOutputProfile.LUX_Current_CSV, LuxMtfPointFormat.Mtf058)]
    [InlineData(CustomTestResultOutputProfile.LUX_Legacy_CSV, LuxMtfPointFormat.Mtf058)]
    [InlineData(CustomTestResultOutputProfile.LUX_Legacy_CSV, LuxMtfPointFormat.Mtf048)]
    public void CustomerEntryWritesOneSanitizedSnFileAndReplacesItOnTheNextCommand(CustomTestResultOutputProfile profile, LuxMtfPointFormat pointFormat)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LuxCustomerExport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var item = Item("OptCenterRotation", 1);
            var context = new ObjectiveTestResultExportContext
            {
                Result = new()
                {
                    W255TestResult = new()
                    {
                        LuminanceUniformity = new() { Value = 1, TestValue = "100.0000", Unit = "%", LowLimit = 0.9, UpLimit = 1 }
                    },
                    OpticCenterTestResult = new() { Key = "OC,\"A\"", OptCenterRotation = item },
                    DistortionTestResult = new()
                    {
                        MaximumTiltDegrees = new() { Name = "MaximumTiltDegrees", Value = 1.505647, Unit = "°" }
                    },
                    MTFHV058TestResults = [new() { MTF_HV_H_LeftUp_0_5F = Item("mtf", 0.58) }]
                },
                SerialNumber = "SN/001", OutputDirectory = directory, BaseFileName = "daily-name-is-not-used", LuxMtfFormat = pointFormat,
                LuxLuminanceUniformityFormat = UniformityDisplayFormat.Decimal
            };
            string expected = Path.Combine(directory, "C_SN_001.csv");
            Assert.Equal(expected, CustomTestResultExportService.GetOutputPath(context, profile));
            Assert.Equal(expected, CustomTestResultExportService.Export(context, profile));
            string csv = File.ReadAllText(expected);
            string uniformityUnit = profile == CustomTestResultOutputProfile.LUX_Current_CSV ? "%" : "";
            Assert.Equal($"W255,Luminance_Uniformity(min_max_100),1,{uniformityUnit},0.9,1,pass",
                Assert.Single(File.ReadLines(expected), line => line.StartsWith("W255,Luminance_Uniformity(", StringComparison.Ordinal)));
            Assert.Contains("\"OC,\"\"A\"\"\",Rotation,1,degree,0,0,pass", csv, StringComparison.Ordinal);
            Assert.Contains("Distortion,MaximumTiltDegrees,1.505647,degree,0,0,pass", csv, StringComparison.Ordinal);
            Assert.DoesNotContain(",°,", csv, StringComparison.Ordinal);
            string mtfPrefix = profile == CustomTestResultOutputProfile.LUX_Current_CSV ? "P2"
                : pointFormat == LuxMtfPointFormat.Mtf048 ? "0.4F_LeftUp" : "0.5F_LeftUp";
            Assert.Contains($"MTF0581,{mtfPrefix}_horizontal,0.58,", csv, StringComparison.Ordinal);
            item.Value = 2;
            CustomTestResultExportService.Export(context, profile);
            Assert.Single(Directory.GetFiles(directory));
            Assert.DoesNotContain(",Rotation,1,", File.ReadAllText(expected), StringComparison.Ordinal);
            Assert.Contains(",Rotation,2,", File.ReadAllText(expected), StringComparison.Ordinal);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(LuxCsvFormat.Current, "P")]
    [InlineData(LuxCsvFormat.Current, "P_")]
    [InlineData(LuxCsvFormat.Legacy, "P")]
    [InlineData(LuxCsvFormat.Legacy, "P_")]
    public void ActiveLuxFamiliesIncludeChessboardPoiSnapshotsAndKeepTheOriginalGroupAndMetricNames(LuxCsvFormat format, string pointPrefix)
    {
        var result = new ObjectiveTestResult
        {
            W51TestResult = new() { DiagonalFieldOfViewAngle = Item("Diagonal_Field_of_View_Angle", 46) },
            W255TestResult = new() { PoixyuvDatas = [new() { Name = "P1", Y = 123 }] },
            ChessboardTestResult = new() { ChessboardContrast = Item("old-copy", 999) },
            MTFHV048TestResults = [new() { MTF_HV_H_Center_0F = Item("mtf", 0.6) }],
            DistortionTestResult = new() { HorizontalTVDistortion = Item("HorizontalTVDistortion", 0.2) },
            OpticCenterTestResult = new() { OptCenterXTilt = Item("OptCenterXTilt", 1) }
        };
        var snapshot = new ProjectARVRReuslt
        {
            FlowStatus = FlowStatus.Completed,
            ProcessTypeFullName = typeof(ChessboardProcess).FullName,
            ViewResultJson = JsonConvert.SerializeObject(new
            {
                PoixyuvDatas = Enumerable.Range(1, 16).Select(index => new { Name = pointPrefix + index, Y = 10 * index }).ToArray(),
                ChessboardContrast = Item("database-contrast", 120)
            })
        };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, format, [snapshot]);
        Assert.Contains(rows, row => row.TestScreen == "W255" && row.TestItem == "Diagonal_Field_of_View_Angle" && row.TestValue == "46");
        Assert.Contains(rows, row => row.TestScreen == "W255" && row.TestItem == "P1(Lv)" && row.TestValue == "123");
        foreach (int index in Enumerable.Range(1, 16))
            Assert.Contains(rows, row => row.TestScreen == "Chessborad_4x4" && row.TestItem == $"P{index}(Lv)" && row.TestValue == (10 * index).ToString(CultureInfo.InvariantCulture));
        Assert.DoesNotContain(rows, row => row.TestScreen == "Chessborad_4x4" && row.TestItem.StartsWith("P_", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.TestScreen == "Chessborad_4x4" && row.TestItem == "Contrast_radio" && row.TestValue == "120");
        Assert.DoesNotContain(rows, row => row.TestValue == "999");
        string mtfName = format == LuxCsvFormat.Current ? "P1_horizontal" : "0F_Center_horizontal";
        Assert.Contains(rows, row => row.TestScreen == "MTF" && row.TestItem == mtfName && row.TestValue == "0.6");
        Assert.Contains(rows, row => row.TestScreen == "Distortion" && row.TestItem == "SMIA_TV_Distortion_Horizontal" && row.TestValue == "0.2");
        Assert.Contains(rows, row => row.TestScreen == "Optical_Center" && row.TestItem == "Tilt_x" && row.TestValue == "1");
        Assert.All(rows, row => Assert.Contains(row.TestResult, new[] { "pass", "fail" }));
    }

    [Fact]
    public void PoiOutputUsesDefaultLimitsAndDoesNotExportNoneAsAStatusOrUnit()
    {
        var result = new ObjectiveTestResult();
        result.DynamicPoixyuvDatas["POI_A"] = [new() { Name = "P1", Y = 123, x = 0.3, y = 0.4, u = 0.2, v = 0.5 }];
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Current);
        Assert.Equal(5, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("pass", row.TestResult);
            Assert.Equal("0", row.LowerLimit);
            Assert.Equal("0", row.UpperLimit);
            Assert.NotEqual("None", row.Unit);
        });
    }

    [Fact]
    public void ExistingCustomerConfigurationKeepsItsFormatAndExportOptionsStayFrozenDuringAsyncCleanup()
    {
        var config = JsonConvert.DeserializeObject<ViewResultManagerConfig>("{\"IsSaveCustomXlsx\":true,\"CustomOutputProfile\":0,\"CustomXlsxSavePath\":\"old-directory\"}")!;
        Assert.True(config.ShowCustomProjectName);
        Assert.False(config.ShowLuxMtfFormat);
        Assert.False(config.ShowLuxLuminanceUniformityFormat);
        Assert.Equal(LuxMtfPointFormat.Mtf058, config.LuxMtfFormat);
        Assert.Equal(UniformityDisplayFormat.Percentage, config.LuxLuminanceUniformityFormat);
        var options = CustomTestResultExportOptions.Capture(config);
        config.CustomOutputProfile = CustomTestResultOutputProfile.LUX_Current_CSV;
        config.CustomXlsxSavePath = "new-directory";
        Assert.False(config.ShowCustomProjectName);
        Assert.Equal(CustomTestResultOutputProfile.金星1_0光机抽检规格_视彩成像色度计, options.Profile);
        Assert.Equal("old-directory", options.Directory);
        Assert.True(CustomTestResultExportService.IsLuxProfile(config.CustomOutputProfile));
        Assert.False(CustomTestResultExportService.IsLuxProfile(options.Profile));
        Assert.False(config.ShowLuxMtfFormat);
        Assert.True(config.ShowLuxLuminanceUniformityFormat);
        config.CustomOutputProfile = CustomTestResultOutputProfile.LUX_Legacy_CSV;
        Assert.True(config.ShowLuxMtfFormat);
        Assert.True(config.ShowLuxLuminanceUniformityFormat);
        config.LuxMtfFormat = LuxMtfPointFormat.Mtf048;
        config.LuxLuminanceUniformityFormat = UniformityDisplayFormat.Decimal;
        string saved = JsonConvert.SerializeObject(config);
        var restored = JsonConvert.DeserializeObject<ViewResultManagerConfig>(saved)!;
        Assert.Equal(LuxMtfPointFormat.Mtf048, restored.LuxMtfFormat);
        Assert.Equal(UniformityDisplayFormat.Decimal, restored.LuxLuminanceUniformityFormat);
        var luxOptions = CustomTestResultExportOptions.Capture(config);
        config.LuxMtfFormat = LuxMtfPointFormat.Mtf058;
        config.LuxLuminanceUniformityFormat = UniformityDisplayFormat.Percentage;
        Assert.Equal(LuxMtfPointFormat.Mtf048, luxOptions.LuxMtfFormat);
        Assert.Equal(UniformityDisplayFormat.Decimal, luxOptions.LuxLuminanceUniformityFormat);
        config.IsSaveCustomXlsx = false;
        Assert.False(config.ShowLuxMtfFormat);
        Assert.False(config.ShowLuxLuminanceUniformityFormat);
        Assert.DoesNotContain(nameof(config.ShowCustomProjectName), JsonConvert.SerializeObject(config), StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(config.ShowLuxMtfFormat), saved, StringComparison.Ordinal);
    }

    private static ObjectiveTestItem Item(string name, double value, double min = 0, double max = 0) =>
        new() { Name = name, Value = value, LowLimit = min, UpLimit = max };

    private static ProjectARVRReuslt MtfSnapshot(int id, string configJson, object result) => new()
    {
        Id = id,
        FlowStatus = FlowStatus.Completed,
        ProcessTypeFullName = typeof(MTFHV058Process).FullName,
        ProcessConfigJson = configJson,
        ViewResultJson = JsonConvert.SerializeObject(result)
    };
}
