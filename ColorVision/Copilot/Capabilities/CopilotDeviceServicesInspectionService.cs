using ColorVision.Copilot.Mcp;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot;

public sealed partial class CopilotDeviceServicesInspectionService
{
    internal const int MaximumContentCharacters = 18_000;
    private readonly Func<CancellationToken, Task<CopilotDeviceServicesSnapshot?>> _read;

    public CopilotDeviceServicesInspectionService(Func<CancellationToken, Task<CopilotDeviceServicesSnapshot?>> read)
        => _read = read ?? throw new ArgumentNullException(nameof(read));

    public async Task<CopilotCapabilityResult> InspectAsync(string? deviceRef, string? expectedSourceInstanceId, int maxDevices, string? cursor, CancellationToken cancellationToken)
    {
        if (maxDevices is < 1 or > 100 || deviceRef != null && !IsHex(deviceRef, 32)
            || expectedSourceInstanceId != null && !IsHex(expectedSourceInstanceId, 32)
            || deviceRef != null && (expectedSourceInstanceId == null || cursor != null))
            return Failure(CopilotToolFailureKind.Validation, "Use max_devices between 1 and 100 and exact 32-hex device_ref/source identifiers. A focus requires expected_source_instance_id and cannot be combined with a cursor.");
        deviceRef = deviceRef?.ToLowerInvariant();
        expectedSourceInstanceId = expectedSourceInstanceId?.ToLowerInvariant();
        Cursor? continuation = null;
        if (cursor != null && (!TryDecode(cursor, out continuation)
            || expectedSourceInstanceId != null && continuation!.SourceInstanceId != expectedSourceInstanceId))
            return InvalidScope();
        cancellationToken.ThrowIfCancellationRequested();
        CopilotDeviceServicesSnapshot? snapshot;
        try
        {
            snapshot = await _read(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotDeviceServicesSourceChangedException) { return InvalidScope(); }
        catch (CopilotDeviceServicesSourceInvalidException) { return InvalidSource(); }
        catch (TimeoutException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The loaded device registry did not provide a snapshot in time. No device was polled or controlled.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(CopilotToolFailureKind.Authorization, "The loaded device registry could not be inspected.");
        }
        catch (IOException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The loaded device registry is temporarily unavailable. No device was polled or controlled.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return InvalidSource();
        }
        if (snapshot == null)
            return continuation != null || expectedSourceInstanceId != null ? InvalidScope()
                : Failure(CopilotToolFailureKind.NotFound, "No loaded device-service registry is available. No manager, device, or UI was created.");
        if (!IsHex(snapshot.SourceInstanceId, 32) || !IsHex(snapshot.InventoryRevision, 64) || snapshot.Devices == null) return InvalidSource();
        var source = snapshot.SourceInstanceId.ToLowerInvariant();
        var inventory = snapshot.InventoryRevision.ToLowerInvariant();
        if (expectedSourceInstanceId != null && expectedSourceInstanceId != source
            || continuation != null && (continuation.SourceInstanceId != source || continuation.InventoryRevision != inventory))
            return InvalidScope();
        var refs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in snapshot.Devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row == null || !IsHex(row.DeviceRef, 32) || !refs.Add(row.DeviceRef)
                || row.ContextSourceId == null || row.Code == null || row.Name == null || !Enum.IsDefined(row.StatusReadState)
                || (row.StatusReadState == CopilotDeviceServiceStatusReadState.Cached) != row.StatusCode.HasValue)
                return InvalidSource();
        }
        if (continuation != null && !refs.Contains(continuation.LastDeviceRef)) return InvalidScope();
        if (deviceRef != null && !refs.Contains(deviceRef))
            return Failure(CopilotToolFailureKind.NotFound, "The exact device_ref is no longer present in this loaded registry. No name, code, resource-id, or current-selection fallback was used.");
        var selected = snapshot.Devices.Where(row => deviceRef == null || string.Equals(row.DeviceRef, deviceRef, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.DeviceRef.ToLowerInvariant(), StringComparer.Ordinal).ToArray();
        var remaining = selected.Where(row => continuation == null || string.CompareOrdinal(row.DeviceRef.ToLowerInvariant(), continuation.LastDeviceRef) > 0).ToArray();
        var page = remaining.Take(deviceRef == null ? maxDevices : 1).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var result = Format(snapshot, page, selected.Length, remaining.Length, deviceRef, continuation != null);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static CopilotCapabilityResult Format(CopilotDeviceServicesSnapshot snapshot, CopilotDeviceServiceObservation[] page, int matchedCount, int remainingCount, string? deviceRef, bool isContinuation)
    {
        var textTruncated = false;
        var textRedacted = false;
        string Text(string value, int maximum)
        {
            var text = CopilotMcpAuditLogger.RedactText(value);
            textRedacted |= text != value;
            if (text.Length <= maximum) return text;
            textTruncated = true;
            if (char.IsHighSurrogate(text[maximum - 1]) && char.IsLowSurrogate(text[maximum])) maximum--;
            return text[..maximum];
        }
        var rows = page.Select(row => new
        {
            device_ref = row.DeviceRef.ToLowerInvariant(), context_source_id = Text(row.ContextSourceId, 128), resource_id = row.ResourceId,
            code = Text(row.Code, 256), name = Text(row.Name, 256), service_type_code = row.ServiceTypeCode,
            service_type = row.ServiceTypeCode.HasValue && Enum.IsDefined((ServiceTypes)row.ServiceTypeCode.Value)
                ? ((ServiceTypes)row.ServiceTypeCode.Value).ToString() : "Unknown",
            status_code = row.StatusCode,
            status = row.StatusCode.HasValue && Enum.IsDefined((DeviceStatusType)row.StatusCode.Value)
                ? ((DeviceStatusType)row.StatusCode.Value).ToString() : "Unknown",
            status_recognized = row.StatusCode.HasValue && Enum.IsDefined((DeviceStatusType)row.StatusCode.Value),
            status_read_state = row.StatusReadState switch
            {
                CopilotDeviceServiceStatusReadState.Cached => "cached", CopilotDeviceServiceStatusReadState.NoExistingService => "no_existing_service",
                CopilotDeviceServiceStatusReadState.Unsupported => "unsupported", _ => "read_failed",
            },
        }).ToList();
        var outputLimited = false;
        bool HasMore() => deviceRef == null && rows.Count < remainingCount;
        string? NextCursor() => !HasMore() || rows.Count == 0 ? null : Encode(new Cursor(
            snapshot.SourceInstanceId.ToLowerInvariant(), snapshot.InventoryRevision.ToLowerInvariant(), rows[^1].device_ref));
        string Serialize() => JsonSerializer.Serialize(new
        {
            source = "current_loaded_device_registry", source_instance_id = snapshot.SourceInstanceId.ToLowerInvariant(),
            inventory_revision = snapshot.InventoryRevision.ToLowerInvariant(), observed_at_utc = snapshot.ObservedAtUtc.ToUniversalTime(),
            total_devices = snapshot.Devices.Count, matched_devices = matchedCount, devices = rows, has_more = HasMore(), next_cursor = NextCursor(),
            interpretation = "Only already-loaded registry metadata and supported built-in cached state were read. Device refs identify member objects only within this host instance; no name, code, resource-id, or selected-device matching is performed. Cached status is not a live probe, online/healthy judgment, heartbeat freshness, readiness guarantee, or measurement result. Unsupported plugins expose metadata only. No configuration, credentials, control command, initialization, reconnect, polling, or UI creation is performed.",
            coverage = new
            {
                returned_devices = rows.Count, text_truncated = textTruncated, text_redacted = textRedacted, output_limited = outputLimited,
                unknown_status_devices = rows.Count(row => !row.status_recognized || row.status == "Unknown"),
                all_matched_devices_returned = rows.Count == matchedCount,
                query_scope = "Each call captures the current loaded registry. Metadata and cached status may change between pages; continuations require the same host and member-object inventory. Removed/replaced objects or another host require restarting. Finishing a page does not prove device health or that unregistered devices do not exist.",
                ordering = "device_ref ordinal ascending; next_cursor resumes after the last row actually delivered",
            },
        });
        var content = Serialize();
        while (content.Length > MaximumContentCharacters && rows.Count > 1)
        {
            rows.RemoveAt(rows.Count - 1);
            outputLimited = true;
            content = Serialize();
        }
        if (content.Length > MaximumContentCharacters) return Failure(CopilotToolFailureKind.Validation, "The device metadata could not fit the bounded output contract; continuation was not advanced.");
        return new CopilotCapabilityResult
        {
            Success = true, Summary = $"Inspected {rows.Count} loaded device-service records.", Content = content,
            PartialResultMessage = isContinuation || HasMore() || textTruncated || textRedacted || rows.Any(row => !row.status_recognized || row.status == "Unknown")
                ? "Only the indicated loaded-device evidence was read; cached or unknown state does not establish live device health. Inspect coverage and use next_cursor when present."
                : string.Empty,
        };
    }

    private static bool IsHex(string? value, int length) => value != null && value.Length == length && value.All(char.IsAsciiHexDigit);
    private static CopilotCapabilityResult InvalidSource() => Failure(CopilotToolFailureKind.Internal, "The loaded device source returned inconsistent identity or cached-state evidence; no device data was published.");
    private static CopilotCapabilityResult InvalidScope() => Failure(CopilotToolFailureKind.Validation, "The device cursor or expected owner is invalid, stale, or belongs to another host or member inventory. Restart the directory before selecting an exact device_ref.");
    private static CopilotCapabilityResult Failure(CopilotToolFailureKind kind, string message) => new()
    {
        Success = false, FailureKind = kind, Summary = "Device services were not inspected.", ErrorMessage = message,
    };
}
