using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotReviewProjectInstructionContext
    {
        private readonly object _sync = new();
        private readonly string _globalInstructionRootPath;
        private readonly CopilotProjectInstructionDiscoveryOptions _discoveryOptions;
        private readonly HashSet<string> _deliveredDocumentPaths;
        private bool _incompleteChangedPathWarningDelivered;

        public CopilotReviewProjectInstructionContext(
            string? globalInstructionRootPath,
            CopilotProjectInstructionDiscoveryOptions discoveryOptions,
            IEnumerable<CopilotProjectInstructionDocument>? initialDocuments)
        {
            _globalInstructionRootPath = globalInstructionRootPath ?? string.Empty;
            _discoveryOptions = discoveryOptions
                ?? throw new ArgumentNullException(nameof(discoveryOptions));
            _deliveredDocumentPaths = new HashSet<string>(
                (initialDocuments ?? Array.Empty<CopilotProjectInstructionDocument>())
                    .Where(document => document?.IsStructurallyValid() == true)
                    .Select(document => document.Path),
                StringComparer.OrdinalIgnoreCase);
        }

        public string BuildAdditionalPromptBlock(
            IReadOnlyList<string> trustedProjectRootPaths,
            CopilotGitDiffSnapshot gitDiff)
        {
            ArgumentNullException.ThrowIfNull(gitDiff);
            if (!CopilotWorkspaceSearchSupport.IsPathWithinRoots(
                    gitDiff.RepositoryRoot,
                    trustedProjectRootPaths))
            {
                return string.Empty;
            }

            var targetPaths = gitDiff.ChangedPaths
                .Select(path => TryResolveTargetPath(gitDiff.RepositoryRoot, path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var discovered = targetPaths.Length == 0
                ? Array.Empty<CopilotProjectInstructionDocument>()
                : CopilotAgentProjectInstructions.DiscoverWithGlobal(
                    trustedProjectRootPaths,
                    activeDocumentPath: null,
                    targetPaths,
                    _globalInstructionRootPath,
                    _discoveryOptions).ToArray();
            CopilotProjectInstructionDocument[] additionalDocuments;
            bool includeIncompletePathWarning;
            lock (_sync)
            {
                additionalDocuments = discovered
                    .Where(document => _deliveredDocumentPaths.Add(document.Path))
                    .ToArray();
                includeIncompletePathWarning = !gitDiff.ChangedPathsComplete
                    && !_incompleteChangedPathWarningDelivered;
                _incompleteChangedPathWarningDelivered |= includeIncompletePathWarning;
            }
            if (additionalDocuments.Length == 0)
            {
                return includeIncompletePathWarning
                    ? BuildIncompletePathWarning()
                    : string.Empty;
            }

            var promptBlock = CopilotAgentProjectInstructions.BuildPromptBlock(additionalDocuments);
            if (promptBlock.Length == 0)
                return string.Empty;

            var completeness = gitDiff.ChangedPathsComplete
                ? "The built-in Git review tool returned a complete changed-path list for this bounded result."
                : "The built-in Git review tool returned an incomplete changed-path list; these added instructions cover only the returned paths.";
            return "# Additional scoped project instructions for reviewed paths"
                + Environment.NewLine
                + completeness
                + Environment.NewLine
                + promptBlock;
        }

        private static string BuildIncompletePathWarning() =>
            "# Scoped project-instruction coverage warning"
            + Environment.NewLine
            + "The built-in Git review tool returned an incomplete changed-path list. "
            + "Path-scoped project instructions may therefore also be incomplete; "
            + "do not treat the absence of an added rule as proof that no rule applies.";

        private static string? TryResolveTargetPath(
            string repositoryRoot,
            string repositoryRelativePath)
        {
            if (!CopilotGitDiffResultProtocol.IsChangedPathStructurallyValid(repositoryRelativePath))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(
                    repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar),
                    repositoryRoot);
                return CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [repositoryRoot])
                    ? fullPath
                    : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
