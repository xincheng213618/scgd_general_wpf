using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using System.Collections.ObjectModel;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class JinxingInspectionXlsxExporterTests
{
    [Fact]
    public void Export_RoundTripsBuiltInWorkbookAndAppendsRows()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro_Jinxing_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var exporter = new JinxingInspectionXlsxExporter();
            string firstPath = exporter.Export(CreateContext(outputDirectory, "SN-001", 0));
            using (FileStream firstInput = File.OpenRead(firstPath))
            using (var firstWorkbook = new XSSFWorkbook(firstInput))
            {
                Assert.Equal("SN-001", firstWorkbook.GetSheet("雷鸟光机测试结果表").GetRow(2).GetCell(2).StringCellValue);
            }

            string secondPath = exporter.Export(CreateContext(outputDirectory, "SN-002", 1));

            Assert.Equal(firstPath, secondPath);
            Assert.True(File.Exists(secondPath));

            using FileStream input = File.OpenRead(secondPath);
            using var workbook = new XSSFWorkbook(input);
            Assert.Equal(2, workbook.NumberOfSheets);

            ISheet resultSheet = workbook.GetSheet("雷鸟光机测试结果表");
            Assert.NotNull(resultSheet);
            Assert.Equal("Sheet1", workbook.GetSheetAt(1).SheetName);
            Assert.Equal("序号", resultSheet.GetRow(0).GetCell(0).StringCellValue);
            Assert.Equal("P1", resultSheet.GetRow(1).GetCell(6).StringCellValue);

            IRow firstRow = resultSheet.GetRow(2);
            Assert.Equal(1, firstRow.GetCell(0).NumericCellValue);
            Assert.Equal("2026-08-12 01:02:03.456", firstRow.GetCell(1).StringCellValue);
            Assert.Equal("SN-001", firstRow.GetCell(2).StringCellValue);
            Assert.Equal(21.5, firstRow.GetCell(3).NumericCellValue, 6);
            Assert.Equal(0.11, firstRow.GetCell(6).NumericCellValue, 6);
            Assert.Equal(0.65, firstRow.GetCell(35).NumericCellValue, 6);
            Assert.Equal(0.31, firstRow.GetCell(36).NumericCellValue, 6);
            Assert.Equal(0.42, firstRow.GetCell(37).NumericCellValue, 6);
            Assert.Equal(123.4, firstRow.GetCell(38).NumericCellValue, 6);
            Assert.Equal(0.88, firstRow.GetCell(39).NumericCellValue, 6);
            Assert.Equal("0.00%", firstRow.GetCell(39).CellStyle.GetDataFormatString());
            Assert.Equal(0.012, firstRow.GetCell(40).NumericCellValue, 6);

            IRow appendedRow = resultSheet.GetRow(3);
            Assert.Equal(2, appendedRow.GetCell(0).NumericCellValue);
            Assert.Equal("SN-002", appendedRow.GetCell(2).StringCellValue);
            Assert.Equal(22.5, appendedRow.GetCell(3).NumericCellValue, 6);
            Assert.Equal(124.4, appendedRow.GetCell(38).NumericCellValue, 6);

            Assert.Equal(16, resultSheet.NumMergedRegions);
            Assert.Contains(
                Enumerable.Range(0, resultSheet.NumMergedRegions).Select(resultSheet.GetMergedRegion),
                region => region.FirstRow == 0 && region.LastRow == 1 && region.FirstColumn == 0 && region.LastColumn == 0);
            Assert.Contains(
                Enumerable.Range(0, resultSheet.NumMergedRegions).Select(resultSheet.GetMergedRegion),
                region => region.FirstRow == 0 && region.LastRow == 0 && region.FirstColumn == 6 && region.LastColumn == 10);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static ObjectiveTestResultExportContext CreateContext(string outputDirectory, string serialNumber, double offset)
    {
        return new ObjectiveTestResultExportContext
        {
            Result = CreateResult(offset),
            SerialNumber = serialNumber,
            OutputDirectory = outputDirectory,
            BaseFileName = "inspection",
            ExportTime = new DateTime(2026, 8, 12, 1, 2, 3, 456, DateTimeKind.Local).AddMinutes(offset),
        };
    }

    private static ObjectiveTestResult CreateResult(double offset)
    {
        var result = new ObjectiveTestResult
        {
            W51TestResult = new W51TestResult
            {
                HorizontalFieldOfViewAngle = Item("HorizontalFieldOfViewAngle", 21.5 + offset),
                VerticalFieldOfViewAngle = Item("VerticalFieldOfViewAngle", 18.25 + offset),
                DiagonalFieldOfViewAngle = Item("DiagonalFieldOfViewAngle", 23.75 + offset),
            },
            DistortionTestResult = new DistortionTestResult
            {
                HorizontalTVDistortion = Item("HorizontalTVDistortion", 0.31 + offset),
                VerticalTVDistortion = Item("VerticalTVDistortion", 0.42 + offset),
            },
            W255TestResult = new W255TestResult
            {
                CenterLunimance = Item("CenterLunimance", 123.4 + offset),
                LuminanceUniformity = Item("LuminanceUniformity", 0.88 + offset / 100),
                ColorUniformity = Item("ColorUniformity", 0.012 + offset / 1000),
            },
        };

        AddMtfScreen(result, "MTFV1", "V", "1", 0.11 + offset / 100);
        AddMtfScreen(result, "MTFH1", "H", "1", 0.21 + offset / 100);
        AddMtfScreen(result, "MTFV2", "V", "2", 0.31 + offset / 100);
        AddMtfScreen(result, "MTFH2", "H", "2", 0.41 + offset / 100);
        AddMtfScreen(result, "MTFV4", "V", "4", 0.51 + offset / 100);
        AddMtfScreen(result, "MTFH4", "H", "4", 0.61 + offset / 100);
        return result;
    }

    private static void AddMtfScreen(ObjectiveTestResult result, string screen, string axis, string frequency, double firstValue)
    {
        result.DynamicTestResults[screen] = new ObservableCollection<ObjectiveTestItem>(
            Enumerable.Range(1, 5).Select(point => Item($"P_{point}_{axis}_{frequency}", firstValue + (point - 1) / 100.0)));
    }

    private static ObjectiveTestItem Item(string name, double value)
    {
        return new ObjectiveTestItem
        {
            Name = name,
            TestValue = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Value = value,
        };
    }
}
