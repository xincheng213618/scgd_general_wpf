using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotWorkspacePatchStore
    {
        private void RestoreState(WorkspacePatchRecord record, WorkspacePatchState state)
        {
            lock (_syncRoot)
                record.State = state;
        }

        private bool RestoreCreationStateAfterFailure(WorkspacePatchRecord record, string fullPath)
        {
            var reachedTargetState = false;
            try
            {
                reachedTargetState = File.Exists(fullPath)
                    && string.Equals(Hash(File.ReadAllBytes(fullPath)), record.AfterSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
            }
            lock (_syncRoot)
                record.State = reachedTargetState ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed;
            return reachedTargetState;
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
            RemoveExpiredChangeSets(now);
            foreach (var key in _records.Where(pair => pair.Value.ExpiresAtUtc <= now).Select(pair => pair.Key).ToArray())
                _records.Remove(key);
        }

        private void StoreRecord(WorkspacePatchRecord record, DateTimeOffset now)
        {
            lock (_syncRoot)
            {
                RemoveExpiredEntries(now);
                if (_records.Count >= MaxEntries)
                {
                    var oldest = _records.Values
                        .Where(item => string.IsNullOrWhiteSpace(item.ChangeSetId))
                        .OrderBy(item => item.CreatedAtUtc)
                        .FirstOrDefault();
                    if (oldest != null)
                        _records.Remove(oldest.PreviewId);
                }
                _records[record.PreviewId] = record;
            }
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

        private static async Task<IReadOnlyList<string>> CreateNewFileAtomicallyAsync(
            string fullPath,
            string writableRoot,
            byte[] content,
            CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The target file has no parent directory.");
            var missingDirectories = GetMissingDirectories(writableRoot, directory);
            if (missingDirectories.Count == 0)
            {
                await WriteNewFileInExistingDirectoryAsync(fullPath, content, cancellationToken);
                return Array.Empty<string>();
            }

            var firstMissingDirectory = missingDirectories[0];
            var existingParent = Path.GetDirectoryName(firstMissingDirectory)
                ?? throw new InvalidOperationException("The first missing directory has no existing parent.");
            var stagingRoot = Path.Combine(existingParent, $".copilot-directory-{Guid.NewGuid():N}.tmp");
            var stagingTargetDirectory = stagingRoot;
            for (var index = 1; index < missingDirectories.Count; index++)
                stagingTargetDirectory = Path.Combine(stagingTargetDirectory, Path.GetFileName(missingDirectories[index]));

            var stagingFile = Path.Combine(stagingTargetDirectory, Path.GetFileName(fullPath));
            var moved = false;
            try
            {
                Directory.CreateDirectory(stagingTargetDirectory);
                await WriteFileBytesAsync(stagingFile, content, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(stagingRoot, firstMissingDirectory);
                moved = true;
                return missingDirectories;
            }
            finally
            {
                if (!moved && Directory.Exists(stagingRoot))
                    RemoveStagingTree(stagingRoot, stagingTargetDirectory, stagingFile);
            }
        }

        private static List<string> GetMissingDirectories(string writableRoot, string targetDirectory)
        {
            var missing = new List<string>();
            var current = writableRoot;
            foreach (var segment in Path.GetRelativePath(writableRoot, targetDirectory)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new UnauthorizedAccessException("Creating files through a directory reparse point is not allowed.");
                    continue;
                }
                if (File.Exists(current))
                    throw new IOException("A file occupies a required parent-directory path: " + current);
                missing.Add(current);
            }
            return missing;
        }

        private static async Task WriteNewFileInExistingDirectoryAsync(
            string fullPath,
            byte[] content,
            CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("The target file has no parent directory.");
            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.copilot-{Guid.NewGuid():N}.tmp");
            try
            {
                await WriteFileBytesAsync(temporaryPath, content, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, fullPath, overwrite: false);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static async Task WriteFileBytesAsync(string path, byte[] content, CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        private static void RemoveEmptyCreatedDirectories(IEnumerable<string> directories)
        {
            foreach (var directory in directories.Reverse())
            {
                try
                {
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        Directory.Delete(directory, recursive: false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }

        private static void RemoveStagingTree(string stagingRoot, string deepestDirectory, string stagingFile)
        {
            try
            {
                if (File.Exists(stagingFile))
                    File.Delete(stagingFile);
                var current = deepestDirectory;
                while (current.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
                        Directory.Delete(current, recursive: false);
                    if (string.Equals(current, stagingRoot, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = Path.GetDirectoryName(current) ?? string.Empty;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
