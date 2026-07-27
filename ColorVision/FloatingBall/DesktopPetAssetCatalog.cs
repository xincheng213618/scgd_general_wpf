#pragma warning disable CA1001
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.FloatingBall
{
    public enum DesktopPetAssetSource
    {
        ColorVisionBuiltIn,
        ColorVisionCustom,
        CodexBuiltIn,
        CodexCustom,
    }

    public sealed class DesktopPetAsset
    {
        internal DesktopPetAsset(
            string id,
            string displayName,
            string description,
            DesktopPetAssetSource source,
            int spriteVersionNumber,
            string? staticImageUri = null,
            string? filePath = null,
            string? archivePath = null,
            long archiveOffset = 0,
            int archiveLength = 0)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Source = source;
            SpriteVersionNumber = spriteVersionNumber;
            StaticImageUri = staticImageUri;
            FilePath = filePath;
            ArchivePath = archivePath;
            ArchiveOffset = archiveOffset;
            ArchiveLength = archiveLength;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public DesktopPetAssetSource Source { get; }

        public int SpriteVersionNumber { get; }

        public string? StaticImageUri { get; }

        public bool IsSpriteSheet => StaticImageUri == null;

        public string SourceLabel => Source switch
        {
            DesktopPetAssetSource.ColorVisionBuiltIn => "ColorVision 内置",
            DesktopPetAssetSource.ColorVisionCustom => "ColorVision 自定义",
            DesktopPetAssetSource.CodexBuiltIn => "Codex 本机素材",
            DesktopPetAssetSource.CodexCustom => "Codex 自定义",
            _ => string.Empty,
        };

        internal string? FilePath { get; }

        internal string? ArchivePath { get; }

        internal long ArchiveOffset { get; }

        internal int ArchiveLength { get; }

        public byte[] ReadSpriteSheetBytes()
        {
            if (!IsSpriteSheet)
                throw new InvalidOperationException("The selected desktop pet does not use a sprite sheet.");

            if (!string.IsNullOrWhiteSpace(FilePath))
                return File.ReadAllBytes(FilePath);

            if (string.IsNullOrWhiteSpace(ArchivePath) || ArchiveOffset < 0 || ArchiveLength <= 0)
                throw new InvalidDataException("The desktop pet archive entry is incomplete.");

            using var stream = new FileStream(ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Position = ArchiveOffset;
            var bytes = new byte[ArchiveLength];
            stream.ReadExactly(bytes);
            return bytes;
        }
    }

    public sealed class DesktopPetAssetCatalog
    {
        public const string DefaultAssetId = "builtin:xiaocai";
        public const int MaximumSpriteSheetBytes = 20 * 1024 * 1024;

        private const string DefaultImageUri = "pack://application:,,,/ColorVision;component/Assets/Pets/xiaocai.png";
        private static readonly Lazy<DesktopPetAssetCatalog> LazyInstance = new(() => new DesktopPetAssetCatalog());
        private static readonly Regex CodexSpriteNameRegex = new(
            @"^webview/assets/(?<id>bsod|codex|dewey|fireball|hoots|null-signal|rocky|seedy|stacky)-spritesheet-v(?<version>\d+)-[^/]+\.webp$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Dictionary<string, CodexPetMetadata> CodexPetMetadataById =
            new Dictionary<string, CodexPetMetadata>(StringComparer.OrdinalIgnoreCase)
            {
                ["codex"] = new(0, "Codex", "The original Codex companion."),
                ["dewey"] = new(1, "Dewey", "A calm companion for focused workspace days."),
                ["fireball"] = new(2, "Fireball", "Hot path energy for fast iteration."),
                ["hoots"] = new(3, "Hoots", "A sharp-eyed owl for polished work in a blink."),
                ["rocky"] = new(4, "Rocky", "A steady rock when the diff gets large."),
                ["seedy"] = new(5, "Seedy", "Small green shoots for new ideas."),
                ["stacky"] = new(6, "Stacky", "A balanced stack for deep work."),
                ["bsod"] = new(7, "BSOD", "A tiny blue-screen gremlin."),
                ["null-signal"] = new(8, "Null Signal", "Quiet signal from the void."),
            };

        private readonly SemaphoreSlim _refreshGate = new(1, 1);
        private IReadOnlyList<DesktopPetAsset> _assets = [CreateDefaultAsset()];
        private bool _isLoaded;

        private DesktopPetAssetCatalog()
        {
        }

        public static DesktopPetAssetCatalog Shared => LazyInstance.Value;

        public IReadOnlyList<DesktopPetAsset> Assets => Volatile.Read(ref _assets);

        public string? CodexArchivePath { get; private set; }

        public static string ColorVisionPetDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "DesktopPets");

        public async Task<IReadOnlyList<DesktopPetAsset>> EnsureLoadedAsync()
        {
            if (_isLoaded)
                return Assets;

            return await RefreshAsync().ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<DesktopPetAsset>> RefreshAsync()
        {
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var result = await Task.Run(DiscoverAssets).ConfigureAwait(false);
                CodexArchivePath = result.CodexArchivePath;
                Volatile.Write(ref _assets, result.Assets);
                _isLoaded = true;
                return result.Assets;
            }
            finally
            {
                _refreshGate.Release();
            }
        }

        public DesktopPetAsset GetSelectedOrDefault(string? selectedAssetId)
        {
            var assets = Assets;
            if (!string.IsNullOrWhiteSpace(selectedAssetId))
            {
                var selected = assets.FirstOrDefault(asset => string.Equals(asset.Id, selectedAssetId, StringComparison.OrdinalIgnoreCase));
                if (selected != null)
                    return selected;
            }

            return assets.FirstOrDefault(asset => string.Equals(asset.Id, DefaultAssetId, StringComparison.OrdinalIgnoreCase))
                ?? CreateDefaultAsset();
        }

        private static CatalogDiscoveryResult DiscoverAssets()
        {
            var assets = new List<DesktopPetAsset> { CreateDefaultAsset() };
            var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DefaultAssetId };

            DiscoverCustomPets(ColorVisionPetDirectory, DesktopPetAssetSource.ColorVisionCustom, "colorvision-custom", "pet.json", assets, knownIds);

            var codexHome = GetCodexHomeDirectory();
            if (!string.IsNullOrWhiteSpace(codexHome))
            {
                DiscoverCustomPets(Path.Combine(codexHome, "pets"), DesktopPetAssetSource.CodexCustom, "codex-custom:pets", "pet.json", assets, knownIds);
                DiscoverCustomPets(Path.Combine(codexHome, "avatars"), DesktopPetAssetSource.CodexCustom, "codex-custom:avatars", "avatar.json", assets, knownIds);
            }

            string? discoveredArchivePath = null;
            foreach (var archivePath in EnumerateCodexArchiveCandidates())
            {
                try
                {
                    var archiveIndex = AsarArchiveIndex.Read(archivePath);
                    var discovered = DiscoverCodexBuiltInPets(archivePath, archiveIndex);
                    if (discovered.Length == 0)
                        continue;

                    discoveredArchivePath = archivePath;
                    foreach (var asset in discovered.OrderBy(asset => CodexPetMetadataById[asset.Id["codex-builtin:".Length..]].Order))
                    {
                        if (knownIds.Add(asset.Id))
                            assets.Add(asset);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Unable to inspect Codex desktop pet assets at '{archivePath}': {ex.Message}");
                }
            }

            return new CatalogDiscoveryResult(assets, discoveredArchivePath);
        }

        private static DesktopPetAsset[] DiscoverCodexBuiltInPets(string archivePath, AsarArchiveIndex archiveIndex)
        {
            var newestEntryByPet = new Dictionary<string, (int Version, AsarEntry Entry)>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archiveIndex.Entries)
            {
                var match = CodexSpriteNameRegex.Match(entry.Path);
                if (!match.Success || entry.Length <= 0 || entry.Length > MaximumSpriteSheetBytes)
                    continue;

                var petId = match.Groups["id"].Value;
                if (!CodexPetMetadataById.ContainsKey(petId))
                    continue;

                var version = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture);
                if (!newestEntryByPet.TryGetValue(petId, out var current) || version > current.Version)
                    newestEntryByPet[petId] = (version, entry);
            }

            return newestEntryByPet.Select(pair =>
            {
                var metadata = CodexPetMetadataById[pair.Key];
                var entry = pair.Value.Entry;
                return new DesktopPetAsset(
                    $"codex-builtin:{pair.Key}",
                    metadata.DisplayName,
                    metadata.Description,
                    DesktopPetAssetSource.CodexBuiltIn,
                    spriteVersionNumber: 2,
                    archivePath: archivePath,
                    archiveOffset: archiveIndex.DataOffset + entry.Offset,
                    archiveLength: entry.Length);
            }).ToArray();
        }

        private static void DiscoverCustomPets(
            string directory,
            DesktopPetAssetSource source,
            string idPrefix,
            string manifestFileName,
            List<DesktopPetAsset> assets,
            HashSet<string> knownIds)
        {
            if (!Directory.Exists(directory))
                return;

            IEnumerable<string> petDirectories;
            try
            {
                petDirectories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch
            {
                return;
            }

            foreach (var petDirectory in petDirectories)
            {
                try
                {
                    var manifestPath = Path.Combine(petDirectory, manifestFileName);
                    if (!File.Exists(manifestPath))
                        continue;

                    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var root = manifest.RootElement;
                    var folderName = Path.GetFileName(petDirectory);
                    var id = $"{idPrefix}:{folderName}";
                    if (!knownIds.Add(id))
                        continue;

                    var displayName = GetOptionalString(root, "displayName")
                        ?? GetOptionalString(root, "id")
                        ?? folderName;
                    var description = GetOptionalString(root, "description") ?? "Compatible Codex desktop pet pack.";
                    var spriteVersion = GetOptionalInt32(root, "spriteVersionNumber") is 2 ? 2 : 1;
                    var spritesheetPath = GetOptionalString(root, "spritesheetPath") ?? "spritesheet.webp";
                    var resolvedSpritePath = ResolveContainedPath(petDirectory, spritesheetPath);
                    if (resolvedSpritePath == null || !File.Exists(resolvedSpritePath))
                        continue;

                    var fileInfo = new FileInfo(resolvedSpritePath);
                    if (fileInfo.Length <= 0 || fileInfo.Length > MaximumSpriteSheetBytes)
                        continue;

                    assets.Add(new DesktopPetAsset(
                        id,
                        displayName,
                        description,
                        source,
                        spriteVersion,
                        filePath: resolvedSpritePath));
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Unable to load desktop pet manifest from '{petDirectory}': {ex.Message}");
                }
            }
        }

        private static IEnumerable<string> EnumerateCodexArchiveCandidates()
        {
            var candidates = new List<string>();

            try
            {
                foreach (var process in Process.GetProcessesByName("ChatGPT"))
                {
                    try
                    {
                        var executablePath = process.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(executablePath))
                            continue;

                        candidates.Add(Path.Combine(Path.GetDirectoryName(executablePath)!, "resources", "app.asar"));
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }

            AddInstalledAppCandidates(candidates);

            return candidates
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void AddInstalledAppCandidates(List<string> candidates)
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(localAppData, "Programs", "Codex", "resources", "app.asar"));
            candidates.Add(Path.Combine(localAppData, "Programs", "ChatGPT", "resources", "app.asar"));

            try
            {
                var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
                if (!Directory.Exists(windowsApps))
                    return;

                foreach (var packageDirectory in Directory.EnumerateDirectories(windowsApps, "OpenAI.Codex_*"))
                    candidates.Add(Path.Combine(packageDirectory, "app", "resources", "app.asar"));
            }
            catch
            {
            }
        }

        private static string? GetCodexHomeDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, ".codex");
        }

        private static string? ResolveContainedPath(string rootDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                return null;

            var root = Path.GetFullPath(rootDirectory);
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
            return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                ? candidate
                : null;
        }

        private static string? GetOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim()
                : null;
        }

        private static int? GetOptionalInt32(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
                ? result
                : null;
        }

        private static DesktopPetAsset CreateDefaultAsset()
        {
            return new DesktopPetAsset(
                DefaultAssetId,
                "小彩",
                "ColorVision 默认桌面伙伴，会跟随 Copilot 状态陪你完成检测工作。",
                DesktopPetAssetSource.ColorVisionBuiltIn,
                spriteVersionNumber: 0,
                staticImageUri: DefaultImageUri);
        }

        private sealed record CodexPetMetadata(int Order, string DisplayName, string Description);

        private sealed record CatalogDiscoveryResult(IReadOnlyList<DesktopPetAsset> Assets, string? CodexArchivePath);
    }

    internal sealed class AsarArchiveIndex
    {
        private const uint MaximumHeaderBytes = 64 * 1024 * 1024;

        private AsarArchiveIndex(long dataOffset, IReadOnlyList<AsarEntry> entries)
        {
            DataOffset = dataOffset;
            Entries = entries;
        }

        public long DataOffset { get; }

        public IReadOnlyList<AsarEntry> Entries { get; }

        public static AsarArchiveIndex Read(string archivePath)
        {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            var sizePicklePayload = reader.ReadUInt32();
            var headerPickleSize = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var headerJsonLength = reader.ReadUInt32();
            if (sizePicklePayload != 4 || headerJsonLength == 0 || headerJsonLength > MaximumHeaderBytes)
                throw new InvalidDataException("The ASAR header is invalid or too large.");

            var headerJson = reader.ReadBytes(checked((int)headerJsonLength));
            if (headerJson.Length != headerJsonLength)
                throw new EndOfStreamException("The ASAR header ended unexpectedly.");

            using var document = JsonDocument.Parse(headerJson);
            if (!document.RootElement.TryGetProperty("files", out var files))
                throw new InvalidDataException("The ASAR header does not contain a file index.");

            var entries = new List<AsarEntry>();
            EnumerateEntries(files, string.Empty, entries);
            return new AsarArchiveIndex(8L + headerPickleSize, entries);
        }

        private static void EnumerateEntries(JsonElement files, string parentPath, ICollection<AsarEntry> entries)
        {
            foreach (var property in files.EnumerateObject())
            {
                var path = string.IsNullOrEmpty(parentPath) ? property.Name : $"{parentPath}/{property.Name}";
                var value = property.Value;
                if (value.TryGetProperty("files", out var children))
                {
                    EnumerateEntries(children, path, entries);
                    continue;
                }

                if (value.TryGetProperty("unpacked", out var unpacked) && unpacked.ValueKind == JsonValueKind.True)
                    continue;
                if (!value.TryGetProperty("size", out var sizeElement) || !sizeElement.TryGetInt32(out var size))
                    continue;
                if (!value.TryGetProperty("offset", out var offsetElement))
                    continue;

                var offsetText = offsetElement.ValueKind == JsonValueKind.String
                    ? offsetElement.GetString()
                    : offsetElement.GetRawText();
                if (!long.TryParse(offsetText, NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
                    continue;

                entries.Add(new AsarEntry(path, offset, size));
            }
        }
    }

    internal readonly record struct AsarEntry(string Path, long Offset, int Length);
}
