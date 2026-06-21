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

        Task<ServiceHostResponse> StatusAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> SelfUpdateAsync(string packageDirectory, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterFileAssociationsAsync(string appPath, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> UnregisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RepairMySqlServiceAsync(string serviceName, string mysqldExePath, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> InstallMySqlFromZipAsync(string serviceName, string zipFilePath, string targetDirectory, int port, string rootPassword, string appUser, string appPassword, string database, int timeoutSeconds = 120, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> InstallServiceAsync(string serviceName, string executablePath, string? displayName = null, string? description = null, string startType = "delayed-auto", bool startAfterInstall = false, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> UninstallServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> StartServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> StopServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RestartServiceAsync(string serviceName, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> TerminateServiceAsync(string serviceName, string? executablePath = null, int timeoutSeconds = 20, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

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
            Task<ServiceHostResponse> sendTask = Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Send(command, data, timeout);
            }, cancellationToken);

            using CancellationTokenSource timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task timeoutTask = Task.Delay(timeout, timeoutTokenSource.Token);
            Task completedTask = await Task.WhenAny(sendTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == sendTask)
            {
                timeoutTokenSource.Cancel();
                return await sendTask.ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"ColorVisionServiceHost command timed out: {command}");
        }

        public Task<ServiceHostResponse> PingAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync("ping", timeout ?? TimeSpan.FromSeconds(3), cancellationToken);
        }

        public Task<ServiceHostResponse> StatusAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync("status", timeout ?? TimeSpan.FromSeconds(3), cancellationToken);
        }

        public Task<ServiceHostResponse> SelfUpdateAsync(string packageDirectory, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "self-update",
                new { packageDirectory },
                timeout ?? TimeSpan.FromSeconds(10),
                cancellationToken);
        }

        public Task<ServiceHostResponse> RegisterFileAssociationsAsync(string appPath, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "register-file-associations",
                new { appPath, timeoutSeconds = 30 },
                timeout ?? TimeSpan.FromSeconds(45),
                cancellationToken);
        }

        public Task<ServiceHostResponse> RegisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "register-thumbnail",
                new { appDirectory, thumbnailCacheDirectory, timeoutSeconds = 30 },
                timeout ?? TimeSpan.FromSeconds(45),
                cancellationToken);
        }

        public Task<ServiceHostResponse> UnregisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "unregister-thumbnail",
                new { appDirectory, thumbnailCacheDirectory, timeoutSeconds = 30 },
                timeout ?? TimeSpan.FromSeconds(45),
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

        public Task<ServiceHostResponse> InstallMySqlFromZipAsync(string serviceName, string zipFilePath, string targetDirectory, int port, string rootPassword, string appUser, string appPassword, string database, int timeoutSeconds = 120, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "install-mysql-from-zip",
                new
                {
                    serviceName,
                    zipFilePath,
                    targetDirectory,
                    port,
                    rootPassword,
                    appUser,
                    appPassword,
                    database,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        public Task<ServiceHostResponse> InstallServiceAsync(string serviceName, string executablePath, string? displayName = null, string? description = null, string startType = "delayed-auto", bool startAfterInstall = false, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-install",
                new
                {
                    serviceName,
                    executablePath,
                    displayName,
                    description,
                    startType,
                    startAfterInstall,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(90),
                cancellationToken);
        }

        public Task<ServiceHostResponse> UninstallServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-uninstall",
                new
                {
                    serviceName,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(90),
                cancellationToken);
        }

        public Task<ServiceHostResponse> StartServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-start",
                new
                {
                    serviceName,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(60),
                cancellationToken);
        }

        public Task<ServiceHostResponse> StopServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-stop",
                new
                {
                    serviceName,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(60),
                cancellationToken);
        }

        public Task<ServiceHostResponse> RestartServiceAsync(string serviceName, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-restart",
                new
                {
                    serviceName,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(90),
                cancellationToken);
        }

        public Task<ServiceHostResponse> TerminateServiceAsync(string serviceName, string? executablePath = null, int timeoutSeconds = 20, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "service-terminate",
                new
                {
                    serviceName,
                    executablePath,
                    timeoutSeconds,
                },
                timeout ?? TimeSpan.FromSeconds(60),
                cancellationToken);
        }

        private ServiceHostResponse Send(string command, object? data, TimeSpan timeout)
        {
            int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
            using NamedPipeClientStream pipe = new(".", _pipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Byte;
            if (pipe.CanTimeout)
            {
                pipe.ReadTimeout = timeoutMilliseconds;
                pipe.WriteTimeout = timeoutMilliseconds;
            }

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
