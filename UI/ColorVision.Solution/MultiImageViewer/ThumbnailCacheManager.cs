using ColorVision.UI;
using SqlSugar;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 缩略图缓存管理器 - 使用SQLite存储缩略图
    /// </summary>
    public class ThumbnailCacheManager : IDisposable
    {
        private static ThumbnailCacheManager? _instance;
        private static readonly object _locker = new();
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

                _db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={SqliteDbPath}",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true
                });

                _db.CodeFirst.InitTables<ThumbnailCacheEntry>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThumbnailCacheManager Initialize Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取或创建缩略图
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <param name="thumbnailSize">缩略图最大尺寸</param>
        /// <returns>缩略图的BitmapSource，失败返回null</returns>
        public async Task<BitmapSource?> GetOrCreateThumbnailAsync(string filePath, int thumbnailSize = 120)
        {
            if (_db == null || _disposed || !File.Exists(filePath))
                return null;

            try
            {
                var fileInfo = new FileInfo(filePath);
                var lastModified = fileInfo.LastWriteTime;

                // 查找缓存
                var cached = await Task.Run(() =>
                    _db.Queryable<ThumbnailCacheEntry>()
                       .First(x => x.FilePath == filePath));

                // 检查缓存是否有效
                if (cached != null && cached.FileLastModified == lastModified && cached.ThumbnailData != null)
                {
                    // 从缓存加载
                    return await LoadThumbnailFromBytesAsync(cached.ThumbnailData);
                }

                // 创建新缩略图
                var (thumbnail, originalWidth, originalHeight) = await CreateThumbnailAsync(filePath, thumbnailSize);
                if (thumbnail == null)
                    return null;

                // 保存到缓存
                var thumbnailBytes = await EncodeThumbnailAsync(thumbnail);
                if (thumbnailBytes != null)
                {
                    await SaveToCacheAsync(filePath, fileInfo, thumbnailBytes, thumbnail, originalWidth, originalHeight);
                }

                return thumbnail;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetOrCreateThumbnailAsync Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从缓存中获取图像信息（不加载缩略图）
        /// </summary>
        public ThumbnailCacheEntry? GetCachedInfo(string filePath)
        {
            if (_db == null || _disposed) return null;

            try
            {
                return _db.Queryable<ThumbnailCacheEntry>()
                          .First(x => x.FilePath == filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetCachedInfo Error: {ex.Message}");
                return null;
            }
        }

        private async Task<(BitmapSource? thumbnail, int width, int height)> CreateThumbnailAsync(string filePath, int thumbnailSize)
        {
            try
            {
                // Check if there's a custom thumbnail provider for this file type
                var customProvider = ThumbnailProviderFactory.GetProvider(filePath);
                if (customProvider != null)
                {
                    var (width, height) = customProvider.GetImageDimensions(filePath);
                    var thumbnail = await customProvider.GenerateThumbnailAsync(filePath, thumbnailSize);
                    return (thumbnail, width, height);
                }

                // Fall back to default WPF thumbnail generation for standard image formats
                return await CreateDefaultThumbnailAsync(filePath, thumbnailSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateThumbnailAsync Error: {ex.Message}");
                return (null, 0, 0);
            }
        }

        /// <summary>
        /// Creates a thumbnail using default WPF BitmapDecoder for standard image formats.
        /// </summary>
        private async Task<(BitmapSource? thumbnail, int width, int height)> CreateDefaultThumbnailAsync(string filePath, int thumbnailSize)
        {
            try
            {
                BitmapSource? result = null;
                int width = 0, height = 0;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    var frame = decoder.Frames[0];

                    width = frame.PixelWidth;
                    height = frame.PixelHeight;

                    // 计算缩略图尺寸
                    double scale = Math.Min((double)thumbnailSize / width, (double)thumbnailSize / height);
                    scale = Math.Min(scale, 1.0); // 不放大小图
                    int thumbWidth = (int)(width * scale);
                    int thumbHeight = (int)(height * scale);

                    var thumbnail = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                    var rtb = new RenderTargetBitmap(thumbWidth, thumbHeight, 96, 96, PixelFormats.Pbgra32);
                    var dv = new DrawingVisual();
                    using (var dc = dv.RenderOpen())
                    {
                        dc.DrawImage(thumbnail, new Rect(0, 0, thumbWidth, thumbHeight));
                    }
                    rtb.Render(dv);
                    rtb.Freeze();
                    result = rtb;
                });

                return (result, width, height);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateDefaultThumbnailAsync Error: {ex.Message}");
                return (null, 0, 0);
            }
        }

        private async Task<byte[]?> EncodeThumbnailAsync(BitmapSource thumbnail)
        {
            try
            {
                byte[]? result = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                    using var ms = new MemoryStream();
                    encoder.Save(ms);
                    result = ms.ToArray();
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EncodeThumbnailAsync Error: {ex.Message}");
                return null;
            }
        }

        private async Task<BitmapSource?> LoadThumbnailFromBytesAsync(byte[] data)
        {
            try
            {
                BitmapSource? result = null;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    using var ms = new MemoryStream(data);
                    var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    result = decoder.Frames[0];
                    result.Freeze();
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadThumbnailFromBytesAsync Error: {ex.Message}");
                return null;
            }
        }

        private async Task SaveToCacheAsync(string filePath, FileInfo fileInfo, byte[] thumbnailData,
            BitmapSource thumbnail, int originalWidth, int originalHeight)
        {
            if (_db == null || _disposed) return;

            await Task.Run(() =>
            {
                try
                {
                    var entry = new ThumbnailCacheEntry
                    {
                        FilePath = filePath,
                        FileLastModified = fileInfo.LastWriteTime,
                        ThumbnailData = thumbnailData,
                        ThumbnailWidth = thumbnail.PixelWidth,
                        ThumbnailHeight = thumbnail.PixelHeight,
                        OriginalWidth = originalWidth,
                        OriginalHeight = originalHeight,
                        FileSize = fileInfo.Length,
                        CreateDate = DateTime.Now
                    };

                    // 使用事务确保原子性UPSERT操作
                    _db?.Ado.BeginTran();
                    try
                    {
                        // 先尝试更新现有记录
                        var updateCount = _db?.Updateable(entry)
                            .Where(x => x.FilePath == filePath)
                            .ExecuteCommand() ?? 0;

                        // 如果更新没有影响任何行，则插入新记录
                        if (updateCount == 0)
                        {
                            _db?.Insertable(entry).ExecuteCommand();
                        }

                        _db?.Ado.CommitTran();
                    }
                    catch
                    {
                        _db?.Ado.RollbackTran();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveToCacheAsync Error: {ex.Message}");
                }
            });
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

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            if (_disposed) return;

            try
            {
                _db?.Deleteable<ThumbnailCacheEntry>().ExecuteCommand();

                // 执行SQLite的VACUUM来释放空间
                _db?.Ado.ExecuteCommand("VACUUM");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearCache Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除指定文件的缓存
        /// </summary>
        public void RemoveCache(string filePath)
        {
            if (_disposed) return;

            try
            {
                _db?.Deleteable<ThumbnailCacheEntry>()
                    .Where(x => x.FilePath == filePath)
                    .ExecuteCommand();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveCache Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (_locker)
            {
                if (_disposed) return;
                _disposed = true;
                _db?.Dispose();
                _db = null;
                _instance = null;
            }
            GC.SuppressFinalize(this);
        }

        ~ThumbnailCacheManager()
        {
            Dispose();
        }
    }
}
