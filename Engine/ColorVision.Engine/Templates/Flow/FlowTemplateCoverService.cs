using ColorVision.Database;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Content-addressed, disposable PNG cache; never writes a flow or loads a runtime editor.</summary>
internal sealed class FlowTemplateCoverService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowTemplateCoverService));
    private static readonly SemaphoreSlim RenderGate = new(1, 1);
    private const int RendererVersion = 2;
    private const int MemoryCoverLimit = 64;
    internal const int PixelWidth = 640;
    internal const int PixelHeight = 360;
    internal static FlowTemplateCoverService Shared { get; } = new(LocalTemplateStore.DefaultDatabasePath);
    private readonly string databasePath;
    private readonly object memoryLock = new();
    private readonly Dictionary<(string Data, string Style), LinkedListNode<((string Data, string Style) Key, BitmapSource Image)>> memoryCovers = new();
    private readonly LinkedList<((string Data, string Style) Key, BitmapSource Image)> memoryOrder = new();
    internal string DatabasePath => databasePath;

    internal FlowTemplateCoverService(string databasePath) => this.databasePath = Path.GetFullPath(databasePath);

    internal bool TryGetCached(string dataBase64, out BitmapSource? image, FlowTemplateCoverStyle? style = null)
    {
        image = null;
        if (string.IsNullOrEmpty(dataBase64)) return true;
        var key = (dataBase64, (style ?? FlowTemplateCoverStyle.Current).CacheKey);
        lock (memoryLock)
        {
            if (!memoryCovers.TryGetValue(key, out var entry)) return false;
            memoryOrder.Remove(entry);
            memoryOrder.AddFirst(entry);
            image = entry.Value.Image;
            return true;
        }
    }

    private BitmapSource Remember(string dataBase64, BitmapSource image, FlowTemplateCoverStyle style)
    {
        var key = (dataBase64, style.CacheKey);
        lock (memoryLock)
        {
            if (memoryCovers.Remove(key, out var previous)) memoryOrder.Remove(previous);
            memoryCovers.Add(key, memoryOrder.AddFirst((key, image)));
            if (memoryCovers.Count > MemoryCoverLimit)
            {
                memoryCovers.Remove(memoryOrder.Last!.Value.Key);
                memoryOrder.RemoveLast();
            }
        }
        return image;
    }

    internal async Task<BitmapSource?> LoadAsync(string dataBase64, CancellationToken cancellationToken, FlowTemplateCoverStyle? style = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(dataBase64)) return null;
        style ??= FlowTemplateCoverStyle.Current;
        if (TryGetCached(dataBase64, out var image, style)) return image;
        await RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Another visible tile may have finished the same content while this request waited.
                if (TryGetCached(dataBase64, out var ready, style)) return ready;
                byte[] data = Convert.FromBase64String(dataBase64);
                if (data.Length == 0) return null;
                using var keyHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                keyHash.AppendData(data);
                keyHash.AppendData(Encoding.UTF8.GetBytes(style.CacheKey));
                string hash = Convert.ToHexString(keyHash.GetHashAndReset());
                try
                {
                    using var db = Open();
                    using var command = db.CreateCommand();
                    command.CommandText = "SELECT png FROM flow_template_covers WHERE content_hash=$hash AND renderer_version=$version";
                    command.Parameters.AddWithValue("$hash", hash);
                    command.Parameters.AddWithValue("$version", RendererVersion);
                    if (command.ExecuteScalar() is byte[] cached) return Remember(dataBase64, Decode(cached), style);
                }
                catch (Exception ex) { log.Warn("Reading flow cover cache failed; regenerating the preview.", ex); }

                cancellationToken.ThrowIfCancellationRequested();
                byte[] png = FlowTemplateCoverRenderer.Render(data, style);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using var db = Open();
                    using var command = db.CreateCommand();
                    command.CommandText = """
                        INSERT INTO flow_template_covers(content_hash,renderer_version,png,generated_at)
                        VALUES($hash,$version,$png,$time)
                        ON CONFLICT(content_hash) DO UPDATE SET renderer_version=$version,png=$png,generated_at=$time;
                        DELETE FROM flow_template_covers WHERE content_hash IN
                          (SELECT content_hash FROM flow_template_covers ORDER BY generated_at DESC LIMIT -1 OFFSET 1000);
                        """;
                    command.Parameters.AddWithValue("$hash", hash);
                    command.Parameters.AddWithValue("$version", RendererVersion);
                    command.Parameters.AddWithValue("$png", png);
                    command.Parameters.AddWithValue("$time", DateTime.UtcNow.ToString("O"));
                    command.ExecuteNonQuery();
                }
                catch (Exception ex) { log.Warn("Writing flow cover cache failed; showing the in-memory preview.", ex); }
                return Remember(dataBase64, Decode(png), style);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { RenderGate.Release(); }
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false, DefaultTimeout = 1 }.ToString());
        try
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS flow_template_covers (
                  content_hash TEXT PRIMARY KEY, renderer_version INTEGER NOT NULL,
                  png BLOB NOT NULL, generated_at TEXT NOT NULL);
                """;
            command.ExecuteNonQuery();
            return db;
        }
        catch { db.Dispose(); throw; }
    }

    private static BitmapSource Decode(byte[] png)
    {
        using var stream = new MemoryStream(png, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        if (image.PixelWidth != PixelWidth || image.PixelHeight != PixelHeight) throw new InvalidDataException("Invalid flow cover size.");
        image.Freeze();
        return image;
    }

}
