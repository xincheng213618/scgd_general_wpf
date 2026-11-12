using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.ImageEditor.Output;

internal static class StagedFilePublication
{
    internal readonly record struct Entry(string Temporary, string Target);
    private readonly record struct Change(string Target, string? Backup, bool Published);
    private static readonly object PublicationGate = new();

    internal static void Commit(IReadOnlyList<Entry> files, bool overwrite, CancellationToken cancellationToken)
    {
        // Staging can run concurrently. Serialize the short publication and rollback of file sets.
        lock (PublicationGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var changes = new List<Change>(files.Count);
            try
            {
                foreach (Entry file in files)
                {
                    string? backup = null;
                    if (overwrite && File.Exists(file.Target))
                    {
                        backup = $"{file.Target}.{Guid.NewGuid():N}.bak";
                        File.Move(file.Target, backup);
                    }

                    var change = new Change(file.Target, backup, Published: false);
                    changes.Add(change);
                    File.Move(file.Temporary, file.Target);
                    changes[^1] = change with { Published = true };
                }
            }
            catch (Exception failure)
            {
                List<Exception>? rollbackFailures = null;
                for (int index = changes.Count - 1; index >= 0; index--)
                {
                    Change change = changes[index];
                    try
                    {
                        if (change.Published) File.Delete(change.Target);
                        if (change.Backup != null) File.Move(change.Backup, change.Target);
                    }
                    catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                    {
                        (rollbackFailures ??= [failure]).Add(new IOException(
                            $"Could not roll back '{change.Target}'. Backup path: {change.Backup ?? "(none)"}", rollback));
                    }
                }
                if (rollbackFailures != null)
                    throw new AggregateException("Publishing the export failed and some files could not be restored.", rollbackFailures);
                throw;
            }

            foreach (Change change in changes)
            {
                if (change.Backup == null) continue;
                try { File.Delete(change.Backup); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
