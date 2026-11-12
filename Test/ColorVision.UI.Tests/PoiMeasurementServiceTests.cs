using ColorVision.Engine.Services.POI;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class PoiMeasurementServiceTests
{
    [Fact]
    public void CalculateRawPreservesNonPositiveXyzWhileLegacyCalculationStillReplacesThem()
    {
        using PoiMeasurementBuffer buffer = new(CreateConstantPlanarData(5, 5, -1, 0, 3), 5, 5, 32, 3);
        PoiMeasurementPoint[] points =
        [
            new(2, 2, 1, 1, PoiMeasurementShape.Point),
            new(2, 2, 4, 4, PoiMeasurementShape.Circle),
            new(2, 2, 2, 2, PoiMeasurementShape.Rect)
        ];
        foreach (PoiMeasurementResult raw in PoiMeasurementService.CalculateRaw(buffer, points))
        {
            Assert.Equal(-1, raw.X);
            Assert.Equal(0, raw.Y);
            Assert.Equal(3, raw.Z);
            Assert.Equal(-0.5f, raw.ChromaX);
            Assert.Equal(0, raw.ChromaY);
            Assert.Equal(-0.5f, raw.U);
        }
        Assert.True(PoiMeasurementService.Calculate(buffer, points[0]).X > 0);
        using PoiMeasurementBuffer luminance = new(CreateConstantPlanarData(1, 1, -2), 1, 1, 32, 1);
        Assert.Equal(-2, PoiMeasurementService.CalculateRaw(luminance,
            new[] { new PoiMeasurementPoint(0, 0, 1, 1, PoiMeasurementShape.Point) })[0].Y);
    }

    [Fact]
    public void Calculate_UsesManagedPlanarBufferForAllStandardShapes()
    {
        const int width = 7;
        const int height = 5;
        byte[] data = CreateConstantPlanarData(width, height, 2, 3, 4);
        using PoiMeasurementBuffer buffer = new(data, width, height, 32, 3);
        PoiMeasurementPoint[] points =
        {
            new(3, 2, 1, 1, PoiMeasurementShape.Point),
            new(3, 2, 4, 4, PoiMeasurementShape.Circle),
            new(3, 2, 4, 2, PoiMeasurementShape.Rect)
        };

        PoiMeasurementResult[] results = PoiMeasurementService.Calculate(buffer, points);

        Assert.Equal(points.Length, results.Length);
        foreach (PoiMeasurementResult result in results)
        {
            Assert.InRange(result.X, 1.99999f, 2.00001f);
            Assert.InRange(result.Y, 2.99999f, 3.00001f);
            Assert.InRange(result.Z, 3.99999f, 4.00001f);
        }
    }

    [Fact]
    public void Calculate_SingleChannelPointReturnsY()
    {
        float[] values =
        {
            1, 2, 3,
            4, 5, 6,
            7, 8, 9
        };
        byte[] data = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        using PoiMeasurementBuffer buffer = new(data, 3, 3, 32, 1);

        PoiMeasurementResult result = PoiMeasurementService.Calculate(
            buffer,
            new PoiMeasurementPoint(1, 1, 1, 1, PoiMeasurementShape.Point));

        Assert.InRange(result.Y, 4.99999f, 5.00001f);
        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Z);
    }

    [Fact]
    public void Calculate_RejectsDisposedManagedBuffer()
    {
        PoiMeasurementBuffer buffer = new(CreateConstantPlanarData(2, 2, 1), 2, 2, 32, 1);
        buffer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => PoiMeasurementService.Calculate(
            buffer,
            new PoiMeasurementPoint(0, 0, 1, 1, PoiMeasurementShape.Point)));
    }

    [Fact]
    public async Task SharedPlanarDataSurvivesOwnerDisposalWhileTheWorkerBorrowsTheSameStorage()
    {
        byte[] pixels = CreateConstantPlanarData(3, 2, -1, 0, 3);
        using var owner = new PoiMeasurementBuffer(pixels, 3, 2, 32, 3);
        using var snapshot = Assert.IsType<PoiMeasurementBuffer>(owner.SharePlanarData());
        Assert.NotSame(owner, snapshot);
        Assert.Equal((owner.Width, owner.Height, owner.BitsPerChannel, owner.Channels),
            (snapshot.Width, snapshot.Height, snapshot.BitsPerChannel, snapshot.Channels));

        var entered = new TaskCompletionSource<(IntPtr Pointer, long Length)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        Task<byte[]> worker = Task.Run(() => snapshot.Borrow((pointer, length) =>
        {
            entered.SetResult((pointer, length));
            if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("POI worker borrow did not release.");
            byte[] observed = new byte[checked((int)length)];
            Marshal.Copy(pointer, observed, 0, observed.Length);
            return observed;
        }));
        Task? retirement = null;
        try
        {
            var borrowed = await entered.Task.WaitAsync(timeout);
            Dispatcher dispatcher = WpfTestHost.Invoke(() => Dispatcher.CurrentDispatcher);
            retirement = dispatcher.InvokeAsync(() =>
            {
                Assert.Equal(borrowed, owner.Borrow((pointer, length) => (pointer, length)));
                owner.Dispose();
                Assert.Throws<ObjectDisposedException>(() => owner.Borrow((_, _) => 0));
            }).Task;
            await retirement.WaitAsync(timeout);
            Assert.False(worker.IsCompleted);
            release.Set();
            Assert.Equal(pixels, await worker.WaitAsync(timeout));
        }
        finally
        {
            release.Set();
            await worker.WaitAsync(timeout);
            if (retirement != null) await retirement.WaitAsync(timeout);
        }
    }

    private static byte[] CreateConstantPlanarData(int width, int height, params float[] channelValues)
    {
        int planeLength = checked(width * height);
        float[] values = new float[checked(planeLength * channelValues.Length)];
        for (int channel = 0; channel < channelValues.Length; channel++)
        {
            Array.Fill(values, channelValues[channel], channel * planeLength, planeLength);
        }
        byte[] data = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        return data;
    }
}
