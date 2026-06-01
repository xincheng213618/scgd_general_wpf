using ColorVision.UI;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotWorkspaceContextProvider : ICopilotContextProvider
    {
        public int Order => 10;

        public bool CanProvide(CopilotContextScope scope)
        {
            return scope == CopilotContextScope.Agent || scope == CopilotContextScope.Diagnose;
        }

        public Task<CopilotContextItem?> CaptureAsync(CopilotContextRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var searchRoots = (request.SearchRootPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();

            if (string.IsNullOrWhiteSpace(request.SolutionDirectoryPath)
                && string.IsNullOrWhiteSpace(request.ActiveDocumentPath)
                && searchRoots.Length == 0)
            {
                return Task.FromResult<CopilotContextItem?>(null);
            }

            var summaryParts = new[]
            {
                string.IsNullOrWhiteSpace(request.SolutionDirectoryPath) ? null : "solution root detected",
                string.IsNullOrWhiteSpace(request.ActiveDocumentPath) ? null : "active content detected",
                searchRoots.Length == 0 ? null : $"search roots {searchRoots.Length}",
            }
            .Where(part => !string.IsNullOrWhiteSpace(part));

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.SolutionDirectoryPath))
                builder.AppendLine($"Solution root: {request.SolutionDirectoryPath}");

            if (!string.IsNullOrWhiteSpace(request.ActiveDocumentPath))
                builder.AppendLine($"Active content: {request.ActiveDocumentPath}");

            if (searchRoots.Length > 0)
            {
                builder.AppendLine("Current search roots:");
                foreach (var root in searchRoots)
                    builder.Append("- ").AppendLine(root);
            }

            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = "workspace",
                Title = "Current workspace",
                Summary = string.Join(", ", summaryParts),
                Content = builder.ToString().TrimEnd(),
            });
        }
    }
}