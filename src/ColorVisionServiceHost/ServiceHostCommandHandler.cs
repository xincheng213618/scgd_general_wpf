using System.Diagnostics;
using System.Security.Principal;

namespace ColorVisionServiceHost;

internal sealed class ServiceHostCommandHandler
{
    private const string ThumbnailClsid = "{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}";
    private const string ThumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}";
    private static readonly string[] ThumbnailExtensions = { ".cvraw", ".cvcie" };

    private readonly DateTimeOffset _startedAt = DateTimeOffset.Now;

    public ServiceHostResponse Handle(ServiceHostRequest request)
    {
        string command = request.Command.Trim();
        ServiceHostLog.Write($"Command received: {command}");

        try
        {
            ServiceHostResponse response = command.ToLowerInvariant() switch
            {
                "ping" => ServiceHostResponse.FromObject(request.RequestId, true, "pong", new
                {
                    service = ServiceHostConstants.ServiceName,
                    time = DateTimeOffset.Now,
                }),
                "status" => ServiceHostResponse.FromObject(request.RequestId, true, "running", BuildStatus(_startedAt)),
                "write-demo-marker" => WriteDemoMarker(request.RequestId),
                "register-thumbnail" => RegisterThumbnail(request),
                "unregister-thumbnail" => UnregisterThumbnail(request),
                _ => ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported command: {command}"),
            };

            return response;
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Command failed: {command}: {ex}");
            return ServiceHostResponse.FromObject(request.RequestId, false, ex.Message);
        }
    }

    private static object BuildStatus(DateTimeOffset startedAt)
    {
        return new
        {
            service = ServiceHostConstants.ServiceName,
            pipe = ServiceHostConstants.PipeName,
            startedAt,
            processId = Environment.ProcessId,
            machineName = Environment.MachineName,
            identity = WindowsIdentity.GetCurrent().Name,
            isElevated = IsElevated(),
            is64BitProcess = Environment.Is64BitProcess,
            baseDirectory = AppContext.BaseDirectory,
            logFile = ServiceHostLog.LogFilePath,
        };
    }

    private static ServiceHostResponse WriteDemoMarker(string requestId)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost");
        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, "demo-marker.txt");
        string content = string.Join(Environment.NewLine, new[]
        {
            $"Time: {DateTimeOffset.Now:O}",
            $"ProcessId: {Environment.ProcessId}",
            $"Identity: {WindowsIdentity.GetCurrent().Name}",
            $"Elevated: {IsElevated()}",
        });

        File.WriteAllText(filePath, content);
        ServiceHostLog.Write($"Demo marker written: {filePath}");
        return ServiceHostResponse.FromObject(requestId, true, "demo marker written", new { filePath });
    }

    private static ServiceHostResponse RegisterThumbnail(ServiceHostRequest request)
    {
        string appDirectory = GetRequiredDataValue(request, "appDirectory");
        string comHostDll = Path.Combine(appDirectory, "ColorVision.ShellExtension.comhost.dll");
        if (!File.Exists(comHostDll))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Shell extension was not found: {comHostDll}");

        ProcessResult registerResult = RunProcess("regsvr32.exe", $"/s \"{comHostDll}\"");
        if (registerResult.ExitCode != 0)
        {
            return ServiceHostResponse.FromObject(request.RequestId, false, $"regsvr32 failed: {registerResult.ExitCode}", registerResult);
        }

        foreach (string extension in ThumbnailExtensions)
        {
            ProcessResult regResult = RunProcess(
                "reg.exe",
                $"add \"HKCR\\{extension}\\ShellEx\\{ThumbnailProviderIid}\" /ve /d \"{ThumbnailClsid}\" /f");
            if (regResult.ExitCode != 0)
            {
                return ServiceHostResponse.FromObject(request.RequestId, false, $"reg add failed for {extension}: {regResult.ExitCode}", regResult);
            }
        }

        ClearThumbnailCache(request);
        ServiceHostLog.Write($"Thumbnail shell extension registered: {comHostDll}");
        return ServiceHostResponse.FromObject(request.RequestId, true, "thumbnail shell extension registered", new { comHostDll });
    }

    private static ServiceHostResponse UnregisterThumbnail(ServiceHostRequest request)
    {
        string appDirectory = GetRequiredDataValue(request, "appDirectory");
        string comHostDll = Path.Combine(appDirectory, "ColorVision.ShellExtension.comhost.dll");

        foreach (string extension in ThumbnailExtensions)
        {
            RunProcess("reg.exe", $"delete \"HKCR\\{extension}\\ShellEx\\{ThumbnailProviderIid}\" /f");
        }

        if (File.Exists(comHostDll))
        {
            ProcessResult unregisterResult = RunProcess("regsvr32.exe", $"/s /u \"{comHostDll}\"");
            if (unregisterResult.ExitCode != 0)
            {
                return ServiceHostResponse.FromObject(request.RequestId, false, $"regsvr32 unregister failed: {unregisterResult.ExitCode}", unregisterResult);
            }
        }

        ClearThumbnailCache(request);
        ServiceHostLog.Write($"Thumbnail shell extension unregistered: {comHostDll}");
        return ServiceHostResponse.FromObject(request.RequestId, true, "thumbnail shell extension unregistered", new { comHostDll });
    }

    private static string GetRequiredDataValue(ServiceHostRequest request, string name)
    {
        string? value = request.Data?[name]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing request data: {name}");

        return value;
    }

    private static void ClearThumbnailCache(ServiceHostRequest request)
    {
        string? thumbnailCacheDirectory = request.Data?["thumbnailCacheDirectory"]?.ToString();
        if (string.IsNullOrWhiteSpace(thumbnailCacheDirectory) || !Directory.Exists(thumbnailCacheDirectory))
            return;

        foreach (string pattern in new[] { "thumbcache_*.db", "iconcache_*.db" })
        {
            foreach (string file in Directory.GetFiles(thumbnailCacheDirectory, pattern))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    ServiceHostLog.Write($"Failed to delete cache file {file}: {ex.Message}");
                }
            }
        }
    }

    private static ProcessResult RunProcess(string fileName, string arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            return new ProcessResult(fileName, arguments, -1, output, "Process timed out.");
        }

        return new ProcessResult(fileName, arguments, process.ExitCode, output, error);
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private sealed record ProcessResult(string FileName, string Arguments, int ExitCode, string Output, string Error);
}
