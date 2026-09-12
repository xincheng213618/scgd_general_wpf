using Conoscope.Core;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO;

namespace Conoscope.Tests;

public class ConoscopeCurveSnapshotTests
{
    [Fact]
    public void CapturedArraysRemainDetachedAndCannotBeMutatedThroughPublicCollections()
    {
        double[] positions = { -0.5, 0, 0.5 };
        double[] values = { 10, 20, 30 };
        var before = DateTimeOffset.UtcNow;
        ConoscopeCurveSnapshot snapshot = Create(positions, values);
        positions[0] = 90;
        values[1] = 999;

        Assert.Equal(new[] { -0.5, 0, 0.5 }, snapshot.Positions);
        Assert.Equal(new[] { 10d, 20d, 30d }, snapshot.Values);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)snapshot.Positions)[0] = 90);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)snapshot.Values)[0] = 999);
        Assert.InRange(snapshot.CapturedAtUtc, before, DateTimeOffset.UtcNow);
        Assert.Equal(TimeSpan.Zero, snapshot.CapturedAtUtc.Offset);
    }

    [Fact]
    public void RenameCreatesNewSnapshotWithoutChangingCaptureIdentityOrSamples()
    {
        ConoscopeCurveSnapshot original = Create(new[] { 0.125 }, new[] { 12.345678901234567 });
        ConoscopeCurveSnapshot renamed = original.WithName("  After processing  ");

        Assert.Equal("Before processing", original.Name);
        Assert.Equal("After processing", renamed.Name);
        Assert.Equal(original.CapturedAtUtc, renamed.CapturedAtUtc);
        Assert.Equal(original.SourceName, renamed.SourceName);
        Assert.Equal(original.ModelName, renamed.ModelName);
        Assert.Equal(original.CoordinateSystemName, renamed.CoordinateSystemName);
        Assert.Equal(original.ReferenceDescription, renamed.ReferenceDescription);
        Assert.Equal(original.AxisKey, renamed.AxisKey);
        Assert.Equal(original.AxisLabel, renamed.AxisLabel);
        Assert.Equal(original.ChannelLabel, renamed.ChannelLabel);
        Assert.Equal(original.UnitLabel, renamed.UnitLabel);
        Assert.Equal(original.Metadata, renamed.Metadata);
        Assert.Equal(original.Positions, renamed.Positions);
        Assert.Equal(original.Values, renamed.Values);
        Assert.Throws<ArgumentException>(() => original.WithName(" "));
    }

    [Fact]
    public void ComparisonUsesStableAxisSemanticsAndUnitInsteadOfTranslatedLabels()
    {
        ConoscopeCurveSnapshot horizontal = Create(new[] { 0d }, new[] { 1d }, axisKey: "hv-horizontal-deg");
        ConoscopeCurveSnapshot translated = new("Translated", "source", "VA60", "H/V", "V=0°",
            "水平角度 (°)", "Y", "cd/m²", new[] { 0d }, new[] { 2d }, axisKey: "hv-horizontal-deg");
        ConoscopeCurveSnapshot vertical = Create(new[] { 0d }, new[] { 1d }, axisKey: "hv-vertical-deg");
        ConoscopeCurveSnapshot dimensionless = new("Ratio", "source", "VA60", "H/V", "V=0°",
            horizontal.AxisLabel, "Contrast", "", new[] { 0d }, new[] { 1d }, axisKey: "hv-horizontal-deg");

        Assert.True(horizontal.IsCompatibleWith(translated));
        Assert.False(horizontal.IsCompatibleWith(vertical));
        Assert.False(horizontal.IsCompatibleWith(dimensionless));
    }

    [Fact]
    public void CsvUnderGermanCultureRoundTripsCapturedSamplesAndQuotedMetadata()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            double[] positions = { -0.12345678901234566, 0, 0.8750000000000001 };
            double[] values = { 12345.678901234567, double.NaN, 1.2345678901234567E-12 };
            ConoscopeCurveSnapshot snapshot = new("Before, \"raw\"", "source,one.cvcie", "VA60", "H/V", "V=1.25°",
                "Horizontal angle (°)", "Y", "cd/m²", positions, values, "Filter: off\r\nChannel: Y", "hv-horizontal-deg");
            using StringWriter writer = new();
            snapshot.WriteCsv(writer);
            using TextFieldParser parser = new(new StringReader(writer.ToString()));
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;
            Dictionary<string, string> metadata = new();
            while (!parser.EndOfData)
            {
                string[] row = parser.ReadFields()!;
                Assert.Equal(2, row.Length);
                if (row[0] == "Position")
                {
                    Assert.Equal("Value", row[1]);
                    break;
                }
                metadata.Add(row[0], row[1]);
            }

            Assert.Equal(snapshot.Name, metadata["# Name"]);
            Assert.Equal(snapshot.SourceName, metadata["# Source"]);
            Assert.Equal("VA60", metadata["# Model"]);
            Assert.Equal("H/V", metadata["# CoordinateSystem"]);
            Assert.Equal("V=1.25°", metadata["# Reference"]);
            Assert.Equal("hv-horizontal-deg", metadata["# AxisKey"]);
            Assert.Equal("Horizontal angle (°)", metadata["# AxisLabel"]);
            Assert.Equal("Y", metadata["# Channel"]);
            Assert.Equal("cd/m²", metadata["# Unit"]);
            Assert.Equal(snapshot.Metadata, metadata["# Metadata"]);
            Assert.Equal("3", metadata["# SampleCount"]);
            Assert.Equal(snapshot.CapturedAtUtc, DateTimeOffset.Parse(metadata["# CapturedAtUtc"], CultureInfo.InvariantCulture));
            for (int index = 0; index < positions.Length; index++)
            {
                string[] row = parser.ReadFields()!;
                Assert.Equal(2, row.Length);
                Assert.Equal(positions[index], double.Parse(row[0], CultureInfo.InvariantCulture));
                Assert.Equal(values[index], double.Parse(row[1], CultureInfo.InvariantCulture));
            }
            Assert.True(parser.EndOfData);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    public void InvalidSamplePairsAreRejectedBeforeCapture(int positionCount, int valueCount)
        => Assert.Throws<ArgumentException>(() => Create(new double[positionCount], new double[valueCount]));

    [Fact]
    public void UndefinedPositionsAreRejectedWhileMissingMeasuredValuesArePreserved()
    {
        Assert.Throws<ArgumentException>(() => Create(new[] { double.NaN }, new[] { 1d }));
        Assert.Throws<ArgumentException>(() => Create(new[] { double.PositiveInfinity }, new[] { 1d }));
        Assert.True(double.IsNaN(Create(new[] { 0d }, new[] { double.NaN }).Values[0]));
    }

    private static ConoscopeCurveSnapshot Create(IReadOnlyList<double> positions, IReadOnlyList<double> values,
        string axisKey = "polar-incidence-deg")
        => new("Before processing", "test_ND0.cvcie", "VA60", "Polar", "φ=90°", "Angle (°)", "Y", "cd/m²",
            positions, values, "Filter: off", axisKey);
}
