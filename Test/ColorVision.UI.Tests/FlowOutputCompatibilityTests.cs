using ColorVision.Engine.Templates.Flow;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class FlowOutputCompatibilityTests
{
    public static TheoryData<string> RuntimeOnlyFlowProperties => new()
    {
        nameof(FlowParam.ResourceId),
        nameof(FlowParam.ResourceCode),
        nameof(FlowParam.FlowKey),
        nameof(FlowParam.TemplateRevision),
        nameof(FlowParam.TemplateContentHash),
        nameof(FlowParam.LoadedContentHash),
        nameof(FlowParam.ExecutionPolicyRevision),
        nameof(FlowParam.ExecutionPolicyHash),
        nameof(FlowParam.ExecutionPolicySnapshotJson),
    };

    [Theory]
    [MemberData(nameof(RuntimeOnlyFlowProperties))]
    public void RuntimeFlowMetadataRemainsExcludedFromTemplateJson(
        string propertyName)
    {
        PropertyInfo property = typeof(FlowParam).GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Missing FlowParam property {propertyName}.");

        Assert.NotNull(property.GetCustomAttribute<
            Newtonsoft.Json.JsonIgnoreAttribute>());
        Assert.NotNull(property.GetCustomAttribute<
            System.Text.Json.Serialization.JsonIgnoreAttribute>());
    }
}
