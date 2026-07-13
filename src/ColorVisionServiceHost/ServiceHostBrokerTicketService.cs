using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ColorVisionServiceHost;

public sealed class ServiceHostBrokerTicketService
{
    private sealed class Payload
    {
        public string TicketId { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string OperationId { get; set; } = string.Empty;
        public string UserSid { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ProcessSha256 { get; set; } = string.Empty;
        public long ExpiresAtUnix { get; set; }
    }

    private readonly byte[] _secret = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, byte> _usedTickets = new(StringComparer.Ordinal);

    public string Issue(ServiceHostRequest request, ServiceHostRequestContext context, string command)
    {
        Payload payload = new()
        {
            TicketId = Guid.NewGuid().ToString("N"),
            Command = command,
            OperationId = request.OperationId,
            UserSid = context.UserSid,
            ProcessId = context.ProcessId,
            ProcessSha256 = context.ProcessSha256,
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds(),
        };
        string body = Base64Url(ServiceHostJson.Encoding.GetBytes(JsonConvert.SerializeObject(payload, ServiceHostJson.Settings)));
        string signature = Base64Url(HMACSHA256.HashData(_secret, Encoding.ASCII.GetBytes(body)));
        return body + "." + signature;
    }

    public bool ValidateAndConsume(ServiceHostRequest request, ServiceHostRequestContext context, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(request.BrokerTicket))
        {
            error = "broker_ticket_required";
            return false;
        }
        string[] parts = request.BrokerTicket.Split('.');
        if (parts.Length != 2)
        {
            error = "invalid_broker_ticket";
            return false;
        }
        byte[] expected = HMACSHA256.HashData(_secret, Encoding.ASCII.GetBytes(parts[0]));
        byte[] actual;
        try
        {
            actual = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            error = "invalid_broker_ticket";
            return false;
        }
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            error = "invalid_broker_ticket";
            return false;
        }

        Payload? payload;
        try
        {
            payload = JsonConvert.DeserializeObject<Payload>(ServiceHostJson.Encoding.GetString(Base64UrlDecode(parts[0])), ServiceHostJson.Settings);
        }
        catch
        {
            payload = null;
        }
        if (payload == null
            || payload.ExpiresAtUnix < DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            || !string.Equals(payload.Command, request.Command, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(payload.OperationId, request.OperationId, StringComparison.Ordinal)
            || !string.Equals(payload.UserSid, context.UserSid, StringComparison.Ordinal)
            || payload.ProcessId != context.ProcessId
            || !string.Equals(payload.ProcessSha256, context.ProcessSha256, StringComparison.Ordinal))
        {
            error = "broker_ticket_scope_mismatch_or_expired";
            return false;
        }
        if (!_usedTickets.TryAdd(payload.TicketId, 0))
        {
            error = "broker_ticket_replayed";
            return false;
        }
        return true;
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
