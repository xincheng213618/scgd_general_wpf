#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace ColorVision.Windowing;

/// <summary>
/// Explicit diagnostic-build instrumentation, not a rendering fix. No Rendering
/// subscription, forced layout, window mutation, or user-content collection.
/// F12 exports a bounded numeric snapshot; ordinary builds contain none of this type.
/// </summary>
internal sealed class MainWindowResizeDiagnostics : IDisposable
{
    private const int Capacity = 4096;
    private const int EditorCapacity = 64;
    private const nuint SubclassId = 0x43565244;
    private static readonly List<WeakReference<MainWindowResizeDiagnostics>> Observers = [];
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private static bool editorLoadedHandlerRegistered;
    private readonly MainWindow window;
    private readonly FrameworkElement root, title, body;
    private readonly bool configuredCompact, selectedCompact, modeOverrideApplied;
    private readonly Entry[] entries = new Entry[Capacity];
    private readonly List<EditorReference> editors = [];
    private readonly SubclassProc callback;
    private readonly DispatcherTimer expiry;
    private readonly long originTimestamp = Stopwatch.GetTimestamp();
    private readonly long originUtcTicks = DateTime.UtcNow.Ticks;
    private IntPtr handle;
    private HwndSource? source;
    private WindowChrome? observedChrome;
    private FrameMetrics previousFrame;
    private long untilTimestamp, sequence, callSequence, currentCall, captureId;
    private long dropped, observerErrors, droppedEditors, exportErrors, skippedExports;
    private long editorLoadedEvents, editorLoadedUnmatched, editorLoadedErrors;
    private long editorScanCount, editorScanVisited, editorScanMatches, editorScanLimitHits, editorScanErrors;
    private int count, depth, pendingExport;
    private uint currentMessage;
    private int lastSizeKind;
    private WindowState lastWindowState;
    private bool installed, capturing, closed, destroyed, disposed, layoutSubscribed, attachedChromeAtSourceInitialized;

    internal static bool? ParseModeOverride(string? text) => text?.Trim() switch
    {
        "native" => false,
        "compact" => true,
        _ => null
    };

    internal static bool ParseMode(string? text, bool configured) => ParseModeOverride(text) ?? configured;

    internal static bool SelectMode(bool configured, out bool overrideApplied)
    {
        overrideApplied = false;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "window-resize-diagnostics.mode");
            if (!File.Exists(path) || new FileInfo(path).Length > 64)
                return configured;
            using var reader = new StreamReader(path);
            var characters = new char[65];
            int length = reader.ReadBlock(characters, 0, characters.Length);
            if (length > 64) return configured;
            string modeText = new string(characters, 0, length);
            bool? mode = ParseModeOverride(modeText);
            overrideApplied = mode.HasValue;
            return mode ?? configured;
        }
        catch (Exception)
        {
            // An optional diagnostic file must never prevent normal startup.
            return configured;
        }
    }

    internal static void Register(MainWindow window, bool configured, bool selected, bool overrideApplied)
    {
        void Attach(object? sender, EventArgs args)
        {
            window.SourceInitialized -= Attach;
            MainWindowResizeDiagnostics? observer = null;
            try
            {
                observer = new MainWindowResizeDiagnostics(window, configured, selected, overrideApplied);
                observer.Attach();
            }
            catch (Exception)
            {
                try { observer?.Dispose(); }
                catch (Exception) { }
            }
        }
        // Factory registration happens after the CompactMainWindow constructor:
        // its existing SourceInitialized chrome/guard setup runs before this one.
        window.SourceInitialized += Attach;
    }

    private MainWindowResizeDiagnostics(MainWindow window, bool configured, bool selected, bool overrideApplied)
    {
        this.window = window;
        root = window.Root;
        title = window.MainWindowTitleBar;
        body = window.DockingManager1;
        configuredCompact = configured;
        selectedCompact = selected;
        modeOverrideApplied = overrideApplied;
        callback = WindowProc;
        expiry = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher);
        expiry.Tick += OnExpiry;
    }

    private void Attach()
    {
        handle = new WindowInteropHelper(window).Handle;
        uint thread = GetWindowThreadProcessId(handle, out uint process);
        if (process != Environment.ProcessId || thread != GetCurrentThreadId())
            throw new InvalidOperationException();
        source = HwndSource.FromHwnd(handle);
        if (source == null || source.IsDisposed)
            throw new InvalidOperationException();
        lastWindowState = window.WindowState;
        lastSizeKind = lastWindowState == WindowState.Maximized ? 2 : lastWindowState == WindowState.Minimized ? 1 : 0;
        installed = SetWindowSubclass(handle, callback, SubclassId, 0);
        if (!installed)
            throw new InvalidOperationException();
        window.Loaded += OnLoaded;
        window.StateChanged += OnStateChanged;
        window.SizeChanged += OnWindowSizeChanged;
        title.SizeChanged += OnTitleSizeChanged;
        window.PreviewKeyDown += OnKeyDown;
        window.Closed += OnClosed;
        ObserveChrome();
        attachedChromeAtSourceInitialized = observedChrome != null;
        Observers.Add(new WeakReference<MainWindowResizeDiagnostics>(this));
        if (!editorLoadedHandlerRegistered)
        {
            // A static method and weak observer/editor references do not retain old
            // windows or documents. Loaded is a direct event, hence a class handler.
            EventManager.RegisterClassHandler(typeof(STNodeEditor), FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnEditorLoaded), true);
            editorLoadedHandlerRegistered = true;
        }
        Record(1);
    }

    private static void OnEditorLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not STNodeEditor editor)
            return;
        for (int index = Observers.Count - 1; index >= 0; index--)
        {
            if (!Observers[index].TryGetTarget(out MainWindowResizeDiagnostics? observer) || observer.disposed)
            {
                Observers.RemoveAt(index);
                continue;
            }
            if (!observer.window.Dispatcher.CheckAccess()) continue;
            observer.editorLoadedEvents++;
            try
            {
                // A docking document may not inherit WindowService along its logical
                // ancestry. Its actual presentation source still identifies this HWND;
                // floating windows and other dispatchers remain outside this capture.
                if (ReferenceEquals(Window.GetWindow(editor), observer.window) ||
                    ReferenceEquals(PresentationSource.FromVisual(editor), observer.source))
                    observer.TrackEditor(editor);
                else
                    observer.editorLoadedUnmatched++;
            }
            catch (Exception)
            {
                observer.editorLoadedErrors++;
                observer.observerErrors++;
            }
        }
    }

    private void TrackEditor(STNodeEditor editor)
    {
        foreach (EditorReference item in editors)
            if (item.Reference.TryGetTarget(out STNodeEditor? existing) && ReferenceEquals(existing, editor))
                return;
        if (editors.Count == EditorCapacity) { droppedEditors++; return; }
        var tracked = new EditorReference(editors.Count + 1, editor);
        editors.Add(tracked);
        if (capturing)
            TryObserve(() => editor.BeginResizeDiagnosticCapture(untilTimestamp));
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        window.Loaded -= OnLoaded;
        // One bounded startup scan covers controls loaded before class registration.
        // Later editors are discovered through Loaded or explicit F12, never resize.
        ScanCurrentEditors();
    }

    private void ScanCurrentEditors()
    {
        editorScanCount++;
        try
        {
            EditorScanResult result = ScanVisualEditors(window, TrackEditor, 20000);
            editorScanVisited += result.Visited;
            editorScanMatches += result.Matches;
            if (result.LimitReached) editorScanLimitHits++;
            editorScanErrors += result.Errors;
            observerErrors += result.Errors;
        }
        catch (Exception)
        {
            editorScanErrors++;
            observerErrors++;
        }
    }

    // Isolated tests can exercise discovery on detached synthetic visuals without
    // creating MainWindow, an HWND, a workspace, or a device service.
    internal static EditorScanResult ScanVisualEditors(DependencyObject scanRoot, Action<STNodeEditor> onEditor, int limit)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);
        ArgumentNullException.ThrowIfNull(onEditor);
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        var pending = new Stack<DependencyObject>();
        pending.Push(scanRoot);
        int visited = 0, matches = 0, errors = 0;
        while (pending.Count != 0 && visited < limit)
        {
            DependencyObject element = pending.Pop();
            visited++;
            if (element is STNodeEditor editor)
            {
                matches++;
                try { onEditor(editor); }
                catch (Exception) { errors++; }
            }
            try
            {
                for (int index = VisualTreeHelper.GetChildrenCount(element) - 1; index >= 0; index--)
                    pending.Push(VisualTreeHelper.GetChild(element, index));
            }
            catch (Exception) { errors++; }
        }
        return new EditorScanResult(visited, matches, pending.Count != 0, errors);
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data)
    {
        bool selected = message is 0x0005 or 0x0046 or 0x0047 or 0x007C or 0x007D or 0x0083 or 0x0112;
        if (!selected && message != 0x0082)
            return DefSubclassProc(hwnd, message, wParam, lParam);
        long parentCall = currentCall;
        uint parentMessage = currentMessage;
        depth++;
        currentMessage = message;
        long call = selected ? ++callSequence : parentCall;
        if (selected) currentCall = call;
        try
        {
            try
            {
                uint command = unchecked((uint)wParam.ToInt64()) & 0xFFF0;
                if (message == 0x0112 && command is 0xF030 or 0xF120)
                    BeginCapture();
                if (message == 0x0005)
                {
                    int kind = wParam.ToInt32();
                    if (kind == 2 || kind == 0 && lastSizeKind == 2) BeginCapture();
                    lastSizeKind = kind;
                }
                if (selected && CaptureIsActive())
                    RecordNative(message, wParam, lParam, 1, 0, parentCall, parentMessage);
            }
            catch (Exception) { observerErrors++; }
            // Exactly one original dispatch, outside observer exception handling.
            // Application exceptions and all native messages retain their semantics.
            IntPtr result = DefSubclassProc(hwnd, message, wParam, lParam);
            try
            {
                if (selected && CaptureIsActive())
                    RecordNative(message, wParam, lParam, 2, result.ToInt64(), parentCall, parentMessage);
                if (message == 0x0082)
                {
                    destroyed = true;
                    if (installed) RemoveWindowSubclass(hwnd, callback, SubclassId);
                    installed = false;
                    Dispose();
                }
            }
            catch (Exception) { observerErrors++; }
            return result;
        }
        finally
        {
            depth--;
            currentCall = parentCall;
            currentMessage = parentMessage;
        }
    }

    private void BeginCapture()
    {
        if (disposed || closed || window.WindowStyle != WindowStyle.SingleBorderWindow)
            return;
        long now = Stopwatch.GetTimestamp();
        if (!capturing)
        {
            capturing = true;
            captureId++;
            if (!layoutSubscribed) { window.LayoutUpdated += OnLayoutUpdated; layoutSubscribed = true; }
        }
        untilTimestamp = now + Stopwatch.Frequency;
        ObserveChrome();
        foreach (EditorReference item in editors)
            if (item.Reference.TryGetTarget(out STNodeEditor? editor))
                TryObserve(() => editor.BeginResizeDiagnosticCapture(untilTimestamp));
        expiry.Interval = TimeSpan.FromSeconds(1);
        if (!expiry.IsEnabled) expiry.Start();
    }

    private bool CaptureIsActive()
    {
        if (!capturing || disposed || closed)
            return false;
        if (Stopwatch.GetTimestamp() <= untilTimestamp)
            return true;
        StopCapture();
        return false;
    }

    private void StopCapture()
    {
        capturing = false;
        expiry.Stop();
        if (layoutSubscribed) window.LayoutUpdated -= OnLayoutUpdated;
        layoutSubscribed = false;
        foreach (EditorReference item in editors)
            if (item.Reference.TryGetTarget(out STNodeEditor? editor))
                TryObserve(editor.StopResizeDiagnosticCapture);
    }

    private void OnExpiry(object? sender, EventArgs args)
    {
        TryObserve(() =>
        {
            long remaining = untilTimestamp - Stopwatch.GetTimestamp();
            if (remaining > 0) { expiry.Interval = TimeSpan.FromSeconds((double)remaining / Stopwatch.Frequency); return; }
            Record(8);
            StopCapture();
            CacheEditorSnapshots();
        });
    }

    private void ObserveChrome()
    {
        WindowChrome? chrome = WindowChrome.GetWindowChrome(window);
        if (ReferenceEquals(chrome, observedChrome)) return;
        if (observedChrome != null) observedChrome.Changed -= OnChromeChanged;
        observedChrome = chrome;
        if (observedChrome != null) observedChrome.Changed += OnChromeChanged;
        previousFrame = ReadFrame();
    }

    private void OnChromeChanged(object? sender, EventArgs args)
    {
        try
        {
            FrameMetrics current = ReadFrame();
            if (CaptureIsActive() && !current.Equals(previousFrame)) Record(6);
            previousFrame = current;
        }
        catch (Exception) { observerErrors++; }
    }
    private void OnStateChanged(object? sender, EventArgs args)
    {
        try
        {
            WindowState state = window.WindowState;
            if (state != lastWindowState && (state == WindowState.Maximized || state == WindowState.Normal && lastWindowState == WindowState.Maximized))
                BeginCapture();
            lastWindowState = state;
            if (CaptureIsActive()) Record(3);
        }
        catch (Exception) { observerErrors++; }
    }
    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs args)
    {
        try { if (CaptureIsActive()) Record(4); }
        catch (Exception) { observerErrors++; }
    }
    private void OnTitleSizeChanged(object sender, SizeChangedEventArgs args)
    {
        try { if (CaptureIsActive()) Record(5); }
        catch (Exception) { observerErrors++; }
    }
    private void OnLayoutUpdated(object? sender, EventArgs args)
    {
        try { if (CaptureIsActive()) Record(7, nativeGeometry: false); }
        catch (Exception) { observerErrors++; }
    }

    private void RecordNative(uint message, IntPtr wParam, IntPtr lParam, int phase, long result, long parentCall, uint parentMessage)
    {
        var payload = new Payload { Message = message, Phase = phase, Result = result, ParentCall = parentCall, ParentMessage = parentMessage };
        if (message == 0x0005)
        {
            payload.Code = wParam.ToInt32();
            payload.Width = (ushort)(lParam.ToInt64() & 0xFFFF);
            payload.Height = (ushort)((lParam.ToInt64() >> 16) & 0xFFFF);
        }
        else if (message == 0x0112) payload.Code = (int)(wParam.ToInt64() & 0xFFF0);
        else if (message is 0x0046 or 0x0047 && lParam != IntPtr.Zero)
        {
            WindowPosition position = ReadNative<WindowPosition>(lParam);
            payload.X = position.X; payload.Y = position.Y; payload.Width = position.Width; payload.Height = position.Height; payload.Flags = position.Flags;
        }
        else if (message is 0x007C or 0x007D && lParam != IntPtr.Zero)
        {
            StyleChange change = ReadNative<StyleChange>(lParam);
            payload.Code = unchecked((int)wParam.ToInt64()); payload.OldStyle = change.Old; payload.NewStyle = change.New;
        }
        else if (message == 0x0083 && lParam != IntPtr.Zero)
        {
            payload.Code = wParam == IntPtr.Zero ? 0 : 1;
            // Both NCCALCSIZE forms start with the same RECT. No pointer is retained.
            payload.NcClient = ReadNative<NativeRect>(lParam);
        }
        Record(2, payload);
    }

    private void Record(int kind, Payload payload = default, bool nativeGeometry = true)
    {
        if (disposed) return;
        sequence++;
        if (count == Capacity) { dropped++; return; }
        var entry = new Entry
        {
            Sequence = sequence, Timestamp = Stopwatch.GetTimestamp(), CaptureId = captureId,
            Kind = kind, CallId = currentCall, Depth = depth, ActiveMessage = currentMessage, Native = payload,
            Window = LayoutNumbers.Read(window), Root = LayoutNumbers.Read(root),
            Title = LayoutNumbers.Read(title), Body = LayoutNumbers.Read(body), Frame = ReadFrame(),
            State = (int)window.WindowState, MarginLeft = root.Margin.Left, MarginTop = root.Margin.Top,
            MarginRight = root.Margin.Right, MarginBottom = root.Margin.Bottom
        };
        if (nativeGeometry && !destroyed)
        {
            entry.HasWindowRect = GetWindowRect(handle, out entry.WindowRect);
            entry.HasClientRect = GetClientRect(handle, out entry.ClientRect);
            entry.HasClientOrigin = ClientToScreen(handle, ref entry.ClientOrigin);
            entry.Dpi = GetDpiForWindow(handle);
            entry.Style = unchecked((uint)GetWindowLongPtr(handle, -16).ToInt64());
            entry.ExStyle = unchecked((uint)GetWindowLongPtr(handle, -20).ToInt64());
            if (source is { IsDisposed: false } && source.CompositionTarget is HwndTarget target)
            {
                Color color = target.BackgroundColor;
                entry.TargetArgb = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
                entry.DpiX = target.TransformToDevice.M11; entry.DpiY = target.TransformToDevice.M22;
            }
        }
        entries[count++] = entry;
    }

    private FrameMetrics ReadFrame()
    {
        WindowChrome? chrome = WindowChrome.GetWindowChrome(window);
        return chrome == null ? default : new FrameMetrics(true, chrome.CaptionHeight,
            chrome.GlassFrameThickness.Left, chrome.GlassFrameThickness.Top, chrome.GlassFrameThickness.Right, chrome.GlassFrameThickness.Bottom,
            chrome.ResizeBorderThickness.Left, chrome.ResizeBorderThickness.Top, chrome.ResizeBorderThickness.Right, chrome.ResizeBorderThickness.Bottom);
    }

    private void CacheEditorSnapshots()
    {
        foreach (EditorReference item in editors)
            if (item.Reference.TryGetTarget(out STNodeEditor? editor))
                TryObserve(() =>
                {
                    STRenderDiagnosticCapture snapshot = editor.GetResizeDiagnosticCapture();
                    // Disposing a document clears its editor's live diagnostic buffer.
                    // Retain only the last detached numeric snapshot, never the document.
                    if (!snapshot.IsDisposed) item.Snapshot = snapshot;
                });
    }

    private void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.F12 || Keyboard.Modifiers != ModifierKeys.None) return;
        args.Handled = true;
        Export(false);
    }

    private void OnClosed(object? sender, EventArgs args)
    {
        if (closed) return;
        TryObserve(() => Record(9));
        closed = true;
        TryObserve(StopCapture);
        // Closed is terminal, not a resize hot path. Complete this explicit export
        // before process shutdown can discard a queued background writer.
        Export(true);
        Dispose();
    }

    private void Export(bool terminal)
    {
        if (disposed) return;
        if (!terminal && Interlocked.CompareExchange(ref pendingExport, 1, 0) != 0) { skippedExports++; return; }
        try
        {
            // Explicit export is outside resize/render hot paths. A startup scan can
            // precede deferred document realization, so F12 rediscovers today's tree
            // and arms any newly found editor for the next real capture window.
            if (!terminal) ScanCurrentEditors();
            CacheEditorSnapshots();
            var copied = new Entry[count];
            Array.Copy(entries, copied, count);
            var editorCopies = new List<EditorSnapshot>();
            int editorsAlive = 0;
            foreach (EditorReference item in editors)
            {
                if (item.Reference.TryGetTarget(out _)) editorsAlive++;
                if (item.Snapshot != null) editorCopies.Add(new EditorSnapshot(item.Id, item.Snapshot));
            }
            Version runtime = Environment.Version;
            Version version = typeof(MainWindow).Assembly.GetName().Version ?? new Version();
            var snapshot = new
            {
                Schema = 1, Pid = Environment.ProcessId,
                ConfiguredCompact = configuredCompact, SelectedCompact = selectedCompact,
                ModeOverrideApplied = modeOverrideApplied,
                AttachedChromeAtSourceInitialized = attachedChromeAtSourceInitialized,
                EffectiveChromeAttachedAtExport = WindowChrome.GetWindowChrome(window) != null,
                RuntimeMajor = runtime.Major, RuntimeMinor = runtime.Minor, RuntimeBuild = runtime.Build, RuntimeRevision = runtime.Revision,
                AssemblyMajor = version.Major, AssemblyMinor = version.Minor, AssemblyBuild = version.Build, AssemblyRevision = version.Revision,
                DebuggerAttached = Debugger.IsAttached, Is64BitProcess = Environment.Is64BitProcess,
                OriginTimestamp = originTimestamp, OriginUtcTicks = originUtcTicks, StopwatchFrequency = Stopwatch.Frequency,
                RenderingSubscribed = false, IsPresentMeasurement = false, TerminalExport = terminal,
                Capacity, Count = count, Sequence = sequence, Dropped = dropped, ObserverErrors = observerErrors,
                DroppedEditors = droppedEditors, ExportErrors = Interlocked.Read(ref exportErrors), SkippedExports = skippedExports,
                EditorDiscovery = new
                {
                    Tracked = editors.Count, Alive = editorsAlive, WithSnapshots = editorCopies.Count,
                    LoadedEvents = editorLoadedEvents, LoadedUnmatched = editorLoadedUnmatched, LoadedErrors = editorLoadedErrors,
                    Scans = editorScanCount, ScannedVisuals = editorScanVisited, ScanMatches = editorScanMatches,
                    ScanLimitHits = editorScanLimitHits, ScanErrors = editorScanErrors
                },
                Entries = copied, Editors = editorCopies.ToArray()
            };
            void Write()
            {
                try
                {
                    string directory = Path.Combine(AppContext.BaseDirectory, "window-resize-traces");
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, $"resize-{Environment.ProcessId}-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.json");
                    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    JsonSerializer.Serialize(stream, snapshot, Json);
                }
                catch (Exception) { Interlocked.Increment(ref exportErrors); }
                finally { if (!terminal) Interlocked.Exchange(ref pendingExport, 0); }
            }
            if (terminal) Write();
            else _ = Task.Run(Write);
        }
        catch (Exception)
        {
            Interlocked.Increment(ref exportErrors);
            if (!terminal) Interlocked.Exchange(ref pendingExport, 0);
        }
    }

    private void TryObserve(Action action)
    {
        try { action(); }
        catch (Exception) { observerErrors++; }
    }

    public void Dispose()
    {
        if (disposed) return;
        TryObserve(StopCapture);
        disposed = true;
        expiry.Tick -= OnExpiry;
        window.Loaded -= OnLoaded;
        window.StateChanged -= OnStateChanged;
        window.SizeChanged -= OnWindowSizeChanged;
        title.SizeChanged -= OnTitleSizeChanged;
        window.PreviewKeyDown -= OnKeyDown;
        window.Closed -= OnClosed;
        if (observedChrome != null) observedChrome.Changed -= OnChromeChanged;
        observedChrome = null;
        if (installed && !destroyed) RemoveWindowSubclass(handle, callback, SubclassId);
        installed = false;
        for (int index = Observers.Count - 1; index >= 0; index--)
            if (!Observers[index].TryGetTarget(out MainWindowResizeDiagnostics? observer) || ReferenceEquals(observer, this))
                Observers.RemoveAt(index);
    }

    private sealed class EditorReference(int id, STNodeEditor editor)
    {
        internal readonly int Id = id;
        internal readonly WeakReference<STNodeEditor> Reference = new(editor);
        internal STRenderDiagnosticCapture? Snapshot;
    }
    private sealed record EditorSnapshot(int Id, STRenderDiagnosticCapture Capture);
    internal readonly record struct EditorScanResult(int Visited, int Matches, bool LimitReached, int Errors);
    private readonly record struct FrameMetrics(bool Attached, double Caption,
        double GlassLeft, double GlassTop, double GlassRight, double GlassBottom,
        double ResizeLeft, double ResizeTop, double ResizeRight, double ResizeBottom);
    private struct LayoutNumbers
    {
        public double ActualWidth, ActualHeight, RenderWidth, RenderHeight;
        public bool MeasureValid, ArrangeValid;
        internal static LayoutNumbers Read(FrameworkElement element) => new()
        {
            ActualWidth = element.ActualWidth, ActualHeight = element.ActualHeight,
            RenderWidth = element.RenderSize.Width, RenderHeight = element.RenderSize.Height,
            MeasureValid = element.IsMeasureValid, ArrangeValid = element.IsArrangeValid
        };
    }
    private struct Entry
    {
        public long Sequence, Timestamp, CaptureId, CallId;
        public int Kind, Depth, State;
        public uint ActiveMessage, Dpi, Style, ExStyle, TargetArgb;
        public Payload Native;
        public LayoutNumbers Window, Root, Title, Body;
        public FrameMetrics Frame;
        public double MarginLeft, MarginTop, MarginRight, MarginBottom, DpiX, DpiY;
        public bool HasWindowRect, HasClientRect, HasClientOrigin;
        public NativeRect WindowRect, ClientRect;
        public NativePoint ClientOrigin;
    }
    private struct Payload
    {
        public uint Message, ParentMessage, Flags, OldStyle, NewStyle;
        public int Phase, Code, X, Y, Width, Height;
        public long Result, ParentCall;
        public NativeRect NcClient;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct WindowPosition { public IntPtr Window, InsertAfter; public int X, Y, Width, Height; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct StyleChange { public uint Old, New; }
    private static unsafe T ReadNative<T>(IntPtr pointer) where T : unmanaged => *(T*)pointer.ToPointer();
    private delegate IntPtr SubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, nuint id, nuint data);
    [DllImport("comctl32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, nuint id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
}
#endif
