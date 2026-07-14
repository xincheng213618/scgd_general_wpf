using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentEnvironmentContext
    {
        public const int CurrentVersion = 1;
        public const int MaxScopedPaths = 8;
        public const int MaxPathLength = 1_024;

        public int Version { get; init; } = CurrentVersion;

        public string WorkingDirectory { get; init; } = string.Empty;

        public string Platform { get; init; } = string.Empty;

        public string Architecture { get; init; } = string.Empty;

        public string Shell { get; init; } = string.Empty;

        public string LocalDate { get; init; } = string.Empty;

        public string TimeZoneId { get; init; } = string.Empty;

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableRoots { get; init; } = Array.Empty<string>();

        public string GitRoot { get; init; } = string.Empty;

        public string GitBranch { get; init; } = string.Empty;

        public string GitHead { get; init; } = string.Empty;

        public string Fingerprint { get; init; } = string.Empty;

        public static CopilotAgentEnvironmentContext Capture(
            CopilotAgentRequest request,
            DateTimeOffset? utcNow = null,
            TimeZoneInfo? localTimeZone = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var searchRoots = NormalizeScopedRoots(request.SearchRootPaths);
            var writableRoots = NormalizeScopedRoots(request.WritableLocalRootPaths);
            var activeDocumentPath = NormalizePath(request.ActiveDocumentPath);
            var workingDirectory = NormalizeExistingDirectory(CopilotShellCommandService.ResolveDefaultWorkingDirectory(request))
                ?? NormalizeExistingDirectory(Environment.CurrentDirectory)
                ?? string.Empty;
            var shell = CopilotShellCommandService.GetShellLabel(
                CopilotShellCommandService.ResolveShell(CopilotShellKind.Auto, request.PreferredShell));
            var timeZone = localTimeZone ?? TimeZoneInfo.Local;
            var localNow = TimeZoneInfo.ConvertTime(utcNow ?? DateTimeOffset.UtcNow, timeZone);
            var git = CaptureGitContext(workingDirectory);
            var context = new CopilotAgentEnvironmentContext
            {
                WorkingDirectory = workingDirectory,
                Platform = OperatingSystem.IsWindows() ? "Windows" : RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Shell = shell,
                LocalDate = localNow.ToString("yyyy-MM-dd"),
                TimeZoneId = timeZone.Id,
                ActiveDocumentPath = activeDocumentPath,
                SearchRoots = searchRoots,
                WritableRoots = writableRoots,
                GitRoot = git.Root,
                GitBranch = git.Branch,
                GitHead = git.Head,
            };
            return context.WithFingerprint(ComputeFingerprint(context));
        }

        public string BuildPromptDataBlock()
        {
            var payload = new Dictionary<string, object?>
            {
                ["working_directory"] = WorkingDirectory,
                ["platform"] = Platform,
                ["architecture"] = Architecture,
                ["shell"] = Shell,
                ["local_date"] = LocalDate,
                ["time_zone"] = TimeZoneId,
                ["active_document"] = EmptyAsNull(ActiveDocumentPath),
                ["search_roots"] = SearchRoots,
                ["writable_roots"] = WritableRoots,
                ["git_root"] = EmptyAsNull(GitRoot),
                ["git_branch"] = EmptyAsNull(GitBranch),
                ["git_head"] = string.IsNullOrWhiteSpace(GitHead) ? null : GitHead[..Math.Min(12, GitHead.Length)],
            };
            return JsonSerializer.Serialize(payload);
        }

        public bool IsStructurallyValid()
        {
            return Version == CurrentVersion
                && IsSafeRequiredValue(WorkingDirectory, MaxPathLength)
                && IsSafeRequiredValue(Platform, 256)
                && IsSafeRequiredValue(Architecture, 64)
                && IsSafeRequiredValue(Shell, 64)
                && DateOnly.TryParseExact(LocalDate, "yyyy-MM-dd", out _)
                && IsSafeRequiredValue(TimeZoneId, 256)
                && IsSafeOptionalValue(ActiveDocumentPath, MaxPathLength)
                && IsSafePathList(SearchRoots)
                && IsSafePathList(WritableRoots)
                && IsSafeOptionalValue(GitRoot, MaxPathLength)
                && IsSafeOptionalValue(GitBranch, 512)
                && IsSafeOptionalValue(GitHead, 64)
                && IsSha256(Fingerprint);
        }

        private CopilotAgentEnvironmentContext WithFingerprint(string fingerprint)
        {
            return new CopilotAgentEnvironmentContext
            {
                Version = Version,
                WorkingDirectory = WorkingDirectory,
                Platform = Platform,
                Architecture = Architecture,
                Shell = Shell,
                LocalDate = LocalDate,
                TimeZoneId = TimeZoneId,
                ActiveDocumentPath = ActiveDocumentPath,
                SearchRoots = SearchRoots,
                WritableRoots = WritableRoots,
                GitRoot = GitRoot,
                GitBranch = GitBranch,
                GitHead = GitHead,
                Fingerprint = fingerprint,
            };
        }

        private static string ComputeFingerprint(CopilotAgentEnvironmentContext context)
        {
            var stableData = JsonSerializer.Serialize(new
            {
                context.Version,
                context.WorkingDirectory,
                context.Platform,
                context.Architecture,
                context.Shell,
                context.TimeZoneId,
                context.ActiveDocumentPath,
                context.SearchRoots,
                context.WritableRoots,
                context.GitRoot,
                context.GitBranch,
                context.GitHead,
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableData))).ToLowerInvariant();
        }

        private static string[] NormalizeScopedRoots(IEnumerable<string>? roots)
        {
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(roots)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(MaxScopedPaths)
                .ToArray();
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                var fullPath = Path.GetFullPath(path.Trim());
                return fullPath.Length <= MaxPathLength && !fullPath.Any(char.IsControl) ? fullPath : string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return string.Empty;
            }
        }

        private static string? NormalizeExistingDirectory(string? path)
        {
            var normalized = NormalizePath(path);
            return Directory.Exists(normalized) ? normalized : null;
        }

        private static GitContext CaptureGitContext(string workingDirectory)
        {
            var current = NormalizeExistingDirectory(workingDirectory);
            for (var depth = 0; depth < 32 && !string.IsNullOrWhiteSpace(current); depth++)
            {
                var marker = Path.Combine(current, ".git");
                var gitDirectory = Directory.Exists(marker) ? marker : ResolveGitDirectory(marker, current);
                if (!string.IsNullOrWhiteSpace(gitDirectory))
                    return ReadGitContext(current, gitDirectory);
                current = Directory.GetParent(current)?.FullName;
            }
            return GitContext.Empty;
        }

        private static string ResolveGitDirectory(string markerPath, string repositoryRoot)
        {
            if (!File.Exists(markerPath))
                return string.Empty;
            var marker = ReadBoundedText(markerPath, 2_048).Trim();
            if (!marker.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var value = marker["gitdir:".Length..].Trim();
            var candidate = Path.IsPathRooted(value) ? value : Path.Combine(repositoryRoot, value);
            return NormalizeExistingDirectory(candidate) ?? string.Empty;
        }

        private static GitContext ReadGitContext(string repositoryRoot, string gitDirectory)
        {
            var headValue = ReadBoundedText(Path.Combine(gitDirectory, "HEAD"), 2_048).Trim();
            if (string.IsNullOrWhiteSpace(headValue))
                return new GitContext(repositoryRoot, string.Empty, string.Empty);
            if (!headValue.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
                return new GitContext(repositoryRoot, string.Empty, NormalizeCommit(headValue));

            var reference = headValue["ref:".Length..].Trim().Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(reference) || reference.Split(Path.DirectorySeparatorChar).Any(part => part == ".."))
                return new GitContext(repositoryRoot, string.Empty, string.Empty);
            var branch = headValue["ref:".Length..].Trim().StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
                ? headValue["ref:".Length..].Trim()["refs/heads/".Length..]
                : headValue["ref:".Length..].Trim();
            var referenceName = headValue["ref:".Length..].Trim();
            var commit = ReadGitReference(gitDirectory, reference, referenceName);
            if (string.IsNullOrWhiteSpace(commit))
            {
                var commonDirectory = ResolveCommonGitDirectory(gitDirectory);
                if (!string.Equals(commonDirectory, gitDirectory, StringComparison.OrdinalIgnoreCase))
                    commit = ReadGitReference(commonDirectory, reference, referenceName);
            }
            return new GitContext(repositoryRoot, Sanitize(branch, 512), commit);
        }

        private static string ResolveCommonGitDirectory(string gitDirectory)
        {
            var value = ReadBoundedText(Path.Combine(gitDirectory, "commondir"), 2_048).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return gitDirectory;
            var candidate = Path.IsPathRooted(value) ? value : Path.Combine(gitDirectory, value);
            return NormalizeExistingDirectory(candidate) ?? gitDirectory;
        }

        private static string ReadGitReference(string gitDirectory, string relativeReference, string referenceName)
        {
            var commit = NormalizeCommit(ReadBoundedText(Path.Combine(gitDirectory, relativeReference), 256).Trim());
            return string.IsNullOrWhiteSpace(commit)
                ? ReadPackedReference(Path.Combine(gitDirectory, "packed-refs"), referenceName)
                : commit;
        }

        private static string ReadPackedReference(string packedRefsPath, string reference)
        {
            var content = ReadBoundedText(packedRefsPath, 1_000_000);
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;
            foreach (var line in content.Split('\n').Take(20_000))
            {
                if (line.StartsWith('#') || line.StartsWith('^'))
                    continue;
                var separator = line.IndexOf(' ');
                if (separator > 0 && string.Equals(line[(separator + 1)..].Trim(), reference, StringComparison.Ordinal))
                    return NormalizeCommit(line[..separator]);
            }
            return string.Empty;
        }

        private static string ReadBoundedText(string path, int maxCharacters)
        {
            if (!File.Exists(path))
                return string.Empty;
            try
            {
                using var reader = new StreamReader(path, Encoding.UTF8, true);
                var buffer = new char[maxCharacters];
                var read = reader.ReadBlock(buffer, 0, buffer.Length);
                return new string(buffer, 0, read);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static string NormalizeCommit(string value)
        {
            var candidate = (value ?? string.Empty).Trim();
            return candidate.Length is 40 or 64 && candidate.All(Uri.IsHexDigit) ? candidate.ToLowerInvariant() : string.Empty;
        }

        private static string Sanitize(string value, int maxLength)
        {
            var candidate = (value ?? string.Empty).Trim();
            return candidate.Length <= maxLength && !candidate.Any(char.IsControl) ? candidate : string.Empty;
        }

        private static string? EmptyAsNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        private static bool IsSafeRequiredValue(string value, int maxLength) => !string.IsNullOrWhiteSpace(value) && IsSafeOptionalValue(value, maxLength);

        private static bool IsSafeOptionalValue(string value, int maxLength) => value != null && value.Length <= maxLength && !value.Any(char.IsControl);

        private static bool IsSafePathList(IReadOnlyList<string> paths)
        {
            return paths != null
                && paths.Count <= MaxScopedPaths
                && paths.All(path => IsSafeRequiredValue(path, MaxPathLength))
                && paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Count;
        }

        private static bool IsSha256(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

        private readonly record struct GitContext(string Root, string Branch, string Head)
        {
            public static GitContext Empty { get; } = new(string.Empty, string.Empty, string.Empty);
        }
    }
}
