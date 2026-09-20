using Conoscope.Core;
using System.Globalization;
using System.IO;
using System.Windows;

namespace Conoscope.Tests;

public sealed class ConoscopeExportCultureTests
{
    [Fact]
    public void AzimuthCrossSectionKeepsTwoColumnsAndNumericValuesUnderGermanCulture()
    {
        WithGermanCultureExport(path => ConoscopeExportService.ExportAzimuthCrossSection(
            path, ExportChannel.Y, CreateContext(), 0.5,
            new ConoscopeCrossSectionExportOptions { StepDegrees = 0.5, DecimalPlaces = 3 }), lines =>
        {
            Assert.Contains("# Azimuth Cross-Section Export (Angle = 0.5°)", lines);
            Assert.Contains("# Max Angle: 1.5°", lines);
            string[][] rows = ReadCsvRows(lines);
            Assert.Equal(["Azimuth Position (degrees)", "Y (cd/m2)"], rows[0]);
            Assert.Equal(8, rows.Length);
            Assert.All(rows, row => Assert.Equal(2, row.Length));
            Assert.Equal(["-1.50", "300.125"], rows[1]);
            Assert.Equal(["0.00", "303.125"], rows[4]);
            Assert.Equal(["1.50", "306.125"], rows[^1]);
            Assert.Equal(303.125, double.Parse(rows[4][1], CultureInfo.InvariantCulture));
        });
    }

    [Fact]
    public void PolarCrossSectionKeepsTwoColumnsAndClosingSampleUnderGermanCulture()
    {
        WithGermanCultureExport(path => ConoscopeExportService.ExportPolarCrossSection(
            path, ExportChannel.Y, CreateContext(), 0.5,
            new ConoscopeCrossSectionExportOptions { StepDegrees = 90.5, DecimalPlaces = 3 }), lines =>
        {
            Assert.Contains("# Polar Cross-Section Export (Radius Angle = 0.5°)", lines);
            Assert.Contains("# Max Angle: 1.5°", lines);
            string[][] rows = ReadCsvRows(lines);
            Assert.Equal(["Circumferential Angle (degrees)", "Y (cd/m2)"], rows[0]);
            Assert.Equal(6, rows.Length);
            Assert.All(rows, row => Assert.Equal(2, row.Length));
            Assert.Equal(["0.00", "304.125"], rows[1]);
            Assert.Equal(["90.50", "203.125"], rows[2]);
            Assert.Equal(["360.00", "304.125"], rows[^1]);
            Assert.Equal(90.5, double.Parse(rows[2][0], CultureInfo.InvariantCulture));
        });
    }

    [Fact]
    public void AzimuthMatrixKeepsAngleHeadersAndDataAlignedUnderGermanCulture()
    {
        WithGermanCultureExport(path => ConoscopeExportService.ExportAzimuthWithStep(
            path, ExportChannel.Y, CreateContext(), 89.5, 0.5, 3), lines =>
        {
            Assert.Contains("# Azimuth Export Data (azimuth step = 89.5°, radial step = 0.5°)", lines);
            Assert.Contains("# Max Angle: 1.5°", lines);
            string[][] rows = ReadCsvRows(lines);
            Assert.Equal(["Phi \\ Theta", "0.00", "89.50", "179.00"], rows[0]);
            Assert.Equal(8, rows.Length);
            Assert.All(rows, row => Assert.Equal(4, row.Length));
            Assert.Equal(["-1.50", "300.125", "603.125", "306.125"], rows[1]);
            Assert.Equal(["0.00", "303.125", "303.125", "303.125"], rows[4]);
        });
    }

    [Fact]
    public void PolarMatrixKeepsRadiusHeadersAndDataAlignedUnderGermanCulture()
    {
        WithGermanCultureExport(path => ConoscopeExportService.ExportPolarWithStep(
            path, ExportChannel.Y, CreateContext(), 0.75, 90, 3), lines =>
        {
            Assert.Contains("# Polar Angle Export Data (ring step = 0.75°, circumferential step = 90°)", lines);
            Assert.Contains("# Phi (Column): Polar radius angle (0-1.5°, step=0.75°)", lines);
            string[][] rows = ReadCsvRows(lines);
            Assert.Equal(["Phi \\ Theta", "0.00", "0.75", "1.50"], rows[0]);
            Assert.Equal(6, rows.Length);
            Assert.All(rows, row => Assert.Equal(4, row.Length));
            Assert.Equal(["90.00", "303.125", "203.125", "3.125"], rows[2]);
            Assert.Equal("360.00", rows[^1][0]);
            Assert.Equal(rows[1].Skip(1), rows[^1].Skip(1));
        });
    }

    private static ConoscopeExportContext CreateContext() => new()
    {
        ModelName = "Culture regression",
        ImageWidth = 7,
        ImageHeight = 7,
        Center = new Point(3, 3),
        MaxAngle = 1.5,
        PixelsPerDegree = 2,
        ReadXyz = (x, y) => new ConoscopeXyzValue(0, y * 100 + x + 0.125, 0)
    };

    private static string[][] ReadCsvRows(string[] lines) => lines
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .Select(line => line.Split(','))
        .ToArray();

    private static void WithGermanCultureExport(Action<string> export, Action<string[]> verify)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        string path = Path.Combine(Path.GetTempPath(), $"conoscope-export-culture-{Guid.NewGuid():N}.csv");
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            export(path);
            verify(File.ReadAllLines(path));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            File.Delete(path);
        }
    }
}
