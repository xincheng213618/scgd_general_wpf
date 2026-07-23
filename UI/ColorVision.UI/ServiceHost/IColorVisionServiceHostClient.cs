using Microsoft.Win32;
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

        Task<ServiceHostResponse> PrepareApplicationUpdateAsync(string? serviceHostPackageDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> BeginApplicationUpdateScanProtectionAsync(string updateRoot, int lifetimeSeconds = 180, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> CompleteApplicationUpdateScanProtectionAsync(string protectionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterFileAssociationsAsync(string appPath, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RegisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> UnregisterThumbnailAsync(string appDirectory, string? thumbnailCacheDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> AllowFirewallApplicationAsync(string appPath, string profile, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> SetLocalMachineRegistryValuesAsync(string keyPath, IReadOnlyCollection<ServiceHostRegistryValue> values, IReadOnlyCollection<string>? deleteValueNames = null, RegistryView registryView = RegistryView.Default, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> DeleteLocalMachineRegistryKeyAsync(string keyPath, bool recursive = false, RegistryView registryView = RegistryView.Default, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RepairMySqlServiceAsync(string serviceName, string mysqldExePath, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> InstallMySqlFromZipAsync(string serviceName, string zipFilePath, string targetDirectory, int port, string rootPassword, string appUser, string appPassword, string database, int timeoutSeconds = 120, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> InstallServiceAsync(string serviceName, string executablePath, string? displayName = null, string? description = null, string startType = "delayed-auto", bool startAfterInstall = false, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> UninstallServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> StartServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> StopServiceAsync(string serviceName, int timeoutSeconds = 45, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> RestartServiceAsync(string serviceName, int timeoutSeconds = 60, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> TerminateServiceAsync(string serviceName, string? executablePath = null, int timeoutSeconds = 20, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> GetCom0ComStatusAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> ListCom0ComPairsAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> CreateCom0ComPairAsync(int? portA = null, int? portB = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<ServiceHostResponse> DeleteCom0ComPairAsync(int pairNumber, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

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

        public Task<ServiceHostResponse> PrepareApplicationUpdateAsync(string? serviceHostPackageDirectory = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            TimeSpan requestTimeout = timeout ?? TimeSpan.FromMinutes(2);
            return string.IsNullOrWhiteSpace(serviceHostPackageDirectory)
                ? SendAsync("prepare-application-update", requestTimeout, cancellationToken)
                : SendAsync("prepare-application-update", new { serviceHostPackageDirectory }, requestTimeout, cancellationToken);
        }

        public Task<ServiceHostResponse> BeginApplicationUpdateScanProtectionAsync(string updateRoot, int lifetimeSeconds = 180, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "begin-application-update-scan-protection",
                new { updateRoot, lifetimeSeconds },
                timeout ?? TimeSpan.FromSeconds(30),
                cancellationToken);
        }

        public Task<ServiceHostResponse> CompleteApplicationUpdateScanProtectionAsync(string protectionId, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "complete-application-update-scan-protection",
                new { protectionId },
                timeout ?? TimeSpan.FromSeconds(30),
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

        public Task<ServiceHostResponse> AllowFirewallApplicationAsync(string appPath, string profile, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "firewall-allow-application",
                new { appPath, profile },
                timeout ?? TimeSpan.FromSeconds(15),
                cancellationToken);
        }

        public Task<ServiceHostResponse> SetLocalMachineRegistryValuesAsync(string keyPath, IReadOnlyCollection<ServiceHostRegistryValue> values, IReadOnlyCollection<string>? deleteValueNames = null, RegistryView registryView = RegistryView.Default, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var serializedValues = values.Select(value => new
            {
                value.Name,
                kind = value.Kind.ToString(),
                value.Value,
            }).ToArray();
            return SendAsync(
                "registry-set-values",
                new
                {
                    keyPath,
                    registryView = registryView.ToString(),
                    values = serializedValues,
                    deleteValueNames = deleteValueNames?.ToArray() ?? [],
                },
                timeout ?? TimeSpan.FromSeconds(15),
                cancellationToken);
        }

        public Task<ServiceHostResponse> DeleteLocalMachineRegistryKeyAsync(string keyPath, bool recursive = false, RegistryView registryView = RegistryView.Default, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "registry-delete-key",
                new { keyPath, recursive, registryView = registryView.ToString() },
                timeout ?? TimeSpan.FromSeconds(15),
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

        public Task<ServiceHostResponse> GetCom0ComStatusAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync("com0com-status", timeout ?? TimeSpan.FromSeconds(10), cancellationToken);
        }

        public Task<ServiceHostResponse> ListCom0ComPairsAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync("com0com-list", timeout ?? TimeSpan.FromSeconds(45), cancellationToken);
        }

        public Task<ServiceHostResponse> CreateCom0ComPairAsync(int? portA = null, int? portB = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "com0com-create-pair",
                new { portA, portB },
                timeout ?? TimeSpan.FromMinutes(4),
                cancellationToken);
        }

        public Task<ServiceHostResponse> DeleteCom0ComPairAsync(int pairNumber, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            return SendAsync(
                "com0com-delete-pair",
                new { pairNumber },
                timeout ?? TimeSpan.FromMinutes(4),
                cancellationToken);
        }

        private ServiceHostResponse Send(string command, object? data, TimeSpan timeout)
        {
            string operationId = Guid.NewGuid().ToString("N");
            string? brokerTicket = null;
            if (RequiresBrokerTicket(command))
            {
                ServiceHostRequest ticketRequest = new()
                {
                    OperationId = operationId,
                    Command = "issue-broker-ticket",
                    Data = CreateDataToken(new { command }),
                };
                ServiceHostResponse ticketResponse = SendRaw(ticketRequest, timeout);
                if (!ticketResponse.Success || string.IsNullOrWhiteSpace(ticketResponse.Data?["ticket"]?.ToString()))
                    return ticketResponse;
                brokerTicket = ticketResponse.Data!["ticket"]!.ToString();
            }

            ServiceHostRequest request = new()
            {
                OperationId = operationId,
                Command = command,
                Data = CreateDataToken(data),
                BrokerTicket = brokerTicket,
            };
            return SendRaw(request, timeout);
        }

        private ServiceHostResponse SendRaw(ServiceHostRequest request, TimeSpan timeout)
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

        internal static bool RequiresBrokerTicket(string command)
        {
            return !command.Equals("ping", StringComparison.OrdinalIgnoreCase)
                && !command.Equals("status", StringComparison.OrdinalIgnoreCase)
                && !command.Equals("issue-broker-ticket", StringComparison.OrdinalIgnoreCase)
                && !command.Equals("self-update", StringComparison.OrdinalIgnoreCase)
                && !command.Equals("prepare-application-update", StringComparison.OrdinalIgnoreCase);
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
