using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorVisionSetup
{
    public class Core
    {
        public enum UNPACK_RESULT
        {
            NOT_PERFORMED_YET = -1,
            SUCCESS,
            FAILED_DLL_LOADING,
            FAILED_UNPACKING,
            NEEDS_REBOOT
        }

        public enum REDIST_RESULT
        {
            SUCCESS,
            NEEDS_REBOOT
        }

        public const string NativeRuntime_x64 = "vc_redist.x64.exe";

        public const string NativeLibrary = "logi_installer_shared.dll";

        public const string NativeCodecsLibrary = "logi_codecs_shared.dll";

        public static UNPACK_RESULT unpackResult = UNPACK_RESULT.NOT_PERFORMED_YET;

        private static readonly object _resource_load_lock = new object();

        private const int _redistRebootRequiredCode = 3010;

        private static IntPtr _dllHandleSharedInstaller;

        private static IntPtr _dllHandleWebp;

        public static D GetDLLFunction<D>(string dllName, string procName) where D : Delegate
        {
            lock (_resource_load_lock)
            {
                if (unpackResult == UNPACK_RESULT.NOT_PERFORMED_YET)
                {
                    unpackResult = UnpackAndInstallRedist();
                }
            }
            IntPtr intPtr = LoadLibraryW(Path.Combine(TempPath, dllName).Replace("/", "\\"));
            if (intPtr == IntPtr.Zero)
            {
                int lastWin32Error = Marshal.GetLastWin32Error();
                throw new ArgumentException($"Core::Failed to load DLL {dllName} ({lastWin32Error})");
            }
            IntPtr procAddress = GetProcAddress(intPtr, procName);
            if (procAddress == IntPtr.Zero)
            {
                int lastWin32Error2 = Marshal.GetLastWin32Error();
                throw new ArgumentException($"Core::Failed to load function {procName} from {dllName} ({lastWin32Error2})");
            }
            return (D)Marshal.GetDelegateForFunctionPointer(procAddress, typeof(D));
        }

        public static string TempPath = string.Empty;

        public static UNPACK_RESULT UnpackAndInstallRedist()
        {
            try
            {
                unpackResource("logi_installer_shared.dll");
                unpackResource("logi_codecs_shared.dll");
                _dllHandleSharedInstaller = LoadLibraryW(Path.Combine(TempPath, "logi_installer_shared.dll").Replace("/", "\\"));
                if (_dllHandleSharedInstaller == IntPtr.Zero)
                {
                    int lastWin32Error = Marshal.GetLastWin32Error();
                    return UNPACK_RESULT.FAILED_DLL_LOADING;
                }
                _dllHandleWebp = LoadLibraryW(Path.Combine(TempPath,"logi_codecs_shared.dll").Replace("/", "\\"));
                if (_dllHandleWebp == IntPtr.Zero)
                {
                    int lastWin32Error2 = Marshal.GetLastWin32Error();
                    return UNPACK_RESULT.FAILED_DLL_LOADING;
                }
                return UNPACK_RESULT.SUCCESS;
            }
            catch (Exception ex)
            {
                return UNPACK_RESULT.FAILED_UNPACKING;
            }
        }

        public static void Cleanup()
        {
            try
            {
                if (IntPtr.Zero != _dllHandleSharedInstaller)
                {
                    FreeLibrary(_dllHandleSharedInstaller);
                    _dllHandleSharedInstaller = IntPtr.Zero;
                }
                if (IntPtr.Zero != _dllHandleWebp)
                {
                    FreeLibrary(_dllHandleWebp);
                    _dllHandleWebp = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private static bool unpackResource(string resource)
        {
            if (string.IsNullOrWhiteSpace(TempPath))
                TempPath = Path.GetTempPath();

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ColorVisionSetup.{resource}" ))
            {
                if (stream != null)
                {
                    byte[] bytes = new BinaryReader(stream).ReadBytes((int)stream.Length);
                    File.WriteAllBytes(Path.Combine(TempPath, resource), bytes);
                    return true;
                }
                return false;
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr LoadLibraryW([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);
    }

}

