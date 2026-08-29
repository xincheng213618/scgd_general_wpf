using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ColorVision.ServiceHost
{
    public sealed class ServiceHostRuntimeIntegrity
    {
        public static ServiceHostRuntimeIntegrity Unavailable { get; } = new();

        public bool CanEvaluate { get; init; }

        public IReadOnlyList<string> ExpectedFiles { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> MissingPackageFiles { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> MissingInstalledFiles { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> MismatchedInstalledFiles { get; init; } = Array.Empty<string>();

        public bool IsPackageComplete => CanEvaluate && MissingPackageFiles.Count == 0;

        public bool IsInstalledComplete => CanEvaluate
            && MissingInstalledFiles.Count == 0
            && MismatchedInstalledFiles.Count == 0;

        public int InstalledIssueCount => MissingInstalledFiles.Count + MismatchedInstalledFiles.Count;
    }

    internal static class ServiceHostRuntimeIntegrityChecker
    {
        private static readonly string[] CoreFiles =
        [
            "ColorVisionServiceHost.exe",
            "ColorVisionServiceHost.dll",
            "ColorVisionServiceHost.deps.json",
            "ColorVisionServiceHost.runtimeconfig.json",
        ];

        internal static ServiceHostRuntimeIntegrity Inspect(string packageDirectory, string installedDirectory)
        {
            if (string.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
                return ServiceHostRuntimeIntegrity.Unavailable;

            HashSet<string> expectedFiles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string coreFile in CoreFiles)
                expectedFiles.Add(coreFile);

            foreach (string packageFile in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(packageDirectory, packageFile);
                if (!string.Equals(Path.GetExtension(relativePath), ".pdb", StringComparison.OrdinalIgnoreCase))
                    expectedFiles.Add(NormalizeRelativePath(relativePath));
            }

            string dependencyFile = Path.Combine(packageDirectory, "ColorVisionServiceHost.deps.json");
            foreach (string dependencyPath in ReadDependencyAssets(dependencyFile))
                expectedFiles.Add(dependencyPath);

            string[] orderedExpectedFiles = expectedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            string[] missingPackageFiles = orderedExpectedFiles
                .Where(relativePath => !File.Exists(CombineUnderRoot(packageDirectory, relativePath)))
                .ToArray();
            string[] missingInstalledFiles = orderedExpectedFiles
                .Where(relativePath => !File.Exists(CombineUnderRoot(installedDirectory, relativePath)))
                .ToArray();

            HashSet<string> missingInstalledSet = new(missingInstalledFiles, StringComparer.OrdinalIgnoreCase);
            string[] mismatchedInstalledFiles = orderedExpectedFiles
                .Where(relativePath => !missingInstalledSet.Contains(relativePath))
                .Where(relativePath => File.Exists(CombineUnderRoot(packageDirectory, relativePath)))
                .Where(relativePath => !FilesMatch(
                    CombineUnderRoot(packageDirectory, relativePath),
                    CombineUnderRoot(installedDirectory, relativePath)))
                .ToArray();

            return new ServiceHostRuntimeIntegrity
            {
                CanEvaluate = true,
                ExpectedFiles = orderedExpectedFiles,
                MissingPackageFiles = missingPackageFiles,
                MissingInstalledFiles = missingInstalledFiles,
                MismatchedInstalledFiles = mismatchedInstalledFiles,
            };
        }

        private static IEnumerable<string> ReadDependencyAssets(string dependencyFile)
        {
            if (!File.Exists(dependencyFile))
                yield break;

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(dependencyFile));
            }
            catch
            {
                yield break;
            }

            if (root["targets"] is not JObject targets)
                yield break;

            foreach (JObject target in targets.Properties().Select(property => property.Value).OfType<JObject>())
            {
                foreach (JObject library in target.Properties().Select(property => property.Value).OfType<JObject>())
                {
                    if (library["runtime"] is JObject runtimeAssets)
                    {
                        foreach (JProperty asset in runtimeAssets.Properties())
                            yield return NormalizeRelativePath(Path.GetFileName(asset.Name));
                    }

                    if (library["runtimeTargets"] is JObject runtimeTargets)
                    {
                        foreach (JProperty asset in runtimeTargets.Properties())
                            yield return NormalizeRelativePath(asset.Name);
                    }

                    if (library["native"] is JObject nativeAssets)
                    {
                        foreach (JProperty asset in nativeAssets.Properties())
                            yield return NormalizeRelativePath(Path.GetFileName(asset.Name));
                    }
                }
            }
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        }

        private static string CombineUnderRoot(string rootDirectory, string relativePath)
        {
            string normalizedRoot = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string combinedPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
            if (!combinedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Runtime dependency path escapes its root directory: {relativePath}");
            return combinedPath;
        }

        private static bool FilesMatch(string packagePath, string installedPath)
        {
            try
            {
                FileInfo packageFile = new(packagePath);
                FileInfo installedFile = new(installedPath);
                if (packageFile.Length != installedFile.Length)
                    return false;

                using FileStream packageStream = new(packagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using FileStream installedStream = new(installedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return SHA256.HashData(packageStream).SequenceEqual(SHA256.HashData(installedStream));
            }
            catch
            {
                return false;
            }
        }
    }
}
