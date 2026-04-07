using ColorVision.UI.Menus;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Update.Export
{
    public class MenuRegisterThumbnail : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1001;
        public override string Header => "注册缩略图预览";

        private static readonly ILog log = LogManager.GetLogger(typeof(MenuRegisterThumbnail));

        public override void Execute()
        {
            try
            {
                string appDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
                string comHostDll = Path.Combine(appDir, "ColorVision.ShellExtension.comhost.dll");

                if (!File.Exists(comHostDll))
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(),
                        $"未找到 Shell Extension:\n{comHostDll}",
                        "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // regsvr32 registers the COM server (CLSID + InprocServer32)
                var regsvr32 = Process.Start(new ProcessStartInfo
                {
                    FileName = "regsvr32",
                    Arguments = $"\"{comHostDll}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });
                regsvr32?.WaitForExit();

                if (regsvr32?.ExitCode != 0)
                {
                    log.Warn($"regsvr32 exited with code {regsvr32?.ExitCode}");
                    MessageBox.Show(Application.Current.GetActiveWindow(),
                        "COM 注册失败，请确认已授予管理员权限。",
                        "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Write shellex thumbnail handler association for .cvraw and .cvcie via reg.exe
                string thumbnailClsid = "{7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C}";
                string thumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}";

                string[] extensions = { ".cvraw", ".cvcie" };
                foreach (var ext in extensions)
                {
                    var reg = Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"add \"HKCR\\{ext}\\ShellEx\\{thumbnailProviderIid}\" /ve /d \"{thumbnailClsid}\" /f",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    reg?.WaitForExit();
                }

                // Clear thumbnail cache
                string thumbCacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(thumbCacheDir))
                {
                    foreach (var file in Directory.GetFiles(thumbCacheDir, "thumbcache_*.db"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(thumbCacheDir, "iconcache_*.db"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }

                log.Info("Thumbnail shell extension registered successfully");
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    "缩略图预览注册成功！\n重启资源管理器后，.cvraw 和 .cvcie 文件将显示图像缩略图。",
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"RegisterThumbnail failed: {ex}");
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    $"注册失败：{ex.Message}",
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class MenuUnregisterThumbnail : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override int Order => 1002;
        public override string Header => "卸载缩略图预览";

        private static readonly ILog log = LogManager.GetLogger(typeof(MenuUnregisterThumbnail));

        public override void Execute()
        {
            try
            {
                string appDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName)!;
                string comHostDll = Path.Combine(appDir, "ColorVision.ShellExtension.comhost.dll");

                // Remove shellex associations first
                string thumbnailProviderIid = "{E357FCCD-A995-4576-B01F-234630154E96}";
                string[] extensions = { ".cvraw", ".cvcie" };
                foreach (var ext in extensions)
                {
                    var reg = Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg",
                        Arguments = $"delete \"HKCR\\{ext}\\ShellEx\\{thumbnailProviderIid}\" /f",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    });
                    reg?.WaitForExit();
                }

                // Unregister COM server
                if (File.Exists(comHostDll))
                {
                    var regsvr32 = Process.Start(new ProcessStartInfo
                    {
                        FileName = "regsvr32",
                        Arguments = $"/u \"{comHostDll}\"",
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    regsvr32?.WaitForExit();
                }

                log.Info("Thumbnail shell extension unregistered successfully");
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    "缩略图预览已卸载。",
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"UnregisterThumbnail failed: {ex}");
                MessageBox.Show(Application.Current.GetActiveWindow(),
                    $"卸载失败：{ex.Message}",
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
