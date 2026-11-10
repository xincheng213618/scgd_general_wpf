#pragma warning disable CA1805,CA1822
using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Linq.Expressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 缩略图缓存管理器 - 使用SQLite存储缩略图
    /// </summary>
    public partial class ThumbnailCacheManager : IDisposable
    {
        private static ThumbnailCacheManager? _instance;
        private static readonly object _locker = new();
        // A codec can still decode full source frames while downscaling. Bound the heavy work across viewers.
        private static readonly SemaphoreSlim StandardDecodeSlots = new(2, 2);
        private static readonly Expression<Func<ThumbnailCacheEntry, ThumbnailCacheEntry>> MetadataProjection = x => new ThumbnailCacheEntry
        {
            Id = x.Id, FilePath = x.FilePath, FileLastModified = x.FileLastModified,
            ThumbnailWidth = x.ThumbnailWidth, ThumbnailHeight = x.ThumbnailHeight,
            OriginalWidth = x.OriginalWidth, OriginalHeight = x.OriginalHeight,
            FileSize = x.FileSize, CreateDate = x.CreateDate
        };
        private SqlSugarClient? _db;
        private bool _disposed = false;

        public static string DirectoryPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ColorVision", "Cache");

        public static string SqliteDbPath { get; } = Path.Combine(DirectoryPath, "ThumbnailCache.db");

        public static ThumbnailCacheManager Instance
        {
            get
            {
                lock (_locker)
                {
                    if (_instance == null || _instance._disposed)
                    {
                        _instance = new ThumbnailCacheManager();
                    }
                    return _instance;
                }
            }
        }

        private ThumbnailCacheManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                _db = OpenDatabase(SqliteDbPath);
                _cacheWriteVersion++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThumbnailCacheManager Initialize Error: {ex.Message}");
            }
        }

        private static SqlSugarClient OpenDatabase(string path)
        {
            var database = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={path}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            try
            {
                database.CodeFirst.InitTables<ThumbnailCacheEntry>();
                return database;
            }
            catch
            {
                database.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 获取或创建缩略图
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <param name="thumbnailSize">缩略图最大尺寸</param>
        /// <returns>缩略图的BitmapSource，失败返回null</returns>
        public async Task<BitmapSource?> GetOrCreateThumbnailAsync(string filePath, int thumbnailSize = 120)
            => (await GetOrCreateThumbnailWithDimensionsAsync(filePath, thumbnailSize))?.Thumbnail;

        internal Task<ThumbnailLoadResult?> GetOrCreateThumbnailWithDimensionsAsync(string filePath, int thumbnailSize,
            CancellationToken token = default)
            => LoadThumbnailAsync(filePath, thumbnailSize, true, token, this);

        internal static async Task<ThumbnailLoadResult?> LoadThumbnailAsync(string filePath, int thumbnailSize, bool useCache,
            CancellationToken token, ThumbnailCacheManager? cache = null)
        {
            token.ThrowIfCancellationRequested();
            // Fence requests already queued when maintenance invalidates outstanding writes.
            long generation = Interlocked.Read(ref _cacheMaintenanceGeneration);

            try
            {
                var prepared = await Task.Run(() => PrepareThumbnail(filePath, useCache, cache, generation, token), token);
                token.ThrowIfCancellationRequested();
                if (prepared.Source is not { } source) return null;

                // 检查缓存是否有效
                if (prepared.Entry is { ThumbnailData: { Length: > 0 } data } cached
                    && cached.RequestedThumbnailSize == thumbnailSize && cached.FileLastModified == source.LastModified
                    && cached.FileSize == source.Length)
                {
                    BitmapSource? cachedThumbnail = await LoadThumbnailFromBytesAsync(data, token);
                    token.ThrowIfCancellationRequested();
                    if (!await IsCurrentSourceAsync(filePath, source, token)) return null;
                    if (cachedThumbnail != null && cachedThumbnail.PixelWidth == cached.ThumbnailWidth && cachedThumbnail.PixelHeight == cached.ThumbnailHeight)
                        return new ThumbnailLoadResult(cachedThumbnail, cached.OriginalWidth, cached.OriginalHeight);
                }

                // Custom providers still run on the calling context and may return an unfrozen bitmap.
                ThumbnailLoadResult? generated = await GenerateThumbnailAsync(filePath, thumbnailSize, token);
                token.ThrowIfCancellationRequested();
                if (generated == null || !await IsCurrentSourceAsync(filePath, source, token)) return null;
                if (prepared.Writer is not { } writer) return generated;

                var thumbnailBytes = await EncodeThumbnailAsync(generated.Thumbnail, token);
                if (thumbnailBytes != null)
                    await writer.SaveToCacheAsync(filePath, source, thumbnailSize, thumbnailBytes, generated, generation, token);

                token.ThrowIfCancellationRequested();
                return await IsCurrentSourceAsync(filePath, source, token) ? generated : null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOrCreateThumbnailAsync Error: {ex.Message}");
                return null;
            }
        }

        private static (ThumbnailSourceStamp? Source, ThumbnailCacheEntry? Entry, ThumbnailCacheManager? Writer) PrepareThumbnail(
            string filePath, bool useCache, ThumbnailCacheManager? cache, long generation, CancellationToken token)
        {
            var source = ThumbnailSourceStamp.Read(filePath);
            if (source == null || !useCache) return (source, null, null);

            token.ThrowIfCancellationRequested();
            cache ??= Instance;
            lock (_locker)
            {
                token.ThrowIfCancellationRequested();
                if (cache._disposed) return default;
                if (cache._db == null || generation != _cacheMaintenanceGeneration) return (source, null, null);
                try
                {
                    var entry = cache._db.Queryable<ThumbnailCacheEntry>().First(x => x.FilePath == filePath);
                    return (source, entry, cache);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail cache query failed: {ex.Message}");
                    return (source, null, null);
                }
            }
        }

        private static Task<bool> IsCurrentSourceAsync(string filePath, ThumbnailSourceStamp source, CancellationToken token)
            => Task.Run(() => ThumbnailSourceStamp.Read(filePath) == source, token);

        private readonly record struct ThumbnailSourceStamp(DateTime LastModified, long Length)
        {
            public static ThumbnailSourceStamp? Read(string path)
            {
                var file = new FileInfo(path);
                return file.Exists ? new ThumbnailSourceStamp(file.LastWriteTime, file.Length) : null;
            }
        }

        /// <summary>
        /// 从缓存中获取图像信息（不加载缩略图）
        /// </summary>
        public ThumbnailCacheEntry? GetCachedInfo(string filePath)
        {
            lock (_locker)
            {
                if (_db == null || _disposed) return null;
                try
                {
                    return _db.Queryable<ThumbnailCacheEntry>().Where(x => x.FilePath == filePath).Select(MetadataProjection).First();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetCachedInfo Error: {ex.Message}");
                    return null;
                }
            }
        }

        internal IReadOnlyDictionary<string, ThumbnailCacheEntry> GetCachedInfos(IReadOnlyList<string> filePaths)
        {
            var result = new Dictionary<string, ThumbnailCacheEntry>(StringComparer.Ordinal);
            lock (_locker)
            {
                if (_db == null || _disposed) return result;
                foreach (string[] paths in filePaths.Distinct(StringComparer.Ordinal).Chunk(500))
                {
                    try
                    {
                        var rows = _db.Queryable<ThumbnailCacheEntry>().Where(x => paths.Contains(x.FilePath)).Select(MetadataProjection).ToList();
                        foreach (var group in rows.GroupBy(row => row.FilePath, StringComparer.Ordinal))
                        {
                            // Legacy caches may contain duplicate paths. Preserve the single-read choice.
                            ThumbnailCacheEntry? entry = group.Count() == 1 ? group.First() : GetCachedInfo(group.Key);
                            if (entry != null) result[group.Key] = entry;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetCachedInfos Error: {ex.Message}");
                        // One unreadable row must not hide metadata that individual reads can still load.
                        foreach (string path in paths)
                            if (GetCachedInfo(path) is { } entry) result[path] = entry;
                    }
                }
            }
            return result;
        }

        private static async Task<ThumbnailLoadResult?> GenerateThumbnailAsync(string filePath, int thumbnailSize, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var customProvider = ThumbnailProviderFactory.GetProvider(filePath);
                if (customProvider != null)
                {
                    BitmapSource? thumbnail;
                    int width, height;
                    if (customProvider is IThumbnailProviderWithDimensions combinedProvider)
                    {
                        var result = await combinedProvider.GenerateThumbnailWithDimensionsAsync(filePath, thumbnailSize, token);
                        if (result is not { } image) return null;
                        (thumbnail, width, height) = image;
                    }
                    else
                    {
                        (width, height) = customProvider.GetImageDimensions(filePath);
                        token.ThrowIfCancellationRequested();
                        thumbnail = await customProvider.GenerateThumbnailAsync(filePath, thumbnailSize);
                    }
                    token.ThrowIfCancellationRequested();
                    if (thumbnail == null) return null;
                    if (!thumbnail.IsFrozen) thumbnail = (BitmapSource)thumbnail.GetCurrentValueAsFrozen();

                    // If thumbnail succeeded but dimensions failed, try to get dimensions from thumbnail
                    if (width <= 0 || height <= 0)
                    {
                        // Calculate original dimensions from thumbnail and scale
                        double scale = Math.Min((double)thumbnailSize / thumbnail.PixelWidth, (double)thumbnailSize / thumbnail.PixelHeight);
                        if (scale > 0 && scale < 1)
                        {
                            width = (int)(thumbnail.PixelWidth / scale);
                            height = (int)(thumbnail.PixelHeight / scale);
                        }
                        else
                        {
                            width = thumbnail.PixelWidth;
                            height = thumbnail.PixelHeight;
                        }
                    }

                    return new ThumbnailLoadResult(thumbnail, width, height);
                }

                await StandardDecodeSlots.WaitAsync(token).ConfigureAwait(false);
                try { return await Task.Run(() => CreateDefaultThumbnail(filePath, thumbnailSize), token).ConfigureAwait(false); }
                finally { StandardDecodeSlots.Release(); }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateThumbnailAsync Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a thumbnail using default WPF BitmapDecoder for standard image formats.
        /// </summary>
        private static ThumbnailLoadResult CreateDefaultThumbnail(string filePath, int thumbnailSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(thumbnailSize);
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            BitmapSource frame = decoder.Frames[0];
            int width = frame.PixelWidth;
            int height = frame.PixelHeight;

            double scale = Math.Min(1.0, Math.Min((double)thumbnailSize / width, (double)thumbnailSize / height));
            int targetWidth = Math.Max(1, (int)(width * scale));
            int targetHeight = Math.Max(1, (int)(height * scale));
            if (targetWidth != width || targetHeight != height)
                frame = new TransformedBitmap(frame, new ScaleTransform((double)targetWidth / width, (double)targetHeight / height));
            if (frame.Format != PixelFormats.Pbgra32)
                frame = new FormatConvertedBitmap(frame, PixelFormats.Pbgra32, null, 0);

            // Materialize the scaled pixels while the decoder's stream is still open.
            int stride = checked(frame.PixelWidth * 4);
            byte[] pixels = new byte[checked(stride * frame.PixelHeight)];
            frame.CopyPixels(pixels, stride, 0);
            var thumbnail = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            thumbnail.Freeze();
            return new ThumbnailLoadResult(thumbnail, width, height);
        }

        private static async Task<byte[]?> EncodeThumbnailAsync(BitmapSource thumbnail, CancellationToken token)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    return ms.ToArray();
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EncodeThumbnailAsync Error: {ex.Message}");
                return null;
            }
        }

        private static async Task<BitmapSource?> LoadThumbnailFromBytesAsync(byte[] data, CancellationToken token)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var ms = new MemoryStream(data);
                    var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var result = decoder.Frames[0];
                    result.Freeze();
                    return result;
                }, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadThumbnailFromBytesAsync Error: {ex.Message}");
                return null;
            }
        }

        private async Task SaveToCacheAsync(string filePath, ThumbnailSourceStamp source, int thumbnailSize,
            byte[] thumbnailData, ThumbnailLoadResult generated, long generation, CancellationToken token)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (ThumbnailSourceStamp.Read(filePath) != source) return;
                    var entry = new ThumbnailCacheEntry
                    {
                        FilePath = filePath,
                        FileLastModified = source.LastModified,
                        ThumbnailData = thumbnailData,
                        ThumbnailWidth = generated.Thumbnail.PixelWidth,
                        ThumbnailHeight = generated.Thumbnail.PixelHeight,
                        RequestedThumbnailSize = thumbnailSize,
                        OriginalWidth = generated.OriginalWidth,
                        OriginalHeight = generated.OriginalHeight,
                        FileSize = source.Length,
                        CreateDate = DateTime.Now
                    };

                    lock (_locker)
                    {
                        if (token.IsCancellationRequested || _db == null || _disposed || generation != _cacheMaintenanceGeneration) return;
                        var updateCount = _db.Updateable(entry).Where(x => x.FilePath == filePath).ExecuteCommand();
                        if (updateCount == 0)
                            _db.Insertable(entry).ExecuteCommand();
                        _cacheWriteVersion++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveToCacheAsync Error: {ex.Message}");
                }
            }, token);
        }

        /// <summary>
        /// 获取缓存大小（字节）
        /// </summary>
        public long GetCacheSize()
        {
            try
            {
                if (File.Exists(SqliteDbPath))
                {
                    return new FileInfo(SqliteDbPath).Length;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCacheSize Error: {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// 获取缓存条目数量
        /// </summary>
        public int GetCacheCount()
        {
            lock (_locker)
            {
                if (_disposed) return 0;
                try
                {
                    return _db?.Queryable<ThumbnailCacheEntry>().Count() ?? 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GetCacheCount Error: {ex.Message}");
                }
                return 0;
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_locker)
            {
                if (_disposed) return;
                ThumbnailCacheMaintenanceResult result = ClearCacheForMaintenance(ScanCacheForMaintenance());
                if (!result.Succeeded)
                    System.Diagnostics.Debug.WriteLine($"ClearCache Error: {result.Message}");
            }
        }

        /// <summary>
        /// 删除指定文件的缓存
        /// </summary>
        public void RemoveCache(string filePath)
        {
            lock (_locker)
            {
                if (_disposed) return;
                try
                {
                    Interlocked.Increment(ref _cacheMaintenanceGeneration);
                    if (_db?.Deleteable<ThumbnailCacheEntry>().Where(x => x.FilePath == filePath).ExecuteCommand() > 0)
                        _cacheWriteVersion++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RemoveCache Error: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed) return;
                _disposed = true;
                Interlocked.Increment(ref _cacheMaintenanceGeneration);
                _db?.Dispose();
                _db = null;
                _instance = null;
            }
            GC.SuppressFinalize(this);
        }

        internal sealed record ThumbnailLoadResult(BitmapSource Thumbnail, int OriginalWidth, int OriginalHeight);

    }
}
