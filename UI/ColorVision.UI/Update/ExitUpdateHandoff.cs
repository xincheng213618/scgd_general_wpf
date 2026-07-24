using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Update
{
    public sealed record ExitUpdateHandoffState(
        string MarkerPath,
        string ReopenRequestPath,
        string LaunchToken,
        string UpdateRoot);

    /// <summary>
    /// Coordinates application launches while an external update batch is replacing files.
    /// </summary>
    public static class ExitUpdateHandoff
    {
        public const string LaunchTokenEnvironmentVariable = "COLORVISION_UPDATE_TOKEN";
        private static readonly TimeSpan MaximumMarkerAge = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MaximumPreparationAge = TimeSpan.FromMinutes(5);

        public static ExitUpdateHandoffState Prepare(
            string programDirectory,
            string updateRoot,
            string? stateRootOverride = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(updateRoot);

            (string markerPath, string reopenRequestPath) = GetStatePaths(programDirectory, stateRootOverride);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            TryDeleteFile(reopenRequestPath);

            string launchToken = Guid.NewGuid().ToString("N");
            ExitUpdateHandoffState state = new(
                markerPath,
                reopenRequestPath,
                launchToken,
                Path.GetFullPath(updateRoot));
            WriteMarker(state, updaterProcessId: 0);
            return state;
        }

        public static bool TryActivate(ExitUpdateHandoffState state, int updaterProcessId)
        {
            try
            {
                WriteMarker(state, updaterProcessId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryDeferLaunchForActiveUpdate(string programDirectory)
        {
            return TryDeferLaunchForActiveUpdate(
                programDirectory,
                Environment.GetEnvironmentVariable(LaunchTokenEnvironmentVariable),
                stateRootOverride: null);
        }

        public static bool TryDeferLaunchForActiveUpdate(
            string programDirectory,
            string? launchToken,
            string? stateRootOverride)
        {
            (string markerPath, string reopenRequestPath) = GetStatePaths(programDirectory, stateRootOverride);
            if (!TryReadActiveMarker(markerPath, reopenRequestPath, out string[] markerLines))
                return false;

            if (!string.IsNullOrWhiteSpace(launchToken) &&
                string.Equals(launchToken, markerLines[0], StringComparison.Ordinal))
            {
                Environment.SetEnvironmentVariable(LaunchTokenEnvironmentVariable, null);
                Clear(markerPath, reopenRequestPath);
                return false;
            }

            try
            {
                File.WriteAllText(reopenRequestPath, DateTime.UtcNow.ToString("O"), new UTF8Encoding(false));
                return true;
            }
            catch
            {
                Clear(markerPath, reopenRequestPath);
                return false;
            }
        }

        public static bool IsUpdateActive(string programDirectory)
        {
            return IsUpdateActive(programDirectory, stateRootOverride: null);
        }

        internal static bool IsUpdateActive(string programDirectory, string? stateRootOverride)
        {
            (string markerPath, string reopenRequestPath) = GetStatePaths(programDirectory, stateRootOverride);
            return TryReadActiveMarker(markerPath, reopenRequestPath, out _);
        }

        private static bool TryReadActiveMarker(
            string markerPath,
            string reopenRequestPath,
            out string[] markerLines)
        {
            markerLines = Array.Empty<string>();
            if (!File.Exists(markerPath))
                return false;

            try
            {
                markerLines = File.ReadAllLines(markerPath);
                if (markerLines.Length < 2 ||
                    string.IsNullOrWhiteSpace(markerLines[0]) ||
                    string.IsNullOrWhiteSpace(markerLines[1]))
                {
                    Clear(markerPath, reopenRequestPath);
                    markerLines = Array.Empty<string>();
                    return false;
                }

                DateTime markerTimeUtc = File.GetLastWriteTimeUtc(markerPath);
                TimeSpan markerAge = DateTime.UtcNow - markerTimeUtc;
                string updateRoot = markerLines[1];
                int updaterProcessId = markerLines.Length >= 3 && int.TryParse(markerLines[2], out int parsedProcessId)
                    ? parsedProcessId
                    : 0;
                bool updaterIsStarting = updaterProcessId <= 0 && markerAge <= MaximumPreparationAge;
                bool updaterIsRunning = updaterProcessId > 0 && IsProcessRunning(updaterProcessId);
                bool markerIsActive = markerAge >= TimeSpan.FromMinutes(-1) &&
                    markerAge <= MaximumMarkerAge &&
                    Directory.Exists(updateRoot) &&
                    File.Exists(Path.Combine(updateRoot, "update.bat")) &&
                    (updaterIsStarting || updaterIsRunning);
                if (markerIsActive)
                    return true;

                Clear(markerPath, reopenRequestPath);
                markerLines = Array.Empty<string>();
                return false;
            }
            catch
            {
                Clear(markerPath, reopenRequestPath);
                markerLines = Array.Empty<string>();
                return false;
            }
        }

        private static void WriteMarker(ExitUpdateHandoffState state, int updaterProcessId)
        {
            string temporaryMarkerPath = state.MarkerPath + $".{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllLines(
                    temporaryMarkerPath,
                    [state.LaunchToken, state.UpdateRoot, updaterProcessId.ToString()],
                    new UTF8Encoding(false));
                File.Move(temporaryMarkerPath, state.MarkerPath, overwrite: true);
            }
            finally
            {
                TryDeleteFile(temporaryMarkerPath);
            }
        }

        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        public static Process Start(ExitUpdateHandoffState state, ProcessStartInfo startInfo)
        {
            Process? process = null;
            try
            {
                process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start external update process.");
                if (!TryActivate(state, process.Id))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    throw new InvalidOperationException("Failed to activate external update handoff.");
                }

                return process;
            }
            catch
            {
                process?.Dispose();
                Clear(state);
                throw;
            }
        }

        public static void Clear(ExitUpdateHandoffState? state)
        {
            if (state != null)
                Clear(state.MarkerPath, state.ReopenRequestPath);
        }

        private static void Clear(string markerPath, string reopenRequestPath)
        {
            TryDeleteFile(markerPath);
            TryDeleteFile(reopenRequestPath);
        }

        private static (string MarkerPath, string ReopenRequestPath) GetStatePaths(
            string programDirectory,
            string? stateRootOverride)
        {
            string installationKey = GetInstallationKey(programDirectory);
            string stateRoot = stateRootOverride ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorVision",
                "UpdateState");
            string stateDirectory = Path.Combine(stateRoot, installationKey);
            return (
                Path.Combine(stateDirectory, "update.pending"),
                Path.Combine(stateDirectory, "reopen.requested"));
        }

        public static string GetInstallationKey(string programDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(programDirectory);
            string normalizedProgramDirectory = Path.GetFullPath(programDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalizedProgramDirectory)))[..16];
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // A stale marker has a bounded lifetime and is retried on the next launch.
            }
        }
    }
}
