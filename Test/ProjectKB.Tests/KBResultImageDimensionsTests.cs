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

    [Fact]
    public void TryLoadResultBitmapAllowsConcurrentReadersAndReleasesSourceFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-SharedImage-{Guid.NewGuid():N}.png");
        try
        {
            SavePng(filePath, width: 17, height: 11);
            WriteableBitmap? bitmap;

            using (FileStream concurrentReader = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                bitmap = ProjectKBWindow.TryLoadResultBitmap(filePath);
                File.Delete(filePath);
            }

            Assert.NotNull(bitmap);
            Assert.Equal(17, bitmap.PixelWidth);
            Assert.Equal(11, bitmap.PixelHeight);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task TryLoadResultBitmapRetriesUntilExclusiveWriterReleasesSourceFile()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-LockedImage-{Guid.NewGuid():N}.png");
        try
        {
            SavePng(filePath, width: 19, height: 13);
            using ManualResetEventSlim loadStarted = new();
            Task<WriteableBitmap?> loadTask;

            using (FileStream exclusiveWriter = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                loadTask = Task.Run(() =>
                {
                    loadStarted.Set();
                    return ProjectKBWindow.TryLoadResultBitmap(filePath);
                });
                Assert.True(loadStarted.Wait(TimeSpan.FromSeconds(1)));
                await Task.Delay(250);
                Assert.False(loadTask.IsCompleted);
            }

            WriteableBitmap? bitmap = await loadTask.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(bitmap);
            Assert.Equal(19, bitmap.PixelWidth);
            Assert.Equal(13, bitmap.PixelHeight);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task TryLoadResultBitmapWaitsForSharedWriterAndLoadsCompletePng()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-SharedWriter-{Guid.NewGuid():N}.png");
        byte[] png = CreatePng(width: 23, height: 15);
        try
        {
            using ManualResetEventSlim loadStarted = new();
            Task<WriteableBitmap?> loadTask;

            using (FileStream writer = new(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                int firstPartLength = png.Length / 2;
                writer.Write(png, 0, firstPartLength);
                writer.Flush(flushToDisk: true);
                loadTask = Task.Run(() =>
                {
                    loadStarted.Set();
                    return ProjectKBWindow.TryLoadResultBitmap(filePath);
                });

                Assert.True(loadStarted.Wait(TimeSpan.FromSeconds(1)));
                await Task.Delay(250);
                Assert.False(loadTask.IsCompleted);

                writer.Write(png, firstPartLength, png.Length - firstPartLength);
                writer.Flush(flushToDisk: true);
            }

            WriteableBitmap? bitmap = await loadTask.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(bitmap);
            Assert.Equal(23, bitmap.PixelWidth);
            Assert.Equal(15, bitmap.PixelHeight);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public async Task TryLoadResultBitmapRetriesIncompletePngBetweenWriterHandles()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-IncompleteImage-{Guid.NewGuid():N}.png");
        byte[] png = CreatePng(width: 29, height: 17);
        try
        {
            File.WriteAllBytes(filePath, png[..(png.Length / 2)]);
            Task<WriteableBitmap?> loadTask = Task.Run(() => ProjectKBWindow.TryLoadResultBitmap(filePath));

            await Task.Delay(250);
            Assert.False(loadTask.IsCompleted);
            File.WriteAllBytes(filePath, png);

            WriteableBitmap? bitmap = await loadTask.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.NotNull(bitmap);
            Assert.Equal(29, bitmap.PixelWidth);
            Assert.Equal(17, bitmap.PixelHeight);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static void SavePng(string filePath, int width, int height)
    {
        File.WriteAllBytes(filePath, CreatePng(width, height));
    }

    private static byte[] CreatePng(int width, int height)
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
        using MemoryStream stream = new();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
