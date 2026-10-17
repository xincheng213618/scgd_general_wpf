using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace cvColorVision;

internal static class CameraNativeLibrary
{
    private static readonly object LoadLock = new();
    private static IntPtr library;

#pragma warning disable CA2255 // Every device binding in this assembly shares the same native resolver.
    [ModuleInitializer]
    internal static void Initialize() => NativeLibrary.SetDllImportResolver(typeof(CameraNativeLibrary).Assembly, Resolve);
#pragma warning restore CA2255

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals("cvCamera.dll", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;

        lock (LoadLock)
        {
            if (library != IntPtr.Zero) return library;

            string assemblyDirectory = Path.GetDirectoryName(assembly.Location) ?? AppContext.BaseDirectory;
            foreach (string directory in new[]
            {
                Path.Combine(assemblyDirectory, "runtimes", "win-x64", "native"),
                Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native"),
                assemblyDirectory,
                AppContext.BaseDirectory
            })
            {
                string path = Path.Combine(directory, libraryName);
                if (!File.Exists(path)) continue;

                // SCGDCamLayer and other vendor SDKs call LoadLibrary by name after initialization.
                // Keep their directory in the process search path, including for those later calls.
                if (!SetDllDirectory(directory)) throw new Win32Exception(Marshal.GetLastWin32Error());
                library = NativeLibrary.Load(path, assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
                return library;
            }
        }

        return IntPtr.Zero;
    }

    [DllImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string directory);
}
