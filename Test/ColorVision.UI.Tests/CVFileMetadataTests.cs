using ColorVision.FileIO;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CVFileMetadataTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void UpsertPreservesPixelsUnknownPropertiesAndLegacyReaders(int version, bool cie)
    {
        string path = CreateFile(version, cie);
        try
        {
            byte[] original = File.ReadAllBytes(path);
            Assert.Empty(CVFileMetadata.Read(path));
            CVFileMetadata.SetProperty(path, "future.parameter", 73, new byte[] { 9, 4 });
            CVFileMetadata.SetProperty(path, "color", 1, new byte[8192]);
            long largeLength = new FileInfo(path).Length;
            CVFileMetadata.SetProperty(path, "color", 2, new byte[] { 1, 2, 3 });
            long smallLength = new FileInfo(path).Length;
            Assert.True(smallLength < largeLength);
            for (int i = 0; i < 20; i++) CVFileMetadata.SetProperty(path, "color", 2, new byte[] { (byte)i, 2, 3 });
            Assert.Equal(smallLength, new FileInfo(path).Length);
            var properties = CVFileMetadata.Read(path);
            Assert.Equal(2, properties.Count);
            Assert.Equal(73u, properties["future.parameter"].Version);
            Assert.Equal(new byte[] { 9, 4 }, properties["future.parameter"].Value);
            Assert.Equal(new byte[] { 19, 2, 3 }, properties["color"].Value);
            Assert.Equal(original, File.ReadAllBytes(path).Take(original.Length));
            Assert.True(CVFileUtil.Read(path, out CVCIEFile loaded));
            using (loaded) Assert.Equal(Enumerable.Range(0, cie ? 72 : 36).Select(i => (byte)i), loaded.Data);
            if (cie)
            {
                Assert.True(CVFileUtil.ReadCIEFileChannel(path, 1, out CVCIEFile channel));
                using (channel) Assert.Equal(Enumerable.Range(24, 24).Select(i => (byte)i), channel.Data);
            }
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("checksum")]
    [InlineData("truncated")]
    public void DamagedOrUnrecognizedTailIsNeverOverwritten(string damage)
    {
        string path = CreateFile(2, false);
        try
        {
            int originalLength = File.ReadAllBytes(path).Length;
            CVFileMetadata.SetProperty(path, "color", 1, new byte[] { 1, 2, 3 });
            byte[] bytes = File.ReadAllBytes(path);
            if (damage == "unknown") bytes[^64] ^= 1;
            if (damage == "checksum") bytes[originalLength + 4] ^= 1;
            if (damage == "truncated") bytes = bytes[..^1];
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => CVFileMetadata.Read(path));
            Assert.Throws<InvalidDataException>(() => CVFileMetadata.SetProperty(path, "color", 1, new byte[] { 4 }));
            Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.True(CVFileUtil.Read(path, out CVCIEFile loaded));
            loaded.Dispose();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LockedAndReadOnlyFilesFailWithoutChangingData()
    {
        string path = CreateFile(1, false);
        byte[] original = File.ReadAllBytes(path);
        try
        {
            using (FileStream locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => CVFileMetadata.SetProperty(path, "color", 1, new byte[1]));
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Assert.Throws<UnauthorizedAccessException>(() => CVFileMetadata.SetProperty(path, "color", 1, new byte[1]));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
    }

    private static string CreateFile(int version, bool cie)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-properties-{Guid.NewGuid():N}." + (cie ? "cvcie" : "cvraw"));
        using var writer = new BinaryWriter(File.Create(path), Encoding.UTF8);
        writer.Write(Encoding.ASCII.GetBytes("CVCIE"));
        writer.Write((uint)version);
        writer.Write(0); // source file name
        if (version == 3) writer.Write(2); // legacy v3 ND port
        writer.Write(1f);
        writer.Write(3);
        for (int i = 0; i < 3; i++) writer.Write(198f);
        writer.Write(3);
        writer.Write(2);
        writer.Write(cie ? 32 : 16);
        byte[] pixels = Enumerable.Range(0, cie ? 72 : 36).Select(i => (byte)i).ToArray();
        if (version == 2) writer.Write((long)pixels.Length); else writer.Write(pixels.Length);
        writer.Write(pixels);
        return path;
    }
}
