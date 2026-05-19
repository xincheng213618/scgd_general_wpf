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
                string.IsNullOrWhiteSpace(request.SolutionDirectoryPath) ? null : "已定位解决方案根目录",
                string.IsNullOrWhiteSpace(request.ActiveDocumentPath) ? null : "已定位当前活动内容",
                searchRoots.Length == 0 ? null : $"搜索根 {searchRoots.Length} 个",
            }
            .Where(part => !string.IsNullOrWhiteSpace(part));

            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.SolutionDirectoryPath))
                builder.AppendLine($"解决方案根目录：{request.SolutionDirectoryPath}");

            if (!string.IsNullOrWhiteSpace(request.ActiveDocumentPath))
                builder.AppendLine($"当前活动内容：{request.ActiveDocumentPath}");

            if (searchRoots.Length > 0)
            {
                builder.AppendLine("当前搜索根：");
                foreach (var root in searchRoots)
                    builder.Append("- ").AppendLine(root);
            }

            return Task.FromResult<CopilotContextItem?>(new CopilotContextItem
            {
                Id = "workspace",
                Title = "当前工作区",
                Summary = string.Join("，", summaryParts),
                Content = builder.ToString().TrimEnd(),
            });
        }
    }
}