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
    public sealed record CopilotGitWorkingTreeEntry(
        string Path,
        string IndexStatus,
        string WorkTreeStatus,
        bool IsConflict,
        bool IsUntracked);

    public sealed record CopilotGitWorkingTreeSnapshot(
        string RepositoryRoot,
        string Branch,
        string Head,
        string Upstream,
        int Ahead,
        int Behind,
        bool IsClean,
        bool StatusComplete,
        int ChangedPathCount,
        int StagedCount,
        int UnstagedCount,
        int UntrackedCount,
        int ConflictCount,
        bool EntriesTruncated,
        IReadOnlyList<CopilotGitWorkingTreeEntry> Entries);

    public sealed class CopilotGitWorkingTreeInspectionService
    {
        public const int MaxEntries = 100;
        private const int MaxPathLength = 2_048;
        private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(15);
        private readonly ICopilotShellProcessRunner _runner;
        private readonly Func<string?> _gitExecutableProvider;

        public CopilotGitWorkingTreeInspectionService()
            : this(new CopilotShellProcessRunner(), CopilotGitProcessSupport.FindTrustedGitExecutable)
        {
        }

        public CopilotGitWorkingTreeInspectionService(
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

            var allowedRoots = CopilotGitProcessSupport.GetAllowedRoots(request);
            if (allowedRoots.Count == 0)
                return Failure(CopilotToolFailureKind.NotFound, "No Git-inspectable workspace is available.", "The current request has no existing search or writable root.");

            if (!CopilotGitProcessSupport.TryResolveTargetPath(input.Path, allowedRoots, requireExisting: true, out _, out var targetDirectory, out var containingRoot, out var pathError))
                return Failure(CopilotToolFailureKind.Validation, "The requested Git inspection path is outside the current workspace.", pathError);

            var repositoryRoot = CopilotGitProcessSupport.FindRepositoryRoot(targetDirectory, containingRoot);
            if (string.IsNullOrWhiteSpace(repositoryRoot))
                return Failure(CopilotToolFailureKind.NotFound, "No Git working tree was found in the selected workspace.", "A .git directory or linked-worktree marker was not found within the allowed root.");

            var gitExecutable = CopilotGitProcessSupport.NormalizeFile(_gitExecutableProvider());
            if (string.IsNullOrWhiteSpace(gitExecutable))
                return Failure(CopilotToolFailureKind.NotFound, "Git could not be located.", "A trusted Git for Windows executable was not found.");

            CopilotShellProcessResult processResult;
            try
            {
                processResult = await _runner.RunAsync(new CopilotShellProcessCommand(
                    CopilotShellKind.PowerShell,
                    gitExecutable,
                    BuildArguments(repositoryRoot),
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
                return Failure(
                    CopilotToolFailureKind.Internal,
                    "Git working-tree inspection could not start.",
                    CopilotMcpAuditLogger.RedactText(ex.Message));
            }

            if (processResult.TimedOut)
                return Failure(CopilotToolFailureKind.Transient, "Git working-tree inspection timed out.", "The fixed Git status command exceeded its 15-second timeout.");
            if (processResult.ExitCode != 0)
            {
                var detail = CopilotMcpAuditLogger.RedactText(processResult.StandardError).Trim();
                if (detail.Length > 600)
                    detail = detail[..600] + "...";
                return Failure(
                    CopilotToolFailureKind.Internal,
                    "Git did not return a usable working-tree status.",
                    string.IsNullOrWhiteSpace(detail) ? $"git status exited with code {processResult.ExitCode}." : detail);
            }

            var snapshot = Parse(repositoryRoot, processResult.StandardOutput);
            return new CopilotToolResult
            {
                ToolName = "InspectGitWorkingTree",
                Success = true,
                Summary = BuildSummary(snapshot),
                Content = BuildContent(snapshot),
            };
        }

        private static IReadOnlyList<string> BuildArguments(string repositoryRoot)
        {
            return
            [
                "--no-pager",
                "--no-optional-locks",
                "-c", "core.quotepath=false",
                "-c", "core.fsmonitor=false",
                "-c", "core.untrackedCache=false",
                "-c", "core.worktree=" + repositoryRoot,
                "-c", "status.relativePaths=false",
                "status",
                "--porcelain=v2",
                "--branch",
                "--untracked-files=normal",
                "--no-renames",
                "--ignore-submodules=all",
            ];
        }

        internal static CopilotGitWorkingTreeSnapshot Parse(string repositoryRoot, string output)
        {
            var branch = string.Empty;
            var head = string.Empty;
            var upstream = string.Empty;
            var ahead = 0;
            var behind = 0;
            var changedPathCount = 0;
            var stagedCount = 0;
            var unstagedCount = 0;
            var untrackedCount = 0;
            var conflictCount = 0;
            var entries = new List<CopilotGitWorkingTreeEntry>();
            var outputWasTruncated = (output ?? string.Empty).Contains("...<shell output truncated>...", StringComparison.Ordinal);

            foreach (var rawLine in (output ?? string.Empty).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.StartsWith("# branch.oid ", StringComparison.Ordinal))
                {
                    var value = line[13..].Trim();
                    head = value == "(initial)" ? string.Empty : Sanitize(value, 128);
                    continue;
                }
                if (line.StartsWith("# branch.head ", StringComparison.Ordinal))
                {
                    branch = Sanitize(line[14..], 512);
                    continue;
                }
                if (line.StartsWith("# branch.upstream ", StringComparison.Ordinal))
                {
                    upstream = Sanitize(line[18..], 512);
                    continue;
                }
                if (line.StartsWith("# branch.ab ", StringComparison.Ordinal))
                {
                    ParseAheadBehind(line[12..], out ahead, out behind);
                    continue;
                }

                if (!TryParseEntry(line, out var entry))
                    continue;

                changedPathCount++;
                if (entry.IsConflict)
                    conflictCount++;
                else if (entry.IsUntracked)
                    untrackedCount++;
                else
                {
                    if (entry.IndexStatus != ".")
                        stagedCount++;
                    if (entry.WorkTreeStatus != ".")
                        unstagedCount++;
                }
                if (entries.Count < MaxEntries)
                    entries.Add(entry);
            }

            var statusComplete = !outputWasTruncated;
            return new CopilotGitWorkingTreeSnapshot(
                repositoryRoot,
                branch,
                head,
                upstream,
                ahead,
                behind,
                statusComplete && changedPathCount == 0,
                statusComplete,
                changedPathCount,
                stagedCount,
                unstagedCount,
                untrackedCount,
                conflictCount,
                outputWasTruncated || changedPathCount > entries.Count,
                entries);
        }

        private static bool TryParseEntry(string line, out CopilotGitWorkingTreeEntry entry)
        {
            entry = null!;
            if (line.StartsWith("? ", StringComparison.Ordinal))
            {
                var path = SanitizePath(line[2..]);
                if (string.IsNullOrWhiteSpace(path))
                    return false;
                entry = new CopilotGitWorkingTreeEntry(path, "?", "?", false, true);
                return true;
            }
            if (line.StartsWith("u ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', 11, StringSplitOptions.None);
                if (parts.Length < 11 || parts[1].Length != 2)
                    return false;
                var path = SanitizePath(parts[10]);
                if (string.IsNullOrWhiteSpace(path))
                    return false;
                entry = new CopilotGitWorkingTreeEntry(path, parts[1][0].ToString(), parts[1][1].ToString(), true, false);
                return true;
            }
            if (line.StartsWith("1 ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', 9, StringSplitOptions.None);
                if (parts.Length < 9 || parts[1].Length != 2)
                    return false;
                var path = SanitizePath(parts[8]);
                if (string.IsNullOrWhiteSpace(path))
                    return false;
                entry = new CopilotGitWorkingTreeEntry(path, parts[1][0].ToString(), parts[1][1].ToString(), false, false);
                return true;
            }
            if (line.StartsWith("2 ", StringComparison.Ordinal))
            {
                var parts = line.Split(' ', 10, StringSplitOptions.None);
                if (parts.Length < 10 || parts[1].Length != 2)
                    return false;
                var path = SanitizePath(parts[9].Split('\t')[0]);
                if (string.IsNullOrWhiteSpace(path))
                    return false;
                entry = new CopilotGitWorkingTreeEntry(path, parts[1][0].ToString(), parts[1][1].ToString(), false, false);
                return true;
            }
            return false;
        }

        private static void ParseAheadBehind(string value, out int ahead, out int behind)
        {
            ahead = 0;
            behind = 0;
            foreach (var part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith('+') && int.TryParse(part.AsSpan(1), out var parsedAhead))
                    ahead = Math.Max(0, parsedAhead);
                else if (part.StartsWith('-') && int.TryParse(part.AsSpan(1), out var parsedBehind))
                    behind = Math.Max(0, parsedBehind);
            }
        }

        private static string SanitizePath(string value) => Sanitize(value, MaxPathLength);

        private static string Sanitize(string value, int maxLength)
        {
            var normalized = new string((value ?? string.Empty)
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        private static string BuildSummary(CopilotGitWorkingTreeSnapshot snapshot)
        {
            var branch = string.IsNullOrWhiteSpace(snapshot.Branch) ? "detached or unborn HEAD" : $"branch {snapshot.Branch}";
            if (snapshot.IsClean)
                return $"Git working tree is clean on {branch}.";
            var qualifier = snapshot.StatusComplete ? string.Empty : "at least ";
            return $"Git working tree on {branch} has {qualifier}{snapshot.ChangedPathCount} changed path(s): {snapshot.StagedCount} staged, {snapshot.UnstagedCount} unstaged, {snapshot.UntrackedCount} untracked, {snapshot.ConflictCount} conflicted.";
        }

        private static string BuildContent(CopilotGitWorkingTreeSnapshot snapshot)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["repository_root"] = snapshot.RepositoryRoot,
                ["branch"] = snapshot.Branch,
                ["head"] = snapshot.Head,
                ["upstream"] = snapshot.Upstream,
                ["ahead"] = snapshot.Ahead,
                ["behind"] = snapshot.Behind,
                ["is_clean"] = snapshot.IsClean,
                ["status_complete"] = snapshot.StatusComplete,
                ["changed_path_count"] = snapshot.ChangedPathCount,
                ["staged_count"] = snapshot.StagedCount,
                ["unstaged_count"] = snapshot.UnstagedCount,
                ["untracked_count"] = snapshot.UntrackedCount,
                ["conflict_count"] = snapshot.ConflictCount,
                ["entries_truncated"] = snapshot.EntriesTruncated,
                ["entries"] = snapshot.Entries.Select(entry => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = entry.Path,
                    ["index_status"] = entry.IndexStatus,
                    ["worktree_status"] = entry.WorkTreeStatus,
                    ["is_conflict"] = entry.IsConflict,
                    ["is_untracked"] = entry.IsUntracked,
                }).ToArray(),
            };
            return $"[Git Working Tree Inspection]\nresult_json: {JsonSerializer.Serialize(result)}";
        }

        private static CopilotToolResult Failure(CopilotToolFailureKind kind, string summary, string error)
        {
            return new CopilotToolResult
            {
                ToolName = "InspectGitWorkingTree",
                Success = false,
                FailureKind = kind,
                Summary = summary,
                ErrorMessage = error,
            };
        }
    }
}
