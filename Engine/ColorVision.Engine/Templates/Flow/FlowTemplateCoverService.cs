using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Compilation;
using log4net;
using Microsoft.Data.Sqlite;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Content-addressed, disposable PNG cache; never writes a flow or loads a runtime editor.</summary>
internal sealed class FlowTemplateCoverService
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowTemplateCoverService));
    private static readonly SemaphoreSlim RenderGate = new(1, 1);
    private const int RendererVersion = 1;
    internal const int PixelWidth = 640;
    internal const int PixelHeight = 360;
    internal static FlowTemplateCoverService Shared { get; } = new(LocalTemplateStore.DefaultDatabasePath);
    private readonly string databasePath;
    internal string DatabasePath => databasePath;

    internal FlowTemplateCoverService(string databasePath) => this.databasePath = Path.GetFullPath(databasePath);

    internal async Task<BitmapSource?> LoadAsync(string dataBase64, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(dataBase64)) return null;
        await RenderGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] data = Convert.FromBase64String(dataBase64);
                if (data.Length == 0) return null;
                string hash = Convert.ToHexString(SHA256.HashData(data));
                try
                {
                    using var db = Open();
                    using var command = db.CreateCommand();
                    command.CommandText = "SELECT png FROM flow_template_covers WHERE content_hash=$hash AND renderer_version=$version";
                    command.Parameters.AddWithValue("$hash", hash);
                    command.Parameters.AddWithValue("$version", RendererVersion);
                    if (command.ExecuteScalar() is byte[] cached) return Decode(cached);
                }
                catch (Exception ex) { log.Warn("Reading flow cover cache failed; regenerating the preview.", ex); }

                cancellationToken.ThrowIfCancellationRequested();
                byte[] png = Render(data);
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
                return Decode(png);
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

    internal static byte[] Render(byte[] data)
    {
        // The catalog projection reads saved geometry and edges without restoring node
        // properties, connecting live options, or calling OnEditorLoadCompleted.
        var projection = new FlowCanvasCatalogBuilder().Build(data);
        var layout = projection.SemanticDocument.Layout.Nodes;
        using var bitmap = new Bitmap(PixelWidth, PixelHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(29, 32, 37));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var gridPen = new Pen(Color.FromArgb(38, 42, 48));
        for (int x = 0; x < PixelWidth; x += 20) graphics.DrawLine(gridPen, x, 0, x, PixelHeight);
        for (int y = 0; y < PixelHeight; y += 20) graphics.DrawLine(gridPen, 0, y, PixelWidth, y);
        if (layout.Count > 0)
        {
            double left = layout.Min(node => node.X), top = layout.Min(node => node.Y);
            double width = Math.Max(1, layout.Max(node => node.X + node.Width) - left);
            double height = Math.Max(1, layout.Max(node => node.Y + node.Height) - top);
            double scale = Math.Min(1.5, Math.Min((PixelWidth - 40) / width, (PixelHeight - 40) / height));
            var rectangles = layout.ToDictionary(node => node.NodeId, node => new RectangleF(
                (float)((node.X - left) * scale + (PixelWidth - width * scale) / 2),
                (float)((node.Y - top) * scale + (PixelHeight - height * scale) / 2),
                (float)(node.Width * scale), (float)(node.Height * scale)));
            using var edgePen = new Pen(Color.FromArgb(104, 182, 114), 1.5f);
            foreach (var edge in projection.SemanticDocument.Edges)
            {
                var source = rectangles[edge.SourceNodeId];
                var target = rectangles[edge.TargetNodeId];
                var a = new PointF(source.Right, source.Top + source.Height / 2);
                var b = new PointF(target.Left, target.Top + target.Height / 2);
                float bend = Math.Max(12, Math.Abs(b.X - a.X) / 2);
                graphics.DrawBezier(edgePen, a, new PointF(a.X + bend, a.Y), new PointF(b.X - bend, b.Y), b);
            }
            var labels = projection.SearchDocuments.ToDictionary(node => node.SourceNodeGuid.ToString("D"));
            using var bodyBrush = new SolidBrush(Color.FromArgb(59, 63, 70));
            using var font = new Font("Microsoft YaHei UI", Math.Max(7, (float)(10 * scale)), GraphicsUnit.Pixel);
            using var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
            foreach (var node in projection.SemanticDocument.Nodes)
            {
                var rect = rectangles[node.NodeId];
                graphics.FillRectangle(bodyBrush, rect);
                Color color = node.TypeKey.Contains("Start", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(168, 130, 29)
                    : node.TypeKey.Contains("End", StringComparison.OrdinalIgnoreCase) ? Color.FromArgb(53, 137, 73)
                    : Color.FromArgb(48, 111, 177);
                using var titleBrush = new SolidBrush(color);
                var titleRect = new RectangleF(rect.X, rect.Y, rect.Width, Math.Min(rect.Height, Math.Max(10, (float)(22 * scale))));
                graphics.FillRectangle(titleBrush, titleRect);
                if (rect.Width < 14) continue;
                labels.TryGetValue(node.NodeId, out var label);
                string title = label?.DisplayName ?? label?.Title ?? node.TypeKey.Split('.').Last();
                titleRect.Inflate(-3, 0);
                graphics.DrawString(title, font, Brushes.White, titleRect, format);
            }
        }
        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }
}
