using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates.POI;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class LocalNinePointPoiCalibrationTests
{
    // Existing flows must keep their measurements until the operator explicitly enables this option.
    [Fact]
    public void ExistingNodeStateLeavesNinePointCalibrationDisabled() => StaTest.Run(() =>
    {
        var node = new LocalCalibrationRealPoiNode();
        node.Create();
        Assert.False(node.EnableNinePointCalibration);
        node.OnLoadNode(new Dictionary<string, byte[]>());
        Assert.False(node.EnableNinePointCalibration);
        Assert.Equal(new[] { "IN_IMG", "IN_POI" }, node.GetAllInputOptions().Select(port => port.Text));
        var setting = typeof(LocalCalibrationRealPoiNode).GetProperty(nameof(node.EnableNinePointCalibration))!
            .GetCustomAttribute<STNodePropertyAttribute>()!;
        Assert.Equal("9点校准", setting.Name);
        Assert.True(setting.IsEditEnable);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NinePointOptionRoundTripsThroughNodeState(bool enabled) => StaTest.Run(() =>
    {
        var original = new LocalCalibrationRealPoiNode { EnableNinePointCalibration = enabled };
        original.Create();
        Dictionary<string, byte[]> state = ReadNodeState(original.GetSaveData());
        Assert.Equal(enabled.ToString(), Encoding.UTF8.GetString(state[nameof(original.EnableNinePointCalibration)]));
        var restored = new LocalCalibrationRealPoiNode();
        restored.Create();
        restored.OnLoadNode(state);
        Assert.Equal(enabled, restored.EnableNinePointCalibration);
    });

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 8)]
    [InlineData(true, 10)]
    public void DisabledOrNonNinePointInputsKeepTheOriginalTemplateWithoutLocating(bool enabled, int count)
    {
        PoiParam source = CreateTemplate(count);
        var before = source.PoiPoints.Select(PointState).ToArray();
        bool called = false;

        PoiParam result = LocalNinePointPoiCalibration.ApplyIfEnabled(enabled, source, _ =>
        {
            called = true;
            throw new InvalidOperationException("This input must not invoke block localization.");
        });

        Assert.False(called);
        Assert.Same(source, result);
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    // Point identity and sampling geometry belong to the existing POI contract; only the located center changes.
    [Fact]
    public void SuccessfulLocalizationOnlyChangesCentersAndLeavesTheInputTemplateUntouched()
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();
        LuminousAreaPoint[] expectedSeeds = source.PoiPoints.Select(point => new LuminousAreaPoint(point.PixX, point.PixY)).ToArray();
        int calls = 0;

        PoiParam result = LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, seeds =>
        {
            calls++;
            Assert.Equal(expectedSeeds, seeds);
            return LocatedPoints(seeds, _ => true);
        });

        Assert.Equal(1, calls);
        Assert.NotSame(source, result);
        Assert.Equal(source.Id, result.Id);
        Assert.Equal(source.Name, result.Name);
        Assert.Equal(9, result.PoiPoints.Count);
        for (int index = 0; index < 9; index++)
        {
            PoiPoint original = source.PoiPoints[index], corrected = result.PoiPoints[index];
            Assert.NotSame(original, corrected);
            AssertPointIdentityAndSize(original, corrected);
            Assert.Equal(original.PixX + 3.25, corrected.PixX);
            Assert.Equal(original.PixY - 6.5, corrected.PixY);
        }
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Fact]
    public void LeftTopRectSearchesFromItsCenterAndAppliesOnlyTheLocatedDisplacementToItsOrigin()
    {
        PoiParam source = CreateTemplate(9);
        foreach (PoiPoint point in source.PoiPoints) point.PointType = PoiShape.LeftTopRect;
        var before = source.PoiPoints.Select(PointState).ToArray();

        PoiParam result = LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, seeds =>
        {
            for (int index = 0; index < 9; index++)
            {
                PoiPoint original = source.PoiPoints[index];
                Assert.Equal(new LuminousAreaPoint(original.PixX + original.PixWidth / 2,
                    original.PixY + original.PixHeight / 2), seeds[index]);
            }
            return LocatedPoints(seeds, _ => true);
        });

        for (int index = 0; index < 9; index++)
        {
            PoiPoint original = source.PoiPoints[index], corrected = result.PoiPoints[index];
            AssertPointIdentityAndSize(original, corrected);
            Assert.Equal(original.PixX + 3.25, corrected.PixX);
            Assert.Equal(original.PixY - 6.5, corrected.PixY);
        }
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Fact]
    public void MissingBlocksKeepTheirOriginalCoordinatesWhileLocatedBlocksAreCorrected()
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();

        PoiParam result = LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, seeds => LocatedPoints(seeds, index => index % 2 == 0));

        Assert.Equal(9, result.PoiPoints.Count);
        for (int index = 0; index < 9; index++)
        {
            PoiPoint original = source.PoiPoints[index], corrected = result.PoiPoints[index];
            AssertPointIdentityAndSize(original, corrected);
            Assert.Equal(original.PixX + (index % 2 == 0 ? 3.25 : 0), corrected.PixX);
            Assert.Equal(original.PixY + (index % 2 == 0 ? -6.5 : 0), corrected.PixY);
        }
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Fact]
    public void BlackScreenWithoutAnyLocatedBlocksKeepsAllOriginalPointsAvailableForMeasurement()
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();

        PoiParam result = LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, seeds => LocatedPoints(seeds, _ => false));

        Assert.Equal(9, result.PoiPoints.Count);
        for (int index = 0; index < 9; index++)
        {
            PoiPoint original = source.PoiPoints[index], retained = result.PoiPoints[index];
            AssertPointIdentityAndSize(original, retained);
            Assert.Equal(original.PixX, retained.PixX);
            Assert.Equal(original.PixY, retained.PixY);
        }
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompleteOrDuplicateResultIndicesAreErrorsRatherThanMissingBlockFallback(bool duplicateIndex)
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();

        Assert.Throws<InvalidOperationException>(() => LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, seeds =>
        {
            NinePointBlockCalibrationPoint[] points = LocatedPoints(seeds, _ => true).ToArray();
            if (!duplicateIndex) return points.Take(8).ToArray();
            points[8] = points[0];
            return points;
        }));

        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Fact]
    public void LocatorSystemErrorsPropagateWithoutChangingTheInputTemplate()
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();
        var error = new InvalidOperationException("Synthetic locator failure.");

        Exception thrown = Assert.Throws<InvalidOperationException>(() =>
            LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, _ => throw error));

        Assert.Same(error, thrown);
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    [Fact]
    public void LocatorCancellationPropagatesWithoutChangingTheInputTemplate()
    {
        PoiParam source = CreateTemplate(9);
        var before = source.PoiPoints.Select(PointState).ToArray();
        var error = new OperationCanceledException();

        Exception thrown = Assert.Throws<OperationCanceledException>(() =>
            LocalNinePointPoiCalibration.ApplyIfEnabled(true, source, _ => throw error));

        Assert.Same(error, thrown);
        Assert.Equal(before, source.PoiPoints.Select(PointState));
    }

    private static PoiParam CreateTemplate(int count)
    {
        PoiParam template = new() { Id = 48, Name = "Synthetic nine-point POI", Width = 640, Height = 480 };
        PoiShape[] shapes = [PoiShape.Circle, PoiShape.Rect, PoiShape.Point];
        for (int index = 0; index < count; index++)
            template.PoiPoints.Add(new PoiPoint
            {
                Id = 100 + index, Pid = 48, Name = "P_" + (index + 1), PointType = shapes[index % shapes.Length],
                PixX = 80 + index % 3 * 160, PixY = 60 + index / 3 * 120,
                PixWidth = 20 + index * 2, PixHeight = 22 + index * 2
            });
        return template;
    }

    private static IReadOnlyList<NinePointBlockCalibrationPoint> LocatedPoints(IReadOnlyList<LuminousAreaPoint> seeds, Func<int, bool> isLocated)
        => seeds.Select((seed, index) => new NinePointBlockCalibrationPoint(index, seed,
            new LuminousAreaPoint(seed.X + 3.25, seed.Y - 6.5), [], isLocated(index) ? 0.95 : 0,
            isLocated(index), isLocated(index) ? string.Empty : "NoCandidate")).ToArray();

    private static void AssertPointIdentityAndSize(PoiPoint expected, PoiPoint actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.PointType, actual.PointType);
        Assert.Equal(expected.PixWidth, actual.PixWidth);
        Assert.Equal(expected.PixHeight, actual.PixHeight);
    }

    private static (int Id, int Pid, string Name, PoiShape Shape, double X, double Y, double Width, double Height) PointState(PoiPoint point)
        => (point.Id, point.Pid, point.Name, point.PointType, point.PixX, point.PixY, point.PixWidth, point.PixHeight);

    private static Dictionary<string, byte[]> ReadNodeState(byte[] data)
    {
        using var reader = new BinaryReader(new MemoryStream(data));
        reader.ReadBytes(reader.ReadByte());
        reader.ReadBytes(reader.ReadByte());
        Dictionary<string, byte[]> state = new();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            string key = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
            state[key] = reader.ReadBytes(reader.ReadInt32());
        }
        return state;
    }
}
