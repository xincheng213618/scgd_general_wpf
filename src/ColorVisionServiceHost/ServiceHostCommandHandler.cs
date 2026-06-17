using System.Diagnostics;
using Microsoft.Win32;
using System.Security.Principal;
using System.ServiceProcess;

namespace ColorVisionServiceHost;

internal sealed class ServiceHostCommandHandler
{
    private const string CvRawThumbnailClsid = "{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}";
    private const string CvCieThumbnailClsid = "{8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D}";
    private const string ThumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}";
    private const string ApprovedShellExtensionsKey = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved";
    private static readonly IReadOnlyDictionary<string, string> ThumbnailHandlers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".cvraw"] = CvRawThumbnailClsid,
        [".cvcie"] = CvCieThumbnailClsid,
    };

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
                "register-file-associations" => RegisterFileAssociations(request),
                "repair-mysql-service" => RepairMySqlService(request),
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

        foreach ((string extension, string clsid) in ThumbnailHandlers)
        {
            ProcessResult regResult = RunProcess(
                "reg.exe",
                $"add \"HKCR\\{extension}\\ShellEx\\{ThumbnailProviderIid}\" /ve /d \"{clsid}\" /f");
            if (regResult.ExitCode != 0)
            {
                return ServiceHostResponse.FromObject(request.RequestId, false, $"reg add failed for {extension}: {regResult.ExitCode}", regResult);
            }
        }

        foreach ((_, string clsid) in ThumbnailHandlers)
        {
            ProcessResult approvedResult = RunProcess(
                "reg.exe",
                $"add \"{ApprovedShellExtensionsKey}\" /v \"{clsid}\" /t REG_SZ /d \"ColorVision Thumbnail Handler\" /f");
            if (approvedResult.ExitCode != 0)
            {
                return ServiceHostResponse.FromObject(request.RequestId, false, $"reg approved add failed for {clsid}: {approvedResult.ExitCode}", approvedResult);
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

        foreach (string extension in ThumbnailHandlers.Keys)
        {
            RunProcess("reg.exe", $"delete \"HKCR\\{extension}\\ShellEx\\{ThumbnailProviderIid}\" /f");
        }

        foreach ((_, string clsid) in ThumbnailHandlers)
        {
            RunProcess("reg.exe", $"delete \"{ApprovedShellExtensionsKey}\" /v \"{clsid}\" /f");
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

    private static ServiceHostResponse RegisterFileAssociations(ServiceHostRequest request)
    {
        string appPath = GetRequiredDataValue(request, "appPath");
        if (!File.Exists(appPath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"ColorVision executable was not found: {appPath}");

        if (!string.Equals(Path.GetFileName(appPath), "ColorVision.exe", StringComparison.OrdinalIgnoreCase))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unexpected executable name: {appPath}");

        string appDirectory = Path.GetDirectoryName(appPath) ?? throw new InvalidOperationException("Unable to resolve executable directory.");
        string regContent = BuildFileAssociationRegistryContent(appPath, appDirectory);
        string tempRegFile = Path.Combine(Path.GetTempPath(), $"CV_Register_{Guid.NewGuid():N}.reg");

        try
        {
            File.WriteAllText(tempRegFile, regContent, System.Text.Encoding.Unicode);
            ProcessResult importResult = RunProcess("reg.exe", $"import \"{tempRegFile}\"");
            if (importResult.ExitCode != 0)
                return ServiceHostResponse.FromObject(request.RequestId, false, $"reg import failed: {importResult.ExitCode}", importResult);

            ServiceHostLog.Write($"File associations registered for: {appPath}");
            return ServiceHostResponse.FromObject(request.RequestId, true, "file associations registered", new { appPath });
        }
        finally
        {
            try
            {
                File.Delete(tempRegFile);
            }
            catch
            {
            }
        }
    }

    private static ServiceHostResponse RepairMySqlService(ServiceHostRequest request)
    {
        string serviceName = GetOptionalDataValue(request, "serviceName", "MySQL").Trim();
        if (!IsAllowedMySqlServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported MySQL service name: {serviceName}");

        string mysqldExePath = Path.GetFullPath(GetRequiredDataValue(request, "mysqldExePath"));
        if (!File.Exists(mysqldExePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"mysqld.exe was not found: {mysqldExePath}");

        if (!string.Equals(Path.GetFileName(mysqldExePath), "mysqld.exe", StringComparison.OrdinalIgnoreCase))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unexpected MySQL executable: {mysqldExePath}");

        if (!LooksLikeMySqlServerExecutable(mysqldExePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Executable does not look like MySQL Server: {mysqldExePath}");

        int timeoutSeconds = GetOptionalDataInt(request, "timeoutSeconds", 45);
        timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 180);

        string binDirectory = Path.GetDirectoryName(mysqldExePath) ?? throw new InvalidOperationException("Unable to resolve mysqld.exe directory.");
        List<string> steps = [];
        List<ProcessResult> processResults = [];

        bool exists = ServiceExists(serviceName);
        string? registeredPath = exists ? GetServiceInstallPath(serviceName) : null;
        bool samePath = exists && IsSamePath(registeredPath, mysqldExePath);
        string action;

        if (exists && !samePath)
        {
            steps.Add($"Service path changed: {registeredPath ?? "(unknown)"} -> {mysqldExePath}");
            StopServiceIfExists(serviceName, timeoutSeconds, steps);

            ProcessResult removeResult = RunProcess(mysqldExePath, $"--remove {serviceName}", binDirectory, timeoutSeconds * 1000);
            processResults.Add(removeResult);

            ProcessResult deleteResult = RunProcess("sc.exe", $"delete \"{serviceName}\"", null, timeoutSeconds * 1000);
            processResults.Add(deleteResult);

            WaitForServiceDeleted(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
            exists = ServiceExists(serviceName);
            samePath = false;
        }

        if (!exists || !samePath)
        {
            action = exists ? "reinstalled" : "installed";
            steps.Add($"Installing MySQL service: {serviceName}");
            ProcessResult installResult = RunProcess(mysqldExePath, $"--install {serviceName}", binDirectory, timeoutSeconds * 1000);
            processResults.Add(installResult);
            if (installResult.ExitCode != 0)
                return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL service install failed: {installResult.ExitCode}", new { steps, processResults });

            WaitForServiceExists(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
            if (!ServiceExists(serviceName))
                return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL service was not created: {serviceName}", new { steps, processResults });

            ProcessResult configResult = RunProcess("sc.exe", $"config \"{serviceName}\" start= auto", null, timeoutSeconds * 1000);
            processResults.Add(configResult);
        }
        else
        {
            action = "restarted";
            steps.Add($"Restarting MySQL service: {serviceName}");
            StopServiceIfExists(serviceName, timeoutSeconds, steps);
        }

        bool started = StartServiceAndWait(serviceName, timeoutSeconds, steps);
        if (!started)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL service start failed: {serviceName}", new { action, steps, processResults });

        string? finalPath = GetServiceInstallPath(serviceName);
        bool running = IsServiceRunning(serviceName);
        ServiceHostLog.Write($"MySQL service {action}: {serviceName}, path={finalPath}");
        return ServiceHostResponse.FromObject(request.RequestId, true, $"MySQL service {action}", new
        {
            action,
            serviceName,
            mysqldExePath,
            registeredPath = finalPath,
            running,
            steps,
            processResults,
        });
    }

    private static string BuildFileAssociationRegistryContent(string appPath, string appDirectory)
    {
        string iconPath = Path.Combine(appDirectory, "ColorVisionIcons64.dll");
        string comHostPath = Path.Combine(appDirectory, "ColorVision.ShellExtension.comhost.dll");
        string escapedAppPath = EscapeRegistryValue(appPath);
        string escapedIconPath = EscapeRegistryValue(iconPath);
        string escapedComHostPath = EscapeRegistryValue(comHostPath);

        System.Text.StringBuilder sb = new();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();

        AppendPackageExtension(sb, ".cvx", "ColorVision.Launcher.cvx", "ColorVision Core Update Package", escapedAppPath, escapedIconPath, 4, compressed: true, preview: true);
        AppendPackageExtension(sb, ".cvxp", "ColorVision.Launcher.cvxp", "ColorVision Launcher Package", escapedAppPath, escapedIconPath, 5, compressed: true, preview: true);
        AppendPackageExtension(sb, ".lic", "ColorVision.Launcher.lic", "ColorVision Launcher Package", escapedAppPath, escapedIconPath, 6, compressed: false, preview: false);
        AppendPackageExtension(sb, ".cvcal", "ColorVision.Launcher.cvcal", "ColorVision Launcher Package", escapedAppPath, escapedIconPath, 7, compressed: true, preview: true);

        AppendThumbnailComClass(sb, CvRawThumbnailClsid, "ColorVision CVRaw Thumbnail Handler", escapedComHostPath);
        AppendThumbnailComClass(sb, CvCieThumbnailClsid, "ColorVision CVCie Thumbnail Handler", escapedComHostPath);
        AppendApprovedShellExtension(sb, CvRawThumbnailClsid, "ColorVision CVRaw Thumbnail Handler");
        AppendApprovedShellExtension(sb, CvCieThumbnailClsid, "ColorVision CVCie Thumbnail Handler");

        AppendImageExtension(sb, ".cvraw", "ColorVision.Launcher.cvraw", "ColorVision Raw Image File", escapedAppPath, escapedIconPath, 1, CvRawThumbnailClsid);
        AppendImageExtension(sb, ".cvcie", "ColorVision.Launcher.cvcie", "ColorVision CIE Image File", escapedAppPath, escapedIconPath, 2, CvCieThumbnailClsid);
        AppendPackageExtension(sb, ".cvflow", "ColorVision.Launcher.cvflow", "ColorVision Launcher Package", escapedAppPath, escapedIconPath, 3, compressed: false, preview: false);

        return sb.ToString();
    }

    private static void AppendPackageExtension(System.Text.StringBuilder sb, string extension, string progId, string description, string escapedAppPath, string escapedIconPath, int iconIndex, bool compressed, bool preview)
    {
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\{extension}]");
        sb.AppendLine($"@=\"{progId}\"");
        if (compressed)
        {
            sb.AppendLine("\"PerceivedType\"=\"compressed\"");
            sb.AppendLine("\"Content Type\"=\"application/x-zip-compressed\"");
        }
        sb.AppendLine();

        if (compressed)
        {
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{extension}\\OpenWithProgids]");
            sb.AppendLine("\"CompressedFolder\"=\"\"");
            sb.AppendLine();
        }

        sb.AppendLine($"[HKEY_CLASSES_ROOT\\{progId}]");
        sb.AppendLine($"@=\"{description}\"");
        sb.AppendLine();
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\{progId}\\DefaultIcon]");
        sb.AppendLine($"@=\"{escapedIconPath},{iconIndex}\"");
        sb.AppendLine();
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\{progId}\\shell\\open\\command]");
        sb.AppendLine($"@=\"\\\"{escapedAppPath}\\\" -i \\\"%1\\\"\"");
        sb.AppendLine();

        if (preview)
        {
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{progId}\\shell\\preview]");
            sb.AppendLine("@=\"Preview as Winrar\"");
            sb.AppendLine();
            sb.AppendLine($"[HKEY_CLASSES_ROOT\\{progId}\\shell\\preview\\command]");
            sb.AppendLine("@=\"\\\"C:\\\\Program Files\\\\WinRAR\\\\WinRAR.exe\\\" \\\"%1\\\"\"");
            sb.AppendLine();
        }
    }

    private static void AppendThumbnailComClass(System.Text.StringBuilder sb, string clsid, string description, string escapedComHostPath)
    {
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\CLSID\\{clsid}]");
        sb.AppendLine($"@=\"{description}\"");
        sb.AppendLine();
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\CLSID\\{clsid}\\InprocServer32]");
        sb.AppendLine($"@=\"{escapedComHostPath}\"");
        sb.AppendLine("\"ThreadingModel\"=\"Both\"");
        sb.AppendLine();
    }

    private static void AppendApprovedShellExtension(System.Text.StringBuilder sb, string clsid, string description)
    {
        sb.AppendLine("[HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved]");
        sb.AppendLine($"\"{clsid}\"=\"{description}\"");
        sb.AppendLine();
    }

    private static void AppendImageExtension(System.Text.StringBuilder sb, string extension, string progId, string description, string escapedAppPath, string escapedIconPath, int iconIndex, string thumbnailClsid)
    {
        AppendPackageExtension(sb, extension, progId, description, escapedAppPath, escapedIconPath, iconIndex, compressed: false, preview: false);
        sb.AppendLine($"[HKEY_CLASSES_ROOT\\{extension}\\ShellEx\\{ThumbnailProviderIid}]");
        sb.AppendLine($"@=\"{thumbnailClsid}\"");
        sb.AppendLine();
    }

    private static string EscapeRegistryValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal);
    }

    private static string GetRequiredDataValue(ServiceHostRequest request, string name)
    {
        string? value = request.Data?[name]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing request data: {name}");

        return value;
    }

    private static string GetOptionalDataValue(ServiceHostRequest request, string name, string defaultValue)
    {
        string? value = request.Data?[name]?.ToString();
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static int GetOptionalDataInt(ServiceHostRequest request, string name, int defaultValue)
    {
        string? value = request.Data?[name]?.ToString();
        return int.TryParse(value, out int result) ? result : defaultValue;
    }

    private static bool IsAllowedMySqlServiceName(string serviceName)
    {
        return serviceName is "MySQL" or "MySQL57" or "MySQL80";
    }

    private static bool LooksLikeMySqlServerExecutable(string filePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
            string combined = string.Join(" ", info.CompanyName, info.ProductName, info.FileDescription, info.OriginalFilename);
            if (combined.Contains("MySQL", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
        }

        string fullPath = Path.GetFullPath(filePath);
        return fullPath.Contains($"{Path.DirectorySeparatorChar}mysql", StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains($"{Path.AltDirectorySeparatorChar}mysql", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ServiceExists(string serviceName)
    {
        try
        {
            return ServiceController.GetServices().Any(service => string.Equals(service.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            using ServiceController controller = new(serviceName);
            return controller.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private static bool StopServiceIfExists(string serviceName, int timeoutSeconds, List<string> steps)
    {
        try
        {
            using ServiceController controller = new(serviceName);
            if (controller.Status == ServiceControllerStatus.Stopped)
            {
                steps.Add($"Service already stopped: {serviceName}");
                return true;
            }

            if (controller.CanStop)
            {
                controller.Stop();
            }

            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(timeoutSeconds));
            steps.Add($"Service stopped: {serviceName}");
            return controller.Status == ServiceControllerStatus.Stopped;
        }
        catch (Exception ex)
        {
            steps.Add($"Service stop skipped or failed: {serviceName}, {ex.Message}");
            return false;
        }
    }

    private static bool StartServiceAndWait(string serviceName, int timeoutSeconds, List<string> steps)
    {
        try
        {
            using ServiceController controller = new(serviceName);
            if (controller.Status == ServiceControllerStatus.Running)
            {
                steps.Add($"Service already running: {serviceName}");
                return true;
            }

            if (controller.Status == ServiceControllerStatus.Paused)
                controller.Continue();
            else if (controller.Status == ServiceControllerStatus.Stopped)
                controller.Start();

            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(timeoutSeconds));
            steps.Add($"Service started: {serviceName}");
            return controller.Status == ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            steps.Add($"Service start failed: {serviceName}, {ex.Message}");
            return false;
        }
    }

    private static void WaitForServiceExists(string serviceName, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (ServiceExists(serviceName))
                return;

            Thread.Sleep(500);
        }
    }

    private static void WaitForServiceDeleted(string serviceName, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!ServiceExists(serviceName))
                return;

            Thread.Sleep(500);
        }
    }

    private static string? GetServiceInstallPath(string serviceName)
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            string? imagePath = key?.GetValue("ImagePath")?.ToString();
            return ExtractExecutablePath(imagePath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractExecutablePath(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        string expanded = Environment.ExpandEnvironmentVariables(imagePath.Trim());
        if (expanded.StartsWith('"'))
        {
            int closingQuote = expanded.IndexOf('"', 1);
            return closingQuote > 1 ? expanded[1..closingQuote] : expanded.Trim('"');
        }

        int exeIndex = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0 ? expanded[..(exeIndex + 4)] : expanded.Split(' ')[0].Trim('"');
    }

    private static bool IsSamePath(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.Trim('"'), right.Trim('"'), StringComparison.OrdinalIgnoreCase);
        }
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

    private static ProcessResult RunProcess(string fileName, string arguments, string? workingDirectory = null, int timeoutMilliseconds = 30000)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMilliseconds))
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
