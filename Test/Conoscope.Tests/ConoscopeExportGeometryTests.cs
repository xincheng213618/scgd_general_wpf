using ColorVision.UI;
using Conoscope.Core;
using OpenCvSharp;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Point = System.Windows.Point;

namespace Conoscope.Tests;

[CollectionDefinition("Conoscope view export", DisableParallelization = true)]
public sealed class ConoscopeViewExportCollection
{
}

[Collection("Conoscope view export")]
public sealed class ConoscopeExportGeometryTests
{
    [Fact]
    public void CappedProjectionsDoNotChangeSourcePixelsUsedByLineAndCircleExports()
    {
        RunOnStaThread(() =>
        {
            IConfigService previousConfig = ConfigService.Instance;
            ConfigService.SetInstance(new MemoryConfig());
            try
            {
                using ConoscopeView view = new();
                const int sourceSize = 4096;
                const double maxAngle = 60;
                Point sourceCenter = new(2048, 2048);
                double sourceScale = sourceSize / (maxAngle * 2);
                Mat source = new(sourceSize, sourceSize, MatType.CV_32FC1, Scalar.All(0));
                ConoscopeDocument document = (ConoscopeDocument)GetField("document").GetValue(view)!;
                typeof(ConoscopeDocument).GetProperty(nameof(ConoscopeDocument.Y))!.SetValue(document, source);
                source.Set(2048, 0, 10f);
                source.Set(2048, 1024, 20f);
                source.Set(2048, 2048, 30f);
                source.Set(2048, 3072, 40f);
                source.Set(2048, 4095, 50f);
                source.Set(1024, 2048, 60f);
                source.Set(3072, 2048, 70f);
                GetField("sourceImageCenter").SetValue(view, sourceCenter);
                GetField("sourcePixelsPerDegree").SetValue(view, sourceScale);
                GetField("currentImageCenter").SetValue(view, sourceCenter);
                GetField("currentPixelsPerDegree").SetValue(view, sourceScale);

                ConoscopeExportContext polarContext = CreateContext(view);
                (double[] polarLine, double[] polarCircle) = ExportSamples(polarContext);
                Assert.Equal(new double[] { 10, 20, 30, 40, 50 }, polarLine);
                Assert.Equal(new double[] { 40, 60, 20, 70, 40 }, polarCircle);

                foreach (ConoscopeCoordinateSystem coordinateSystem in new[]
                {
                    ConoscopeCoordinateSystem.HorizontalVertical,
                    ConoscopeCoordinateSystem.NorthPolar,
                    ConoscopeCoordinateSystem.EastPolar,
                })
                {
                    using ConoscopeHorizontalVerticalProjection projection = ConoscopeHorizontalVerticalProjection.Create(
                        sourceSize, sourceSize, sourceCenter, sourceScale, maxAngle, coordinateSystem);
                    Assert.Equal(2049, projection.OutputSize);
                    Assert.Equal(new Point(1024, 1024), projection.OutputCenter);
                    Assert.NotEqual(sourceScale, projection.OutputPixelsPerDegree);
                    GetField("currentImageCenter").SetValue(view, projection.OutputCenter);
                    GetField("currentPixelsPerDegree").SetValue(view, projection.OutputPixelsPerDegree);

                    ConoscopeExportContext projectedContext = CreateContext(view);
                    Assert.Equal(sourceSize, projectedContext.ImageWidth);
                    Assert.Equal(sourceSize, projectedContext.ImageHeight);
                    Assert.Equal(sourceCenter, projectedContext.Center);
                    Assert.Equal(sourceScale, projectedContext.PixelsPerDegree);
                    (double[] projectedLine, double[] projectedCircle) = ExportSamples(projectedContext);
                    Assert.Equal(polarLine, projectedLine);
                    Assert.Equal(polarCircle, projectedCircle);
                }
            }
            finally
            {
                ConfigService.SetInstance(previousConfig);
            }
        });
    }

    private static ConoscopeExportContext CreateContext(ConoscopeView view)
    {
        return (ConoscopeExportContext)typeof(ConoscopeView)
            .GetMethod("CreateExportContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, null)!;
    }

    private static FieldInfo GetField(string name) => typeof(ConoscopeView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static (double[] Line, double[] Circle) ExportSamples(ConoscopeExportContext context)
    {
        string linePath = Path.GetTempFileName();
        string circlePath = Path.GetTempFileName();
        try
        {
            ConoscopeExportService.ExportAzimuthCrossSection(linePath, ExportChannel.Y, context, 0,
                new ConoscopeCrossSectionExportOptions { StepDegrees = 30, IncludeMetadata = false });
            ConoscopeExportService.ExportPolarCrossSection(circlePath, ExportChannel.Y, context, 30,
                new ConoscopeCrossSectionExportOptions { StepDegrees = 90, IncludeMetadata = false });
            return (ReadValues(linePath), ReadValues(circlePath));
        }
        finally
        {
            File.Delete(linePath);
            File.Delete(circlePath);
        }
    }

    private static double[] ReadValues(string path)
    {
        return File.ReadLines(path).Skip(1).Select(line => double.Parse(line.Split(',')[1], CultureInfo.InvariantCulture)).ToArray();
    }

    private sealed class MemoryConfig : IConfigService
    {
        private readonly Dictionary<Type, IConfig> values = new();
        public IConfig GetRequiredService(Type type)
        {
            if (!values.TryGetValue(type, out IConfig? value))
                values[type] = value = (IConfig)Activator.CreateInstance(type)!;
            return value;
        }
        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA export-geometry test did not finish.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
