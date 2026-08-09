using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnRuntimeConfigSnapshot
    {
        private readonly CopilotAgentDefaultsConfig _agentDefaults;
        private readonly IReadOnlyList<CopilotMcpClientServerConfig> _externalMcpServers;

        public CopilotTurnRuntimeConfigSnapshot(
            CopilotAgentDefaultsConfig agentDefaults,
            IEnumerable<CopilotMcpClientServerConfig>? externalMcpServers)
        {
            _agentDefaults = (agentDefaults ?? throw new ArgumentNullException(nameof(agentDefaults))).Clone();
            _externalMcpServers = (externalMcpServers ?? Array.Empty<CopilotMcpClientServerConfig>())
                .Where(server => server != null)
                .Select(server => server.Clone())
                .ToArray();
        }

        public CopilotAgentDefaultsConfig CreateAgentDefaultsSnapshot() => _agentDefaults.Clone();

        public IReadOnlyList<CopilotMcpClientServerConfig> CreateExternalMcpServerSnapshots() =>
            _externalMcpServers.Select(server => server.Clone()).ToArray();

        public CopilotTurnRuntimeConfigSnapshot CreateSnapshot() =>
            new(_agentDefaults, _externalMcpServers);
    }
}
