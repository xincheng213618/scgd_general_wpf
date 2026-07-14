using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Text;

namespace ColorVisionServiceHost;

public sealed class ServiceHostRequest
{
    public int ProtocolVersion { get; set; } = 2;

    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");

    public string Command { get; set; } = string.Empty;

    public JToken? Data { get; set; }

    public string? BrokerTicket { get; set; }
}

public sealed class ServiceHostRequestContext
{
    public int ProcessId { get; init; }

    public string UserSid { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public string ProcessPath { get; init; } = string.Empty;

    public string ProcessSha256 { get; init; } = string.Empty;
}

internal sealed class ServiceHostResponse
{
    public string RequestId { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public JToken? Data { get; set; }

    public static ServiceHostResponse FromObject(string requestId, bool success, string message, object? data = null)
    {
        JToken? token = data == null
            ? null
            : JToken.FromObject(data, ServiceHostJson.Serializer);

        return new ServiceHostResponse
        {
            RequestId = requestId,
            Success = success,
            Message = message,
            Data = token,
        };
    }

    public string ToDisplayText()
    {
        string dataText = Data?.ToString(Formatting.None) ?? "{}";
        return $"{(Success ? "OK" : "FAILED")}: {Message}{Environment.NewLine}{dataText}";
    }
}

internal static class ServiceHostJson
{
    public static JsonSerializerSettings Settings { get; } = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static JsonSerializer Serializer { get; } = JsonSerializer.Create(Settings);

    public static Encoding Encoding { get; } = new UTF8Encoding(false);
}
