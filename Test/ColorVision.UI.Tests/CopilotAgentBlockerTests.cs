using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentBlockerTests
{
    [Fact]
    public void PersistedMessageFiltersBlockersWithNullTextFields()
    {
        var message = JsonConvert.DeserializeObject<CopilotChatMessage>(
            """
            {
              "AgentBlockers": [
                {
                  "Kind": 2,
                  "Code": "tool_failure",
                  "Summary": null,
                  "ToolName": null
                }
              ]
            }
            """)!;

        Assert.Empty(message.AgentBlockers);
    }
}
