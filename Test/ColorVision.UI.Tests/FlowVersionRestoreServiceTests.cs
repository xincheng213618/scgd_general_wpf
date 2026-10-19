using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates.Flow.Search;
using FlowEngineLib;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class FlowVersionRestoreServiceTests
{
    private const string FlowKey =
        "flow:11111111111111111111111111111111";

    [Fact]
    public void RepeatedHistoricalRestoresReloadRepeatMembershipCountAndParameters()
    {
        StaTest.Run(() =>
        {
            using var editor = new STNodeEditor { EnableHistory = true };
            using var control = new FlowEngineControl(editor, false, new FlowNodeManager());
            var node = new LocalSmuNode();
            node.Create();
            editor.Nodes.Add(node);
            var catalog = new FlowCatalogService(new InMemoryFlowRevisionStore(), new InMemoryFlowNodeSearchIndex());
            FlowRevision Record(byte[] canvas)
            {
                var projection = new FlowCanvasCatalogBuilder().Build(canvas);
                return catalog.RecordEditorSave(FlowKey, canvas, projection.SemanticDocument, projection.SearchDocuments);
            }
            FlowRevision plain = Record(editor.GetCanvasData());
            node.SetRepeatRegion("historical-repeat", 3, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(LocalSmuNode.MeasureValue)] = [-1.2, -1.3, -1.4],
                [nameof(LocalSmuNode.LimitValue)] = [5, 6, 7],
            });
            FlowRevision repeated = Record(editor.GetCanvasData());
            var flowParam = new FlowParam { FlowKey = FlowKey, DataBase64 = Convert.ToBase64String(repeated.FullSnapshot) };
            var service = new FlowVersionRestoreService((param, _) =>
            {
                FlowRevision saved = Record(Convert.FromBase64String(param.DataBase64));
                param.LoadedContentHash = saved.BinaryHash;
                param.TemplateRevision = saved.Revision;
            });
            foreach (FlowRevision target in new[] { plain, repeated, plain, repeated })
            {
                Assert.True(service.Restore(new FlowVersionRestoreRequest(flowParam, target, flowParam.LoadedContentHash)).Succeeded);
                control.LoadFromBase64(flowParam.DataBase64);
                LocalSmuNode loaded = Assert.Single(editor.Nodes.OfType<LocalSmuNode>());
                bool hasRepeat = ReferenceEquals(target, repeated);
                Assert.Equal(hasRepeat ? "historical-repeat" : string.Empty, loaded.RepeatRegionId);
                Assert.Equal(hasRepeat ? 3 : 1, loaded.RepeatCount);
                Assert.Equal(hasRepeat ? 1 : 0, loaded.RepeatMemberCount);
                Assert.Equal(hasRepeat ? 1 : 0, editor.GetNodeGroups().Length);
                if (hasRepeat)
                {
                    Assert.Equal(new double[] { -1.2, -1.3, -1.4 }, loaded.RepeatParameterValues[nameof(LocalSmuNode.MeasureValue)]);
                    Assert.Equal(new double[] { 5, 6, 7 }, loaded.RepeatParameterValues[nameof(LocalSmuNode.LimitValue)]);
                }
                else Assert.Empty(loaded.RepeatParameterValues);
                Assert.False(editor.IsModified);
            }
        });
    }

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
