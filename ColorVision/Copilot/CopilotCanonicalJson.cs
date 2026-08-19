using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal static class CopilotCanonicalJson
    {
        public static string Serialize(JsonElement value)
        {
            var buffer = new ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer))
                Write(writer, value);
            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        public static void Write(Utf8JsonWriter writer, JsonElement value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            WriteCore(writer, value);
        }

        private static void WriteCore(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject()
                        .OrderBy(property => property.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCore(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        WriteCore(writer, item);
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    break;
                default:
                    value.WriteTo(writer);
                    break;
            }
        }
    }
}
