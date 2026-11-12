using ColorVision.Engine.Services;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotDeviceServicesServiceTests
{
    private static readonly string Owner = new('a', 32);
    private static readonly string Revision = new('b', 64);

    [Fact]
    public async Task BudgetedDirectoryPagesUseTheLastDeliveredReferenceWithoutRepeatingOrSkippingRows()
    {
        var rows = Enumerable.Range(1, 100).Select(index => Row(index) with
        {
            ContextSourceId = new string('文', 128), Code = "password=\"alpha, beta\"; " + new string('中', 256), Name = new string('中', 256) + "😀",
        }).Reverse().ToArray();
        var service = Service(Snapshot(rows));
        var delivered = new List<string>();
        var cursors = new HashSet<string>();
        string? cursor = null;
        do
        {
            var result = await service.InspectAsync(null, null, cursor == null ? 100 : 3, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotDeviceServicesInspectionService.MaximumContentCharacters);
            Assert.DoesNotContain("alpha", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("beta", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            Assert.Equal(100, root.GetProperty("total_devices").GetInt32());
            Assert.Equal(100, root.GetProperty("matched_devices").GetInt32());
            var page = root.GetProperty("devices").EnumerateArray().ToArray();
            Assert.NotEmpty(page);
            var refs = page.Select(row => row.GetProperty("device_ref").GetString()!).ToArray();
            Assert.Equal(refs.Order(StringComparer.Ordinal), refs);
            Assert.Equal(page.Length, root.GetProperty("coverage").GetProperty("returned_devices").GetInt32());
            if (cursor == null) Assert.True(root.GetProperty("coverage").GetProperty("output_limited").GetBoolean());
            Assert.True(root.GetProperty("coverage").GetProperty("text_redacted").GetBoolean());
            Assert.NotEmpty(result.PartialResultMessage);
            delivered.AddRange(refs);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(rows.Select(row => row.DeviceRef).Order(StringComparer.Ordinal), delivered);
        Assert.Equal(rows.Length, delivered.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task OwnershipInventoryAndExactFocusAreBoundWhileMetadataAndCachedStateMayChangeLive()
    {
        var snapshot = Snapshot([Row(1), Row(2)]);
        var calls = 0;
        var service = new CopilotDeviceServicesInspectionService(_ => { calls++; return Task.FromResult<CopilotDeviceServicesSnapshot?>(snapshot); });
        var first = await service.InspectAsync(null, Owner, 1, null, CancellationToken.None);
        var cursor = Next(first)!;
        snapshot = snapshot with { Devices = [Row(1), Row(2) with { Name = "Changed label", StatusCode = (int)DeviceStatusType.Busy }] };
        var next = await service.InspectAsync(null, Owner.ToUpperInvariant(), 100, cursor, CancellationToken.None);
        Assert.True(next.Success, next.ErrorMessage);
        using (var document = JsonDocument.Parse(next.Content))
            Assert.Equal("Busy", Assert.Single(document.RootElement.GetProperty("devices").EnumerateArray()).GetProperty("status").GetString());
        var focused = await service.InspectAsync(Ref(2), Owner, 100, null, CancellationToken.None);
        Assert.True(focused.Success);
        using (var document = JsonDocument.Parse(focused.Content))
        {
            Assert.Equal(1, document.RootElement.GetProperty("matched_devices").GetInt32());
            Assert.Single(document.RootElement.GetProperty("devices").EnumerateArray());
            Assert.False(document.RootElement.GetProperty("has_more").GetBoolean());
        }
        var previousCalls = calls;
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), Owner, 1, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, new string('c', 32), 1, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(previousCalls, calls);
        snapshot = snapshot with { InventoryRevision = new string('c', 64), Devices = [Row(3) with { ResourceId = 1, Code = Row(1).Code, Name = Row(1).Name }] };
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, Owner, 100, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.NotFound, (await service.InspectAsync(Ref(1), Owner, 40, null, CancellationToken.None)).FailureKind);
        snapshot = snapshot with { SourceInstanceId = new string('d', 32) };
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(3), Owner, 40, null, CancellationToken.None)).FailureKind);
    }

    [Theory]
    [InlineData(CopilotDeviceServiceStatusReadState.Cached, -99, "Unknown", true)]
    [InlineData(CopilotDeviceServiceStatusReadState.Cached, 12345, "Unknown", false)]
    [InlineData(CopilotDeviceServiceStatusReadState.NoExistingService, null, "Unknown", false)]
    [InlineData(CopilotDeviceServiceStatusReadState.Unsupported, null, "Unknown", false)]
    [InlineData(CopilotDeviceServiceStatusReadState.ReadFailed, null, "Unknown", false)]
    public async Task UnknownAndUnsupportedCachedEvidenceNeverBecomesAHealthOrOnlineClaim(CopilotDeviceServiceStatusReadState readState, int? code, string name, bool recognized)
    {
        var result = await Service(Snapshot([Row(1) with { StatusCode = code, StatusReadState = readState }]))
            .InspectAsync(null, null, 40, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.PartialResultMessage);
        using var document = JsonDocument.Parse(result.Content);
        var row = Assert.Single(document.RootElement.GetProperty("devices").EnumerateArray());
        Assert.Equal(code, row.GetProperty("status_code").ValueKind == JsonValueKind.Null ? null : (int?)row.GetProperty("status_code").GetInt32());
        Assert.Equal(name, row.GetProperty("status").GetString());
        Assert.Equal(recognized, row.GetProperty("status_recognized").GetBoolean());
        Assert.Equal(1, document.RootElement.GetProperty("coverage").GetProperty("unknown_status_devices").GetInt32());
        Assert.False(row.TryGetProperty("is_alive", out _));
        Assert.False(row.TryGetProperty("healthy", out _));
        Assert.False(row.TryGetProperty("last_alive_time", out _));
    }

    [Fact]
    public async Task InvalidSelectorsAndMalformedCursorsAreRejectedBeforeCapture()
    {
        var calls = 0;
        var service = new CopilotDeviceServicesInspectionService(_ => { calls++; return Task.FromResult<CopilotDeviceServicesSnapshot?>(Snapshot([Row(1), Row(2)])); });
        var cursor = Next(await service.InspectAsync(null, null, 1, null, CancellationToken.None))!;
        calls = 0;
        var payload = JsonNode.Parse(Decode(cursor))!.AsObject();
        payload["version"] = 2;
        var unknown = Encode(payload.ToJsonString());
        var duplicate = Encode(Encoding.UTF8.GetString(Decode(cursor)).Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal));
        foreach (var invalid in new[] { "", " ", cursor + "=", " " + cursor, new string('x', 4097), unknown, duplicate })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, null, 40, invalid, CancellationToken.None)).FailureKind);
        foreach (var (focus, owner, maximum) in new[] { (Ref(1), (string?)null, 40), (" Camera ", Owner, 40), ((string?)null, " " + Owner, 40), (null, Owner, 0), (null, Owner, 101) })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(focus, owner, maximum, null, CancellationToken.None)).FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("ref")]
    [InlineData("owner")]
    [InlineData("inventory")]
    [InlineData("state")]
    [InlineData("cached-null")]
    [InlineData("unsupported-code")]
    public async Task InconsistentSourceMetadataIsNeverPublished(string kind)
    {
        var snapshot = kind switch
        {
            "duplicate" => Snapshot([Row(1), Row(1)]),
            "ref" => Snapshot([Row(1) with { DeviceRef = "wrong" }]),
            "owner" => Snapshot([Row(1)]) with { SourceInstanceId = "wrong" },
            "inventory" => Snapshot([Row(1)]) with { InventoryRevision = "wrong" },
            "state" => Snapshot([Row(1) with { StatusReadState = (CopilotDeviceServiceStatusReadState)99 }]),
            "cached-null" => Snapshot([Row(1) with { StatusCode = null }]),
            _ => Snapshot([Row(1) with { StatusReadState = CopilotDeviceServiceStatusReadState.Unsupported }]),
        };
        var result = await Service(snapshot).InspectAsync(null, null, 40, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("changed", CopilotToolFailureKind.Validation)]
    [InlineData("invalid", CopilotToolFailureKind.Internal)]
    [InlineData("timeout", CopilotToolFailureKind.Transient)]
    [InlineData("io", CopilotToolFailureKind.Transient)]
    [InlineData("denied", CopilotToolFailureKind.Authorization)]
    public async Task CaptureFailuresStayClassifiedWithoutPrivateExceptionDetails(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotDeviceServicesInspectionService(_ => throw (kind switch
        {
            "changed" => (Exception)new CopilotDeviceServicesSourceChangedException("PRIVATE_DETAIL"),
            "invalid" => new CopilotDeviceServicesSourceInvalidException("PRIVATE_DETAIL"),
            "timeout" => new TimeoutException("PRIVATE_DETAIL"),
            "denied" => new UnauthorizedAccessException("PRIVATE_DETAIL"),
            _ => new IOException("PRIVATE_DETAIL"),
        }));
        var result = await service.InspectAsync(null, null, 40, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE_DETAIL", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableOwnerAndEmptyLoadedRegistryAreDistinct()
    {
        var missing = new CopilotDeviceServicesInspectionService(_ => Task.FromResult<CopilotDeviceServicesSnapshot?>(null));
        Assert.Equal(CopilotToolFailureKind.NotFound, (await missing.InspectAsync(null, null, 40, null, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await missing.InspectAsync(null, Owner, 40, null, CancellationToken.None)).FailureKind);
        var empty = await Service(Snapshot([])).InspectAsync(null, null, 40, null, CancellationToken.None);
        Assert.True(empty.Success);
        Assert.Null(Next(empty));
        using var document = JsonDocument.Parse(empty.Content);
        Assert.Equal(0, document.RootElement.GetProperty("total_devices").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterCaptureCannotPublishALateSnapshot(bool late)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new CopilotDeviceServicesInspectionService(token =>
        {
            calls++;
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult<CopilotDeviceServicesSnapshot?>(Snapshot([Row(1)]));
        });
        if (!late) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(null, null, 40, null, cancellation.Token));
        Assert.Equal(late ? 1 : 0, calls);
    }

    internal static string Ref(int number) => number.ToString("x32");
    internal static CopilotDeviceServiceObservation Row(int number) => new(Ref(number), "device-service:" + number, number,
        "camera-" + number, "Camera " + number, 1, (int)DeviceStatusType.Free, CopilotDeviceServiceStatusReadState.Cached);
    internal static CopilotDeviceServicesSnapshot Snapshot(IReadOnlyList<CopilotDeviceServiceObservation> rows) => new(Owner, Revision,
        new DateTimeOffset(2026, 10, 27, 1, 0, 0, TimeSpan.Zero), rows);
    private static CopilotDeviceServicesInspectionService Service(CopilotDeviceServicesSnapshot snapshot) => new(_ => Task.FromResult<CopilotDeviceServicesSnapshot?>(snapshot));
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static byte[] Decode(string cursor)
    {
        var encoded = cursor["cvds1:".Length..].Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4));
    }
    private static string Encode(string json) => "cvds1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
