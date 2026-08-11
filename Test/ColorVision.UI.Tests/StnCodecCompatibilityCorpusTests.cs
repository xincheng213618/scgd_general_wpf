using ColorVision.Engine.Templates.Flow;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public class StnCodecCompatibilityCorpusTests
{
    private const string OriginalTemplateName = "相机模板-α";
    private const string ReplacementTemplateName = "相机模板-β";
    private const string UnknownPropertyName = "Vendor.扩展备注";
    private const string UnknownPropertyValue = "保留值-Ω";
    private const string SourceTypeKey =
        "10000000-0000-0000-0000-000000000001";
    private const string SinkTypeKey =
        "20000000-0000-0000-0000-000000000002";
    private static readonly Guid SourceNodeId =
        new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid SinkNodeId =
        new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    // Frozen STND v1 golden corpus. These bytes were encoded once from the
    // framing and model-key formats in the named revisions, then pasted here.
    // The tests deliberately never call STNodeCanvasWriter to create them.
    //
    // LegacyShortModel: b7deed1c, module|type.Name.
    // CurrentFullModel: 93f9451656, module|type.FullName.
    private const string LegacyShortModelCorpus =
        "U1RORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUBIELdpIq0hJ3QYhChmYLfxoLTPXHNS5wzgDES1CCClatDGQEgxaqJAhbfIDpL34D9w1o6t+Q9xJw0VQCw/ce7nnfQ8HHsDvASzvQGlvBkCcA7a9NEWVqJgRqeSIHDjCJKNmHEIqdR5SJS9VZC0ToprCkyAVyKZz51v2X66RnBYA+5oorACwWq1Wm83t9vkiptgsQOIqewUAswDCVGbfUwBzAI5FQU0w4wbAPAA/EU8SZmIXwAIAj6bSkHhGOABgg8OYklwDYDRvh9fZQbZh1y/beq0xfHk27gqLrJTyySNZFalkhpZGVoDysR+TtfNUk8wubrQVJmk5FVNJMJYmywAG92/GQ9MoPxrFjr1XXwUQJZJAFYdxVdHref0pZ7yWWfKjUxzkC/pF296rbHDA1j9Ii1LS5Oz6m7NrknO1Wq22Wt1uv/9papJzgpsGZ27Med2EUDNKuWmiHh82/AJ7q9e9pwIAAA==";
    private const string CurrentFullModelCorpus =
        "U1RORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJW2Suo2EEnIwGzhR2txbjk4zpVxBqJahBBStGhjICUYtNCBDGmTHyDtxX/grhld9RviThouhCA8cO/lvuc9HHiAgA+wfQBl7xyAcw7w7VKJKjExK1LZGd13RkhWzToFSbqYaoRVeZcqGS0bppqSICEqkC3X9o8cU66RXDYAe5oorAHgeZ632z0evz9qiXlBcqyyVwAwDyBCM+x7CmABwJEoqEk2uAWwCCBAxJOkldgBsATAp6k0LJ4RDgCYcRBXUhsAzObd8CY3yDUcxlXbqDWGry/mfXGZldJE6jCjilS2QiujUZAm4r9D1p6gmmx1caOtCElnpLhKQvE0WQUweHg3H5tm5cksdRy9+jqAGJEFqjjN66pRLxjPefOtwpKfndKgUDQu245e1c0B3v/iF+WUBd/9N3z3JHxd1/VWq9vt978sTcJPcrOAz43hb1pkamY5P0v+48PMb2pNkoLRAgAA";

    // The malformed STND v1 payloads are frozen independently from the valid
    // typed graph and isolate conditions rejected by all four entrypoints.
    private const string BadMagicCorpus =
        "WFRORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJU2LWZIhdoEIRgpmC7UWo/OlIMf14YZqGgRQkjRoo2BlGDQQoUMaZMfIO3Ff+CuGV31G+JOGgZBBB649/Ke9x4OPIDbCVjegMLWBIAjBtgIBHmvHFYE5YQPeXgxHj/76fiIKG2LskoUPhCkg19T3Vp4xbb2Je6XayCbBcCOJosLAFiWZa1Wh8PlCpmiO690oNJXBDAJIEhSdDwGMAVgXxbVKDWuAUwDcEvyYdRMbAKYAeDUVBKQTyUGAOhiV1BiSwCM+k3/Kt1L1zj9oqlXav3nJ+M2N0tLSSTmT6kySZqhuYHlJRHh26TtEaIlzS5m8CsoJVJxQZV8QkKaB9C7ezXu60bxwci3uE51EcCelBSJwhuXJb2a1R8zxkuRJt9b+V42p583uU5plQHW/w3dk6TM7X8zt48yL5fL5Uaj3e52P0yNMo8y42DODJkvm0AqRiEzTuzDQ5efWhOmub4CAAA=";
    private const string BadVersionCorpus =
        "U1RORAIfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJU2LWZIhdoEIRgpmC7UWo/OlIMf14YZqGgRQkjRoo2BlGDQQoUMaZMfIO3Ff+CuGV31G+JOGgZBBB649/Ke9x4OPIDbCVjegMLWBIAjBtgIBHmvHFYE5YQPeXgxHj/76fiIKG2LskoUPhCkg19T3Vp4xbb2Je6XayCbBcCOJosLAFiWZa1Wh8PlCpmiO690oNJXBDAJIEhSdDwGMAVgXxbVKDWuAUwDcEvyYdRMbAKYAeDUVBKQTyUGAOhiV1BiSwCM+k3/Kt1L1zj9oqlXav3nJ+M2N0tLSSTmT6kySZqhuYHlJRHh26TtEaIlzS5m8CsoJVJxQZV8QkKaB9C7ezXu60bxwci3uE51EcCelBSJwhuXJb2a1R8zxkuRJt9b+V42p583uU5plQHW/w3dk6TM7X8zt48yL5fL5Uaj3e52P0yNMo8y42DODJkvm0AqRiEzTuzDQ5efWhOmub4CAAA=";
    private const string BadCrcCorpus =
        "U1RORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJU2LWZIhdoEIRgpmC7UWo/OlIMf14YZqGgRQkjRoo2BlGDQQoUMaZMfIO3Ff+CuGV31G+JOGgZBBB649/Ke9x4OPIDbCVjegMLWBIAjBtgIBHmvHFYE5YQPeXgxHj/76fiIKG2LskoUPhCkg19T3Vp4xbb2Je6XayCbBcCOJosLAFiWZa1Wh8PlCpmiO690oNJXBDAJIEhSdDwGMAVgXxbVKDWuAUwDcEvyYdRMbAKYAeDUVBKQTyUGAOhiV1BiSwCM+k3/Kt1L1zj9oqlXav3nJ+M2N0tLSSTmT6kySZqhuYHlJRHh26TtEaIlzS5m8CsoJVJxQZV8QkKaB9C7ezXu60bxwci3uE51EcCelBSJwhuXJb2a1R8zxkuRJt9b+V42p583uU5plQHW/w3dk6TM7X8zt48yL5fL5Uaj3e52P0yNMo8y42DODJkvm0AqRiEzTuzDQ5efWxOmub4CAAA=";
    private const string TruncatedCorpus =
        "U1RORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJU2LWZIhdoEIRgpmC7UWo/OlIMf14YZqGgRQkjRoo2BlGDQQoUMaZMfIO3Ff+CuGV31G+JOGgZBBB649/Ke9x4OPIDbCVjegMLWBIAjBtgIBHmvHFYE5YQPeXgxHj/76fiIKG2LskoUPhCkg19T3Vp4xbb2Je6XayCbBcCOJosLAFiWZa1Wh8PlCpmiO690oNJXBDAJIEhSdDwGMAVgXxbVKDWuAUwDcEvyYdRMbAKYAeDUVBKQTyUGAOhiV1BiSwCM+k3/Kt1L1zj9oqlXav3nJ+M2N0tLSSTmT6kySZqhuYHlJRHh26TtEaIlzS5m8CsoJVJxQZV8QkKaB9C7ezXu60bxwci3uE51EcCelBSJwhuXJb2a1R8zxkuRJt9b+V42p583uU5plQHW/w3dk6TM7X8zt48yL5fL5Uaj3e52P0yNMo8y42DODJkvm0AqRiEzTuzDQ5efWhOmub4=";
    private const string NegativeNodeLengthCorpus =
        "U1RORAEfiwgAAAAAAAAKY2DwcGRgYDnIwLDAnpGBgeH/////GRgYGAAIcFW/GAAAAA==";
    private const string OverLimitNodeLengthCorpus =
        "U1RORAEfiwgAAAAAAAAKY2DwcGRgYDnIwLDAnpGBgYERjBgYALB5QtQYAAAA";
    private const string TrailingGarbageCorpus =
        "U1RORAEfiwgAAAAAAAAKrZBNSwJRGIXPlEVEUEMQtJU2LWZIhdoEIRgpmC7UWo/OlIMf14YZqGgRQkjRoo2BlGDQQoUMaZMfIO3Ff+CuGV31G+JOGgZBBB649/Ke9x4OPIDbCVjegMLWBIAjBtgIBHmvHFYE5YQPeXgxHj/76fiIKG2LskoUPhCkg19T3Vp4xbb2Je6XayCbBcCOJosLAFiWZa1Wh8PlCpmiO690oNJXBDAJIEhSdDwGMAVgXxbVKDWuAUwDcEvyYdRMbAKYAeDUVBKQTyUGAOhiV1BiSwCM+k3/Kt1L1zj9oqlXav3nJ+M2N0tLSSTmT6kySZqhuYHlJRHh26TtEaIlzS5m8CsoJVJxQZV8QkKaB9C7ezXu60bxwci3uE51EcCelBSJwhuXJb2a1R8zxkuRJt9b+V42p583uU5plQHW/w3dk6TM7X8zt48yL5fL5Uaj3e52P0yNMo8y42DODJkvm0AqRiEzTuzDQ5efWhOmub4CAAB/";

    public static TheoryData<string, string, string, bool> ValidCorpus =>
        new()
        {
            {
                "legacy-short-model",
                LegacyShortModelCorpus,
                "212357b8480ab9d0a21578e551bb916ab4558b122b7e67dc6a30dbbca6fdd1b4",
                false
            },
            {
                "current-full-model",
                CurrentFullModelCorpus,
                "5d55a18aac85e8d7a6d1106fa7e51f1038beb0965cd0feecf640c584412fbeab",
                true
            },
        };

    public static TheoryData<string, string> MalformedCorpus =>
        new()
        {
            { "bad-STND-magic", BadMagicCorpus },
            { "unsupported-version", BadVersionCorpus },
            { "gzip-CRC", BadCrcCorpus },
            { "truncated-gzip", TruncatedCorpus },
            { "negative-node-data-length", NegativeNodeLengthCorpus },
            { "over-limit-node-data-length", OverLimitNodeLengthCorpus },
            { "trailing-garbage", TrailingGarbageCorpus },
        };

    [Theory]
    [MemberData(nameof(ValidCorpus))]
    public void FrozenValidCorpusPreservesContractAcrossAllEntrypoints(
        string corpusName,
        string base64,
        string expectedSha256,
        bool usesFullModelName)
    {
        byte[] stnData = DecodeFrozenCorpus(corpusName, base64);
        Assert.Equal(
            expectedSha256,
            Convert.ToHexString(SHA256.HashData(stnData))
                .ToLowerInvariant());

        byte[] originalBody =
            FlowPackageStnValidator.ValidateAndDecompress(stnData);
        CanvasSnapshot original = ParseCanvas(originalBody);
        AssertFrozenSnapshot(
            original,
            usesFullModelName,
            OriginalTemplateName);
        AssertLoadedGraph(stnData);

        Assert.Equal(
            [OriginalTemplateName],
            FlowPackageHelper.ExtractTemplateNames(stnData));

        byte[] replaced = FlowPackageHelper.ReplaceTemplateNames(
            stnData,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OriginalTemplateName] = ReplacementTemplateName,
            });

        Assert.NotSame(stnData, replaced);
        byte[] replacedBody =
            FlowPackageStnValidator.ValidateAndDecompress(replaced);
        CanvasSnapshot replacement = ParseCanvas(replacedBody);
        AssertOnlyTemplateNameChanged(
            original,
            replacement,
            usesFullModelName);
        AssertLoadedGraph(replaced);
        Assert.Equal(
            [ReplacementTemplateName],
            FlowPackageHelper.ExtractTemplateNames(replaced));
    }

    [Theory]
    [MemberData(nameof(MalformedCorpus))]
    public void FrozenMalformedCorpusIsRejectedByAllEntrypoints(
        string corpusName,
        string base64)
    {
        byte[] stnData = DecodeFrozenCorpus(corpusName, base64);
        byte[] original = stnData.ToArray();

        Assert.ThrowsAny<InvalidDataException>(() =>
            FlowPackageStnValidator.ValidateAndDecompress(stnData));

        using (var container = new CVNodeContainer())
        {
            Assert.ThrowsAny<InvalidDataException>(() =>
                container.LoadCanvas(stnData));
        }

        Assert.Empty(FlowPackageHelper.ExtractTemplateNames(stnData));
        byte[] replacement = FlowPackageHelper.ReplaceTemplateNames(
            stnData,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [OriginalTemplateName] = ReplacementTemplateName,
            });
        Assert.Same(stnData, replacement);
        Assert.Equal(original, replacement);
    }

    private static void AssertFrozenSnapshot(
        CanvasSnapshot snapshot,
        bool usesFullModelName,
        string expectedTemplateName)
    {
        Assert.Equal(12.5f, snapshot.CanvasOffsetX);
        Assert.Equal(-8.25f, snapshot.CanvasOffsetY);
        Assert.Equal(1.25f, snapshot.CanvasScale);
        Assert.Equal([1L], snapshot.PackedConnections);
        Assert.Equal(2, snapshot.Nodes.Count);

        NodeSnapshot source = snapshot.Nodes[0];
        NodeSnapshot sink = snapshot.Nodes[1];
        string modelPrefix = "ColorVision.UI.Tests.dll|";
        string typePrefix = usesFullModelName
            ? "ColorVision.UI.Tests."
            : string.Empty;
        Assert.Equal(
            modelPrefix + typePrefix + nameof(StnCorpusSourceNode),
            source.ModelKey);
        Assert.Equal(
            modelPrefix + typePrefix + nameof(StnCorpusSinkNode),
            sink.ModelKey);
        Assert.Equal(SourceTypeKey, source.TypeKey);
        Assert.Equal(SinkTypeKey, sink.TypeKey);
        AssertProperty(source, "Guid", SourceNodeId.ToByteArray());
        AssertProperty(sink, "Guid", SinkNodeId.ToByteArray());
        AssertProperty(source, "Left", BitConverter.GetBytes(100));
        AssertProperty(source, "Top", BitConverter.GetBytes(120));
        AssertProperty(sink, "Left", BitConverter.GetBytes(360));
        AssertProperty(sink, "Top", BitConverter.GetBytes(120));
        Assert.Equal(
            expectedTemplateName,
            DecodeProperty(source, "TemplateName"));
        Assert.Equal(
            UnknownPropertyValue,
            DecodeProperty(source, UnknownPropertyName));
    }

    private static byte[] DecodeFrozenCorpus(
        string corpusName,
        string base64)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(corpusName),
            "Frozen corpus cases require a stable diagnostic name.");
        return Convert.FromBase64String(base64);
    }

    private static void AssertLoadedGraph(byte[] stnData)
    {
        Assert.NotEqual(
            SourceTypeKey,
            typeof(StnCorpusSourceNode).GUID.ToString());
        Assert.NotEqual(
            SinkTypeKey,
            typeof(StnCorpusSinkNode).GUID.ToString());

        using var container = new CVNodeContainer();
        Assert.True(
            container.LoadAssembly(typeof(StnCorpusSourceNode).Assembly));
        container.LoadCanvas(stnData);

        STNode[] nodes = container.Nodes.Cast<STNode>().ToArray();
        Assert.Equal(2, nodes.Length);
        var source = Assert.IsType<StnCorpusSourceNode>(nodes[0]);
        var sink = Assert.IsType<StnCorpusSinkNode>(nodes[1]);
        Assert.Equal(SourceNodeId, source.Guid);
        Assert.Equal(SinkNodeId, sink.Guid);
        Assert.Equal(100, source.Left);
        Assert.Equal(120, source.Top);
        Assert.Equal(360, sink.Left);
        Assert.Equal(120, sink.Top);
        Assert.Equal("源节点-兼容语料", source.Mark);
        Assert.Equal("目标节点-兼容语料", sink.Mark);

        STNodeOption output = Assert.Single(
            source.GetAllOutputOptions());
        STNodeOption input = Assert.Single(
            sink.GetAllInputOptions());
        Assert.Same(input, Assert.Single(output.ConnectedOption));
        Assert.Same(output, Assert.Single(input.ConnectedOption));
    }

    private static void AssertOnlyTemplateNameChanged(
        CanvasSnapshot original,
        CanvasSnapshot replacement,
        bool usesFullModelName)
    {
        AssertFrozenSnapshot(
            replacement,
            usesFullModelName,
            ReplacementTemplateName);
        Assert.Equal(original.CanvasOffsetX, replacement.CanvasOffsetX);
        Assert.Equal(original.CanvasOffsetY, replacement.CanvasOffsetY);
        Assert.Equal(original.CanvasScale, replacement.CanvasScale);
        Assert.Equal(original.PackedConnections, replacement.PackedConnections);
        Assert.Equal(original.Nodes.Count, replacement.Nodes.Count);

        for (int index = 0; index < original.Nodes.Count; index++)
        {
            NodeSnapshot before = original.Nodes[index];
            NodeSnapshot after = replacement.Nodes[index];
            Assert.Equal(before.ModelKey, after.ModelKey);
            Assert.Equal(before.TypeKey, after.TypeKey);
            Assert.Equal(
                before.Properties.Keys.Order(StringComparer.Ordinal),
                after.Properties.Keys.Order(StringComparer.Ordinal));

            foreach ((string key, byte[] value) in before.Properties)
            {
                if (key == "TemplateName")
                    continue;
                AssertProperty(after, key, value);
            }
        }

        Assert.Equal(
            UnknownPropertyValue,
            DecodeProperty(replacement.Nodes[0], UnknownPropertyName));
    }

    private static void AssertProperty(
        NodeSnapshot node,
        string propertyName,
        byte[] expectedValue)
    {
        Assert.True(
            node.Properties.TryGetValue(propertyName, out byte[]? value),
            $"Missing property '{propertyName}'.");
        Assert.Equal(expectedValue, value);
    }

    private static string DecodeProperty(
        NodeSnapshot node,
        string propertyName)
    {
        Assert.True(
            node.Properties.TryGetValue(propertyName, out byte[]? value),
            $"Missing property '{propertyName}'.");
        return Encoding.UTF8.GetString(value);
    }

    private static CanvasSnapshot ParseCanvas(byte[] body)
    {
        using var stream = new MemoryStream(body, writable: false);
        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: false);
        float canvasOffsetX = reader.ReadSingle();
        float canvasOffsetY = reader.ReadSingle();
        float canvasScale = reader.ReadSingle();
        int nodeCount = reader.ReadInt32();
        var nodes = new List<NodeSnapshot>(nodeCount);
        for (int index = 0; index < nodeCount; index++)
        {
            int nodeLength = reader.ReadInt32();
            nodes.Add(ParseNode(ReadBytes(reader, nodeLength)));
        }

        int connectionCount = reader.ReadInt32();
        var connections = new List<long>(connectionCount);
        for (int index = 0; index < connectionCount; index++)
            connections.Add(reader.ReadInt64());
        Assert.Equal(stream.Length, stream.Position);
        return new CanvasSnapshot(
            canvasOffsetX,
            canvasOffsetY,
            canvasScale,
            nodes,
            connections);
    }

    private static NodeSnapshot ParseNode(byte[] nodeData)
    {
        using var stream = new MemoryStream(nodeData, writable: false);
        using var reader = new BinaryReader(
            stream,
            Encoding.UTF8,
            leaveOpen: false);
        string modelKey = Encoding.UTF8.GetString(
            ReadBytes(reader, reader.ReadByte()));
        string typeKey = Encoding.UTF8.GetString(
            ReadBytes(reader, reader.ReadByte()));
        var properties = new Dictionary<string, byte[]>(
            StringComparer.Ordinal);
        while (stream.Position < stream.Length)
        {
            string key = Encoding.UTF8.GetString(
                ReadBytes(reader, reader.ReadInt32()));
            byte[] value = ReadBytes(reader, reader.ReadInt32());
            Assert.True(
                properties.TryAdd(key, value),
                $"Duplicate property '{key}'.");
        }
        return new NodeSnapshot(modelKey, typeKey, properties);
    }

    private static byte[] ReadBytes(
        BinaryReader reader,
        int length)
    {
        Assert.True(length >= 0, $"Negative fixture length: {length}.");
        byte[] value = reader.ReadBytes(length);
        Assert.Equal(length, value.Length);
        return value;
    }

    private sealed record CanvasSnapshot(
        float CanvasOffsetX,
        float CanvasOffsetY,
        float CanvasScale,
        IReadOnlyList<NodeSnapshot> Nodes,
        IReadOnlyList<long> PackedConnections);

    private sealed record NodeSnapshot(
        string ModelKey,
        string TypeKey,
        IReadOnlyDictionary<string, byte[]> Properties);
}

public sealed class StnCorpusSourceNode : STNode
{
    protected override void OnCreate()
    {
        base.OnCreate();
        OutputOptions.Add("OUT", typeof(string), bSingle: false);
    }
}

public sealed class StnCorpusSinkNode : STNode
{
    protected override void OnCreate()
    {
        base.OnCreate();
        InputOptions.Add("IN", typeof(string), bSingle: true);
    }
}
