using ColorVision.ImageEditor.Draw;
using ScottPlot;
using ScottPlot.Plottables;
using System.IO;

namespace Spectrum.Tests;

public class SpectrumPlotFillTests
{
    [Fact]
    public void GradientFillRetainsOriginalResolutionAndReplacesPreviousFrame()
    {
        Plot plot = new();
        double[] xs = Enumerable.Range(0,4001).Select(i => 380+i*0.1).ToArray();
        double[] ys = xs.Select(w => 0.001+Math.Exp(-Math.Pow((w-546.1)/0.15,2))).ToArray();
        var curve = plot.Add.Scatter(xs,ys);
        SpectrumPlotStyle.AddFill(plot,curve,_=>Colors.Green);
        Assert.Equal(2,plot.PlottableList.Count);
        var fill = plot.PlottableList.OfType<Scatter>().Single(item=>item!=curve);
        Assert.Equal(xs.Length,fill.Data.GetScatterPoints().Count);
        Assert.Equal(ys.Max(),fill.Data.GetScatterPoints().Max(p=>p.Y));
        SpectrumPlotStyle.AddFill(plot,curve,_=>Colors.Red);
        Assert.Equal(2,plot.PlottableList.Count);
        SpectrumPlotStyle.ClearFill(plot);
        Assert.Same(curve,Assert.Single(plot.PlottableList));
    }

    [Fact]
    public void FillClipsNegativeCrossingsAndVisibleBandWithoutChangingMeasurements()
    {
        Plot plot=new();
        double[] ys=[-1,1,1,1];
        var curve=plot.Add.Scatter(new double[]{370,390,770,790},ys);
        SpectrumPlotStyle.AddFill(plot,curve,_=>Colors.Blue);
        var fill=plot.PlottableList.OfType<Scatter>().Single(item=>item!=curve);
        var points=fill.Data.GetScatterPoints();
        Assert.Equal(new Coordinates(380,0),points[0]);
        Assert.Equal(new Coordinates(780,1),points[^1]);
        Assert.All(points,p=>Assert.True(p.Y>=0 && p.X>=380 && p.X<=780));
        Assert.Equal(-1,ys[0]);
        plot.Axes.SetLimits(400,700,0,2);
        var limits=plot.Axes.GetLimits();
        SpectrumPlotStyle.ApplyTheme(plot,true);
        Assert.Equal(limits,plot.Axes.GetLimits());
        SpectrumPlotStyle.ClearFill(plot);
        Assert.Same(curve,Assert.Single(plot.PlottableList));
    }

    [Fact]
    public void MissingSamplesBreakFillAndPaletteKeepsShortBandsAtTheirWavelength()
    {
        Plot plot=new();
        var curve=plot.Add.Scatter(new double[]{420,421,422,423,424,425},new double[]{1,1,double.NaN,1,1,1});
        SpectrumPlotStyle.AddFill(plot,curve,w=>new Color((byte)(w-380),0,0));
        Assert.Equal(3,plot.PlottableList.Count);
        foreach(var fill in plot.PlottableList.OfType<Scatter>().Where(item=>item!=curve))
        {
            Assert.Equal(380,fill.ColorPositions[0].Position);
            Assert.Equal(780,fill.ColorPositions[^1].Position);
            Assert.All(fill.Data.GetScatterPoints(),p=>Assert.True(double.IsFinite(p.Y)));
        }
    }
}
