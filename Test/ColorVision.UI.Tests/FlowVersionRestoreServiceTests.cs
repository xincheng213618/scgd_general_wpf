using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;

namespace ColorVision.UI.Tests;

public sealed class FlowVersionRestoreServiceTests
{
    private const string FlowKey =
        "flow:11111111111111111111111111111111";

    [Fact]
    public void RestoreReturnsCommittedStateForUiRefresh()
    {
        var flowParam = new FlowParam
        {
            DataBase64 = "before",
            FlowKey = FlowKey,
            TemplateRevision = 3,
        };
        var revision = new FlowRevision
        {
            FlowKey = FlowKey,
            Revision = 8,
            FullSnapshot = [1, 2, 3],
            SemanticDocument = new FlowSemanticDocument(),
        };
        FlowTemplateSaveCondition? receivedCondition = null;
        var service = new FlowVersionRestoreService(
            (param, condition) =>
            {
                receivedCondition = condition;
                param.LoadedContentHash = "committed-hash";
                param.TemplateRevision = 9;
            });

        FlowVersionRestoreResult result = service.Restore(
            new FlowVersionRestoreRequest(
                flowParam,
                revision,
                ExpectedContentHash: "loaded-hash"));

        Assert.True(result.Succeeded);
        Assert.Equal("AQID", flowParam.DataBase64);
        Assert.Equal("loaded-hash", receivedCondition?.ExpectedContentHash);
        Assert.Equal("committed-hash", result.LoadedContentHash);
        Assert.True(result.VersionCatalogUpdated);
    }

    [Fact]
    public void RestoreRestoresTemplateStateWhenSaveFails()
    {
        var flowParam = new FlowParam
        {
            DataBase64 = "before",
            FlowKey = FlowKey,
            TemplateRevision = 3,
        };
        var revision = new FlowRevision
        {
            FlowKey = FlowKey,
            Revision = 8,
            FullSnapshot = [1, 2, 3],
            SemanticDocument = new FlowSemanticDocument(),
        };
        var service = new FlowVersionRestoreService(
            (_, _) => throw new InvalidOperationException(
                "template-save-failed"));

        FlowVersionRestoreResult result = service.Restore(
            new FlowVersionRestoreRequest(
                flowParam,
                revision,
                ExpectedContentHash: "loaded-hash"));

        Assert.False(result.Succeeded);
        Assert.Equal("template-save-failed", result.FailureMessage);
        Assert.Equal("before", flowParam.DataBase64);
        Assert.Equal(3, flowParam.TemplateRevision);
    }
}
