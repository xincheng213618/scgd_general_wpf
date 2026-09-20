using ColorVision.FileIO;
using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Media3D;

namespace HeightMapValidation;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    [STAThread]
    private static int Main(string[] args)
    {
        var options = args.Chunk(2).ToDictionary(pair => pair[0].TrimStart('-'), pair => pair.Length > 1 ? pair[1] : "true");
        string output = Path.GetFullPath(options.GetValueOrDefault("output", "artifacts/heightmap-validation/run"));
        Directory.CreateDirectory(output);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => File.WriteAllText(Path.Combine(output, "fatal.txt"), e.ExceptionObject.ToString());
        ConfigService.SetInstance(new MemoryConfig());
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            File.WriteAllText(Path.Combine(output, "error.txt"), e.Exception.ToString());
            e.Handled = true;
            app.Shutdown(1);
        };
        app.Startup += async (_, _) =>
        {
            try { await Run(options, output); app.Shutdown(); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(output, "error.txt"), ex.ToString()); app.Shutdown(1); }
        };
        return app.Run();
    }

    private static async Task Run(Dictionary<string, string> options, string output)
    {
        if (options.TryGetValue("inspect-model", out string? modelPath))
        {
            var loader = typeof(ColorVision.ImageEditor.Window3D).Assembly.GetType("ColorVision.ImageEditor.EditorTools.ThreeD.ModelViewer3DLoader")!;
            var watch = Stopwatch.StartNew();
            var loadTask = (Task)loader.GetMethod("LoadAsync")!.Invoke(null, [Path.GetFullPath(modelPath), CancellationToken.None])!;
            await loadTask;
            object model = Property(loadTask, "Result")!;
            try
            {
                File.WriteAllText(Path.Combine(output, "reimport.json"), JsonSerializer.Serialize(new
                {
                    Source = Path.GetFullPath(modelPath), LoadMilliseconds = watch.Elapsed.TotalMilliseconds,
                    Statistics = Property(model, "Statistics"), Bounds = Property(model, "Bounds")
                }, JsonOptions));
            }
            finally { ((IDisposable)model).Dispose(); }
            return;
        }
        string source = options.GetValueOrDefault("source", "synthetic-smooth");
        string mode = options.GetValueOrDefault("mode", "legacy");
        int target = int.Parse(options.GetValueOrDefault("target", "512"));
        int interaction = int.Parse(options.GetValueOrDefault("interaction", "512"));
        int duration = int.Parse(options.GetValueOrDefault("seconds", "12"));
        var decodeWatch = Stopwatch.StartNew();
        WriteableBitmap bitmap = LoadBitmap(source);
        double decodeMs = decodeWatch.Elapsed.TotalMilliseconds;
        var timings = new Dictionary<string, object?>
        {
            ["mode"] = mode, ["source"] = source, ["sourceWidth"] = bitmap.PixelWidth, ["sourceHeight"] = bitmap.PixelHeight,
            ["sourceFormat"] = bitmap.Format.ToString(), ["decodeAndWriteableBitmapMs"] = decodeMs,
            ["requestedTarget"] = target, ["logicalWindowWidth"] = 1280, ["logicalWindowHeight"] = 820,
            ["interactionTarget"] = interaction,
            ["processId"] = Environment.ProcessId, ["processorCount"] = Environment.ProcessorCount,
            ["frameMetric"] = "CompositionTarget.Rendering callback wall-clock interval; not GPU render or present latency",
            ["gpuTiming"] = "Unavailable: no per-frame GPU timestamp/present tracing used",
            ["trajectory"] = "Absolute yaw=15*sin(t*.75) degrees, pitch=8*sin(t*.53) degrees; applied on composition callback",
        };
        var openWatch = Stopwatch.StartNew();
        Window window;
        Action<double> animate;
        Task ready;
        if (mode == "legacy")
        {
            Legacy.Window3DConfig.Instance.TargetPixelsX = target;
            Legacy.Window3DConfig.Instance.TargetPixelsY = target;
            var legacy = new Legacy.Window3D(bitmap);
            window = legacy;
            animate = legacy.Animate;
            ready = legacy.Ready.Task;
        }
        else
        {
            ColorVision.ImageEditor.Window3D.Config.TargetPixelsX = interaction;
            ColorVision.ImageEditor.Window3D.Config.TargetPixelsY = interaction;
            typeof(ColorVision.ImageEditor.Window3DConfig).GetProperty("DetailResolution")?.SetValue(ColorVision.ImageEditor.Window3D.Config, target);
            typeof(ColorVision.ImageEditor.Window3DConfig).GetProperty("AdaptiveDetail")?.SetValue(ColorVision.ImageEditor.Window3D.Config, options.GetValueOrDefault("adaptive", "true") == "true");
            window = new ColorVision.ImageEditor.Window3D(bitmap);
            double previousYaw = 0, previousPitch = 0;
            animate = seconds =>
            {
                double yaw = 15 * Math.Sin(seconds * .75), pitch = 8 * Math.Sin(seconds * .53);
                Invoke(window, "Rotate", yaw - previousYaw, pitch - previousPitch);
                previousYaw = yaw; previousPitch = pitch;
            };
            ready = WaitReady(window);
            timings["trajectory"] = "Orbit control: yaw delta of 15*sin(t*.75), pitch delta of 8*sin(t*.53) degrees, product time smoothing enabled; legacy rotates model about its center. Product experience comparison, not isolated renderer multiplier.";
        }
        window.Width = 1280;
        window.Height = 820;
        window.Left = 40;
        window.Top = 40;
        window.ShowActivated = false;
        window.Title = $"HeightMap validation | {mode} | {Path.GetFileName(source)} | {target}";
        window.Show();
        await ready.WaitAsync(TimeSpan.FromSeconds(120));
        if (options.GetValueOrDefault("verify-reload", "false") == "true")
        {
            if (mode != "current") throw new ArgumentException("--verify-reload requires --mode current");
            await VerifyReload(window, output);
            window.Close();
            return;
        }
        timings["windowReadyMs"] = openWatch.Elapsed.TotalMilliseconds;
        await Task.Delay(1800);
        Capture(window, Path.Combine(output, "initial.png"));
        if (window is Legacy.Window3D old)
        {
            foreach (var timing in old.Timings) timings[timing.Key] = timing.Value;
            timings["vertices"] = old.VertexCount;
            timings["triangles"] = old.TriangleCount;
            timings["sampleWidth"] = old.SampleWidth;
            timings["sampleHeight"] = old.SampleHeight;
            timings["meshArraysWorkerMs"] = Legacy.Window3D.LastBuildArraysMilliseconds;
            timings["meshCollectionsWorkerMs"] = Legacy.Window3D.LastBuildCollectionsMilliseconds;
            old.ResetValidationView();
            await Task.Delay(500);
            Capture(window, Path.Combine(output, "reset.png"));
            timings["hoverHitTestMs"] = Describe(old.MeasureHitTests());
        }
        else
        {
            timings["sampleMs"] = Property(window, "SamplingMilliseconds");
            timings["buildMetrics"] = Property(window, "BuildMetrics");
            object renderer = Field(window, "renderer")!;
            var viewport = (FrameworkElement)Property(renderer, "Viewport")!;
            timings["viewportLogicalWidth"] = viewport.ActualWidth;
            timings["viewportLogicalHeight"] = viewport.ActualHeight;
            var device = (SharpDX.Direct3D11.Device)Property(Field(renderer, "effectsManager")!, "Device")!;
            using (var dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device>())
            using (var adapter = dxgiDevice.Adapter)
                timings["actualDxAdapter"] = adapter.Description.Description;
            var hitSamples = new List<double>();
            int hits = 0;
            var tryHit = renderer.GetType().GetMethod("TryHit")!;
            for (int i = 0; i < 50; i++)
            {
                object?[] arguments = [new Point(viewport.ActualWidth * (.3 + (i % 5) * .1), viewport.ActualHeight * (.3 + (i / 5 % 5) * .1)), null];
                long start = Stopwatch.GetTimestamp();
                bool hit = (bool)tryHit.Invoke(renderer, arguments)!;
                hitSamples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                if (hit) hits++;
            }
            timings["hoverHitTestMs"] = Describe(hitSamples);
            timings["hoverHitCount"] = hits;
            CaptureCurrent(window, Path.Combine(output, "gpu-initial.png"));
            await Task.Delay(500);
        }
        var intervals = new List<double>();
        var callbackCosts = new List<double>();
        var process = Process.GetCurrentProcess();
        TimeSpan cpuStart = process.TotalProcessorTime;
        var runWatch = Stopwatch.StartNew();
        double previous = 0;
        TimeSpan lastRender = TimeSpan.MinValue;
        EventHandler render = (_, e) =>
        {
            if (e is RenderingEventArgs rendering && rendering.RenderingTime == lastRender) return;
            if (e is RenderingEventArgs renderArgs) lastRender = renderArgs.RenderingTime;
            double now = runWatch.Elapsed.TotalMilliseconds;
            if (previous > 0) intervals.Add(now - previous);
            previous = now;
            long started = Stopwatch.GetTimestamp();
            animate(now / 1000);
            callbackCosts.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        };
        CompositionTarget.Rendering += render;
        await Task.Delay(TimeSpan.FromSeconds(duration));
        CompositionTarget.Rendering -= render;
        double elapsedMs = runWatch.Elapsed.TotalMilliseconds;
        double cpuMs = (process.TotalProcessorTime - cpuStart).TotalMilliseconds;
        process.Refresh();
        timings["interactiveWallMs"] = elapsedMs;
        timings["interactiveCpuMs"] = cpuMs;
        timings["cpuOneCorePercent"] = cpuMs / elapsedMs * 100;
        timings["cpuMachinePercent"] = cpuMs / elapsedMs * 100 / Environment.ProcessorCount;
        timings["privateBytes"] = process.PrivateMemorySize64;
        timings["workingSetBytes"] = process.WorkingSet64;
        timings["compositionIntervalsMs"] = Describe(intervals);
        timings["animationCallbackCpuWallMs"] = Describe(callbackCosts);
        timings["rawCompositionIntervalsMs"] = intervals;
        if (mode != "legacy")
        {
            var renderer = Field(window, "renderer")!;
            var host = Property(Property(renderer, "Viewport")!, "RenderHost");
            var stats = host == null ? null : Property(host, "RenderStatistics");
            if (stats != null)
            {
                timings["helixRenderLatencyRollingMeanMs"] = Property(Property(stats, "LatencyStatistics")!, "AverageValue");
                timings["helixRenderIntervalRollingMeanMs"] = Property(Property(stats, "FPSStatistics")!, "AverageValue");
                timings["helixRenderLatencyDefinition"] = "Helix v3.1.2 DX11RenderHostBase.UpdateAndRender Stopwatch from per-frame update through PreRender, OnRender, EndDraw, 2D, Present, PostRender. CPU wall time may contain synchronization; not isolated GPU timestamps or input-to-display latency.";
            }
        }
        Capture(window, Path.Combine(output, "rotated.png"));
        if (mode != "legacy") CaptureCurrent(window, Path.Combine(output, "gpu-rotated.png"));
        if (mode != "legacy" && options.GetValueOrDefault("export", "false") == "true")
        {
            object renderer = Field(window, "renderer")!;
            var exportWatch = Stopwatch.StartNew();
            string obj = Path.Combine(output, "heightmap.obj");
            var geometry = Property(renderer, "ExportGeometry")!;
            var exporter = typeof(ColorVision.ImageEditor.Window3D).Assembly.GetType("ColorVision.ImageEditor.EditorTools.ThreeD.HeightMapModelExporter")!;
            var exportMethod = exporter.GetMethod("ExportAsync")!;
            await (Task)exportMethod.Invoke(null, [geometry, Property(renderer, "ExportLut"), (double)Field(window, "heightScale")!, obj, true, CancellationToken.None])!;
            timings["exportObjMs"] = exportWatch.Elapsed.TotalMilliseconds;
            timings["exportObjBytes"] = new FileInfo(obj).Length;
            timings["exportVertices"] = ((Array)Property(geometry, "Positions")!).Length;
            timings["exportTriangles"] = ((Array)Property(geometry, "Indices")!).Length / 3;
        }
        File.WriteAllText(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(timings, JsonOptions));
        if (options.TryGetValue("hold", out string? hold)) await Task.Delay(TimeSpan.FromSeconds(int.Parse(hold)));
        window.Close();
    }

    private static object? Property(object instance, string name) => instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
    private static object? Field(object instance, string name) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
    private static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(instance, args);
    private static async Task VerifyReload(Window window, string output)
    {
        object renderer = Field(window, "renderer")!;
        object State()
        {
            object active = Property(renderer, "ActiveSample")!, detail = Property(renderer, "Sample")!;
            return new
            {
                Ready = Property(window, "IsReady"),
                WindowInteractionFlag = Field(window, "usingInteractionMesh"),
                RendererInteractionFlag = Field(renderer, "isInteracting"),
                ActiveWidth = Property(active, "Width"), ActiveHeight = Property(active, "Height"),
                DetailWidth = Property(detail, "Width"), DetailHeight = Property(detail, "Height"),
                ActiveUsesDetailGrayBuffer = ReferenceEquals(Property(active, "Gray"), Property(detail, "Gray")),
                DetailModelVisible = Property(Field(renderer, "detailModel")!, "IsRendering"),
                InteractionModelVisible = Property(Field(renderer, "interactionModel")!, "IsRendering")
            };
        }
        Invoke(window, "SetInteractionMesh", true);
        object before = State();
        bool enteredInteraction = Equals(Property(before, "WindowInteractionFlag"), true)
            && Equals(Property(before, "RendererInteractionFlag"), true)
            && Equals(Property(before, "ActiveUsesDetailGrayBuffer"), false)
            && Equals(Property(before, "InteractionModelVisible"), true);
        if (!enteredInteraction)
            throw new InvalidOperationException("Reload validation needs distinct detail/interaction meshes; choose --target 1536 --interaction 512.");
        var watch = Stopwatch.StartNew();
        await ((Task)Invoke(window, "ReloadAsync")!).WaitAsync(TimeSpan.FromSeconds(30));
        object after = State();
        bool passed = Equals(Property(after, "Ready"), true)
            && Equals(Property(after, "WindowInteractionFlag"), false)
            && Equals(Property(after, "RendererInteractionFlag"), false)
            && Equals(Property(after, "ActiveUsesDetailGrayBuffer"), true)
            && Equals(Property(after, "DetailModelVisible"), true)
            && Equals(Property(after, "InteractionModelVisible"), false);
        File.WriteAllText(Path.Combine(output, "reload-contract.json"), JsonSerializer.Serialize(new
        {
            VerifiedAtUtc = DateTimeOffset.UtcNow,
            Contract = "ReloadAsync must restore the full detail renderer and clear the window interaction flag together.",
            EnteredInteraction = enteredInteraction, BeforeReload = before, AfterReload = after,
            ReloadMilliseconds = watch.Elapsed.TotalMilliseconds, Passed = passed
        }, JsonOptions));
        if (!passed) throw new InvalidOperationException("Reload returned with inconsistent detail/interaction state; see reload-contract.json.");
    }
    private static async Task WaitReady(Window window)
    {
        var watch = Stopwatch.StartNew();
        while (!Equals(Property(window, "IsReady"), true))
        {
            if (watch.Elapsed.TotalSeconds > 120) throw new TimeoutException("Current Window3D did not become ready");
            await Task.Delay(50);
        }
    }
    private static void CaptureCurrent(Window window, string file)
    {
        object renderer = Field(window, "renderer")!;
        var viewport = (FrameworkElement)Property(renderer, "Viewport")!;
        var bitmap = (BitmapSource?)Invoke(renderer, "CaptureBitmap", (int)viewport.ActualWidth, (int)viewport.ActualHeight);
        if (bitmap == null) return;
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    private static object Describe(List<double> samples)
    {
        var ordered = samples.Order().ToArray();
        double P(double q) => ordered.Length == 0 ? 0 : ordered[(int)Math.Min(ordered.Length - 1, Math.Round(q * (ordered.Length - 1)))];
        return new { count = ordered.Length, mean = ordered.Length == 0 ? 0 : ordered.Average(), p50 = P(.5), p95 = P(.95), p99 = P(.99), max = P(1), over33ms = ordered.Count(x => x > 33.333), over50ms = ordered.Count(x => x > 50) };
    }

    private static void Capture(Window window, string file)
    {
        var visual = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    private static WriteableBitmap LoadBitmap(string source)
    {
        if (source.StartsWith("synthetic-", StringComparison.Ordinal))
        {
            int width = 4096, height = source.Contains("portrait") ? 5460 : 2730;
            byte[] pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double dx = (x - width / 2d) / (width * .19), dy = (y - height / 2d) / (height * .19);
                    double value = source.Contains("detail") ? 128 + 72 * Math.Sin(x * .14) * Math.Cos(y * .13) + 40 * Math.Sin(x * .026) : 235 * Math.Exp(-.5 * (dx * dx + dy * dy));
                    pixels[y * width + x] = (byte)Math.Clamp(Math.Round(value), 0, 255);
                }
            return new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width));
        }
        if (Path.GetExtension(source).Equals(".cvraw", StringComparison.OrdinalIgnoreCase))
        {
            int offset = CVFileUtil.ReadCIEFileHeader(source, out CVCIEFile file);
            try
            {
                if (offset <= 0 || !CVFileUtil.ReadCIEFileData(source, ref file, offset)) throw new InvalidDataException("Invalid CVRAW file");
                PixelFormat format = (file.Channels, file.Bpp) switch
                {
                    (1, 8) => PixelFormats.Gray8,
                    (1, 16) => PixelFormats.Gray16,
                    (3, 8) => PixelFormats.Bgr24,
                    (3, 16) => PixelFormats.Rgb48,
                    _ => throw new NotSupportedException($"Validation CVRAW decoder: {file.Channels} channels, {file.Bpp} bits")
                };
                if (file.Channels == 3 && file.Bpp == 16)
                    for (int i = 0; i < file.Data.Length; i += 6)
                    {
                        (file.Data[i], file.Data[i + 4]) = (file.Data[i + 4], file.Data[i]);
                        (file.Data[i + 1], file.Data[i + 5]) = (file.Data[i + 5], file.Data[i + 1]);
                    }
                return new WriteableBitmap(BitmapSource.Create(file.Cols, file.Rows, 96, 96, format, null, file.Data, file.Cols * file.Channels * (file.Bpp / 8)));
            }
            finally { file.Dispose(); }
        }
        using var stream = File.OpenRead(source);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames[0];
        return frame.Format == PixelFormats.Gray32Float
            ? ColorVision.ImageEditor.Tif.Opentif.ConvertGray32FloatToBitmapSource(frame)
            : new WriteableBitmap(frame);
    }

    private sealed class MemoryConfig : IConfigService
    {
        private readonly Dictionary<Type, IConfig> instances = new();
        public IConfig GetRequiredService(Type type) => instances.TryGetValue(type, out var value) ? value : instances[type] = (IConfig)Activator.CreateInstance(type)!;
        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
