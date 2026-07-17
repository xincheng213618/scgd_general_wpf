using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;

namespace ColorVisionServiceHost;

internal static class ServiceHostCallerIdentity
{
    public static bool TryResolve(NamedPipeServerStream pipe, out ServiceHostRequestContext context, out string error)
    {
        context = new ServiceHostRequestContext();
        error = string.Empty;
        if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint processId) || processId == 0)
        {
            error = "Unable to identify the pipe client process.";
            return false;
        }

        string sid = string.Empty;
        string userName = string.Empty;
        try
        {
            pipe.RunAsClient(() =>
            {
                using WindowsIdentity identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
                sid = identity.User?.Value ?? string.Empty;
                userName = identity.Name ?? string.Empty;
            });
        }
        catch (Exception ex)
        {
            error = $"Unable to identify the pipe client user: {ex.Message}";
            return false;
        }

        string processPath;
        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            processPath = process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception ex)
        {
            error = $"Unable to inspect the pipe client process: {ex.Message}";
            return false;
        }

        if (!IsAllowedClientPath(processPath))
        {
            error = "The pipe client executable is not an approved ColorVision host.";
            return false;
        }

        context = new ServiceHostRequestContext
        {
            ProcessId = checked((int)processId),
            UserSid = sid,
            UserName = userName,
            ProcessPath = processPath,
            ProcessSha256 = ComputeSha256(processPath),
        };
        return true;
    }

    private static bool IsAllowedClientPath(string processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath) || !Path.IsPathFullyQualified(processPath) || !File.Exists(processPath))
            return false;
        string name = Path.GetFileName(processPath);
        if (string.Equals(name, ServiceHostConstants.ExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            string runningHost = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ServiceHostConstants.ExecutableName);
            return string.Equals(Path.GetFullPath(processPath), Path.GetFullPath(runningHost), StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(name, "ColorVision.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);
}
