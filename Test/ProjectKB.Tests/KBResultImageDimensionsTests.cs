using ColorVision.Engine;
using SqlSugar;
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
            int databaseReads = 0;

            bool populated = KBResultImageDimensions.TryPopulate(result, _ =>
            {
                databaseReads++;
                return [];
            });

            Assert.True(populated);
            Assert.Equal(17, result.ImageWidth);
            Assert.Equal(11, result.ImageHeight);
            Assert.Equal(0, databaseReads);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void SaveUsesTheExplicitImageReaderAndLeavesMissingScopedDimensionsUnset()
    {
        DirectoryInfo files = Directory.CreateTempSubdirectory("ProjectKB-ScopedDimensions-");
        const int batchId = 31;
        string missingImage = Path.Combine(files.FullName, "capture.cvcie");
        try
        {
            using var original = Open("original");
            using var replacement = Open("replacement");
            int originalId = Seed(original, """{"width":17,"height":11}""");
            int replacementId = Seed(replacement, """{"width":53,"height":31}""");
            Assert.Equal(originalId, replacementId);
            var config = new ViewResultManagerConfig { AutoRefresh = false };
            using var manager = new ViewResultManager(config, Open("kb-results"));
            KBItemMaster first = NewResult("scoped-original");
            int originalReads = 0;
            manager.Save(first, readMeasureResults: requestedBatch =>
            {
                Assert.Equal(batchId, requestedBatch);
                originalReads++;
                return original.Queryable<MeasureResultImgModel>().Where(image => image.BatchId == requestedBatch).ToList();
            });
            manager.Save(NewResult("empty-scoped"), readMeasureResults: _ => []);
            manager.Save(NewResult("failed-scoped"), readMeasureResults: _ => throw new InvalidOperationException("Fixture reader unavailable."));

            manager.LoadAll();

            Assert.Equal(1, originalReads);
            var saved = manager.ViewResluts.Single(row => row.SN == "scoped-original");
            Assert.Equal(first.Id, saved.Id);
            Assert.Equal(17, saved.ImageWidth);
            Assert.Equal(11, saved.ImageHeight);
            Assert.All(manager.ViewResluts.Where(row => row.Id != first.Id), row =>
            {
                Assert.Null(row.ImageWidth);
                Assert.Null(row.ImageHeight);
            });
            Assert.Equal(3, manager.ViewResluts.Count);
        }
        finally
        {
            foreach (FileInfo file in files.EnumerateFiles())
                file.Delete();
            files.Delete();
        }

        KBItemMaster NewResult(string serialNumber) => new() { BatchId = batchId, SN = serialNumber, ResultImagFile = missingImage };

        SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(files.FullName, name + ".db")};Pooling=False;Default Timeout=5",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        int Seed(SqlSugarClient database, string metadata)
        {
            database.CodeFirst.InitTables<MeasureResultImgModel>();
            return database.Insertable(new MeasureResultImgModel
            {
                BatchId = batchId, FileUrl = missingImage, ImgFrameInfo = metadata, CreateDate = DateTime.Now
            }).ExecuteReturnIdentity();
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
            TaskCompletionSource<bool> loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<WriteableBitmap?> loadTask;

            using (FileStream exclusiveWriter = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                loadTask = Task.Run(() =>
                {
                    loadStarted.TrySetResult(true);
                    return ProjectKBWindow.TryLoadResultBitmap(filePath);
                });
                await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
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
            TaskCompletionSource<bool> loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<WriteableBitmap?> loadTask;

            using (FileStream writer = new(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                int firstPartLength = png.Length / 2;
                writer.Write(png, 0, firstPartLength);
                writer.Flush(flushToDisk: true);
                loadTask = Task.Run(() =>
                {
                    loadStarted.TrySetResult(true);
                    return ProjectKBWindow.TryLoadResultBitmap(filePath);
                });

                await loadStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
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
