#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Editor;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public class SugiyamaLayoutTests
{
    [Fact]
    public void SplitSerialLaneRows_FoldsLongSingleChainAtReadableColumnCount()
    {
        List<STNode> nodes = Enumerable
            .Range(0, 21)
            .Select(_ => (STNode)new STNodeHub())
            .ToList();

        List<List<STNode>> rows = SugiyamaLayout.SplitSerialLaneRows(nodes, 6);

        Assert.Collection(
            rows,
            row => Assert.Equal(6, row.Count),
            row => Assert.Equal(6, row.Count),
            row => Assert.Equal(6, row.Count),
            row => Assert.Equal(3, row.Count));
        Assert.Equal(nodes, rows.SelectMany(row => row));
    }

    [Fact]
    public void SplitSerialLaneRows_LeavesShortChainOnOneRow()
    {
        List<STNode> nodes = Enumerable
            .Range(0, 8)
            .Select(_ => (STNode)new STNodeHub())
            .ToList();

        List<List<STNode>> rows = SugiyamaLayout.SplitSerialLaneRows(nodes, 6);

        Assert.Single(rows);
        Assert.Same(nodes, rows[0]);
    }

    [Fact]
    public void CalculateSerialLaneRowCount_UsesOddRowsForReadableSerpentineLayout()
    {
        int rowCount = SugiyamaLayout.CalculateSerialLaneRowCount(
            nodeCount: 100,
            viewportWidth: 1000,
            viewportHeight: 600,
            columnPitch: 220,
            rowPitch: 120);

        Assert.Equal(11, rowCount);
        Assert.True(rowCount % 2 == 1);
    }

    [Fact]
    public void SplitSerialLaneIntoRows_BalancesNodesWithoutChangingFlowOrder()
    {
        List<STNode> nodes = Enumerable
            .Range(0, 21)
            .Select(_ => (STNode)new STNodeHub())
            .ToList();

        List<List<STNode>> rows = SugiyamaLayout.SplitSerialLaneIntoRows(nodes, 5);

        Assert.Equal(new[] { 5, 4, 4, 4, 4 }, rows.Select(row => row.Count));
        Assert.Equal(nodes, rows.SelectMany(row => row));
    }

    [Theory]
    [InlineData("CommonSensorNode", "通用传感器")]
    [InlineData("ManualConfirmNode", "手动确认")]
    [InlineData("PGGECSNode", "PG.GECS")]
    public void IsLaneStartCandidate_RecognizesRepeatedFlowStages(string typeName, string title)
    {
        Assert.True(SugiyamaLayout.IsLaneStartCandidate(typeName, title));
    }

    [Fact]
    public void PrepareForwardLaneRows_KeepsEveryLaneLeftToRight()
    {
        List<STNode> firstRow =
        [
            new STNodeHub(),
            new STNodeHub(),
            new STNodeHub()
        ];
        List<STNode> secondRow =
        [
            new STNodeHub(),
            new STNodeHub()
        ];

        List<List<STNode>> visualRows = SugiyamaLayout.PrepareForwardLaneRows(
            new[] { firstRow, secondRow });

        Assert.Equal(firstRow, visualRows[0]);
        Assert.Equal(secondRow, visualRows[1]);
    }
}
