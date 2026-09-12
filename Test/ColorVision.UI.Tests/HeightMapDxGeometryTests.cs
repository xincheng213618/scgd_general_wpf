using ColorVision.ImageEditor.EditorTools.ThreeD;
using System.Numerics;

namespace ColorVision.UI.Tests;

public sealed class HeightMapDxGeometryTests
{
    [Fact]
    public void Build_PreservesWorldExtentAndByteHeightAcrossDetailLevels()
    {
        var sample = new HeightMapSample([0, 64, 128, 255, 0, 64, 128, 255, 0, 64, 128, 255], null, 4, 3);
        var field = HeightMapDxGeometry.Build(sample, 511, 340, CancellationToken.None);

        Assert.Equal(new Vector3(0, 340, 0), field.Positions[0]);
        Assert.Equal(new Vector3(511, 0, 1), field.Positions[^1]);
        Assert.Equal(12, field.Positions.Length);
        Assert.Equal(36, field.Indices.Length);
        Assert.Equal((128.5f / 256), field.TextureCoordinates[2].X);
        foreach (int index in field.Indices) Assert.InRange(index, 0, field.Positions.Length - 1);
    }

    [Fact]
    public void Build_UsesAlphaThresholdAndDropsTheWholeQuad()
    {
        var visible = new HeightMapSample([255, 255, 255, 255], [128, 128, 128, 128], 2, 2);
        Assert.Equal(6, HeightMapDxGeometry.Build(visible, 1, 1, CancellationToken.None).Indices.Length);

        var hole = visible with { Alpha = [128, 128, 127, 128] };
        var geometry = HeightMapDxGeometry.Build(hole, 1, 1, CancellationToken.None);
        Assert.Empty(geometry.Indices);
        Assert.False(geometry.TryIntersect(new Vector3(.5f, .5f, 2), -Vector3.UnitZ, 1, 1, 1, out _));
    }

    [Fact]
    public void ReducedMesh_DoesNotBridgeATransparentPixelBetweenCoarseCorners()
    {
        byte[] gray = Enumerable.Repeat((byte)180, 25).ToArray();
        byte[] alpha = Enumerable.Repeat((byte)255, 25).ToArray();
        alpha[1 * 5 + 1] = 0;
        var source = new HeightMapSample(gray, alpha, 5, 5);
        var reduced = HeightMapDxGeometry.Reduce(source, 2, 2, CancellationToken.None);

        Assert.All(reduced.Alpha!, item => Assert.Equal(255, item));
        var geometry = HeightMapDxGeometry.Build(reduced, 4, 4, CancellationToken.None, source);
        Assert.Empty(geometry.Indices);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(0, 3)]
    [InlineData(4, 3)]
    [InlineData(2, 1)]
    public void RayPicking_FlatSurfaceIncludesEdgesAndCorners(float x, float y)
    {
        var sample = new HeightMapSample(Enumerable.Repeat((byte)128, 20).ToArray(), null, 5, 4);
        var geometry = HeightMapDxGeometry.Build(sample, 4, 3, CancellationToken.None);

        bool found = geometry.TryIntersect(new Vector3(x, y, 100), -Vector3.UnitZ, 4, 3, 20, out Vector3 point);

        Assert.True(found);
        Assert.Equal(x, point.X);
        Assert.Equal(y, point.Y);
        Assert.InRange(Math.Abs(point.Z - 128 / 255f * 20), 0, 0.00001f);
    }

    [Fact]
    public void RayPicking_ReportsDisplayedHeightOnSlopedSurface()
    {
        var sample = new HeightMapSample([0, 255, 0, 255], null, 2, 2);
        var geometry = HeightMapDxGeometry.Build(sample, 10, 8, CancellationToken.None);
        Vector3 direction = Vector3.Normalize(new Vector3(1, 0, -1));

        Assert.True(geometry.TryIntersect(new Vector3(0, 4, 20), direction, 10, 8, 30, out Vector3 point));
        Assert.InRange(Vector3.Distance(new Vector3(5, 4, 15), point), 0, 0.0001f);
    }

    [Fact]
    public void RayPicking_ReturnsNearestSurfaceAndMatchesIndependentTriangleSearch()
    {
        var random = new Random(1729);
        byte[] gray = new byte[17 * 13];
        byte[] alpha = Enumerable.Repeat((byte)255, gray.Length).ToArray();
        random.NextBytes(gray);
        alpha[7 * 17 + 9] = 0;
        var geometry = HeightMapDxGeometry.Build(new HeightMapSample(gray, alpha, 17, 13), 37, 23, CancellationToken.None);
        for (int i = 0; i < 250; i++)
        {
            Vector3 origin = new((float)(random.NextDouble() * 90 - 25), (float)(random.NextDouble() * 80 - 25), (float)(random.NextDouble() * 60 + 25));
            Vector3 target = new((float)(random.NextDouble() * 37), (float)(random.NextDouble() * 23), (float)(random.NextDouble() * 20));
            Vector3 direction = Vector3.Normalize(target - origin);
            bool found = geometry.TryIntersect(origin, direction, 37, 23, 25, out Vector3 actual);
            bool expected = BruteForce(geometry, origin, direction, 25, out Vector3 point);
            Assert.Equal(expected, found);
            if (found) Assert.InRange(Vector3.Distance(point, actual), 0, 0.001f);
        }
    }

    [Fact]
    public void RayPicking_RejectsParallelOutsideAndAwayFacingRays()
    {
        var geometry = HeightMapDxGeometry.Build(new HeightMapSample([128, 128, 128, 128], null, 2, 2), 1, 1, CancellationToken.None);
        Assert.False(geometry.TryIntersect(new Vector3(2, .5f, 2), -Vector3.UnitZ, 1, 1, 1, out _));
        Assert.False(geometry.TryIntersect(new Vector3(.5f, .5f, 2), Vector3.UnitZ, 1, 1, 1, out _));
        Assert.False(geometry.TryIntersect(new Vector3(.5f, .5f, 2), Vector3.UnitX, 1, 1, 1, out _));
    }

    [Fact]
    public void Build_HonorsCancellationBeforeAllocatingIndices()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sample = new HeightMapSample([0, 0, 0, 0], null, 2, 2);
        Assert.Throws<OperationCanceledException>(() => HeightMapDxGeometry.Build(sample, 1, 1, cancellation.Token));
    }

    // Independent ray/plane intersection followed by barycentric coordinates is deliberately
    // different from the renderer's grid traversal and Moller-Trumbore triangle intersection.
    private static bool BruteForce(HeightMapDxGeometry geometry, Vector3 origin, Vector3 direction, float scale, out Vector3 point)
    {
        point = default;
        double nearest = double.PositiveInfinity;
        for (int i = 0; i < geometry.Indices.Length; i += 3)
        {
            Vector3 a = geometry.Positions[geometry.Indices[i]] * new Vector3(1, 1, scale);
            Vector3 b = geometry.Positions[geometry.Indices[i + 1]] * new Vector3(1, 1, scale);
            Vector3 c = geometry.Positions[geometry.Indices[i + 2]] * new Vector3(1, 1, scale);
            Vector3 normal = Vector3.Cross(b - a, c - a);
            double denominator = Dot(normal, direction);
            if (Math.Abs(denominator) < 1e-10) continue;
            double t = Dot(normal, a - origin) / denominator;
            if (t < 0 || t >= nearest) continue;
            Vector3 candidate = origin + direction * (float)t;
            Vector3 v0 = b - a, v1 = c - a, v2 = candidate - a;
            double d00 = Dot(v0, v0), d01 = Dot(v0, v1), d11 = Dot(v1, v1), d20 = Dot(v2, v0), d21 = Dot(v2, v1);
            double divisor = d00 * d11 - d01 * d01;
            double u = (d11 * d20 - d01 * d21) / divisor;
            double v = (d00 * d21 - d01 * d20) / divisor;
            if (u < -1e-6 || v < -1e-6 || u + v > 1.000001) continue;
            nearest = t;
            point = candidate;
        }
        return double.IsFinite(nearest);
    }

    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
}
