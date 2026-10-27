using ColorVision.UI;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Copilot.Mcp;

/// <summary>Serializes model-visible previews while retaining the host's original snapshot and value types.</summary>
internal static class CopilotFlowPatchPreviewFormatter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new ParameterConverter(), new RedactedStringConverter() },
    };
    private static readonly string[] Effects = ["Changes only the active Flow editor after approval", "Does not save or run the flow"];

    internal static string Serialize(string revision, object change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return JsonSerializer.Serialize(new
        {
            format = "colorvision.flow-patch-preview.v1", expectedRevision = revision, change, effects = Effects,
        }, Options);
    }

    private sealed class ParameterConverter : JsonConverter<CopilotContextProperty>
    {
        public override CopilotContextProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("Flow preview formatting only writes parameter summaries.");

        public override void Write(Utf8JsonWriter writer, CopilotContextProperty value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("propertyName", value.PropertyName);
            writer.WriteString("name", CopilotMcpAuditLogger.RedactText(value.Name));
            writer.WriteString("value", CopilotFlowParameterRedaction.RedactValue(value));
            writer.WriteEndObject();
        }
    }

    private sealed class RedactedStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("Flow preview formatting only writes strings.");

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(CopilotMcpAuditLogger.RedactText(value));
    }
}
