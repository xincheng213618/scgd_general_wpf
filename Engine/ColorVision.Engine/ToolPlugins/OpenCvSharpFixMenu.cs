// ==================================================================================
// TEMPORARY WORKAROUND — OpenCvSharpExtern.dll export crash fix
// This menu item downloads and replaces a known-good older version of
// OpenCvSharpExtern.dll to work around an export crash in newer versions.
// TODO: Remove this entire file once the upstream OpenCvSharp issue is resolved
//       and the project has been updated to a stable release.
// ==================================================================================

using ColorVision.UI.Menus;
using log4net;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
        private const string HttpUser = "1";
        private const string HttpPass = "1";

        public override string OwnerGuid => "MenuUpdate";
        public override string GuidId => "OpenCvSharpFix";
        public override int Order => 10099; // Just before the Update menu item
        public override string Header => Properties.Resources.FixOpenCvSharpExportCrash;

        public override async void Execute()
        {
            string targetDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenCvSharpExtern.dll");
            string tempDll = Path.Combine(Path.GetTempPath(), "OpenCvSharpExtern_fix.dll");

            try
            {
                // Show start message
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    Properties.Resources.DownloadingOpenCvSharpFix,
                    Properties.Resources.FixOpenCvSharpExportCrash,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                log.Info("OpenCvSharp fix: Starting download...");

                // Download with basic authentication
                using var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(HttpUser, HttpPass)
                };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromMinutes(5);

                var response = await client.GetAsync(PatchUrl);
                response.EnsureSuccessStatusCode();

                // Save to temp file
                await using (var fs = File.Create(tempDll))
                {
                    await response.Content.CopyToAsync(fs);
                }

                log.Info($"OpenCvSharp fix: Download completed, saved to {tempDll}");

                // Backup existing DLL
                if (File.Exists(targetDll))
                {
                    string backupName = $"{targetDll}.bak-{DateTime.Now:yyyyMMddHHmmss}";
                    try
                    {
                        File.Copy(targetDll, backupName, true);
                        log.Info($"OpenCvSharp fix: Backed up to {backupName}");
                    }
                    catch (IOException ex)
                    {
                        log.Error($"OpenCvSharp fix: Backup failed", ex);
                        MessageBox.Show(
                            Application.Current.GetActiveWindow(),
                            string.Format(Properties.Resources.OpenCvSharpFixBackupFailed, ex.Message),
                            Properties.Resources.FixOpenCvSharpExportCrash,
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        CleanupTemp(tempDll);
                        return;
                    }
                }

                // Try to replace the DLL
                try
                {
                    File.Copy(tempDll, targetDll, true);
                    log.Info("OpenCvSharp fix: DLL replaced successfully");

                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        Properties.Resources.OpenCvSharpFixSuccess,
                        Properties.Resources.FixOpenCvSharpExportCrash,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (IOException ex)
                {
                    // DLL is locked — prompt user to restart
                    log.Warn("OpenCvSharp fix: DLL is locked, cannot replace", ex);
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        string.Format(Properties.Resources.OpenCvSharpFixDllLocked, ex.Message),
                        Properties.Resources.FixOpenCvSharpExportCrash,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                log.Error("OpenCvSharp fix: Failed", ex);
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    string.Format(Properties.Resources.OpenCvSharpFixFailed, ex.Message),
                    Properties.Resources.FixOpenCvSharpExportCrash,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                CleanupTemp(tempDll);
            }
        }

        private static void CleanupTemp(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
