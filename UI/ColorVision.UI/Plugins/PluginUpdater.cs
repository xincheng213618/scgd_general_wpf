#pragma warning disable CS8604
#pragma warning disable CA1863
using ColorVision.Update;
using ColorVision.Common.Utilities;
using log4net;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace ColorVision.UI.Plugins
{
    public static class PluginUpdater
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginUpdater));

        /// <summary>
        /// Deletes one or more plugins.
        /// </summary>
        /// <param name="packageNames">The package names of the plugins to delete.</param>
        public static void DeletePlugin(params string[] packageNames)
        {
            if (packageNames == null || packageNames.Length == 0) return;

            ConfigService.Instance.SaveConfigs();
            PluginLoaderrConfig.Instance.Save();

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }

            Directory.CreateDirectory(tempDirectory);
            // 创建批处理文件内容
            string batchFilePath = Path.Combine(tempDirectory, "update.bat");
            string programPluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

            string? executableName = Path.GetFileName(Environment.ProcessPath);

            var batchContentBuilder = new StringBuilder();
            batchContentBuilder.AppendLine($@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
setlocal
");

            foreach (var packageName in packageNames)
            {
                if (string.IsNullOrWhiteSpace(packageName)) continue;
                string targetPluginDirectory = Path.Combine(programPluginsDirectory, packageName);
                batchContentBuilder.AppendLine($@"
rem Set directory path to delete
set targetDirectory=""{targetPluginDirectory}""

rem Check if directory exists
if exist %targetDirectory% (
    echo Deleting directory: %targetDirectory%
    rd /s /q %targetDirectory%
    echo Deletion complete.
) else (
    echo Directory not found: %targetDirectory%
)
");
            }

            batchContentBuilder.AppendLine($@"
endlocal
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
");
            File.WriteAllText(batchFilePath, batchContentBuilder.ToString());

            // 设置批处理文件的启动信息
            ProcessStartInfo startInfo = new()
            {
                FileName = batchFilePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                startInfo.Verb = "runas"; // 请求管理员权限
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }
            Process.Start(startInfo);
            Environment.Exit(0);

        }

        /// <summary>
        /// Updates one or more plugins from their downloaded ZIP archives.
        /// 1. Extracts all ZIPs to a temporary staging directory %TEMP%\\ColorVisionPluginsUpdate\\ColorVision\\Plugins\\.
        /// 2. Generates a batch script to kill the main process, replace the old plugin files with the new ones, and restart the application.
        /// </summary>
        /// <param name="downloadPaths">Full paths to the downloaded plugin ZIP files.</param>
        public static void UpdatePlugin(params string[] downloadPaths) => UpdatePluginWithRestartArguments("-c MenuPluginManager", downloadPaths);

        public static void UpdatePluginWithRestartArguments(string? restartArguments, params string[] downloadPaths)
        {
            if (downloadPaths == null || downloadPaths.Length == 0) return;

            UpdateBackupPrepareResult? backupPrepareResult = null;
            try
            {
                // 1. 保存配置（原逻辑）
                ConfigService.Instance.SaveConfigs();
                PluginLoaderrConfig.Instance.Save();

                // 2. 定义临时与目标路径
                string tempRoot = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
                string stageRoot = Path.Combine(tempRoot, "ColorVision");
                string stagingRoot = Path.Combine(stageRoot, "Plugins"); // Staging for all plugins
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;     // 程序当前目录
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeName = Path.GetFileName(exePath);

                if (string.IsNullOrEmpty(exeName))
                    throw new InvalidOperationException("Cannot determine the current executable file name.");

                // 3. 清理旧临时目录
                SafeDeleteDirectory(tempRoot);
                Directory.CreateDirectory(stagingRoot);

                // 4. 解压所有插件包到同一个临时目录
                foreach (var downloadPath in downloadPaths)
                {
                    if (string.IsNullOrWhiteSpace(downloadPath) || !File.Exists(downloadPath)) continue;
                    ZipFile.ExtractToDirectory(downloadPath, stagingRoot);
                }

                backupPrepareResult = UpdateRecoveryService.Instance.PrepareBackup(
                    stageRoot,
                    baseDir,
                    null,
                    null,
                    Array.Empty<string>(),
                    downloadPaths);

                // 5. 生成批处理
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                GenerateBatchFile(
                    batchFilePath: batchFilePath,
                    stagingRoot: stagingRoot,
                    baseDir: baseDir,
                    exeName: exeName,
                    restartArguments: restartArguments,
                    updateRecovery: backupPrepareResult
                );

                // 6. 启动批处理（管理员权限：如果安装在 Program Files 下）
                var psi = new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempRoot
                };

                if (!Tool.HasWritePermission(AppDomain.CurrentDomain.BaseDirectory))
                {
                    psi.Verb = "runas";
                    psi.WindowStyle = ProcessWindowStyle.Normal;
                }
                Process.Start(psi);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                if (backupPrepareResult != null)
                    UpdateRecoveryService.Instance.MarkFailed($"Plugin update failed before updater batch completed: {ex.Message}");

                log.Error("Plugin update failed before updater batch completed.", ex);
                MessageBox.Show($"Update failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 合并 srcDir 到 destDir，同名文件覆盖
        /// </summary>
        private static void MergeDirectory(string srcDir, string destDir)
        {
            if (!Directory.Exists(srcDir)) return;
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(srcDir, "*", SearchOption.TopDirectoryOnly))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(srcDir, "*", SearchOption.TopDirectoryOnly))
            {
                string subDest = Path.Combine(destDir, Path.GetFileName(dir));
                MergeDirectory(dir, subDest);
            }
        }

        private static void SafeDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // 取消只读属性
                    foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        var attr = File.GetAttributes(file);
                        if (attr.HasFlag(FileAttributes.ReadOnly))
                            File.SetAttributes(file, attr & ~FileAttributes.ReadOnly);
                    }
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // 忽略删除异常
            }
        }

        /// <summary>
        /// 仅做批处理特殊字符转义，不加引号。
        /// </summary>
        private static string EscapeForBatch(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>");
        }

        /// <summary>
        /// 生成更新批处理脚本。
        /// stagingRoot: 临时目录（外面那层，里面包含 ColorVision 目录）
        /// baseDir: 程序目标根目录（复制内容到这里，而不是再套一层 ColorVision）
        /// exeName: 主程序 exe，例如 ColorVision.exe
        /// </summary>
        public static void GenerateBatchFile(
            string batchFilePath,
            string stagingRoot,
            string baseDir,
            string exeName,
            string? restartArguments = "-c MenuPluginManager",
            bool mirrorMode = false,         // true = 使用 /MIR（危险：删除目标中不存在的文件）
            bool enableBackup = false,       // true = 启用备份
            string? backupParentDir = null,   // 为 null 则默认放在 baseDir 的同级目录
            bool useUtf8 = false,             // true = UTF-8 (BOM)，否则使用 GBK(936)
            UpdateBackupPrepareResult? updateRecovery = null
        )
        {
            if (string.IsNullOrWhiteSpace(batchFilePath))
                throw new ArgumentException(Properties.Resources.BatchFilePathCannotBeEmpty, nameof(batchFilePath));
            if (string.IsNullOrWhiteSpace(baseDir))
                throw new ArgumentException(Properties.Resources.BaseDirCannotBeEmpty, nameof(baseDir));
            if (string.IsNullOrWhiteSpace(exeName))
                throw new ArgumentException(Properties.Resources.ExeNameCannotBeEmpty, nameof(exeName));

            // 统一去掉 baseDir 尾部反斜杠，避免双反斜杠、美观问题
            baseDir = baseDir.TrimEnd('\\');

            // robocopy 复制模式
            var copySwitch = mirrorMode ? "/MIR" : "/E";

            // 备份目录处理
            if (enableBackup && string.IsNullOrWhiteSpace(backupParentDir))
            {
                // 默认放到 baseDir 的上级
                backupParentDir = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar)) ?? baseDir;
            }

            var escapedBaseDir = EscapeForBatch(baseDir);
            var escapedExePath = EscapeForBatch(Path.Combine(baseDir, exeName));

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal enabledelayedexpansion");
            sb.AppendLine("title ColorVision Updater");
            sb.AppendLine();
            sb.AppendLine(string.Format(Properties.Resources.EchoTerminatingProcess, exeName));
            sb.AppendLine($"taskkill /f /im \"{exeName}\" >nul 2>nul");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();
            sb.AppendLine(Properties.Resources.EchoStartCopyingFiles);
            sb.AppendLine(Properties.Resources.RemStagePointsToTemp);
            sb.AppendLine("set \"STAGE=%~dp0ColorVision\"");
            sb.AppendLine($"set \"TARGET={escapedBaseDir}\"");
            if (updateRecovery != null)
            {
                sb.AppendLine($"set \"UPDATE_STATE_PATH={EscapeForBatch(updateRecovery.StateFilePath)}\"");
                sb.AppendLine($"set \"STATE_APPLYING={EscapeForBatch(updateRecovery.ApplyingStatePath)}\"");
                sb.AppendLine($"set \"STATE_APPLIED={EscapeForBatch(updateRecovery.AppliedStatePath)}\"");
                sb.AppendLine($"set \"STATE_FAILED={EscapeForBatch(updateRecovery.FailedStatePath)}\"");
                sb.AppendLine($"set \"BACKUP={EscapeForBatch(updateRecovery.BackupPath)}\"");
            }
            else
            {
                sb.AppendLine("set \"UPDATE_STATE_PATH=\"");
                sb.AppendLine("set \"STATE_APPLYING=\"");
                sb.AppendLine("set \"STATE_APPLIED=\"");
                sb.AppendLine("set \"STATE_FAILED=\"");
                sb.AppendLine("set \"BACKUP=\"");
            }
            sb.AppendLine();

            sb.AppendLine("call :mark_state \"%STATE_APPLYING%\"");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine();

            if (enableBackup)
            {
                sb.AppendLine(Properties.Resources.EchoBackingUpCurrentVersion);
                sb.AppendLine("if not exist \"%TARGET%\" (");
                sb.AppendLine(Properties.Resources.EchoTargetDirNotExist);
                sb.AppendLine(") else (");
                sb.AppendLine("  for /f \"tokens=1-5 delims=/-: .\" %%a in (\"%date% %time%\") do (");
                sb.AppendLine("     set \"BKSTAMP=%%a%%b%%c_%%d%%e\"");
                sb.AppendLine("  )");
                sb.AppendLine($"  set \"BKDIR={EscapeForBatch(backupParentDir).TrimEnd('\\')}\\ColorVisionBackup_%BKSTAMP%\"");
                sb.AppendLine(Properties.Resources.EchoBackupTo);
                sb.AppendLine("  mkdir \"%BKDIR%\" >nul 2>nul");
                sb.AppendLine("  where robocopy >nul 2>nul");
                sb.AppendLine("  if %ERRORLEVEL% EQU 0 (");
                sb.AppendLine("     robocopy \"%TARGET%\" \"%BKDIR%\" /E /NFL /NDL /NP /NJH /NJS >nul");
                sb.AppendLine("  ) else (");
                sb.AppendLine("     xcopy /y /e /i \"%TARGET%\\*\" \"%BKDIR%\\\" >nul");
                sb.AppendLine("  )");
                sb.AppendLine(Properties.Resources.EchoBackupComplete);
                sb.AppendLine(")");
                sb.AppendLine();
            }

            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if %ERRORLEVEL% EQU 0 (");
            sb.AppendLine($"  robocopy \"%STAGE%\" \"%TARGET%\" *.* {copySwitch} /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("  set RC=%ERRORLEVEL%");
            sb.AppendLine(Properties.Resources.RemRobocopySuccess);
            sb.AppendLine("  if !RC! GEQ 8 (");
            sb.AppendLine(Properties.Resources.EchoRobocopyFailed);
            sb.AppendLine("     goto fallback_copy");
            sb.AppendLine("  ) else (");
            sb.AppendLine("     goto copy_done");
            sb.AppendLine("  )");
            sb.AppendLine(") else (");
            sb.AppendLine(Properties.Resources.EchoRobocopyNotFound);
            sb.AppendLine("  goto fallback_copy");
            sb.AppendLine(")");
            sb.AppendLine();

            sb.AppendLine(":fallback_copy");
            sb.AppendLine(Properties.Resources.EchoUsingXCOPY);
            sb.AppendLine("xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if %ERRORLEVEL% NEQ 0 (");
            sb.AppendLine(Properties.Resources.EchoXCOPYFailed);
            sb.AppendLine("  goto fail");
            sb.AppendLine(")");
            sb.AppendLine("goto copy_done");
            sb.AppendLine();

            sb.AppendLine(":copy_done");
            sb.AppendLine("call :mark_state \"%STATE_APPLIED%\"");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine(Properties.Resources.EchoCopyComplete);
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoRestartingProgram);
            if (string.IsNullOrWhiteSpace(restartArguments))
            {
                sb.AppendLine($"start \"\" \"{escapedExePath}\"");
            }
            else
            {
                sb.AppendLine($"start \"\" \"{escapedExePath}\" {restartArguments}");
            }
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoSchedulingCleanup);
            sb.AppendLine("set \"SELF=%~f0\"");
            sb.AppendLine("set \"SELF_DIR=%~dp0\"");
            sb.AppendLine("start \"\" cmd /c \"ping -n 4 127.0.0.1 >nul & rd /s /q \\\"%SELF_DIR%\\\" 2>nul & del /q \\\"%SELF%\\\" 2>nul\"");
            sb.AppendLine();

            sb.AppendLine(Properties.Resources.EchoUpdateComplete);
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();

            sb.AppendLine(":fail");
            sb.AppendLine("call :mark_state \"%STATE_FAILED%\"");
            sb.AppendLine("call :rollback");
            if (string.IsNullOrWhiteSpace(restartArguments))
            {
                sb.AppendLine($"start \"\" \"{escapedExePath}\"");
            }
            else
            {
                sb.AppendLine($"start \"\" \"{escapedExePath}\" {restartArguments}");
            }
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 1");
            sb.AppendLine();

            sb.AppendLine(":mark_state");
            sb.AppendLine("if \"%UPDATE_STATE_PATH%\"==\"\" exit /b 0");
            sb.AppendLine("if \"%~1\"==\"\" exit /b 1");
            sb.AppendLine("if not exist \"%~1\" exit /b 1");
            sb.AppendLine("copy /y \"%~1\" \"%UPDATE_STATE_PATH%\" >nul");
            sb.AppendLine("exit /b !ERRORLEVEL!");
            sb.AppendLine();

            sb.AppendLine(":rollback");
            sb.AppendLine("if \"%BACKUP%\"==\"\" exit /b 0");
            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if !ERRORLEVEL! EQU 0 (");
            sb.AppendLine("  if exist \"%BACKUP%\\App\" robocopy \"%BACKUP%\\App\" \"%TARGET%\" *.* /E /NFL /NDL /NP /NJH /NJS /R:2 /W:1 >nul");
            sb.AppendLine("  if exist \"%BACKUP%\\Plugins\" robocopy \"%BACKUP%\\Plugins\" \"%TARGET%\\Plugins\" *.* /E /NFL /NDL /NP /NJH /NJS /R:2 /W:1 >nul");
            sb.AppendLine("  exit /b 0");
            sb.AppendLine(")");
            sb.AppendLine("if exist \"%BACKUP%\\App\" xcopy /y /e /i \"%BACKUP%\\App\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if exist \"%BACKUP%\\Plugins\" xcopy /y /e /i \"%BACKUP%\\Plugins\\*\" \"%TARGET%\\Plugins\\\" >nul");
            sb.AppendLine("exit /b 0");

            var encoding = useUtf8
                ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)  // BOM，避免某些中文控制台乱码
                : Encoding.GetEncoding(936);

            File.WriteAllText(batchFilePath, sb.ToString(), encoding);
        }
    }
}
