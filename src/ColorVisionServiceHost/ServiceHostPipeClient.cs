using System.IO.Pipes;
using Newtonsoft.Json;

namespace ColorVisionServiceHost;

internal static class ServiceHostPipeClient
{
    public static async Task<ServiceHostResponse> SendAsync(string command, TimeSpan timeout)
    {
        return await Task.Run(() => Send(command, timeout)).ConfigureAwait(false);
    }

    private static ServiceHostResponse Send(string command, TimeSpan timeout)
    {
        string operationId = Guid.NewGuid().ToString("N");
        string? ticket = null;
        if (RequiresBrokerTicket(command))
        {
            ServiceHostResponse ticketResponse = SendRaw(new ServiceHostRequest
            {
                OperationId = operationId,
                Command = "issue-broker-ticket",
                Data = Newtonsoft.Json.Linq.JToken.FromObject(new { command }, ServiceHostJson.Serializer),
            }, timeout);
            if (!ticketResponse.Success)
                return ticketResponse;
            ticket = ticketResponse.Data?["ticket"]?.ToString();
        }

        return SendRaw(new ServiceHostRequest { OperationId = operationId, Command = command, BrokerTicket = ticket }, timeout);
    }

    private static ServiceHostResponse SendRaw(ServiceHostRequest request, TimeSpan timeout)
    {
        int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
        using NamedPipeClientStream pipe = new(".", ServiceHostConstants.PipeName, PipeDirection.InOut);
        pipe.Connect(timeoutMilliseconds);
        pipe.ReadMode = PipeTransmissionMode.Byte;
        string requestJson = JsonConvert.SerializeObject(request, ServiceHostJson.Settings);

        using StreamWriter writer = new(pipe, ServiceHostJson.Encoding, leaveOpen: true) { AutoFlush = true };
        using StreamReader reader = new(pipe, ServiceHostJson.Encoding, false, leaveOpen: true);
        writer.WriteLine(requestJson);

        string? responseJson = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(responseJson))
            throw new InvalidOperationException("ColorVisionServiceHost returned an empty response.");

        return JsonConvert.DeserializeObject<ServiceHostResponse>(responseJson, ServiceHostJson.Settings)
            ?? throw new InvalidOperationException("ColorVisionServiceHost returned an invalid response.");
    }

    private static bool RequiresBrokerTicket(string command) => !command.Equals("ping", StringComparison.OrdinalIgnoreCase)
        && !command.Equals("status", StringComparison.OrdinalIgnoreCase)
        && !command.Equals("issue-broker-ticket", StringComparison.OrdinalIgnoreCase);
}
