using System;
using System.IO;
using System.IO.Pipes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ServiceHost
{
    public static class ServiceHostProtocol
    {
        public const string ServiceName = "ColorVisionServiceHost";
        public const string DisplayName = "ColorVision Service Host";
        public const string PipeName = "ColorVisionServiceHost";
        public const string ExecutableName = "ColorVisionServiceHost.exe";
        public const string Description = "Runs local ColorVision maintenance and privileged operation requests.";

        public static JsonSerializerSettings JsonSettings { get; } = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static Encoding Encoding { get; } = new UTF8Encoding(false);

        public static string PackageDirectory => ResolvePackageDirectory();

        public static string PackageExecutablePath => Path.Combine(PackageDirectory, ExecutableName);

        public static string InstallDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost");

        public static string InstalledExecutablePath => Path.Combine(InstallDirectory, ExecutableName);

        public static string ExecutablePath => InstalledExecutablePath;

        private static string OutputPackageDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServiceHost");

        private static string ResolvePackageDirectory()
        {
            string? developerPackageDirectory = FindDeveloperPackageDirectory();
            if (!string.IsNullOrWhiteSpace(developerPackageDirectory))
                return developerPackageDirectory;

            return OutputPackageDirectory;
        }

        private static string? FindDeveloperPackageDirectory()
        {
            DirectoryInfo? directory = new(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 8 && directory != null; i++)
            {
                string binDirectory = Path.Combine(directory.FullName, "src", "ColorVisionServiceHost", "bin");
                string? packageDirectory = FindNewestPackageDirectory(binDirectory);
                if (!string.IsNullOrWhiteSpace(packageDirectory))
                    return packageDirectory;

                directory = directory.Parent;
            }

            return null;
        }

        private static string? FindNewestPackageDirectory(string binDirectory)
        {
            if (!Directory.Exists(binDirectory))
                return null;

            string? newestDirectory = null;
            DateTime newestWriteTime = DateTime.MinValue;
            foreach (string executablePath in Directory.EnumerateFiles(binDirectory, ExecutableName, SearchOption.AllDirectories))
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(executablePath);
                if (newestDirectory == null || writeTime > newestWriteTime)
                {
                    newestDirectory = Path.GetDirectoryName(executablePath);
                    newestWriteTime = writeTime;
                }
            }

            return newestDirectory;
        }
    }

    public sealed class ServiceHostRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");

        public string Command { get; set; } = string.Empty;

        public JToken? Data { get; set; }
    }

    public sealed class ServiceHostResponse
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

    public static class ServiceHostPipeClient
    {
        public static async Task<ServiceHostResponse> SendAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await SendAsync(command, null, timeout, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<ServiceHostResponse> SendAsync(string command, object? data, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Send(command, data, timeout);
            }, cancellationToken).ConfigureAwait(false);
        }

        private static ServiceHostResponse Send(string command, object? data, TimeSpan timeout)
        {
            int timeoutMilliseconds = Math.Max(1, (int)timeout.TotalMilliseconds);
            using NamedPipeClientStream pipe = new(".", ServiceHostProtocol.PipeName, PipeDirection.InOut);
            pipe.Connect(timeoutMilliseconds);
            pipe.ReadMode = PipeTransmissionMode.Byte;

            ServiceHostRequest request = new()
            {
                Command = command,
                Data = data == null ? null : JToken.FromObject(data),
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
    }
}
