using ColorVision.Engine.FlowProcessing.Integration;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System.Globalization;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowSensitivePropertyTests
{
    [Theory]
    [InlineData(nameof(SensitiveNode.ApiKey))]
    [InlineData(nameof(SensitiveNode.Configuration))]
    [InlineData(nameof(SensitiveNode.AccessSetting))]
    [InlineData(nameof(SensitiveNode.LocalSetting))]
    [InlineData(nameof(SensitiveNode.Authorization))]
    [InlineData(nameof(SensitiveNode.RequestSetting))]
    public void SensitiveClrOrDisplayNamesRejectPreviewAndApplyBeforeReadingCloningOrConverting(string propertyName)
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            var node = new SensitiveNode();
            node.Create();
            editor.Nodes.Add(node);
            string revision = host.CaptureCopilotFlowSnapshot().Revision;
            SensitiveNode.ResetCounters();

            var preview = Assert.Throws<InvalidOperationException>(() => host.PreviewCopilotFlowNodePropertyChange(node.Guid.ToString(), propertyName, "changed", revision));
            var apply = Assert.Throws<InvalidOperationException>(() => host.SetCopilotFlowNodeProperty(node.Guid.ToString(), propertyName, "changed", revision));

            Assert.Contains("sensitive", preview.Message, StringComparison.Ordinal);
            Assert.Contains("sensitive", apply.Message, StringComparison.Ordinal);
            Assert.Equal(0, SensitiveNode.Constructions);
            Assert.Equal(0, SensitiveNode.GetterCalls);
            Assert.Equal(0, SensitiveNode.SetterCalls);
            Assert.Equal(0, SensitiveNode.LoadCalls);
            Assert.Equal(0, SensitiveNode.DescriptorConstructions);
            Assert.Same(node, Assert.Single(editor.Nodes.Cast<STNode>()));
        });
    }

    [Fact]
    public void OrdinaryPropertyKeepsItsTextDescriptorPreviewAndApplyContract()
    {
        FlowExecutionEditingTests.WithCopilotGraph((host, editor) =>
        {
            var node = new SensitiveNode();
            node.Create();
            editor.Nodes.Add(node);
            string revision = host.CaptureCopilotFlowSnapshot().Revision;

            var preview = host.PreviewCopilotFlowNodePropertyChange(node.Guid.ToString(), nameof(SensitiveNode.Threshold), "units:42", revision);

            Assert.Equal("7", preview.OldValue);
            Assert.Equal("42", preview.NewValue);
            Assert.Equal(7, node.Threshold);
            host.SetCopilotFlowNodeProperty(node.Guid.ToString(), nameof(SensitiveNode.Threshold), "units:42", revision);
            Assert.Equal(42, node.Threshold);
        });
    }

    [Fact]
    public void CatalogSensitivityAndReadonlyMetadataUseOnlyReflectionAndMatchTheMutationGuard()
    {
        SensitiveNode.ResetCounters();
        var build = typeof(FlowCopilotService).GetMethod("BuildNodePropertySchemas", BindingFlags.Static | BindingFlags.NonPublic)!;

        var schemas = Assert.IsAssignableFrom<IReadOnlyList<CopilotFlowNodePropertySchemaSnapshot>>(build.Invoke(null, [typeof(SensitiveNode)]));

        foreach (string name in new[] { nameof(SensitiveNode.ApiKey), nameof(SensitiveNode.Configuration), nameof(SensitiveNode.AccessSetting), nameof(SensitiveNode.LocalSetting),
            nameof(SensitiveNode.Authorization), nameof(SensitiveNode.RequestSetting), nameof(SensitiveNode.ReadonlyValue) })
            Assert.False(Assert.Single(schemas, property => property.PropertyName == name).IsWritable);
        Assert.True(Assert.Single(schemas, property => property.PropertyName == nameof(SensitiveNode.Threshold)).IsWritable);
        Assert.DoesNotContain(schemas, property => property.PropertyName == nameof(SensitiveNode.HiddenValue));
        Assert.Equal(0, SensitiveNode.Constructions);
        Assert.Equal(0, SensitiveNode.GetterCalls);
        Assert.Equal(0, SensitiveNode.SetterCalls);
        Assert.Equal(0, SensitiveNode.LoadCalls);
        Assert.Equal(0, SensitiveNode.DescriptorConstructions);
    }

    public sealed class SensitiveNode : STNode
    {
        public static int Constructions { get; private set; }
        public static int GetterCalls { get; private set; }
        public static int SetterCalls { get; private set; }
        public static int LoadCalls { get; private set; }
        public static int DescriptorConstructions { get; set; }
        private string _apiKey = "fixture credential";
        private string _configuration = "fixture password";
        private string _accessSetting = "fixture key";
        private string _localSetting = "fixture localized key";
        private string _authorization = "fixture header value";
        private string _requestSetting = "fixture request value";
        private int _threshold = 7;

        public SensitiveNode() => Constructions++;

        [STNodeProperty("Connection setting", "Ordinary label with a sensitive CLR key.", DescriptorType = typeof(CountingDescriptor))]
        public string ApiKey { get { GetterCalls++; return _apiKey; } set { SetterCalls++; _apiKey = value; } }

        [STNodeProperty("Password", "Sensitive display label with an ordinary CLR key.", DescriptorType = typeof(CountingDescriptor))]
        public string Configuration { get { GetterCalls++; return _configuration; } set { SetterCalls++; _configuration = value; } }

        [STNodeProperty("API Key", "A human-readable sensitive label using a separator.", DescriptorType = typeof(CountingDescriptor))]
        public string AccessSetting { get { GetterCalls++; return _accessSetting; } set { SetterCalls++; _accessSetting = value; } }

        [STNodeProperty("密钥", "The shared Common classifier also protects localized sensitive labels.", DescriptorType = typeof(CountingDescriptor))]
        public string LocalSetting { get { GetterCalls++; return _localSetting; } set { SetterCalls++; _localSetting = value; } }

        [STNodeProperty("Request header", "An ordinary label cannot bypass the shared Authorization key policy.", DescriptorType = typeof(CountingDescriptor))]
        public string Authorization { get { GetterCalls++; return _authorization; } set { SetterCalls++; _authorization = value; } }

        [STNodeProperty("Bearer", "The sensitive display label must protect an ordinary CLR property name.", DescriptorType = typeof(CountingDescriptor))]
        public string RequestSetting { get { GetterCalls++; return _requestSetting; } set { SetterCalls++; _requestSetting = value; } }

        [STNodeProperty("Threshold", "This description may mention a password; descriptions are not sensitivity keys.", DescriptorType = typeof(UnitsDescriptor))]
        public int Threshold { get { GetterCalls++; return _threshold; } set { SetterCalls++; _threshold = value; } }

        [STNodeProperty("Readonly value", "Keep the existing readonly metadata contract.", false, false, true)]
        public int ReadonlyValue { get => 1; set { SetterCalls++; } }

        [STNodeProperty("Hidden value", "Keep hidden fields out of the catalog.", false, true)]
        public int HiddenValue { get => 1; set { SetterCalls++; } }

        public override void OnLoadNode(Dictionary<string, byte[]> data) { LoadCalls++; base.OnLoadNode(data); }

        public static void ResetCounters() => Constructions = GetterCalls = SetterCalls = LoadCalls = DescriptorConstructions = 0;
    }

    public sealed class CountingDescriptor : STNodePropertyDescriptor
    {
        public CountingDescriptor() => SensitiveNode.DescriptorConstructions++;
    }

    public sealed class UnitsDescriptor : STNodePropertyDescriptor
    {
        public UnitsDescriptor() => SensitiveNode.DescriptorConstructions++;
        protected override object GetValueFromString(string text) => text.StartsWith("units:", StringComparison.Ordinal)
            ? int.Parse(text[6..], CultureInfo.InvariantCulture) : throw new FormatException("Expected units:<integer>.");
        protected override byte[] GetBytesFromValue() => BitConverter.GetBytes((int)PropertyInfo.GetValue(Node)!);
        protected override object GetValueFromBytes(byte[] data) => BitConverter.ToInt32(data, 0);
    }
}
