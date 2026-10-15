using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace ColorVisionSetup
{
    internal sealed class InstallerPackage
    {
        internal string Path { get; }
        internal Version Version { get; }
        internal string Sha256 { get; }
        private InstallerPackage(string path, Version version, string sha256) { Path = path; Version = version; Sha256 = sha256; }

        internal static InstallerPackage Verify(string path, Version expectedVersion, CancellationToken token)
        {
            using (var file = OpenLocked(path)) return VerifyLocked(path, expectedVersion, file, token);
        }

        private static FileStream OpenLocked(string path)
        {
            if (!string.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) || File.Exists(path + ".partial"))
                throw new InvalidDataException("请选择完整的 EXE 安装包。");
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static InstallerPackage VerifyLocked(string path, Version expectedVersion, FileStream file, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PublisherSignature.Verify(path);
            token.ThrowIfCancellationRequested();
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.Equals(info.ProductName, "ColorVision", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(info.FileDescription, "ColorVision Installer", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("所选文件不是 ColorVision 完整安装程序。");
            Version version = ReleaseClient.ParseVersion(info.FileVersion);
            if (expectedVersion != null && version != expectedVersion) throw new InvalidDataException("安装包版本与目标版本不一致。");
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[81920];
                int read;
                while ((read = file.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, read, buffer, 0);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return new InstallerPackage(System.IO.Path.GetFullPath(path), version, BitConverter.ToString(sha.Hash).Replace("-", ""));
            }
        }

        internal void Launch()
        {
            // Keep the file immutable from the final verification through ShellExecute's handoff.
            using (var file = OpenVerified())
            {
                using (var process = Process.Start(new ProcessStartInfo(Path)
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Path)
                }))
                {
                    if (process == null) throw new InvalidOperationException("未能确认安装程序已启动。");
                    SetupFiles.Log("Installer handed off, PID=" + process.Id + ", version=" + Version + ", SHA256=" + Sha256);
                }
            }
        }

        internal FileStream OpenVerified()
        {
            var file = OpenLocked(Path);
            try
            {
                var current = VerifyLocked(Path, Version, file, CancellationToken.None);
                if (!string.Equals(current.Sha256, Sha256, StringComparison.Ordinal)) throw new InvalidDataException("安装包在校验后发生变化，请重新准备安装包。");
                return file;
            }
            catch { file.Dispose(); throw; }
        }
    }
}
