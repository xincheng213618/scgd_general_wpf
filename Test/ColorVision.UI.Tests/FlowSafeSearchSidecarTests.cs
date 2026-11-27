using ColorVision.Engine.Templates.Flow.Search;

namespace ColorVision.UI.Tests;

public sealed class FlowSafeSearchSidecarTests
{
    [Fact]
    public void IndexerDropsUnsafeValuesAndDeepLinkRoundTrips()
    {
        Guid nodeGuid = Guid.Parse(
            "7f08650d-32f8-4613-a171-a58692be307e");
        FlowNodeSearchEntry entry = Assert.Single(
            FlowNodeSearchIndexer.Build(
                "flow:search",
                12,
                [
                    new FlowNodeSearchDocument
                    {
                        SourceNodeGuid = nodeGuid,
                        NodePath = "root/camera-node",
                        NodeTypeKey = "CameraCaptureNode",
                        DisplayName = "相机曝光",
                        Title = "曝光采集",
                        TemplateName = """{"secret":"not-indexed"}""",
                        DeviceCode = @"C:\device\config.json",
                        ServiceCode = "token=abc123",
                        Tags =
                        [
                            "camera",
                            """{"payload":"mqtt"}""",
                            @"D:\private",
                        ],
                    },
                ]));

        Assert.Equal("相机曝光", entry.DisplayName);
        Assert.Null(entry.TemplateName);
        Assert.Null(entry.DeviceCode);
        Assert.Null(entry.ServiceCode);
        Assert.Equal("camera", entry.Tags);
        Assert.DoesNotContain("abc123", entry.SearchText);
        Assert.DoesNotContain("config.json", entry.SearchText);
        Assert.DoesNotContain("payload", entry.SearchText);

        string serialized = entry.DeepLink.ToString();
        Assert.True(FlowDeepLink.TryParse(serialized, out FlowDeepLink? parsed));
        Assert.Equal(entry.FlowKey, parsed!.FlowKey);
        Assert.Equal(entry.Revision, parsed.Revision);
        Assert.Equal(nodeGuid, parsed.SourceNodeGuid);
        Assert.Equal(entry.NodePath, parsed.NodePath);
    }

    [Fact]
    public void InMemoryAndSqliteIndexesUseSameSafeTypedQuery()
    {
        FlowNodeSearchDocument[] nodes =
        [
            new FlowNodeSearchDocument
            {
                SourceNodeGuid = Guid.Parse(
                    "f40bd9ae-8f97-46ea-a78d-f43c996912ce"),
                NodePath = "root/camera",
                NodeTypeKey = "CameraNode",
                DisplayName = "曝光采集",
                DeviceCode = "Camera01",
            },
            new FlowNodeSearchDocument
            {
                SourceNodeGuid = Guid.Parse(
                    "5a23c064-42f6-44e8-9947-c6cb37d5cab4"),
                NodePath = "root/spectrum",
                NodeTypeKey = "SpectrumNode",
                DisplayName = "光谱采集",
                DeviceCode = "Spectrum01",
            },
        ];

        var memory = new InMemoryFlowNodeSearchIndex();
        using var sqlite =
            new SqliteFlowNodeSearchIndex("Data Source=:memory:");
        memory.ReplaceRevision("flow:search", 3, nodes);
        sqlite.ReplaceRevision("flow:search", 3, nodes);

        var query = new FlowNodeSearchQuery
        {
            Text = "曝光",
            FlowKey = "flow:search",
            Revision = 3,
        };
        FlowNodeSearchEntry memoryHit = Assert.Single(memory.Search(query));
        FlowNodeSearchEntry sqliteHit = Assert.Single(sqlite.Search(query));
        Assert.Equal(memoryHit.SourceNodeGuid, sqliteHit.SourceNodeGuid);
        Assert.Equal(memoryHit.DeepLink, sqliteHit.DeepLink);

        var injection = new FlowNodeSearchQuery
        {
            Text = "%' OR 1=1 --",
        };
        Assert.Empty(memory.Search(injection));
        Assert.Empty(sqlite.Search(injection));
    }

    [Fact]
    public void UnfilteredSqliteSearchAppliesOrderAndLimitAndPreservesMissingFields()
    {
        using var index = new SqliteFlowNodeSearchIndex("Data Source=:memory:");
        var filled = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse("7f08650d-32f8-4613-a171-a58692be307e"),
            NodePath = "root/a",
            NodeTypeKey = "Camera",
            DisplayName = "曝光",
            Title = "采集",
            TemplateName = "Template",
            DeviceCode = "Camera-1",
            ServiceCode = "CameraService",
            Tags = ["camera"],
        };
        var empty = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse("5a23c064-42f6-44e8-9947-c6cb37d5cab4"),
            NodePath = "root/b",
            NodeTypeKey = "Spectrum",
        };
        index.ReplaceRevision("flow:search", 1, [filled]);
        index.ReplaceRevision("flow:search", 2, [filled, empty]);

        var results = index.Search(new FlowNodeSearchQuery { Limit = 2 });

        Assert.Equal(["root/a", "root/b"], results.Select(entry => entry.NodePath));
        Assert.All(results, entry => Assert.Equal(2, entry.Revision));
        Assert.Equal("曝光", results[0].DisplayName);
        Assert.Null(results[1].DisplayName);
        Assert.Null(results[1].Title);
        Assert.Null(results[1].TemplateName);
        Assert.Null(results[1].DeviceCode);
        Assert.Null(results[1].ServiceCode);
        Assert.Equal(string.Empty, results[1].Tags);
    }

    [Fact]
    public void PublicIndexInputHasNoGenericSensitivePayloadFields()
    {
        string[] propertyNames = typeof(FlowNodeSearchDocument)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("FilePath", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Properties", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LatestOnlyExcludesSupersededRevisionEntries()
    {
        var revisionOne = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse(
                "6619cb66-a48a-40fe-af07-273494a5d64e"),
            NodePath = "root/nodes/6619cb66a48a40feaf07273494a5d64e",
            NodeTypeKey = "CameraNode",
            DisplayName = "旧曝光节点",
        };
        var revisionTwo = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse(
                "8ea8497a-8ef8-40bc-8e16-02cd18499c4f"),
            NodePath = "root/nodes/8ea8497a8ef840bc8e1602cd18499c4f",
            NodeTypeKey = "CameraNode",
            DisplayName = "新曝光节点",
        };

        var memory = new InMemoryFlowNodeSearchIndex();
        using var sqlite =
            new SqliteFlowNodeSearchIndex("Data Source=:memory:");
        foreach (IFlowNodeSearchIndex index in new IFlowNodeSearchIndex[]
                 {
                     memory,
                     sqlite,
                 })
        {
            index.ReplaceRevision("flow:latest", 1, [revisionOne]);
            index.ReplaceRevision("flow:latest", 2, [revisionTwo]);

            FlowNodeSearchEntry hit = Assert.Single(index.Search(
                new FlowNodeSearchQuery
                {
                    Text = "曝光",
                    LatestOnly = true,
                }));
            Assert.Equal(2, hit.Revision);
            Assert.Equal("新曝光节点", hit.DisplayName);
        }
    }

    [Fact]
    public void LatestOnlySelectsEachFlowsNewestIndexedRevisionBeforeFiltering()
    {
        using var index = new SqliteFlowNodeSearchIndex("Data Source=:memory:");
        var query = new FlowNodeSearchQuery
        {
            Text = "曝光_%",
            LatestOnly = true,
        };
        Assert.Empty(index.Search(query));

        var first = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse("f40bd9ae-8f97-46ea-a78d-f43c996912ce"),
            NodePath = "root/capture",
            NodeTypeKey = "CameraNode",
            DisplayName = "曝光_%",
        };
        var second = new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse("7f08650d-32f8-4613-a171-a58692be307e"),
            NodePath = first.NodePath,
            NodeTypeKey = first.NodeTypeKey,
            DisplayName = first.DisplayName,
        };
        var nonmatching = new FlowNodeSearchDocument
        {
            SourceNodeGuid = first.SourceNodeGuid,
            NodePath = first.NodePath,
            NodeTypeKey = "SpectrumNode",
            DisplayName = "曝光-1",
        };
        index.ReplaceRevision("flow:a", 1, [first]);
        index.ReplaceRevision("flow:a", 8, [nonmatching]);
        index.ReplaceRevision("flow:b", 4, [first, second]);
        index.ReplaceRevision("flow:c", 5, [second]);
        index.ReplaceRevision("flow:c", 9, []);

        Assert.Equal(
            [("flow:c", 5, second.SourceNodeGuid), ("flow:b", 4, first.SourceNodeGuid), ("flow:b", 4, second.SourceNodeGuid)],
            index.Search(query).Select(hit => (hit.FlowKey, hit.Revision, hit.SourceNodeGuid)));

        index.ReplaceRevision("flow:a", 8, []);
        Assert.Equal(1, index.Search(query)[^1].Revision);
    }
}
