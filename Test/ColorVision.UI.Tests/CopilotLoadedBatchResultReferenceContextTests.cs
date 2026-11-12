using ColorVision.UI;
using System.Text.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace ColorVision.UI.Tests;

public sealed class CopilotLoadedBatchResultReferenceContextTests
{
    private const string Prefix = "Loaded batch result source: ";
    private const string SourceRef = "ABCDEF0123456789ABCDEF0123456789";
    private const string Owner = "FEDCBA9876543210FEDCBA9876543210";

    [Fact]
    public void LegacyIncompleteAndMalformedReferencesDoNotChangeTheExistingContext()
    {
        foreach (var legacy in DeserializeBoth("{\"BatchId\":17,\"Surface\":\"Batch details\",\"SelectedResultId\":42}"))
        {
            Assert.Equal(string.Empty, legacy.LoadedBatchSourceInstanceId);
            Assert.Equal(string.Empty, legacy.LoadedBatchSourceRef);
            Assert.Equal("measurement-results", legacy.SourceId);
            var context = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(legacy);
            Assert.Equal("measurement-results:results", context.Id);
            Assert.Equal("Measurement results · batch 17", context.Title);
            Assert.Contains("Internal result id: 42", context.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(Prefix, context.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("\"result_ref\"", context.Content, StringComparison.Ordinal);
        }

        var expected = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(Snapshot()).Content;
        foreach (var (source, owner) in new (string?, string?)[]
        {
            (SourceRef, string.Empty), (string.Empty, Owner),
            (SourceRef, null), (null, Owner),
            (new string('a', 31) + " ", Owner),
            (SourceRef, new string('a', 31) + "\n"),
            (new string('Ａ', 32), Owner), (SourceRef, new string('g', 32)),
        })
        {
            var actual = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(Snapshot(source, owner));
            Assert.Equal(expected, actual.Content);
        }
    }

    [Fact]
    public void CompleteReferencesSurviveBothJsonSerializersAndProduceOneCanonicalSelectorPair()
    {
        var original = Snapshot(SourceRef, Owner);
        foreach (var json in new[] { JsonSerializer.Serialize(original), JsonConvert.SerializeObject(original) })
        {
            foreach (var snapshot in DeserializeBoth(json))
            {
                Assert.Equal(SourceRef, snapshot.LoadedBatchSourceRef);
                Assert.Equal(Owner, snapshot.LoadedBatchSourceInstanceId);
                Assert.Equal("measurement-results", snapshot.SourceId);
                var context = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(snapshot);
                var line = AssertSelectorLine(context.Content);
                Assert.Equal(CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(Snapshot()).Content,
                    context.Content.Replace(line + Environment.NewLine, string.Empty, StringComparison.Ordinal));
                Assert.DoesNotContain("\"result_ref\"", context.Content, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CompleteSelectorPairPrecedesVariableMetadataAndSurvivesContextTailTruncation()
    {
        var context = CopilotBusinessContextBuilder.BuildMeasurementResultContextItem(new CopilotMeasurementResultContextSnapshot
        {
            LoadedBatchSourceRef = SourceRef,
            LoadedBatchSourceInstanceId = Owner,
            Surface = new string('x', 20000),
            BatchId = 17,
            TemplateName = new string('y', 20000),
            SelectedResultId = 42,
        });

        AssertSelectorLine(context.Content);
        Assert.Contains("<content truncated;", context.Content, StringComparison.Ordinal);
        Assert.True(context.Content.IndexOf(Prefix, StringComparison.Ordinal) < context.Content.IndexOf("Current view: ", StringComparison.Ordinal));
    }

    private static string AssertSelectorLine(string content)
    {
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var line = Assert.Single(lines.Where(value => value.StartsWith(Prefix, StringComparison.Ordinal)));
        Assert.StartsWith("Note: ", lines[1], StringComparison.Ordinal);
        Assert.Equal(line, lines[2]);
        Assert.Equal(Prefix + "{\"source_ref\":\"" + SourceRef.ToLowerInvariant()
            + "\",\"expected_source_instance_id\":\"" + Owner.ToLowerInvariant() + "\"}", line);
        using var document = JsonDocument.Parse(line[Prefix.Length..]);
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.Equal(SourceRef.ToLowerInvariant(), document.RootElement.GetProperty("source_ref").GetString());
        Assert.Equal(Owner.ToLowerInvariant(), document.RootElement.GetProperty("expected_source_instance_id").GetString());
        return line;
    }

    private static CopilotMeasurementResultContextSnapshot Snapshot(string? source = "", string? owner = "") => new()
    {
        LoadedBatchSourceRef = source!,
        LoadedBatchSourceInstanceId = owner!,
        Surface = "Batch details",
        BatchId = 17,
        SelectedResultId = 42,
    };

    private static IEnumerable<CopilotMeasurementResultContextSnapshot> DeserializeBoth(string json)
    {
        yield return JsonSerializer.Deserialize<CopilotMeasurementResultContextSnapshot>(json)!;
        yield return JsonConvert.DeserializeObject<CopilotMeasurementResultContextSnapshot>(json)!;
    }
}
