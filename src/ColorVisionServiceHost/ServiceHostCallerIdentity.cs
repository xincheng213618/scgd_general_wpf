using Microsoft.Win32.SafeHandles;
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

        if (!TryResolveProcessIdentity(
                checked((int)processId),
                out string sid,
                out string userName,
                out string processPath,
                out error))
            return false;

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

    internal static bool TryResolveProcessIdentity(
        int processId,
        out string sid,
        out string userName,
        out string processPath,
        out string error)
    {
        sid = string.Empty;
        userName = string.Empty;
        processPath = string.Empty;
        error = string.Empty;
        try
        {
            using SafeProcessHandle processHandle = OpenProcess(ProcessQueryLimitedInformation, false, checked((uint)processId));
            if (processHandle.IsInvalid)
            {
                error = $"Unable to open the pipe client process token: {Marshal.GetLastWin32Error()}.";
                return false;
            }
            char[] pathBuffer = new char[32768];
            int pathLength = pathBuffer.Length;
            if (!QueryFullProcessImageName(processHandle, 0, pathBuffer, ref pathLength))
            {
                error = $"Unable to inspect the pipe client process: {Marshal.GetLastWin32Error()}.";
                return false;
            }
            processPath = new string(pathBuffer, 0, pathLength);
            if (!OpenProcessToken(processHandle, TokenQuery, out SafeAccessTokenHandle tokenHandle))
            {
                error = $"Unable to open the pipe client token: {Marshal.GetLastWin32Error()}.";
                return false;
            }

            using (tokenHandle)
            using (WindowsIdentity identity = new(tokenHandle.DangerousGetHandle()))
            {
                sid = identity.User?.Value ?? string.Empty;
                userName = identity.Name ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(sid))
            {
                error = "The pipe client token did not contain a user SID.";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unable to identify the pipe client user: {ex.Message}";
            return false;
        }
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

    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint processAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        SafeProcessHandle processHandle,
        int flags,
        [Out] char[] executablePath,
        ref int size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);
}
