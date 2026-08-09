using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotWorkspacePatchStore
    {
        private const int MaxEntries = 32;
        private const int MaxFileBytes = 1_000_000;
        private const int MaxReplacementCharacters = 20_000;
        private const int MaxReplacementsPerFile = 16;
        private const int MaxTotalReplacementCharacters = 100_000;
        private const int MaxNewFileCharacters = 200_000;
        private const int MaxPreviewCharacters = 8_000;
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(30);
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, WorkspacePatchRecord> _records = new(StringComparer.Ordinal);

        private async Task<CopilotToolResult> PreviewUpdateOperationAsync(
            CopilotAgentRequest request,
            string requestedPath,
            IReadOnlyList<WorkspaceTextReplacement> requestedReplacements,
            CancellationToken cancellationToken)
        {
            if (requestedReplacements.Count is < 1 or > MaxReplacementsPerFile)
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The workspace patch replacement count is outside the allowed range.",
                    $"Each updated file must contain 1-{MaxReplacementsPerFile} exact replacements.");
            }
            if (requestedReplacements.Any(replacement => replacement.OldText.Length == 0
                || replacement.OldText.Length > MaxReplacementCharacters
                || replacement.NewText.Length > MaxReplacementCharacters)
                || requestedReplacements.Sum(replacement => (long)replacement.OldText.Length + replacement.NewText.Length) > MaxTotalReplacementCharacters)
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "Workspace patch text is outside the allowed size.",
                    $"Each oldText must contain 1-{MaxReplacementCharacters} characters, each newText at most {MaxReplacementCharacters} characters, and their combined size at most {MaxTotalReplacementCharacters} characters per file.");
            }
            if (!CopilotWorkspacePatchScope.TryResolve(request, requestedPath, MaxFileBytes, out var fullPath, out var scopeError))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Authorization,
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
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Internal,
                    "The target file could not be read for patch preview.", ex.Message);
            }

            if (!TryDecodeText(originalBytes, out var originalText, out var encodingInfo, out var decodeError))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The target is not a supported text file.", decodeError);
            }

            var newline = DetectNewline(originalText);
            var replacements = requestedReplacements
                .Select(replacement => new WorkspaceTextReplacement(
                    NormalizeNewlines(replacement.OldText, newline),
                    NormalizeNewlines(replacement.NewText, newline)))
                .ToArray();
            var unchangedReplacement = Array.FindIndex(replacements,
                replacement => string.Equals(replacement.OldText, replacement.NewText, StringComparison.Ordinal));
            if (unchangedReplacement >= 0)
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "A proposed replacement does not change the file.",
                    $"Replacement {unchangedReplacement + 1} has identical oldText and newText after newline normalization.");
            }

            var matches = new List<WorkspaceTextReplacementMatch>(replacements.Length);
            for (var index = 0; index < replacements.Length; index++)
            {
                var replacement = replacements[index];
                var occurrenceCount = CountOccurrences(originalText, replacement.OldText);
                if (occurrenceCount != 1)
                {
                    return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Conflict,
                        occurrenceCount == 0
                            ? $"The exact oldText for replacement {index + 1} was not found in the target file."
                            : $"The oldText for replacement {index + 1} is ambiguous in the target file.",
                        occurrenceCount == 0
                            ? $"Replacement {index + 1} did not match. Read the current file and prepare replacements that match its exact text."
                            : $"Replacement {index + 1} matched {occurrenceCount} locations; include more surrounding text so it matches exactly once.");
                }

                matches.Add(new WorkspaceTextReplacementMatch(
                    originalText.IndexOf(replacement.OldText, StringComparison.Ordinal),
                    replacement));
            }

            var orderedMatches = matches.OrderBy(match => match.StartIndex).ToArray();
            for (var index = 1; index < orderedMatches.Length; index++)
            {
                var previous = orderedMatches[index - 1];
                var current = orderedMatches[index];
                if (current.StartIndex < previous.StartIndex + previous.Replacement.OldText.Length)
                {
                    return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Conflict,
                        "The requested exact replacements overlap in the target file.",
                        "Use independent non-overlapping oldText regions for each replacement.");
                }
            }

            var patchedText = originalText;
            foreach (var match in orderedMatches.Reverse())
            {
                patchedText = patchedText.Remove(match.StartIndex, match.Replacement.OldText.Length)
                    .Insert(match.StartIndex, match.Replacement.NewText);
            }
            var patchedBytes = EncodeText(patchedText, encodingInfo);
            var now = DateTimeOffset.UtcNow;
            var record = new WorkspacePatchRecord
            {
                PreviewId = "workspace-patch:" + Guid.NewGuid().ToString("N"),
                Operation = WorkspacePatchOperation.Replace,
                FullPath = fullPath,
                OriginalBytes = originalBytes,
                PatchedBytes = patchedBytes,
                BeforeSha256 = Hash(originalBytes),
                AfterSha256 = Hash(patchedBytes),
                Replacements = replacements,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(EntryLifetime),
                State = WorkspacePatchState.Previewed,
            };
            StoreRecord(record, now);

            return new CopilotToolResult
            {
                ToolName = "PreviewWorkspacePatchEnvelope",
                Success = true,
                Summary = $"Prepared a conflict-checked workspace patch preview with {replacements.Length} replacement(s) for {Path.GetFileName(fullPath)}.",
                Content = BuildPreviewContent(record),
            };
        }

        private Task<CopilotToolResult> PreviewCreateOperationAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            input ??= CopilotAgentToolInput.Empty;
            if (string.IsNullOrWhiteSpace(input.Path)
                || !TryGetTextArgument(input, "content", out var content))
            {
                return Task.FromResult(Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "Workspace file creation arguments are incomplete.", "path and content are required string arguments."));
            }
            if (content.Length > MaxNewFileCharacters)
            {
                return Task.FromResult(Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The new workspace file content is outside the allowed size.",
                    $"content must contain at most {MaxNewFileCharacters} characters."));
            }
            if (!CopilotWorkspacePatchScope.TryResolveNewFile(request, input.Path, out var fullPath, out _, out var scopeError))
            {
                var failureKind = File.Exists(fullPath) || Directory.Exists(fullPath)
                    ? CopilotToolFailureKind.Conflict
                    : CopilotToolFailureKind.Authorization;
                return Task.FromResult(Failure("PreviewWorkspacePatchEnvelope", failureKind,
                    failureKind == CopilotToolFailureKind.Conflict
                        ? "The requested workspace file already exists."
                        : "The requested file is outside the current writable workspace scope.",
                    scopeError));
            }

            byte[] createdBytes;
            try
            {
                createdBytes = StrictUtf8.GetBytes(content);
            }
            catch (EncoderFallbackException ex)
            {
                return Task.FromResult(Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The new workspace file content is not valid UTF-8 text.", ex.Message));
            }
            if (createdBytes.Length > MaxFileBytes)
            {
                return Task.FromResult(Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The encoded workspace file exceeds the allowed size.",
                    $"The UTF-8 content exceeds the {MaxFileBytes}-byte workspace file limit."));
            }

            var now = DateTimeOffset.UtcNow;
            var record = new WorkspacePatchRecord
            {
                PreviewId = "workspace-create:" + Guid.NewGuid().ToString("N"),
                Operation = WorkspacePatchOperation.Create,
                FullPath = fullPath,
                PatchedBytes = createdBytes,
                BeforeSha256 = "missing",
                AfterSha256 = Hash(createdBytes),
                NewText = content,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(EntryLifetime),
                State = WorkspacePatchState.Previewed,
            };
            StoreRecord(record, now);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = "PreviewWorkspacePatchEnvelope",
                Success = true,
                Summary = $"Prepared a conflict-checked workspace file creation preview for {Path.GetFileName(fullPath)}.",
                Content = BuildPreviewContent(record),
            });
        }

        private async Task<CopilotToolResult> MutateAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            bool rollback,
            WorkspacePatchOperation? expectedOperation,
            string? changeSetId,
            string toolName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
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
                if (expectedOperation.HasValue && record.Operation != expectedOperation.Value)
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        "The preview belongs to a different workspace operation.",
                        $"Expected {expectedOperation.Value}, but the preview describes {record.Operation}.");
                }
                if (!string.Equals(record.ChangeSetId, changeSetId ?? string.Empty, StringComparison.Ordinal))
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        string.IsNullOrWhiteSpace(record.ChangeSetId)
                            ? "The workspace preview is not part of the requested change set."
                            : "The workspace preview is reserved by a multi-file change set.",
                        string.IsNullOrWhiteSpace(record.ChangeSetId)
                            ? "Use the original single-file apply or rollback tool for this preview."
                            : $"Use the matching workspace change-set tool with {record.ChangeSetId}.");
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

            if (record.Operation == WorkspacePatchOperation.Create)
                return await MutateCreationAsync(request, record, rollback, toolName, cancellationToken);
            if (record.Operation == WorkspacePatchOperation.Delete)
                return await MutateDeletionAsync(request, record, rollback, toolName, cancellationToken);

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
                WorkspaceMutation = CreateWorkspaceMutationSnapshot([record], rollback),
            };
        }

        private async Task<CopilotToolResult> MutateCreationAsync(
            CopilotAgentRequest request,
            WorkspacePatchRecord record,
            bool rollback,
            string toolName,
            CancellationToken cancellationToken)
        {
            if (rollback)
            {
                if (!CopilotWorkspacePatchScope.TryResolve(request, record.FullPath, MaxFileBytes, out var rollbackPath, out var rollbackScopeError))
                {
                    RestoreState(record, WorkspacePatchState.Applied);
                    return Failure(toolName, CopilotToolFailureKind.Authorization,
                        "The created file is no longer inside the current writable workspace scope.", rollbackScopeError);
                }

                byte[] currentBytes;
                try
                {
                    currentBytes = await File.ReadAllBytesAsync(rollbackPath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    RestoreState(record, WorkspacePatchState.Applied);
                    throw;
                }
                catch (Exception ex)
                {
                    RestoreState(record, WorkspacePatchState.Applied);
                    return Failure(toolName, CopilotToolFailureKind.Internal,
                        "The created file could not be revalidated before rollback.", ex.Message);
                }

                var currentHash = Hash(currentBytes);
                if (!string.Equals(currentHash, record.AfterSha256, StringComparison.OrdinalIgnoreCase))
                {
                    lock (_syncRoot)
                        record.State = WorkspacePatchState.Invalidated;
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        "The created file changed after creation; rollback did not delete it.",
                        $"Expected SHA-256 {record.AfterSha256}, current SHA-256 {currentHash}.");
                }

                try
                {
                    File.Delete(rollbackPath);
                    RemoveEmptyCreatedDirectories(record.CreatedDirectories);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    RestoreState(record, WorkspacePatchState.Applied);
                    return Failure(toolName, CopilotToolFailureKind.Internal,
                        "The created workspace file could not be removed.", ex.Message);
                }

                lock (_syncRoot)
                    record.State = WorkspacePatchState.RolledBack;
                return new CopilotToolResult
                {
                    ToolName = toolName,
                    Success = true,
                    Summary = $"Rolled back workspace file creation for {Path.GetFileName(rollbackPath)}.",
                    Content = $"path: {rollbackPath}\npreview_id: {record.PreviewId}\nfile_exists: false\nstate: {record.State}",
                    WorkspaceMutation = CreateWorkspaceMutationSnapshot([record], rollback: true),
                };
            }

            if (!CopilotWorkspacePatchScope.TryResolveNewFile(request, record.FullPath, out var fullPath, out var writableRoot, out var scopeError))
            {
                var existingPath = SafeFullPath(record.FullPath);
                var isConflict = File.Exists(existingPath) || Directory.Exists(existingPath);
                lock (_syncRoot)
                    record.State = isConflict ? WorkspacePatchState.Invalidated : WorkspacePatchState.Previewed;
                return Failure(toolName,
                    isConflict ? CopilotToolFailureKind.Conflict : CopilotToolFailureKind.Authorization,
                    isConflict
                        ? "The workspace file path was created after the preview; no bytes were overwritten."
                        : "The new file is no longer inside the current writable workspace scope.",
                    scopeError);
            }

            IReadOnlyList<string> createdDirectories;
            try
            {
                createdDirectories = await CreateNewFileAtomicallyAsync(fullPath, writableRoot, record.PatchedBytes, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RestoreCreationStateAfterFailure(record, fullPath);
                throw;
            }
            catch (Exception ex)
            {
                var reachedTargetState = RestoreCreationStateAfterFailure(record, fullPath);
                if (!reachedTargetState)
                {
                    var conflict = File.Exists(fullPath);
                    return Failure(toolName,
                        conflict ? CopilotToolFailureKind.Conflict : CopilotToolFailureKind.Internal,
                        conflict
                            ? "The workspace file path was created concurrently; no existing bytes were overwritten."
                            : "The new workspace file could not be created.",
                        ex.Message);
                }
                createdDirectories = Array.Empty<string>();
            }

            lock (_syncRoot)
            {
                record.CreatedDirectories = createdDirectories.ToArray();
                record.State = WorkspacePatchState.Applied;
            }
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = $"Created the approved workspace file {Path.GetFileName(fullPath)}.",
                Content = $"path: {fullPath}\npreview_id: {record.PreviewId}\nsha256: {record.AfterSha256}\nstate: {record.State}",
                WorkspaceMutation = CreateWorkspaceMutationSnapshot([record], rollback: false),
            };
        }

        private static string BuildPreviewContent(WorkspacePatchRecord record)
        {
            var builder = new StringBuilder();
            builder.AppendLine(record.Operation switch
            {
                WorkspacePatchOperation.Create => "[Workspace File Creation Preview]",
                WorkspacePatchOperation.Delete => "[Workspace File Deletion Preview]",
                _ => "[Workspace Patch Preview]",
            });
            builder.AppendLine($"preview_id: {record.PreviewId}");
            builder.AppendLine($"path: {record.FullPath}");
            builder.AppendLine($"operation: {record.Operation}");
            builder.AppendLine($"before_sha256: {record.BeforeSha256}");
            builder.AppendLine($"after_sha256: {record.AfterSha256}");
            builder.AppendLine($"expires_at_utc: {record.ExpiresAtUtc:O}");
            if (record.Operation == WorkspacePatchOperation.Replace)
                builder.AppendLine($"replacement_count: {record.Replacements.Length}");
            builder.AppendLine();
            if (record.Operation == WorkspacePatchOperation.Replace)
            {
                for (var index = 0; index < record.Replacements.Length; index++)
                {
                    if (record.Replacements.Length > 1)
                        builder.AppendLine($"[Replacement {index + 1}]");
                    builder.AppendLine("--- old text");
                    AppendPrefixedLines(builder, record.Replacements[index].OldText, '-');
                    builder.AppendLine("+++ new text");
                    AppendPrefixedLines(builder, record.Replacements[index].NewText, '+');
                    if (index < record.Replacements.Length - 1)
                        builder.AppendLine();
                }
            }
            else if (record.Operation == WorkspacePatchOperation.Create)
            {
                builder.AppendLine("+++ new file");
                AppendPrefixedLines(builder, record.NewText, '+');
            }
            else
            {
                builder.AppendLine("--- deleted file");
                AppendPrefixedLines(builder, record.OldText, '-');
            }
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

    }

}
