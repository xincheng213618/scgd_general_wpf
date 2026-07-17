using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotWorkspacePatchStore
    {
        public async Task<CopilotToolResult> PreviewPatchEnvelopeAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            if (!TryGetEnvelopeOperations(input, out var operations, out var validationError))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The workspace patch envelope is invalid.", validationError);
            }

            var previewIds = new List<string>(operations.Length);
            try
            {
                foreach (var operation in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var childInput = new CopilotAgentToolInput
                    {
                        Path = operation.Path,
                        Arguments = operation.Kind switch
                        {
                            WorkspacePatchOperation.Create => new Dictionary<string, object?> { ["content"] = operation.Content },
                            _ => new Dictionary<string, object?>(),
                        },
                    };
                    var preview = operation.Kind switch
                    {
                        WorkspacePatchOperation.Create => await PreviewCreateOperationAsync(request, childInput, cancellationToken),
                        WorkspacePatchOperation.Replace => await PreviewUpdateOperationAsync(
                            request, operation.Path, operation.Replacements, cancellationToken),
                        WorkspacePatchOperation.Delete => await PreviewDeleteAsync(request, childInput, cancellationToken),
                        _ => throw new InvalidOperationException("Unsupported workspace envelope operation."),
                    };
                    if (!preview.Success)
                    {
                        ReleaseEnvelopePreviews(previewIds);
                        return Failure("PreviewWorkspacePatchEnvelope",
                            preview.FailureKind == CopilotToolFailureKind.None ? CopilotToolFailureKind.Validation : preview.FailureKind,
                            $"The {operation.Kind.ToString().ToLowerInvariant()} operation for {Path.GetFileName(operation.Path)} could not be previewed.",
                            string.IsNullOrWhiteSpace(preview.ErrorMessage) ? preview.Summary : preview.ErrorMessage);
                    }
                    if (!TryExtractPreviewId(preview.Content, out var previewId))
                    {
                        ReleaseEnvelopePreviews(previewIds);
                        return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Internal,
                            "A workspace operation preview did not return its identifier.",
                            "Discard the incomplete envelope and prepare it again.");
                    }
                    previewIds.Add(previewId);
                }

                var result = CreateChangeSetPreview(previewIds.ToArray(), "PreviewWorkspacePatchEnvelope", minimumFiles: 1);
                if (!result.Success)
                    ReleaseEnvelopePreviews(previewIds);
                return result;
            }
            catch (OperationCanceledException)
            {
                ReleaseEnvelopePreviews(previewIds);
                throw;
            }
        }

        private async Task<CopilotToolResult> PreviewDeleteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.Path))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "The workspace file deletion path is missing.", "path is required for a delete operation.");
            }
            if (!CopilotWorkspacePatchScope.TryResolve(request, input.Path, MaxFileBytes, out var fullPath, out var scopeError))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Authorization,
                    "The deletion target is outside the current writable workspace scope.", scopeError);
            }
            if (!IsInsideWritableRoot(request, fullPath))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Authorization,
                    "Workspace file deletion is limited to a writable workspace root.",
                    "An explicitly writable standalone file may be updated but cannot be deleted because its rollback path is not an authorized creation root.");
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
                    "The deletion target could not be read for preview.", ex.Message);
            }
            if (!TryDecodeText(originalBytes, out var originalText, out _, out var decodeError))
            {
                return Failure("PreviewWorkspacePatchEnvelope", CopilotToolFailureKind.Validation,
                    "Only supported workspace text files can be deleted by the Agent.", decodeError);
            }

            var now = DateTimeOffset.UtcNow;
            var record = new WorkspacePatchRecord
            {
                PreviewId = "workspace-delete:" + Guid.NewGuid().ToString("N"),
                Operation = WorkspacePatchOperation.Delete,
                FullPath = fullPath,
                OriginalBytes = originalBytes,
                BeforeSha256 = Hash(originalBytes),
                AfterSha256 = "missing",
                OldText = originalText.Length <= MaxPreviewCharacters ? originalText : originalText[..MaxPreviewCharacters],
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(EntryLifetime),
                State = WorkspacePatchState.Previewed,
            };
            StoreRecord(record, now);
            return new CopilotToolResult
            {
                ToolName = "PreviewWorkspacePatchEnvelope",
                Success = true,
                Summary = $"Prepared a hash-bound workspace file deletion preview for {Path.GetFileName(fullPath)}.",
                Content = BuildPreviewContent(record),
            };
        }

        private async Task<CopilotToolResult> MutateDeletionAsync(
            CopilotAgentRequest request,
            WorkspacePatchRecord record,
            bool rollback,
            string toolName,
            CancellationToken cancellationToken)
        {
            if (rollback)
            {
                if (!CopilotWorkspacePatchScope.TryResolveNewFile(request, record.FullPath, out var fullPath, out var writableRoot, out var scopeError))
                {
                    var conflict = File.Exists(SafeFullPath(record.FullPath)) || Directory.Exists(SafeFullPath(record.FullPath));
                    RestoreState(record, conflict ? WorkspacePatchState.Invalidated : WorkspacePatchState.Applied);
                    return Failure(toolName,
                        conflict ? CopilotToolFailureKind.Conflict : CopilotToolFailureKind.Authorization,
                        conflict
                            ? "The deleted-file path is no longer empty; rollback did not overwrite it."
                            : "The deleted file is no longer inside the writable workspace scope.",
                        scopeError);
                }

                IReadOnlyList<string> createdDirectories;
                try
                {
                    createdDirectories = await CreateNewFileAtomicallyAsync(fullPath, writableRoot, record.OriginalBytes, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    RestoreDeletionStateAfterCreateFailure(record, fullPath);
                    throw;
                }
                catch (Exception ex)
                {
                    var restored = RestoreDeletionStateAfterCreateFailure(record, fullPath);
                    if (!restored)
                    {
                        return Failure(toolName, CopilotToolFailureKind.Internal,
                            "The deleted workspace file could not be restored.", ex.Message);
                    }
                    createdDirectories = Array.Empty<string>();
                }

                lock (_syncRoot)
                {
                    record.CreatedDirectories = createdDirectories.ToArray();
                    record.State = WorkspacePatchState.RolledBack;
                }
                return new CopilotToolResult
                {
                    ToolName = toolName,
                    Success = true,
                    Summary = $"Restored the deleted workspace file {Path.GetFileName(fullPath)}.",
                    Content = $"path: {fullPath}\npreview_id: {record.PreviewId}\nsha256: {record.BeforeSha256}\nstate: {record.State}",
                };
            }

            if (!CopilotWorkspacePatchScope.TryResolve(request, record.FullPath, MaxFileBytes, out var deletePath, out var deleteScopeError))
            {
                RestoreState(record, WorkspacePatchState.Previewed);
                return Failure(toolName, CopilotToolFailureKind.Authorization,
                    "The deletion target is no longer inside the writable workspace scope.", deleteScopeError);
            }

            byte[] currentBytes;
            try
            {
                currentBytes = await File.ReadAllBytesAsync(deletePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                RestoreState(record, WorkspacePatchState.Previewed);
                throw;
            }
            catch (Exception ex)
            {
                RestoreState(record, WorkspacePatchState.Previewed);
                return Failure(toolName, CopilotToolFailureKind.Internal,
                    "The deletion target could not be revalidated before writing.", ex.Message);
            }

            var currentHash = Hash(currentBytes);
            if (!string.Equals(currentHash, record.BeforeSha256, StringComparison.OrdinalIgnoreCase))
            {
                RestoreState(record, WorkspacePatchState.Invalidated);
                return Failure(toolName, CopilotToolFailureKind.Conflict,
                    "The deletion target changed after preview; it was not deleted.",
                    $"Expected SHA-256 {record.BeforeSha256}, current SHA-256 {currentHash}.");
            }

            try
            {
                File.Delete(deletePath);
                RemoveEmptyCreatedDirectories(record.CreatedDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                RestoreState(record, File.Exists(deletePath) ? WorkspacePatchState.Previewed : WorkspacePatchState.Applied);
                return Failure(toolName, CopilotToolFailureKind.Internal,
                    "The approved workspace file deletion failed.", ex.Message);
            }

            RestoreState(record, WorkspacePatchState.Applied);
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = $"Deleted the approved workspace file {Path.GetFileName(deletePath)}.",
                Content = $"path: {deletePath}\npreview_id: {record.PreviewId}\nfile_exists: false\nstate: {record.State}",
            };
        }

        private bool RestoreDeletionStateAfterCreateFailure(WorkspacePatchRecord record, string fullPath)
        {
            var restored = false;
            try
            {
                restored = File.Exists(fullPath)
                    && string.Equals(Hash(File.ReadAllBytes(fullPath)), record.BeforeSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
            }
            RestoreState(record, restored ? WorkspacePatchState.RolledBack : WorkspacePatchState.Applied);
            return restored;
        }

        private void ReleaseEnvelopePreviews(IEnumerable<string> previewIds)
        {
            lock (_syncRoot)
            {
                foreach (var previewId in previewIds)
                {
                    if (_records.TryGetValue(previewId, out var record)
                        && record.State == WorkspacePatchState.Previewed
                        && string.IsNullOrWhiteSpace(record.ChangeSetId))
                    {
                        _records.Remove(previewId);
                    }
                }
            }
        }

        private static bool TryGetEnvelopeOperations(
            CopilotAgentToolInput input,
            out WorkspaceEnvelopeOperation[] operations,
            out string error)
        {
            operations = Array.Empty<WorkspaceEnvelopeOperation>();
            error = "operations must contain 1-8 add, update, or delete objects.";
            if (input?.Arguments == null)
                return false;
            var pair = input.Arguments.FirstOrDefault(item => string.Equals(item.Key, "operations", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                return false;

            JsonElement array;
            try
            {
                array = pair.Value is JsonElement element ? element : JsonSerializer.SerializeToElement(pair.Value);
            }
            catch
            {
                return false;
            }
            if (array.ValueKind != JsonValueKind.Array)
                return false;
            var items = array.EnumerateArray().ToArray();
            if (items.Length is < 1 or > MaxChangeSetFiles || items.Any(item => item.ValueKind != JsonValueKind.Object))
                return false;

            var parsed = new List<WorkspaceEnvelopeOperation>(items.Length);
            foreach (var item in items)
            {
                if (!TryGetJsonString(item, "operation", out var operationText)
                    || !TryGetJsonString(item, "path", out var path)
                    || string.IsNullOrWhiteSpace(path))
                {
                    error = "Every operation requires string operation and path fields.";
                    return false;
                }

                switch (operationText.Trim().ToLowerInvariant())
                {
                    case "add":
                        if (HasUnknownProperties(item, "operation", "path", "content"))
                        {
                            error = "An add operation accepts only operation, path, and content fields.";
                            return false;
                        }
                        if (!TryGetJsonString(item, "content", out var content))
                        {
                            error = "An add operation requires complete file content.";
                            return false;
                        }
                        parsed.Add(new WorkspaceEnvelopeOperation(
                            WorkspacePatchOperation.Create, path, Array.Empty<WorkspaceTextReplacement>(), content));
                        break;
                    case "update":
                        if (HasUnknownProperties(item, "operation", "path", "oldText", "newText", "replacements"))
                        {
                            error = "An update operation accepts only operation, path, legacy oldText/newText, or replacements fields.";
                            return false;
                        }
                        if (!TryGetUpdateReplacements(item, out var replacements, out error))
                        {
                            return false;
                        }
                        parsed.Add(new WorkspaceEnvelopeOperation(WorkspacePatchOperation.Replace, path, replacements, string.Empty));
                        break;
                    case "delete":
                        if (HasUnknownProperties(item, "operation", "path"))
                        {
                            error = "A delete operation accepts only operation and path fields.";
                            return false;
                        }
                        parsed.Add(new WorkspaceEnvelopeOperation(
                            WorkspacePatchOperation.Delete, path, Array.Empty<WorkspaceTextReplacement>(), string.Empty));
                        break;
                    default:
                        error = $"Unsupported workspace envelope operation '{operationText}'. Use add, update, or delete.";
                        return false;
                }
            }

            operations = parsed.ToArray();
            return true;
        }

        private static bool TryGetUpdateReplacements(
            JsonElement item,
            out WorkspaceTextReplacement[] replacements,
            out string error)
        {
            replacements = Array.Empty<WorkspaceTextReplacement>();
            error = string.Empty;
            var hasReplacementArray = item.TryGetProperty("replacements", out var replacementArray);
            var hasOldText = item.TryGetProperty("oldText", out _);
            var hasNewText = item.TryGetProperty("newText", out _);
            if (hasReplacementArray && (hasOldText || hasNewText))
            {
                error = "An update operation must use either replacements or legacy oldText/newText, not both.";
                return false;
            }

            if (!hasReplacementArray)
            {
                if (!TryGetJsonString(item, "oldText", out var oldText)
                    || !TryGetJsonString(item, "newText", out var newText)
                    || oldText.Length == 0)
                {
                    error = "An update operation requires replacements or non-empty legacy oldText with string newText.";
                    return false;
                }

                replacements = [new WorkspaceTextReplacement(oldText, newText)];
                return true;
            }

            if (replacementArray.ValueKind != JsonValueKind.Array)
            {
                error = "Update replacements must be an array.";
                return false;
            }
            var items = replacementArray.EnumerateArray().ToArray();
            if (items.Length is < 1 or > MaxReplacementsPerFile)
            {
                error = $"Update replacements must contain 1-{MaxReplacementsPerFile} items.";
                return false;
            }

            var parsed = new List<WorkspaceTextReplacement>(items.Length);
            for (var index = 0; index < items.Length; index++)
            {
                var replacement = items[index];
                if (replacement.ValueKind != JsonValueKind.Object
                    || HasUnknownProperties(replacement, "oldText", "newText")
                    || !TryGetJsonString(replacement, "oldText", out var oldText)
                    || !TryGetJsonString(replacement, "newText", out var newText)
                    || oldText.Length == 0)
                {
                    error = $"Replacement {index + 1} must contain only non-empty string oldText and string newText fields.";
                    return false;
                }
                parsed.Add(new WorkspaceTextReplacement(oldText, newText));
            }

            replacements = parsed.ToArray();
            return true;
        }

        private static bool TryGetJsonString(JsonElement item, string name, out string value)
        {
            value = string.Empty;
            if (!item.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
                return false;
            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static bool HasUnknownProperties(JsonElement item, params string[] allowedNames)
        {
            var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
            return item.EnumerateObject().Any(property => !allowed.Contains(property.Name));
        }

        private static bool TryExtractPreviewId(string content, out string previewId)
        {
            previewId = (content ?? string.Empty)
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .FirstOrDefault(line => line.StartsWith("preview_id:", StringComparison.OrdinalIgnoreCase))?
                ["preview_id:".Length..]
                .Trim() ?? string.Empty;
            return IsSinglePreviewId(previewId);
        }

        private static bool IsSinglePreviewId(string previewId)
        {
            return previewId.StartsWith("workspace-patch:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-patch:".Length + 32
                || previewId.StartsWith("workspace-create:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-create:".Length + 32
                || previewId.StartsWith("workspace-delete:", StringComparison.Ordinal)
                    && previewId.Length == "workspace-delete:".Length + 32;
        }

        private static bool IsInsideWritableRoot(CopilotAgentRequest request, string fullPath)
        {
            foreach (var root in CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.WritableLocalRootPaths))
            {
                var relative = Path.GetRelativePath(root, fullPath);
                if (!Path.IsPathRooted(relative)
                    && !string.Equals(relative, "..", StringComparison.Ordinal)
                    && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private readonly record struct WorkspaceEnvelopeOperation(
            WorkspacePatchOperation Kind,
            string Path,
            WorkspaceTextReplacement[] Replacements,
            string Content);
    }
}
