using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed record CopilotGitDiffSection(
        string Scope,
        bool HasChanges,
        bool OutputComplete,
        bool PatchTruncated,
        string Patch);

    public sealed record CopilotGitDiffSnapshot(
        string RepositoryRoot,
        string Scope,
        string PathFilter,
        bool HasChanges,
        bool OutputComplete,
        bool PatchTruncated,
        IReadOnlyList<CopilotGitDiffSection> Sections)
    {
        public string Target { get; init; } = "working_tree";

        public string Revision { get; init; } = string.Empty;

        public string ResolvedRevision { get; init; } = string.Empty;

        public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

        public bool ChangedPathsComplete { get; init; }

        internal bool IsStructurallyValid() =>
            CopilotGitDiffResultProtocol.IsStructurallyValid(this);

        internal CopilotGitDiffSnapshot CreateSnapshot() =>
            CopilotGitDiffResultProtocol.CreateSnapshot(this);
    }

    internal sealed class CopilotGitDiffInspectionService
    {
        public const int MaxPatchCharactersPerSection = 24_000;
        private const string ShellTruncationMarker = "...<shell output truncated>...";
        private const string PatchTruncationMarker = "...<Git diff excerpt truncated>...";
        private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(20);
        private readonly ICopilotShellProcessRunner _runner;
        private readonly Func<string?> _gitExecutableProvider;

        public CopilotGitDiffInspectionService()
            : this(new CopilotShellProcessRunner(), CopilotGitProcessSupport.FindTrustedGitExecutable)
        {
        }

        public CopilotGitDiffInspectionService(
            ICopilotShellProcessRunner runner,
            Func<string?> gitExecutableProvider)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _gitExecutableProvider = gitExecutableProvider ?? throw new ArgumentNullException(nameof(gitExecutableProvider));
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryReadScope(input, out var scope, out var scopeError))
                return Failure(CopilotToolFailureKind.Validation, "The requested Git diff scope is invalid.", scopeError);
            if (!TryReadTarget(input, out var target, out var revision, out var targetError))
                return Failure(CopilotToolFailureKind.Validation, "The requested Git review target is invalid.", targetError);
            if (!string.Equals(target, "working_tree", StringComparison.Ordinal)
                && input.Arguments.Keys.Any(key => string.Equals(key, "scope", StringComparison.OrdinalIgnoreCase)))
            {
                return Failure(
                    CopilotToolFailureKind.Validation,
                    "A revision review cannot also select a working-tree scope.",
                    "Omit 'scope' when target is base_branch or commit.");
            }

            var allowedRoots = CopilotGitProcessSupport.GetAllowedRoots(request);
            if (allowedRoots.Count == 0)
                return Failure(CopilotToolFailureKind.NotFound, "No Git-inspectable workspace is available.", "The current request has no existing search or writable root.");

            if (!CopilotGitProcessSupport.TryResolveTargetPath(input.Path, allowedRoots, requireExisting: true, out var selectedPath, out var targetDirectory, out var containingRoot, out var pathError))
                return Failure(CopilotToolFailureKind.Validation, "The requested Git diff path is outside the current workspace.", pathError);

            var repositoryRoot = CopilotGitProcessSupport.FindRepositoryRoot(targetDirectory, containingRoot);
            if (string.IsNullOrWhiteSpace(repositoryRoot))
                return Failure(CopilotToolFailureKind.NotFound, "No Git working tree was found in the selected workspace.", "A .git directory or linked-worktree marker was not found within the allowed root.");

            var pathFilter = CopilotGitProcessSupport.GetRepositoryRelativePath(repositoryRoot, selectedPath);
            if (!string.Equals(repositoryRoot, selectedPath, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(pathFilter))
                return Failure(CopilotToolFailureKind.Validation, "The requested Git diff path could not be made repository-relative.", "The selected path is not safely addressable within the discovered repository.");

            var gitExecutable = CopilotGitProcessSupport.NormalizeFile(_gitExecutableProvider());
            if (string.IsNullOrWhiteSpace(gitExecutable))
                return Failure(CopilotToolFailureKind.NotFound, "Git could not be located.", "A trusted Git for Windows executable was not found.");

            var sections = new List<CopilotGitDiffSection>();
            var changedPaths = new List<string>();
            var changedPathsComplete = true;
            var resolvedRevision = string.Empty;
            var environmentVariables = request.CodexShellEnvironmentPolicy
                .CreateEnvironmentVariables(request.ConversationId);
            if (string.Equals(target, "working_tree", StringComparison.Ordinal))
            {
                foreach (var sectionScope in GetSectionScopes(scope))
                {
                    var sectionResult = await ExecuteSectionAsync(
                        gitExecutable,
                        repositoryRoot,
                        pathFilter,
                        sectionScope,
                        environmentVariables,
                        cancellationToken);
                    if (sectionResult.Failure != null)
                        return sectionResult.Failure;
                    sections.Add(sectionResult.Section!);
                    changedPaths.AddRange(sectionResult.ChangedPaths);
                    changedPathsComplete &= sectionResult.ChangedPathsComplete;
                }

                if (scope is "unstaged" or "both")
                {
                    var untrackedResult = await ExecuteUntrackedSectionAsync(
                        gitExecutable,
                        repositoryRoot,
                        pathFilter,
                        environmentVariables,
                        cancellationToken);
                    if (untrackedResult.Failure != null)
                        return untrackedResult.Failure;
                    sections.Add(untrackedResult.Section!);
                    changedPaths.AddRange(untrackedResult.ChangedPaths);
                    changedPathsComplete &= untrackedResult.ChangedPathsComplete;
                }
            }
            else
            {
                var revisionResult = await ExecuteRevisionTargetAsync(
                    gitExecutable,
                    repositoryRoot,
                    pathFilter,
                    target,
                    revision,
                    environmentVariables,
                    cancellationToken);
                if (revisionResult.Failure != null)
                    return revisionResult.Failure;
                resolvedRevision = revisionResult.ResolvedRevision;
                sections.Add(revisionResult.Section!);
                changedPaths.AddRange(revisionResult.ChangedPaths);
                changedPathsComplete &= revisionResult.ChangedPathsComplete;
            }

            var normalizedChangedPaths = changedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Take(CopilotGitDiffResultProtocol.MaxChangedPaths)
                .ToArray();
            changedPathsComplete &= normalizedChangedPaths.Length
                == changedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count();

            var snapshot = new CopilotGitDiffSnapshot(
                repositoryRoot,
                scope,
                pathFilter,
                sections.Any(section => section.HasChanges),
                sections.All(section => section.OutputComplete),
                sections.Any(section => section.PatchTruncated),
                sections)
            {
                Target = target,
                Revision = revision,
                ResolvedRevision = resolvedRevision,
                ChangedPaths = normalizedChangedPaths,
                ChangedPathsComplete = changedPathsComplete,
            };
            return new CopilotToolResult
            {
                ToolName = "InspectGitDiff",
                Success = true,
                Summary = BuildSummary(snapshot),
                Content = CopilotGitDiffResultProtocol.Serialize(snapshot),
            };
        }

        private async Task<(
            CopilotGitDiffSection? Section,
            IReadOnlyList<string> ChangedPaths,
            bool ChangedPathsComplete,
            CopilotToolResult? Failure)> ExecuteSectionAsync(
            string gitExecutable,
            string repositoryRoot,
            string pathFilter,
            string scope,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            var execution = await RunGitAsync(
                gitExecutable,
                repositoryRoot,
                BuildArguments(repositoryRoot, pathFilter, scope),
                $"{scope} Git diff",
                CopilotToolFailureKind.Internal,
                environmentVariables,
                cancellationToken);
            if (execution.Failure != null)
                return (null, Array.Empty<string>(), false, execution.Failure);

            var changedPathResult = await ExecuteChangedPathsAsync(
                gitExecutable,
                repositoryRoot,
                BuildChangedPathArguments(repositoryRoot, pathFilter, scope),
                $"{scope} Git changed-path listing",
                environmentVariables,
                cancellationToken);
            if (changedPathResult.Failure != null)
                return (null, Array.Empty<string>(), false, changedPathResult.Failure);

            var rawPatch = execution.Result!.StandardOutput ?? string.Empty;
            var runnerTruncated = execution.Result.StandardOutputTruncated
                || rawPatch.Contains(ShellTruncationMarker, StringComparison.Ordinal);
            var boundedPatch = BoundPatch(rawPatch, out var serviceTruncated);
            var truncated = runnerTruncated || serviceTruncated;
            return (new CopilotGitDiffSection(
                scope,
                !string.IsNullOrWhiteSpace(rawPatch),
                !truncated,
                truncated,
                boundedPatch),
                changedPathResult.Paths,
                changedPathResult.Complete,
                null);
        }

        private async Task<(
            CopilotGitDiffSection? Section,
            string ResolvedRevision,
            IReadOnlyList<string> ChangedPaths,
            bool ChangedPathsComplete,
            CopilotToolResult? Failure)> ExecuteRevisionTargetAsync(
            string gitExecutable,
            string repositoryRoot,
            string pathFilter,
            string target,
            string revision,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            var resolution = await RunGitAsync(
                gitExecutable,
                repositoryRoot,
                BuildRevisionResolveArguments(repositoryRoot, revision),
                "Git revision resolution",
                CopilotToolFailureKind.NotFound,
                environmentVariables,
                cancellationToken);
            if (resolution.Failure != null)
                return (null, string.Empty, Array.Empty<string>(), false, resolution.Failure);
            if (!TryReadObjectId(resolution.Result!.StandardOutput, out var resolvedRevision))
            {
                return (null, string.Empty, Array.Empty<string>(), false, Failure(
                    CopilotToolFailureKind.Internal,
                    "Git returned an invalid resolved revision.",
                    "The fixed revision lookup did not return one hexadecimal object id."));
            }

            string comparisonRevision;
            IReadOnlyList<string> patchArguments;
            if (string.Equals(target, "base_branch", StringComparison.Ordinal))
            {
                var mergeBase = await RunGitAsync(
                    gitExecutable,
                    repositoryRoot,
                    BuildMergeBaseArguments(repositoryRoot, resolvedRevision),
                    "Git merge-base resolution",
                    CopilotToolFailureKind.NotFound,
                    environmentVariables,
                    cancellationToken);
                if (mergeBase.Failure != null)
                    return (null, string.Empty, Array.Empty<string>(), false, mergeBase.Failure);
                if (!TryReadObjectId(mergeBase.Result!.StandardOutput, out comparisonRevision))
                {
                    return (null, string.Empty, Array.Empty<string>(), false, Failure(
                        CopilotToolFailureKind.Internal,
                        "Git returned an invalid merge base.",
                        "The fixed merge-base lookup did not return one hexadecimal object id."));
                }
                patchArguments = BuildBaseBranchArguments(
                    repositoryRoot,
                    pathFilter,
                    comparisonRevision);
            }
            else
            {
                comparisonRevision = resolvedRevision;
                patchArguments = BuildCommitArguments(
                    repositoryRoot,
                    pathFilter,
                    resolvedRevision);
            }

            var patch = await RunGitAsync(
                gitExecutable,
                repositoryRoot,
                patchArguments,
                string.Equals(target, "base_branch", StringComparison.Ordinal)
                    ? "base-branch Git diff"
                    : "commit Git diff",
                CopilotToolFailureKind.Internal,
                environmentVariables,
                cancellationToken);
            if (patch.Failure != null)
                return (null, string.Empty, Array.Empty<string>(), false, patch.Failure);

            var changedPathArguments = string.Equals(target, "base_branch", StringComparison.Ordinal)
                ? BuildBaseBranchChangedPathArguments(repositoryRoot, pathFilter, comparisonRevision)
                : BuildCommitChangedPathArguments(repositoryRoot, pathFilter, resolvedRevision);
            var changedPathResult = await ExecuteChangedPathsAsync(
                gitExecutable,
                repositoryRoot,
                changedPathArguments,
                string.Equals(target, "base_branch", StringComparison.Ordinal)
                    ? "base-branch Git changed-path listing"
                    : "commit Git changed-path listing",
                environmentVariables,
                cancellationToken);
            if (changedPathResult.Failure != null)
            {
                return (
                    null,
                    string.Empty,
                    Array.Empty<string>(),
                    false,
                    changedPathResult.Failure);
            }

            var rawPatch = patch.Result!.StandardOutput ?? string.Empty;
            var runnerTruncated = patch.Result.StandardOutputTruncated
                || rawPatch.Contains(ShellTruncationMarker, StringComparison.Ordinal);
            var boundedPatch = BoundPatch(rawPatch, out var serviceTruncated);
            var truncated = runnerTruncated || serviceTruncated;
            return (new CopilotGitDiffSection(
                target,
                !string.IsNullOrWhiteSpace(rawPatch),
                !truncated,
                truncated,
                boundedPatch),
                resolvedRevision,
                changedPathResult.Paths,
                changedPathResult.Complete,
                null);
        }

        private async Task<(
            CopilotGitDiffSection? Section,
            IReadOnlyList<string> ChangedPaths,
            bool ChangedPathsComplete,
            CopilotToolResult? Failure)> ExecuteUntrackedSectionAsync(
            string gitExecutable,
            string repositoryRoot,
            string pathFilter,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            var changedPathResult = await ExecuteChangedPathsAsync(
                gitExecutable,
                repositoryRoot,
                BuildUntrackedArguments(repositoryRoot, pathFilter),
                "untracked Git changed-path listing",
                environmentVariables,
                cancellationToken);
            if (changedPathResult.Failure != null)
            {
                return (
                    null,
                    Array.Empty<string>(),
                    false,
                    changedPathResult.Failure);
            }

            var patch = BuildUntrackedPatch(
                repositoryRoot,
                changedPathResult.Paths,
                out var contentComplete);
            if (!changedPathResult.Complete && !patch.Contains(PatchTruncationMarker, StringComparison.Ordinal))
                patch += Environment.NewLine + PatchTruncationMarker + Environment.NewLine;
            var boundedPatch = BoundPatch(patch, out var serviceTruncated);
            var truncated = !changedPathResult.Complete || !contentComplete || serviceTruncated;
            return (
                new CopilotGitDiffSection(
                    "untracked",
                    !string.IsNullOrWhiteSpace(patch),
                    !truncated,
                    truncated,
                    boundedPatch),
                changedPathResult.Paths,
                changedPathResult.Complete,
                null);
        }

        private async Task<(
            IReadOnlyList<string> Paths,
            bool Complete,
            CopilotToolResult? Failure)> ExecuteChangedPathsAsync(
            string gitExecutable,
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            string operation,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            var execution = await RunGitAsync(
                gitExecutable,
                repositoryRoot,
                arguments,
                operation,
                CopilotToolFailureKind.Internal,
                environmentVariables,
                cancellationToken);
            if (execution.Failure != null)
                return (Array.Empty<string>(), false, execution.Failure);

            var result = execution.Result!;
            var paths = ParseChangedPaths(
                repositoryRoot,
                result.StandardOutput,
                result.StandardOutputTruncated,
                out var complete);
            return (paths, complete, null);
        }

        private static IReadOnlyList<string> ParseChangedPaths(
            string repositoryRoot,
            string? output,
            bool runnerTruncated,
            out bool complete)
        {
            var value = output ?? string.Empty;
            complete = !runnerTruncated
                && !value.Contains(ShellTruncationMarker, StringComparison.Ordinal);
            if (value.Length == 0)
                return Array.Empty<string>();

            if (!value.EndsWith('\0'))
                complete = false;
            var paths = new List<string>();
            foreach (var rawPath in value.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                if (rawPath.Contains(ShellTruncationMarker, StringComparison.Ordinal)
                    || !TryNormalizeChangedPath(repositoryRoot, rawPath, out var normalizedPath))
                {
                    complete = false;
                    continue;
                }
                if (paths.Contains(normalizedPath, StringComparer.OrdinalIgnoreCase))
                    continue;
                if (paths.Count >= CopilotGitDiffResultProtocol.MaxChangedPaths)
                {
                    complete = false;
                    continue;
                }
                paths.Add(normalizedPath);
            }
            return paths;
        }

        private static bool TryNormalizeChangedPath(
            string repositoryRoot,
            string path,
            out string normalizedPath)
        {
            normalizedPath = (path ?? string.Empty).Replace('\\', '/');
            if (!CopilotGitDiffResultProtocol.IsChangedPathStructurallyValid(normalizedPath))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(
                    normalizedPath.Replace('/', Path.DirectorySeparatorChar),
                    repositoryRoot);
                if (!CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [repositoryRoot]))
                    return false;
                normalizedPath = CopilotGitProcessSupport.GetRepositoryRelativePath(
                    repositoryRoot,
                    fullPath);
                return CopilotGitDiffResultProtocol.IsChangedPathStructurallyValid(normalizedPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                normalizedPath = string.Empty;
                return false;
            }
        }

        private static string BuildUntrackedPatch(
            string repositoryRoot,
            IReadOnlyList<string> paths,
            out bool complete)
        {
            complete = true;
            if (paths.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var path in paths)
            {
                var displayPath = QuoteGitPath("b/" + path);
                builder.Append("diff --git ")
                    .Append(QuoteGitPath("a/" + path))
                    .Append(' ')
                    .AppendLine(displayPath);
                builder.AppendLine("new file mode 100644");
                builder.AppendLine("--- /dev/null");
                builder.Append("+++ ").AppendLine(displayPath);

                if (!CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(
                    path,
                    [repositoryRoot],
                    out var fullPath,
                    out _))
                {
                    complete = false;
                    builder.AppendLine("@@ -0,0 +1 @@");
                    builder.AppendLine("+...<untracked file became unavailable during review>...");
                    continue;
                }

                if (!TryReadBoundedFile(fullPath, out var bytes, out var fileComplete)
                    || !TryDecodeText(bytes, out var text))
                {
                    complete &= fileComplete;
                    builder.Append("Binary files /dev/null and ")
                        .Append(displayPath)
                        .AppendLine(" differ");
                    continue;
                }

                complete &= fileComplete;
                AppendNewFileHunk(builder, text, fileComplete);
            }
            return builder.ToString();
        }

        private static bool TryReadBoundedFile(
            string path,
            out byte[] bytes,
            out bool complete)
        {
            const int maximumBytes = MaxPatchCharactersPerSection;
            bytes = Array.Empty<byte>();
            complete = false;
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                var buffer = new byte[maximumBytes + 1];
                var count = 0;
                while (count < buffer.Length)
                {
                    var read = stream.Read(buffer, count, buffer.Length - count);
                    if (read == 0)
                        break;
                    count += read;
                }
                complete = count <= maximumBytes && stream.Position == stream.Length;
                bytes = buffer[..Math.Min(count, maximumBytes)];
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return false;
            }
        }

        private static bool TryDecodeText(byte[] bytes, out string text)
        {
            text = string.Empty;
            if (bytes.Contains((byte)0)
                && !(bytes.Length >= 2
                    && ((bytes[0] == 0xff && bytes[1] == 0xfe)
                        || (bytes[0] == 0xfe && bytes[1] == 0xff))))
            {
                return false;
            }

            try
            {
                Encoding encoding;
                var offset = 0;
                if (bytes.Length >= 3
                    && bytes[0] == 0xef
                    && bytes[1] == 0xbb
                    && bytes[2] == 0xbf)
                {
                    encoding = new UTF8Encoding(false, true);
                    offset = 3;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
                {
                    encoding = new UnicodeEncoding(false, true, true);
                    offset = 2;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
                {
                    encoding = new UnicodeEncoding(true, true, true);
                    offset = 2;
                }
                else
                {
                    encoding = new UTF8Encoding(false, true);
                }

                text = encoding.GetString(bytes, offset, bytes.Length - offset);
                return !text.Any(character => char.IsControl(character)
                    && character is not '\r' and not '\n' and not '\t');
            }
            catch (DecoderFallbackException)
            {
                text = string.Empty;
                return false;
            }
        }

        private static void AppendNewFileHunk(
            StringBuilder builder,
            string text,
            bool complete)
        {
            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            if (normalized.Length == 0 && complete)
                return;
            var endsWithNewLine = normalized.EndsWith('\n');
            var lines = normalized.Split('\n').ToList();
            if (endsWithNewLine)
                lines.RemoveAt(lines.Count - 1);
            if (!complete)
                lines.Add("...<untracked file content truncated>...");
            if (lines.Count == 0)
                return;

            builder.Append("@@ -0,0 +1,")
                .Append(lines.Count)
                .AppendLine(" @@");
            foreach (var line in lines)
                builder.Append('+').AppendLine(line);
            if (!endsWithNewLine && complete)
                builder.AppendLine("\\ No newline at end of file");
        }

        private static string QuoteGitPath(string path)
        {
            if (!path.Any(character => char.IsWhiteSpace(character) || character is '\\' or '"'))
                return path;
            return '"' + path
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal)
                .Replace("\t", "\\t", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal)
                + '"';
        }

        private async Task<(CopilotShellProcessResult? Result, CopilotToolResult? Failure)> RunGitAsync(
            string gitExecutable,
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            string operation,
            CopilotToolFailureKind nonzeroFailureKind,
            IReadOnlyDictionary<string, string> environmentVariables,
            CancellationToken cancellationToken)
        {
            CopilotShellProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    CopilotShellKind.PowerShell,
                    gitExecutable,
                    arguments,
                    repositoryRoot,
                    ExecutionTimeout)
                {
                    EnvironmentVariables = environmentVariables,
                    EnvironmentOverrides = CopilotGitProcessSupport.EnvironmentOverrides,
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                return (null, Failure(
                    CopilotToolFailureKind.Internal,
                    operation + " could not start.",
                    CopilotMcpAuditLogger.RedactText(ex.Message)));
            }

            if (processResult.TimedOut)
            {
                return (null, Failure(
                    CopilotToolFailureKind.Transient,
                    operation + " timed out.",
                    $"The fixed Git command exceeded its {ExecutionTimeout.TotalSeconds:N0}-second timeout."));
            }
            if (processResult.ExitCode != 0)
            {
                var detail = CopilotMcpAuditLogger.RedactText(processResult.StandardError).Trim();
                if (detail.Length > 600)
                    detail = detail[..600] + "...";
                return (null, Failure(
                    nonzeroFailureKind,
                    operation + " did not return a usable result.",
                    string.IsNullOrWhiteSpace(detail)
                        ? $"The fixed Git command exited with code {processResult.ExitCode}."
                        : detail));
            }

            return (processResult, null);
        }

        private static List<string> BuildArguments(string repositoryRoot, string pathFilter, string scope)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "diff",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--no-color",
                "--unified=3",
            ]);
            if (string.Equals(scope, "staged", StringComparison.Ordinal))
                arguments.Add("--cached");
            arguments.Add("--");
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildChangedPathArguments(
            string repositoryRoot,
            string pathFilter,
            string scope)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "diff",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--name-only",
                "-z",
            ]);
            if (string.Equals(scope, "staged", StringComparison.Ordinal))
                arguments.Add("--cached");
            arguments.Add("--");
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildUntrackedArguments(
            string repositoryRoot,
            string pathFilter)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "ls-files",
                "--others",
                "--exclude-standard",
                "-z",
                "--",
            ]);
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildRevisionResolveArguments(string repositoryRoot, string revision)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(["rev-parse", "--verify", "--end-of-options", revision + "^{commit}"]);
            return arguments;
        }

        private static List<string> BuildMergeBaseArguments(string repositoryRoot, string resolvedRevision)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(["merge-base", resolvedRevision, "HEAD"]);
            return arguments;
        }

        private static List<string> BuildBaseBranchArguments(
            string repositoryRoot,
            string pathFilter,
            string mergeBase)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "diff",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--no-color",
                "--unified=3",
                mergeBase,
                "HEAD",
                "--",
            ]);
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildBaseBranchChangedPathArguments(
            string repositoryRoot,
            string pathFilter,
            string mergeBase)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "diff",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--name-only",
                "-z",
                mergeBase,
                "HEAD",
                "--",
            ]);
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildCommitArguments(
            string repositoryRoot,
            string pathFilter,
            string resolvedRevision)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "show",
                "--format=",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--no-color",
                "--unified=3",
                resolvedRevision,
                "--",
            ]);
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildCommitChangedPathArguments(
            string repositoryRoot,
            string pathFilter,
            string resolvedRevision)
        {
            var arguments = BuildCommonArguments(repositoryRoot);
            arguments.AddRange(
            [
                "show",
                "--format=",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--name-only",
                "-z",
                resolvedRevision,
                "--",
            ]);
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

        private static List<string> BuildCommonArguments(string repositoryRoot) =>
        [
            "--no-pager",
            "--no-optional-locks",
            "-c", "core.quotepath=false",
            "-c", "core.fsmonitor=false",
            "-c", "core.untrackedCache=false",
            "-c", "core.worktree=" + repositoryRoot,
        ];

        private static IReadOnlyList<string> GetSectionScopes(string scope)
        {
            return scope switch
            {
                "both" => ["unstaged", "staged"],
                "staged" => ["staged"],
                _ => ["unstaged"],
            };
        }

        private static bool TryReadScope(CopilotAgentToolInput input, out string scope, out string error)
        {
            scope = "unstaged";
            error = string.Empty;
            var pair = input.Arguments.FirstOrDefault(argument => string.Equals(argument.Key, "scope", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                return true;

            var value = pair.Value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null,
            };
            if (value is null)
            {
                error = "Argument 'scope' must be a string.";
                return false;
            }
            scope = value.Trim().ToLowerInvariant();
            if (scope is "unstaged" or "staged" or "both")
                return true;
            error = "Argument 'scope' must be one of: unstaged, staged, both.";
            return false;
        }

        private static bool TryReadTarget(
            CopilotAgentToolInput input,
            out string target,
            out string revision,
            out string error)
        {
            target = "working_tree";
            revision = string.Empty;
            error = string.Empty;
            if (!TryReadOptionalString(input, "target", out var targetValue, out error)
                || !TryReadOptionalString(input, "revision", out var revisionValue, out error))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(targetValue))
                target = targetValue.Trim().ToLowerInvariant();
            revision = revisionValue.Trim();

            if (target is not ("working_tree" or "base_branch" or "commit"))
            {
                error = "Argument 'target' must be one of: working_tree, base_branch, commit.";
                return false;
            }

            var hasRevisionArgument = input.Arguments.Keys.Any(
                key => string.Equals(key, "revision", StringComparison.OrdinalIgnoreCase));
            if (string.Equals(target, "working_tree", StringComparison.Ordinal))
            {
                if (hasRevisionArgument)
                {
                    error = "Argument 'revision' is only valid for base_branch or commit targets.";
                    return false;
                }
                return true;
            }

            return TryValidateRevision(target, revision, out error);
        }

        internal static bool TryValidateRevision(string target, string revision, out string error)
        {
            error = string.Empty;
            revision = revision?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(revision))
            {
                error = "Argument 'revision' is required for base_branch or commit targets.";
                return false;
            }
            if (revision.Length > 256)
            {
                error = "Argument 'revision' must not exceed 256 characters.";
                return false;
            }

            if (string.Equals(target, "commit", StringComparison.Ordinal))
            {
                if (revision.Length is < 7 or > 64 || !revision.All(Uri.IsHexDigit))
                {
                    error = "A commit revision must be a 7-to-64-character hexadecimal object id.";
                    return false;
                }
                return true;
            }

            if (!IsSafeBranchRevision(revision))
            {
                error = "A base-branch revision must be a plain Git ref name without revision operators or option syntax.";
                return false;
            }
            return true;
        }

        private static bool TryReadOptionalString(
            CopilotAgentToolInput input,
            string name,
            out string value,
            out string error)
        {
            value = string.Empty;
            error = string.Empty;
            var pair = input.Arguments.FirstOrDefault(
                argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(pair.Key))
                return true;
            value = pair.Value switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
                _ => string.Empty,
            };
            if (pair.Value is string || pair.Value is JsonElement { ValueKind: JsonValueKind.String })
                return true;
            error = $"Argument '{name}' must be a string.";
            return false;
        }

        private static bool IsSafeBranchRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision)
                || revision[0] is '-' or '/' or '.'
                || revision[^1] is '/' or '.'
                || string.Equals(revision, "@", StringComparison.Ordinal)
                || revision.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)
                || revision.Contains("..", StringComparison.Ordinal)
                || revision.Contains("//", StringComparison.Ordinal)
                || revision.Contains("@{", StringComparison.Ordinal)
                || revision.Contains('\\'))
            {
                return false;
            }

            foreach (var character in revision)
            {
                if (char.IsWhiteSpace(character)
                    || char.IsControl(character)
                    || character is '~' or '^' or ':' or '?' or '*' or '[')
                {
                    return false;
                }
            }
            return revision.Split('/').All(component =>
                component.Length > 0
                && component[0] != '.'
                && !component.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryReadObjectId(string? output, out string objectId)
        {
            objectId = string.Empty;
            var lines = (output ?? string.Empty)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 1 || lines[0].Length is not (40 or 64) || !lines[0].All(Uri.IsHexDigit))
                return false;
            objectId = lines[0].ToLowerInvariant();
            return true;
        }

        private static string BoundPatch(string patch, out bool truncated)
        {
            patch ??= string.Empty;
            if (patch.Length <= MaxPatchCharactersPerSection)
            {
                truncated = false;
                return patch;
            }

            truncated = true;
            var marker = "\n" + PatchTruncationMarker + "\n";
            var available = MaxPatchCharactersPerSection - marker.Length;
            var headLength = available / 2;
            var tailLength = available - headLength;
            return patch[..headLength] + marker + patch[^tailLength..];
        }

        private static string BuildSummary(CopilotGitDiffSnapshot snapshot)
        {
            if (!string.Equals(snapshot.Target, "working_tree", StringComparison.Ordinal))
            {
                var targetLabel = string.Equals(snapshot.Target, "base_branch", StringComparison.Ordinal)
                    ? "changes from the selected base branch's merge base to HEAD"
                    : "the selected commit";
                if (!snapshot.OutputComplete)
                    return $"Git returned a bounded, incomplete excerpt for {targetLabel}.";
                return snapshot.HasChanges
                    ? $"Git returned the patch for {targetLabel}."
                    : $"Git found no patch content for {targetLabel}.";
            }

            var scopeLabel = snapshot.Scope switch
            {
                "both" => "staged, unstaged, and untracked",
                "staged" => "staged",
                _ => "unstaged and untracked",
            };
            if (!snapshot.OutputComplete)
                return $"Git returned a bounded, incomplete excerpt of the {scopeLabel} diff.";
            return snapshot.HasChanges
                ? $"Git returned {scopeLabel} changes."
                : $"Git found no {scopeLabel} changes.";
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "InspectGitDiff",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
