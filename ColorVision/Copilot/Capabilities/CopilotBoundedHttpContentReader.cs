using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotHttpContentSizeLimitException : InvalidOperationException
    {
        public CopilotHttpContentSizeLimitException(string message)
            : base(message)
        {
        }
    }

    public static class CopilotBoundedHttpContentReader
    {
        private const int BufferSize = 8192;

        static CopilotBoundedHttpContentReader()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static async Task<string> ReadAsStringAsync(
            HttpContent content,
            int maximumBytes,
            string contentLabel,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(content);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
            cancellationToken.ThrowIfCancellationRequested();

            var label = string.IsNullOrWhiteSpace(contentLabel) ? "HTTP response content" : contentLabel.Trim();
            if (content.Headers.ContentLength is long declaredLength && declaredLength > maximumBytes)
                throw CreateSizeLimitException(label, maximumBytes);

            await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var initialCapacity = content.Headers.ContentLength is long knownLength
                ? (int)Math.Min(knownLength, maximumBytes)
                : Math.Min(BufferSize, maximumBytes);
            await using var buffer = new MemoryStream(initialCapacity);
            var chunk = new byte[Math.Min(BufferSize, maximumBytes)];
            var totalBytes = 0;

            while (true)
            {
                var bytesRead = await source.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                    break;
                if (bytesRead > maximumBytes - totalBytes)
                    throw CreateSizeLimitException(label, maximumBytes);

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalBytes += bytesRead;
            }

            cancellationToken.ThrowIfCancellationRequested();
            buffer.Position = 0;
            using var reader = new StreamReader(
                buffer,
                ResolveEncoding(content.Headers.ContentType?.CharSet),
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: false);
            return reader.ReadToEnd();
        }

        private static Encoding ResolveEncoding(string? charset)
        {
            var normalized = (charset ?? string.Empty).Trim().Trim('"', '\'');
            if (string.IsNullOrWhiteSpace(normalized))
                return Encoding.UTF8;

            try
            {
                return Encoding.GetEncoding(
                    normalized,
                    EncoderFallback.ReplacementFallback,
                    DecoderFallback.ReplacementFallback);
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8;
            }
        }

        private static CopilotHttpContentSizeLimitException CreateSizeLimitException(string label, int maximumBytes)
        {
            return new CopilotHttpContentSizeLimitException($"{label} exceeded the size limit ({maximumBytes / 1024} KB).");
        }
    }
}
