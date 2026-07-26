using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotSubagentEvidencePolicy
    {
        internal static IReadOnlyList<string> FindUnobservedWorkspaceFileCitations(
            CopilotSubagentRoleDescriptor role,
            IReadOnlyList<CopilotAgentStepRecord> steps,
            string answer)
        {
            ArgumentNullException.ThrowIfNull(role);
            if (role.ContextScope != CopilotSubagentContextScope.WorkspaceReadOnly
                || !role.ReadCapabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile)
                || string.IsNullOrWhiteSpace(answer))
            {
                return Array.Empty<string>();
            }

            var successfullyReadPaths = (steps ?? Array.Empty<CopilotAgentStepRecord>())
                .Where(step =>
                    step?.Observation?.Success == true
                    && string.Equals(step.ToolCall?.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase))
                .SelectMany(step => step.Observation.SuccessfullyReadLocalFilePaths ?? Array.Empty<string>())
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(answer)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                .Where(path => !successfullyReadPaths.Contains(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim().Trim('`', '*', '_'));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
