using ColorVision.Engine.FlowProcessing.Artifacts;
using ColorVision.Engine.Templates.Flow;
using System.Reflection;
using System.Text;

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

    [Fact]
    public void ArtifactManifestCanonicalJsonShapeRemainsStable()
    {
        var manifest = new FlowArtifactManifest(
            FormatVersion: 1,
            FlowKey: "flow-key",
            Revision: "r1",
            SourceHash: "source",
            SubflowHash: "subflow",
            PolicyHash: "policy",
            SemanticHash: "semantic",
            LayoutHash: "layout",
            DefinitionHash: "definition",
            DependencyHash: "dependency",
            CompiledStnHash: "compiled",
            EffectivePolicyHash: "effective",
            CompilationMapHash: "map",
            CompilerHash: "compiler-hash",
            ArtifactHash: "artifact",
            Compiler: new FlowArtifactCompilerDescriptor(
                "compiler",
                "1.0",
                1,
                2,
                3,
                4));

        string json = Encoding.UTF8.GetString(
            FlowArtifactSerializer.SerializeManifest(manifest));

        Assert.Equal(
            "{\"formatVersion\":1,\"flowKey\":\"flow-key\","
            + "\"revision\":\"r1\",\"sourceHash\":\"source\","
            + "\"subflowHash\":\"subflow\",\"policyHash\":\"policy\","
            + "\"semanticHash\":\"semantic\",\"layoutHash\":\"layout\","
            + "\"definitionHash\":\"definition\","
            + "\"dependencyHash\":\"dependency\","
            + "\"compiledStnHash\":\"compiled\","
            + "\"effectivePolicyHash\":\"effective\","
            + "\"compilationMapHash\":\"map\","
            + "\"compilerHash\":\"compiler-hash\","
            + "\"artifactHash\":\"artifact\","
            + "\"compiler\":{\"name\":\"compiler\","
            + "\"version\":\"1.0\",\"stndVersion\":1,"
            + "\"maximumDepth\":2,\"maximumNodeCount\":3,"
            + "\"maximumConnectionCount\":4}}",
            json);
    }
}
