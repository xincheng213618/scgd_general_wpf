using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotNarrowEvidenceAnswerPolicyTests
{
    [Theory]
    [InlineData("发现 1 条可验证的问题：附件上限是 32，可能不足以满足大型审查。", "the claimed impact is speculative")]
    [InlineData("问题：未观察到取消方法的实现，需要检查并确认是否会阻塞。", "the answer says required evidence was not inspected")]
    [InlineData("One issue: this hard-coded limit may be insufficient for large reviews.", "the claimed impact is speculative")]
    [InlineData("Finding: the implementation was not inspected and needs verification.", "the answer says required evidence was not inspected")]
    public void RejectsSpeculativeOrSelfDeclaredUnverifiedFindings(string answer, string expectedReason)
    {
        Assert.True(CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(
            Request(),
            answer,
            out var reason));
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData("问题：Dispose 在 UI 线程同步调用 CancellationTokenSource.Cancel；注册的回调会在该线程执行，因此阻塞回调会直接冻结关闭流程。")]
    [InlineData("本轮未发现可验证的问题；候选点仍需读取实现，未作为缺陷报告。")]
    [InlineData("No verified finding was established because the relevant implementation could not be inspected.")]
    public void AllowsConcreteFindingsAndExplicitNoFindingAnswers(string answer)
    {
        Assert.False(CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(
            Request(),
            answer,
            out _));
    }

    [Fact]
    public void DoesNotApplyToBroadAuditRequests()
    {
        var request = Request();
        request = new CopilotAgentRequest
        {
            Mode = request.Mode,
            UserText = @"只读全面审计 C:\workspace，列出 1 条问题；不要修改文件。",
            SearchRootPaths = request.SearchRootPaths,
        };

        Assert.False(CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(
            request,
            "发现的问题可能导致故障。",
            out _));
    }

    [Fact]
    public void BuildsAnswerInTheRequestLanguage()
    {
        var chineseAnswer = CopilotNarrowEvidenceAnswerPolicy.BuildNoVerifiedFindingAnswer(Request());
        Assert.StartsWith("本轮收集的证据不足", chineseAnswer, StringComparison.Ordinal);
        Assert.False(CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(Request(), chineseAnswer, out _));

        var englishRequest = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = @"Read-only audit C:\workspace and list one verifiable issue. Do not modify files.",
            SearchRootPaths = [@"C:\workspace"],
        };
        var englishAnswer = CopilotNarrowEvidenceAnswerPolicy.BuildNoVerifiedFindingAnswer(englishRequest);
        Assert.StartsWith("This run did not establish", englishAnswer, StringComparison.Ordinal);
        Assert.False(CopilotNarrowEvidenceAnswerPolicy.TryGetUnsupportedFindingReason(englishRequest, englishAnswer, out _));
    }

    private static CopilotAgentRequest Request()
    {
        return new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = @"只读审计 C:\workspace，列出 1 条可验证的问题；不要修改文件。",
            SearchRootPaths = [@"C:\workspace"],
        };
    }
}
