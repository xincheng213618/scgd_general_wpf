using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace WindowsServicePlugin.ServiceManager
{
    internal sealed class ColorVisionServiceHostResponse
    {
        public string RequestId { get; set; } = string.Empty;

        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public JToken? Data { get; set; }

        public string ToDisplayText()
        {
            string dataText = Data?.ToString(Formatting.None) ?? "{}";
            return $"{(Success ? "OK" : "FAILED")}: {Message}{Environment.NewLine}{dataText}";
        }
    }

    internal static class ColorVisionServiceHostPipeClient
    {
        private const string PipeName = "ColorVisionServiceHost";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private static readonly Encoding PipeEncoding = new UTF8Encoding(false);

        public static async Task<ColorVisionServiceHostResponse> SendAsync(string command, object? data, TimeSpan timeout)
        {
            return await Task.Run(() => Send(command, data, timeout)).ConfigureAwait(false);
        }

        private static ColorVisionServiceHostResponse Send(string command, object? data, TimeSpan timeout)
        {
            int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
            using NamedPipeClientStream pipe = new(".", PipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Byte;

            var request = new
            {
                requestId = Guid.NewGuid().ToString("N"),
                command,
                data = data == null ? null : JToken.FromObject(data),
            };

            string requestJson = JsonConvert.SerializeObject(request, JsonSettings);
            using StreamWriter writer = new(pipe, PipeEncoding, leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(pipe, PipeEncoding, false, leaveOpen: true);

            writer.WriteLine(requestJson);
            string? responseJson = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(responseJson))
                throw new InvalidOperationException("ColorVisionServiceHost returned an empty response.");

            return JsonConvert.DeserializeObject<ColorVisionServiceHostResponse>(responseJson, JsonSettings)
                ?? throw new InvalidOperationException("ColorVisionServiceHost returned an invalid response.");
        }
    }
}
