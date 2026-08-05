using ColorVision.Engine.Services.POI;

namespace ColorVision.UI.Tests;

public sealed class PoiMeasurementServiceTests
{
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
