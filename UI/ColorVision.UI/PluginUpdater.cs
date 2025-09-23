#pragma warning disable CS8604
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace ColorVision.UI
{
    public static class PluginUpdater
    {
        public static void DeletePlugin(string PackageName)
        {
            ConfigService.Instance.SaveConfigs();

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }

            Directory.CreateDirectory(tempDirectory);
            // 创建批处理文件内容
            string batchFilePath = Path.Combine(tempDirectory, "update.bat");
            string programPluginsDirectory = AppDomain.CurrentDomain.BaseDirectory + "Plugins";

            string targetPluginDirectory = Path.Combine(programPluginsDirectory, PackageName);

            string? executableName = Path.GetFileName(Environment.ProcessPath);

            string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
setlocal

rem 设置要删除的目录路径
set targetDirectory=""{targetPluginDirectory}""

rem 检查目录是否存在
if exist %targetDirectory% (
    echo 正在删除目录: %targetDirectory%
    rd /s /q %targetDirectory%
    echo 删除完成。
) else (
    echo 目录不存在: %targetDirectory%
)

endlocal
start """" ""{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executableName)}"" -c MenuPluginManager
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";
            File.WriteAllText(batchFilePath, batchContent);

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
        /// 更新插件：
        /// 1. 解压 ZIP 到 %TEMP%\\ColorVisionPluginsUpdate\\ColorVision\\
        /// 2. 规范化多语言目录（en, fr, ja, ko, ru, zh-Hant）到 ColorVision 根目录
        /// 3. 生成批处理，用管理员权限结束进程并整体复制 ColorVision 目录覆盖现有
        /// </summary>
        /// <param name="packageName">插件包名称（可用于定制逻辑）</param>
        /// <param name="downloadPath">下载的 ZIP 文件完整路径</param>
        public static void UpdatePlugin(string downloadPath)
        {
            try
            {
                // 1. 保存配置（原逻辑）
                ConfigService.Instance.SaveConfigs();

                // 2. 定义临时与目标路径
                string tempRoot = Path.Combine(Path.GetTempPath(), "ColorVisionPluginsUpdate");
                string staging1Root = Path.Combine(tempRoot, "ColorVision"); // 期望的新的解压根
                string stagingRoot = Path.Combine(tempRoot, "ColorVision","Plugins"); // 期望的新的解压根
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;     // 程序当前目录（包含 ColorVision 子目录假设）
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string exeName = Path.GetFileName(exePath);

                if (string.IsNullOrEmpty(exeName))
                    throw new InvalidOperationException("无法确定当前执行文件名。");

                // 3. 清理旧临时目录
                SafeDeleteDirectory(tempRoot);

                Directory.CreateDirectory(stagingRoot);

                // 4. 解压
                ZipFile.ExtractToDirectory(downloadPath, stagingRoot);

                //// 5. 归并多语言目录到 stagingRoot
                //NormalizeLanguageDirectories(stagingRoot, staging1Root);

                // （可选）如果你只打算更新单个插件，可以在这里仅处理 Plugins\\{packageName}，
                // 但当前逻辑是整体 ColorVision 目录结构覆盖更新。

                // 6. 生成批处理
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                GenerateBatchFile(
                    batchFilePath: batchFilePath,
                    stagingRoot: stagingRoot,
                    baseDir: baseDir,
                    exeName: exeName
                );

                // 7. 启动批处理（管理员权限：如果安装在 Program Files 下）
                var psi = new ProcessStartInfo
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempRoot
                };

                if (baseDir.StartsWith(@"C:\Program Files", StringComparison.OrdinalIgnoreCase)
                    || baseDir.StartsWith(@"C:\Program Files (x86)", StringComparison.OrdinalIgnoreCase))
                {
                    psi.Verb = "runas";
                    psi.WindowStyle = ProcessWindowStyle.Normal;
                }

                Process.Start(psi);

                // 8. 退出当前进程，等待批处理替换
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 规范化多语言目录：
        /// 在任意子层级找到名为 en, fr, ja, ko, ru, zh-Hant 的文件夹，将其内容合并提升到 stagingRoot\\{lang}\\
        /// </summary>
        private static void NormalizeLanguageDirectories(string stagingRoot,string stagingRoot1)
        {
            string[] langFolders = { "en", "fr", "ja", "ko", "ru", "zh-Hant" };

            // 确保语言根目录存在（若后续需要合并）
            foreach (var lang in langFolders)
            {
                Directory.CreateDirectory(Path.Combine(stagingRoot1, lang));
            }

            // 遍历所有子目录，找到语言目录
            var allDirs = Directory.GetDirectories(stagingRoot, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(d => d.Length) // 先处理更深层，避免移动后路径改变影响枚举
                                   .ToList();

            foreach (var dir in allDirs)
            {
                string name = Path.GetFileName(dir);
                if (langFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    string targetLangDir = Path.Combine(stagingRoot1, name);

                    // 如果本身就是根语言目录，跳过
                    if (string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar), targetLangDir, StringComparison.Ordinal))
                        continue;

                    // 合并内容到根语言目录
                    MergeDirectory(dir, targetLangDir);

                    // 删除原目录
                    //SafeDeleteDirectory(dir);
                }
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
            bool mirrorMode = false,         // true = 使用 /MIR（危险：删除目标中不存在的文件）
            bool enableBackup = false,       // true = 启用备份
            string backupParentDir = null,   // 为 null 则默认放在 baseDir 的同级目录
            bool useUtf8 = false             // true = UTF-8 (BOM)，否则使用 GBK(936)
        )
        {
            if (string.IsNullOrWhiteSpace(batchFilePath))
                throw new ArgumentException("batchFilePath 不能为空", nameof(batchFilePath));
            if (string.IsNullOrWhiteSpace(baseDir))
                throw new ArgumentException("baseDir 不能为空", nameof(baseDir));
            if (string.IsNullOrWhiteSpace(exeName))
                throw new ArgumentException("exeName 不能为空", nameof(exeName));

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
            sb.AppendLine($"echo 正在结束进程: {exeName}");
            sb.AppendLine($"taskkill /f /im \"{exeName}\" >nul 2>nul");
            sb.AppendLine("timeout /t 1 /nobreak >nul");
            sb.AppendLine();
            sb.AppendLine("echo 开始复制新版本文件...");
            sb.AppendLine("rem STAGE 指向临时目录中解压出的 ColorVision 内容根");
            sb.AppendLine("set \"STAGE=%~dp0ColorVision\"");
            sb.AppendLine($"set \"TARGET={escapedBaseDir}\"");
            sb.AppendLine();

            if (enableBackup)
            {
                sb.AppendLine("echo 正在备份当前版本...");
                sb.AppendLine("if not exist \"%TARGET%\" (");
                sb.AppendLine("  echo 目标目录不存在，跳过备份。");
                sb.AppendLine(") else (");
                sb.AppendLine("  for /f \"tokens=1-5 delims=/-: .\" %%a in (\"%date% %time%\") do (");
                sb.AppendLine("     set \"BKSTAMP=%%a%%b%%c_%%d%%e\"");
                sb.AppendLine("  )");
                sb.AppendLine($"  set \"BKDIR={EscapeForBatch(backupParentDir).TrimEnd('\\')}\\ColorVisionBackup_%BKSTAMP%\"");
                sb.AppendLine("  echo 备份到：%BKDIR%");
                sb.AppendLine("  mkdir \"%BKDIR%\" >nul 2>nul");
                sb.AppendLine("  where robocopy >nul 2>nul");
                sb.AppendLine("  if %ERRORLEVEL% EQU 0 (");
                sb.AppendLine("     robocopy \"%TARGET%\" \"%BKDIR%\" /E /NFL /NDL /NP /NJH /NJS >nul");
                sb.AppendLine("  ) else (");
                sb.AppendLine("     xcopy /y /e /i \"%TARGET%\\*\" \"%BKDIR%\\\" >nul");
                sb.AppendLine("  )");
                sb.AppendLine("  echo 备份完成。");
                sb.AppendLine(")");
                sb.AppendLine();
            }

            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if %ERRORLEVEL% EQU 0 (");
            sb.AppendLine($"  robocopy \"%STAGE%\" \"%TARGET%\" *.* {copySwitch} /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("  set RC=%ERRORLEVEL%");
            sb.AppendLine("  rem Robocopy 0-7 视为成功");
            sb.AppendLine("  if !RC! GEQ 8 (");
            sb.AppendLine("     echo Robocopy 失败，错误码 !RC! ，尝试使用 XCOPY 回退...");
            sb.AppendLine("     goto fallback_copy");
            sb.AppendLine("  ) else (");
            sb.AppendLine("     goto copy_done");
            sb.AppendLine("  )");
            sb.AppendLine(") else (");
            sb.AppendLine("  echo 未找到 Robocopy，回退到 XCOPY...");
            sb.AppendLine("  goto fallback_copy");
            sb.AppendLine(")");
            sb.AppendLine();

            sb.AppendLine(":fallback_copy");
            sb.AppendLine("echo 使用 XCOPY 回退复制...");
            sb.AppendLine("xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if %ERRORLEVEL% NEQ 0 (");
            sb.AppendLine("  echo XCOPY 复制失败，更新中止。");
            sb.AppendLine("  pause");
            sb.AppendLine("  exit /b 1");
            sb.AppendLine(")");
            sb.AppendLine("goto copy_done");
            sb.AppendLine();

            sb.AppendLine(":copy_done");
            sb.AppendLine("echo 复制完成。");
            sb.AppendLine();

            sb.AppendLine("echo 正在重新启动程序...");
            sb.AppendLine($"start \"\" \"{escapedExePath}\" -c MenuPluginManager");
            sb.AppendLine();

            sb.AppendLine("echo 安排延迟清理临时目录与脚本...");
            sb.AppendLine("set \"SELF=%~f0\"");
            sb.AppendLine("set \"SELF_DIR=%~dp0\"");
            sb.AppendLine("start \"\" cmd /c \"ping -n 4 127.0.0.1 >nul & rd /s /q \\\"%SELF_DIR%\\\" 2>nul & del /q \\\"%SELF%\\\" 2>nul\"");
            sb.AppendLine();

            sb.AppendLine("echo 更新完成。（清理在后台进行）");
            sb.AppendLine("endlocal");
            sb.AppendLine("exit /b 0");

            var encoding = useUtf8
                ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)  // BOM，避免某些中文控制台乱码
                : Encoding.GetEncoding(936);

            File.WriteAllText(batchFilePath, sb.ToString(), encoding);
        }
    }
}
