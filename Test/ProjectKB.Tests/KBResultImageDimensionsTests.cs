using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace ProjectKB.Tests;

public sealed class KBResultImageDimensionsTests
{
    [Fact]
    public void TryPopulateReadsAndStoresExistingImageDimensions()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-Dimensions-{Guid.NewGuid():N}.png");
        try
        {
            SavePng(filePath, width: 17, height: 11);
            KBItemMaster result = new() { ResultImagFile = filePath };

            bool populated = KBResultImageDimensions.TryPopulate(result);

            Assert.True(populated);
            Assert.Equal(17, result.ImageWidth);
            Assert.Equal(11, result.ImageHeight);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void TryPopulatePreservesPersistedDimensionsAfterImageDeletion()
    {
        KBItemMaster result = new()
        {
            ResultImagFile = @"Z:\deleted\historical.png",
            ImageWidth = 9680,
            ImageHeight = 5460,
        };

        bool populated = KBResultImageDimensions.TryPopulate(result);

        Assert.False(populated);
        Assert.Equal(9680, result.ImageWidth);
        Assert.Equal(5460, result.ImageHeight);
    }

    private static void SavePng(string filePath, int width, int height)
    {
        BitmapSource source = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Gray8,
            null,
            new byte[width * height],
            width);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
