using ColorVision.UI;
using Conoscope.Core;
using OpenCvSharp;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Point = System.Windows.Point;

namespace Conoscope.Tests;

[Collection("Conoscope view export")]
public sealed class ConoscopeCrossSectionWorkflowTests
{
    [Fact]
    public void FixedCoordinatesFreezeValuesAndKeepProjectionAndAxisMeaningDistinct()
    {
        WithView(view =>
        {
            view.State.CoordinateSystem = ConoscopeCoordinateSystem.HorizontalVertical;
            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedHorizontal);
            view.SetReferenceValue(20);
            Invoke(view, "UpdateHorizontalVerticalReference");
            ConoscopeCurveSnapshot first = Assert.IsType<ConoscopeCurveSnapshot>(view.CreateCurrentCurveSnapshot());
            Assert.Equal("HorizontalVertical:V-degrees", first.AxisKey);
            Assert.Contains("SourceXYZ=Bilinear", first.Metadata);
            Assert.Equal(1201, first.Positions.Count);
            Assert.Equal(-60, first.Positions[0]);
            Assert.Equal(60, first.Positions[^1]);
            Assert.True(double.IsNaN(first.Values[0]));
            Assert.True(double.IsNaN(first.Values[^1]));
            double original = first.Values[600];
            Assert.Equal(1260, original, 6); // H=20,V=0 -> source (160,120), Y=x+2y+860.

            view.SetReferenceValue(-20);
            Invoke(view, "UpdateHorizontalVerticalReference");
            ConoscopeCurveSnapshot moved = view.CreateCurrentCurveSnapshot()!;
            Assert.True(first.IsCompatibleWith(moved));
            Assert.Equal(1180, moved.Values[600], 6);
            view.YMat!.Set(120, 160, 9f);
            Assert.Equal(original, first.Values[600]);

            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedVertical);
            Invoke(view, "UpdateHorizontalVerticalReference");
            Assert.False(first.IsCompatibleWith(view.CreateCurrentCurveSnapshot()!));
            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedHorizontal);
            view.State.CoordinateSystem = ConoscopeCoordinateSystem.NorthPolar;
            Invoke(view, "UpdateHorizontalVerticalReference");
            Assert.False(first.IsCompatibleWith(view.CreateCurrentCurveSnapshot()!));
        });
    }

    [Fact]
    public void CurrentHvExportMatchesLiveSamplingAndPreservesGapsInCommaDecimalCulture()
    {
        WithView(view =>
        {
            view.State.CoordinateSystem = ConoscopeCoordinateSystem.EastPolar;
            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedVertical);
            view.SetReferenceValue(-23.45);
            Invoke(view, "UpdateHorizontalVerticalReference");
            ConoscopeCurveSnapshot snapshot = view.CreateCurrentCurveSnapshot()!;
            var context = (ConoscopeExportContext)Invoke(view, "CreateExportContext")!;
            string file = System.IO.Path.GetTempFileName();
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                Invoke(view, "WriteHorizontalVerticalCsv", file, ExportChannel.Y, context, -23.45,
                    new ConoscopeCrossSectionExportOptions { StepDegrees = 0.1, DecimalPlaces = 8, IncludeMetadata = true });
                string[] lines = System.IO.File.ReadAllLines(file);
                Assert.Contains(lines, line => line.Contains("EastPolar", StringComparison.Ordinal));
                Assert.Contains(lines, line => line.Contains("V=-23.45", StringComparison.Ordinal));
                string[] rows = lines.Where(line => !line.StartsWith('#')).Skip(1).ToArray();
                Assert.Equal(snapshot.Values.Count, rows.Length);
                for (int i = 0; i < rows.Length; i++)
                {
                    string[] cells = rows[i].Split(',');
                    Assert.Equal(6, cells.Length);
                    Assert.Equal(snapshot.Positions[i], double.Parse(cells[0], CultureInfo.InvariantCulture));
                    Assert.Equal(-23.45, double.Parse(cells[1], CultureInfo.InvariantCulture));
                    double exported = double.Parse(cells[2], CultureInfo.InvariantCulture);
                    if (double.IsNaN(snapshot.Values[i])) { Assert.True(double.IsNaN(exported)); Assert.Equal("0", cells[3]); }
                    else { Assert.Equal(snapshot.Values[i].ToString("F8", CultureInfo.InvariantCulture), cells[2]); Assert.Equal("1", cells[3]); }
                }
            }
            finally { System.IO.File.Delete(file); }
        });
    }

    [Fact]
    public void InvalidInputsCannotChangeFixedAngleOrEnableHvInPolar()
    {
        WithView(view =>
        {
            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedHorizontal);
            Assert.Equal(ConoscopeCoordinateReferenceMode.AzimuthLine, view.State.CoordinateAxis.ReferenceMode);
            view.State.CoordinateSystem = ConoscopeCoordinateSystem.HorizontalVertical;
            view.SetReferenceMode(ConoscopeCoordinateReferenceMode.FixedHorizontal);
            view.SetReferenceValue(12.5);
            view.SetReferenceValue(double.NaN);
            view.SetReferenceValue(double.PositiveInfinity);
            Assert.Equal(12.5, view.State.CoordinateAxis.ReferenceHorizontalAngle);
            view.SetReferenceValue(100);
            Assert.Equal(60, view.State.CoordinateAxis.ReferenceHorizontalAngle);
        });
    }

    [Fact]
    public void MissingSourceNumbersRemainGapsForDerivedChannels()
    {
        WithView(view =>
        {
            var sample = new ConoscopeHorizontalVerticalSample(0, 0, 0, 120, 120,
                new ConoscopeXyzValue(double.NaN, 20, 10), true);
            foreach (ExportChannel channel in new[] { ExportChannel.CieX, ExportChannel.CieY, ExportChannel.CieU, ExportChannel.CieV, ExportChannel.ColorDifference })
                Assert.True(double.IsNaN((double)Invoke(view, "GetHorizontalVerticalValue", sample, channel)!));
            Assert.Equal(20d, Invoke(view, "GetHorizontalVerticalValue", sample, ExportChannel.Y));
            var infinite = sample with { Xyz = new ConoscopeXyzValue(10, double.PositiveInfinity, 10) };
            Assert.True(double.IsNaN((double)Invoke(view, "GetHorizontalVerticalValue", infinite, ExportChannel.Y)!));
        });
    }

    private static object? Invoke(ConoscopeView view, string name, params object[] args)
        => typeof(ConoscopeView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(view, args);

    private static void WithView(Action<ConoscopeView> action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            IConfigService previous = ConfigService.Instance;
            try
            {
                ConfigService.SetInstance(new MemoryConfig());
                using ConoscopeView view = new();
                var document = (ConoscopeDocument)typeof(ConoscopeView).GetField("document", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
                Mat y = new(241, 241, MatType.CV_32FC1);
                for (int row = 0; row < 241; row++)
                    for (int col = 0; col < 241; col++) y.Set(row, col, (float)(col + 2 * row + 860));
                typeof(ConoscopeDocument).GetProperty(nameof(ConoscopeDocument.Y))!.SetValue(document, y);
                typeof(ConoscopeView).GetField("sourceImageCenter", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, new Point(120, 120));
                typeof(ConoscopeView).GetField("sourcePixelsPerDegree", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(view, 2d);
                action(view);
            }
            catch (Exception ex) { failure = ex; }
            finally { ConfigService.SetInstance(previous); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Cross-section workflow did not finish.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class MemoryConfig : IConfigService
    {
        private readonly Dictionary<Type, IConfig> values = new();
        public IConfig GetRequiredService(Type type) => values.TryGetValue(type, out IConfig? value) ? value : values[type] = (IConfig)Activator.CreateInstance(type)!;
        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
