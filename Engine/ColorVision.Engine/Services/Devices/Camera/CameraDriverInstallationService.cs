using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public sealed record CameraDriverInstallationStatus(
        Version? RegisteredVersion,
        Version? FirmwareVersion,
        Version? IoVersion,
        bool HasInstallerRegistration)
    {
        // No camera needs to be connected. This checks installation, not device health or driver binding.
        public bool IsInstalled => RegisteredVersion != null && FirmwareVersion != null && IoVersion != null;
        public bool HasPartialInstallation => HasInstallerRegistration || RegisteredVersion != null || FirmwareVersion != null || IoVersion != null;
    }

    public interface ICameraDriverInstallationService
    {
        Task<CameraDriverInstallationStatus> QueryAsync(CancellationToken cancellationToken);
        Task<int> InstallAsync(string installerPath, CancellationToken cancellationToken);
    }

    public sealed class CameraDriverInstallationService : ICameraDriverInstallationService
    {
        public const string InstallerFileName = "ColorVisionDriver240301win10.exe";
        public const string DownloadPage = "http://xc213618.ddns.me:9998/browse/Tool/ColorVisionDriver";
        public const string DownloadUrl = "http://xc213618.ddns.me:9998/download/Tool/ColorVisionDriver/" + InstallerFileName;
        internal const string InstallerSha256 = "5E210170715DD32E86554735D43A3EDBD36628405ADFE36E1DDA9126C62F9340";
        private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{065F095D-2E56-4990-A834-B8E383C37588}_is1";

        public Task<CameraDriverInstallationStatus> QueryAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => InspectInstallation(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                HasInstallerRegistration(), cancellationToken), cancellationToken);
        }

        internal static CameraDriverInstallationStatus InspectInstallation(string windowsDirectory, bool hasInstallerRegistration,
            CancellationToken cancellationToken)
        {
            Version? registeredVersion = null;
            // The legacy installer publishes the INF but copies SYS files itself. Check both locations.
            foreach (string path in Directory.EnumerateFiles(Path.Combine(windowsDirectory, "INF"), "oem*.inf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string contents;
                try { contents = File.ReadAllText(path); }
                catch (FileNotFoundException) { continue; } // A concurrent driver update may remove an entry.

                Version? version = ReadRegisteredVersion(contents);
                if (version != null && (registeredVersion == null || version > registeredVersion))
                    registeredVersion = version;
            }

            string systemDirectory = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess ? "Sysnative" : "System32";
            string driversDirectory = Path.Combine(windowsDirectory, systemDirectory, "drivers");
            return new(registeredVersion,
                ReadFileVersion(Path.Combine(driversDirectory, "ColorVision_FW.sys")),
                ReadFileVersion(Path.Combine(driversDirectory, "ColorVision_IO.sys")),
                hasInstallerRegistration);
        }

        internal static Version? ReadRegisteredVersion(string contents)
        {
            Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? section = null;
            using StringReader reader = new(contents);
            while (reader.ReadLine() is string line)
            {
                line = line.Split(';', 2)[0].Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    string name = line[1..^1].Trim();
                    if (!sections.TryGetValue(name, out section))
                        sections[name] = section = new(StringComparer.OrdinalIgnoreCase);
                }
                else if (section != null && line.IndexOf('=') is int separator && separator > 0)
                {
                    section[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
                }
            }

            string? Get(string name, string key) => sections.TryGetValue(name, out var values) ? values.GetValueOrDefault(key) : null;
            string? provider = Get("Version", "Provider");
            if (provider != null && provider.StartsWith('%') && provider.EndsWith('%'))
                provider = Get("Strings", provider.Trim('%'));

            if (!string.Equals(provider, "ColorVision", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Get("Version", "ClassGUID"), "{94F18CEC-CA64-40F6-9A81-17A67720ABF0}", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Get("ColorVision_1ST.AddService", "ServiceBinary"), @"%12%\ColorVision_FW.sys", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Get("ColorVision_2ND.AddService", "ServiceBinary"), @"%12%\ColorVision_IO.sys", StringComparison.OrdinalIgnoreCase))
                return null;

            string[]? driverVersion = Get("Version", "DriverVer")?.Split(',', 2);
            return driverVersion?.Length == 2 && Version.TryParse(driverVersion[1].Trim(), out Version? version)
                && version > new Version(0, 0) ? version : null;
        }

        private static Version? ReadFileVersion(string path)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                Version version = new(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
                return version > new Version(0, 0, 0, 0) ? version : null;
            }
            catch (FileNotFoundException) { return null; }
        }

        private static bool HasInstallerRegistration()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using RegistryKey machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? key = machine.OpenSubKey(UninstallKey);
                if (key != null) return true;
            }
            return false;
        }

        public async Task<int> InstallAsync(string installerPath, CancellationToken cancellationToken)
        {
            // Keep the downloaded installer locked until its process has exited. Do not execute registry paths.
            using FileStream installer = new(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(installer, cancellationToken).ConfigureAwait(true));
            if (!string.Equals(hash, InstallerSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ColorVision camera driver installer checksum mismatch.");

            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Logs", "DriverInstallation");
            Directory.CreateDirectory(logDirectory);
            string logPath = Path.Combine(logDirectory, $"CameraDriver-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log");
            cancellationToken.ThrowIfCancellationRequested();
            using Process process = Process.Start(new ProcessStartInfo(installerPath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(installerPath),
                Arguments = $"/NORESTART /RESTARTEXITCODE=3010 /LOG=\"{logPath}\""
            }) ?? throw new IOException("Unable to start the ColorVision camera driver installer.");

            // Cancellation after launch must not make the wizard imply that an elevated installer has stopped.
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return process.ExitCode;
        }
    }
}
