using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class ExternalToolsConfig : IConfig
    {
        public static ExternalToolsConfig Instance => ConfigService.Instance.GetRequiredService<ExternalToolsConfig>();

        public string ImageJPath { get => _imageJPath; set => _imageJPath = value; }
        private string _imageJPath = string.Empty;

        public string BeyondComparePath { get => _beyondComparePath; set => _beyondComparePath = value; }
        private string _beyondComparePath = string.Empty;
    }

    public class ExternalToolsAppProvider : IThirdPartyAppProvider
    {
        private const string BaseUrl = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool";

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = Properties.Resources.InstallTools;
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "ImageJ",
                    Group = group,
                    Order = 30,
                    LaunchAction = LaunchImageJ,
                    GetIconPath = () => ExternalToolsConfig.Instance.ImageJPath,
                },
                new ThirdPartyAppInfo
                {
                    Name = "Beyond Compare",
                    Group = group,
                    Order = 31,
                    LaunchAction = LaunchBeyondCompare,
                    GetIconPath = () => ExternalToolsConfig.Instance.BeyondComparePath,
                }
            };
        }


        private static void LaunchImageJ()
        {
            EnsureAndLaunch(
                appName: "ImageJ",
                currentPath: () => ExternalToolsConfig.Instance.ImageJPath,
                setPath: value => ExternalToolsConfig.Instance.ImageJPath = value,
                zipFileName: "ij154-win-java8.zip",
                downloadUrl: $"{BaseUrl}/ImageJ/ij154-win-java8.zip",
                extractedExeRelativePath: Path.Combine("ImageJ", "ImageJ.exe"),
                selectExeTitle: "Select ImageJ.exe",
                selectExeFilter: "ImageJ.exe|ImageJ.exe");
        }

        private static void LaunchBeyondCompare()
        {
            EnsureAndLaunch(
                appName: "BCompare",
                currentPath: () => ExternalToolsConfig.Instance.BeyondComparePath,
                setPath: value => ExternalToolsConfig.Instance.BeyondComparePath = value,
                zipFileName: "Beyond_Compare_5.zip",
                downloadUrl: $"{BaseUrl}/BeyondCompare/Beyond_Compare_5.zip",
                extractedExeRelativePath: Path.Combine("Beyond Compare 5", "BCompare.exe"),
                selectExeTitle: "Select BCompare.exe",
                selectExeFilter: "BCompare.exe|BCompare.exe");
        }

        private static void EnsureAndLaunch(
            string appName,
            Func<string> currentPath,
            Action<string> setPath,
            string zipFileName,
            string downloadUrl,
            string extractedExeRelativePath,
            string selectExeTitle,
            string selectExeFilter)
        {
            if (File.Exists(currentPath()))
            {
                StartApp(currentPath());
                return;
            }

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"Cannot find {appName}. Download now?",
                "ColorVision",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
                var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
                if (service == null) return;

                service.ShowDownloadWindow();
                service.Download(downloadUrl, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                {
                    if (filePath == null) return;
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
                            ZipFile.ExtractToDirectory(filePath, path, true);

                            string exePath = Path.Combine(path, extractedExeRelativePath);
                            setPath(exePath);
                            StartApp(exePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                        }
                        finally
                        {
                            TryDelete(filePath);
                        }
                    });
                });
                return;
            }

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"I can't find {appName}. Would you like to help me find it?",
                $"Open in {appName}",
                MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                return;
            }

            using System.Windows.Forms.OpenFileDialog openFileDialog = new();
            openFileDialog.Title = selectExeTitle;
            openFileDialog.Filter = selectExeFilter;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            setPath(openFileDialog.FileName);
            StartApp(openFileDialog.FileName);
        }

        private static void StartApp(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"File not found: {path}");
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = path,
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // ignore cleanup error
            }
        }
    }
}