using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.FloatingBall
{
    internal sealed record DesktopPetCodexAvailability(
        bool IsAvailable,
        string Status,
        string? SkillSourceDirectory,
        string SkillDestinationDirectory);

    internal sealed record DesktopPetCodexLaunchResult(
        string DeepLink,
        string SkillDirectory);

    internal static class DesktopPetCodexService
    {
        internal const string SkillName = "hatch-pet";
        internal const int MaximumConceptLength = 600;

        public static string? CodexHomeDirectory
        {
            get
            {
                var configured = Environment.GetEnvironmentVariable("CODEX_HOME");
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    try
                    {
                        return Path.GetFullPath(configured);
                    }
                    catch
                    {
                    }
                }

                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, ".codex");
            }
        }

        public static string? CodexPetDirectory =>
            CodexHomeDirectory is { } codexHome ? Path.Combine(codexHome, "pets") : null;

        public static DesktopPetCodexAvailability InspectAvailability()
        {
            var destination = GetSkillDestinationDirectory();
            var installedManifest = Path.Combine(destination, "SKILL.md");
            if (File.Exists(installedManifest))
            {
                return new DesktopPetCodexAvailability(
                    true,
                    DesktopPetText.CodexReady,
                    null,
                    destination);
            }

            if (Directory.Exists(destination))
            {
                return new DesktopPetCodexAvailability(
                    false,
                    DesktopPetText.CodexSkillIncomplete,
                    null,
                    destination);
            }

            var source = FindBundledSkillDirectory();
            return source == null
                ? new DesktopPetCodexAvailability(
                    false,
                    DesktopPetText.CodexNotFound,
                    null,
                    destination)
                : new DesktopPetCodexAvailability(
                    true,
                    DesktopPetText.CodexDetected,
                    source,
                    destination);
        }

        public static async Task<DesktopPetCodexLaunchResult> LaunchAsync(
            string? concept,
            CancellationToken cancellationToken = default)
        {
            var availability = InspectAvailability();
            if (!availability.IsAvailable)
                throw new InvalidOperationException(availability.Status);

            var skillDirectory = await EnsureHatchPetInstalledAsync(
                availability.SkillSourceDirectory,
                availability.SkillDestinationDirectory,
                cancellationToken).ConfigureAwait(false);
            var deepLink = BuildNewThreadDeepLink(BuildPrompt(concept));

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = deepLink,
                UseShellExecute = true,
            });
            process?.Dispose();
            return new DesktopPetCodexLaunchResult(deepLink, skillDirectory);
        }

        internal static string BuildPrompt(string? concept)
        {
            var normalizedConcept = NormalizeConcept(concept);
            return normalizedConcept.Length == 0
                ? "$hatch-pet create a pet based on what you know about me. Package the completed Codex-compatible v2 pet in the default Codex pets directory so ColorVision can discover it automatically."
                : $"$hatch-pet create a pet based on this idea: {normalizedConcept}. Package the completed Codex-compatible v2 pet in the default Codex pets directory so ColorVision can discover it automatically.";
        }

        internal static string BuildNewThreadDeepLink(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Codex prompt cannot be empty.", nameof(prompt));

            return $"codex://new?prompt={Uri.EscapeDataString(prompt)}";
        }

        internal static IReadOnlySet<string> SnapshotPetPackageIds(string? petDirectory = null)
        {
            var root = petDirectory ?? CodexPetDirectory;
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return result;

            try
            {
                foreach (var packageDirectory in Directory.EnumerateDirectories(root))
                {
                    if (File.Exists(Path.Combine(packageDirectory, "pet.json")))
                        result.Add($"codex-custom:pets:{Path.GetFileName(packageDirectory)}");
                }
            }
            catch
            {
            }

            return result;
        }

        internal static async Task<string> EnsureHatchPetInstalledAsync(
            string? sourceDirectory,
            string destinationDirectory,
            CancellationToken cancellationToken = default)
        {
            var destination = Path.GetFullPath(destinationDirectory);
            if (File.Exists(Path.Combine(destination, "SKILL.md")))
                return destination;
            if (Directory.Exists(destination))
                throw new InvalidDataException(DesktopPetText.SkillMissingManifest);
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new DirectoryNotFoundException(DesktopPetText.BundledSkillNotFound);

            var source = Path.GetFullPath(sourceDirectory);
            if (!File.Exists(Path.Combine(source, "SKILL.md")))
                throw new InvalidDataException(DesktopPetText.BundledSkillIncomplete);

            var destinationRoot = Path.GetDirectoryName(destination)
                ?? throw new InvalidDataException(DesktopPetText.InvalidSkillDirectory);
            Directory.CreateDirectory(destinationRoot);
            var stagingDirectory = Path.Combine(destinationRoot, $".{SkillName}-install-{Guid.NewGuid():N}");
            try
            {
                await Task.Run(
                    () => CopyDirectory(source, stagingDirectory, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(Path.Combine(stagingDirectory, "SKILL.md")))
                    throw new InvalidDataException(DesktopPetText.SkillCopyIncomplete);

                if (Directory.Exists(destination))
                {
                    if (File.Exists(Path.Combine(destination, "SKILL.md")))
                        return destination;

                    throw new InvalidDataException(DesktopPetText.SkillMissingManifest);
                }

                Directory.Move(stagingDirectory, destination);
                return destination;
            }
            finally
            {
                DeleteInstallStagingDirectory(destinationRoot, stagingDirectory);
            }
        }

        private static string NormalizeConcept(string? concept)
        {
            var normalized = string.Join(
                " ",
                (concept ?? string.Empty)
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length > MaximumConceptLength)
                throw new ArgumentException(DesktopPetText.Format("ConceptTooLong", MaximumConceptLength), nameof(concept));

            return normalized;
        }

        private static string GetSkillDestinationDirectory()
        {
            var codexHome = CodexHomeDirectory
                ?? throw new DirectoryNotFoundException(DesktopPetText.CodexHomeNotFound);
            return Path.Combine(codexHome, "skills", SkillName);
        }

        private static string? FindBundledSkillDirectory()
        {
            foreach (var resourcesDirectory in EnumerateCodexResourceDirectories())
            {
                foreach (var relativePath in new[]
                {
                    Path.Combine("skills", "skills", ".curated", SkillName),
                    Path.Combine("skills", ".curated", SkillName),
                })
                {
                    try
                    {
                        var candidate = Path.Combine(resourcesDirectory, relativePath);
                        if (File.Exists(Path.Combine(candidate, "SKILL.md")))
                            return candidate;
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateCodexResourceDirectories()
        {
            var candidates = new List<string>();
            var archivePath = DesktopPetAssetCatalog.Shared.CodexArchivePath;
            if (!string.IsNullOrWhiteSpace(archivePath))
            {
                var resourcesDirectory = Path.GetDirectoryName(archivePath);
                if (!string.IsNullOrWhiteSpace(resourcesDirectory))
                    candidates.Add(resourcesDirectory);
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(localAppData, "Programs", "Codex", "resources"));
            candidates.Add(Path.Combine(localAppData, "Programs", "ChatGPT", "resources"));

            try
            {
                foreach (var process in Process.GetProcessesByName("ChatGPT"))
                {
                    try
                    {
                        var executablePath = process.MainModule?.FileName;
                        if (!string.IsNullOrWhiteSpace(executablePath))
                            candidates.Add(Path.Combine(Path.GetDirectoryName(executablePath)!, "resources"));
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

            try
            {
                var windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
                if (Directory.Exists(windowsApps))
                {
                    candidates.AddRange(
                        Directory.EnumerateDirectories(windowsApps, "OpenAI.Codex_*")
                            .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                            .Select(path => Path.Combine(path, "app", "resources")));
                }
            }
            catch
            {
            }

            return candidates
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void CopyDirectory(
            string sourceDirectory,
            string destinationDirectory,
            CancellationToken cancellationToken)
        {
            var sourceRoot = Path.GetFullPath(sourceDirectory)
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(destinationDirectory);
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(sourceDirectory);

            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentDirectory = pendingDirectories.Pop();
                var relativeDirectory = Path.GetRelativePath(sourceDirectory, currentDirectory);
                var targetDirectory = relativeDirectory == "."
                    ? destinationDirectory
                    : Path.Combine(destinationDirectory, relativeDirectory);
                Directory.CreateDirectory(targetDirectory);

                foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(filePath);
                    if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException(DesktopPetText.SkillLinkedFile);

                    var normalizedFilePath = Path.GetFullPath(filePath);
                    if (!normalizedFilePath.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(DesktopPetText.SkillOutsideDirectory);

                    File.Copy(filePath, Path.Combine(targetDirectory, fileInfo.Name), overwrite: false);
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
                {
                    var directoryInfo = new DirectoryInfo(childDirectory);
                    if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException(DesktopPetText.SkillLinkedDirectory);
                    pendingDirectories.Push(childDirectory);
                }
            }
        }

        private static void DeleteInstallStagingDirectory(string destinationRoot, string stagingDirectory)
        {
            try
            {
                var normalizedRoot = Path.GetFullPath(destinationRoot)
                    .TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var normalizedStaging = Path.GetFullPath(stagingDirectory);
                if (normalizedStaging.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(normalizedStaging).StartsWith($".{SkillName}-install-", StringComparison.Ordinal)
                    && Directory.Exists(normalizedStaging))
                {
                    Directory.Delete(normalizedStaging, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
