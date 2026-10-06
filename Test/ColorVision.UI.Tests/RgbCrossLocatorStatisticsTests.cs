using ColorVision.ImageEditor.Algorithms;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossLocatorStatisticsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(100001)]
    public void LocatorUpperMedianAndNoiseMatchFullSorting(int length)
    {
        var select = typeof(DisplayMetrologyProvider).GetMethod("SelectCrossUpperMedian", BindingFlags.Static | BindingFlags.NonPublic)!;
        float Select(float[] values) => (float)select.Invoke(null, [values, CancellationToken.None])!;
        var random = new Random(173);
        foreach (double exponent in new[] { 0.1, 1.0, 2.2, 5.0 })
            for (int distribution = 0; distribution < 8; distribution++)
            {
                var signal = new float[length];
                for (int i = 0; i < length; i++)
                {
                    double value = distribution switch
                    {
                        0 => 0,
                        1 => random.Next(65536) / 65535d,
                        2 => i % 4 == 0 ? 1 : 0,
                        3 => random.Next(4) / 3d,
                        4 => (double)i / length,
                        5 => (double)(length - i) / length,
                        6 => (double)Math.Min(i, length - 1 - i) / length,
                        _ => random.NextDouble(),
                    };
                    signal[i] = (float)Math.Pow(value, exponent);
                }
                var ordered = (float[])signal.Clone();
                Array.Sort(ordered);
                float expectedMedian = ordered[length / 2];
                var work = (float[])signal.Clone();
                float median = Select(work);
                Assert.Equal(expectedMedian, median);
                double center = expectedMedian;
                var expectedDeviations = signal.Select(value => (float)Math.Abs(value - center)).ToArray();
                Array.Sort(expectedDeviations);
                for (int i = 0; i < work.Length; i++) work[i] = (float)Math.Abs(signal[i] - (double)median);
                Assert.Equal(expectedDeviations[length / 2], Select(work));
            }
    }
}
