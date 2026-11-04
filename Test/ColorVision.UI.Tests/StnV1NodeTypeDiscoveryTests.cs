using ColorVision.Engine.FlowProcessing.Compilation;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace ColorVision.UI.Tests;

public sealed class StnV1NodeTypeDiscoveryTests
{
    private static readonly Func<Assembly, Type[]> ReadTypes = typeof(StnV1NeutralCodec)
        .GetMethod("GetLoadableNodeTypes", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<Assembly, Type[]>>();
    private static readonly Func<string, string, Type> Resolve = typeof(StnV1NeutralCodec)
        .GetMethod("ResolveNodeType", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<string, string, Type>>();

    // Missing dependencies can recover after discovery has already examined an assembly.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedAndPartialScansRetryUntilACompleteSnapshotIsAvailable(bool partialFailure)
    {
        var assembly = new RetryingAssembly(partialFailure);

        Assert.Equal(partialFailure ? new[] { typeof(FirstNode) } : [], ReadTypes(assembly));
        Type[] complete = ReadTypes(assembly);

        Assert.Equal(new[] { typeof(FirstNode), typeof(SecondNode) }, complete);
        Assert.Same(complete, ReadTypes(assembly));
        Assert.Equal(2, assembly.Attempts);
    }

    [Fact]
    public void MetadataFailuresDuringFilteringPropagateAndDoNotBecomeCached()
    {
        var assembly = new FilteringFailureAssembly();
        Assert.Throws<InvalidOperationException>(() => ReadTypes(assembly));
        Assert.Equal(new[] { typeof(FirstNode) }, ReadTypes(assembly));
        Assert.Equal(2, assembly.Attempts);
    }

    [Fact]
    public void AnUnknownNodeCanResolveAfterItsAssemblyLoads()
    {
        string assemblyName = "LateCodecNode_" + Guid.NewGuid().ToString("N");
        string nodeName = assemblyName + ".Node_" + Guid.NewGuid().ToString("N");
        string model = assemblyName + ".dll|" + nodeName;
        string missingGuid = Guid.NewGuid().ToString();
        Assert.Equal(FlowCompilationError.UnknownNodeType,
            Assert.Throws<FlowCompilationException>(() => Resolve(missingGuid, model)).Error);

        // A persisted assembly is not dynamic, matching an optional plugin loaded after startup.
        var builder = new PersistedAssemblyBuilder(new AssemblyName(assemblyName), typeof(object).Assembly);
        TypeBuilder node = builder.DefineDynamicModule(assemblyName + ".dll")
            .DefineType(nodeName, TypeAttributes.Public, typeof(STNode));
        node.DefineDefaultConstructor(MethodAttributes.Public);
        node.CreateType();
        using var image = new MemoryStream();
        builder.Save(image);
        Assembly loaded = Assembly.Load(image.ToArray());
        Type expected = loaded.GetType(nodeName, throwOnError: true)!;

        Assert.False(loaded.IsDynamic);
        Assert.Same(expected, Resolve(missingGuid, model));
        Assert.Same(expected, Resolve(expected.GUID.ToString().ToUpperInvariant(), "unrelated.dll|Node"));
    }

    private sealed class RetryingAssembly(bool partialFailure) : Assembly
    {
        internal int Attempts { get; private set; }

        public override Type[] GetTypes()
        {
            Attempts++;
            if (Attempts == 1)
            {
                if (partialFailure)
                    throw new ReflectionTypeLoadException([typeof(FirstNode), null!], [new FileNotFoundException("Optional dependency is unavailable.")]);
                throw new FileNotFoundException("Optional dependency is unavailable.");
            }
            return [typeof(FirstNode), typeof(string), typeof(SecondNode), typeof(STNode)];
        }
    }

    private sealed class FilteringFailureAssembly : Assembly
    {
        internal int Attempts { get; private set; }
        public override Type[] GetTypes() => ++Attempts == 1 ? [new BrokenMetadataType()] : [typeof(FirstNode)];
    }

    private sealed class BrokenMetadataType() : TypeDelegator(typeof(FirstNode))
    {
        protected override TypeAttributes GetAttributeFlagsImpl() => throw new InvalidOperationException("Type metadata is unavailable.");
    }

    public sealed class FirstNode : STNode { }
    public sealed class SecondNode : STNode { }
}
