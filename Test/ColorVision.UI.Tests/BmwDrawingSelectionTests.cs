using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class BmwDrawingSelectionTests
{
    [Fact]
    public void ExistingRectanglesKeepIdentityAcrossSingleBatchAndMovedRuns()
    {
        var state = new BmwDrawingAnalysisState();
        var a = new Rectangle(new(10,20,40,50));
        var b = new Rectangle(new(100,200,60,70));
        var scope = new ImageSelectionScope(Guid.NewGuid(),1,2000,2000,192,192);
        var single = state.Capture([b],scope);
        var batch = state.Capture([a,b],scope);
        Assert.Equal(single[0].Id,batch[1].Id);
        Assert.NotEqual(batch[0].Id,batch[1].Id);
        Assert.Equal(200,batch[1].Roi.X);
        Assert.Equal(400,batch[1].Roi.Y);
        Assert.Equal(120,batch[1].Roi.Width);
        b.Rect = new(150,250,60,70);
        var moved = state.Capture([b],scope);
        Assert.Equal(single[0].Id,moved[0].Id);
        Assert.Equal(300,moved[0].Roi.X);
        Assert.Single(state.Capture([b,b],scope));
    }

    [Fact]
    public void OutOfImageRectangleIsNotClippedOrDropped()
    {
        var state = new BmwDrawingAnalysisState();
        var result = state.Capture([new Rectangle(new(-10,20,50,60)),new Rectangle(Rect.Empty)],new(Guid.NewGuid(),1,100,100,96,96));
        Assert.Equal(2,result.Count);
        Assert.Equal(-10,result[0].Roi.X);
        Assert.Equal(50,result[0].Roi.Width);
        Assert.Equal(0,result[1].Roi.Width);
    }

    [Fact]
    public void SingleAndMultipleSelectionExposeDistinctDirectCommands()
    {
        WpfTestHost.Invoke(() =>
        {
            var rectangle = new DVRectangle { Rect = new(10,20,40,50) };
            var draw = new DrawEditorContext(new DrawCanvas(),new Zoombox());
            using var selection = new SelectEditorVisual(draw);
            draw.SelectionVisual=selection;
            draw.DrawingVisualLists.Add(rectangle);
            var second=new DVRectangle { Rect = new(100,20,40,50) };
            draw.DrawingVisualLists.Add(second);
            Assert.Equal(2,BmwDrawingAnalysisRunner.SelectRectangles(draw).Length);
            selection.SelectVisuals.Add(rectangle);
            Assert.Single(BmwDrawingAnalysisRunner.SelectRectangles(draw));
            var provider=new BmwSfrRectangleContextMenu(null!,draw);
            Assert.Equal("BMW 四边 SFR",Assert.Single(provider.GetContextMenuItems(rectangle)).Header);
            selection.SelectVisuals.Add(second);
            Assert.Equal(2,BmwDrawingAnalysisRunner.SelectRectangles(draw,rectangle).Length);
            Assert.Single(BmwDrawingAnalysisRunner.SelectRectangles(draw,new DVRectangle()));
            Assert.Single(provider.GetContextMenuItems(rectangle));
        });
    }

    private sealed class Rectangle(Rect rect) : IRectangle
    {
        public Rect Rect { get; set; } = rect;
    }
}
