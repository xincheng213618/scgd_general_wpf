using ColorVision.Copilot.Mcp;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowGraphInspectionServiceTests
{
    private const string Revision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Source = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task LargeReorderedGraphCanBeReadAsCompleteIndependentNodeAndCrossPageEdgeStreams()
    {
        var graph = Graph(220, largeText: true);
        var calls = 0;
        var service = new CopilotFlowGraphInspectionService(_ =>
        {
            calls++;
            // Drawing/selection ordering is not part of revision. It must not move continuation offsets.
            var nodes = calls % 2 == 0 ? graph.Nodes.Reverse().ToArray() : graph.Nodes.ToArray();
            return Task.FromResult<CopilotFlowContextSnapshot?>(Copy(graph, nodes: nodes, edges: graph.Edges.Reverse().ToArray()));
        });
        var nodeIds = new List<string>();
        var edgeIds = new List<string>();
        foreach (var section in new[] { "nodes", "edges" })
        {
            string? cursor = null;
            var cursors = new HashSet<string>();
            var delivered = 0;
            do
            {
                var result = await service.InspectAsync(null, true, cursor == null ? 200 : 80, section, 200, Revision, cursor, CancellationToken.None, Source);
                Assert.True(result.Success, result.ErrorMessage);
                Assert.InRange(result.Content.Length, 1, CopilotFlowGraphInspectionService.MaximumContentCharacters);
                Assert.DoesNotContain("sk-1234567890abcdef1234567890abcdef", result.Content, StringComparison.Ordinal);
                using var json = JsonDocument.Parse(result.Content);
                var root = json.RootElement;
                Assert.Equal(220, root.GetProperty("totalNodeCount").GetInt32());
                Assert.Equal(219, root.GetProperty("totalEdgeCount").GetInt32());
                Assert.Equal(section, root.GetProperty("section").GetString());
                Assert.Equal(delivered, root.GetProperty("coverage").GetProperty("page_offset").GetInt32());
                if (section == "nodes")
                {
                    var rows = root.GetProperty("nodes").EnumerateArray().ToArray();
                    Assert.NotEmpty(rows);
                    nodeIds.AddRange(rows.Select(row => row.GetProperty("instanceId").GetString()!));
                    var pageIds = rows.Select(row => row.GetProperty("instanceId").GetString()).ToHashSet();
                    foreach (var edge in root.GetProperty("edges").EnumerateArray())
                    {
                        Assert.Contains(edge.GetProperty("sourceNodeId").GetString(), pageIds);
                        Assert.Contains(edge.GetProperty("targetNodeId").GetString(), pageIds);
                    }
                    delivered += rows.Length;
                }
                else
                {
                    Assert.Empty(root.GetProperty("nodes").EnumerateArray());
                    var rows = root.GetProperty("edges").EnumerateArray().ToArray();
                    Assert.NotEmpty(rows);
                    edgeIds.AddRange(rows.Select(EdgeKey));
                    delivered += rows.Length;
                }
                cursor = root.GetProperty("next_cursor").GetString();
                Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
                if (cursor != null) Assert.True(cursors.Add(cursor), "Continuation must advance by the actual delivered rows.");
            } while (cursor != null);
        }
        Assert.Equal(graph.Nodes.Select(node => node.InstanceId).Order(StringComparer.Ordinal), nodeIds);
        Assert.Equal(graph.Edges.Select(EdgeKey).Order(StringComparer.Ordinal), edgeIds.Order(StringComparer.Ordinal));
        Assert.Equal(nodeIds.Count, nodeIds.Distinct().Count());
        Assert.Equal(edgeIds.Count, edgeIds.Distinct().Count());
    }

    [Fact]
    public async Task FocusedEdgesUseAllExactMatchesAndIncludeNeighborsOutsideTheNodePage()
    {
        var graph = Graph(4);
        var nodes = graph.Nodes.Select((node, index) => new CopilotFlowNodeContextSnapshot
        {
            InstanceId = node.InstanceId, NodeId = index is 1 or 2 ? "shared-id" : "other", Title = node.Title,
        }).ToArray();
        var service = Service(Copy(graph, nodes: nodes));
        var nodePage = await service.InspectAsync("SHARED-ID", false, 1, "nodes", 1, null, null, CancellationToken.None);
        Assert.True(nodePage.Success, nodePage.ErrorMessage);
        using (var json = JsonDocument.Parse(nodePage.Content))
        {
            Assert.Single(json.RootElement.GetProperty("nodes").EnumerateArray());
            Assert.Empty(json.RootElement.GetProperty("edges").EnumerateArray());
            Assert.True(json.RootElement.GetProperty("has_more").GetBoolean());
        }
        var edges = await service.InspectAsync("shared-id", false, 1, "edges", 200, Revision, null, CancellationToken.None, Source);
        Assert.True(edges.Success, edges.ErrorMessage);
        using var document = JsonDocument.Parse(edges.Content);
        Assert.Equal(3, document.RootElement.GetProperty("edges").GetArrayLength());
        Assert.False(document.RootElement.GetProperty("has_more").GetBoolean());
    }

    [Theory]
    [InlineData("source")]
    [InlineData("revision")]
    [InlineData("stream")]
    [InlineData("section")]
    [InlineData("focus")]
    [InlineData("properties")]
    [InlineData("missing")]
    public async Task ChangedContinuationBindingsRejectMixedGraphEvidence(string mutation)
    {
        var graph = Graph(3);
        var calls = 0;
        var service = new CopilotFlowGraphInspectionService(_ => { calls++; return Task.FromResult<CopilotFlowContextSnapshot?>(graph); });
        var cursor = Next(await service.InspectAsync(null, false, 1, "nodes", 80, null, null, CancellationToken.None));
        graph = mutation switch
        {
            "source" => Copy(graph, sourceId: new string('c', 32)), "revision" => Copy(graph, revision: new string('c', 64)),
            "stream" => Copy(graph, nodes: graph.Nodes.Skip(1).ToArray(), edges: graph.Edges.Take(1).ToArray()), "missing" => null!, _ => graph,
        };
        var result = await service.InspectAsync(mutation == "focus" ? "other" : null, mutation == "properties", 200,
            mutation == "section" ? "edges" : "nodes", 200, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.Contains("Restart", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(mutation is "section" or "focus" or "properties" ? 1 : 2, calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("version")]
    [InlineData("offset-type")]
    [InlineData("negative")]
    [InlineData("whitespace")]
    public async Task MalformedGraphCursorsCannotCaptureTheEditor(string mutation)
    {
        var calls = 0;
        var service = new CopilotFlowGraphInspectionService(_ => { calls++; return Task.FromResult<CopilotFlowContextSnapshot?>(Graph(3)); });
        var cursor = Next(await service.InspectAsync(null, false, 1, "nodes", 80, null, null, CancellationToken.None))!;
        var encoded = cursor[6..].Replace('-', '+').Replace('_', '/');
        var source = Encoding.UTF8.GetString(Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4)));
        var json = JsonNode.Parse(source)!.AsObject();
        switch (mutation)
        {
            case "extra": json["extra"] = 1; break;
            case "missing": json.Remove("stream_key"); break;
            case "version": json["version"] = 2; break;
            case "offset-type": json["offset"] = "1"; break;
            case "negative": json["offset"] = -1; break;
        }
        var text = mutation == "duplicate" ? source.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal) : json.ToJsonString();
        cursor = "cvfg1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (mutation == "whitespace") cursor = " " + cursor;
        var result = await service.InspectAsync(null, false, 1, "nodes", 80, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(1, calls);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEmptyEditorOrUnmatchedFocusIsDifferentFromNoActiveEditor(bool focusMissing)
    {
        var service = Service(Graph(focusMissing ? 3 : 0));
        var empty = await service.InspectAsync(focusMissing ? "not-retained" : null, false, 80, "nodes", 80, null, null, CancellationToken.None);
        Assert.True(empty.Success, empty.ErrorMessage);
        using var json = JsonDocument.Parse(empty.Content);
        Assert.Empty(json.RootElement.GetProperty("nodes").EnumerateArray());
        Assert.False(json.RootElement.GetProperty("has_more").GetBoolean());
        var unavailable = new CopilotFlowGraphInspectionService(_ => Task.FromResult<CopilotFlowContextSnapshot?>(null));
        Assert.Equal(CopilotToolFailureKind.NotFound, (await unavailable.InspectAsync(null, false, 80, "nodes", 80, null, null, CancellationToken.None)).FailureKind);
    }

    [Fact]
    public async Task LargeSingleNodeDetailsDoNotBlockOverviewAndCanBeReadWithoutLossAcrossResizedPages()
    {
        var graph = Graph(3);
        var node = new CopilotFlowNodeContextSnapshot
        {
            InstanceId = graph.Nodes[0].InstanceId,
            InputPorts = Enumerable.Range(0, 300).Select(index => new CopilotFlowPortContextSnapshot { PortId = "in:" + index, Name = new string('中', 128) }).ToArray(),
            OutputPorts = Enumerable.Range(0, 300).Select(index => new CopilotFlowPortContextSnapshot { PortId = "out:" + index, Name = new string('文', 128) }).ToArray(),
            Parameters = Enumerable.Range(0, 300).Select(index => new CopilotContextProperty
            {
                PropertyName = "Parameter" + index.ToString("D3"), Name = "Same display name", Value = new string('中', 511) + "😀tail",
            }).Append(new() { PropertyName = "ApiKey", Name = "Connection setting", Value = "plain-provider-credential-value" }).ToArray(),
        };
        graph = Copy(graph, nodes: graph.Nodes.Skip(1).Append(node).ToArray());
        var calls = 0;
        var service = new CopilotFlowGraphInspectionService(_ =>
        {
            calls++;
            // Sorting must use CLR identity, not reflection, port array or editor drawing order.
            var reordered = graph.Nodes.Reverse().Select(item => new CopilotFlowNodeContextSnapshot
            {
                InstanceId = item.InstanceId, Title = item.Title,
                InputPorts = calls % 2 == 0 ? item.InputPorts.Reverse().ToArray() : item.InputPorts,
                OutputPorts = calls % 2 == 0 ? item.OutputPorts.Reverse().ToArray() : item.OutputPorts,
                Parameters = calls % 2 == 0 ? item.Parameters.Reverse().ToArray() : item.Parameters,
            }).ToArray();
            return Task.FromResult<CopilotFlowContextSnapshot?>(Copy(graph, nodes: reordered));
        });
        var overview = await service.InspectAsync(null, true, 200, "nodes", 200, null, null, CancellationToken.None);
        Assert.True(overview.Success, overview.ErrorMessage);
        Assert.InRange(overview.Content.Length, 1, CopilotFlowGraphInspectionService.MaximumContentCharacters);
        Assert.DoesNotContain("plain-provider-credential-value", overview.Content, StringComparison.Ordinal);
        using (var json = JsonDocument.Parse(overview.Content))
        {
            var rows = json.RootElement.GetProperty("nodes").EnumerateArray().ToArray();
            Assert.Equal(3, rows.Length);
            var large = Assert.Single(rows, row => row.GetProperty("instanceId").GetString() == node.InstanceId);
            Assert.Equal(300, large.GetProperty("inputPortCount").GetInt32());
            Assert.Equal(300, large.GetProperty("outputPortCount").GetInt32());
            Assert.Equal(301, large.GetProperty("propertyCount").GetInt32());
            Assert.True(large.GetProperty("portsTruncated").GetBoolean());
            Assert.True(large.GetProperty("propertiesTruncated").GetBoolean());
            Assert.InRange(large.GetProperty("inputs").GetArrayLength(), 0, 8);
            Assert.False(json.RootElement.GetProperty("has_more").GetBoolean());
            Assert.True(json.RootElement.GetProperty("isTruncated").GetBoolean());
        }
        foreach (var section in new[] { "ports", "properties" })
        {
            string? cursor = null;
            var delivered = new List<string>();
            var cursors = new HashSet<string>();
            do
            {
                var result = await service.InspectAsync(node.InstanceId, true, 200, section, 200, Revision, cursor, CancellationToken.None, Source, cursor == null ? 200 : 80);
                Assert.True(result.Success, result.ErrorMessage);
                Assert.InRange(result.Content.Length, 1, CopilotFlowGraphInspectionService.MaximumContentCharacters);
                Assert.DoesNotContain("plain-provider-credential-value", result.Content, StringComparison.Ordinal);
                using var json = JsonDocument.Parse(result.Content);
                var root = json.RootElement;
                Assert.Empty(root.GetProperty("nodes").EnumerateArray());
                Assert.Empty(root.GetProperty("edges").EnumerateArray());
                Assert.Equal(delivered.Count, root.GetProperty("coverage").GetProperty("page_offset").GetInt32());
                var rows = root.GetProperty(section).EnumerateArray().ToArray();
                Assert.NotEmpty(rows);
                foreach (var row in rows)
                {
                    Assert.Equal(node.InstanceId, row.GetProperty("nodeInstanceId").GetString());
                    if (section == "ports") delivered.Add(row.GetProperty("direction").GetString() + ":" + row.GetProperty("portId").GetString());
                    else
                    {
                        Assert.Equal(delivered.Count, row.GetProperty("parameterIndex").GetInt32());
                        var key = row.GetProperty("propertyName").GetString()!;
                        var value = row.GetProperty("value").GetString()!;
                        delivered.Add(key);
                        if (key == "ApiKey") Assert.Equal("<redacted>", value);
                        else
                        {
                            Assert.Equal(new string('中', 511), value);
                            Assert.True(row.GetProperty("textTruncated").GetBoolean());
                        }
                    }
                }
                cursor = root.GetProperty("next_cursor").GetString();
                Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
                if (cursor != null) Assert.True(cursors.Add(cursor));
            } while (cursor != null);
            Assert.Equal(section == "ports" ? 600 : 301, delivered.Count);
            Assert.Equal(delivered.Count, delivered.Distinct(StringComparer.Ordinal).Count());
            if (section == "properties") Assert.Equal(node.Parameters.Select(item => item.PropertyName).Order(StringComparer.Ordinal), delivered);
        }
    }

    [Theory]
    [InlineData("ports")]
    [InlineData("properties")]
    public async Task DetailCursorRejectsChangedBoundedEvidenceEvenWhenSavedStateRevisionDidNotChange(string section)
    {
        var graph = Graph(3);
        var service = new CopilotFlowGraphInspectionService(_ => Task.FromResult<CopilotFlowContextSnapshot?>(graph));
        var cursor = Next(await service.InspectAsync(null, true, 80, section, 80, null, null, CancellationToken.None, maxItems: 1));
        graph = Copy(graph, nodes: graph.Nodes.Select(node => new CopilotFlowNodeContextSnapshot
        {
            InstanceId = node.InstanceId,
            InputPorts = [new() { PortId = "in:0", Name = "Changed definition" }], OutputPorts = node.OutputPorts,
            Parameters = [new() { Name = "Threshold", Value = "Changed value" }],
        }).ToArray());
        var changed = await service.InspectAsync(null, true, 80, section, 80, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, changed.FailureKind);
        Assert.Empty(changed.Content);
    }

    [Fact]
    public async Task WorstCaseEscapedLabelsAndLongFocusStillDeliverStableNodeIdsWithAnAdvancingCursor()
    {
        var text = new string('中', 256);
        var graph = Graph(2);
        graph = new CopilotFlowContextSnapshot
        {
            SourceInstanceId = Source, Revision = Revision, FlowName = text, TemplateId = text, Status = text,
            Nodes = graph.Nodes.Select(node => new CopilotFlowNodeContextSnapshot
            {
                InstanceId = node.InstanceId, NodeId = text, TypeKey = text, RuntimeType = text, CategoryPath = text,
                Title = text, NodeName = text, NodeType = text, DeviceCode = text, Mark = text,
            }).ToArray(),
        };
        var service = Service(graph);
        var first = await service.InspectAsync(text, false, 200, "nodes", 80, null, null, CancellationToken.None);
        Assert.True(first.Success, first.ErrorMessage);
        Assert.InRange(first.Content.Length, 1, CopilotFlowGraphInspectionService.MaximumContentCharacters);
        using var json = JsonDocument.Parse(first.Content);
        Assert.Single(json.RootElement.GetProperty("nodes").EnumerateArray());
        Assert.True(json.RootElement.GetProperty("coverage").GetProperty("text_truncated").GetBoolean());
        var last = await service.InspectAsync(text, false, 200, "nodes", 80, null, Next(first), CancellationToken.None);
        Assert.True(last.Success, last.ErrorMessage);
        using var final = JsonDocument.Parse(last.Content);
        Assert.Single(final.RootElement.GetProperty("nodes").EnumerateArray());
        Assert.NotEqual(json.RootElement.GetProperty("nodes")[0].GetProperty("instanceId").GetString(), final.RootElement.GetProperty("nodes")[0].GetProperty("instanceId").GetString());
        Assert.False(final.RootElement.GetProperty("has_more").GetBoolean());
    }

    [Theory]
    [InlineData("port")]
    [InlineData("property")]
    public async Task DuplicateDetailIdentitiesFailWithoutPublishingAmbiguousRows(string kind)
    {
        var graph = Graph(1);
        var node = new CopilotFlowNodeContextSnapshot
        {
            InstanceId = graph.Nodes[0].InstanceId,
            InputPorts = kind == "port" ? [new() { PortId = "in:0" }, new() { PortId = "in:0" }] : [],
            Parameters = kind == "property" ? [new() { PropertyName = "Threshold", Name = "A" }, new() { PropertyName = "Threshold", Name = "B" }] : [],
        };
        var result = await Service(Copy(graph, nodes: [node])).InspectAsync(null, true, 80, "nodes", 80, null, null, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("ports")]
    [InlineData("properties")]
    public async Task DetailCursorsRemainBoundToTheirSectionAndPropertyConsent(string section)
    {
        var service = Service(Graph(3));
        var cursor = Next(await service.InspectAsync(null, true, 80, section, 80, null, null, CancellationToken.None, maxItems: 1));
        var wrongSection = await service.InspectAsync(null, true, 80, section == "ports" ? "properties" : "ports", 80, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, wrongSection.FailureKind);
        var wrongConsent = await service.InspectAsync(null, false, 80, section, 80, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, wrongConsent.FailureKind);
        var wrongFocus = await service.InspectAsync("another", true, 80, section, 80, null, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, wrongFocus.FailureKind);
    }

    [Fact]
    public async Task BothClrIdentityAndDisplayNameProtectOrdinaryCredentialText()
    {
        var graph = Graph(1);
        var node = new CopilotFlowNodeContextSnapshot
        {
            InstanceId = graph.Nodes[0].InstanceId,
            Parameters = [new() { PropertyName = "ApiKey", Name = "Setting", Value = "raw-credential-one" },
                new() { PropertyName = "Address", Name = "Password", Value = "raw-credential-two" },
                new() { PropertyName = "Threshold", Name = "Setting", Value = "safe-value" }],
        };
        var service = Service(Copy(graph, nodes: [node]));
        foreach (var section in new[] { "nodes", "properties" })
        {
            var result = await service.InspectAsync(null, true, 80, section, 80, null, null, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain("raw-credential", result.Content, StringComparison.Ordinal);
            Assert.Contains("safe-value", result.Content, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(result.Content);
            var rows = section == "nodes" ? json.RootElement.GetProperty("nodes")[0].GetProperty("properties") : json.RootElement.GetProperty("properties");
            Assert.All(rows.EnumerateArray().Where(row => row.GetProperty("propertyName").GetString() != "Threshold"),
                row => Assert.Equal("<redacted>", row.GetProperty("value").GetString()));
        }
        var notRequested = await service.InspectAsync(null, false, 80, "nodes", 80, null, null, CancellationToken.None);
        using var omitted = JsonDocument.Parse(notRequested.Content);
        Assert.Empty(omitted.RootElement.GetProperty("nodes")[0].GetProperty("properties").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, omitted.RootElement.GetProperty("totalPropertyCount").ValueKind);
    }

    [Fact]
    public async Task CancellationAfterSnapshotCaptureRejectsLateGraphEvidence()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new CopilotFlowGraphInspectionService(token =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult<CopilotFlowContextSnapshot?>(Graph(3));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(null, false, 80, "nodes", 80, null, null, cancellation.Token));
    }

    private static CopilotFlowContextSnapshot Graph(int count, bool largeText = false)
    {
        var nodes = Enumerable.Range(1, count).Select(index => new CopilotFlowNodeContextSnapshot
        {
            InstanceId = new Guid(index, 0, 0, new byte[8]).ToString(), NodeId = "node-" + index,
            Title = largeText ? new string('中', 180) + "😀" : "Node " + index,
            InputPorts = [new() { PortId = "in:0", Name = "IN" }], OutputPorts = [new() { PortId = "out:0", Name = "OUT" }],
            Parameters = [new() { Name = "Threshold", Value = largeText ? "sk-1234567890abcdef1234567890abcdef" + new string('中', 600) : "1" }],
        }).ToArray();
        return new CopilotFlowContextSnapshot
        {
            SourceInstanceId = Source, Revision = Revision, FlowName = "Graph", Nodes = nodes.Reverse().ToArray(),
            Edges = nodes.Zip(nodes.Skip(1), (source, target) => new CopilotFlowEdgeContextSnapshot
            {
                SourceNodeId = source.InstanceId, SourcePortId = "out:0", TargetNodeId = target.InstanceId, TargetPortId = "in:0",
                SourcePortName = largeText ? new string('中', 128) : "OUT", TargetPortName = largeText ? new string('文', 128) : "IN",
            }).ToArray(),
        };
    }
    private static CopilotFlowContextSnapshot Copy(CopilotFlowContextSnapshot source, IReadOnlyList<CopilotFlowNodeContextSnapshot>? nodes = null,
        IReadOnlyList<CopilotFlowEdgeContextSnapshot>? edges = null, string? revision = null, string? sourceId = null) => new()
    {
        SourceInstanceId = sourceId ?? source.SourceInstanceId, Revision = revision ?? source.Revision,
        FlowName = source.FlowName, Nodes = nodes ?? source.Nodes, Edges = edges ?? source.Edges,
    };
    private static CopilotFlowGraphInspectionService Service(CopilotFlowContextSnapshot graph) => new(_ => Task.FromResult<CopilotFlowContextSnapshot?>(graph));
    private static string EdgeKey(CopilotFlowEdgeContextSnapshot edge) => string.Join("|", edge.SourceNodeId, edge.SourcePortId, edge.TargetNodeId, edge.TargetPortId);
    private static string EdgeKey(JsonElement edge) => string.Join("|", edge.GetProperty("sourceNodeId").GetString(), edge.GetProperty("sourcePortId").GetString(), edge.GetProperty("targetNodeId").GetString(), edge.GetProperty("targetPortId").GetString());
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var json = JsonDocument.Parse(result.Content);
        return json.RootElement.GetProperty("next_cursor").GetString();
    }
}
