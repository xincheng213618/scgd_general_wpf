using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CsvHelper;
using ProjectARVRPro.Process.Chessboard;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ChessboardCsvExporterTests
{
    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void BuildEnhancedCsvContent_KeepsColorWithItsPointWhenNameSpansLines(string lineBreak)
    {
        string firstPointName = $"Black, \"upper{lineBreak}left\"";
        PoiResultCIExyuvData[] points =
        [
            CreatePoint(firstPointName, 10, 10, 0),
            CreatePoint("P2", 20, 10, 10),
            CreatePoint("P3", 10, 20, 12),
            CreatePoint("P4", 20, 20, 2)
        ];
        ChessboardContrastCalculationResult calculation = ChessboardContrastCalculator.Calculate(
            points, 2, 2, ChessboardFirstPointColor.Black, 0, allowNegativeCorrectedDarkLuminance: false);
        Assert.True(calculation.Success, calculation.ErrorMessage);

        string path = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro_ChessboardCsv_{Guid.NewGuid():N}.csv");
        try
        {
            new ObservableCollection<PoiResultCIExyuvData>(points).SaveCsv(path);
            string enhanced = ChessboardCsvExporter.BuildEnhancedCsvContent(
                File.ReadAllText(path), calculation, calculation.CorrectedContrast, "contrast", "local");

            using var csv = new CsvParser(new StringReader(enhanced), CultureInfo.InvariantCulture);
            Assert.True(csv.Read());
            Assert.Equal("cell_color", csv[15]);
            Assert.True(csv.Read());
            Assert.Equal(16, csv.Count);
            Assert.Equal(firstPointName, csv[0]);
            Assert.Equal("0", csv[7]);
            Assert.Equal("Black", csv[15]);
            Assert.True(csv.Read());
            Assert.Equal(16, csv.Count);
            Assert.Equal("P2", csv[0]);
            Assert.Equal("10", csv[7]);
            Assert.Equal("White", csv[15]);
            Assert.True(csv.Read());
            Assert.Equal("White", csv[15]);
            Assert.True(csv.Read());
            Assert.Equal("Black", csv[15]);
            Assert.True(csv.Read());
            Assert.Equal(16, csv.Count);
            Assert.Equal(string.Empty, csv[15]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PoiResultCIExyuvData CreatePoint(string name, int x, int y, double luminance) => new()
    {
        Name = name,
        Point = new PoiPoint(0, 0, name, PoiShape.Circle, x, y, 2, 2),
        Y = luminance
    };
}
