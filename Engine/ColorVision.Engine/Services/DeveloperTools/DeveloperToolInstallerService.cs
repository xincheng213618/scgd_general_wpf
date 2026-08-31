using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ColorVision.Engine.Services.DeveloperTools
{
    public static class DeveloperToolInstallerService
    {
        public static VerifiedInstaller PrepareInstaller(string path, DeveloperToolRelease release, string expectedSha256)
        {
            FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                string hash = VerifyLockedInstaller(file, release, expectedSha256);
                return new VerifiedInstaller(file, release.Kind, hash);
            }
            catch { file.Dispose(); throw; }
        }

        /// <summary>Keeps the verified file locked against writes/replacement through the explicit launch.</summary>
        public sealed class VerifiedInstaller : IDisposable
        {
            private readonly FileStream _file;
            private readonly DeveloperToolKind _kind;
            internal VerifiedInstaller(FileStream file, DeveloperToolKind kind, string sha256) { _file = file; _kind = kind; Sha256 = sha256; }
            public string Sha256 { get; }
            public Process Start()
            {
                ObjectDisposedException.ThrowIf(!_file.CanRead, this);
                var start = new ProcessStartInfo { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(_file.Name)! };
                if (_kind == DeveloperToolKind.NodeJs)
                {
                    start.FileName = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
                    start.ArgumentList.Add("/i");
                    start.ArgumentList.Add(_file.Name);
                }
                else start.FileName = _file.Name;
                return Process.Start(start) ?? throw new IOException("无法启动官方安装向导。");
            }
            public void Dispose() => _file.Dispose();
        }

        private static string VerifyLockedInstaller(FileStream file, DeveloperToolRelease release, string expectedSha256)
        {
            if (!string.Equals(Path.GetFileName(file.Name), release.FileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("下载文件名与所选版本不一致，已阻止安装。");
            string hash = Convert.ToHexString(SHA256.HashData(file));
            if (expectedSha256.Length != 64 || !string.Equals(hash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包 SHA256 与官网不一致，已阻止安装。请重新下载或切换官方源。");

            VerifyAuthenticode(file.Name);
            // X509CertificateLoader cannot extract the Authenticode signer from an EXE/MSI.
#pragma warning disable SYSLIB0057
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(file.Name);
#pragma warning restore SYSLIB0057
            using X509Certificate2 signer = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
            string expectedPublisher = release.Kind == DeveloperToolKind.Python ? "Python Software Foundation" : "OpenJS Foundation";
            string publisher = signer.GetNameInfo(X509NameType.SimpleName, false);
            if (!string.Equals(publisher, expectedPublisher, StringComparison.Ordinal))
                throw new InvalidDataException($"安装包发布者不是 {expectedPublisher}，已阻止安装。");
            return hash;
        }

        private static void VerifyAuthenticode(string path)
        {
            IntPtr pathPointer = Marshal.StringToCoTaskMemUni(path);
            IntPtr filePointer = Marshal.AllocCoTaskMem(Marshal.SizeOf<WinTrustFileInfo>());
            var action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            var data = new WinTrustData
            {
                Size = (uint)Marshal.SizeOf<WinTrustData>(),
                UiChoice = 2, // WTD_UI_NONE
                RevocationChecks = 1, // WTD_REVOKE_WHOLECHAIN; fail closed when trust cannot be established.
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = filePointer,
                StateAction = 1, // WTD_STATEACTION_VERIFY
            };
            try
            {
                Marshal.StructureToPtr(new WinTrustFileInfo
                {
                    Size = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    FilePath = pathPointer,
                }, filePointer, false);
                int result = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                if (result != 0)
                    throw new InvalidDataException($"Windows 无法确认安装包数字签名可信（0x{result:X8}），已阻止安装。请检查网络、系统时间及证书状态。");
            }
            finally
            {
                data.StateAction = 2; // WTD_STATEACTION_CLOSE
                _ = WinVerifyTrust(new IntPtr(-1), ref action, ref data);
                Marshal.FreeCoTaskMem(filePointer);
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustFileInfo
        {
            public uint Size;
            public IntPtr FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public uint Size;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
            public IntPtr SignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref WinTrustData data);
    }
}
