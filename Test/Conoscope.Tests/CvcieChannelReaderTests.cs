using ColorVision.FileIO;
using System.IO;

namespace Conoscope.Tests;

public class CvcieChannelReaderTests
{
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    public void ReadsOnlyRequestedEmbeddedChannel(uint version)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"conoscope-channel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "sample.cvcie");

        try
        {
            float[] values = Enumerable.Range(0, 18).Select(index => index + 0.25f).ToArray();
            using (CVCIEFile source = new()
            {
                Version = version,
                FileExtType = CVType.CIE,
                Rows = 2,
                Cols = 3,
                Bpp = 32,
                Channels = 3,
                Gain = 2,
                Exp = [10, 20, 30],
                SrcFileName = "must-not-be-followed.tif",
                Data = values.SelectMany(BitConverter.GetBytes).ToArray(),
            })
            {
                Assert.True(CVFileUtil.WriteCIEFile(filePath, source));
            }

            Assert.True(CVFileUtil.ReadCIEFileChannel(filePath, 1, out CVCIEFile channel));
            using (channel)
            {
                Assert.Equal(version, channel.Version);
                Assert.Equal(2, channel.Rows);
                Assert.Equal(3, channel.Cols);
                Assert.Equal(32, channel.Bpp);
                Assert.Equal(3, channel.Channels);
                Assert.Collection(
                    channel.Exp,
                    value => Assert.Equal(10f, value),
                    value => Assert.Equal(20f, value),
                    value => Assert.Equal(30f, value));
                Assert.Equal(6 * sizeof(float), channel.Data.Length);
                Assert.Equal(values.Skip(6).Take(6), ToFloats(channel.Data));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RejectsOutOfRangeChannelWithoutReturningPayload()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"conoscope-channel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, "sample.cvcie");

        try
        {
            using (CVCIEFile source = new()
            {
                Version = 2,
                FileExtType = CVType.CIE,
                Rows = 1,
                Cols = 1,
                Bpp = 32,
                Channels = 3,
                Exp = [1, 1, 1],
                Data = new byte[3 * sizeof(float)],
            })
            {
                Assert.True(CVFileUtil.WriteCIEFile(filePath, source));
            }

            Assert.False(CVFileUtil.ReadCIEFileChannel(filePath, 3, out CVCIEFile channel));
            using (channel)
            {
                Assert.Null(channel.Data);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static float[] ToFloats(byte[] bytes)
    {
        float[] values = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
        return values;
    }
}
