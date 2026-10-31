using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI
{
    /// <summary>
    /// File-level persistence for configuration JSON: the cross-handler file lock, validated reads
    /// and atomic replacement. It knows nothing about configuration types or cached instances.
    /// </summary>
    internal static class ConfigFileStore
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigFileStore));
        private static readonly TimeSpan SaveFileLockTimeout = TimeSpan.FromSeconds(30);
        private static readonly string[] ObsoleteSectionNames =
        {
            "ConfigOptions",
            "MarketplaceServiceConfig"
        };

        public static IDisposable AcquireLock(string fullPath)
        {
            string canonicalPath = Path.GetFullPath(fullPath).ToUpperInvariant();
            string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath)));
            var mutex = new Mutex(initiallyOwned: false, $"Local\\ColorVision.ConfigSave.{pathHash}");
            try
            {
                var acquired = false;
                try
                {
                    acquired = mutex.WaitOne(SaveFileLockTimeout);
                }
                catch (AbandonedMutexException)
                {
                    acquired = true;
                }

                if (!acquired)
                {
                    throw new TimeoutException(
                        $"Timed out waiting to save configuration file '{fullPath}'.");
                }

                return new LockLease(mutex);
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        private sealed class LockLease : IDisposable
        {
            private Mutex? _mutex;

            public LockLease(Mutex mutex)
            {
                _mutex = mutex;
            }

            public void Dispose()
            {
                var mutex = Interlocked.Exchange(ref _mutex, null);
                if (mutex == null)
                    return;

                try
                {
                    mutex.ReleaseMutex();
                }
                finally
                {
                    mutex.Dispose();
                }
            }
        }

        /// <summary>Reads the merge target for a save: a missing file is empty, an invalid one is refused.</summary>
        public static JObject ReadForMerge(string fileName)
        {
            if (!File.Exists(fileName))
                return new JObject();

            Exception? readException = null;
            if (TryRead(fileName, out var jObject, ex => readException = ex))
            {
                foreach (string sectionName in ObsoleteSectionNames)
                    jObject.Remove(sectionName);
                return jObject;
            }

            throw new InvalidDataException(
                $"Existing configuration file '{fileName}' is not a valid JSON object.",
                readException);
        }

        public static bool TryRead(string fileName, out JObject jObject, Action<Exception> logException)
        {
            jObject = new JObject();
            if (!File.Exists(fileName))
                return false;

            try
            {
                using StreamReader file = File.OpenText(fileName);
                using JsonTextReader reader = new(file);
                jObject = JObject.Load(reader);
                if (reader.Read())
                    throw new JsonReaderException("Additional content was found after the configuration JSON object.");
                return true;
            }
            catch (Exception ex)
            {
                logException(ex);
                return false;
            }
        }

        public static void Write(string fileName, JObject jObject)
        {
            string fullPath = Path.GetFullPath(fileName);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new ArgumentException("Configuration path must include a directory.", nameof(fileName));
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                using (var file = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    4096,
                    leaveOpen: true))
                using (var writer = new JsonTextWriter(file) { CloseOutput = false })
                {
                    jObject.WriteTo(writer);
                    writer.Flush();
                    file.Flush();
                    stream.Flush(flushToDisk: true);
                }

                Exception? validationException = null;
                if (!TryRead(temporaryPath, out _, ex => validationException = ex))
                {
                    throw new InvalidDataException(
                        $"Temporary configuration file '{temporaryPath}' failed JSON validation.",
                        validationException);
                }

                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception ex)
                {
                    log.Warn($"Failed to remove temporary configuration file '{temporaryPath}'.", ex);
                }
            }
        }
    }
}
