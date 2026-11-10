using ColorVision.Engine.Templates.Flow.Versioning;
using log4net;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing;

internal static class StandaloneFlowFileStore
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(StandaloneFlowFileStore));

    public static string? CaptureSelectedDestination(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            return ComputeContentHash(stream);
        }
        catch (FileNotFoundException) { return null; }
    }

    public static string Save(string path, byte[] canvasData, string? expectedContentHash)
    {
        string destination = Path.GetFullPath(path);
        string temporaryPath = Path.Combine(Path.GetDirectoryName(destination)!, $".colorvision-flow-{Guid.NewGuid():N}.tmp");
        string newContentHash = FlowSemanticHash.ComputeBinaryHash(canvasData);
        using var mutex = new Mutex(initiallyOwned: false, GetSaveMutexName(destination));
        AcquireSaveLock(mutex);
        bool created = false;
        try
        {
            // Keep ordinary writers out while checking the loaded revision and replacing it.
            using FileStream? current = expectedContentHash == null ? null : OpenForReplacement(destination);
            if (current != null && !string.Equals(ComputeContentHash(current), expectedContentHash, StringComparison.Ordinal))
                throw new StandaloneFlowFileConflictException();

            using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                temporary.Write(canvasData);
                temporary.Flush(flushToDisk: true);
            }

            if (current != null) Replace(temporaryPath, destination);
            else File.Move(temporaryPath, destination);
            return newContentHash;
        }
        catch
        {
            if (created) TryDelete(temporaryPath);
            throw;
        }
        finally { mutex.ReleaseMutex(); }
    }

    internal static string GetSaveMutexName(string destination)
        => @"Local\ColorVision.StandaloneFlow." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(destination.ToUpperInvariant())));

    private static string ComputeContentHash(Stream stream)
        => Convert.ToHexStringLower(SHA256.HashData(stream));

    private static void AcquireSaveLock(Mutex mutex)
    {
        try
        {
            if (!mutex.WaitOne(0))
                throw new IOException(EngineLocalization.Get("Flow_StandaloneFileSaveBusy"));
        }
        catch (AbandonedMutexException)
        {
            // Ownership transfers to this thread. The file revision will be checked again.
        }
    }

    private static void Replace(string temporaryPath, string destination)
    {
        // ReplaceFile may leave the previous file under the backup name if the final rename fails.
        string backupPath = Path.ChangeExtension(temporaryPath, ".bak");
        try
        {
            File.Replace(temporaryPath, destination, backupPath, ignoreMetadataErrors: true);
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                try { File.Move(backupPath, destination); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Log.Warn($"Previous standalone flow preserved at {backupPath}.", error);
                }
            }
            throw;
        }
        TryDelete(backupPath);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Failed to remove standalone flow temporary file {path}.", error);
        }
    }

    private static FileStream OpenForReplacement(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new StandaloneFlowFileConflictException(error);
        }
    }
}

internal sealed class StandaloneFlowFileConflictException(Exception? innerException = null)
    : IOException(EngineLocalization.Get("Flow_StandaloneFileConflict"), innerException);
