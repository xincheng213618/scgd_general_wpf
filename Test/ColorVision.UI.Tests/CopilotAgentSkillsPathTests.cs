using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSkillsPathTests
{
    [Fact]
    public void SkillRootWithReparsePointParentIsNotTrusted()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-skill-path-{Guid.NewGuid():N}");
        var workspace = Path.Combine(root, "Workspace");
        var outsideAgents = Path.Combine(root, "OutsideAgents");
        var linkedAgents = Path.Combine(workspace, ".agents");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(Path.Combine(outsideAgents, "skills"));
        Directory.CreateSymbolicLink(linkedAgents, outsideAgents);

        try
        {
            var request = new CopilotAgentRequest
            {
                TrustedProjectRootPaths = [workspace],
            };

            var skillRoots = CopilotAgentSkills.ResolveSearchPaths(request, Path.Combine(root, "Application"));

            Assert.DoesNotContain(
                Path.Combine(linkedAgents, "skills"),
                skillRoots,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(linkedAgents, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }
}
