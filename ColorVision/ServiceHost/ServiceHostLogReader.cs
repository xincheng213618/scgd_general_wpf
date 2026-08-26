using System;
using System.IO;
using System.Text;

namespace ColorVision.ServiceHost
{
    internal sealed record ServiceHostLogSnapshot(
        bool Exists,
        string Text,
        long Length,
        DateTime LastWriteTimeUtc,
        string Error);

    internal static class ServiceHostLogReader
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        internal static ServiceHostLogSnapshot ReadTail(string path, int maxBytes)
        {
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            if (!File.Exists(path))
                return new ServiceHostLogSnapshot(false, string.Empty, 0, DateTime.MinValue, string.Empty);

            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long length = stream.Length;
                long startPosition = Math.Max(0, length - maxBytes);
                stream.Seek(startPosition, SeekOrigin.Begin);
                byte[] buffer = new byte[checked((int)(length - startPosition))];
                stream.ReadExactly(buffer);
                int contentOffset = ResolveContentOffset(buffer, startPosition > 0);
                string text = DecodeLines(buffer, contentOffset);
                return new ServiceHostLogSnapshot(true, text, length, File.GetLastWriteTimeUtc(path), string.Empty);
            }
            catch (Exception ex)
            {
                return new ServiceHostLogSnapshot(true, string.Empty, 0, DateTime.MinValue, ex.Message);
            }
        }

        private static int ResolveContentOffset(byte[] buffer, bool startsMidFile)
        {
            if (startsMidFile)
            {
                int firstLineBreak = Array.IndexOf(buffer, (byte)'\n');
                return firstLineBreak < 0 ? buffer.Length : firstLineBreak + 1;
            }

            return buffer.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        }

        private static string DecodeLines(byte[] buffer, int offset)
        {
            if (offset >= buffer.Length)
                return string.Empty;

            StringBuilder builder = new(buffer.Length - offset);
            int lineStart = offset;
            for (int index = offset; index < buffer.Length; index++)
            {
                if (buffer[index] != (byte)'\n')
                    continue;

                builder.Append(DecodeSegment(buffer, lineStart, index - lineStart));
                builder.Append('\n');
                lineStart = index + 1;
            }

            if (lineStart < buffer.Length)
                builder.Append(DecodeSegment(buffer, lineStart, buffer.Length - lineStart));
            return builder.ToString();
        }

        private static string DecodeSegment(byte[] buffer, int offset, int count)
        {
            try
            {
                return StrictUtf8.GetString(buffer, offset, count);
            }
            catch (DecoderFallbackException)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(936).GetString(buffer, offset, count);
            }
        }

        internal static string GetLatestInstallationFailure(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string latestOutcome = string.Empty;
            foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains("installation failed", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("安装失败", StringComparison.OrdinalIgnoreCase))
                {
                    latestOutcome = line.Trim();
                }
                else if (line.Contains("installation completed", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("安装完成", StringComparison.OrdinalIgnoreCase))
                {
                    latestOutcome = string.Empty;
                }
            }

            return latestOutcome;
        }
    }
}
