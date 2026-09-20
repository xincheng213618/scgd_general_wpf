using Conoscope.Core;
using System.Globalization;
using System.IO;

namespace Conoscope.Tests;

public sealed class ConoscopeStreamingExportTests
{
    [Theory]
    [InlineData(false, 17.3, 7.3)]
    [InlineData(true, 7.3, 17.3)]
    [InlineData(false, 1, 1)]
    [InlineData(true, 1, 1)]
    public void MatrixPreservesLegacyAngleAccumulationGeometryAndFormat(bool polar, double columnStep, double rowStep)
    {
        string file = Path.GetTempFileName();
        try
        {
            var context = Context();
            if (polar) ConoscopeExportService.ExportPolarWithStep(file, ExportChannel.Y, context, columnStep, rowStep, 4);
            else ConoscopeExportService.ExportAzimuthWithStep(file, ExportChannel.Y, context, columnStep, rowStep, 4);
            string[] lines = File.ReadAllLines(file).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();
            List<double> columns = new();
            for (double angle = 0; polar ? angle <= 60.0001 : angle < 179.9999; angle += columnStep) columns.Add(angle);
            List<double> rows = new();
            for (double angle = polar ? 0 : -60; angle <= (polar ? 360.0001 : 60.0001); angle += rowStep) rows.Add(angle);
            Assert.Equal("Phi \\ Theta," + string.Join(',', columns.Select(angle => angle.ToString("F2", CultureInfo.InvariantCulture))), lines[0]);
            Assert.Equal(rows.Count + 1, lines.Length);
            Assert.Equal((long)columns.Count * rows.Count, ConoscopeExportService.EstimateMatrixSamples(60, columnStep, rowStep, polar));
            for (int index = 0; index < rows.Count; index++)
            {
                string[] cells = lines[index + 1].Split(',');
                Assert.Equal(rows[index].ToString("F2", CultureInfo.InvariantCulture), cells[0]);
                for (int column = 0; column < columns.Count; column++)
                {
                    double angle = (polar ? rows[index] % 360 : columns[column]) * Math.PI / 180;
                    double radius = polar ? columns[column] : rows[index];
                    int x = Math.Clamp((int)Math.Round(60 + radius * Math.Cos(angle)), 0, 120);
                    int y = Math.Clamp((int)Math.Round(60 - radius * Math.Sin(angle)), 0, 120);
                    Assert.Equal((1000 * y + x).ToString("F4", CultureInfo.InvariantCulture), cells[column + 1]);
                }
            }
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CancelMidMatrixRetainsDestinationAndRemovesOnlyOwnedTemporaryFile()
    {
        string file = Path.GetTempFileName();
        using CancellationTokenSource cancellation = new();
        int reads = 0;
        try
        {
            File.WriteAllText(file, "previous result");
            var context = Context(cancellation.Token, read: (x, y) =>
            {
                if (++reads == 500) cancellation.Cancel();
                return new(x, y, 0);
            });
            Assert.Throws<OperationCanceledException>(() => ConoscopeExportService.ExportAzimuthWithStep(file, ExportChannel.Y, context, .1, .1));
            Assert.InRange(reads, 500, 756);
            Assert.Equal("previous result", File.ReadAllText(file));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, $".{Path.GetFileName(file)}.*.tmp"));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void FailureAndPreCancelledExportCannotTruncateAnExistingFile()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "previous result");
            var failure = Context(read: (_, _) => throw new IOException("reader failure"));
            Assert.Throws<IOException>(() => ConoscopeExportService.ExportCircleModeToCsv(file, ExportChannel.Y, failure));
            var cancelled = Context(new CancellationToken(true));
            Assert.Throws<OperationCanceledException>(() => ConoscopeExportService.ExportAngleModeToCsv(file, ExportChannel.Y, cancelled));
            Assert.Equal("previous result", File.ReadAllText(file));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, $".{Path.GetFileName(file)}.*.tmp"));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void LockedDestinationIsPreservedWhenFinalReplacementFails()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "previous result");
            using (FileStream locked = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Exception? error = Record.Exception(() => ConoscopeExportService.ExportAngleModeToCsv(file, ExportChannel.Y, Context()));
                // Windows can map denied replacement to access-denied or sharing-violation.
                Assert.True(error is IOException or UnauthorizedAccessException);
            }
            Assert.Equal("previous result", File.ReadAllText(file));
            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, $".{Path.GetFileName(file)}.*.tmp"));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ProgressCountsActualWrittenSamples()
    {
        string file = Path.GetTempFileName();
        List<ConoscopeExportProgress> reports = new();
        try
        {
            var context = Context(progress: new ImmediateProgress(reports.Add));
            ConoscopeExportService.ExportCircleModeToCsv(file, ExportChannel.Y, context);
            Assert.NotEmpty(reports);
            Assert.Equal(new ConoscopeExportProgress(61 * 361, 61 * 361), reports[^1]);
            Assert.True(reports.Zip(reports.Skip(1), (a, b) => a.CompletedSamples <= b.CompletedSamples).All(value => value));
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.01)]
    [InlineData(0.099999)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidStepFailsBeforeWriting(double step)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "previous result");
            Assert.Throws<ArgumentOutOfRangeException>(() => ConoscopeExportService.ExportPolarWithStep(file, ExportChannel.Y, Context(), step, 1));
            Assert.Equal("previous result", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }

    private static ConoscopeExportContext Context(CancellationToken token = default, IProgress<ConoscopeExportProgress>? progress = null,
        Func<int, int, ConoscopeXyzValue>? read = null) => new()
        {
            ModelName = "VA60", ImageWidth = 121, ImageHeight = 121, Center = new(60, 60), MaxAngle = 60, PixelsPerDegree = 1,
            ReadXyz = read ?? ((x, y) => new(0, 1000 * y + x, 0)), CancellationToken = token, Progress = progress
        };

    private sealed class ImmediateProgress(Action<ConoscopeExportProgress> report) : IProgress<ConoscopeExportProgress>
    {
        public void Report(ConoscopeExportProgress value) => report(value);
    }
}
