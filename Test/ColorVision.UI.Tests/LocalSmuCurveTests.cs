using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class LocalSmuCurveTests
{
    [Fact]
    public void ComplianceLimitedMeasurementDoesNotInventPlateauPoints()
    {
        double[] voltages = [1, 1.9, 1.8], currents = [0.005, 0.01, 0.008];
        var view = new ViewResultSMU(MeasurementType.Voltage, 100, voltages, currents);
        var points = view.ScatterPlot.Data.GetScatterPoints();
        Assert.Equal(3, points.Count);
        Assert.Equal(voltages, points.Select(point => point.X));
        Assert.Equal(new double[] { 5, 10, 8 }, points.Select(point => point.Y));
        Assert.Same(voltages, view.VList);
        Assert.Same(currents, view.IList);
        Assert.Equal(voltages, view.SMUDatas.Select(point => point.Voltage));
        Assert.Equal(new double[] { 5, 10, 8 }, view.SMUDatas.Select(point => point.Current));
        Assert.Equal(new ViewResultSMU(MeasurementType.Voltage, 0, voltages, currents).xMax, view.xMax);
        Assert.True(view.xMax > voltages.Max());
    }

    [Theory]
    [InlineData(MeasurementType.Voltage)]
    [InlineData(MeasurementType.Current)]
    public void ManualJumpsKeepMeasurementOrderAndUseTheSelectedSourceAxis(MeasurementType source)
    {
        double[] voltages = [1.9, -0.5, 2.7, 1.2], currents = [0.01, -0.005, 0.002, 0.008];
        var view = new ViewResultSMU(source, 1000, voltages, currents);
        var points = view.ScatterPlot.Data.GetScatterPoints();
        double[] displayCurrents = [10, -5, 2, 8];
        double[] xs = source == MeasurementType.Voltage ? voltages : displayCurrents;
        double[] ys = source == MeasurementType.Voltage ? displayCurrents : voltages;
        Assert.Equal(4, points.Count);
        Assert.Equal(xs, points.Select(point => point.X));
        Assert.Equal(ys, points.Select(point => point.Y));
        Assert.True(view.xMin < xs.Min() && view.xMax > xs.Max());
        Assert.True(view.yMin < ys.Min() && view.yMax > ys.Max());
        var opposite = new ViewResultSMU(source == MeasurementType.Voltage ? MeasurementType.Current : MeasurementType.Voltage, 0, voltages, currents);
        Assert.Equal(opposite.yMin, view.xMin);
        Assert.Equal(opposite.yMax, view.xMax);
        Assert.Equal(opposite.xMin, view.yMin);
        Assert.Equal(opposite.xMax, view.yMax);
        Assert.Equal(voltages, view.VList);
        Assert.Equal(currents, view.IList);
        Assert.Equal(voltages, view.SMUDatas.Select(point => point.Voltage));
        Assert.Equal(displayCurrents, view.SMUDatas.Select(point => point.Current));
    }

    [Fact]
    public void HistoricalCurrentSourceKeepsChannelAndRawAmpsWhileDisplayingMilliamps()
    {
        double[] voltages = [1.9, 1.8], currents = [0.01, 0.02];
        var view = new ViewResultSMU(new SmuScanModel
        {
            ChannelType = SMUChannelType.B, IsSourceV = false, SrcBegin = 10, SrcEnd = 1000,
            VResult = JsonConvert.SerializeObject(voltages), IResult = JsonConvert.SerializeObject(currents)
        });
        Assert.Equal(SMUChannelType.B, view.ChannelType);
        Assert.Equal(MeasurementType.Current, view.MeasurementType);
        var points = view.ScatterPlot.Data.GetScatterPoints();
        Assert.Equal(new double[] { 10, 20 }, points.Select(point => point.X));
        Assert.Equal(voltages, points.Select(point => point.Y));
        Assert.Equal(new ViewResultSMU(MeasurementType.Voltage, 0, voltages, currents).yMax, view.xMax);
        Assert.Equal(currents, view.IList);
        Assert.Equal(new double[] { 10, 20 }, view.SMUDatas.Select(point => point.Current));
    }

    [Theory]
    [InlineData(MeasurementType.Voltage, false)]
    [InlineData(MeasurementType.Current, false)]
    [InlineData(MeasurementType.Voltage, true)]
    [InlineData(MeasurementType.Current, true)]
    public void EmptyAndSinglePointCurvesHaveFiniteNondegenerateRangesWithoutAddingMeasurements(MeasurementType source, bool singlePoint)
    {
        double[] voltages = singlePoint ? [0] : [], currents = singlePoint ? [0.001] : [];
        var view = new ViewResultSMU(source, 1000, voltages, currents);
        Assert.Equal(singlePoint ? 1 : 0, view.ScatterPlot.Data.GetScatterPoints().Count);
        Assert.Equal(singlePoint ? 1 : 0, view.SMUDatas.Count);
        Assert.All(new[] { view.xMin, view.xMax, view.yMin, view.yMax }, value => Assert.True(double.IsFinite(value)));
        Assert.True(view.xMin < view.xMax);
        Assert.True(view.yMin < view.yMax);
        if (singlePoint)
        {
            double x = source == MeasurementType.Voltage ? 0 : 1;
            double y = source == MeasurementType.Voltage ? 1 : 0;
            Assert.True(view.xMin < x && view.xMax > x);
            Assert.True(view.yMin < y && view.yMax > y);
        }
    }

    [Fact]
    public void ScanAndSinglePointShowTheSameTwelveMilliampMeasurementWithoutChangingRawUnits()
    {
        var scan = new ViewResultSMU(MeasurementType.Voltage, 5, [1.9], [0.012]);
        var point = new ViewResultSMU(new SMUResultModel { IsSourceV = true, VResult = 1.9f, IResult = 12 });
        Assert.Equal(12, Assert.Single(scan.ScatterPlot.Data.GetScatterPoints()).Y);
        Assert.Equal(12, Assert.Single(point.ScatterPlot.Data.GetScatterPoints()).Y);
        Assert.Equal(12, Assert.Single(scan.SMUDatas).Current);
        Assert.Equal(12, Assert.Single(point.SMUDatas).Current);
        Assert.Equal(new double[] { 0.012 }, scan.IList);
        Assert.Equal(new double[] { 12 }, point.IList);
        scan.Gen();
        point.Gen();
        Assert.Equal(12, Assert.Single(scan.ScatterPlot.Data.GetScatterPoints()).Y);
        Assert.Equal(12, Assert.Single(point.ScatterPlot.Data.GetScatterPoints()).Y);
    }
}
