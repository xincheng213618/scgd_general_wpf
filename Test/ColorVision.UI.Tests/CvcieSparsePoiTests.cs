using ColorVision.Engine.Media;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Draw;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class CvcieSparsePoiTests
{
    [Theory]
    [InlineData(1, 1u, 2048)]
    [InlineData(3, 2u, 683)]
    public void SparseMeasurementsMatchTheFullSnapshotForMixedShapesAndPreserveNegativeValues(int channels, uint version, int height)
    {
        const int width = 1025;
        using var fixture = new Fixture(width, height, channels, version);
        CvcieFilePoiRequest[] requests =
        [
            new(new(0, 0, 1, 1, PoiMeasurementShape.Point), null, "negative point"),
            new(default, ClosedPixelRegion.Ellipse(new(20, 16), 5, 7.5), "ellipse boundary"),
            new(new(20, 16, 10, 15, PoiMeasurementShape.Ellipse), null, "ellipse point"),
            new(new(20, 16, 5, 5, PoiMeasurementShape.Circle), null, "odd circle"),
            new(new(0, 0, 6, 6, PoiMeasurementShape.Circle), null, "clipped even circle"),
            new(default, ClosedPixelRegion.Polygon([new(-2, -2), new(9, -2), new(9, 3), new(4, 3), new(4, 7), new(-2, 7)]), "concave polygon"),
            new(new(width - 1, height - 1, 6, 5, PoiMeasurementShape.Rect), null, "clipped rectangle"),
            new(default, ClosedPixelRegion.Polygon([new(121, 57), new(141, 57), new(141, 68), new(121, 68)], 27.5), "rotated rectangle"),
        ];

        var sparse = fixture.Source.TryCalculateSparse(requests, CancellationToken.None);
        Assert.True(sparse.HasValue);
        Assert.Equal(channels, sparse.Value.Channels);
        using (File.Open(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

        using PoiMeasurementBuffer full = fixture.Source.Load(CancellationToken.None);
        PoiMeasurementResult[] expected = requests.Select(request => request.Region is { } region
            ? PoiMeasurementService.CalculateRegion(full, region, preserveNonPositiveValues: true)
            : PoiMeasurementService.CalculateRaw(full, [request.Point])[0]).ToArray();
        Assert.Equal(expected, sparse.Value.Results);
        Assert.True((channels == 1 ? sparse.Value.Results[0].Y : sparse.Value.Results[0].X) < 0);

        File.SetLastWriteTimeUtc(fixture.Path, fixture.Revision.LastWriteTimeUtc.AddMinutes(-5));
        Assert.Throws<CvcieFileChangedException>(() => fixture.Source.TryCalculateSparse(requests, CancellationToken.None));
    }

    [Fact]
    public void SparsePlanningDeclinesDenseAndHighRowCountRequestsBeforeReading()
    {
        const int width = 1025, height = 683;
        using var fixture = new Fixture(width, height, 3, 2);
        using FileStream exclusive = File.Open(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        CvcieFilePoiRequest[] dense = [new(new(width / 2, height / 2, width, height, PoiMeasurementShape.Rect), null, "dense")];
        CvcieFilePoiRequest[] manyRows = Enumerable.Range(0, 12)
            .Select(index => new CvcieFilePoiRequest(new(index + 10, 100, 1, 128, PoiMeasurementShape.Rect), null, "narrow"))
            .ToArray();
        Assert.Null(fixture.Source.TryCalculateSparse(dense, CancellationToken.None));
        Assert.Null(fixture.Source.TryCalculateSparse(manyRows, CancellationToken.None));
        Assert.Throws<OperationCanceledException>(() => fixture.Source.TryCalculateSparse(dense, new CancellationToken(true)));
    }

    [Fact]
    public void SparseEmptyRegionErrorKeepsItsDrawingName()
    {
        using var fixture = new Fixture(1025, 683, 3, 2);
        var noPixels = ClosedPixelRegion.Polygon([new(10.1, 10.1), new(10.2, 10.1), new(10.2, 10.2)]);
        var error = Assert.Throws<ArgumentException>(() => fixture.Source.TryCalculateSparse(
            [new(default, noPixels, "narrow region")], CancellationToken.None));
        Assert.Contains("narrow region", error.Message);
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cv-sparse-poi-{Guid.NewGuid():N}.cvcie");
        public CvcieFileRevision Revision { get; }
        public CvcieFileMeasurements Source { get; }

        public Fixture(int width, int height, int channels, uint version)
        {
            try
            {
                using var file = new CVCIEFile
                {
                    Version = version, FileExtType = CVType.CIE, Rows = height, Cols = width,
                    Bpp = 32, Channels = channels, Exp = Enumerable.Repeat(1f, channels).ToArray(),
                };
                Assert.True(CVFileUtil.WriteCIEFile(Path, file, (long)width * height * channels * sizeof(float), stream =>
                {
                    float[] row = new float[width];
                    for (int channel = 0; channel < channels; channel++)
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++) row[x] = (x * 3 + y * 5 + channel * 13) % 191 - 55.25f;
                        if (y == 10) row[17] += 1234.5f;
                        stream.Write(MemoryMarshal.AsBytes(row.AsSpan()));
                    }
                }));
                Revision = CvcieFileRevision.Capture(Path);
                using FileStream input = File.OpenRead(Path);
                int headerEnd = CVFileUtil.ReadCIEFileHeader(input, out CVCIEFile header);
                using (header) Assert.True(headerEnd > 0);
                Source = new CvcieFileMeasurements(Path, Revision, headerEnd, version, width, height, 32, channels);
            }
            catch
            {
                File.Delete(Path);
                throw;
            }
        }

        public void Dispose() => File.Delete(Path);
    }
}
