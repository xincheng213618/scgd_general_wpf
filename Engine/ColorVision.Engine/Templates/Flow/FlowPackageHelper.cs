#pragma warning disable CS8603,CS8625
using ColorVision.Database;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// 流程包数据模型，用于导出/导入流程及其关联的模板
    /// </summary>
    public class FlowPackageManifest
    {
        public string Schema { get; set; } = string.Empty;
        public string FlowName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string FlowContentHash { get; set; } = string.Empty;
        public List<FlowPackageTemplate> Templates { get; set; } = new List<FlowPackageTemplate>();
    }

    /// <summary>
    /// 流程包中的模板数据
    /// </summary>
    public class FlowPackageTemplate
    {
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateCode { get; set; } = string.Empty;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int TemplateDicId { get; set; }
        public string PackageTemplateId { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public string? ContentEntry { get; set; }
        public List<string> Dependencies { get; set; } = new List<string>();
        public string? SerializedContent { get; set; }
        public List<FlowPackageDetailItem> Details { get; set; } = new List<FlowPackageDetailItem>();
    }

    public sealed class FlowPackageTemplatePayload
    {
        public string? SerializedContent { get; set; }
        public List<FlowPackageDetailItem> Details { get; set; } = new();
    }

    /// <summary>
    /// 模板详情项 (对应 ModDetailModel 的可序列化版本)
    /// </summary>
    public class FlowPackageDetailItem
    {
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int SysPid { get; set; }
        public string? Symbol { get; set; }
        public long? AddressCode { get; set; }
        public string? ValueA { get; set; }
        public string? ValueB { get; set; }
        public bool IsEnable { get; set; } = true;
        public bool IsDelete { get; set; }
    }

    /// <summary>
    /// STN 数据解析和修改工具
    /// </summary>
    public static class FlowPackageHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowPackageHelper));
        private static readonly object TemplateImportSync = new();
        private const string CurrentPackageSchema = "colorvision.cvflow";
        private const string CurrentPackageVersion = "3.0";
        private const long MaxManifestBytes = 4 * 1024 * 1024;
        private const long MaxFlowBytes =
            FlowPackageStnValidator.MaximumStndLength;
        private const long MaxTemplatePayloadBytes = 16 * 1024 * 1024;
        private const int MaxTemplateCount = 4096;
        private const int MaxArchiveEntryCount = 8192;
        private const long MaxArchiveUncompressedBytes = 512L * 1024 * 1024;

        /// <summary>
        /// 已知的模板属性名称集合 (STNodeProperty 标记的模板引用属性)
        /// </summary>
        private static readonly HashSet<string> TemplatePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TempName",
            "TemplateName",
            "CalibTempName",
            "CaliTempName",
            "POITempName",
            "POIFilterTempName",
            "POIReviseTempName",
            "FilterTemplateName",
            "ReviseTemplateName",
            "OutputTemplateName",
            "RePOITemplateName",
            "SavePOITempName",
            "XRTempName",
            "CamTempName",
            "ExpTempName",
            "AutoExpTempName",
            "FocusTempName",
            "AlgTempName",
            "AutoFocusTemp",
            "ModelName",
            "LayoutROITemplate",
            "LayoutROITemplateName",
            "LayoutTemplateName",
            "ParameterTemplateName",
            "SubPixelTemplateName",
            "OutputTempName",
            "PoiTemplateName",
        };

        /// <summary>
        /// 从 STN 数据中提取所有被节点引用的模板名称
        /// </summary>
        public static HashSet<string> ExtractTemplateNames(byte[] stnData)
        {
            var templateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (stnData == null || stnData.Length < 5)
                return templateNames;

            byte[] decompressed = DecompressSTN(stnData);
            if (decompressed == null)
                return templateNames;

            ParseNodesForTemplateNames(decompressed, templateNames, null);
            return templateNames;
        }

        /// <summary>
        /// 替换 STN 数据中的模板名称引用，返回修改后的 STN 数据
        /// </summary>
        public static byte[] ReplaceTemplateNames(byte[] stnData, Dictionary<string, string> nameMap)
        {
            if (stnData == null || stnData.Length < 5 || nameMap == null || nameMap.Count == 0)
                return stnData;

            byte[] decompressed = DecompressSTN(stnData);
            if (decompressed == null)
                return stnData;

            byte[] modified = RebuildDecompressedData(decompressed, nameMap);
            return CompressSTN(modified);
        }

        /// <summary>
        /// 将流程数据和模板数据打包为 .cvflow ZIP 文件
        /// </summary>
        public static void ExportFlowPackage(string outputPath, string flowName, byte[] stnData, FlowPackageManifest manifest)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(flowName);
            ArgumentNullException.ThrowIfNull(stnData);
            ArgumentNullException.ThrowIfNull(manifest);
            if (stnData.LongLength > MaxFlowBytes)
            {
                throw new InvalidDataException(
                    "flow.stn 超过 cvflow 允许大小。");
            }
            ValidateStndV1Payload(stnData);
            ValidateExportSource(manifest);

            string fullOutputPath = Path.GetFullPath(outputPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath)
                ?? throw new InvalidOperationException("无法确定 cvflow 输出目录。");
            string temporaryPath = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var zipToOpen = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None))
                using (var archive = new ZipArchive(
                    zipToOpen,
                    ZipArchiveMode.Create))
                {
                    // STN remains an opaque, byte-for-byte compatible payload.
                    var stnEntry = archive.CreateEntry("flow.stn");
                    using (var stream = stnEntry.Open())
                    {
                        stream.Write(stnData, 0, stnData.Length);
                    }

                    FlowPackageManifest exportManifest =
                        WriteTemplatePayloads(
                            archive,
                            stnEntry,
                            manifest,
                            flowName,
                            stnData);

                    // Write the manifest last so an incomplete archive is
                    // never mistaken for a complete package.
                    var manifestEntry = archive.CreateEntry(
                        "manifest.json",
                        CompressionLevel.Optimal);
                    using var manifestStream = manifestEntry.Open();
                    using var writer = new StreamWriter(
                        manifestStream,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false));
                    var json = JsonConvert.SerializeObject(
                        exportManifest,
                        Formatting.Indented,
                        new JsonSerializerSettings
                        {
                            NullValueHandling =
                                NullValueHandling.Ignore,
                        });
                    if (Encoding.UTF8.GetByteCount(json)
                        > MaxManifestBytes)
                    {
                        throw new InvalidDataException(
                            "cvflow 清单超过允许大小。");
                    }
                    writer.Write(json);
                }

                File.Move(
                    temporaryPath,
                    fullOutputPath,
                    overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// 从 .cvflow ZIP 文件中读取流程包数据
        /// </summary>
        public static (byte[] StnData, FlowPackageManifest? Manifest) ImportFlowPackage(string inputPath)
        {
            byte[]? stnData = null;
            FlowPackageManifest? manifest = null;

            using var zipToOpen = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read);
            IReadOnlyDictionary<string, ZipArchiveEntry> entries =
                BuildEntryIndex(archive);

            entries.TryGetValue("flow.stn", out var stnEntry);
            if (stnEntry != null)
            {
                ValidateEntrySize(stnEntry, MaxFlowBytes, "flow.stn");
                using var stream = stnEntry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                stnData = ms.ToArray();
            }

            entries.TryGetValue("manifest.json", out var manifestEntry);
            if (manifestEntry != null)
            {
                ValidateEntrySize(
                    manifestEntry,
                    MaxManifestBytes,
                    "manifest.json");
                using var stream = manifestEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var json = reader.ReadToEnd();
                manifest = JsonConvert.DeserializeObject<FlowPackageManifest>(json);
            }

            if (manifest != null)
            {
                int packageMajorVersion =
                    GetPackageMajorVersion(manifest);
                if (packageMajorVersion > 3)
                {
                    throw new NotSupportedException(
                        $"不支持 cvflow v{manifest.Version}，"
                        + $"当前最高支持 {CurrentPackageVersion}。");
                }
                if (stnData != null)
                {
                    ValidateStndV1Payload(stnData);
                }
                bool versionThree = packageMajorVersion == 3;
                if (versionThree)
                {
                    ValidateVersionThreeManifest(
                        manifest,
                        stnEntry);
                }
                HydrateAndValidateTemplatePayloads(
                    entries,
                    manifest,
                    requireExternalPayload: versionThree);
                if (versionThree)
                {
                    ValidateVersionThreeTemplatePayloads(
                        manifest);
                    ValidateDeclaredTemplateDependencies(
                        manifest.Templates
                            ?? new List<FlowPackageTemplate>());
                }
                if (!string.IsNullOrWhiteSpace(
                        manifest.FlowContentHash))
                {
                    string actualFlowHash =
                        ComputeSha256(stnData ?? Array.Empty<byte>());
                    if (!string.Equals(
                            manifest.FlowContentHash,
                            actualFlowHash,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "cvflow 中 flow.stn 的内容校验失败。");
                    }
                }
            }
            else if (stnData != null)
            {
                ValidateStndV1Payload(stnData);
            }

            return (stnData ?? Array.Empty<byte>(), manifest);
        }

        private static FlowPackageManifest WriteTemplatePayloads(
            ZipArchive archive,
            ZipArchiveEntry stnEntry,
            FlowPackageManifest source,
            string flowName,
            byte[] stnData)
        {
            var result = new FlowPackageManifest
            {
                Schema = CurrentPackageSchema,
                FlowName = string.IsNullOrWhiteSpace(source.FlowName)
                    ? flowName
                    : source.FlowName,
                Version = CurrentPackageVersion,
                PackageId = Guid.TryParseExact(
                    source.PackageId,
                    "N",
                    out Guid packageId)
                    ? packageId.ToString("N")
                    : Guid.NewGuid().ToString("N"),
                CreatedUtc = source.CreatedUtc == default
                    ? DateTime.UtcNow
                    : source.CreatedUtc,
                FlowContentHash = ComputeSha256(stnData),
            };
            var writtenPayloadEntries =
                new HashSet<string>(StringComparer.Ordinal);
            long totalUncompressedBytes =
                stnData.LongLength + MaxManifestBytes;

            foreach (FlowPackageTemplate template
                in (source.Templates ?? new List<FlowPackageTemplate>())
                    .OrderBy(
                        template => template.TemplateCode,
                        StringComparer.Ordinal)
                    .ThenBy(
                        template => template.TemplateName,
                        StringComparer.Ordinal))
            {
                ValidatePortableTemplateDetails(template);
                FlowPackageTemplatePayload payload =
                    FlowPackageContentIdentity.CreatePayload(template);
                string payloadJson =
                    FlowPackageContentIdentity.SerializePayload(payload);
                if (Encoding.UTF8.GetByteCount(payloadJson)
                    > MaxTemplatePayloadBytes)
                {
                    throw new InvalidDataException(
                        $"cvflow 模板内容超过允许大小："
                        + template.TemplateName);
                }
                string payloadHash =
                    FlowPackageContentIdentity.ComputePayloadHash(
                        payloadJson);
                string contentHash =
                    FlowPackageContentIdentity.ComputeContentHash(
                        template.TemplateCode,
                        template.SerializedContent,
                        template.Details);
                string contentEntry =
                    $"templates/{payloadHash}.json";

                if (writtenPayloadEntries.Add(contentEntry))
                {
                    totalUncompressedBytes = checked(
                        totalUncompressedBytes
                        + Encoding.UTF8.GetByteCount(payloadJson));
                    if (totalUncompressedBytes
                        > MaxArchiveUncompressedBytes)
                    {
                        throw new InvalidDataException(
                            "cvflow 解压后的条目总大小超过允许值。");
                    }
                    ZipArchiveEntry payloadEntry =
                        archive.CreateEntry(
                            contentEntry,
                            CompressionLevel.Optimal);
                    using Stream stream = payloadEntry.Open();
                    using var writer = new StreamWriter(
                        stream,
                        new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false));
                    writer.Write(payloadJson);
                }

                result.Templates.Add(
                    new FlowPackageTemplate
                    {
                        TemplateName = template.TemplateName,
                        TemplateCode = template.TemplateCode,
                        // v3 resolves the local dictionary by TemplateCode.
                        // Database ids are intentionally not portable.
                        TemplateDicId = 0,
                        PackageTemplateId = ComputePackageTemplateId(
                            template.TemplateCode,
                            template.TemplateName,
                            contentHash),
                        ContentHash = contentHash,
                        PayloadHash = payloadHash,
                        ContentEntry = contentEntry,
                        Dependencies = (template.Dependencies
                                ?? new List<string>())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(
                                dependency => dependency,
                                StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        SerializedContent = null,
                        Details = new List<FlowPackageDetailItem>(),
                    });
            }
            ValidateVersionThreeManifest(
                result,
                stnEntry);
            return result;
        }

        private static void HydrateAndValidateTemplatePayloads(
            IReadOnlyDictionary<string, ZipArchiveEntry> entries,
            FlowPackageManifest manifest,
            bool requireExternalPayload)
        {
            var payloadCache =
                new Dictionary<
                    string,
                    (string Hash, FlowPackageTemplatePayload Payload)>(
                    StringComparer.OrdinalIgnoreCase);
            foreach (FlowPackageTemplate template
                in manifest.Templates ?? new List<FlowPackageTemplate>())
            {
                if (string.IsNullOrWhiteSpace(template.ContentEntry))
                {
                    // v1/v2 packages stored payloads inline.
                    if (requireExternalPayload)
                    {
                        throw new InvalidDataException(
                            $"cvflow v3 模板缺少内容条目："
                            + template.TemplateName);
                    }
                    continue;
                }

                ValidateTemplateEntryName(template.ContentEntry);
                string expectedContentEntry =
                    $"templates/{template.PayloadHash.ToLowerInvariant()}.json";
                if (!template.ContentEntry.Equals(
                        expectedContentEntry,
                        requireExternalPayload
                            ? StringComparison.Ordinal
                            : StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板内容地址无效："
                        + template.TemplateName);
                }
                if (!entries.TryGetValue(
                        template.ContentEntry,
                        out ZipArchiveEntry? entry))
                {
                    throw new InvalidDataException(
                        $"cvflow 缺少模板内容条目："
                        + template.ContentEntry);
                }
                if (requireExternalPayload
                    && !entry.FullName.Equals(
                        template.ContentEntry,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板内容条目大小写不一致："
                        + template.ContentEntry);
                }
                if (!payloadCache.TryGetValue(
                        template.ContentEntry,
                        out var cachedPayload))
                {
                    ValidateEntrySize(
                        entry,
                        MaxTemplatePayloadBytes,
                        template.ContentEntry);

                    string payloadJson;
                    using (Stream stream = entry.Open())
                    using (var reader = new StreamReader(
                        stream,
                        Encoding.UTF8))
                    {
                        payloadJson = reader.ReadToEnd();
                    }

                    string payloadHash =
                        FlowPackageContentIdentity.ComputePayloadHash(
                            payloadJson);
                    if (string.IsNullOrWhiteSpace(template.PayloadHash)
                        || !string.Equals(
                            template.PayloadHash,
                            payloadHash,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"cvflow 模板内容校验失败："
                            + template.TemplateName);
                    }

                    FlowPackageTemplatePayload? parsedPayload =
                        JsonConvert.DeserializeObject<
                            FlowPackageTemplatePayload>(payloadJson);
                    if (parsedPayload == null)
                    {
                        throw new InvalidDataException(
                            $"cvflow 模板内容无法解析："
                            + template.TemplateName);
                    }
                    cachedPayload = (payloadHash, parsedPayload);
                    payloadCache[template.ContentEntry] =
                        cachedPayload;
                }
                else if (!string.Equals(
                    template.PayloadHash,
                    cachedPayload.Hash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板共享内容声明不一致："
                        + template.TemplateName);
                }
                FlowPackageTemplatePayload payload =
                    cachedPayload.Payload;
                template.SerializedContent =
                    payload.SerializedContent;
                template.Details =
                    FlowPackageContentIdentity.CloneDetails(
                        payload.Details);

                string contentHash =
                    FlowPackageContentIdentity.ComputeContentHash(
                        template.TemplateCode,
                        template.SerializedContent,
                        template.Details);
                if (string.IsNullOrWhiteSpace(template.ContentHash)
                    || !string.Equals(
                        template.ContentHash,
                        contentHash,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板语义校验失败："
                        + template.TemplateName);
                }
            }
        }

        private static void ValidateTemplateEntryName(
            string contentEntry)
        {
            if (!contentEntry.StartsWith(
                    "templates/",
                    StringComparison.Ordinal)
                || !contentEntry.EndsWith(
                    ".json",
                    StringComparison.OrdinalIgnoreCase)
                || contentEntry.Contains("..", StringComparison.Ordinal)
                || contentEntry.Contains('\\'))
            {
                throw new InvalidDataException(
                    "cvflow 模板内容路径无效。");
            }
        }

        private static IReadOnlyDictionary<string, ZipArchiveEntry>
            BuildEntryIndex(ZipArchive archive)
        {
            ArgumentNullException.ThrowIfNull(archive);
            if (archive.Entries.Count > MaxArchiveEntryCount)
            {
                throw new InvalidDataException(
                    "cvflow 包含过多条目。");
            }

            var entries =
                new Dictionary<string, ZipArchiveEntry>(
                    StringComparer.OrdinalIgnoreCase);
            long totalLength = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.FullName)
                    || entry.Length < 0
                    || !entries.TryAdd(entry.FullName, entry))
                {
                    throw new InvalidDataException(
                        $"cvflow 包含无效或重复条目："
                        + entry.FullName);
                }
                try
                {
                    totalLength = checked(totalLength + entry.Length);
                }
                catch (OverflowException ex)
                {
                    throw new InvalidDataException(
                        "cvflow 条目总大小无效。",
                        ex);
                }
                if (totalLength > MaxArchiveUncompressedBytes)
                {
                    throw new InvalidDataException(
                        "cvflow 解压后的条目总大小超过允许值。");
                }
            }
            return entries;
        }

        private static void ValidateEntrySize(
            ZipArchiveEntry entry,
            long maximumBytes,
            string displayName)
        {
            if (entry.Length < 0 || entry.Length > maximumBytes)
            {
                throw new InvalidDataException(
                    $"cvflow 条目大小无效：{displayName}");
            }
        }

        private static string ComputeSha256(byte[] data)
        {
            return Convert.ToHexString(SHA256.HashData(data))
                .ToLowerInvariant();
        }

        internal static string ComputePackageTemplateId(
            string templateCode,
            string templateName,
            string contentHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
            if (!IsSha256(contentHash))
            {
                throw new ArgumentException(
                    "模板内容哈希不是有效的 SHA-256。",
                    nameof(contentHash));
            }
            return ComputeSha256(
                Encoding.UTF8.GetBytes(
                    templateCode
                    + "\n"
                    + templateName
                    + "\n"
                    + contentHash.ToLowerInvariant()));
        }

        private static int GetPackageMajorVersion(
            FlowPackageManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(manifest.Version))
            {
                bool hasVersionedFields =
                    !string.IsNullOrWhiteSpace(manifest.Schema)
                    || !string.IsNullOrWhiteSpace(
                        manifest.FlowContentHash)
                    || (manifest.Templates?.Any(template =>
                        !string.IsNullOrWhiteSpace(
                            template.ContentEntry)
                        || !string.IsNullOrWhiteSpace(
                            template.PayloadHash)
                        || !string.IsNullOrWhiteSpace(
                            template.ContentHash)) ?? false);
                if (hasVersionedFields)
                {
                    throw new InvalidDataException(
                        "cvflow 缺少包版本号。");
                }
                return 1;
            }
            if (!Version.TryParse(
                    manifest.Version,
                    out Version? parsed)
                || parsed.Major < 1)
            {
                throw new InvalidDataException(
                    $"cvflow 版本号无效：{manifest.Version}");
            }
            return parsed.Major;
        }

        private static void ValidateExportSource(
            FlowPackageManifest manifest)
        {
            IReadOnlyList<FlowPackageTemplate> templates =
                manifest.Templates
                ?? new List<FlowPackageTemplate>();
            if (templates.Count > MaxTemplateCount)
            {
                throw new InvalidDataException(
                    "cvflow 关联模板数量超过允许值。");
            }

            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (FlowPackageTemplate template in templates)
            {
                if (string.IsNullOrWhiteSpace(
                        template.TemplateName)
                    || string.IsNullOrWhiteSpace(
                        template.TemplateCode)
                    || !names.Add(template.TemplateName))
                {
                    throw new InvalidDataException(
                        "cvflow 包含无效或重复的关联模板。");
                }
                ValidatePortableTemplateDetails(template);
            }

            foreach (FlowPackageTemplate template in templates)
            {
                if ((template.Dependencies
                        ?? new List<string>())
                    .Any(dependency =>
                        string.IsNullOrWhiteSpace(dependency)
                        || !names.Contains(dependency)))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板依赖不完整："
                        + template.TemplateName);
                }
            }
            ValidateDeclaredTemplateDependencies(templates);
        }

        private static void ValidateVersionThreeManifest(
            FlowPackageManifest manifest,
            ZipArchiveEntry? stnEntry)
        {
            if (!string.Equals(
                    manifest.Schema,
                    CurrentPackageSchema,
                    StringComparison.Ordinal)
                || stnEntry == null
                || string.IsNullOrWhiteSpace(manifest.FlowName)
                || !Guid.TryParseExact(
                    manifest.PackageId,
                    "N",
                    out _)
                || manifest.CreatedUtc == default
                || !IsSha256(manifest.FlowContentHash))
            {
                throw new InvalidDataException(
                    "cvflow v3 的格式标识或流程元数据无效。");
            }

            var names = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var packageTemplateIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<FlowPackageTemplate> templates =
                manifest.Templates
                ?? new List<FlowPackageTemplate>();
            if (templates.Count > MaxTemplateCount)
            {
                throw new InvalidDataException(
                    "cvflow v3 关联模板数量超过允许值。");
            }
            foreach (FlowPackageTemplate template
                in templates)
            {
                if (string.IsNullOrWhiteSpace(
                        template.TemplateName)
                    || string.IsNullOrWhiteSpace(
                        template.TemplateCode)
                    || !IsSha256(
                        template.PackageTemplateId)
                    || !packageTemplateIds.Add(
                        template.PackageTemplateId)
                    || !names.Add(template.TemplateName)
                    || !IsSha256(template.ContentHash)
                    || !IsSha256(template.PayloadHash)
                    || template.TemplateDicId != 0
                    || string.IsNullOrWhiteSpace(
                        template.ContentEntry)
                    || template.SerializedContent != null
                    || (template.Details?.Count ?? 0) != 0
                    || !string.Equals(
                        template.PackageTemplateId,
                        ComputePackageTemplateId(
                            template.TemplateCode,
                            template.TemplateName,
                            template.ContentHash),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "cvflow v3 的关联模板元数据无效。");
                }
            }
            foreach (FlowPackageTemplate template
                in templates)
            {
                List<string> dependencies =
                    template.Dependencies
                    ?? new List<string>();
                if (dependencies.Count
                        != dependencies.Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .Count()
                    || dependencies.Any(dependency =>
                        string.IsNullOrWhiteSpace(dependency)
                        || !names.Contains(dependency)))
                {
                    throw new InvalidDataException(
                        $"cvflow v3 模板依赖不完整："
                        + template.TemplateName);
                }
            }
        }

        private static void ValidatePortableTemplateDetails(
            FlowPackageTemplate template)
        {
            var keys = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (FlowPackageDetailItem detail
                in template.Details
                    ?? new List<FlowPackageDetailItem>())
            {
                string? key =
                    FlowPackageContentIdentity
                        .GetStableDetailKey(detail);
                if ((string.IsNullOrWhiteSpace(detail.Symbol)
                        && (!detail.AddressCode.HasValue
                            || detail.AddressCode.Value == 0))
                    || string.IsNullOrWhiteSpace(key)
                    || !keys.Add(key))
                {
                    throw new InvalidDataException(
                        $"cvflow v3 模板参数标识无效："
                        + template.TemplateName);
                }
            }
        }

        private static bool IsSha256(string? value)
        {
            return value?.Length == 64
                && value.All(character =>
                    (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F'));
        }

        private static void ValidateVersionThreeTemplatePayloads(
            FlowPackageManifest manifest)
        {
            foreach (FlowPackageTemplate template
                in manifest.Templates
                    ?? new List<FlowPackageTemplate>())
            {
                ValidatePortableTemplateDetails(template);
            }
        }

        private static void ValidateStndV1Payload(
            byte[] stnData)
        {
            try
            {
                _ = FlowPackageStnValidator
                    .ValidateAndDecompress(stnData);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException(
                    "cvflow 中的 flow.stn 已损坏或超过允许大小。",
                    ex);
            }
        }

        /// <summary>
        /// 解压 STN 数据 (跳过5字节header后GZip解压)
        /// </summary>
        private static byte[]? DecompressSTN(byte[] stnData)
        {
            try
            {
                return FlowPackageStnValidator
                    .ValidateAndDecompress(stnData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 压缩数据为 STN 格式 (加上 "STND" + version header)
        /// </summary>
        private static byte[] CompressSTN(byte[] decompressedData)
        {
            using var output = new MemoryStream();

            // 写入 header
            output.Write(new byte[] { 83, 84, 78, 68 }, 0, 4); // "STND"
            output.WriteByte(1); // version

            using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(decompressedData, 0, decompressedData.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 解析解压后的数据，提取模板名称
        /// </summary>
        private static void ParseNodesForTemplateNames(byte[] data, HashSet<string> templateNames, Dictionary<string, string> nameMap)
        {
            int pos = 0;

            // 跳过 canvas metadata: offsetX(4) + offsetY(4) + scale(4) + nodeCount(4)
            if (data.Length < 16) return;
            int nodeCount = BitConverter.ToInt32(data, 12);
            pos = 16;

            for (int i = 0; i < nodeCount && pos < data.Length; i++)
            {
                if (pos > data.Length
                    || sizeof(int) > data.Length - pos)
                    break;
                int nodeDataLength = BitConverter.ToInt32(data, pos);
                pos += 4;

                if (nodeDataLength < 0
                    || nodeDataLength > data.Length - pos)
                    break;
                byte[] nodeData = new byte[nodeDataLength];
                Array.Copy(data, pos, nodeData, 0, nodeDataLength);
                pos += nodeDataLength;

                // 解析节点数据中的属性
                var properties = ParseNodeProperties(nodeData);
                foreach (var kvp in properties)
                {
                    if (TemplatePropertyNames.Contains(kvp.Key))
                    {
                        string value = Encoding.UTF8.GetString(kvp.Value);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            templateNames?.Add(value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 重建解压后的数据，替换模板名称
        /// </summary>
        private static byte[] RebuildDecompressedData(byte[] data, Dictionary<string, string> nameMap)
        {
            int pos = 0;
            if (data.Length < 16) return data;

            // 读取 canvas metadata
            byte[] canvasMetadata = new byte[16];
            Array.Copy(data, 0, canvasMetadata, 0, 16);
            int nodeCount = BitConverter.ToInt32(data, 12);
            pos = 16;

            var output = new List<byte>();
            output.AddRange(canvasMetadata);

            // 处理每个节点
            for (int i = 0; i < nodeCount && pos < data.Length; i++)
            {
                if (pos > data.Length
                    || sizeof(int) > data.Length - pos)
                    break;
                int nodeDataLength = BitConverter.ToInt32(data, pos);
                pos += 4;

                if (nodeDataLength < 0
                    || nodeDataLength > data.Length - pos)
                    break;
                byte[] nodeData = new byte[nodeDataLength];
                Array.Copy(data, pos, nodeData, 0, nodeDataLength);
                pos += nodeDataLength;

                // 重建节点数据，替换模板名称
                byte[] modifiedNodeData = RebuildNodeData(nodeData, nameMap);
                output.AddRange(BitConverter.GetBytes(modifiedNodeData.Length));
                output.AddRange(modifiedNodeData);
            }

            // 复制剩余数据 (connections)
            if (pos < data.Length)
            {
                byte[] remaining = new byte[data.Length - pos];
                Array.Copy(data, pos, remaining, 0, remaining.Length);
                output.AddRange(remaining);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 解析单个节点数据，提取所有属性键值对
        /// </summary>
        private static Dictionary<string, byte[]> ParseNodeProperties(byte[] nodeData)
        {
            var properties = new Dictionary<string, byte[]>();
            int pos = 0;

            // 跳过 class info
            if (pos >= nodeData.Length) return properties;
            int classInfoLen = nodeData[pos];
            pos += 1 + classInfoLen;

            // 跳过 type GUID
            if (pos >= nodeData.Length) return properties;
            int guidLen = nodeData[pos];
            pos += 1 + guidLen;

            // 读取属性键值对
            while (pos + 8 <= nodeData.Length) // 至少需要 4+4 bytes for key and value lengths
            {
                int keyLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (keyLen < 0
                    || keyLen > nodeData.Length - pos)
                    break;

                string key = Encoding.UTF8.GetString(nodeData, pos, keyLen);
                pos += keyLen;

                if (pos > nodeData.Length
                    || sizeof(int) > nodeData.Length - pos)
                    break;
                int valueLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (valueLen < 0
                    || valueLen > nodeData.Length - pos)
                    break;

                byte[] value = new byte[valueLen];
                Array.Copy(nodeData, pos, value, 0, valueLen);
                pos += valueLen;

                properties[key] = value;
            }

            return properties;
        }

        /// <summary>
        /// 重建节点数据，替换指定属性的值
        /// </summary>
        private static byte[] RebuildNodeData(byte[] nodeData, Dictionary<string, string> nameMap)
        {
            var output = new List<byte>();
            int pos = 0;

            // 复制 class info
            if (pos >= nodeData.Length) return nodeData;
            int classInfoLen = nodeData[pos];
            output.Add(nodeData[pos]);
            pos++;
            for (int j = 0; j < classInfoLen && pos < nodeData.Length; j++)
            {
                output.Add(nodeData[pos]);
                pos++;
            }

            // 复制 type GUID
            if (pos >= nodeData.Length) return output.ToArray();
            int guidLen = nodeData[pos];
            output.Add(nodeData[pos]);
            pos++;
            for (int j = 0; j < guidLen && pos < nodeData.Length; j++)
            {
                output.Add(nodeData[pos]);
                pos++;
            }

            // 处理属性键值对
            while (pos + 8 <= nodeData.Length)
            {
                int keyLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (keyLen < 0
                    || keyLen > nodeData.Length - pos)
                    break;

                string key = Encoding.UTF8.GetString(nodeData, pos, keyLen);
                byte[] keyBytes = new byte[keyLen];
                Array.Copy(nodeData, pos, keyBytes, 0, keyLen);
                pos += keyLen;

                if (pos > nodeData.Length
                    || sizeof(int) > nodeData.Length - pos)
                    break;
                int valueLen = BitConverter.ToInt32(nodeData, pos);
                pos += 4;
                if (valueLen < 0
                    || valueLen > nodeData.Length - pos)
                    break;

                byte[] valueBytes = new byte[valueLen];
                Array.Copy(nodeData, pos, valueBytes, 0, valueLen);
                pos += valueLen;

                // 检查是否需要替换值
                if (TemplatePropertyNames.Contains(key))
                {
                    string oldValue = Encoding.UTF8.GetString(valueBytes);
                    if (nameMap.TryGetValue(oldValue, out string newValue))
                    {
                        valueBytes = Encoding.UTF8.GetBytes(newValue);
                    }
                }

                // 写入属性
                output.AddRange(BitConverter.GetBytes(keyBytes.Length));
                output.AddRange(keyBytes);
                output.AddRange(BitConverter.GetBytes(valueBytes.Length));
                output.AddRange(valueBytes);
            }

            return output.ToArray();
        }

        /// <summary>
        /// 收集流程中所有引用的模板信息，用于导出
        /// </summary>
        public static FlowPackageManifest CollectTemplatesForExport(string flowName, byte[] stnData)
        {
            var manifest = new FlowPackageManifest
            {
                Schema = CurrentPackageSchema,
                FlowName = flowName,
                Version = CurrentPackageVersion,
                FlowContentHash = ComputeSha256(stnData),
            };

            var exportedTemplateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pendingTemplateNames = new Queue<string>(ExtractTemplateNames(stnData));

            while (pendingTemplateNames.Count > 0)
            {
                string name = pendingTemplateNames.Dequeue();
                if (!exportedTemplateNames.Add(name))
                    continue;

                if (!TryResolveTemplate(name, out string templateCode, out ITemplate iTemplate, out int index))
                {
                    throw new InvalidOperationException(
                        $"流程引用的模板 '{name}' "
                        + "在当前模板目录中不存在。");
                }

                FlowPackageTemplate pkgTemplate;
                try
                {
                    object templateValue =
                        CaptureTemplateValue(
                            iTemplate,
                            index)
                        ?? throw new InvalidOperationException(
                            "模板没有返回可导出的参数值。");

                    string? serializedContent =
                        templateValue is ParamModBase
                            ? null
                            : SerializeTemplateContent(
                                templateValue);
                    pkgTemplate = new FlowPackageTemplate
                    {
                        TemplateName = name,
                        TemplateCode = templateCode,
                        TemplateDicId = iTemplate.TemplateDicId,
                        SerializedContent = serializedContent,
                        Details = ExtractTemplateDetails(
                            templateValue,
                            iTemplate.TemplateDicId),
                    };
                    HashSet<string> dependencies =
                        ExtractTemplateNamesFromSerializedContent(
                            serializedContent,
                            name);
                    dependencies.UnionWith(
                        ExtractTemplateNamesFromDetails(
                            pkgTemplate.Details));
                    pkgTemplate.Dependencies = dependencies
                        .OrderBy(
                            dependency => dependency,
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    pkgTemplate.ContentHash =
                        FlowPackageContentIdentity.ComputeContentHash(
                            templateCode,
                            serializedContent,
                            pkgTemplate.Details);
                    pkgTemplate.PackageTemplateId =
                        ComputePackageTemplateId(
                            templateCode,
                            name,
                            pkgTemplate.ContentHash);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"无法完整导出关联模板 '{name}' "
                        + $"({templateCode})。",
                        ex);
                }
                manifest.Templates.Add(pkgTemplate);

                foreach (string referencedTemplateName
                    in pkgTemplate.Dependencies)
                {
                    if (!exportedTemplateNames.Contains(referencedTemplateName))
                    {
                        pendingTemplateNames.Enqueue(referencedTemplateName);
                    }
                }
            }

            ValidateExportSource(manifest);
            return manifest;
        }

        /// <summary>
        /// 导入模板到数据库，返回名称映射表 (旧名称 → 新名称)
        /// </summary>
        public static Dictionary<string, string> ImportTemplates(FlowPackageManifest manifest, string flowName)
        {
            return ImportTemplates(
                manifest,
                flowName,
                TemplateControl.ITemplateNames);
        }

        internal static Dictionary<string, string> ImportTemplates(
            FlowPackageManifest manifest,
            string flowName,
            IReadOnlyDictionary<string, ITemplate> templateCatalog)
        {
            lock (TemplateImportSync)
            {
                return ImportTemplatesCore(
                    manifest,
                    flowName,
                    templateCatalog);
            }
        }

        private static Dictionary<string, string>
            ImportTemplatesCore(
                FlowPackageManifest manifest,
                string flowName,
                IReadOnlyDictionary<string, ITemplate> templateCatalog)
        {
            ArgumentNullException.ThrowIfNull(templateCatalog);
            var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (manifest?.Templates == null) return nameMap;
            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var importPlans = new List<TemplateImportPlan>();
            var packageNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pkgTemplate in manifest.Templates)
            {
                string originalName = pkgTemplate.TemplateName;
                string templateCode = pkgTemplate.TemplateCode;
                if (string.IsNullOrWhiteSpace(originalName)
                    || string.IsNullOrWhiteSpace(templateCode)
                    || !packageNames.Add(originalName))
                {
                    throw new InvalidDataException(
                        "流程包包含无效或重复的关联模板。");
                }

                // 查找对应的 ITemplate 实例
                if (!templateCatalog.TryGetValue(
                        templateCode,
                        out var iTemplate))
                {
                    throw new InvalidOperationException(
                        $"当前环境不支持模板类型 "
                        + $"'{templateCode}'（{originalName}）。");
                }
                if (!CanCreateParamFromModData(iTemplate)
                    && string.IsNullOrWhiteSpace(
                        pkgTemplate.SerializedContent))
                {
                    throw new InvalidDataException(
                        $"关联模板 '{originalName}' "
                        + $"({templateCode}) 缺少可重建的内容。");
                }

                string createName = originalName;
                if (IsReservedTemplateName(
                        originalName,
                        reservedNames,
                        templateCatalog))
                {
                    createName = GenerateUniqueName(
                        originalName,
                        flowName,
                        reservedNames,
                        templateCatalog);
                }

                reservedNames.Add(createName);
                importPlans.Add(
                    new TemplateImportPlan(
                        iTemplate,
                        originalName,
                        createName,
                        pkgTemplate));
            }

            Dictionary<string, string> plannedNameMap =
                ResolveEquivalentTemplateTargets(importPlans);
            foreach (TemplateImportPlan importPlan in importPlans)
            {
                if (importPlan.ReuseExisting)
                {
                    if (!importPlan.OriginalName.Equals(
                            importPlan.TargetName,
                            StringComparison.Ordinal))
                    {
                        nameMap[importPlan.OriginalName] =
                            importPlan.TargetName;
                    }
                    log.Info(
                        $"Reuse equivalent flow package template "
                        + $"'{importPlan.OriginalName}' as "
                        + $"'{importPlan.TargetName}' "
                        + $"({importPlan.PackageTemplate.TemplateCode}).");
                    continue;
                }

                try
                {
                    CreateTemplateFromPackage(
                        importPlan.Template,
                        importPlan.TargetName,
                        importPlan.PackageTemplate,
                        plannedNameMap);
                    if (!importPlan.OriginalName.Equals(
                            importPlan.TargetName,
                            StringComparison.Ordinal))
                    {
                        nameMap[importPlan.OriginalName] =
                            importPlan.TargetName;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"导入关联模板 "
                        + $"'{importPlan.OriginalName}' "
                        + $"({importPlan.PackageTemplate.TemplateCode}) "
                        + "失败。",
                        ex);
                }
                finally
                {
                    importPlan.Template
                        .ClearCreateTemplateSource();
                }
            }

            return nameMap;
        }

        private static Dictionary<string, string>
            ResolveEquivalentTemplateTargets(
                IReadOnlyList<TemplateImportPlan> importPlans)
        {
            if (TryResolveExistingImportSetBySharedSuffix(
                    importPlans,
                    out Dictionary<string, string>? resolvedSetMap))
            {
                return resolvedSetMap;
            }

            Dictionary<string, string> currentMap =
                BuildPlannedNameMap(importPlans);
            int maxPasses = Math.Max(2, importPlans.Count + 2);
            for (int pass = 0; pass < maxPasses; pass++)
            {
                foreach (TemplateImportPlan plan in importPlans)
                {
                    string? existingName =
                        FindEquivalentExistingTemplateName(
                            plan,
                            currentMap);
                    plan.ReuseExisting =
                        !string.IsNullOrWhiteSpace(existingName);
                    plan.TargetName = existingName
                        ?? plan.CreateName;
                }

                Dictionary<string, string> nextMap =
                    BuildPlannedNameMap(importPlans);
                if (NameMapsEqual(currentMap, nextMap))
                    return nextMap;
                currentMap = nextMap;
            }

            // A cyclic set of template references should never make reuse
            // ambiguous. If target selection did not converge, keep all
            // conflict copies rather than risk binding to unequal content.
            foreach (TemplateImportPlan plan in importPlans)
            {
                plan.ReuseExisting = false;
                plan.TargetName = plan.CreateName;
            }
            log.Warn(
                "Flow package template reuse did not converge; "
                + "falling back to conflict copies.");
            return BuildPlannedNameMap(importPlans);
        }

        private static bool
            TryResolveExistingImportSetBySharedSuffix(
                IReadOnlyList<TemplateImportPlan> importPlans,
                out Dictionary<string, string> nameMap)
        {
            nameMap = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            if (importPlans.Count == 0)
                return true;

            TemplateImportPlan firstPlan = importPlans[0];
            List<string> firstNames;
            try
            {
                firstNames =
                    firstPlan.Template.GetTemplateNames();
            }
            catch
            {
                return false;
            }

            IEnumerable<string> suffixes = firstNames
                .Select(name => TryGetSharedImportSuffix(
                    firstPlan.OriginalName,
                    name))
                .Where(suffix => suffix != null)
                .Select(suffix => suffix!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(suffix => suffix.Length)
                .ThenBy(
                    suffix => suffix,
                    StringComparer.OrdinalIgnoreCase);
            foreach (string suffix in suffixes)
            {
                var candidates =
                    new List<(TemplateImportPlan Plan, string Name, int Index)>();
                bool completeSet = true;
                foreach (TemplateImportPlan plan in importPlans)
                {
                    List<string> names;
                    try
                    {
                        names = plan.Template.GetTemplateNames();
                    }
                    catch
                    {
                        completeSet = false;
                        break;
                    }
                    string expectedName =
                        plan.OriginalName + suffix;
                    int index = names.FindIndex(name =>
                        name.Equals(
                            expectedName,
                            StringComparison.OrdinalIgnoreCase));
                    if (index < 0)
                    {
                        completeSet = false;
                        break;
                    }
                    candidates.Add((plan, names[index], index));
                }
                if (!completeSet)
                    continue;

                var candidateMap =
                    new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                foreach (var candidate in candidates)
                {
                    if (!candidate.Plan.OriginalName.Equals(
                            candidate.Name,
                            StringComparison.Ordinal))
                    {
                        candidateMap[
                            candidate.Plan.OriginalName] =
                            candidate.Name;
                    }
                }

                bool equivalent = true;
                foreach (var candidate in candidates)
                {
                    try
                    {
                        object value = CaptureTemplateValue(
                            candidate.Plan.Template,
                            candidate.Index);
                        if (value == null
                            || !FlowPackageContentIdentity
                                .IsEquivalent(
                                    candidate.Plan
                                        .PackageTemplate
                                        .TemplateCode,
                                    value,
                                    candidate.Plan
                                        .PackageTemplate,
                                    candidateMap,
                                    candidate.Plan.Template
                                        .TemplateDicId))
                        {
                            equivalent = false;
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn(
                            "Unable to compare a cvflow template "
                            + "reuse set.",
                            ex);
                        equivalent = false;
                        break;
                    }
                }
                if (!equivalent)
                    continue;

                foreach (var candidate in candidates)
                {
                    candidate.Plan.ReuseExisting = true;
                    candidate.Plan.TargetName =
                        candidate.Name;
                }
                nameMap = candidateMap;
                return true;
            }
            return false;
        }

        private static string? TryGetSharedImportSuffix(
            string originalName,
            string candidateName)
        {
            if (candidateName.Equals(
                    originalName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            string prefix = originalName + "_";
            return candidateName.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase)
                ? candidateName[originalName.Length..]
                : null;
        }

        private static string?
            FindEquivalentExistingTemplateName(
                TemplateImportPlan plan,
                IReadOnlyDictionary<string, string> nameMap)
        {
            List<string> names;
            try
            {
                names = plan.Template.GetTemplateNames();
            }
            catch (Exception ex)
            {
                log.Warn(
                    $"Unable to enumerate templates for "
                    + $"{plan.PackageTemplate.TemplateCode}.",
                    ex);
                return null;
            }

            IEnumerable<(string Name, int Index)> candidates =
                names.Select((name, index) => (
                    Name: name,
                    Index: index))
                    .OrderByDescending(candidate =>
                        candidate.Name.Equals(
                            plan.OriginalName,
                            StringComparison.OrdinalIgnoreCase))
                    .ThenBy(
                        candidate => candidate.Name,
                        StringComparer.OrdinalIgnoreCase);
            foreach ((string name, int index) in candidates)
            {
                try
                {
                    object value =
                        CaptureTemplateValue(
                            plan.Template,
                            index);
                    if (value != null
                        && FlowPackageContentIdentity.IsEquivalent(
                            plan.PackageTemplate.TemplateCode,
                            value,
                            plan.PackageTemplate,
                            nameMap,
                            plan.Template.TemplateDicId))
                    {
                        return name;
                    }
                }
                catch (Exception ex)
                {
                    log.Warn(
                        $"Unable to compare flow package template "
                        + $"'{plan.OriginalName}' with '{name}'.",
                        ex);
                }
            }
            return null;
        }

        private static object CaptureTemplateValue(
            ITemplate template,
            int index)
        {
            return template is IFlowPackageTemplateCodec codec
                ? codec.CaptureFlowPackageValue(index)
                : template.GetParamValue(index);
        }

        private static Dictionary<string, string>
            BuildPlannedNameMap(
                IEnumerable<TemplateImportPlan> plans)
        {
            var map = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (TemplateImportPlan plan in plans)
            {
                if (!plan.OriginalName.Equals(
                        plan.TargetName,
                        StringComparison.Ordinal))
                {
                    map[plan.OriginalName] = plan.TargetName;
                }
            }
            return map;
        }

        private static bool NameMapsEqual(
            IReadOnlyDictionary<string, string> left,
            IReadOnlyDictionary<string, string> right)
        {
            if (left.Count != right.Count)
                return false;
            return left.All(item =>
                right.TryGetValue(item.Key, out string? value)
                && string.Equals(
                    item.Value,
                    value,
                    StringComparison.Ordinal));
        }

        private sealed class TemplateImportPlan
        {
            public TemplateImportPlan(
                ITemplate template,
                string originalName,
                string createName,
                FlowPackageTemplate packageTemplate)
            {
                Template = template;
                OriginalName = originalName;
                CreateName = createName;
                TargetName = createName;
                PackageTemplate = packageTemplate;
            }

            public ITemplate Template { get; }
            public string OriginalName { get; }
            public string CreateName { get; }
            public string TargetName { get; set; }
            public FlowPackageTemplate PackageTemplate { get; }
            public bool ReuseExisting { get; set; }
        }

        /// <summary>
        /// 生成不冲突的模板名称
        /// </summary>
        private static string GenerateUniqueName(
            string baseName,
            string flowName,
            HashSet<string> reservedNames,
            IReadOnlyDictionary<string, ITemplate> templateCatalog)
        {
            string candidate = $"{baseName}_{flowName}";
            if (!IsReservedTemplateName(
                    candidate,
                    reservedNames,
                    templateCatalog))
                return candidate;

            for (int i = 1; i < 9999; i++)
            {
                candidate = $"{baseName}_{flowName}_{i}";
                if (!IsReservedTemplateName(
                        candidate,
                        reservedNames,
                        templateCatalog))
                    return candidate;
            }

            return $"{baseName}_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// 从包数据创建模板
        /// </summary>
        private static void CreateTemplateFromPackage(ITemplate iTemplate, string templateName, FlowPackageTemplate pkgTemplate, Dictionary<string, string> nameMap)
        {
            bool canCreateFromModData =
                CanCreateParamFromModData(iTemplate);
            if (!canCreateFromModData
                && !string.IsNullOrWhiteSpace(
                    pkgTemplate.SerializedContent))
            {
                string? adjustedContent =
                    ReplaceTemplateReferencesInJsonContent(
                        pkgTemplate.SerializedContent,
                        nameMap);
                if (adjustedContent != null
                    && PrepareTemplateImport(
                        iTemplate,
                        templateName,
                        adjustedContent))
                {
                    EnsureTemplateCreated(
                        iTemplate,
                        templateName);
                    return;
                }
            }

            // 某些模板（如 POI）不使用 ModMaster/ModDetail 架构，需走模板自身的 Create 逻辑。
            if (!canCreateFromModData)
            {
                EnsureTemplateCreated(
                    iTemplate,
                    templateName);
                return;
            }

            var modMaster = new ModMasterModel
            {
                Pid = iTemplate.TemplateDicId,
                Name = templateName,
                TenantId = 0
            };
            var details = new List<ModDetailModel>();
            if (pkgTemplate.Details != null && pkgTemplate.Details.Count > 0)
            {
                List<SysDictionaryModDetaiModel> localDefinitions =
                    SysDictionaryModDetailDao.Instance
                        .GetAllByPid(iTemplate.TemplateDicId);
                foreach (var item in pkgTemplate.Details)
                {
                    int localSystemId =
                        ResolveLocalDetailSystemId(
                            item,
                            localDefinitions);
                    SysDictionaryModDetaiModel localDefinition =
                        localDefinitions.Single(definition =>
                            definition.Id == localSystemId);
                    bool isTemplateReference =
                        IsTemplateReferenceProperty(
                            item.Symbol)
                        || IsTemplateReferenceProperty(
                            localDefinition.Symbol);
                    details.Add(new ModDetailModel
                    {
                        SysPid = localSystemId,
                        ValueA = isTemplateReference
                            ? ReplaceTemplateReferencesInString(
                                item.ValueA,
                                nameMap)
                            : item.ValueA,
                        ValueB = isTemplateReference
                            ? ReplaceTemplateReferencesInString(
                                item.ValueB,
                                nameMap)
                            : item.ValueB,
                        IsEnable = item.IsEnable,
                        IsDelete = item.IsDelete
                    });
                }
            }
            else
            {
                // 如果没有 detail 数据，使用系统默认值
                foreach (var item in SysDictionaryModDetailDao.Instance.GetAllByPid(iTemplate.TemplateDicId))
                {
                    details.Add(new ModDetailModel
                    {
                        SysPid = item.Id,
                        ValueA = item.DefaultValue
                    });
                }
            }

            using var Db = new SqlSugar.SqlSugarClient(new SqlSugar.ConnectionConfig
            {
                ConnectionString = Database.MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });
            List<ModDetailModel> modDetailModels;
            Db.Ado.BeginTran();
            try
            {
                int id = Db.Insertable(modMaster)
                    .ExecuteReturnIdentity();
                if (id <= 0)
                {
                    throw new InvalidOperationException(
                        $"数据库没有创建模板 {templateName}。");
                }
                modMaster.Id = id;
                foreach (ModDetailModel detail in details)
                {
                    detail.Pid = id;
                }
                if (details.Count > 0)
                {
                    int inserted = Db.Insertable(details)
                        .ExecuteCommand();
                    if (inserted != details.Count)
                    {
                        throw new InvalidOperationException(
                            $"模板 {templateName} 的参数未完整写入。");
                    }
                }
                modDetailModels = Db.Queryable<ModDetailModel>()
                    .Where(item => item.Pid == id)
                    .ToList();
                Db.Ado.CommitTran();
            }
            catch
            {
                try
                {
                    Db.Ado.RollbackTran();
                }
                catch
                {
                }
                throw;
            }

            // 将新模板加入到内存中的模板集合
            AddTemplateToCollection(iTemplate, modMaster, modDetailModels);
            if (!TemplateExists(
                    iTemplate,
                    templateName))
            {
                iTemplate.Load();
            }
            if (!TemplateExists(
                    iTemplate,
                    templateName))
            {
                throw new InvalidOperationException(
                    $"模板 {templateName} 已写入数据库，"
                    + "但没有加载到模板目录。");
            }
        }

        private static bool PrepareTemplateImport(
            ITemplate template,
            string templateName,
            string serializedContent)
        {
            if (template is IFlowPackageTemplateCodec codec)
            {
                return codec.TryPrepareFlowPackageImport(
                    templateName,
                    serializedContent);
            }
            return template.ImportJsonContent(
                templateName,
                serializedContent);
        }

        internal static int ResolveLocalDetailSystemId(
            FlowPackageDetailItem detail,
            IReadOnlyList<SysDictionaryModDetaiModel>
                localDefinitions)
        {
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(localDefinitions);
            IEnumerable<SysDictionaryModDetaiModel> matches;
            if (!string.IsNullOrWhiteSpace(detail.Symbol))
            {
                matches = localDefinitions.Where(definition =>
                    string.Equals(
                        definition.Symbol,
                        detail.Symbol,
                        StringComparison.Ordinal));
            }
            else if (detail.AddressCode.HasValue)
            {
                matches = localDefinitions.Where(definition =>
                    definition.AddressCode
                        == detail.AddressCode.Value);
            }
            else
            {
                matches = localDefinitions.Where(definition =>
                    definition.Id == detail.SysPid);
            }

            SysDictionaryModDetaiModel[] resolved =
                matches.Take(2).ToArray();
            if (resolved.Length != 1)
            {
                throw new InvalidDataException(
                    $"无法在本地模板字典中唯一解析参数 "
                    + $"'{detail.Symbol ?? detail.AddressCode?.ToString() ?? detail.SysPid.ToString()}'.");
            }
            return resolved[0].Id;
        }

        private static void EnsureTemplateCreated(
            ITemplate template,
            string templateName)
        {
            if (!template.TryCreateTemplate(
                    templateName,
                    out string message))
            {
                throw new InvalidOperationException(message);
            }
        }

        private static bool TemplateExists(
            ITemplate template,
            string templateName)
        {
            try
            {
                return template.GetTemplateNames().Any(name =>
                    name.Equals(
                        templateName,
                        StringComparison.Ordinal));
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveTemplate(string templateName, out string templateCode, out ITemplate template, out int index)
        {
            foreach (var kvp in TemplateControl.ITemplateNames)
            {
                int templateIndex;
                try
                {
                    templateIndex = kvp.Value.GetTemplateIndex(templateName);
                }
                catch (NotImplementedException)
                {
                    continue;
                }
                catch (NotSupportedException)
                {
                    continue;
                }

                if (templateIndex >= 0)
                {
                    templateCode = kvp.Key;
                    template = kvp.Value;
                    index = templateIndex;
                    return true;
                }
            }

            templateCode = string.Empty;
            template = null!;
            index = -1;
            return false;
        }

        private static List<FlowPackageDetailItem>
            ExtractTemplateDetails(
                object templateValue,
                int templateDictionaryId)
        {
            if (templateValue is not ParamModBase paramModBase)
                return new List<FlowPackageDetailItem>();

            var details = new List<ModDetailModel>();
            paramModBase.GetDetail(details);
            Dictionary<int, SysDictionaryModDetaiModel> definitions =
                SysDictionaryModDetailDao.Instance
                    .GetAllByPid(templateDictionaryId)
                    .ToDictionary(definition => definition.Id);
            return details.Select(detail =>
            {
                if (!definitions.TryGetValue(
                        detail.SysPid,
                        out SysDictionaryModDetaiModel? definition)
                    || (string.IsNullOrWhiteSpace(
                            definition.Symbol)
                        && definition.AddressCode == 0))
                {
                    throw new InvalidDataException(
                        $"模板参数 {detail.SysPid} "
                        + "缺少可移植的字典标识。");
                }
                return new FlowPackageDetailItem
                {
                    Symbol = definition.Symbol,
                    AddressCode = definition.AddressCode,
                    ValueA = detail.ValueA,
                    ValueB = detail.ValueB,
                    IsEnable = detail.IsEnable,
                    IsDelete = detail.IsDelete
                };
            }).ToList();
        }

        private static string SerializeTemplateContent(object templateValue)
        {
            return JsonConvert.SerializeObject(templateValue, Formatting.Indented);
        }

        private static HashSet<string> ExtractTemplateNamesFromSerializedContent(string? serializedContent, string currentTemplateName)
        {
            _ = currentTemplateName;
            var referencedTemplateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectTemplateNamesFromString(
                serializedContent,
                referencedTemplateNames,
                allowDirectReference: false);
            return referencedTemplateNames;
        }

        private static void ValidateDeclaredTemplateDependencies(
            IReadOnlyList<FlowPackageTemplate> templates)
        {
            var packageNames = new HashSet<string>(
                templates.Select(template =>
                    template.TemplateName),
                StringComparer.OrdinalIgnoreCase);
            foreach (FlowPackageTemplate template in templates)
            {
                HashSet<string> actual =
                    ExtractTemplateNamesFromSerializedContent(
                        template.SerializedContent,
                        template.TemplateName);
                actual.UnionWith(
                    ExtractTemplateNamesFromDetails(
                        template.Details));
                var declared = new HashSet<string>(
                    template.Dependencies
                        ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);
                if (!actual.SetEquals(declared)
                    || actual.Any(reference =>
                        !packageNames.Contains(reference)))
                {
                    throw new InvalidDataException(
                        $"cvflow 模板依赖声明与内容不一致："
                        + template.TemplateName);
                }
            }
        }

        private static HashSet<string>
            ExtractTemplateNamesFromDetails(
                IEnumerable<FlowPackageDetailItem>? details)
        {
            var referencedTemplateNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
            foreach (FlowPackageDetailItem detail
                in details
                    ?? Enumerable.Empty<FlowPackageDetailItem>())
            {
                if (!IsTemplateReferenceProperty(detail.Symbol))
                    continue;
                CollectTemplateNamesFromString(
                    detail.ValueA,
                    referencedTemplateNames,
                    allowDirectReference: true);
            }
            return referencedTemplateNames;
        }

        private static void CollectTemplateNamesFromString(
            string? rawValue,
            HashSet<string> referencedTemplateNames,
            bool allowDirectReference)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return;

            string trimmedValue = rawValue.Trim();
            if (allowDirectReference)
            {
                referencedTemplateNames.Add(trimmedValue);
            }

            if (!LooksLikeJson(trimmedValue))
                return;

            try
            {
                JToken token = JToken.Parse(trimmedValue);
                CollectTemplateNamesFromToken(
                    token,
                    referencedTemplateNames,
                    allowDirectReference: false);
            }
            catch (JsonException)
            {
            }
        }

        private static void CollectTemplateNamesFromToken(
            JToken token,
            HashSet<string> referencedTemplateNames,
            bool allowDirectReference)
        {
            if (token.Type == JTokenType.String)
            {
                CollectTemplateNamesFromString(
                    token.Value<string>(),
                    referencedTemplateNames,
                    allowDirectReference);
                return;
            }

            if (token is JObject objectToken)
            {
                foreach (JProperty property
                    in objectToken.Properties())
                {
                    CollectTemplateNamesFromToken(
                        property.Value,
                        referencedTemplateNames,
                        IsTemplateReferenceProperty(
                            property.Name));
                }
                return;
            }

            foreach (JToken child in token.Children())
            {
                CollectTemplateNamesFromToken(
                    child,
                    referencedTemplateNames,
                    allowDirectReference);
            }
        }

        internal static bool IsTemplateReferenceProperty(
            string? propertyName)
        {
            return !string.IsNullOrWhiteSpace(propertyName)
                && TemplatePropertyNames.Contains(propertyName);
        }

        private static bool IsReservedTemplateName(
            string templateName,
            HashSet<string> reservedNames,
            IReadOnlyDictionary<string, ITemplate> templateCatalog)
        {
            return reservedNames.Contains(templateName)
                || templateCatalog.Values.Any(template =>
                {
                    try
                    {
                        return template.GetTemplateNames().Any(name =>
                            name.Equals(
                                templateName,
                                StringComparison.OrdinalIgnoreCase));
                    }
                    catch
                    {
                        return false;
                    }
                });
        }

        internal static string? ReplaceTemplateReferencesInJsonContent(
            string? jsonContent,
            Dictionary<string, string> nameMap)
        {
            if (string.IsNullOrWhiteSpace(jsonContent) || nameMap.Count == 0)
                return jsonContent;

            try
            {
                JToken token = JToken.Parse(jsonContent);
                if (!ReplaceTemplateReferencesInToken(
                        token,
                        nameMap,
                        allowDirectReference: false))
                    return jsonContent;

                return token.ToString(Formatting.Indented);
            }
            catch (JsonException)
            {
                return jsonContent;
            }
        }

        private static bool ReplaceTemplateReferencesInToken(
            JToken token,
            Dictionary<string, string> nameMap,
            bool allowDirectReference)
        {
            if (token.Type == JTokenType.String)
            {
                string? currentValue = token.Value<string>();
                string? replacedValue =
                    ReplaceTemplateReferencesInStringCore(
                        currentValue,
                        nameMap,
                        allowDirectReference);
                if (!string.Equals(currentValue, replacedValue, StringComparison.Ordinal))
                {
                    ((JValue)token).Value = replacedValue;
                    return true;
                }

                return false;
            }

            bool changed = false;
            if (token is JObject objectToken)
            {
                foreach (JProperty property in objectToken.Properties().ToList())
                {
                    if (ReplaceTemplateReferencesInToken(
                            property.Value,
                            nameMap,
                            IsTemplateReferenceProperty(
                                property.Name)))
                        changed = true;
                }

                return changed;
            }

            foreach (JToken child in token.Children())
            {
                if (ReplaceTemplateReferencesInToken(
                        child,
                        nameMap,
                        allowDirectReference))
                    changed = true;
            }

            return changed;
        }

        internal static string? ReplaceTemplateReferencesInString(
            string? rawValue,
            Dictionary<string, string> nameMap)
        {
            return ReplaceTemplateReferencesInStringCore(
                rawValue,
                nameMap,
                allowDirectReference: true);
        }

        private static string?
            ReplaceTemplateReferencesInStringCore(
                string? rawValue,
                Dictionary<string, string> nameMap,
                bool allowDirectReference)
        {
            if (string.IsNullOrWhiteSpace(rawValue) || nameMap.Count == 0)
                return rawValue;

            string trimmedValue = rawValue.Trim();
            if (allowDirectReference)
            {
                if (nameMap.TryGetValue(
                        rawValue,
                        out string? directReplacement))
                    return directReplacement;
                if (!trimmedValue.Equals(
                        rawValue,
                        StringComparison.Ordinal)
                    && nameMap.TryGetValue(
                        trimmedValue,
                        out directReplacement))
                {
                    return directReplacement;
                }
            }

            if (!LooksLikeJson(trimmedValue))
                return rawValue;

            try
            {
                JToken nestedToken = JToken.Parse(trimmedValue);
                if (!ReplaceTemplateReferencesInToken(
                        nestedToken,
                        nameMap,
                        allowDirectReference: false))
                    return rawValue;

                return nestedToken.ToString(Formatting.None);
            }
            catch (JsonException)
            {
                return rawValue;
            }
        }

        private static bool LooksLikeJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            char firstChar = value[0];
            char lastChar = value[^1];
            return (firstChar == '{' && lastChar == '}') || (firstChar == '[' && lastChar == ']');
        }

        private static bool CanCreateParamFromModData(ITemplate iTemplate)
        {
            var templateType = iTemplate.GetType();
            var baseType = templateType;
            while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(ITemplate<>)))
            {
                baseType = baseType.BaseType;
            }

            if (baseType == null) return false;

            var paramType = baseType.GetGenericArguments()[0];
            return paramType.GetConstructor(new[] { typeof(ModMasterModel), typeof(List<ModDetailModel>) }) != null;
        }

        /// <summary>
        /// 通过反射将新创建的模板添加到 ITemplate 的 TemplateParams 集合中
        /// </summary>
        private static void AddTemplateToCollection(ITemplate iTemplate, ModMasterModel modMaster, List<ModDetailModel> details)
        {
            // 获取 ITemplate<T> 的泛型参数类型 T
            var templateType = iTemplate.GetType();
            var baseType = templateType;
            while (baseType != null && (!baseType.IsGenericType || baseType.GetGenericTypeDefinition() != typeof(ITemplate<>)))
            {
                baseType = baseType.BaseType;
            }

            if (baseType == null) return;

            var paramType = baseType.GetGenericArguments()[0]; // typeof(T)

            // 创建 T 实例: new T(modMaster, details)
            var ctor = paramType.GetConstructor(new[] { typeof(ModMasterModel), typeof(List<ModDetailModel>) });
            if (ctor == null)
            {
                iTemplate.Load();
                return;
            }

            var param = ctor.Invoke(new object[] { modMaster, details });
            if (param == null) return;

            // 创建 TemplateModel<T> 实例
            var templateModelType = typeof(TemplateModel<>).MakeGenericType(paramType);
            var templateModel = Activator.CreateInstance(templateModelType, new object[] { modMaster.Name ?? "default", param });

            // 获取 TemplateParams 属性并添加
            var templateParamsProperty = templateType.GetProperty("TemplateParams");
            if (templateParamsProperty != null)
            {
                var collection = templateParamsProperty.GetValue(iTemplate);
                var addMethod = collection?.GetType().GetMethod("Add");
                addMethod?.Invoke(collection, new[] { templateModel });
            }
        }
    }
}
