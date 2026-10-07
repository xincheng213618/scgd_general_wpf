using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.FloatingBall
{
    internal sealed record DesktopPetImportRequest(
        string DisplayName,
        string Description,
        string SpriteSheetPath,
        int SpriteVersionNumber);

    internal sealed record DesktopPetImportResult(
        string AssetId,
        string PackageDirectory);

    internal static class DesktopPetPackageService
    {
        private static readonly string[] ReservedFileNames =
        [
            "con",
            "prn",
            "aux",
            "nul",
            "com1",
            "com2",
            "com3",
            "com4",
            "com5",
            "com6",
            "com7",
            "com8",
            "com9",
            "lpt1",
            "lpt2",
            "lpt3",
            "lpt4",
            "lpt5",
            "lpt6",
            "lpt7",
            "lpt8",
            "lpt9",
        ];
        private static readonly SemaphoreSlim ImportGate = new(1, 1);
        private static readonly JsonSerializerOptions ManifestJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        public static async Task<DesktopPetImportResult> ImportAsync(
            DesktopPetImportRequest request,
            string? destinationRoot = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var displayName = request.DisplayName?.Trim() ?? string.Empty;
            if (displayName.Length is < 1 or > 80)
                throw new ArgumentException(DesktopPetText.NameRequired, nameof(request));

            var description = request.Description?.Trim() ?? string.Empty;
            if (description.Length > 240)
                throw new ArgumentException(DesktopPetText.DescriptionTooLong, nameof(request));
            if (request.SpriteVersionNumber is not 1 and not 2)
                throw new ArgumentOutOfRangeException(nameof(request), DesktopPetText.InvalidSpriteVersion);

            if (string.IsNullOrWhiteSpace(request.SpriteSheetPath))
                throw new ArgumentException(DesktopPetText.ChooseSpriteFile, nameof(request));

            var sourcePath = Path.GetFullPath(request.SpriteSheetPath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(DesktopPetText.SpriteFileNotFound, sourcePath);

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension is not ".webp" and not ".png")
                throw new InvalidDataException(DesktopPetText.UnsupportedSpriteFormat);

            var sourceInfo = new FileInfo(sourcePath);
            if (sourceInfo.Length <= 0 || sourceInfo.Length > DesktopPetAssetCatalog.MaximumSpriteSheetBytes)
                throw new InvalidDataException(DesktopPetText.SpriteTooLarge);

            var encodedImage = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            using (DesktopPetSpriteSheet.Load(encodedImage, request.SpriteVersionNumber))
            {
            }

            var rootDirectory = Path.GetFullPath(destinationRoot ?? DesktopPetAssetCatalog.ColorVisionPetDirectory);
            await ImportGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(rootDirectory);
                var packageName = GetAvailablePackageName(rootDirectory, CreatePackageSlug(displayName));
                var packageDirectory = Path.Combine(rootDirectory, packageName);
                var stagingDirectory = Path.Combine(rootDirectory, $".import-{Guid.NewGuid():N}");
                Directory.CreateDirectory(stagingDirectory);

                try
                {
                    var spriteFileName = $"spritesheet{extension}";
                    await File.WriteAllBytesAsync(
                        Path.Combine(stagingDirectory, spriteFileName),
                        encodedImage,
                        cancellationToken).ConfigureAwait(false);

                    var manifest = new DesktopPetManifest(
                        packageName,
                        displayName,
                        description,
                        request.SpriteVersionNumber,
                        spriteFileName);
                    var manifestJson = JsonSerializer.Serialize(manifest, ManifestJsonOptions);
                    await File.WriteAllTextAsync(
                        Path.Combine(stagingDirectory, "pet.json"),
                        manifestJson,
                        cancellationToken).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.Move(stagingDirectory, packageDirectory);
                    return new DesktopPetImportResult(
                        $"colorvision-custom:{packageName}",
                        packageDirectory);
                }
                catch
                {
                    DeleteStagingDirectory(rootDirectory, stagingDirectory);
                    throw;
                }
            }
            finally
            {
                ImportGate.Release();
            }
        }

        private static string CreatePackageSlug(string displayName)
        {
            Span<char> slugBuffer = stackalloc char[Math.Min(displayName.Length, 48)];
            var length = 0;
            var pendingSeparator = false;
            foreach (var character in displayName)
            {
                if (length >= slugBuffer.Length)
                    break;

                if (character <= 127 && char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && length > 0 && length < slugBuffer.Length)
                        slugBuffer[length++] = '-';
                    if (length >= slugBuffer.Length)
                        break;

                    slugBuffer[length++] = char.ToLower(character, CultureInfo.InvariantCulture);
                    pendingSeparator = false;
                }
                else if (length > 0)
                {
                    pendingSeparator = true;
                }
            }

            var slug = length == 0
                ? ($"pet-{Guid.NewGuid():N}")[..12]
                : new string(slugBuffer[..length]).TrimEnd('-');
            return Array.IndexOf(ReservedFileNames, slug) >= 0 ? $"pet-{slug}" : slug;
        }

        private static string GetAvailablePackageName(string rootDirectory, string baseName)
        {
            if (!Path.Exists(Path.Combine(rootDirectory, baseName)))
                return baseName;

            for (var suffix = 2; suffix < 10_000; suffix++)
            {
                var candidate = $"{baseName}-{suffix.ToString(CultureInfo.InvariantCulture)}";
                if (!Path.Exists(Path.Combine(rootDirectory, candidate)))
                    return candidate;
            }

            return $"{baseName}-{Guid.NewGuid():N}";
        }

        internal static void DeleteStagingDirectory(string rootDirectory, string stagingDirectory)
        {
            try
            {
                var normalizedRoot = Path.GetFullPath(rootDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var normalizedStaging = Path.GetFullPath(stagingDirectory);
                if (normalizedStaging.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(normalizedStaging).StartsWith(".import-", StringComparison.Ordinal)
                    && Directory.Exists(normalizedStaging))
                {
                    Directory.Delete(normalizedStaging, recursive: true);
                }
            }
            catch
            {
            }
        }

        private sealed record DesktopPetManifest(
            string Id,
            string DisplayName,
            string Description,
            int SpriteVersionNumber,
            string SpritesheetPath);
    }
}
