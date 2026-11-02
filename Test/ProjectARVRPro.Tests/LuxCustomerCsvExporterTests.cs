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
using ProjectARVRPro.Process.OpticCenter;
using ProjectARVRPro.Process.W255;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class LuxCustomerCsvExporterTests
{
    [Theory]
    [InlineData(LuxCsvFormat.Current, "%", false)]
    [InlineData(LuxCsvFormat.Legacy, "", true)]
    public void WhiteProjectionMatchesTheArchivedNamesUnitsAndFieldSelectionWithoutChangingMeasurements(LuxCsvFormat format, string uniformityUnit, bool includesDiagonal)
    {
        var white = new W255TestResult
        {
            PoixyuvDatas = [new() { Name = "P1", Y = 123, x = 0.3, y = 0.4, u = 0.2, v = 0.5 }],
            LuminanceUniformity = Item("new-uniformity-name", 0.8, 0.9, 1),
            CenterLunimance = Item("new-center-name", 123),
            DiagonalFieldOfViewAngle = Item("diagonal", 42)
        };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(new() { W255TestResult = white }, format);
        ObjectiveTestCsvRow uniformity = Assert.Single(rows, row => row.TestItem == "Luminance_Uniformity(min_max_100)");
        Assert.Equal(("80", "90", "100", "fail", uniformityUnit),
            (uniformity.TestValue, uniformity.LowerLimit, uniformity.UpperLimit, uniformity.TestResult, uniformity.Unit));
        Assert.Equal(includesDiagonal, rows.Any(row => row.TestItem == "Diagonal_Field_of_View_Angle"));
        ObjectiveTestCsvRow point = Assert.Single(rows, row => row.TestItem == "P1(Lv)");
        Assert.Equal(("123", "cd/m^2", "pass", "0", "0"), (point.TestValue, point.Unit, point.TestResult, point.LowerLimit, point.UpperLimit));
        ObjectiveTestCsvRow average = Assert.Single(rows, row => row.TestItem == "Average_Luminance");
        Assert.Equal(("0", "pass"), (average.TestValue, average.TestResult));
        Assert.DoesNotContain(rows, row => row.TestResult == "None");
        Assert.Equal(0.8, white.LuminanceUniformity.Value);
        Assert.Equal(0.9, white.LuminanceUniformity.LowLimit);
        Assert.Equal("new-uniformity-name", white.LuminanceUniformity.Name);
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
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, format);
        Assert.Equal(54, rows.Count);
        ObjectiveTestCsvRow row = Assert.Single(rows, item => item.TestItem == itemName);
        Assert.Equal(("MTF", "0.53", "0.4", unit, "pass"), (row.TestScreen, row.TestValue, row.LowerLimit, row.Unit, row.TestResult));
        Assert.Contains(rows, item => item.TestItem.EndsWith("H1", StringComparison.Ordinal) && item.TestValue == "0" && item.TestResult == "pass");
        Assert.DoesNotContain(rows, item => item.TestResult == "None");
    }

    [Fact]
    public void KeyedWhiteAndFovReplaceCompatibilityCopiesInsteadOfExportingThemTwice()
    {
        var result = new ObjectiveTestResult
        {
            W255TestResult = new W255TestResult { PoixyuvDatas = [new() { Name = "P1", Y = 99 }] }
        };
        result.LuminanceChromaticityTestResults["White"] = new() { PoixyuvDatas = [new() { Name = "P1", Y = 200 }] };
        result.FieldOfViewTestResults["White"] = new() { HorizontalFieldOfViewAngle = Item("Fov", 45) };
        result.LuminanceChromaticityTestResults["Red"] = new() { PoixyuvDatas = [new() { Name = "P1", Y = 300 }] };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Current);
        Assert.Equal("200", Assert.Single(rows, row => row.TestScreen == "W255" && row.TestItem == "P1(Lv)").TestValue);
        Assert.Equal("45", Assert.Single(rows, row => row.TestItem == "Horizontal_Field_Of_View_Angle").TestValue);
        Assert.Contains(rows, row => row.TestScreen == "Red" && row.TestItem == "P1(Lv)" && row.TestValue == "300");
        Assert.DoesNotContain(rows, row => row.TestScreen is "R255" or "G255" or "B255");
    }

    [Theory]
    [InlineData(CustomTestResultOutputProfile.LUX_Current_CSV)]
    [InlineData(CustomTestResultOutputProfile.LUX_Legacy_CSV)]
    public void CustomerEntryWritesOneSanitizedSnFileAndReplacesItOnTheNextCommand(CustomTestResultOutputProfile profile)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LuxCustomerExport-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var item = Item("OptCenterRotation", 1);
            var context = new ObjectiveTestResultExportContext
            {
                Result = new() { OpticCenterTestResult = new() { Key = "OC,\"A\"", OptCenterRotation = item } },
                SerialNumber = "SN/001", OutputDirectory = directory, BaseFileName = "daily-name-is-not-used"
            };
            string expected = Path.Combine(directory, "C_SN_001.csv");
            Assert.Equal(expected, CustomTestResultExportService.GetOutputPath(context, profile));
            Assert.Equal(expected, CustomTestResultExportService.Export(context, profile));
            Assert.Contains("\"OC,\"\"A\"\"\",Rotation,1,degree,0,0,pass", File.ReadAllText(expected), StringComparison.Ordinal);
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

    [Fact]
    public void ActiveLuxFamiliesIncludeChessboardPoiSnapshotsAndKeepTheOriginalGroupAndMetricNames()
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
                PoixyuvDatas = Enumerable.Range(1, 16).Select(index => new { Name = "P" + index, Y = 10 * index }).ToArray(),
                ChessboardContrast = Item("database-contrast", 120)
            })
        };
        IReadOnlyList<ObjectiveTestCsvRow> rows = LuxCsvProjector.CollectRows(result, LuxCsvFormat.Current, [snapshot]);
        Assert.Contains(rows, row => row.TestScreen == "W255" && row.TestItem == "Diagonal_Field_of_View_Angle" && row.TestValue == "46");
        Assert.Contains(rows, row => row.TestScreen == "W255" && row.TestItem == "P1(Lv)" && row.TestValue == "123");
        Assert.Contains(rows, row => row.TestScreen == "Chessborad_4x4" && row.TestItem == "P16(Lv)" && row.TestValue == "160");
        Assert.Contains(rows, row => row.TestScreen == "Chessborad_4x4" && row.TestItem == "Contrast_radio" && row.TestValue == "120");
        Assert.DoesNotContain(rows, row => row.TestValue == "999");
        Assert.Contains(rows, row => row.TestScreen == "MTF" && row.TestItem == "P1_horizontal" && row.TestValue == "0.6");
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
        var options = CustomTestResultExportOptions.Capture(config);
        config.CustomOutputProfile = CustomTestResultOutputProfile.LUX_Current_CSV;
        config.CustomXlsxSavePath = "new-directory";
        Assert.False(config.ShowCustomProjectName);
        Assert.Equal(CustomTestResultOutputProfile.金星1_0光机抽检规格_视彩成像色度计, options.Profile);
        Assert.Equal("old-directory", options.Directory);
        Assert.True(CustomTestResultExportService.IsLuxProfile(config.CustomOutputProfile));
        Assert.False(CustomTestResultExportService.IsLuxProfile(options.Profile));
        Assert.DoesNotContain(nameof(config.ShowCustomProjectName), JsonConvert.SerializeObject(config), StringComparison.Ordinal);
    }

    private static ObjectiveTestItem Item(string name, double value, double min = 0, double max = 0) =>
        new() { Name = name, Value = value, LowLimit = min, UpLimit = max };
}
