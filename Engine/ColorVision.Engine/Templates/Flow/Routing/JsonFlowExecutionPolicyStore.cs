using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ColorVision.Engine.Templates.Flow.Routing
{
    /// <summary>
    /// Atomic JSON sidecar storage. Files are addressed by a SHA-256 of the
    /// stable FlowKey, so no FlowKey can escape the injected directory.
    /// The legacy STN/CVFlow payload is never read or written here.
    /// </summary>
    public sealed class JsonFlowExecutionPolicyStore :
        IFlowExecutionPolicyStore
    {
        private const int CurrentSchemaVersion = 1;

        private static readonly ConcurrentDictionary<string, object> PathLocks =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters =
            {
                new JsonStringEnumConverter(),
            },
        };

        private readonly string directoryPath;

        public JsonFlowExecutionPolicyStore(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException(
                    "执行策略侧车目录不能为空。",
                    nameof(directoryPath));
            }
            this.directoryPath = Path.GetFullPath(directoryPath);
        }

        public FlowExecutionPolicySnapshot Load(string flowKey)
        {
            string normalizedFlowKey =
                FlowExecutionPolicyRules.NormalizeFlowKey(flowKey);
            string filePath = GetFilePath(normalizedFlowKey);
            lock (GetPathLock(filePath))
                return LoadCore(normalizedFlowKey, filePath);
        }

        public bool TryLoad(
            string flowKey,
            out FlowExecutionPolicySnapshot snapshot,
            out string? failureReason)
        {
            string normalizedFlowKey =
                FlowExecutionPolicyRules.NormalizeFlowKey(flowKey);

            string filePath = GetFilePath(normalizedFlowKey);
            lock (GetPathLock(filePath))
            {
                try
                {
                    snapshot = LoadCore(normalizedFlowKey, filePath);
                    failureReason = null;
                    return true;
                }
                catch (Exception ex)
                    when (ex is IOException
                        || ex is UnauthorizedAccessException
                        || ex is JsonException
                        || ex is ArgumentException
                        || ex is InvalidOperationException)
                {
                    snapshot = CreateEmptySnapshot(normalizedFlowKey);
                    failureReason = ex.Message;
                    return false;
                }
            }
        }

        public FlowExecutionPolicySnapshot Save(
            FlowExecutionPolicySaveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.ExpectedRevision < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    request.ExpectedRevision,
                    "期望 revision 不能小于零。");
            }

            NormalizedFlowExecutionPolicy normalized =
                FlowExecutionPolicyRules.Normalize(
                    request.FlowKey,
                    request.ErrorRoutes,
                    request.RetryPolicies);
            string filePath = GetFilePath(normalized.FlowKey);
            lock (GetPathLock(filePath))
            {
                using Mutex interprocessLock = new(
                    initiallyOwned: false,
                    GetMutexName(filePath));
                bool lockAcquired = false;
                try
                {
                    try
                    {
                        lockAcquired = interprocessLock.WaitOne(
                            TimeSpan.FromSeconds(15));
                    }
                    catch (AbandonedMutexException)
                    {
                        lockAcquired = true;
                    }
                    if (!lockAcquired)
                    {
                        throw new IOException(
                            $"等待执行策略文件锁超时：{filePath}");
                    }

                    FlowExecutionPolicySnapshot current =
                        LoadCore(normalized.FlowKey, filePath);
                    if (current.Revision != request.ExpectedRevision)
                    {
                        throw new FlowExecutionPolicyConflictException(
                            normalized.FlowKey,
                            request.ExpectedRevision,
                            current.Revision);
                    }

                    long revision = checked(current.Revision + 1);
                    DateTime updatedTimeUtc = DateTime.UtcNow;
                    var document = new FlowExecutionPolicyFileDocument
                    {
                        SchemaVersion = CurrentSchemaVersion,
                        FlowKey = normalized.FlowKey,
                        Revision = revision,
                        ContentHash = normalized.ContentHash,
                        UpdatedTimeUtc = updatedTimeUtc.ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                        ErrorRoutes = normalized.ErrorRoutes,
                        RetryPolicies = normalized.RetryPolicies,
                    };

                    WriteAtomic(filePath, document);
                    return CreateSnapshot(
                        normalized,
                        revision,
                        updatedTimeUtc);
                }
                finally
                {
                    if (lockAcquired)
                        interprocessLock.ReleaseMutex();
                }
            }
        }

        private static FlowExecutionPolicySnapshot LoadCore(
            string flowKey,
            string filePath)
        {
            if (!File.Exists(filePath))
                return CreateEmptySnapshot(flowKey);

            FlowExecutionPolicyFileDocument? document;
            try
            {
                using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                document =
                    JsonSerializer.Deserialize<FlowExecutionPolicyFileDocument>(
                        stream,
                        JsonOptions);
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is JsonException
                    || ex is NotSupportedException)
            {
                throw Corrupt(
                    flowKey,
                    filePath,
                    "JSON 无法读取。",
                    ex);
            }

            if (document == null)
                throw Corrupt(flowKey, filePath, "JSON 根对象为空。");
            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                throw Corrupt(
                    flowKey,
                    filePath,
                    $"不支持 schemaVersion {document.SchemaVersion}。");
            }
            if (document.Revision <= 0)
                throw Corrupt(flowKey, filePath, "revision 必须大于零。");

            string documentFlowKey;
            NormalizedFlowExecutionPolicy normalized;
            DateTime updatedTimeUtc;
            string contentHash;
            try
            {
                documentFlowKey =
                    FlowExecutionPolicyRules.NormalizeFlowKey(
                        document.FlowKey);
                normalized = FlowExecutionPolicyRules.Normalize(
                    documentFlowKey,
                    document.ErrorRoutes,
                    document.RetryPolicies);
                contentHash =
                    FlowExecutionPolicyRules.NormalizeHash(
                        document.ContentHash);
                if (!DateTime.TryParse(
                    document.UpdatedTimeUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime parsedUpdatedTimeUtc))
                {
                    throw new InvalidDataException("更新时间不是有效时间。");
                }
                updatedTimeUtc =
                    FlowExecutionPolicyRules.NormalizeUtc(
                        parsedUpdatedTimeUtc);
            }
            catch (Exception ex)
                when (ex is ArgumentException
                    || ex is InvalidDataException
                    || ex is InvalidOperationException)
            {
                throw Corrupt(
                    flowKey,
                    filePath,
                    "侧车内容未通过校验。",
                    ex);
            }

            if (!string.Equals(
                flowKey,
                documentFlowKey,
                StringComparison.Ordinal))
            {
                throw Corrupt(
                    flowKey,
                    filePath,
                    $"文件内 FlowKey 为 {documentFlowKey}。");
            }
            if (!string.Equals(
                normalized.ContentHash,
                contentHash,
                StringComparison.Ordinal))
            {
                throw Corrupt(flowKey, filePath, "内容哈希不匹配。");
            }

            return CreateSnapshot(
                normalized,
                document.Revision,
                updatedTimeUtc);
        }

        private void WriteAtomic(
            string filePath,
            FlowExecutionPolicyFileDocument document)
        {
            Directory.CreateDirectory(directoryPath);
            string temporaryPath = Path.Combine(
                directoryPath,
                $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (FileStream stream = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 16_384,
                    FileOptions.WriteThrough))
                {
                    JsonSerializer.Serialize(stream, document, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(filePath))
                {
                    File.Replace(
                        temporaryPath,
                        filePath,
                        destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporaryPath, filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private string GetFilePath(string flowKey)
        {
            byte[] digest = SHA256.HashData(
                Encoding.UTF8.GetBytes(flowKey));
            string fileName =
                Convert.ToHexString(digest).ToLowerInvariant()
                + ".flow-routing.json";
            return Path.Combine(directoryPath, fileName);
        }

        private static object GetPathLock(string filePath)
        {
            return PathLocks.GetOrAdd(
                Path.GetFullPath(filePath),
                static _ => new object());
        }

        private static string GetMutexName(string filePath)
        {
            byte[] digest = SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    Path.GetFullPath(filePath).ToUpperInvariant()));
            return @"Local\ColorVision.FlowExecutionPolicy."
                + Convert.ToHexString(digest);
        }

        private static FlowExecutionPolicySnapshot CreateEmptySnapshot(
            string flowKey)
        {
            NormalizedFlowExecutionPolicy empty =
                FlowExecutionPolicyRules.Normalize(
                    flowKey,
                    Array.Empty<FlowErrorRoutePolicy>(),
                    Array.Empty<FlowRetryPolicy>());
            return CreateSnapshot(
                empty,
                revision: 0,
                updatedTimeUtc: DateTime.UnixEpoch);
        }

        private static FlowExecutionPolicySnapshot CreateSnapshot(
            NormalizedFlowExecutionPolicy normalized,
            long revision,
            DateTime updatedTimeUtc)
        {
            return new FlowExecutionPolicySnapshot(
                normalized.FlowKey,
                revision,
                normalized.ContentHash,
                updatedTimeUtc,
                normalized.ErrorRoutes,
                normalized.RetryPolicies);
        }

        private static FlowExecutionPolicyCorruptException Corrupt(
            string flowKey,
            string filePath,
            string message,
            Exception? innerException = null)
        {
            return new FlowExecutionPolicyCorruptException(
                flowKey,
                filePath,
                message,
                innerException);
        }

        private sealed class FlowExecutionPolicyFileDocument
        {
            public int SchemaVersion { get; init; }

            public string FlowKey { get; init; } = string.Empty;

            public long Revision { get; init; }

            public string ContentHash { get; init; } = string.Empty;

            public string UpdatedTimeUtc { get; init; } = string.Empty;

            public IReadOnlyList<FlowErrorRoutePolicy> ErrorRoutes
            {
                get;
                init;
            } = Array.Empty<FlowErrorRoutePolicy>();

            public IReadOnlyList<FlowRetryPolicy> RetryPolicies
            {
                get;
                init;
            } = Array.Empty<FlowRetryPolicy>();
        }
    }
}
