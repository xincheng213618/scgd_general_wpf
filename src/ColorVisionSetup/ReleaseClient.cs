using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVisionSetup
{
    internal sealed class TransferProgress
    {
        internal long Received { get; }
        internal long? Total { get; }
        internal double BytesPerSecond { get; }
        internal TransferProgress(long received, long? total, double speed) { Received = received; Total = total; BytesPerSecond = speed; }
    }

    internal sealed class ReleaseClient : IDisposable
    {
        // Same public endpoints as MarketplaceConfig. No embedded credentials or host-app dependency.
        internal const string ServiceBaseUrl = "http://xc213618.ddns.me:9998/";
        private const long MaximumPackageBytes = 4L * 1024 * 1024 * 1024;
        private readonly HttpClient _http;
        internal ReleaseClient() : this(new HttpClientHandler()) { }
        internal ReleaseClient(HttpMessageHandler handler)
        {
            _http = new HttpClient(handler) { BaseAddress = new Uri(ServiceBaseUrl), Timeout = Timeout.InfiniteTimeSpan };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("ColorVisionSetup/2.0");
        }

        [DataContract]
        private sealed class LatestResponse { [DataMember(Name = "version")] public string Version { get; set; } }

        internal static Version ParseVersion(string value)
        {
            Version version;
            if (string.IsNullOrWhiteSpace(value) || !Version.TryParse(value.Trim(), out version) || version.Major < 1 || version.Build < 0 || version.Revision < 0)
                throw new InvalidDataException("版本信息无效，请重新检查。");
            return version;
        }

        internal static Version ParseLatest(string payload)
        {
            string text = payload.Trim();
            if (!text.StartsWith("{", StringComparison.Ordinal)) return ParseVersion(text);
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                    return ParseVersion(((LatestResponse)new DataContractJsonSerializer(typeof(LatestResponse)).ReadObject(stream)).Version);
            }
            catch (SerializationException ex) { throw new InvalidDataException("服务器未返回有效版本信息。", ex); }
        }

        internal async Task<Version> GetLatestAsync(CancellationToken token)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                try
                {
                    using (var response = await _http.GetAsync("api/app/latest-version", HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new MemoryStream())
                        {
                            await CopyAsync(input, output, response.Content.Headers.ContentLength, 16 * 1024, null, deadline.Token).ConfigureAwait(false);
                            return ParseLatest(Encoding.UTF8.GetString(output.ToArray()));
                        }
                    }
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("版本查询超时，请检查网络后重试。"); }
            }
        }

        internal async Task DownloadAsync(Version version, string destination, IProgress<TransferProgress> progress, CancellationToken token)
        {
            string partial = destination + ".partial";
            try
            {
                using (var headersDeadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    headersDeadline.CancelAfter(TimeSpan.FromSeconds(30));
                    HttpResponseMessage response;
                    try
                    {
                        response = await _http.GetAsync("api/app/releases/" + Uri.EscapeDataString(version.ToString()) + "/download", HttpCompletionOption.ResponseHeadersRead, headersDeadline.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("连接下载服务器超时。"); }
                    headersDeadline.CancelAfter(Timeout.Infinite);
                    using (response)
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                            await CopyAsync(input, output, response.Content.Headers.ContentLength, MaximumPackageBytes, progress, token).ConfigureAwait(false);
                    }
                }
                token.ThrowIfCancellationRequested();
                File.Move(partial, destination);
            }
            finally { DeletePartial(partial); }
        }

        internal static async Task CopyAsync(Stream input, Stream output, long? length, long maximumBytes, IProgress<TransferProgress> progress, CancellationToken token)
        {
            if (length > maximumBytes || length == 0) throw new InvalidDataException("文件大小无效。");
            var buffer = new byte[81920];
            var watch = Stopwatch.StartNew();
            long received = 0;
            long lastReport = -250;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int read;
                using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    // Framework streams do not all interrupt a pending read on cancellation.
                    using (deadline.Token.Register(input.Dispose))
                    {
                        try { read = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false); }
                        catch (Exception) when (deadline.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested();
                            throw new TimeoutException("文件传输超过 30 秒没有响应，请重试。");
                        }
                    }
                }
                if (read == 0) break;
                received += read;
                if (received > maximumBytes || (length.HasValue && received > length.Value)) throw new InvalidDataException("文件长度与服务器声明不一致。");
                await output.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                if (watch.ElapsedMilliseconds - lastReport >= 250)
                {
                    progress?.Report(new TransferProgress(received, length, received / Math.Max(0.001, watch.Elapsed.TotalSeconds)));
                    lastReport = watch.ElapsedMilliseconds;
                }
            }
            token.ThrowIfCancellationRequested();
            if (received == 0 || (length.HasValue && received != length.Value)) throw new InvalidDataException("文件未传输完整，请重试。");
            progress?.Report(new TransferProgress(received, length, received / Math.Max(0.001, watch.Elapsed.TotalSeconds)));
        }

        private static void DeletePartial(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException ex) { SetupFiles.Log("Partial retained: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { SetupFiles.Log("Partial retained: " + ex.Message); }
        }
        public void Dispose() => _http.Dispose();
    }
}
