using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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

    public readonly record struct NativeLogRecord(
        DateTimeOffset Timestamp,
        int ManagedThreadId,
        NativeLogSource Source,
        NativeLogLevel Level,
        string Message);

    public sealed record NativeLogInitializationResult(
        bool HelperAvailable,
        bool CudaAvailable,
        IReadOnlyList<string> Diagnostics)
    {
        public bool AnySourceAvailable => HelperAvailable || CudaAvailable;

        public string Summary
        {
            get
            {
                if (AnySourceAvailable)
                {
                    string sources = HelperAvailable && CudaAvailable
                        ? "opencv_helper, opencv_cuda"
                        : HelperAvailable ? "opencv_helper" : "opencv_cuda";
                    return $"Native log capture ready: {sources}.";
                }

                return Diagnostics.Count > 0
                    ? string.Join(" ", Diagnostics)
                    : "No native logging source is available.";
            }
        }
    }

    /// <summary>
    /// Process-local bridge for the optional native logging callbacks. The
    /// native libraries are not loaded until <see cref="InitializeWithResult"/>
    /// is called, and logging remains disabled by default.
    /// </summary>
    public static class NativeLogBridge
    {
        private const string HelperLib = "opencv_helper.dll";
        private const string CudaLib = "opencv_cuda.dll";
        private static readonly object StateSync = new();
        private static readonly object SubscriberSync = new();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void NativeLogCallback(int source, int level, IntPtr messagePtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetCallbackDelegate(IntPtr callback);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetIntDelegate(int value);

        private sealed class NativeLogApi
        {
            public NativeLogApi(
                string name,
                IntPtr module,
                SetCallbackDelegate setCallback,
                SetIntDelegate setEnabled,
                SetIntDelegate setLevel,
                SetIntDelegate setNativeSink)
            {
                Name = name;
                Module = module;
                SetCallback = setCallback;
                SetEnabled = setEnabled;
                SetLevel = setLevel;
                SetNativeSink = setNativeSink;
            }

            public string Name { get; }
            public IntPtr Module { get; }
            public SetCallbackDelegate SetCallback { get; }
            public SetIntDelegate SetEnabled { get; }
            public SetIntDelegate SetLevel { get; }
            public SetIntDelegate SetNativeSink { get; }
        }

        private static NativeLogCallback? _callback;
        private static Action<NativeLogSource, NativeLogLevel, string>? _sink;
        private static NativeLogApi? _helperApi;
        private static NativeLogApi? _cudaApi;
        private static Action<NativeLogRecord>[] _subscribers = [];
        private static int _isInitialized;
        private static int _isEnabled;
        private static int _level = (int)NativeLogLevel.Info;
        private static int _isNativeSinkEnabled;
        private static int _cudaAttachAttempted;
        private static NativeLogInitializationResult _lastInitializationResult =
            new(false, false, ["Native logging has not been initialized."]);

        public static event Action<NativeLogRecord> LogReceived
        {
            add
            {
                ArgumentNullException.ThrowIfNull(value);
                lock (SubscriberSync)
                {
                    Action<NativeLogRecord>[] current = _subscribers;
                    Action<NativeLogRecord>[] updated = new Action<NativeLogRecord>[current.Length + 1];
                    Array.Copy(current, updated, current.Length);
                    updated[^1] = value;
                    Volatile.Write(ref _subscribers, updated);
                }
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                lock (SubscriberSync)
                {
                    Action<NativeLogRecord>[] current = _subscribers;
                    int index = Array.LastIndexOf(current, value);
                    if (index < 0)
                    {
                        return;
                    }

                    if (current.Length == 1)
                    {
                        Volatile.Write(ref _subscribers, []);
                        return;
                    }

                    Action<NativeLogRecord>[] updated = new Action<NativeLogRecord>[current.Length - 1];
                    if (index > 0)
                    {
                        Array.Copy(current, 0, updated, 0, index);
                    }
                    if (index < current.Length - 1)
                    {
                        Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                    }
                    Volatile.Write(ref _subscribers, updated);
                }
            }
        }

        public static bool IsInitialized => Volatile.Read(ref _isInitialized) != 0;
        public static bool IsEnabled => Volatile.Read(ref _isEnabled) != 0;
        public static NativeLogLevel Level => (NativeLogLevel)Volatile.Read(ref _level);
        public static bool IsNativeSinkEnabled => Volatile.Read(ref _isNativeSinkEnabled) != 0;
        public static NativeLogInitializationResult LastInitializationResult => Volatile.Read(ref _lastInitializationResult);

        public static bool IsSourceAvailable(NativeLogSource source)
        {
            NativeLogInitializationResult result = LastInitializationResult;
            return source switch
            {
                NativeLogSource.OpencvHelper => result.HelperAvailable,
                NativeLogSource.OpencvCuda => result.CudaAvailable,
                _ => false,
            };
        }

        public static void Initialize(
            Action<NativeLogSource, NativeLogLevel, string> sink,
            NativeLogLevel level = NativeLogLevel.Info,
            bool enableLogs = false,
            bool enableNativeSink = false)
        {
            ArgumentNullException.ThrowIfNull(sink);
            InitializeWithResult(sink, level, enableLogs, enableNativeSink);
        }

        public static NativeLogInitializationResult InitializeWithResult(
            Action<NativeLogSource, NativeLogLevel, string>? sink = null,
            NativeLogLevel level = NativeLogLevel.Info,
            bool enableLogs = false,
            bool enableNativeSink = false)
        {
            ValidateLevel(level);

            lock (StateSync)
            {
                if (enableLogs && _cudaApi == null)
                {
                    Volatile.Write(ref _cudaAttachAttempted, 0);
                }

                if (sink != null)
                {
                    Volatile.Write(ref _sink, sink);
                }

                _callback ??= OnNativeLog;
                List<string> diagnostics = [];

                if (_helperApi == null)
                {
                    if (TryLoadLibrary(HelperLib, out IntPtr helperModule, out string? helperLoadError))
                    {
                        NativeLogApi? helperApi = TryCreateApi(
                            HelperLib,
                            helperModule,
                            GetExportPrefix(NativeLogSource.OpencvHelper),
                            diagnostics);
                        if (helperApi == null)
                        {
                            NativeLibrary.Free(helperModule);
                        }
                        else
                        {
                            _helperApi = helperApi;
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(helperLoadError))
                    {
                        diagnostics.Add(helperLoadError);
                    }
                }

                if (_cudaApi == null)
                {
                    TryAttachLoadedCuda(diagnostics);
                }

                bool helperReady = ConfigureApi(_helperApi, level, enableLogs, enableNativeSink, diagnostics);
                bool cudaReady = ConfigureApi(_cudaApi, level, enableLogs, enableNativeSink, diagnostics);
                if (_cudaApi == null)
                {
                    diagnostics.Add("opencv_cuda is not attached; it will be attached on the next managed CUDA call while capture is enabled.");
                }

                bool enabled = enableLogs && (helperReady || cudaReady);
                Volatile.Write(ref _level, (int)level);
                Volatile.Write(ref _isNativeSinkEnabled, enableNativeSink && (helperReady || cudaReady) ? 1 : 0);
                Volatile.Write(ref _isEnabled, enabled ? 1 : 0);
                Volatile.Write(ref _isInitialized, 1);

                NativeLogInitializationResult result = new(helperReady, cudaReady, diagnostics.ToArray());
                Volatile.Write(ref _lastInitializationResult, result);
                return result;
            }
        }

        public static bool Configure(
            NativeLogLevel level,
            bool enableLogs,
            bool enableNativeSink = false)
        {
            ValidateLevel(level);
            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0)
                {
                    return InitializeWithResult(null, level, enableLogs, enableNativeSink).AnySourceAvailable;
                }

                List<string> diagnostics = [];
                bool helperReady = ConfigureApi(_helperApi, level, enableLogs, enableNativeSink, diagnostics);
                bool cudaReady = ConfigureApi(_cudaApi, level, enableLogs, enableNativeSink, diagnostics);
                bool anyReady = helperReady || cudaReady;

                Volatile.Write(ref _level, (int)level);
                Volatile.Write(ref _isNativeSinkEnabled, enableNativeSink && anyReady ? 1 : 0);
                Volatile.Write(ref _isEnabled, enableLogs && anyReady ? 1 : 0);

                NativeLogInitializationResult result = new(helperReady, cudaReady, diagnostics.ToArray());
                Volatile.Write(ref _lastInitializationResult, result);
                return anyReady;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0)
                {
                    if (!enabled)
                    {
                        Volatile.Write(ref _isEnabled, 0);
                        return true;
                    }

                    return InitializeWithResult(null, Level, true, IsNativeSinkEnabled).AnySourceAvailable;
                }

                return Configure(Level, enabled, IsNativeSinkEnabled);
            }
        }

        public static bool SetLevel(NativeLogLevel level)
        {
            ValidateLevel(level);
            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0)
                {
                    Volatile.Write(ref _level, (int)level);
                    return true;
                }

                return Configure(level, IsEnabled, IsNativeSinkEnabled);
            }
        }

        public static bool SetNativeSinkEnabled(bool enabled)
        {
            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0)
                {
                    Volatile.Write(ref _isNativeSinkEnabled, enabled ? 1 : 0);
                    return true;
                }

                return Configure(Level, IsEnabled, enabled);
            }
        }

        public static void Shutdown()
        {
            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0)
                {
                    return;
                }

                DisableAndDetach(_helperApi);
                DisableAndDetach(_cudaApi);
                Volatile.Write(ref _isEnabled, 0);
                Volatile.Write(ref _isNativeSinkEnabled, 0);
                Volatile.Write(ref _cudaAttachAttempted, 0);
                Volatile.Write(ref _sink, (Action<NativeLogSource, NativeLogLevel, string>?)null);
                Volatile.Write(ref _isInitialized, 0);

                // Keep _callback rooted for the process lifetime. A native
                // producer that captured it just before disable may still be
                // returning through the reverse P/Invoke.
            }
        }

        private static bool ConfigureApi(
            NativeLogApi? api,
            NativeLogLevel level,
            bool enableLogs,
            bool enableNativeSink,
            List<string> diagnostics)
        {
            if (api == null)
            {
                return false;
            }

            try
            {
                api.SetEnabled(0);
                api.SetCallback(Marshal.GetFunctionPointerForDelegate(_callback!));
                api.SetLevel((int)level);
                api.SetNativeSink(enableNativeSink ? 1 : 0);
                api.SetEnabled(enableLogs ? 1 : 0);
                return true;
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Unable to configure {api.Name}: {ex.Message}");
                try
                {
                    api.SetEnabled(0);
                }
                catch
                {
                }
                return false;
            }
        }

        private static void DisableAndDetach(NativeLogApi? api)
        {
            if (api == null)
            {
                return;
            }

            try
            {
                api.SetEnabled(0);
                api.SetNativeSink(0);
                api.SetCallback(IntPtr.Zero);
            }
            catch
            {
                // Diagnostics must not interfere with application shutdown.
            }
        }

        internal static void PrepareForNativeCall(NativeLogSource source)
        {
            if (source != NativeLogSource.OpencvCuda
                || !IsEnabled
                || Volatile.Read(ref _cudaApi) != null
                || Volatile.Read(ref _cudaAttachAttempted) != 0)
            {
                return;
            }

            lock (StateSync)
            {
                if (Volatile.Read(ref _isInitialized) == 0
                    || Volatile.Read(ref _isEnabled) == 0
                    || _cudaApi != null
                    || Volatile.Read(ref _cudaAttachAttempted) != 0)
                {
                    return;
                }

                List<string> diagnostics = [];
                Volatile.Write(ref _cudaAttachAttempted, 1);
                bool cudaReady = TryLoadAndConfigureCuda(
                    Level,
                    enableLogs: true,
                    enableNativeSink: IsNativeSinkEnabled,
                    diagnostics: diagnostics);
                NativeLogInitializationResult result = new(
                    _helperApi != null,
                    cudaReady,
                    diagnostics.ToArray());
                Volatile.Write(ref _lastInitializationResult, result);
            }
        }

        private static void TryAttachLoadedCuda(List<string> diagnostics)
        {
            if (_cudaApi != null || GetModuleHandle(CudaLib) == IntPtr.Zero)
            {
                return;
            }

            Volatile.Write(ref _cudaAttachAttempted, 1);
            _ = TryLoadAndCreateApi(
                CudaLib,
                NativeLogSource.OpencvCuda,
                diagnostics,
                out _cudaApi);
        }

        private static bool TryLoadAndConfigureCuda(
            NativeLogLevel level,
            bool enableLogs,
            bool enableNativeSink,
            List<string> diagnostics)
        {
            if (_cudaApi == null
                && !TryLoadAndCreateApi(
                    CudaLib,
                    NativeLogSource.OpencvCuda,
                    diagnostics,
                    out _cudaApi))
            {
                return false;
            }

            return ConfigureApi(_cudaApi, level, enableLogs, enableNativeSink, diagnostics);
        }

        private static bool TryLoadAndCreateApi(
            string libraryName,
            NativeLogSource source,
            List<string> diagnostics,
            out NativeLogApi? api)
        {
            api = null;
            if (!TryLoadLibrary(libraryName, out IntPtr module, out string? loadError))
            {
                if (!string.IsNullOrWhiteSpace(loadError))
                {
                    diagnostics.Add(loadError);
                }
                return false;
            }

            api = TryCreateApi(libraryName, module, GetExportPrefix(source), diagnostics);
            if (api != null)
            {
                return true;
            }

            NativeLibrary.Free(module);
            return false;
        }

        private static bool TryLoadLibrary(string libraryName, out IntPtr module, out string? error)
        {
            module = IntPtr.Zero;
            error = null;
            try
            {
                if (NativeLibrary.TryLoad(
                    libraryName,
                    typeof(NativeLogBridge).Assembly,
                    DllImportSearchPath.SafeDirectories,
                    out module))
                {
                    return true;
                }

                error = $"{libraryName} could not be loaded.";
                return false;
            }
            catch (Exception ex) when (ex is DllNotFoundException
                or BadImageFormatException
                or FileLoadException)
            {
                error = $"{libraryName} could not be loaded: {ex.Message}";
                return false;
            }
        }

        private static NativeLogApi? TryCreateApi(
            string name,
            IntPtr module,
            string exportPrefix,
            List<string> diagnostics)
        {
            try
            {
                return new NativeLogApi(
                    name,
                    module,
                    GetExport<SetCallbackDelegate>(module, $"{exportPrefix}SetLogCallback"),
                    GetExport<SetIntDelegate>(module, $"{exportPrefix}SetLogEnabled"),
                    GetExport<SetIntDelegate>(module, $"{exportPrefix}SetLogLevel"),
                    GetExport<SetIntDelegate>(module, $"{exportPrefix}EnableNativeSink"));
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException
                or ArgumentException
                or MarshalDirectiveException)
            {
                diagnostics.Add($"{name} does not expose the expected logging ABI: {ex.Message}");
                return null;
            }
        }

        private static string GetExportPrefix(NativeLogSource source)
        {
            return source == NativeLogSource.OpencvCuda ? "CM_" : "M_";
        }

        private static T GetExport<T>(IntPtr module, string exportName) where T : Delegate
        {
            IntPtr address = NativeLibrary.GetExport(module, exportName);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        private static void OnNativeLog(int source, int level, IntPtr messagePtr)
        {
            if (!IsEnabled || messagePtr == IntPtr.Zero)
            {
                return;
            }

            try
            {
                string message = Marshal.PtrToStringUTF8(messagePtr) ?? string.Empty;
                NativeLogSource mappedSource = source switch
                {
                    (int)NativeLogSource.OpencvHelper => NativeLogSource.OpencvHelper,
                    (int)NativeLogSource.OpencvCuda => NativeLogSource.OpencvCuda,
                    _ => NativeLogSource.Unknown,
                };
                NativeLogLevel mappedLevel = level switch
                {
                    (int)NativeLogLevel.Trace => NativeLogLevel.Trace,
                    (int)NativeLogLevel.Debug => NativeLogLevel.Debug,
                    (int)NativeLogLevel.Info => NativeLogLevel.Info,
                    (int)NativeLogLevel.Warn => NativeLogLevel.Warn,
                    (int)NativeLogLevel.Error => NativeLogLevel.Error,
                    _ => NativeLogLevel.Info,
                };

                Action<NativeLogSource, NativeLogLevel, string>? sink = Volatile.Read(ref _sink);
                if (sink != null)
                {
                    try
                    {
                        sink(mappedSource, mappedLevel, message);
                    }
                    catch
                    {
                    }
                }

                Action<NativeLogRecord>[] subscribers = Volatile.Read(ref _subscribers);
                if (subscribers.Length == 0)
                {
                    return;
                }

                NativeLogRecord record = new(
                    DateTimeOffset.Now,
                    Environment.CurrentManagedThreadId,
                    mappedSource,
                    mappedLevel,
                    message);
                foreach (Action<NativeLogRecord> subscriber in subscribers)
                {
                    try
                    {
                        subscriber(record);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                // Never allow managed diagnostics to escape through the native ABI.
            }
        }

        private static void ValidateLevel(NativeLogLevel level)
        {
            if (level < NativeLogLevel.Trace || level > NativeLogLevel.Error)
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
