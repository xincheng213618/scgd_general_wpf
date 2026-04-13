using System;
using System.Runtime.InteropServices;

namespace ColorVision.Core
{
    public enum NativeLogSource
    {
        Unknown = 0,
        OpencvHelper = 1,
        OpencvCuda = 2,
    }

    public enum NativeLogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
    }

    public static class NativeLogBridge
    {
        private const string HelperLib = "opencv_helper.dll";
        private const string CudaLib = "opencv_cuda.dll";

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void NativeLogCallback(int source, int level, IntPtr messagePtr);

        private static NativeLogCallback? _callback;
        private static Action<NativeLogSource, NativeLogLevel, string>? _sink;

        [DllImport(HelperLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void M_SetLogCallback(NativeLogCallback callback);

        [DllImport(HelperLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void M_SetLogEnabled(int enabled);

        [DllImport(HelperLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void M_SetLogLevel(int level);

        [DllImport(HelperLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void M_EnableNativeSink(int enabled);

        [DllImport(CudaLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CM_SetLogCallback(NativeLogCallback callback);

        [DllImport(CudaLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CM_SetLogEnabled(int enabled);

        [DllImport(CudaLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CM_SetLogLevel(int level);

        [DllImport(CudaLib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CM_EnableNativeSink(int enabled);

        public static void Initialize(
            Action<NativeLogSource, NativeLogLevel, string> sink,
            NativeLogLevel level = NativeLogLevel.Info,
            bool enableLogs = true,
            bool enableNativeSink = false)
        {
            _sink = sink;
            _callback ??= OnNativeLog;

            TryInitHelper(level, enableLogs, enableNativeSink);
            TryInitCuda(level, enableLogs, enableNativeSink);
        }

        private static void TryInitHelper(NativeLogLevel level, bool enableLogs, bool enableNativeSink)
        {
            try
            {
                M_SetLogCallback(_callback!);
                M_SetLogLevel((int)level);
                M_SetLogEnabled(enableLogs ? 1 : 0);
                M_EnableNativeSink(enableNativeSink ? 1 : 0);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static void TryInitCuda(NativeLogLevel level, bool enableLogs, bool enableNativeSink)
        {
            try
            {
                CM_SetLogCallback(_callback!);
                CM_SetLogLevel((int)level);
                CM_SetLogEnabled(enableLogs ? 1 : 0);
                CM_EnableNativeSink(enableNativeSink ? 1 : 0);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private static void OnNativeLog(int source, int level, IntPtr messagePtr)
        {
            if (_sink == null)
            {
                return;
            }

            string message = Marshal.PtrToStringAnsi(messagePtr) ?? string.Empty;
            NativeLogSource src = Enum.IsDefined(typeof(NativeLogSource), source)
                ? (NativeLogSource)source
                : NativeLogSource.Unknown;
            NativeLogLevel lvl = Enum.IsDefined(typeof(NativeLogLevel), level)
                ? (NativeLogLevel)level
                : NativeLogLevel.Info;

            _sink(src, lvl, message);
        }
    }
}
