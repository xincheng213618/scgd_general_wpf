#pragma warning disable CA1707
using ColorVision.Engine.Templates.Flow;
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
}
