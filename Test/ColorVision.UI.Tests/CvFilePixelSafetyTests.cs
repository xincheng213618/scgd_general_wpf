using ColorVision.Engine.Media;
using ColorVision.FileIO;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CvFilePixelSafetyTests
{
    [Theory]
    [InlineData(CVType.Raw)]
    [InlineData(CVType.CIE)]
    public void DisplayRejectsLargeDimensionsWithShortArrayBeforeNativeAccess(CVType fileType)
    {
        using CVCIEFile file = new()
        {
            FileExtType = fileType,
            Cols = 14208,
            Rows = 10640,
            Bpp = 32,
            Channels = fileType == CVType.CIE ? 3 : 1,
            Data = new byte[4]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Theory]
    [InlineData(0, 1, 8, 1, 1)]
    [InlineData(1, 0, 8, 1, 1)]
    [InlineData(1, 1, 24, 1, 3)]
    [InlineData(1, 1, 8, 0, 1)]
    [InlineData(1, 1, 8, 2, 2)]
    [InlineData(1, 1, 8, 1, 2)]
    [InlineData(int.MaxValue, int.MaxValue, 64, 4, 1)]
    public void DisplayRejectsInvalidLayoutsAndOversizedPayload(int rows, int cols, int bpp, int channels, int length)
    {
        using CVCIEFile file = new()
        {
            FileExtType = CVType.Raw,
            Rows = rows, Cols = cols, Bpp = bpp, Channels = channels, Data = new byte[length]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Fact]
    public void ThreePlaneCieRequiresAllDeclaredPlanesBeforeNativeAccess()
    {
        using CVCIEFile file = new()
        {
            FileExtType = CVType.CIE,
            Rows = 1, Cols = 2, Bpp = 32, Channels = 3,
            Data = new byte[2 * sizeof(float)]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Theory]
    [InlineData(1u, 0)]
    [InlineData(1u, 1)]
    [InlineData(1u, 2)]
    [InlineData(2u, 0)]
    [InlineData(2u, 1)]
    [InlineData(2u, 2)]
    public void LegacyChannelReaderReturnsCompleteSelectedPlane(uint version, int channel)
    {
        string path = CreateFixture(version);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, channel, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(0, status);
                Assert.Equal(CVType.Raw, plane.FileExtType);
                Assert.Equal(1, plane.Channels);
                Assert.Equal(2, plane.Cols);
                Assert.Equal(1, plane.Rows);
                Assert.Equal(2 * sizeof(float), plane.Data.Length);
                Assert.Equal(channel * 2 + 1f, BitConverter.ToSingle(plane.Data, 0));
                Assert.Equal(channel * 2 + 2f, BitConverter.ToSingle(plane.Data, sizeof(float)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderRejectsTruncatedPayloadWithoutReturningPartialPixels()
    {
        string path = CreateFixture(2);
        try
        {
            using (FileStream stream = new(path, FileMode.Open, FileAccess.Write))
                stream.SetLength(stream.Length - 1);

            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderRejectsLargeMetadataWithTinyPayloadWithoutAllocatingImage()
    {
        string path = CreateFixture(2, cols: 14208, rows: 10640);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void LegacyChannelReaderRejectsInvalidChannel(int channel)
    {
        string path = CreateFixture(1);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, channel, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderKeepsHeaderFailureReturnCode()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [67, 86, 67, 73, 69]);
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-1, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderKeepsSingleChannelFailureReturnCode()
    {
        string path = CreateFixture(1, channels: 1);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateFixture(uint version, int cols = 2, int rows = 1, int channels = 3)
    {
        string path = Path.Combine(Path.GetTempPath(), $"CiePixelSafety_{Guid.NewGuid():N}.cvcie");
        float[] values = Enumerable.Range(1, channels * 2).Select(value => (float)value).ToArray();
        byte[] data = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        using CVCIEFile file = new()
        {
            Version = version,
            FileExtType = CVType.CIE,
            Cols = cols, Rows = rows, Channels = channels, Bpp = 32,
            Gain = 1, Exp = [1, 1, 1], Data = data
        };
        Assert.True(CVFileUtil.WriteCIEFile(path, file));
        return path;
    }
}
