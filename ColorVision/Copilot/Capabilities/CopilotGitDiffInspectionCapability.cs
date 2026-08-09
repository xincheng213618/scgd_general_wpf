using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
            }

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
            };
            return new CopilotToolResult
            {
                ToolName = "InspectGitDiff",
                Success = true,
                Summary = BuildSummary(snapshot),
                Content = BuildContent(snapshot),
            };
        }

        private async Task<(CopilotGitDiffSection? Section, CopilotToolResult? Failure)> ExecuteSectionAsync(
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
                return (null, execution.Failure);

            var rawPatch = execution.Result!.StandardOutput ?? string.Empty;
            var runnerTruncated = rawPatch.Contains(ShellTruncationMarker, StringComparison.Ordinal);
            var boundedPatch = BoundPatch(rawPatch, out var serviceTruncated);
            var truncated = runnerTruncated || serviceTruncated;
            return (new CopilotGitDiffSection(
                scope,
                !string.IsNullOrWhiteSpace(rawPatch),
                !truncated,
                truncated,
                boundedPatch), null);
        }

        private async Task<(CopilotGitDiffSection? Section, string ResolvedRevision, CopilotToolResult? Failure)> ExecuteRevisionTargetAsync(
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
                return (null, string.Empty, resolution.Failure);
            if (!TryReadObjectId(resolution.Result!.StandardOutput, out var resolvedRevision))
            {
                return (null, string.Empty, Failure(
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
                    return (null, string.Empty, mergeBase.Failure);
                if (!TryReadObjectId(mergeBase.Result!.StandardOutput, out comparisonRevision))
                {
                    return (null, string.Empty, Failure(
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
                return (null, string.Empty, patch.Failure);

            var rawPatch = patch.Result!.StandardOutput ?? string.Empty;
            var runnerTruncated = rawPatch.Contains(ShellTruncationMarker, StringComparison.Ordinal);
            var boundedPatch = BoundPatch(rawPatch, out var serviceTruncated);
            var truncated = runnerTruncated || serviceTruncated;
            return (new CopilotGitDiffSection(
                target,
                !string.IsNullOrWhiteSpace(rawPatch),
                !truncated,
                truncated,
                boundedPatch), resolvedRevision, null);
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
                "both" => "staged and unstaged",
                "staged" => "staged",
                _ => "unstaged",
            };
            if (!snapshot.OutputComplete)
                return $"Git returned a bounded, incomplete excerpt of the {scopeLabel} diff.";
            return snapshot.HasChanges
                ? $"Git returned {scopeLabel} changes."
                : $"Git found no {scopeLabel} changes.";
        }

        private static string BuildContent(CopilotGitDiffSnapshot snapshot)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["repository_root"] = snapshot.RepositoryRoot,
                ["target"] = snapshot.Target,
                ["revision"] = snapshot.Revision,
                ["resolved_revision"] = snapshot.ResolvedRevision,
                ["scope"] = snapshot.Scope,
                ["path_filter"] = snapshot.PathFilter,
                ["has_changes"] = snapshot.HasChanges,
                ["output_complete"] = snapshot.OutputComplete,
                ["patch_truncated"] = snapshot.PatchTruncated,
                ["sections"] = snapshot.Sections.Select(section => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["scope"] = section.Scope,
                    ["has_changes"] = section.HasChanges,
                    ["output_complete"] = section.OutputComplete,
                    ["patch_truncated"] = section.PatchTruncated,
                    ["patch"] = section.Patch,
                }).ToArray(),
            };
            return $"[Git Diff Inspection]\nresult_json: {JsonSerializer.Serialize(result)}";
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
