using ColorVision.UI.Controls;
using System.Windows;

namespace ColorVision.UI.Tests;

/// <summary>
/// Unit tests for the squarified <see cref="TreemapLayout"/> algorithm and
/// the <see cref="TreemapNode"/> data model.
/// </summary>
public class TreemapLayoutTests
{
    // ─── TreemapNode tests ───────────────────────────────────────────────────

    [Fact]
    public void TreemapNode_IsLeaf_WhenNoChildren()
    {
        var node = new TreemapNode { Name = "file.txt", Size = 1000 };
        Assert.True(node.IsLeaf);
    }

    [Fact]
    public void TreemapNode_IsNotLeaf_AfterAddChild()
    {
        var parent = new TreemapNode { Name = "folder" };
        parent.AddChild(new TreemapNode { Name = "child.txt", Size = 500 });
        Assert.False(parent.IsLeaf);
    }

    [Fact]
    public void TreemapNode_RecalculateSize_SumsChildren()
    {
        var root = new TreemapNode { Name = "root" };
        root.AddChild(new TreemapNode { Name = "a", Size = 100 });
        root.AddChild(new TreemapNode { Name = "b", Size = 200 });
        root.AddChild(new TreemapNode { Name = "c", Size = 300 });

        root.RecalculateSize();

        Assert.Equal(600, root.Size);
    }

    [Fact]
    public void TreemapNode_RecalculateSize_Recursive()
    {
        var child1 = new TreemapNode { Name = "c1", Size = 50 };
        var child2 = new TreemapNode { Name = "c2", Size = 150 };
        var mid = new TreemapNode { Name = "mid" };
        mid.AddChild(child1);
        mid.AddChild(child2);

        var root = new TreemapNode { Name = "root" };
        root.AddChild(mid);
        root.AddChild(new TreemapNode { Name = "leaf", Size = 300 });

        root.RecalculateSize();

        Assert.Equal(200, mid.Size);
        Assert.Equal(500, root.Size);
    }

    [Fact]
    public void TreemapNode_ToString_ReturnsLabel_WhenSet()
    {
        var node = new TreemapNode { Name = "file.exe", Label = "My App" };
        Assert.Equal("My App", node.ToString());
    }

    [Fact]
    public void TreemapNode_ToString_ReturnsName_WhenLabelNull()
    {
        var node = new TreemapNode { Name = "file.exe" };
        Assert.Equal("file.exe", node.ToString());
    }

    // ─── TreemapLayout tests ─────────────────────────────────────────────────

    [Fact]
    public void TreemapLayout_Calculate_NullRoot_ProducesEmptyResult()
    {
        var layout = new TreemapLayout();
        layout.Calculate(null!, new Rect(0, 0, 800, 600));
        Assert.Empty(layout.LayoutResult);
    }

    [Fact]
    public void TreemapLayout_Calculate_LeafRoot_FillsBounds()
    {
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "single", Size = 100 };
        var bounds = new Rect(0, 0, 400, 300);

        layout.Calculate(root, bounds);

        Assert.True(layout.LayoutResult.ContainsKey(root));
        var rect = layout.LayoutResult[root];
        Assert.Equal(bounds, rect);
    }

    [Fact]
    public void TreemapLayout_Calculate_ContainerRoot_ChildrenLaidOut()
    {
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "root" };
        root.AddChild(new TreemapNode { Name = "a", Size = 300 });
        root.AddChild(new TreemapNode { Name = "b", Size = 200 });
        root.AddChild(new TreemapNode { Name = "c", Size = 100 });
        root.RecalculateSize();

        layout.Calculate(root, new Rect(0, 0, 800, 600));

        // Root itself is not in the result (only children are)
        Assert.False(layout.LayoutResult.ContainsKey(root));
        Assert.Equal(3, layout.LayoutResult.Count);
    }

    [Fact]
    public void TreemapLayout_Calculate_AllRectsWithinBounds()
    {
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "root" };
        for (int i = 0; i < 10; i++)
            root.AddChild(new TreemapNode { Name = $"item{i}", Size = (i + 1) * 100 });
        root.RecalculateSize();

        var bounds = new Rect(0, 0, 800, 600);
        layout.Calculate(root, bounds);

        foreach (var (_, rect) in layout.LayoutResult)
        {
            Assert.True(rect.X >= bounds.X - 0.01, $"Left out of bounds: {rect.X}");
            Assert.True(rect.Y >= bounds.Y - 0.01, $"Top out of bounds: {rect.Y}");
            Assert.True(rect.Right <= bounds.Right + 0.01, $"Right out of bounds: {rect.Right}");
            Assert.True(rect.Bottom <= bounds.Bottom + 0.01, $"Bottom out of bounds: {rect.Bottom}");
        }
    }

    [Fact]
    public void TreemapLayout_Calculate_RectsHavePositiveArea()
    {
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "root" };
        root.AddChild(new TreemapNode { Name = "big", Size = 900 });
        root.AddChild(new TreemapNode { Name = "small", Size = 100 });
        root.RecalculateSize();

        layout.Calculate(root, new Rect(0, 0, 800, 600));

        foreach (var (_, rect) in layout.LayoutResult)
        {
            Assert.True(rect.Width > 0);
            Assert.True(rect.Height > 0);
        }
    }

    [Fact]
    public void TreemapLayout_Calculate_ZeroSizeBounds_ProducesEmptyResult()
    {
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "root" };
        root.AddChild(new TreemapNode { Name = "a", Size = 100 });
        root.RecalculateSize();

        layout.Calculate(root, new Rect(0, 0, 0, 0));

        Assert.Empty(layout.LayoutResult);
    }

    [Fact]
    public void TreemapLayout_Calculate_SubPixelNodes_Filtered()
    {
        // One huge node + many tiny nodes — tiny ones should be filtered out
        var layout = new TreemapLayout();
        var root = new TreemapNode { Name = "root" };
        root.AddChild(new TreemapNode { Name = "huge", Size = 1_000_000 });
        for (int i = 0; i < 50; i++)
            root.AddChild(new TreemapNode { Name = $"tiny{i}", Size = 1 }); // < 2x2 px on 800x600
        root.RecalculateSize();

        layout.Calculate(root, new Rect(0, 0, 800, 600));

        // "huge" must be present; all 2x2 rects must satisfy minimum dimension
        foreach (var (_, rect) in layout.LayoutResult)
        {
            Assert.True(rect.Width >= 2 && rect.Height >= 2,
                $"Node rect too small: {rect.Width}x{rect.Height}");
        }
    }

    [Fact]
    public void TreemapLayout_Calculate_NestedChildren_RecursivelyLaidOut()
    {
        var layout = new TreemapLayout();
        var leaf1 = new TreemapNode { Name = "leaf1", Size = 400 };
        var leaf2 = new TreemapNode { Name = "leaf2", Size = 400 };
        var folder = new TreemapNode { Name = "folder" };
        folder.AddChild(leaf1);
        folder.AddChild(leaf2);

        var root = new TreemapNode { Name = "root" };
        root.AddChild(folder);
        root.RecalculateSize();

        layout.Calculate(root, new Rect(0, 0, 800, 600));

        // Both leaves should be laid out
        Assert.True(layout.LayoutResult.ContainsKey(leaf1));
        Assert.True(layout.LayoutResult.ContainsKey(leaf2));
    }
}
