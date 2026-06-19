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
        int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
        using NamedPipeClientStream pipe = new(".", ServiceHostConstants.PipeName, PipeDirection.InOut);
        pipe.Connect(timeoutMilliseconds);
        pipe.ReadMode = PipeTransmissionMode.Byte;
        ServiceHostRequest request = new() { Command = command };
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
}
