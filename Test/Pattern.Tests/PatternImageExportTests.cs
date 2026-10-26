using ColorVision.UI.Tests;
using OpenCvSharp;
using System.Buffers.Binary;
using System.IO;
using System.Reflection;

namespace Pattern.Tests;

public sealed class PatternImageExportTests
{
    [Theory]
    [InlineData(PatternFormat.bmp24, 640, 480)]
    [InlineData(PatternFormat.bmp24, 7, 3)]
    [InlineData(PatternFormat.bmp1, 13, 3)]
    [InlineData(PatternFormat.bmp4, 7, 3)]
    [InlineData(PatternFormat.bmp8, 7, 3)]
    public void BmpExportWritesExplicitPixelDataLengthAndPreservesPixels(PatternFormat format, int width, int height)
    {
        string file = Path.Combine(Path.GetTempPath(), $"PatternBmp-{Guid.NewGuid():N}.bmp");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                using var source = new Mat(height, width, MatType.CV_8UC3,
                    format == PatternFormat.bmp24 ? new Scalar(17, 93, 201) : Scalar.Black);
                using (var left = new Mat(source, new Rect(0, 0, (width + 1) / 2, height)))
                    left.SetTo(Scalar.White);

                typeof(PatternWindow).GetMethod("SavePatternImage", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [source, file, format]);

                byte[] data = File.ReadAllBytes(file);
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(10, 4));
                ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(28, 2));
                int expectedBits = format switch
                {
                    PatternFormat.bmp1 => 1,
                    PatternFormat.bmp4 => 4,
                    PatternFormat.bmp8 => 8,
                    _ => 24
                };
                Assert.Equal(expectedBits, bits);
                uint expectedLength = (uint)(((width * expectedBits + 31) / 32) * 4 * height);
                Assert.Equal(expectedLength, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(34, 4)));
                Assert.Equal(offset + expectedLength, (uint)data.Length);
                using var decoded = Cv2.ImRead(file, ImreadModes.Color);
                Assert.Equal(0, Cv2.Norm(source, decoded, NormTypes.INF));
            });
        }
        finally
        {
            File.Delete(file);
        }
    }
}
