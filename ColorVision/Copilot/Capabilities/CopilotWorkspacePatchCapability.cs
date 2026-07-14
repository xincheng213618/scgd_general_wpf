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
    public sealed class CopilotWorkspacePatchStore
    {
        private const int MaxEntries = 32;
        private const int MaxFileBytes = 1_000_000;
        private const int MaxReplacementCharacters = 20_000;
        private const int MaxPreviewCharacters = 8_000;
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(30);
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, WorkspacePatchRecord> _records = new(StringComparer.Ordinal);

        public async Task<CopilotToolResult> PreviewAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            if (!TryGetTextArgument(input, "oldText", out var requestedOldText)
                || !TryGetTextArgument(input, "newText", out var requestedNewText)
                || string.IsNullOrWhiteSpace(input.Path))
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Validation,
                    "Workspace patch arguments are incomplete.", "path, oldText, and newText are required string arguments.");
            }
            if (requestedOldText.Length == 0 || requestedOldText.Length > MaxReplacementCharacters
                || requestedNewText.Length > MaxReplacementCharacters)
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Validation,
                    "Workspace patch text is outside the allowed size.",
                    $"oldText must contain 1-{MaxReplacementCharacters} characters and newText at most {MaxReplacementCharacters} characters.");
            }
            if (!CopilotWorkspacePatchScope.TryResolve(request, input.Path, MaxFileBytes, out var fullPath, out var scopeError))
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Authorization,
                    "The target file is outside the current writable workspace scope.", scopeError);
            }

            byte[] originalBytes;
            try
            {
                originalBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Internal,
                    "The target file could not be read for patch preview.", ex.Message);
            }

            if (!TryDecodeText(originalBytes, out var originalText, out var encodingInfo, out var decodeError))
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Validation,
                    "The target is not a supported text file.", decodeError);
            }

            var newline = DetectNewline(originalText);
            var oldText = NormalizeNewlines(requestedOldText, newline);
            var newText = NormalizeNewlines(requestedNewText, newline);
            if (string.Equals(oldText, newText, StringComparison.Ordinal))
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Validation,
                    "The proposed replacement does not change the file.", "oldText and newText are identical after newline normalization.");
            }

            var occurrenceCount = CountOccurrences(originalText, oldText);
            if (occurrenceCount != 1)
            {
                return Failure("PreviewWorkspacePatch", CopilotToolFailureKind.Conflict,
                    occurrenceCount == 0 ? "The exact oldText was not found in the target file." : "The oldText is ambiguous in the target file.",
                    occurrenceCount == 0
                        ? "Read the current file and prepare a replacement that matches its exact text."
                        : $"oldText matched {occurrenceCount} locations; include more surrounding text so exactly one location matches.");
            }

            var patchedText = originalText.Replace(oldText, newText, StringComparison.Ordinal);
            var patchedBytes = EncodeText(patchedText, encodingInfo);
            var now = DateTimeOffset.UtcNow;
            var record = new WorkspacePatchRecord
            {
                PreviewId = "workspace-patch:" + Guid.NewGuid().ToString("N"),
                FullPath = fullPath,
                OriginalBytes = originalBytes,
                PatchedBytes = patchedBytes,
                BeforeSha256 = Hash(originalBytes),
                AfterSha256 = Hash(patchedBytes),
                OldText = oldText,
                NewText = newText,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(EntryLifetime),
                State = WorkspacePatchState.Previewed,
            };
            lock (_syncRoot)
            {
                RemoveExpiredEntries(now);
                if (_records.Count >= MaxEntries)
                {
                    var oldest = _records.Values.OrderBy(item => item.CreatedAtUtc).First();
                    _records.Remove(oldest.PreviewId);
                }
                _records[record.PreviewId] = record;
            }

            return new CopilotToolResult
            {
                ToolName = "PreviewWorkspacePatch",
                Success = true,
                Summary = $"Prepared a conflict-checked workspace patch preview for {Path.GetFileName(fullPath)}.",
                Content = BuildPreviewContent(record),
            };
        }

        public async Task<CopilotToolResult> ApplyAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            return await MutateAsync(request, input, rollback: false, cancellationToken);
        }

        public async Task<CopilotToolResult> RollbackAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            return await MutateAsync(request, input, rollback: true, cancellationToken);
        }

        public string GetConcurrencyKey(CopilotAgentToolInput input, string fallbackToolName)
        {
            if (TryGetPreviewId(input, out var previewId))
            {
                lock (_syncRoot)
                {
                    if (_records.TryGetValue(previewId, out var record))
                        return "path:" + record.FullPath;
                }
            }
            return "tool:" + fallbackToolName;
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentToolInput input,
            bool rollback)
        {
            if (!TryGetPreviewId(input, out var previewId))
            {
                return new CopilotToolApprovalPresentation(
                    rollback ? "Approve workspace patch rollback" : "Approve workspace patch",
                    "The patch preview identifier is missing or invalid.");
            }

            lock (_syncRoot)
            {
                if (!_records.TryGetValue(previewId, out var record))
                {
                    return new CopilotToolApprovalPresentation(
                        rollback ? "Approve workspace patch rollback" : "Approve workspace patch",
                        $"The referenced patch preview {previewId} is no longer available.");
                }

                return new CopilotToolApprovalPresentation(
                    rollback ? $"Rollback {Path.GetFileName(record.FullPath)}" : $"Apply patch to {Path.GetFileName(record.FullPath)}",
                    rollback
                        ? $"Restore the pre-patch bytes for {record.FullPath}. The rollback will run only if the file still has SHA-256 {record.AfterSha256}."
                        : $"Replace one exact text region in {record.FullPath}. The write will run only if the file still has SHA-256 {record.BeforeSha256}; resulting SHA-256: {record.AfterSha256}.");
            }
        }

        private async Task<CopilotToolResult> MutateAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            bool rollback,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            var toolName = rollback ? "RollbackWorkspacePatch" : "ApplyWorkspacePatch";
            if (!TryGetPreviewId(input, out var previewId))
            {
                return Failure(toolName, CopilotToolFailureKind.Validation,
                    "The workspace patch preview identifier is missing.", "previewId is required.");
            }

            WorkspacePatchRecord record;
            lock (_syncRoot)
            {
                RemoveExpiredEntries(DateTimeOffset.UtcNow);
                if (!_records.TryGetValue(previewId, out record!))
                {
                    return Failure(toolName, CopilotToolFailureKind.NotFound,
                        "The workspace patch preview is unavailable or expired.", "Create a new patch preview before trying again.");
                }
                var expectedState = rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed;
                if (record.State != expectedState)
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        rollback ? "The workspace patch is not in an applied state." : "The workspace patch preview has already been consumed.",
                        $"Current preview state: {record.State}.");
                }
                record.State = rollback ? WorkspacePatchState.RollingBack : WorkspacePatchState.Applying;
            }

            if (!CopilotWorkspacePatchScope.TryResolve(request, record.FullPath, MaxFileBytes, out var fullPath, out var scopeError))
            {
                RestoreState(record, rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed);
                return Failure(toolName, CopilotToolFailureKind.Authorization,
                    "The target file is no longer inside the current writable workspace scope.", scopeError);
            }

            var expectedHash = rollback ? record.AfterSha256 : record.BeforeSha256;
            var targetBytes = rollback ? record.OriginalBytes : record.PatchedBytes;
            byte[] currentBytes;
            try
            {
                currentBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RestoreState(record, rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed);
                throw;
            }
            catch (Exception ex)
            {
                RestoreState(record, rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed);
                return Failure(toolName, CopilotToolFailureKind.Internal,
                    "The target file could not be revalidated before writing.", ex.Message);
            }

            var currentHash = Hash(currentBytes);
            if (!string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                lock (_syncRoot)
                    record.State = WorkspacePatchState.Invalidated;
                return Failure(toolName, CopilotToolFailureKind.Conflict,
                    "The target file changed after the patch preview; no bytes were written.",
                    $"Expected SHA-256 {expectedHash}, current SHA-256 {currentHash}. Create a fresh preview from the current file.");
            }

            try
            {
                await WriteAtomicallyAsync(fullPath, targetBytes, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RestoreStateAfterUncertainWrite(record, fullPath, targetBytes, rollback);
                throw;
            }
            catch (Exception ex)
            {
                var reachedTargetState = RestoreStateAfterUncertainWrite(record, fullPath, targetBytes, rollback);
                if (!reachedTargetState)
                {
                    return Failure(toolName, CopilotToolFailureKind.Internal,
                        rollback ? "The workspace patch rollback failed." : "The workspace patch could not be applied.", ex.Message);
                }
            }

            lock (_syncRoot)
                record.State = rollback ? WorkspacePatchState.RolledBack : WorkspacePatchState.Applied;
            var resultingHash = rollback ? record.BeforeSha256 : record.AfterSha256;
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = rollback
                    ? $"Rolled back the workspace patch for {Path.GetFileName(fullPath)}."
                    : $"Applied the approved workspace patch to {Path.GetFileName(fullPath)}.",
                Content = $"path: {fullPath}\npreview_id: {record.PreviewId}\nsha256: {resultingHash}\nstate: {record.State}",
            };
        }

        private void RestoreState(WorkspacePatchRecord record, WorkspacePatchState state)
        {
            lock (_syncRoot)
                record.State = state;
        }

        private bool RestoreStateAfterUncertainWrite(
            WorkspacePatchRecord record,
            string fullPath,
            byte[] targetBytes,
            bool rollback)
        {
            var reachedTargetState = false;
            try
            {
                reachedTargetState = File.Exists(fullPath)
                    && string.Equals(Hash(File.ReadAllBytes(fullPath)), Hash(targetBytes), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
            }
            lock (_syncRoot)
            {
                record.State = reachedTargetState
                    ? rollback ? WorkspacePatchState.RolledBack : WorkspacePatchState.Applied
                    : rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed;
            }
            return reachedTargetState;
        }

        private void RemoveExpiredEntries(DateTimeOffset now)
        {
            foreach (var key in _records.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray())
                _records.Remove(key);
        }

        private static async Task WriteAtomicallyAsync(string fullPath, byte[] content, CancellationToken cancellationToken)
        {
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                throw new UnauthorizedAccessException("The target file is read-only.");

            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The target file has no parent directory.");
            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.copilot-{Guid.NewGuid():N}.tmp");
            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(content, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                File.Replace(temporaryPath, fullPath, null, ignoreMetadataErrors: false);
                File.SetAttributes(fullPath, attributes);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static string BuildPreviewContent(WorkspacePatchRecord record)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Workspace Patch Preview]");
            builder.AppendLine($"preview_id: {record.PreviewId}");
            builder.AppendLine($"path: {record.FullPath}");
            builder.AppendLine($"before_sha256: {record.BeforeSha256}");
            builder.AppendLine($"after_sha256: {record.AfterSha256}");
            builder.AppendLine($"expires_at_utc: {record.ExpiresAtUtc:O}");
            builder.AppendLine("replacement_count: 1");
            builder.AppendLine();
            builder.AppendLine("--- old text");
            AppendPrefixedLines(builder, record.OldText, '-');
            builder.AppendLine("+++ new text");
            AppendPrefixedLines(builder, record.NewText, '+');
            var content = builder.ToString().TrimEnd();
            return content.Length <= MaxPreviewCharacters
                ? content
                : content[..(MaxPreviewCharacters - 32)] + "\n...<patch preview truncated>";
        }

        private static void AppendPrefixedLines(StringBuilder builder, string text, char prefix)
        {
            foreach (var line in NormalizeNewlines(text, "\n").Split('\n'))
                builder.Append(prefix).AppendLine(line);
        }

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
                && previewId.StartsWith("workspace-patch:", StringComparison.Ordinal)
                && previewId.Length == "workspace-patch:".Length + 32;
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
                index += value.Length;
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
            public string FullPath { get; init; } = string.Empty;
            public byte[] OriginalBytes { get; init; } = Array.Empty<byte>();
            public byte[] PatchedBytes { get; init; } = Array.Empty<byte>();
            public string BeforeSha256 { get; init; } = string.Empty;
            public string AfterSha256 { get; init; } = string.Empty;
            public string OldText { get; init; } = string.Empty;
            public string NewText { get; init; } = string.Empty;
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public WorkspacePatchState State { get; set; }
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

    internal static class CopilotWorkspacePatchScope
    {
        public static bool TryResolve(
            CopilotAgentRequest request,
            string requestedPath,
            int maxFileBytes,
            out string fullPath,
            out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;
            try
            {
                fullPath = Path.GetFullPath(requestedPath);
            }
            catch (Exception ex)
            {
                error = "Invalid target path: " + ex.Message;
                return false;
            }
            if (!File.Exists(fullPath))
            {
                error = "The target file does not exist: " + fullPath;
                return false;
            }
            var resolvedPath = fullPath;
            try
            {
                if (!CopilotWorkspaceSearchSupport.IsTextLikeFile(resolvedPath))
                {
                    error = "The target extension is not in the workspace text-file allowlist: " + Path.GetExtension(resolvedPath);
                    return false;
                }
                if (new FileInfo(resolvedPath).Length > maxFileBytes)
                {
                    error = $"The target file exceeds the {maxFileBytes}-byte workspace patch limit.";
                    return false;
                }

                var exactFiles = (request.WritableLocalFilePaths ?? Array.Empty<string>())
                    .Select(NormalizePath)
                    .Where(path => path.Length > 0)
                    .ToArray();
                var isExactFile = exactFiles.Contains(resolvedPath, StringComparer.OrdinalIgnoreCase);
                var roots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths);
                var containingRoot = roots.FirstOrDefault(root => IsWithinRoot(resolvedPath, root));
                if (!isExactFile && string.IsNullOrWhiteSpace(containingRoot))
                {
                    error = "The target file is neither explicitly writable nor inside a writable workspace root: " + resolvedPath;
                    return false;
                }

                if ((File.GetAttributes(resolvedPath) & FileAttributes.ReparsePoint) != 0)
                {
                    error = "Writing through a file-system reparse point is not allowed.";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(containingRoot) && ContainsReparsePoint(containingRoot, resolvedPath))
                {
                    error = "Writing through a workspace directory reparse point is not allowed.";
                    return false;
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                error = "The target file could not be validated safely: " + ex.Message;
                return false;
            }
        }

        private static string NormalizePath(string path)
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

        private static bool IsWithinRoot(string path, string root)
        {
            var relative = Path.GetRelativePath(root, path);
            return !Path.IsPathRooted(relative)
                && !string.Equals(relative, "..", StringComparison.Ordinal)
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static bool ContainsReparsePoint(string root, string target)
        {
            var current = root;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
            foreach (var segment in Path.GetRelativePath(root, target)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }
            return false;
        }
    }
}
