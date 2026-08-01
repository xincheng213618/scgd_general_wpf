using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotWorkspaceVerification
    {
        public static string BuildPrompt(string? focusInstructions)
        {
            var prompt = new StringBuilder(
                "Verify the changes in the current uncommitted workspace. "
                + "Do not modify files or apply fixes. "
                + "Inspect the Git working tree and relevant staged or unstaged diff, then run at least one suitable bounded dotnet build or test through RunWorkspaceValidation after native approval. "
                + "Judge whether the changes satisfy their apparent request and report concrete evidence, findings, and residual gaps. "
                + "End with VERDICT: PASS only when the inspected changes are correct and the collected validation succeeded; otherwise end with VERDICT: FAIL.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                prompt.Append(" Focus: ").Append(focusInstructions.Trim());
            return prompt.ToString();
        }
    }
}
