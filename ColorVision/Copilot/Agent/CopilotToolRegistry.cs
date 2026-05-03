using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolRegistry
    {
        private readonly IReadOnlyList<ICopilotTool> _tools;

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools)
        {
            _tools = tools?.ToArray() ?? Array.Empty<ICopilotTool>();
        }

        public IReadOnlyList<ICopilotTool> Tools => _tools;

        public IReadOnlyList<ICopilotTool> FindTools(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return _tools
                .Where(tool => tool.CanHandle(request))
                .Take(5)
                .ToArray();
        }

        public static CopilotToolRegistry CreateDefault()
        {
            return new CopilotToolRegistry(new ICopilotTool[]
            {
                new CopilotFetchUrlTool(),
                new CopilotReadAttachedFileTool(),
                new CopilotGetRecentLogTool(),
            });
        }
    }
}