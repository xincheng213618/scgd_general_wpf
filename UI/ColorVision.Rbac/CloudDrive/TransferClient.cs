using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using log4net;

namespace ColorVision.Rbac.CloudDrive;

public sealed record TransferCapabilities(bool AnonymousTransferUploadEnabled, long AnonymousTransferMaxBytes);
public sealed record TransferSession(string UploadId, string Name, long TotalSize, long Offset, bool Complete, string ShareUrl, DateTimeOffset? ExpiresAt, int ChunkSize);
public sealed record TransferProgress(long Sent, long Confirmed, long Total)
{
    internal bool AwaitingConfirmation { get; init; }
    internal string? RetryNotice { get; init; }
}

public sealed class TransferClient(HttpClient http, Uri baseUri, string clientId)
{
    private const int MaxChunkSize = 1024 * 1024;
    private const int MinChunkSize = 64 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan UploadInactivityTimeout = TimeSpan.FromSeconds(60);
    private static readonly ILog Log = LogManager.GetLogger(typeof(TransferClient));
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public Task<TransferCapabilities> GetCapabilitiesAsync(CancellationToken token) => RequestAsync<TransferCapabilities>(HttpMethod.Get, "api/auth/session", null, null, token);

    public async Task UploadAsync(CloudDriveItem item, Action checkpoint, IProgress<TransferProgress>? progress, CancellationToken token)
    {
        await using var file = new FileStream(item.UploadPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        long modified = File.GetLastWriteTimeUtc(item.UploadPath).Ticks;
        // Full hashing also catches changes that preserve file size and timestamp.
        string fingerprint = Convert.ToHexStringLower(await Task.Run(() => SHA256.HashDataAsync(file, token).AsTask(), token));
        file.Position = 0;
        if (item.Fingerprint.Length > 0 && (item.Fingerprint != fingerprint || item.Size != file.Length))
            throw new IOException("原文件已变化，不能续传旧内容。请移除此记录后重新添加。");
        item.Size = file.Length;
        item.LastWriteTicks = modified;
        item.Fingerprint = fingerprint;
        checkpoint();

        void ReportRetry(string message) => progress?.Report(new(item.Offset, item.Offset, item.Size) { RetryNotice = message });

        TransferSession? session = null;
        if (!string.IsNullOrEmpty(item.UploadId))
        {
            try { session = await RetryRequestAsync(() => GetSessionAsync(item.UploadId, token), ReportRetry, token); }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                // An expired receipt cannot be safely replaced under the same remote name.
                item.UploadId = "";
                item.RemoteName = UniqueRemoteName(item.DisplayName);
                item.Offset = 0;
                checkpoint();
            }
        }
        session ??= await RetryRequestAsync(() => RequestAsync<TransferSession>(HttpMethod.Post, "api/transfer/uploads",
            JsonContent.Create(new { filename = item.RemoteName, total_size = item.Size, fingerprint }, options: JsonOptions), null, token), ReportRetry, token);
        ApplySession(item, session, checkpoint, progress);
        // Confirm smaller blocks more often so an interrupted connection has less data to resend.
        int chunkSize = Math.Clamp(session.ChunkSize, MinChunkSize, MaxChunkSize);
        var buffer = new byte[chunkSize];
        while (!session.Complete && session.Offset < item.Size)
        {
            token.ThrowIfCancellationRequested();
            long start = session.Offset;
            string uploadId = session.UploadId;
            bool mustCheckOffset = false;
            HttpRequestException? conflict = null;
            session = await RetryRequestAsync(async () =>
            {
                if (mustCheckOffset)
                {
                    // A lost response can still have committed the block. A failed lookup must never resend it.
                    ReportRetry("正在查询服务器断点…");
                    var recovered = await GetSessionAsync(uploadId, token);
                    ValidateSession(item, recovered);
                    if (recovered.Complete || recovered.Offset != start) return recovered;
                    if (conflict != null) throw conflict;
                }

                file.Position = start;
                int length = (int)Math.Min(chunkSize, item.Size - start);
                await file.ReadExactlyAsync(buffer.AsMemory(0, length), token);
                progress?.Report(new(start, start, item.Size));
                mustCheckOffset = true;
                try
                {
                    var updated = await RequestAsync<TransferSession>(HttpMethod.Patch, "api/transfer/uploads/" + uploadId,
                        new ProgressContent(buffer, length, sent => progress?.Report(new(start + sent, start, item.Size)
                        {
                            AwaitingConfirmation = sent == length
                        })), start, token);
                    ValidateSession(item, updated);
                    if (!updated.Complete && updated.Offset <= start) throw new HttpRequestException("服务器没有确认分块进度");
                    return updated;
                }
                catch (TimeoutException)
                {
                    chunkSize = Math.Max(MinChunkSize, chunkSize / 2);
                    throw;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    conflict = ex;
                    throw new HttpRequestException(ex.Message, ex);
                }
            }, message => ReportRetry(message + " 将先查询服务器断点。"), token);
            ApplySession(item, session, checkpoint, progress);
        }
        if (!session.Complete)
        {
            session = await RetryRequestAsync(() => GetSessionAsync(item.UploadId, token), ReportRetry, token);
            ApplySession(item, session, checkpoint, progress);
        }
        if (!session.Complete) throw new IOException("服务器尚未确认上传完成，请稍后继续。");
    }

    public static string UniqueRemoteName(string name)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        // Keep filenames within common filesystem limits, including multibyte characters.
        if (stem.Length > 60) stem = stem[..60];
        if (extension.Length > 15) extension = extension[..15];
        return $"{stem}_{Guid.NewGuid():N}{extension}";
    }

    internal static async Task<T> RetryRequestAsync<T>(Func<Task<T>> request, Action<string> onRetry, CancellationToken token)
    {
        int delaySeconds = 1;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return await request(); }
            catch (Exception ex) when (!token.IsCancellationRequested && IsTransient(ex))
            {
                string message = $"{ex.Message} · {delaySeconds} 秒后自动重试。";
                Log.Warn(message);
                onRetry(message);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                delaySeconds = Math.Min(delaySeconds * 2, 30);
            }
        }
    }

    private static bool IsTransient(Exception ex) => ex is TimeoutException or HttpIOException || ex is HttpRequestException httpError &&
        (httpError.StatusCode is null or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)httpError.StatusCode >= 500);

    private static void ValidateSession(CloudDriveItem item, TransferSession session)
    {
        if (!Guid.TryParseExact(session.UploadId, "N", out _) || session.Name != item.RemoteName || session.TotalSize != item.Size || session.Offset < 0 || session.Offset > item.Size ||
            (session.Complete && (session.Offset != item.Size || string.IsNullOrEmpty(session.ShareUrl))))
            throw new InvalidDataException("服务器返回的上传会话不匹配，已停止上传。");
    }

    private void ApplySession(CloudDriveItem item, TransferSession session, Action checkpoint, IProgress<TransferProgress>? progress)
    {
        ValidateSession(item, session);
        item.UploadId = session.UploadId;
        item.Offset = session.Offset;
        if (session.Complete)
        {
            var share = new Uri(baseUri, session.ShareUrl);
            if (share.Scheme != baseUri.Scheme || share.Authority != baseUri.Authority || !share.AbsolutePath.StartsWith("/transfer/share/", StringComparison.Ordinal))
                throw new InvalidDataException("服务器返回的分享地址无效。");
            item.ShareUrl = share.AbsoluteUri;
            item.ExpiresAt = session.ExpiresAt;
        }
        checkpoint();
        progress?.Report(new(session.Offset, session.Offset, item.Size));
    }

    private Task<TransferSession> GetSessionAsync(string id, CancellationToken token) =>
        RequestAsync<TransferSession>(HttpMethod.Get, "api/transfer/uploads/" + Uri.EscapeDataString(id), null, null, token);

    private async Task<T> RequestAsync<T>(HttpMethod method, string path, HttpContent? content, long? offset, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        var uploadContent = content as ProgressContent;
        TimeSpan idleTimeout = uploadContent == null ? RequestTimeout : UploadInactivityTimeout;
        timeout.CancelAfter(idleTimeout);
        if (uploadContent != null) uploadContent.OnActivity = () => timeout.CancelAfter(idleTimeout);
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path)) { Content = content };
        if (method == HttpMethod.Patch && content != null) content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        request.Headers.Add("X-Transfer-Client", clientId);
        request.Headers.Accept.ParseAdd("application/json");
        if (offset.HasValue) request.Headers.Add("Upload-Offset", offset.Value.ToString(CultureInfo.InvariantCulture));
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            return await ReadResponseAsync<T>(response, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            string operation = uploadContent == null ? "等待云盘服务器响应" : "等待分块发送或服务器确认";
            throw new TimeoutException($"{operation}超时", ex);
        }
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode)
        {
            string message = $"服务器返回 HTTP {(int)response.StatusCode}";
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    message += $"：{error.GetString()}";
            }
            catch (JsonException) { }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) message = "服务器尚未开放免账号上传，请联系管理员启用匿名文件中转。";
            if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge) message = "文件或分块超过服务器限制，请分批上传。";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, token) ?? throw new InvalidDataException("服务器返回了空的上传结果。");
    }

    private sealed class ProgressContent(byte[] buffer, int length, Action<long> progress) : HttpContent
    {
        public Action? OnActivity { get; set; }

        protected override bool TryComputeLength(out long size) { size = length; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => WriteAsync(stream, CancellationToken.None);
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken token) => WriteAsync(stream, token);
        private async Task WriteAsync(Stream stream, CancellationToken token)
        {
            for (int sent = 0; sent < length;)
            {
                int count = Math.Min(65536, length - sent);
                await stream.WriteAsync(buffer.AsMemory(sent, count), token);
                sent += count;
                OnActivity?.Invoke();
                progress(sent);
            }
        }
    }
}
