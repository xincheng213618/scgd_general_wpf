using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI;
using FlowEngineLib.Base;
using FlowEngineLib.Logical;
using ST.Library.UI.NodeEditor;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowNodePropertyCatalogTests
{
    [Fact]
    public void DeclaredConditionNamesDriveTheRealDescriptorPreviewWithoutChangingTheCanvas()
    {
        FlowExecutionEditingTests.WithCopilotGraph((service, editor) =>
        {
            editor.LoadAssembly(typeof(ConditionBranchNode).Assembly);
            var node = new ConditionBranchNode();
            node.Create();
            editor.Nodes.Add(node);
            editor.SetActiveNode(node);
            var nodes = editor.Nodes.Cast<STNode>().ToArray();
            var canvas = editor.GetCanvasData();
            var revision = service.CaptureCopilotFlowSnapshot().Revision;
            var wasModified = editor.IsModified;

            var catalog = service.CaptureCopilotFlowNodeCatalog(nameof(ConditionBranchNode), 100);
            var type = Assert.Single(catalog.NodeTypes, item => item.RuntimeType == typeof(ConditionBranchNode).FullName);
            var source = Assert.Single(type.Properties, item => item.PropertyName == nameof(ConditionBranchNode.ConditionSource));
            Assert.Equal(new[] { "FlowStatus", "DataField" }, source.DeclaredEnumNames);
            Assert.Equal(typeof(STNodePropertyDescriptor).FullName, source.DescriptorType);
            Assert.False(source.IsFlagsEnum);
            Assert.Empty(source.BooleanStringCandidates);
            var comparison = Assert.Single(type.Properties, item => item.PropertyName == nameof(ConditionBranchNode.ConditionOperator));
            Assert.Equal(new[]
            {
                "Exists", "NotExists", "Equal", "NotEqual", "GreaterThan", "GreaterThanOrEqual",
                "LessThan", "LessThanOrEqual", "Contains", "NotContains",
            }, comparison.DeclaredEnumNames);
            var status = Assert.Single(type.Properties, item => item.PropertyName == nameof(ConditionBranchNode.ExpectedStatus));
            Assert.Equal(Enum.GetNames<StatusTypeEnum>(), status.DeclaredEnumNames);
            Assert.Contains("Runing", status.DeclaredEnumNames);
            Assert.DoesNotContain("Running", status.DeclaredEnumNames);

            var preview = service.PreviewCopilotFlowNodePropertyChange(node.Guid.ToString(), source.PropertyName,
                Assert.Single(source.DeclaredEnumNames, name => name == "DataField"), revision);
            Assert.Equal(nameof(ConditionBranchNode.ConditionSource), preview.PropertyName);
            Assert.Equal("FlowStatus", preview.OldValue);
            Assert.Equal("DataField", preview.NewValue);
            Assert.Equal(FlowConditionSource.FlowStatus, node.ConditionSource);
            Assert.Throws<InvalidOperationException>(() => service.PreviewCopilotFlowNodePropertyChange(
                node.Guid.ToString(), source.PropertyName, "datafield", revision));

            Assert.Equal(nodes, editor.Nodes.Cast<STNode>().ToArray());
            Assert.Same(node, editor.ActiveNode);
            Assert.Equal(FlowConditionSource.FlowStatus, node.ConditionSource);
            Assert.Equal(canvas, editor.GetCanvasData());
            Assert.Equal(revision, service.CaptureCopilotFlowSnapshot().Revision);
            Assert.Equal(wasModified, editor.IsModified);
        });
    }

    [Fact]
    public void RealBooleanPropertyPublishesCanonicalDefaultDescriptorCandidates()
    {
        FlowExecutionEditingTests.WithCopilotGraph((service, editor) =>
        {
            editor.LoadAssembly(typeof(LocalCleanupNode).Assembly);
            var catalog = service.CaptureCopilotFlowNodeCatalog(nameof(LocalCleanupNode), 100);
            var type = Assert.Single(catalog.NodeTypes, item => item.RuntimeType == typeof(LocalCleanupNode).FullName);
            var property = Assert.Single(type.Properties, item => item.PropertyName == nameof(LocalCleanupNode.IncludeSubfolders));

            Assert.Equal(typeof(bool).FullName, property.DataType);
            Assert.Equal(typeof(STNodePropertyDescriptor).FullName, property.DescriptorType);
            Assert.Equal(new[] { "True", "False" }, property.BooleanStringCandidates);
            Assert.Empty(property.DeclaredEnumNames);
            Assert.False(property.IsFlagsEnum);
            Assert.True(property.IsWritable);
        });
    }

    [Fact]
    public void SchemaMetadataPreservesFlagsAliasesNullableAndCustomDescriptorsWithoutReadingValues()
    {
        MetadataProbe.ConstructorCalls = 0;
        MetadataProbe.GetterCalls = 0;
        MetadataProbe.SetterCalls = 0;
        ThrowingDescriptor.ConstructorCalls = 0;
        // Keep the probe out of the process-wide node registry. This checks schema reflection only;
        // the full catalog retains its existing node construction for titles and device defaults.
        var method = typeof(FlowCopilotService).GetMethod("BuildNodePropertySchemas", BindingFlags.NonPublic | BindingFlags.Static)!;
        var properties = Assert.IsAssignableFrom<IReadOnlyList<CopilotFlowNodePropertySchemaSnapshot>>(
            method.Invoke(null, new object[] { typeof(MetadataProbe) }));

        var flags = Assert.Single(properties, item => item.PropertyName == nameof(MetadataProbe.Flags));
        Assert.Equal(Enum.GetNames<ProbeFlags>(), flags.DeclaredEnumNames);
        Assert.Contains("Read", flags.DeclaredEnumNames);
        Assert.Contains("ReadAlias", flags.DeclaredEnumNames);
        Assert.Equal(5, flags.DeclaredEnumNames.Count);
        Assert.DoesNotContain(flags.DeclaredEnumNames, name => name.Contains(','));
        Assert.True(flags.IsFlagsEnum);
        Assert.Empty(flags.BooleanStringCandidates);
        var customEnum = Assert.Single(properties, item => item.PropertyName == nameof(MetadataProbe.CustomFlags));
        Assert.Equal(flags.DeclaredEnumNames, customEnum.DeclaredEnumNames);
        Assert.True(customEnum.IsFlagsEnum);
        Assert.Equal(typeof(ThrowingDescriptor).FullName, customEnum.DescriptorType);
        Assert.Empty(customEnum.BooleanStringCandidates);
        var customBoolean = Assert.Single(properties, item => item.PropertyName == nameof(MetadataProbe.CustomBoolean));
        Assert.Equal(typeof(ThrowingDescriptor).FullName, customBoolean.DescriptorType);
        Assert.Empty(customBoolean.BooleanStringCandidates);
        Assert.Empty(customBoolean.DeclaredEnumNames);
        foreach (var name in new[] { nameof(MetadataProbe.NullableFlags), nameof(MetadataProbe.NullableBoolean) })
        {
            var nullable = Assert.Single(properties, item => item.PropertyName == name);
            Assert.Empty(nullable.DeclaredEnumNames);
            Assert.Empty(nullable.BooleanStringCandidates);
            Assert.False(nullable.IsFlagsEnum);
            Assert.Equal(typeof(STNodePropertyDescriptor).FullName, nullable.DescriptorType);
        }
        Assert.Equal(typeof(ProbeFlags?).FullName, properties.Single(item => item.PropertyName == nameof(MetadataProbe.NullableFlags)).DataType);
        Assert.Equal(typeof(bool?).FullName, properties.Single(item => item.PropertyName == nameof(MetadataProbe.NullableBoolean)).DataType);
        Assert.Equal(0, MetadataProbe.ConstructorCalls);
        Assert.Equal(0, MetadataProbe.GetterCalls);
        Assert.Equal(0, MetadataProbe.SetterCalls);
        Assert.Equal(0, ThrowingDescriptor.ConstructorCalls);
    }

    [Flags]
    public enum ProbeFlags { None = 0, Read = 1, ReadAlias = 1, Write = 2, All = -1 }

    public sealed class MetadataProbe
    {
        public static int ConstructorCalls;
        public static int GetterCalls;
        public static int SetterCalls;

        public MetadataProbe() { ConstructorCalls++; throw new InvalidOperationException("Metadata must not construct an instance."); }

        [STNodeProperty("Flags", "Declared flag names and aliases")]
        public ProbeFlags Flags { get => Read<ProbeFlags>(); set => Write(); }

        [STNodeProperty("Custom flags", "The CLR names do not establish the descriptor string syntax", DescriptorType = typeof(ThrowingDescriptor))]
        public ProbeFlags CustomFlags { get => Read<ProbeFlags>(); set => Write(); }

        [STNodeProperty("Custom bool", "No default Boolean conversion guarantee", DescriptorType = typeof(ThrowingDescriptor))]
        public bool CustomBoolean { get => Read<bool>(); set => Write(); }

        [STNodeProperty("Nullable flags", "Do not unwrap the declaration into a default parser guarantee")]
        public ProbeFlags? NullableFlags { get => Read<ProbeFlags?>(); set => Write(); }

        [STNodeProperty("Nullable bool", "Do not infer nullable Boolean literals")]
        public bool? NullableBoolean { get => Read<bool?>(); set => Write(); }

        private static T Read<T>() { GetterCalls++; throw new InvalidOperationException("Metadata must not read values."); }
        private static void Write() { SetterCalls++; throw new InvalidOperationException("Metadata must not set values."); }
    }

    public sealed class ThrowingDescriptor : STNodePropertyDescriptor
    {
        public static int ConstructorCalls;
        public ThrowingDescriptor() { ConstructorCalls++; throw new InvalidOperationException("Metadata must not construct a descriptor."); }
    }
}
