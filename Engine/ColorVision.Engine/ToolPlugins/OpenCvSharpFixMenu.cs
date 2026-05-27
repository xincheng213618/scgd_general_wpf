// ==================================================================================
// TEMPORARY WORKAROUND — OpenCvSharpExtern.dll export crash fix
// This menu item downloads and replaces a known-good older version of
// OpenCvSharpExtern.dll to work around an export crash in newer versions.
// The download reuses the existing DownloadWindow + Aria2cDownloadManager
// infrastructure, and the actual DLL replacement is performed by a .cmd script
// that runs after ColorVision exits.
// TODO: Remove this entire file once the upstream OpenCvSharp issue is resolved
//       and the project has been updated to a stable release.
// ==================================================================================

using ColorVision.Common.Utilities;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins
{
    /// <summary>
    /// Temporary workaround: Downloads and replaces OpenCvSharpExtern.dll
    /// to fix an export crash caused by a newer version of the native library.
    /// </summary>
    public class OpenCvSharpFixMenu : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(OpenCvSharpFixMenu));

        // Temporary workaround: hardcoded patch URL and credentials
        private const string PatchUrl = "http://xc213618.ddns.me:9999/D%3A/OpenCvSharpExtern.dll";
        private const string Authorization = "1:1";
        private const string DllFileName = "OpenCvSharpExtern.dll";

        public override string OwnerGuid => "MenuUpdate";
        public override string GuidId => "OpenCvSharpFix";
        public override int Order => 10099; // Just before the Update menu item
        public override string Header => Properties.Resources.FixOpenCvSharpExportCrash;

        public override void Execute()
        {
            string downloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Patches", "OpenCvSharp");

            try
            {
                DownloadWindow.ShowInstance();
                Aria2cDownloadManager.GetInstance().AddDownload(
                    PatchUrl, downloadDir, Authorization, OnDownloadCompleted, DllFileName);
            }
            catch (Exception ex)
            {
                log.Error("OpenCvSharp fix: Failed to start download", ex);
                MessageBox.Show(
                    string.Format(Properties.Resources.OpenCvSharpFixFailed, ex.Message),
                    Properties.Resources.FixOpenCvSharpExportCrash,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OnDownloadCompleted(DownloadTask task)
        {
            if (task.Status != DownloadStatus.Completed)
            {
                log.Warn($"OpenCvSharp fix: Download failed, status={task.Status}, error={task.ErrorMessage}");
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show(
                        string.Format(Properties.Resources.OpenCvSharpFixFailed, task.ErrorMessage ?? task.Status.ToString()),
                        Properties.Resources.FixOpenCvSharpExportCrash,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                return;
            }

            string downloadedDll = task.SavePath;
            log.Info($"OpenCvSharp fix: Download completed: {downloadedDll}");

            Application.Current?.Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    Properties.Resources.OpenCvSharpFixConfirmClose,
                    Properties.Resources.FixOpenCvSharpExportCrash,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                try
                {
                    LaunchPatchCmdAndExit(downloadedDll);
                }
                catch (Exception ex)
                {
                    log.Error("OpenCvSharp fix: Failed to launch patch script", ex);
                    MessageBox.Show(
                        string.Format(Properties.Resources.OpenCvSharpFixFailed, ex.Message),
                        Properties.Resources.FixOpenCvSharpExportCrash,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        }

        private static void LaunchPatchCmdAndExit(string downloadedDll)
        {
            string programDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            string targetDll = Path.Combine(programDir, DllFileName);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupDll = $"{targetDll}.bak-{timestamp}";

            string cmdScript = Path.Combine(
                Path.GetTempPath(), $"OpenCvSharpPatch_{timestamp}.cmd");

            string exePath = Environment.ProcessPath ?? Path.Combine(programDir, "ColorVision.exe");

            // Build .cmd script — all paths quoted to handle spaces
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal");
            sb.AppendLine($"title OpenCvSharp Patch");
            sb.AppendLine($"set \"TARGET_DLL={targetDll}\"");
            sb.AppendLine($"set \"BACKUP_DLL={backupDll}\"");
            sb.AppendLine($"set \"DOWNLOADED_DLL={downloadedDll}\"");
            sb.AppendLine($"set \"EXE_PATH={exePath}\"");
            sb.AppendLine();
            sb.AppendLine(":: Wait for ColorVision process to exit");
            sb.AppendLine(":wait_loop");
            sb.AppendLine($"tasklist /fi \"imagename eq {Path.GetFileName(exePath)}\" | find /i \"{Path.GetFileName(exePath)}\" >nul 2>nul");
            sb.AppendLine("if %errorlevel%==0 (");
            sb.AppendLine("    timeout /t 2 /nobreak >nul");
            sb.AppendLine("    goto wait_loop");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine(":: Backup existing DLL");
            sb.AppendLine("if exist \"%TARGET_DLL%\" (");
            sb.AppendLine("    copy /y \"%TARGET_DLL%\" \"%BACKUP_DLL%\" >nul 2>nul");
            sb.AppendLine("    if %errorlevel% neq 0 (");
            sb.AppendLine("        echo WARNING: Failed to backup existing DLL.");
            sb.AppendLine("    ) else (");
            sb.AppendLine("        echo Backed up to %BACKUP_DLL%");
            sb.AppendLine("    )");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine(":: Replace DLL");
            sb.AppendLine("copy /y \"%DOWNLOADED_DLL%\" \"%TARGET_DLL%\" >nul 2>nul");
            sb.AppendLine("if %errorlevel% neq 0 (");
            sb.AppendLine("    echo ERROR: Failed to replace DLL. You may need to run as administrator.");
            sb.AppendLine("    echo Script and downloaded file preserved at:");
            sb.AppendLine("    echo   Script: %~f0");
            sb.AppendLine("    echo   Source: %DOWNLOADED_DLL%");
            sb.AppendLine("    pause");
            sb.AppendLine("    exit /b 1");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("echo Patch applied successfully.");
            sb.AppendLine();
            sb.AppendLine(":: Restart ColorVision");
            sb.AppendLine("start \"\" \"%EXE_PATH%\"");
            sb.AppendLine();
            sb.AppendLine(":: Self-cleanup");
            sb.AppendLine("start \"\" cmd /c \"ping -n 3 127.0.0.1 >nul & del \"%~f0\" 2>nul\"");
            sb.AppendLine("exit /b 0");

            File.WriteAllText(cmdScript, sb.ToString(), Encoding.Default);
            log.Info($"OpenCvSharp fix: Patch script written to {cmdScript}");

            var startInfo = new ProcessStartInfo
            {
                FileName = cmdScript,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (!Tool.HasWritePermission(programDir))
            {
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                log.Info("OpenCvSharp fix: Requesting admin elevation for DLL replacement");
            }

            Process.Start(startInfo);
            Environment.Exit(0);
        }
    }
}
