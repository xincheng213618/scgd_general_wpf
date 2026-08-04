using ColorVision.Engine.Templates.Flow;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateIdentityTests
{
    [Fact]
    public void ExplicitWindowSaveConditionDoesNotUseSharedBaseline()
    {
        var flowParam = new FlowParam
        {
            LoadedContentHash = new string('b', 64),
        };
        var condition = new FlowTemplateSaveCondition(
            new string('a', 64));

        string? expected =
            TemplateFlow.ResolveExpectedContentHash(
                flowParam,
                condition);

        Assert.Equal(new string('a', 64), expected);
    }

    [Fact]
    public void GuidResourceCodeSurvivesTemplateAndResourceIdChanges()
    {
        const string code = "de782aa3dfe84fc482825c12bbb2df65";

        string? before = FlowTemplateIdentity.Create(12, 101, code);
        string? afterReorderOrImport = FlowTemplateIdentity.Create(
            99,
            404,
            "DE782AA3-DFE8-4FC4-8282-5C12BBB2DF65");

        Assert.Equal("flow:de782aa3dfe84fc482825c12bbb2df65", before);
        Assert.Equal(before, afterReorderOrImport);
    }

    [Fact]
    public void LegacyResourceIdentitySurvivesTemplateOrderSwap()
    {
        string? before = FlowTemplateIdentity.Create(12, 101, "legacy-name");
        string? afterReorder = FlowTemplateIdentity.Create(99, 101, "legacy-name");

        Assert.Equal("flow-resource:101", before);
        Assert.Equal(before, afterReorder);
    }

    [Fact]
    public void UnsavedFlowFallsBackToTemplateIdentity()
    {
        Assert.Equal(
            "flow-template:12",
            FlowTemplateIdentity.Create(12, null, null));
        Assert.Null(FlowTemplateIdentity.Create(0, null, null));
    }

    [Fact]
    public void RuntimeVersionIdentityStaysOutOfTemplateJson()
    {
        var flow = new FlowParam
        {
            DataBase64 = "U1RORAE=",
            FlowKey = "flow:runtime-only",
            TemplateRevision = 8,
            TemplateContentHash = new string('a', 64),
            LoadedContentHash = new string('c', 64),
        };

        string newtonsoftJson =
            Newtonsoft.Json.JsonConvert.SerializeObject(flow);
        string systemTextJson =
            System.Text.Json.JsonSerializer.Serialize(flow);
        foreach (string json in new[]
                 {
                     newtonsoftJson,
                     systemTextJson,
                 })
        {
            Assert.DoesNotContain("FlowKey", json);
            Assert.DoesNotContain("TemplateRevision", json);
            Assert.DoesNotContain("TemplateContentHash", json);
            Assert.DoesNotContain("LoadedContentHash", json);
        }
    }
}
