using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.IO.Pipes;

namespace ColorVision.UI.ServiceHost
{
    public interface IColorVisionServiceHostClient
    {
        Task<ServiceHostResponse> SendAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> SendAsync(string command, object? data, TimeSpan timeout, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> PingAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterFileAssociationsAsync(string appPath, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> UnregisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RepairMySqlServiceAsync(string serviceName, string mysqldExePath, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public sealed class ColorVisionServiceHostClient : IColorVisionServiceHostClient
    {
        public static ColorVisionServiceHostClient Default { get; } = new();

        private readonly string _pipeName;

        public ColorVisionServiceHostClient(string pipeName = ServiceHostProtocol.PipeName)
        {
            _pipeName = string.IsNullOrWhiteSpace(pipeName) ? ServiceHostProtocol.PipeName : pipeName;
        }

        public Task<ServiceHostResponse> SendAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return SendAsync(command, null, timeout, cancellationToken);
        }

        public async Task<ServiceHostResponse> SendAsync(string command, object? data, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Send(command, data, timeout);
            }, cancellationToken).ConfigureAwait(false);
        }

        public Task<ServiceHostResponse> PingAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync("ping", timeout ?? TimeSpan.FromSeconds(3), cancellationToken);
        }

        public Task<ServiceHostResponse> RegisterFileAssociationsAsync(string appPath, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "register-file-associations",
                new { appPath },
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken);
        }

        public Task<ServiceHostResponse> RegisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "register-thumbnail",
                new { appDirectory, thumbnailCacheDirectory },
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken);
        }

        public Task<ServiceHostResponse> UnregisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "unregister-thumbnail",
                new { appDirectory, thumbnailCacheDirectory },
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken);
        }

        public Task<ServiceHostResponse> RepairMySqlServiceAsync(string serviceName, string mysqldExePath, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "repair-mysql-service",
                new
                {
                    serviceName,
                    mysqldExePath,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(90),
                cancellationToken);
        }

        private ServiceHostResponse Send(string command, object? data, TimeSpan timeout)
        {
            int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
            using NamedPipeClientStream pipe = new(".", _pipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Byte;

            ServiceHostRequest request = new()
            {
                Command = command,
                Data = CreateDataToken(data),
            };
            string requestJson = JsonConvert.SerializeObject(request, ServiceHostProtocol.JsonSettings);

            using StreamWriter writer = new(pipe, ServiceHostProtocol.Encoding, leaveOpen: true) { AutoFlush = true };
            using StreamReader reader = new(pipe, ServiceHostProtocol.Encoding, false, leaveOpen: true);

            writer.WriteLine(requestJson);
            string? responseJson = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(responseJson))
                throw new InvalidOperationException("ColorVisionServiceHost returned an empty response.");

            return JsonConvert.DeserializeObject<ServiceHostResponse>(responseJson, ServiceHostProtocol.JsonSettings)
                ?? throw new InvalidOperationException("ColorVisionServiceHost returned an invalid response.");
        }

        private static JToken? CreateDataToken(object? data)
        {
            if (data == null)
                return null;

            if (data is JToken token)
                return token;

            return JToken.FromObject(data, JsonSerializer.Create(ServiceHostProtocol.JsonSettings));
        }
    }

    public static class ServiceHostPipeClient
    {
        public static Task<ServiceHostResponse> SendAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return ColorVisionServiceHostClient.Default.SendAsync(command, timeout, cancellationToken);
        }

        public static Task<ServiceHostResponse> SendAsync(string command, object? data, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return ColorVisionServiceHostClient.Default.SendAsync(command, data, timeout, cancellationToken);
        }
    }
}
