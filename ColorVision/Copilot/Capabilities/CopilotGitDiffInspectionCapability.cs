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
        IReadOnlyList<CopilotGitDiffSection> Sections);

    public sealed class CopilotGitDiffInspectionService
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
            foreach (var sectionScope in GetSectionScopes(scope))
            {
                var sectionResult = await ExecuteSectionAsync(gitExecutable, repositoryRoot, pathFilter, sectionScope, cancellationToken);
                if (sectionResult.Failure != null)
                    return sectionResult.Failure;
                sections.Add(sectionResult.Section!);
            }

            var snapshot = new CopilotGitDiffSnapshot(
                repositoryRoot,
                scope,
                pathFilter,
                sections.Any(section => section.HasChanges),
                sections.All(section => section.OutputComplete),
                sections.Any(section => section.PatchTruncated),
                sections);
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
            CancellationToken cancellationToken)
        {
            CopilotShellProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    CopilotShellKind.PowerShell,
                    gitExecutable,
                    BuildArguments(repositoryRoot, pathFilter, scope),
                    repositoryRoot,
                    ExecutionTimeout)
                {
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
                    "Git diff inspection could not start.",
                    CopilotMcpAuditLogger.RedactText(ex.Message)));
            }

            if (processResult.TimedOut)
            {
                return (null, Failure(
                    CopilotToolFailureKind.Transient,
                    "Git diff inspection timed out.",
                    $"The fixed {scope} Git diff command exceeded its 20-second timeout."));
            }
            if (processResult.ExitCode != 0)
            {
                var detail = CopilotMcpAuditLogger.RedactText(processResult.StandardError).Trim();
                if (detail.Length > 600)
                    detail = detail[..600] + "...";
                return (null, Failure(
                    CopilotToolFailureKind.Internal,
                    $"Git did not return a usable {scope} diff.",
                    string.IsNullOrWhiteSpace(detail) ? $"git diff exited with code {processResult.ExitCode}." : detail));
            }

            var rawPatch = processResult.StandardOutput ?? string.Empty;
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

        private static List<string> BuildArguments(string repositoryRoot, string pathFilter, string scope)
        {
            var arguments = new List<string>
            {
                "--no-pager",
                "--no-optional-locks",
                "-c", "core.quotepath=false",
                "-c", "core.fsmonitor=false",
                "-c", "core.untrackedCache=false",
                "-c", "core.worktree=" + repositoryRoot,
                "diff",
                "--no-ext-diff",
                "--no-textconv",
                "--no-renames",
                "--ignore-submodules=all",
                "--no-color",
                "--unified=3",
            };
            if (string.Equals(scope, "staged", StringComparison.Ordinal))
                arguments.Add("--cached");
            arguments.Add("--");
            if (!string.IsNullOrWhiteSpace(pathFilter))
                arguments.Add(pathFilter);
            return arguments;
        }

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
