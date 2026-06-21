using System.Diagnostics;
using Microsoft.Win32;
using System.Security.Principal;
using System.ServiceProcess;

namespace ColorVisionServiceHost;

internal sealed class ServiceHostCommandHandler
{
    private static readonly Dictionary<string, string> MaintenanceTaskScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["register-file-associations"] = "RegisterFileAssociations.ps1",
        ["register-thumbnail"] = "RegisterThumbnail.ps1",
        ["unregister-thumbnail"] = "UnregisterThumbnail.ps1",
    };
    private static readonly Dictionary<string, string[]> ServiceProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RegistrationCenterService"] = ["RegWindowsService"],
        ["CVMainService_x64"] = ["CVMainWindowsService_x64"],
        ["CVMainService_dev"] = ["CVMainWindowsService_dev"],
        ["MySQL"] = ["mysqld"],
        ["MySQL57"] = ["mysqld"],
        ["MySQL80"] = ["mysqld"],
        ["mosquitto"] = ["mosquitto"],
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
                "self-update" => SelfUpdate(request),
                "run-maintenance-task" => RunMaintenanceTask(request, GetRequiredDataValue(request, "taskId")),
                "register-file-associations" => RunMaintenanceTask(request, "register-file-associations"),
                "install-mysql-from-zip" => InstallMySqlFromZip(request),
                "install-existing-mysql-service" => RepairMySqlService(request),
                "repair-mysql-service" => RepairMySqlService(request),
                "service-install" => InstallWindowsService(request),
                "service-uninstall" => UninstallWindowsService(request),
                "service-start" => StartWindowsService(request),
                "service-stop" => StopWindowsService(request),
                "service-restart" => RestartWindowsService(request),
                "service-terminate" => TerminateWindowsService(request),
                "register-thumbnail" => RunMaintenanceTask(request, "register-thumbnail"),
                "unregister-thumbnail" => RunMaintenanceTask(request, "unregister-thumbnail"),
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
            processPath = Environment.ProcessPath,
            assemblyVersion = typeof(ServiceHostCommandHandler).Assembly.GetName().Version?.ToString(),
            fileVersion = GetFileVersion(Environment.ProcessPath),
            productVersion = GetProductVersion(Environment.ProcessPath),
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

    private static ServiceHostResponse SelfUpdate(ServiceHostRequest request)
    {
        string packageDirectory = Path.GetFullPath(GetRequiredDataValue(request, "packageDirectory"));
        string sourceExe = Path.Combine(packageDirectory, ServiceHostConstants.ExecutableName);
        if (!File.Exists(sourceExe))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service host package executable was not found: {sourceExe}");

        if (!LooksLikeServiceHostExecutable(sourceExe))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Executable does not look like ColorVisionServiceHost: {sourceExe}");

        string? currentExe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
            return ServiceHostResponse.FromObject(request.RequestId, false, "Unable to resolve current service host executable.");

        Version? sourceVersion = GetExecutableVersion(sourceExe);
        Version? currentVersion = GetExecutableVersion(currentExe);
        if (sourceVersion != null && currentVersion != null && sourceVersion < currentVersion)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Refusing to downgrade service host: {currentVersion} -> {sourceVersion}");

        string updateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost",
            "Updates");
        Directory.CreateDirectory(updateDirectory);

        string scriptPath = Path.Combine(updateDirectory, $"self-update-{Guid.NewGuid():N}.ps1");
        string logPath = Path.Combine(updateDirectory, "self-update.log");
        string script = CreateSelfUpdateScript(packageDirectory, ServiceHostProtocolCompatibleInstallDirectory(), scriptPath, logPath);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.UTF8);

        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File {QuoteForCommandLine(scriptPath)}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = updateDirectory,
        };
        Process.Start(startInfo)?.Dispose();

        ServiceHostLog.Write($"Self update scheduled. Source={packageDirectory}, Script={scriptPath}");
        return ServiceHostResponse.FromObject(request.RequestId, true, "self update scheduled", new
        {
            packageDirectory,
            installedDirectory = ServiceHostProtocolCompatibleInstallDirectory(),
            sourceVersion = sourceVersion?.ToString(),
            currentVersion = currentVersion?.ToString(),
            scriptPath,
            logPath,
        });
    }

    private static ServiceHostResponse RunMaintenanceTask(ServiceHostRequest request, string taskId)
    {
        if (!MaintenanceTaskScripts.TryGetValue(taskId, out string? scriptName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported maintenance task: {taskId}");

        string taskDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Tasks"));
        string scriptPath = Path.GetFullPath(Path.Combine(taskDirectory, scriptName));
        if (!scriptPath.StartsWith(taskDirectory, StringComparison.OrdinalIgnoreCase))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Invalid maintenance task path: {scriptPath}");
        if (!File.Exists(scriptPath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Maintenance task script was not found: {scriptPath}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 45), 5, 180);
        string inputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost",
            "TaskInput");
        Directory.CreateDirectory(inputDirectory);
        string inputPath = Path.Combine(inputDirectory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(inputPath, request.Data?.ToString(Newtonsoft.Json.Formatting.None) ?? "{}", System.Text.Encoding.UTF8);

        try
        {
            ProcessResult result = RunProcess(
                "powershell.exe",
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-InputJsonPath", inputPath],
                null,
                timeoutSeconds * 1000);

            bool success = result.ExitCode == 0;
            ServiceHostLog.Write($"Maintenance task {taskId} completed. ExitCode={result.ExitCode}");
            return ServiceHostResponse.FromObject(request.RequestId, success, success ? "maintenance task completed" : $"maintenance task failed: {result.ExitCode}", new
            {
                taskId,
                scriptPath,
                result,
            });
        }
        finally
        {
            try
            {
                File.Delete(inputPath);
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

    private static ServiceHostResponse InstallMySqlFromZip(ServiceHostRequest request)
    {
        string serviceName = GetOptionalDataValue(request, "serviceName", "MySQL").Trim();
        if (!IsAllowedMySqlServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported MySQL service name: {serviceName}");

        string zipFilePath = Path.GetFullPath(GetRequiredDataValue(request, "zipFilePath"));
        if (!File.Exists(zipFilePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL zip was not found: {zipFilePath}");

        string targetDirectory = Path.GetFullPath(GetRequiredDataValue(request, "targetDirectory"));
        string rootPassword = GetRequiredDataValue(request, "rootPassword");
        string appUser = GetOptionalDataValue(request, "appUser", "cv").Trim();
        string appPassword = GetRequiredDataValue(request, "appPassword");
        string database = GetOptionalDataValue(request, "database", "color_vision_4xx").Trim();
        int port = Math.Clamp(GetOptionalDataInt(request, "port", 3306), 1, 65535);
        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 120), 10, 300);

        if (string.IsNullOrWhiteSpace(appUser))
            appUser = "cv";
        if (string.IsNullOrWhiteSpace(database))
            database = "color_vision_4xx";

        List<string> steps = [];
        List<ProcessResult> processResults = [];

        Directory.CreateDirectory(targetDirectory);
        string? registeredPath = ServiceExists(serviceName) ? GetServiceInstallPath(serviceName) : null;
        RemoveMySqlServiceRegistration(serviceName, registeredPath, timeoutSeconds, steps, processResults);
        KillProcessesByName(["mysqld", "mysql"], steps);

        CleanupMySqlPackageDirectories(zipFilePath, targetDirectory, steps);
        steps.Add($"Extracting MySQL zip: {zipFilePath}");
        System.IO.Compression.ZipFile.ExtractToDirectory(zipFilePath, targetDirectory, overwriteFiles: true);

        string? installBasePath = ResolveExtractedMySqlBasePath(zipFilePath, targetDirectory);
        if (string.IsNullOrWhiteSpace(installBasePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, "MySQL directory was not found after extraction.", new { steps, processResults });

        string binDirectory = Path.Combine(installBasePath, "bin");
        string mysqldExePath = Path.Combine(binDirectory, "mysqld.exe");
        string mysqlExePath = Path.Combine(binDirectory, "mysql.exe");
        string mysqladminExePath = Path.Combine(binDirectory, "mysqladmin.exe");

        if (!File.Exists(mysqldExePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"mysqld.exe was not found: {mysqldExePath}", new { steps, processResults });
        if (!LooksLikeMySqlServerExecutable(mysqldExePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Executable does not look like MySQL Server: {mysqldExePath}", new { steps, processResults });
        if (!File.Exists(mysqlExePath) || !File.Exists(mysqladminExePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL client tools were not found under: {binDirectory}", new { steps, processResults });

        string dataDirectory = Path.Combine(installBasePath, "data");

        steps.Add("Initializing MySQL data directory.");
        ProcessResult initResult = RunProcess(
            mysqldExePath,
            ["--initialize-insecure", $"--basedir={installBasePath}", $"--datadir={dataDirectory}"],
            binDirectory,
            timeoutSeconds * 1000);
        processResults.Add(initResult);
        if (initResult.ExitCode != 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, BuildProcessFailureMessage("MySQL initialization failed", initResult), new { steps, processResults, installBasePath });

        steps.Add($"Installing MySQL service: {serviceName}");
        ProcessResult installResult = RunProcess(mysqldExePath, $"--install {serviceName}", binDirectory, timeoutSeconds * 1000);
        processResults.Add(installResult);
        if (installResult.ExitCode != 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL service install failed: {installResult.ExitCode}", new { steps, processResults, installBasePath });

        WaitForServiceExists(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
        ProcessResult configResult = RunProcess("sc.exe", $"config \"{serviceName}\" start= auto", null, timeoutSeconds * 1000);
        processResults.Add(configResult);

        bool started = StartServiceAndWait(serviceName, timeoutSeconds, steps);
        if (!started)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL service start failed: {serviceName}", new { steps, processResults, installBasePath });

        if (!WaitForMySqlReady(mysqlExePath, port, timeoutSeconds, steps, processResults))
            return ServiceHostResponse.FromObject(request.RequestId, false, "MySQL did not become ready after service start.", new { steps, processResults, installBasePath });

        steps.Add("Setting MySQL root password.");
        ProcessResult rootPasswordResult = RunProcess(
            mysqladminExePath,
            ["-P", port.ToString(), "-u", "root", "password", rootPassword],
            binDirectory,
            timeoutSeconds * 1000);
        processResults.Add(rootPasswordResult with { Arguments = $"-P {port} -u root password ********" });
        if (rootPasswordResult.ExitCode != 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL root password setup failed: {rootPasswordResult.ExitCode}", new { steps, processResults, installBasePath });

        if (!string.IsNullOrWhiteSpace(appUser))
        {
            steps.Add($"Creating MySQL app user: {appUser}");
            string sql = BuildCreateMySqlUserSql(appUser, appPassword, database);
            ProcessResult userResult = RunProcess(
                mysqlExePath,
                ["-P", port.ToString(), "-u", "root", $"-p{rootPassword}", "-e", sql],
                binDirectory,
                timeoutSeconds * 1000);
            processResults.Add(userResult with { Arguments = $"-P {port} -u root -p******** -e ********" });
            if (userResult.ExitCode != 0)
                return ServiceHostResponse.FromObject(request.RequestId, false, $"MySQL app user setup failed: {userResult.ExitCode}", new { steps, processResults, installBasePath });
        }

        string? registeredFinalPath = GetServiceInstallPath(serviceName);
        ServiceHostLog.Write($"MySQL installed from zip: {serviceName}, path={registeredFinalPath}");
        return ServiceHostResponse.FromObject(request.RequestId, true, "MySQL installed from zip", new
        {
            serviceName,
            installBasePath,
            mysqldExePath,
            registeredPath = registeredFinalPath,
            running = IsServiceRunning(serviceName),
            port,
            appUser,
            database,
            steps,
            processResults,
        });
    }

    private static ServiceHostResponse StartWindowsService(ServiceHostRequest request, string? defaultServiceName = null)
    {
        string serviceName = ResolveRequestedServiceName(request, defaultServiceName);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        if (!ServiceExists(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service was not found: {serviceName}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 45), 5, 180);
        List<string> steps = [];
        bool started = StartServiceAndWait(serviceName, timeoutSeconds, steps);
        return ServiceHostResponse.FromObject(request.RequestId, started, started ? "service started" : "service start failed", new
        {
            serviceName,
            running = IsServiceRunning(serviceName),
            steps,
        });
    }

    private static ServiceHostResponse StopWindowsService(ServiceHostRequest request, string? defaultServiceName = null)
    {
        string serviceName = ResolveRequestedServiceName(request, defaultServiceName);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        if (!ServiceExists(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service was not found: {serviceName}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 45), 5, 180);
        List<string> steps = [];
        bool stopped = StopServiceIfExists(serviceName, timeoutSeconds, steps);
        return ServiceHostResponse.FromObject(request.RequestId, stopped, stopped ? "service stopped" : "service stop failed", new
        {
            serviceName,
            running = IsServiceRunning(serviceName),
            steps,
        });
    }

    private static ServiceHostResponse RestartWindowsService(ServiceHostRequest request)
    {
        string serviceName = ResolveRequestedServiceName(request);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        if (!ServiceExists(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service was not found: {serviceName}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 60), 5, 180);
        List<string> steps = [];
        bool stopped = StopServiceIfExists(serviceName, timeoutSeconds, steps);
        bool started = stopped && StartServiceAndWait(serviceName, timeoutSeconds, steps);

        return ServiceHostResponse.FromObject(request.RequestId, started, started ? "service restarted" : "service restart failed", new
        {
            serviceName,
            running = IsServiceRunning(serviceName),
            steps,
        });
    }

    private static ServiceHostResponse TerminateWindowsService(ServiceHostRequest request)
    {
        string serviceName = ResolveRequestedServiceName(request);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 20), 5, 180);
        List<string> steps = [];

        if (ServiceExists(serviceName))
            StopServiceIfExists(serviceName, Math.Min(timeoutSeconds, 30), steps);
        else
            steps.Add($"Service not installed: {serviceName}");

        string? executablePath = request.Data?["executablePath"]?.ToString();
        HashSet<string> processNames = ResolveAllowedProcessNames(serviceName, executablePath);
        if (processNames.Count == 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"No allowed process name was found for service: {serviceName}", new { serviceName, steps });

        int killed = KillProcessesByName(processNames, steps);
        bool running = IsServiceRunning(serviceName);
        bool processStillExists = AnyProcessExists(processNames);
        bool success = !running && !processStillExists;

        return ServiceHostResponse.FromObject(request.RequestId, success, success ? "service terminated" : "service terminate incomplete", new
        {
            serviceName,
            processNames,
            killed,
            running,
            processStillExists,
            steps,
        });
    }

    private static ServiceHostResponse InstallWindowsService(ServiceHostRequest request)
    {
        string serviceName = ResolveRequestedServiceName(request);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        string executablePath = Path.GetFullPath(GetRequiredDataValue(request, "executablePath"));
        if (!File.Exists(executablePath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service executable was not found: {executablePath}");
        if (!string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Service executable must be an .exe file: {executablePath}");

        string displayName = GetOptionalDataValue(request, "displayName", serviceName).Trim();
        string description = GetOptionalDataValue(request, "description", string.Empty).Trim();
        string startType = GetOptionalDataValue(request, "startType", "delayed-auto").Trim();
        bool startAfterInstall = GetOptionalDataBool(request, "startAfterInstall", false);
        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 45), 5, 180);

        if (string.IsNullOrWhiteSpace(displayName))
            displayName = serviceName;
        if (string.IsNullOrWhiteSpace(startType))
            startType = "delayed-auto";

        List<string> steps = [];
        List<ProcessResult> processResults = [];

        if (ServiceExists(serviceName))
        {
            string? registeredPath = GetServiceInstallPath(serviceName);
            if (IsSamePath(registeredPath, executablePath))
            {
                steps.Add($"Service already installed: {serviceName}");
                ProcessResult configExistingResult = RunProcess("sc.exe", ["config", serviceName, "start=", startType], null, timeoutSeconds * 1000);
                processResults.Add(configExistingResult);

                bool alreadyOk = !startAfterInstall || StartServiceAndWait(serviceName, timeoutSeconds, steps);
                return ServiceHostResponse.FromObject(request.RequestId, alreadyOk, alreadyOk ? "service already installed" : "service start failed", new
                {
                    serviceName,
                    executablePath,
                    registeredPath,
                    running = IsServiceRunning(serviceName),
                    steps,
                    processResults,
                });
            }

            steps.Add($"Service path changed: {registeredPath ?? "(unknown)"} -> {executablePath}");
            StopServiceIfExists(serviceName, timeoutSeconds, steps);
            processResults.Add(RunProcess("sc.exe", ["delete", serviceName], null, timeoutSeconds * 1000));
            WaitForServiceDeleted(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
        }

        ProcessResult createResult = RunProcess(
            "sc.exe",
            ["create", serviceName, "binPath=", executablePath, "start=", startType, "DisplayName=", displayName],
            null,
            timeoutSeconds * 1000);
        processResults.Add(createResult);
        if (createResult.ExitCode != 0)
            return ServiceHostResponse.FromObject(request.RequestId, false, $"service install failed: {createResult.ExitCode}", new { serviceName, executablePath, steps, processResults });

        if (!string.IsNullOrWhiteSpace(description))
        {
            ProcessResult descriptionResult = RunProcess("sc.exe", ["description", serviceName, description], null, timeoutSeconds * 1000);
            processResults.Add(descriptionResult);
        }

        WaitForServiceExists(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
        bool exists = ServiceExists(serviceName);
        bool started = !startAfterInstall || StartServiceAndWait(serviceName, timeoutSeconds, steps);

        return ServiceHostResponse.FromObject(request.RequestId, exists && started, exists && started ? "service installed" : "service install incomplete", new
        {
            serviceName,
            executablePath,
            registeredPath = GetServiceInstallPath(serviceName),
            running = IsServiceRunning(serviceName),
            steps,
            processResults,
        });
    }

    private static ServiceHostResponse UninstallWindowsService(ServiceHostRequest request)
    {
        string serviceName = ResolveRequestedServiceName(request);
        if (!IsAllowedServiceName(serviceName))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Unsupported service name: {serviceName}");

        int timeoutSeconds = Math.Clamp(GetOptionalDataInt(request, "timeoutSeconds", 45), 5, 180);
        List<string> steps = [];
        List<ProcessResult> processResults = [];

        if (!ServiceExists(serviceName))
        {
            steps.Add($"Service not installed: {serviceName}");
            return ServiceHostResponse.FromObject(request.RequestId, true, "service not installed", new { serviceName, steps, processResults });
        }

        StopServiceIfExists(serviceName, timeoutSeconds, steps);
        processResults.Add(RunProcess("sc.exe", ["delete", serviceName], null, timeoutSeconds * 1000));
        WaitForServiceDeleted(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));

        bool exists = ServiceExists(serviceName);
        return ServiceHostResponse.FromObject(request.RequestId, !exists, exists ? "service uninstall failed" : "service uninstalled", new
        {
            serviceName,
            exists,
            steps,
            processResults,
        });
    }

    private static void RemoveMySqlServiceRegistration(string serviceName, string? mysqldExePath, int timeoutSeconds, List<string> steps, List<ProcessResult> processResults)
    {
        if (!ServiceExists(serviceName))
        {
            steps.Add($"Service not installed: {serviceName}");
            return;
        }

        StopServiceIfExists(serviceName, timeoutSeconds, steps);

        string? registeredPath = GetServiceInstallPath(serviceName);
        string? removeExePath = !string.IsNullOrWhiteSpace(mysqldExePath) && File.Exists(mysqldExePath)
            ? mysqldExePath
            : registeredPath;

        if (!string.IsNullOrWhiteSpace(removeExePath) && File.Exists(removeExePath))
        {
            string workingDirectory = Path.GetDirectoryName(removeExePath) ?? string.Empty;
            steps.Add($"Removing MySQL service through mysqld.exe: {removeExePath}");
            processResults.Add(RunProcess(removeExePath, $"--remove {serviceName}", workingDirectory, timeoutSeconds * 1000));
        }

        if (ServiceExists(serviceName))
        {
            steps.Add($"Deleting service through sc.exe: {serviceName}");
            processResults.Add(RunProcess("sc.exe", $"delete \"{serviceName}\"", null, timeoutSeconds * 1000));
            WaitForServiceDeleted(serviceName, TimeSpan.FromSeconds(Math.Min(timeoutSeconds, 30)));
        }
    }

    private static int KillProcessesByName(IEnumerable<string> processNames, List<string> steps)
    {
        int killed = 0;
        foreach (string processName in processNames)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    steps.Add($"Killing process: {process.ProcessName} ({process.Id})");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                    killed++;
                }
                catch (Exception ex)
                {
                    steps.Add($"Failed to kill process {processName}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return killed;
    }

    private static void CleanupMySqlPackageDirectories(string zipFilePath, string targetDirectory, List<string> steps)
    {
        foreach (string directoryName in GetMySqlPackageTopLevelDirectoryNames(zipFilePath))
        {
            string directory = Path.Combine(targetDirectory, directoryName);
            if (!Directory.Exists(directory))
                continue;

            steps.Add($"Deleting existing MySQL directory: {directory}");
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string? ResolveExtractedMySqlBasePath(string zipFilePath, string targetDirectory)
    {
        string[] packageDirectories = GetMySqlPackageTopLevelDirectoryNames(zipFilePath)
            .Select(name => Path.Combine(targetDirectory, name))
            .Where(path => File.Exists(Path.Combine(path, "bin", "mysqld.exe")))
            .ToArray();

        if (packageDirectories.Length > 0)
            return packageDirectories[0];

        return Directory.Exists(targetDirectory)
            ? Directory.GetDirectories(targetDirectory, "mysql-*", SearchOption.TopDirectoryOnly)
                .Where(path => File.Exists(Path.Combine(path, "bin", "mysqld.exe")))
                .OrderByDescending(path => path)
                .FirstOrDefault()
            : null;
    }

    private static string[] GetMySqlPackageTopLevelDirectoryNames(string zipFilePath)
    {
        try
        {
            using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zipFilePath);
            return archive.Entries
                .Select(entry => GetTopLevelEntryName(entry.FullName))
                .Where(name => name.StartsWith("mysql-", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string GetTopLevelEntryName(string entryName)
    {
        string normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        string top = normalized.Split('/')[0].Trim();
        return top is "." or ".." ? string.Empty : top;
    }

    private static bool WaitForMySqlReady(string mysqlExePath, int port, int timeoutSeconds, List<string> steps, List<ProcessResult> processResults)
    {
        string workingDirectory = Path.GetDirectoryName(mysqlExePath) ?? string.Empty;
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            ProcessResult result = RunProcess(
                mysqlExePath,
                ["-P", port.ToString(), "-u", "root", "-e", "SELECT 1;"],
                workingDirectory,
                timeoutMilliseconds: 5000);
            processResults.Add(result);
            if (result.ExitCode == 0)
            {
                steps.Add("MySQL is ready.");
                return true;
            }

            Thread.Sleep(1000);
        }

        steps.Add("Timed out waiting for MySQL readiness.");
        return false;
    }

    private static string BuildCreateMySqlUserSql(string userName, string password, string database)
    {
        string safeDb = EscapeSqlIdentifier(database);
        string safeUser = EscapeSqlLiteral(userName);
        string safePassword = EscapeSqlLiteral(password);
        return $"CREATE DATABASE IF NOT EXISTS `{safeDb}` CHARACTER SET utf8mb4; " +
            $"CREATE USER IF NOT EXISTS '{safeUser}'@'localhost' IDENTIFIED BY '{safePassword}'; " +
            $"ALTER USER '{safeUser}'@'localhost' IDENTIFIED BY '{safePassword}'; " +
            $"GRANT ALL PRIVILEGES ON `{safeDb}`.* TO '{safeUser}'@'localhost'; " +
            $"CREATE USER IF NOT EXISTS '{safeUser}'@'%' IDENTIFIED BY '{safePassword}'; " +
            $"ALTER USER '{safeUser}'@'%' IDENTIFIED BY '{safePassword}'; " +
            $"GRANT ALL PRIVILEGES ON `{safeDb}`.* TO '{safeUser}'@'%'; " +
            "FLUSH PRIVILEGES;";
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);
    }

    private static string EscapeSqlIdentifier(string value)
    {
        return value.Replace("`", "``", StringComparison.Ordinal);
    }

    private static string CreateSelfUpdateScript(string packageDirectory, string installDirectory, string scriptPath, string logPath)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'Stop'",
            $"$serviceName = {PsQuote(ServiceHostConstants.ServiceName)}",
            $"$displayName = {PsQuote(ServiceHostConstants.DisplayName)}",
            $"$description = {PsQuote(ServiceHostConstants.Description)}",
            $"$source = {PsQuote(packageDirectory)}",
            $"$destination = {PsQuote(installDirectory)}",
            $"$executableName = {PsQuote(ServiceHostConstants.ExecutableName)}",
            $"$scriptPath = {PsQuote(scriptPath)}",
            $"$logPath = {PsQuote(logPath)}",
            "function Write-Step([string]$message) {",
            "    $line = \"$(Get-Date -Format o) $message\"",
            "    Add-Content -LiteralPath $logPath -Value $line",
            "}",
            "try {",
            "    Write-Step \"Self update started. Source=$source Destination=$destination\"",
            "    $sourceExe = Join-Path $source $executableName",
            "    if (-not (Test-Path -LiteralPath $sourceExe)) { throw \"Service host executable was not found: $sourceExe\" }",
            "    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
            "    if ($service -and $service.Status -ne 'Stopped') {",
            "        Write-Step \"Stopping service $serviceName\"",
            "        Stop-Service -Name $serviceName -Force -ErrorAction Stop",
            "        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))",
            "    }",
            "    New-Item -ItemType Directory -Force -Path $destination | Out-Null",
            "    Write-Step \"Copying service host files\"",
            "    Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force",
            "    $exe = Join-Path $destination $executableName",
            "    if (-not (Test-Path -LiteralPath $exe)) { throw \"Updated service host executable was not found: $exe\" }",
            "    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue",
            "    if ($service) {",
            "        & sc.exe config $serviceName binPath= ('\"' + $exe + '\"') start= auto DisplayName= $displayName | Out-Null",
            "    } else {",
            "        & sc.exe create $serviceName binPath= ('\"' + $exe + '\"') start= auto DisplayName= $displayName | Out-Null",
            "    }",
            "    if ($LASTEXITCODE -ne 0) { throw \"Failed to create or configure service: $LASTEXITCODE\" }",
            "    & sc.exe description $serviceName $description | Out-Null",
            "    if ($LASTEXITCODE -ne 0) { throw \"Failed to set service description: $LASTEXITCODE\" }",
            "    Write-Step \"Starting service $serviceName\"",
            "    Start-Service -Name $serviceName -ErrorAction Stop",
            "    (Get-Service -Name $serviceName).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))",
            "    Write-Step \"Self update completed.\"",
            "    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue",
            "    exit 0",
            "} catch {",
            "    Write-Step (\"Self update failed: \" + $_.Exception.Message)",
            "    exit 1",
            "}",
            string.Empty,
        });
    }

    private static string ServiceHostProtocolCompatibleInstallDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ColorVision",
            "ServiceHost");
    }

    private static string QuoteForCommandLine(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static string PsQuote(string value)
    {
        return $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static string? GetFileVersion(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
    }

    private static string? GetProductVersion(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        return FileVersionInfo.GetVersionInfo(filePath).ProductVersion;
    }

    private static Version? GetExecutableVersion(string filePath)
    {
        return TryParseVersion(GetFileVersion(filePath)) ?? TryParseVersion(GetProductVersion(filePath));
    }

    private static Version? TryParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string versionText = new(value.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
        return Version.TryParse(versionText.Trim('.'), out Version? version) ? version : null;
    }

    private static bool LooksLikeServiceHostExecutable(string filePath)
    {
        if (!string.Equals(Path.GetFileName(filePath), ServiceHostConstants.ExecutableName, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
            string combined = string.Join(" ", info.ProductName, info.FileDescription, info.OriginalFilename);
            return combined.Contains("ColorVision", StringComparison.OrdinalIgnoreCase)
                || combined.Contains("ColorVisionServiceHost", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
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

    private static bool GetOptionalDataBool(ServiceHostRequest request, string name, bool defaultValue)
    {
        string? value = request.Data?[name]?.ToString();
        return bool.TryParse(value, out bool result) ? result : defaultValue;
    }

    private static string ResolveRequestedServiceName(ServiceHostRequest request, string? defaultServiceName = null)
    {
        string serviceName = GetOptionalDataValue(request, "serviceName", defaultServiceName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("Missing request data: serviceName");

        return serviceName;
    }

    private static bool IsAllowedServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName) || serviceName.Length > 256)
            return false;

        return serviceName.IndexOfAny(['\\', '/', '"']) < 0
            && !serviceName.Any(char.IsControl);
    }

    private static bool IsAllowedMySqlServiceName(string serviceName)
    {
        return serviceName is "MySQL" or "MySQL57" or "MySQL80";
    }

    private static HashSet<string> ResolveAllowedProcessNames(string serviceName, string? executablePath)
    {
        HashSet<string> processNames = new(StringComparer.OrdinalIgnoreCase);

        TryAddProcessNameFromPath(processNames, GetServiceInstallPath(serviceName));
        TryAddProcessNameFromPath(processNames, executablePath);

        if (ServiceProcessNames.TryGetValue(serviceName, out string[]? knownNames))
        {
            foreach (string knownName in knownNames)
            {
                if (IsSafeProcessName(knownName))
                    processNames.Add(knownName);
            }
        }

        return processNames;
    }

    private static void TryAddProcessNameFromPath(HashSet<string> processNames, string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return;

        string processName = Path.GetFileNameWithoutExtension(ExtractExecutablePath(executablePath) ?? executablePath.Trim());
        if (IsSafeProcessName(processName))
            processNames.Add(processName);
    }

    private static bool IsSafeProcessName(string processName)
    {
        return !string.IsNullOrWhiteSpace(processName)
            && processName.Length <= 128
            && processName.IndexOfAny(['\\', '/', '"', ':']) < 0
            && !processName.Any(char.IsControl);
    }

    private static bool AnyProcessExists(IEnumerable<string> processNames)
    {
        foreach (string processName in processNames)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                if (processes.Length > 0)
                    return true;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        return false;
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

    private static ProcessResult RunProcess(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null, int timeoutMilliseconds = 30000)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };

        ConfigureProcessEnvironment(process.StartInfo, workingDirectory);

        foreach (string argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            KillProcessTree(process);
            string timeoutOutput = ReadCompletedOutput(outputTask);
            string timeoutError = AppendProcessError(ReadCompletedOutput(errorTask), "Process timed out.");
            return new ProcessResult(fileName, FormatArgumentsForLog(arguments), -1, timeoutOutput, timeoutError);
        }

        string output = ReadCompletedOutput(outputTask);
        string error = ReadCompletedOutput(errorTask);
        return new ProcessResult(fileName, FormatArgumentsForLog(arguments), process.ExitCode, output, error);
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

        ConfigureProcessEnvironment(process.StartInfo, workingDirectory);

        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            KillProcessTree(process);
            string timeoutOutput = ReadCompletedOutput(outputTask);
            string timeoutError = AppendProcessError(ReadCompletedOutput(errorTask), "Process timed out.");
            return new ProcessResult(fileName, arguments, -1, timeoutOutput, timeoutError);
        }

        string output = ReadCompletedOutput(outputTask);
        string error = ReadCompletedOutput(errorTask);
        return new ProcessResult(fileName, arguments, process.ExitCode, output, error);
    }

    private static void ConfigureProcessEnvironment(ProcessStartInfo startInfo, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return;

        string path = startInfo.Environment.TryGetValue("PATH", out string? currentPath)
            ? currentPath ?? string.Empty
            : Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        if (!path.Split(Path.PathSeparator).Any(item => IsSamePath(item, workingDirectory)))
            startInfo.Environment["PATH"] = workingDirectory + Path.PathSeparator + path;
    }

    private static string BuildProcessFailureMessage(string message, ProcessResult result)
    {
        string explanation = ExplainProcessExitCode(result.ExitCode);
        return string.IsNullOrWhiteSpace(explanation)
            ? $"{message}: {result.ExitCode}"
            : $"{message}: {result.ExitCode} ({explanation})";
    }

    private static string ExplainProcessExitCode(int exitCode)
    {
        return exitCode switch
        {
            unchecked((int)0xC0000135) => "缺少运行时 DLL，通常是 Visual C++ Redistributable 或 MySQL bin 目录依赖未加载",
            unchecked((int)0xC000007B) => "程序或依赖 DLL 架构不匹配，可能混用了 x86/x64 组件",
            _ => string.Empty
        };
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Failed to kill timed out process {process.StartInfo.FileName}: {ex.Message}");
        }
    }

    private static string ReadCompletedOutput(Task<string> outputTask)
    {
        try
        {
            return outputTask.Wait(1000) ? outputTask.GetAwaiter().GetResult() : string.Empty;
        }
        catch (Exception ex)
        {
            return $"Failed to read process output: {ex.Message}";
        }
    }

    private static string AppendProcessError(string currentError, string message)
    {
        return string.IsNullOrWhiteSpace(currentError)
            ? message
            : currentError.TrimEnd() + Environment.NewLine + message;
    }

    private static string FormatArgumentsForLog(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(argument =>
            argument.Any(char.IsWhiteSpace) || argument.Contains('"', StringComparison.Ordinal)
                ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
                : argument));
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
