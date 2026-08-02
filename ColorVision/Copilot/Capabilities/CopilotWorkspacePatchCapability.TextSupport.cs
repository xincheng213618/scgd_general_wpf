using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotWorkspacePatchStore
    {
        internal static bool TryGetTextArgument(CopilotAgentToolInput input, string name, out string value)
        {
            value = string.Empty;
            if (input?.Arguments == null)
                return false;
            var pair = input.Arguments.FirstOrDefault(item => string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                return false;
            if (pair.Value is string text)
            {
                value = text;
                return true;
            }
            if (pair.Value is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? string.Empty;
                return true;
            }
            return false;
        }

        internal static bool TryGetPreviewId(CopilotAgentToolInput input, out string previewId)
        {
            return TryGetTextArgument(input, "previewId", out previewId)
                && (previewId.StartsWith("workspace-patch:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-patch:".Length + 32
                    || previewId.StartsWith("workspace-create:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-create:".Length + 32
                    || previewId.StartsWith("workspace-delete:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-delete:".Length + 32);
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryDecodeText(
            byte[] bytes,
            out string text,
            out WorkspaceTextEncodingInfo encodingInfo,
            out string error)
        {
            text = string.Empty;
            error = string.Empty;
            Encoding encoding;
            var preambleLength = 0;
            if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
            {
                encoding = new UTF32Encoding(false, true, true);
                preambleLength = 4;
            }
            else if (bytes.AsSpan().StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
            {
                encoding = new UTF32Encoding(true, true, true);
                preambleLength = 4;
            }
            else if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            {
                encoding = new UTF8Encoding(true, true);
                preambleLength = 3;
            }
            else if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            {
                encoding = new UnicodeEncoding(false, true, true);
                preambleLength = 2;
            }
            else if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            {
                encoding = new UnicodeEncoding(true, true, true);
                preambleLength = 2;
            }
            else
            {
                encoding = StrictUtf8;
            }

            try
            {
                text = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
            }
            catch (DecoderFallbackException ex)
            {
                encodingInfo = default;
                error = "The file encoding is not supported or contains invalid text bytes: " + ex.Message;
                return false;
            }
            if (text.Contains('\0'))
            {
                encodingInfo = default;
                error = "The file contains NUL characters and appears to be binary.";
                return false;
            }

            encodingInfo = new WorkspaceTextEncodingInfo(encoding, preambleLength > 0);
            return true;
        }

        private static byte[] EncodeText(string text, WorkspaceTextEncodingInfo encodingInfo)
        {
            var body = encodingInfo.Encoding.GetBytes(text);
            if (!encodingInfo.HasPreamble)
                return body;
            var preamble = encodingInfo.Encoding.GetPreamble();
            return preamble.Concat(body).ToArray();
        }

        private static string DetectNewline(string text)
        {
            if (text.Contains("\r\n", StringComparison.Ordinal))
                return "\r\n";
            return text.Contains('\n') ? "\n" : text.Contains('\r') ? "\r" : Environment.NewLine;
        }

        private static string NormalizeNewlines(string value, string newline)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", newline, StringComparison.Ordinal);
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index++;
            }
            return count;
        }

        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private static CopilotToolResult Failure(
            string toolName,
            CopilotToolFailureKind failureKind,
            string summary,
            string error)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = summary,
                ErrorMessage = error,
                FailureKind = failureKind,
            };
        }

        private sealed class WorkspacePatchRecord
        {
            public string PreviewId { get; init; } = string.Empty;
            public WorkspacePatchOperation Operation { get; init; }
            public string FullPath { get; init; } = string.Empty;
            public byte[] OriginalBytes { get; init; } = Array.Empty<byte>();
            public byte[] PatchedBytes { get; init; } = Array.Empty<byte>();
            public string BeforeSha256 { get; init; } = string.Empty;
            public string AfterSha256 { get; init; } = string.Empty;
            public string OldText { get; init; } = string.Empty;
            public string NewText { get; init; } = string.Empty;
            public WorkspaceTextReplacement[] Replacements { get; init; } = Array.Empty<WorkspaceTextReplacement>();
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public string[] CreatedDirectories { get; set; } = Array.Empty<string>();
            public string ChangeSetId { get; set; } = string.Empty;
            public WorkspacePatchState State { get; set; }
        }

        private readonly record struct WorkspaceTextReplacement(string OldText, string NewText);

        private readonly record struct WorkspaceTextReplacementMatch(int StartIndex, WorkspaceTextReplacement Replacement);

        private enum WorkspacePatchOperation
        {
            Replace,
            Create,
            Delete,
        }

        private enum WorkspacePatchState
        {
            Previewed,
            Applying,
            Applied,
            RollingBack,
            RolledBack,
            Invalidated,
        }

        private readonly record struct WorkspaceTextEncodingInfo(Encoding Encoding, bool HasPreamble);
    }
}
