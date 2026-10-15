using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;

namespace ColorVision.UI.Desktop.Download
{
    internal sealed class DownloadReuseService : IDisposable
    {
        private readonly HttpClient _httpClient;

        public DownloadReuseService(HttpMessageHandler? handler = null)
        {
            _httpClient = handler == null ? new HttpClient() : new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
        }

        public static string? NormalizeSha256(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash)) return null;
            hash = hash.Trim();
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new ArgumentException("Expected SHA-256 must contain 64 hexadecimal characters.", nameof(hash));
            return hash.ToUpperInvariant();
        }

        public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        public static long GetReusableSourceLength(DownloadEntry entry)
        {
            if (entry.TotalBytes > 0)
                return entry.TotalBytes;

            return File.Exists(entry.SavePath) ? new FileInfo(entry.SavePath).Length : 0;
        }

        public static bool CanRetryLocalReuse(DownloadTask task)
        {
            return !string.IsNullOrWhiteSpace(task.LocalReuseSourcePath) && File.Exists(task.LocalReuseSourcePath);
        }

        public static bool CanReuseCompletedEntry(DownloadEntry entry, string targetDirectory, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(entry.SavePath))
                return false;

            if (string.Equals(entry.SavePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                return false;

            string? sourceDirectory = Path.GetDirectoryName(entry.SavePath);
            if (!string.Equals(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
                return false;

            return IsEntryBackedByCompleteFile(entry);
        }

        public static bool TryGetRemoteValidatedCandidate(string url, string sourcePath, string destinationPath, out long expectedBytes)
        {
            expectedBytes = 0;

            if (!CanAttemptRemoteValidation(url))
                return false;

            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) || File.Exists(sourcePath + ".aria2"))
                return false;

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                expectedBytes = new FileInfo(sourcePath).Length;
                return expectedBytes > 0;
            }
            catch
            {
                expectedBytes = 0;
                return false;
            }
        }

        public async Task<RemoteFileValidationInfo?> TryValidateLocalFileAgainstRemoteAsync(string url, string sourcePath, string? authorization, CancellationToken cancellationToken, string? expectedSha256 = null)
        {
            if (!CanAttemptRemoteValidation(url))
                return null;

            if (!File.Exists(sourcePath) || File.Exists(sourcePath + ".aria2"))
                return null;

            long localFileLength = new FileInfo(sourcePath).Length;
            var validationInfo = expectedSha256 != null
                ? new RemoteFileValidationInfo { ContentLength = localFileLength, ContentSha256 = NormalizeSha256(expectedSha256) }
                : await TryGetRemoteFileValidationInfoAsync(url, authorization, cancellationToken).ConfigureAwait(false);
            if (validationInfo?.ContentLength is not long remoteLength || remoteLength <= 0 || validationInfo.ContentSha256 == null)
                return null;

            if (remoteLength != localFileLength) return null;
            string actualHash = await ComputeSha256Async(sourcePath, cancellationToken).ConfigureAwait(false);
            return actualHash.Equals(validationInfo.ContentSha256, StringComparison.OrdinalIgnoreCase) ? validationInfo : null;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        private async Task<RemoteFileValidationInfo?> TryGetRemoteFileValidationInfoAsync(string url, string? authorization, CancellationToken cancellationToken)
        {
            using var headRequest = CreateRemoteValidationRequest(HttpMethod.Head, url, authorization);
            try
            {
                using var response = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return CreateRemoteFileValidationInfo(response);

                if (response.StatusCode != HttpStatusCode.MethodNotAllowed && response.StatusCode != HttpStatusCode.NotImplemented)
                    return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }

            using var rangeRequest = CreateRemoteValidationRequest(HttpMethod.Get, url, authorization);
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
            using var rangeResponse = await _httpClient.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!rangeResponse.IsSuccessStatusCode && rangeResponse.StatusCode != HttpStatusCode.PartialContent)
                return null;

            return CreateRemoteFileValidationInfo(rangeResponse);
        }

        private static bool IsEntryBackedByCompleteFile(DownloadEntry entry)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(entry.SavePath) || !File.Exists(entry.SavePath))
                    return false;

                if (File.Exists(entry.SavePath + ".aria2"))
                    return false;

                long fileLength = new FileInfo(entry.SavePath).Length;
                if (entry.TotalBytes > 0)
                    return fileLength > 0 && entry.DownloadedBytes >= entry.TotalBytes && fileLength == entry.TotalBytes;

                return fileLength > 0 && entry.CompleteTime != null;
            }
            catch
            {
                return false;
            }
        }

        private static RemoteFileValidationInfo CreateRemoteFileValidationInfo(HttpResponseMessage response)
        {
            return new RemoteFileValidationInfo
            {
                ContentLength = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength,
                ETag = response.Headers.ETag?.Tag,
                LastModified = response.Content.Headers.LastModified,
                ContentSha256 = GetDigestSha256(response)
            };
        }

        private static string? GetDigestSha256(HttpResponseMessage response)
        {
            foreach (string header in new[] { "Content-Digest", "Digest" })
            {
                if (!response.Headers.TryGetValues(header, out var values)) continue;
                foreach (string digest in values.SelectMany(value => value.Split(',')))
                {
                    string[] parts = digest.Trim().Split('=', 2);
                    if (parts.Length != 2 || !parts[0].Equals("sha-256", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(parts[1].Trim().Trim(':', '"'));
                        if (bytes.Length == 32) return Convert.ToHexString(bytes);
                    }
                    catch (FormatException) { }
                }
            }
            return null;
        }

        private static HttpRequestMessage CreateRemoteValidationRequest(HttpMethod method, string url, string? authorization)
        {
            var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(authorization) && authorization.Contains(':'))
            {
                string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorization));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
            }
            return request;
        }

        private static bool CanAttemptRemoteValidation(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }

    internal sealed class RemoteFileValidationInfo
    {
        public long? ContentLength { get; init; }
        public string? ETag { get; init; }
        public DateTimeOffset? LastModified { get; init; }
        public string? ContentSha256 { get; init; }
    }
}
