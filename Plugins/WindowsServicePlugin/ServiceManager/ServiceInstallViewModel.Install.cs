using ColorVision.Database;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 安装编排：组件安装、一键安装、服务生命周期管理
    /// </summary>
    public partial class ServiceInstallViewModel
    {
        private async Task ExecuteInstallAsync()
        {
            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool hasAnyComponentChecked = InstallServiceChecked || InstallMySqlChecked || InstallMqttChecked;
            if (!hasAnyComponentChecked)
            {
                MessageBox.Show("请先勾选要安装的组件（服务包、MySQL、MQTT）", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在执行安装...");
            await Task.Run(() =>
            {
                try
                {
                    int progress = 0;

                    // 1. 备份数据库
                    if (BackupBeforeInstall)
                    {
                        SetProgress(progress += 5, "备份数据库...");
                        DoBackupNow();
                    }

                    bool servicesStoppedForInstall = false;

                    // 2. 备份服务文件夹
                    if (BackupServiceBeforeInstall && InstallServiceChecked)
                    {
                        SetProgress(progress += 5, "备份服务文件夹...");
                        StopPackagedServices();
                        servicesStoppedForInstall = true;
                        DoBackupServiceArchiveOnly();

                    }

                    // 3. 安装 MySQL
                    if (InstallMySqlChecked && !string.IsNullOrWhiteSpace(MySqlPackagePath) && File.Exists(MySqlPackagePath))
                    {
                        SetProgress(progress += 15, "安装 MySQL...");
                        string mysqlTarget = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, "Mysql");
                        var serviceManager = ServiceManagerViewModel.Instance;
                        var credentials = serviceManager.CreateFreshMySqlInstallCredentials();
                        var helper = new MySqlServiceHelper
                        {
                            Port = serviceManager.GetConfiguredMySqlPort()
                        };
                        bool mysqlInstalled = helper.InstallFromZipAsync(
                            MySqlPackagePath,
                            mysqlTarget,
                            AddLog,
                            credentials.RootPassword,
                            credentials.AppUser,
                            credentials.AppPassword,
                            credentials.Database).GetAwaiter().GetResult();
                        if (!mysqlInstalled)
                        {
                            throw new InvalidOperationException("MySQL 安装失败");
                        }

                        serviceManager.ApplyInstalledMySqlCredentials(
                            credentials.RootPassword,
                            credentials.AppUser,
                            credentials.AppPassword,
                            credentials.Database,
                            helper.BasePath);
                        AddLog($"MySQL root 密码: {credentials.RootPassword}");
                        AddLog($"MySQL 业务账号: {credentials.AppUser}");
                        AddLog($"MySQL 业务密码: {credentials.AppPassword}");
                    }

                    // 4. 安装 MQTT
                    if (InstallMqttChecked && !string.IsNullOrWhiteSpace(MqttInstallerPath) && File.Exists(MqttInstallerPath))
                    {
                        SetProgress(progress += 15, "安装 MQTT...");
                        InstallMqttFromExe(MqttInstallerPath);
                    }

                    // 5. 安装/更新服务包
                    if (InstallServiceChecked && !string.IsNullOrWhiteSpace(ServicePackagePath) && File.Exists(ServicePackagePath))
                    {
                        if (IsIncrementalUpdatePackage(ServicePackagePath))
                        {
                            // ── 增量更新包：仅覆盖变更文件，保留 cfg/，不重新注册服务 ──
                            SetProgress(progress += 20, "正在应用增量更新...");
                            if (!servicesStoppedForInstall)
                            {
                                StopPackagedServices();
                                servicesStoppedForInstall = true;
                            }
                            ApplyIncrementalUpdate(basePath);
                        }
                        else
                        {
                            // ── 完整安装包：全量解压 + 重新注册服务 ──
                            SetProgress(progress += 20, "安装 CVWindowsService...");
                            if (!servicesStoppedForInstall)
                            {
                                StopPackagedServices();
                                servicesStoppedForInstall = true;
                            }
                            ZipFile.ExtractToDirectory(ServicePackagePath, basePath, true);
                            AddLog("解压服务包完成");

                            string installRoot = ResolveServiceInstallRoot(basePath);
                            AddLog($"服务安装根目录: {installRoot}");
                            DeleteCommonDllAfterUpdate(installRoot);

                            SetProgress(progress += 5, "注册/更新服务...");
                            InstallOrUpdatePackagedServices(installRoot);
                        }
                    }

                    // 6. 执行数据库脚本
                    if (AutoUpdateDatabase)
                    {
                        SetProgress(progress += 15, "执行数据库脚本...");
                        if (!ExecuteColorVisionAllSql(basePath))
                        {
                            throw new InvalidOperationException("执行 color_vision_all.sql 失败");
                        }
                    }

                    // 7. 同步配置
                    SetProgress(progress += 10, "同步配置...");
                    ServiceManagerViewModel.Instance.ApplyConfigAndRefreshAfterInstall();

                    // 8. 启动服务
                    if (AutoStartAfterInstall)
                    {
                        SetProgress(progress += 10, "启动服务...");
                        ServiceManagerViewModel.Instance.OneKeyStartCommand.Execute(null);
                    }
                    else if (servicesStoppedForInstall)
                    {
                        AddLog("安装前已停止服务，当前未自动启动（根据配置）");
                    }

                    CleanupPackDirectory(basePath);

                    SetProgress(100, "安装完成");
                    AddLog("安装完成！");
                }
                catch (Exception ex)
                {
                    AddLog($"安装失败: {ex.Message}");
                    log.Error("安装失败", ex);
                    MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            SetBusy(false);
        }

        private void CleanupPackDirectory(string basePath)
        {
            try
            {
                string packDir = Path.Combine(basePath, "pack");
                if (Directory.Exists(packDir))
                {
                    Directory.Delete(packDir, true);
                    AddLog($"安装后已删除pack目录: {packDir}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"清理pack目录失败: {ex.Message}");
            }
        }

        private async Task OneKeyInstallAllAsync()
        {
            // 选择安装包
            string? zipFile = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "安装包 (*.zip)|*.zip",
                    Title = "选择一键安装包 (FullPackage.zip)"
                };
                if (dlg.ShowDialog() == true)
                    zipFile = dlg.FileName;
            });

            if (string.IsNullOrWhiteSpace(zipFile) || !File.Exists(zipFile))
                return;

            SetBusy(true, "正在解析安装包...");
            await Task.Run(() =>
            {
                try
                {
                    string packDir = Path.Combine(Directory.GetParent(zipFile)!.FullName, Path.GetFileNameWithoutExtension(zipFile));
                    if (Directory.Exists(packDir))
                        Directory.Delete(packDir, true);

                    ZipFile.ExtractToDirectory(zipFile, packDir);
                    AddLog($"解压完成: {packDir}");

                    // 自动识别包内内容，配置路径并勾选
                    string mysqlZip = Path.Combine(packDir, "mysql-5.7.37-winx64.zip");
                    string mqttInstaller = Path.Combine(packDir, "mosquitto-2.0.18-install-windows-x64.exe");
                    string serviceZip = FindServicePackageZip(packDir);
                    bool hasSvc = !string.IsNullOrWhiteSpace(serviceZip) && File.Exists(serviceZip);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (hasSvc)
                        {
                            ServicePackagePath = serviceZip;
                            InstallServiceChecked = true;
                            AddLog($"已应用服务包路径: {ServicePackagePath}");
                        }
                        if (File.Exists(mysqlZip))
                        {
                            MySqlPackagePath = mysqlZip;
                            InstallMySqlChecked = true;
                            AddLog($"已应用 MySQL 包路径: {MySqlPackagePath}");
                        }
                        if (File.Exists(mqttInstaller))
                        {
                            MqttInstallerPath = mqttInstaller;
                            InstallMqttChecked = true;
                            AddLog($"已应用 MQTT 包路径: {MqttInstallerPath}");
                        }
                    });

                    AddLog($"检测到组件: 服务={hasSvc}, MySQL={File.Exists(mysqlZip)}, MQTT={File.Exists(mqttInstaller)}");
                    AddLog("一键安装包解析完成，pack目录将在安装成功后清理");
                }
                catch (Exception ex)
                {
                    AddLog($"解析一键安装包失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        private static string FindServicePackageZip(string packDir)
        {
            if (!Directory.Exists(packDir))
                return string.Empty;

            string exact = Path.Combine(packDir, "CVWindowsService.zip");
            if (File.Exists(exact))
                return exact;

            string[] serviceZipCandidates = Directory.GetFiles(packDir, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return !name.Equals("mysql-5.7.37-winx64.zip", StringComparison.OrdinalIgnoreCase)
                        && name.Contains("CVWindowsService", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();

            if (serviceZipCandidates.Length > 0)
                return serviceZipCandidates[0];

            return string.Empty;
        }

        private void InstallMqttFromExe(string exeFile)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exeFile,
                Verb = "runas",
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            process?.WaitForExit();

            ColorVision.Common.Utilities.Tool.ExecuteCommandAsAdmin("net start mosquitto");
            AddLog("MQTT 服务已启动");
        }

        private void StopPackagedServices()
        {

            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                try
                {
                    if (WinServiceHelper.IsServiceExisted(svc.ServiceName) && WinServiceHelper.IsServiceRunning(svc.ServiceName))
                    {
                        AddLog($"停止服务: {svc.ServiceName}");
                        WinServiceHelper.StopService(svc.ServiceName, 30);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"停止服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }
        }

        private void StartPackagedServices()
        {
            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                try
                {
                    if (WinServiceHelper.IsServiceExisted(svc.ServiceName))
                    {
                        AddLog($"启动服务: {svc.ServiceName}");
                        WinServiceHelper.StartService(svc.ServiceName, 30);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"启动服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }

        }

        private void InstallOrUpdatePackagedServices(string basePath)
        {
            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                if (!svc.IsPackaged)
                    continue;

                string exePath = Path.Combine(basePath, svc.FolderName, svc.GetExecutableName());
                if (!File.Exists(exePath))
                {
                    AddLog($"跳过服务（未找到可执行文件）: {svc.ServiceName}");
                    continue;
                }

                if (WinServiceHelper.IsServiceExisted(svc.ServiceName))
                {
                    try
                    {
                        WinServiceHelper.StopService(svc.ServiceName, 20);
                        Application.Current.Dispatcher.Invoke(() =>  ServiceManagerViewModel.Instance.RefreshAll());
                        
                    }
                    catch
                    {
                    }
                    WinServiceHelper.UninstallService(svc.ServiceName);
                    Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
                }

                bool ok = WinServiceHelper.InstallService(svc.ServiceName, exePath);
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
                AddLog(ok
                    ? $"服务安装成功: {svc.ServiceName}"
                    : $"服务安装失败: {svc.ServiceName}");
            }
        }

        private string ResolveServiceInstallRoot(string basePath)
        {
            string nested = Path.Combine(basePath, "CVWindowsService");
            string[] candidates =
            {
                basePath,
                nested
            };

            string[] markers =
            {
                "RegWindowsService",
                "CVMainWindowsService_x64",
                "CVMainWindowsService_dev"
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                    continue;

                if (markers.Any(m => Directory.Exists(Path.Combine(candidate, m))))
                    return candidate;
            }

            return basePath;
        }

        private bool ExecuteColorVisionAllSql(string basePath)
        {
            string? sqlFilePath = ResolveColorVisionAllSqlPath(basePath);
            if (string.IsNullOrWhiteSpace(sqlFilePath))
            {
                AddLog("未找到 color_vision_all.sql，跳过数据库初始化脚本执行");
                return true;
            }

            var serviceManager = ServiceManagerViewModel.Instance;
            serviceManager.MySqlHelper.Port = serviceManager.GetConfiguredMySqlPort();
            if (!File.Exists(serviceManager.MySqlHelper.MysqlExePath))
            {
                serviceManager.MySqlHelper.DetectFromRegistry();
            }

            var rootCfg = MySqlSetting.Instance.FindProfile(MySqlSetting.RootProfileName);
            string rootPassword = rootCfg?.UserPwd ?? serviceManager.MySqlRootPassword;
            if (string.IsNullOrWhiteSpace(rootPassword))
            {
                AddLog("未找到 root 密码，无法执行 color_vision_all.sql");
                return false;
            }

            AddLog($"执行 SQL: {Path.GetFileName(sqlFilePath)}");
            return serviceManager.MySqlHelper.ExecuteSqlFile("root", rootPassword, null, sqlFilePath, AddLog);
        }

        private string? ResolveColorVisionAllSqlPath(string basePath)
        {
            string installRoot = ResolveServiceInstallRoot(basePath);
            string[] candidates =
            [
                Path.Combine(installRoot, "SQL", "color_vision_all.sql"),
                Path.Combine(basePath, "SQL", "color_vision_all.sql"),
                Path.Combine(basePath, "CVWindowsService", "SQL", "color_vision_all.sql")
            ];

            return candidates.FirstOrDefault(File.Exists);
        }

        private static bool IsLogPath(string fullPath, string rootPath)
        {
            string relative = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
            return relative.StartsWith("log/", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/log/", StringComparison.OrdinalIgnoreCase)
                || relative.Equals("log", StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteCommonDllAfterUpdate(string basePath)
        {
            string commonDllDir = Path.Combine(basePath, "CommonDll");
            try
            {
                if (Directory.Exists(commonDllDir))
                {
                    string[] targets =
                    {
                        Path.Combine(basePath, "RegWindowsService"),
                        Path.Combine(basePath, "CVMainWindowsService_x64"),
                        Path.Combine(basePath, "CVMainWindowsService_dev")
                    };

                    foreach (string target in targets)
                    {
                        if (!Directory.Exists(target))
                        {
                            AddLog($"CommonDll 复制目标目录不存在，跳过: {target}");
                            continue;
                        }

                        int copiedCount = CopyDirectoryRecursive(commonDllDir, target);
                        AddLog($"已复制 CommonDll 到: {target}，文件数: {copiedCount}");
                    }

                    Directory.Delete(commonDllDir, true);
                    AddLog("安装后已删除 CommonDll 目录");
                }
                else
                {
                    AddLog($"未找到 CommonDll 目录: {commonDllDir}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除 CommonDll 失败: {ex.Message}");
            }
        }

        private static int CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            int copiedCount = 0;

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(targetDir, relativePath);
                string? destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
                copiedCount++;
            }

            return copiedCount;
        }
    }
}
