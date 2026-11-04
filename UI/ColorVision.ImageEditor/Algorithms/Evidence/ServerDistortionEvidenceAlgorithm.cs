using ColorVision.Algorithms;
using ColorVision.Core;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

/// <summary>Runs the installed service provider locally, without service, DAO or result-file writes.</summary>
public static class ServerDistortionEvidenceAlgorithm
{
    public const string Id = "distortion.server-legacy";
    private static readonly Dictionary<string, NativeRuntime> Runtimes = new(StringComparer.OrdinalIgnoreCase);

    public static ImageEvidenceAlgorithm Create(string libraryPath, string parameterJson, string parameterSource) =>
        new(Id, "畸变 · 服务端旧版", image => Evaluate(image, libraryPath, parameterJson, parameterSource));

    private static AlgorithmResult Evaluate(HImage image, string libraryPath, string parameterJson, string parameterSource)
    {
        JsonObject parameters = PrepareParameters(parameterJson);
        string path = string.IsNullOrWhiteSpace(libraryPath) ? string.Empty : Path.GetFullPath(libraryPath);
        object? library = null;
        object? nativeInput = null;
        object Provenance() => new
        {
            LibraryPath = path, Library = library, EntryPoint = "CV_Ali_calcDistortionSocket",
            OpenCvDependency = DistortionEvidenceArtifacts.LoadedLibrary("opencv_world401.dll"),
            ParameterSource = parameterSource, Parameters = parameters,
            SourceInput = new { image.cols, image.rows, image.depth, image.channels, image.stride },
            NativeInput = nativeInput,
            // Keep the provider's formula and scaling switches; do not silently normalize its values.
            TvCalcWay = parameters["caclDistorType"]?["TvCaclWay"]?.DeepClone(),
            PercentageDisplay = parameters["CommonParams"]?["PercentageDisplay"]?.DeepClone()
        };
        try
        {
            if (path.Length == 0) throw new FileNotFoundException("未找到服务端畸变 DLL，请选择 CV_algorithm.dll 及其配套依赖所在目录。");
            NativeRuntime runtime;
            lock (Runtimes)
            {
                if (!Runtimes.TryGetValue(path, out runtime!)) Runtimes.Add(path, runtime = new(path));
            }
            library = runtime.Library;
            using Mat pixels = CopyInput(image);
            nativeInput = new
            {
                Width = pixels.Width, Height = pixels.Height, Depth = image.depth,
                Channels = pixels.Channels(), Stride = pixels.Step(), ColorOrder = pixels.Channels() == 1 ? "Gray" : "BGR"
            };
            NativeReply reply = runtime.Run(pixels, image.depth, parameters.ToJsonString());
            return ConvertReply(reply.Code, reply.Json, image.cols, image.rows, parameters, Provenance());
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException
            or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or NotSupportedException or SEHException)
        {
            return new AlgorithmResult
            {
                AlgorithmId = new(Id), Status = AlgorithmResultStatus.Failed,
                Artifacts = [new AlgorithmStructuredDataArtifact("parameters", "ColorVision.ServerDistortionEvidence.v1", JsonSerializer.SerializeToElement(Provenance()))],
                Failures = [new("NativeExecutionFailed", ex.Message)]
            };
        }
    }

    internal static JsonObject PrepareParameters(string parameterJson)
    {
        JsonObject parameters = JsonNode.Parse(parameterJson) as JsonObject ?? throw new InvalidDataException("畸变参数必须是 JSON 对象。");
        if (parameters["debugCfg"] is not JsonObject debug) parameters["debugCfg"] = debug = new JsonObject();
        debug["Debug"] = false;
        return parameters;
    }

    internal static Mat CopyInput(HImage image)
    {
        if (image.pData == IntPtr.Zero || image.rows <= 0 || image.cols <= 0) throw new ArgumentException("畸变输入图像为空。");
        MatType type = (image.depth, image.channels) switch
        {
            (8, 1) => MatType.CV_8UC1, (8, 3) => MatType.CV_8UC3, (8, 4) => MatType.CV_8UC4,
            (16, 1) => MatType.CV_16UC1, (16, 3) => MatType.CV_16UC3, (16, 4) => MatType.CV_16UC4,
            _ => throw new NotSupportedException("服务端畸变仅接受 8/16 位 Gray、BGR 图像；不自动缩放浮点像素。")
        };
        int rowBytes = checked(image.cols * image.channels * (image.depth / 8));
        int stride = image.stride == 0 ? rowBytes : image.stride;
        if (stride < rowBytes) throw new ArgumentException("畸变输入图像行跨度无效。");
        using Mat source = Mat.FromPixelData(image.rows, image.cols, type, image.pData, stride);
        // The old ABI has no stride or const qualifier; never pass the shared evidence frame.
        if (image.channels != 4) return source.Clone();
        Mat bgr = new();
        try { Cv2.CvtColor(source, bgr, ColorConversionCodes.BGRA2BGR); return bgr; }
        catch { bgr.Dispose(); throw; }
    }

    internal static AlgorithmResult ConvertReply(int code, string json, int width, int height, JsonObject parameters, object provenance)
    {
        List<AlgorithmArtifact> artifacts =
        [
            new AlgorithmStructuredDataArtifact("parameters", "ColorVision.ServerDistortionEvidence.v1", JsonSerializer.SerializeToElement(provenance)),
            new AlgorithmStructuredDataArtifact("diagnostics", "ColorVision.ServerDistortionReply.v1", JsonSerializer.SerializeToElement(new { NativeReturnCode = code, RawJson = json }))
        ];
        string? error = code == 1 ? null : code == 2
            ? "服务端畸变仅部分成功（返回码 2）；完整原始分项保留在诊断中。"
            : $"服务端畸变调用失败，返回码 {code}。";
        if (error == null)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                List<AlgorithmArtifact> accepted = [];
                List<AlgorithmMeasurement> measurements = [];
                bool? percentage = parameters["CommonParams"]?["PercentageDisplay"]?.GetValue<bool>();
                string? unit = percentage == true ? "%" : percentage == false ? "ratio" : null;
                int groups = 0;
                ReadGroup("TV_distortion", "DistortionTV", "TV", [("horizontalRatio", "水平 TV（服务端口径）"), ("verticalRatio", "垂直 TV（服务端口径）")]);
                ReadGroup("Point9_distortion", "Distortion9Point", "P9", [("topRatio", "九点上畸变（服务端口径）"), ("bottomRatio", "九点下畸变（服务端口径）"),
                    ("leftRatio", "九点左畸变（服务端口径）"), ("rightRatio", "九点右畸变（服务端口径）"), ("keyStoneHoriRatio", "水平梯形（服务端口径）"), ("keyStoneVercRatio", "垂直梯形（服务端口径）")]);
                ReadGroup("Optic_Distortion", "DistortionOptic", "Optic", [("opticRatio", "光学畸变（服务端口径）")]);
                if (groups == 0) throw new JsonException("服务端未返回任何有效畸变分项。");
                accepted.Add(new AlgorithmMeasurementArtifact("distortion", measurements));
                artifacts.AddRange(accepted);

                void ReadGroup(string key, string enabledKey, string group, (string Key, string Name)[] fields)
                {
                    bool? requested = parameters["caclDistorType"]?[enabledKey]?.GetValue<bool>();
                    bool present = root.TryGetProperty(key, out JsonElement section) && section.ValueKind != JsonValueKind.Null;
                    if (requested == false) return;
                    if (!present)
                    {
                        if (requested == true) throw new JsonException($"服务端缺少请求的 {key} 分项。");
                        return;
                    }
                    if (section.TryGetProperty("message", out JsonElement message) && message.GetString() is string text
                        && !string.Equals(text, "Success", StringComparison.OrdinalIgnoreCase))
                        throw new JsonException($"{key}：{text}");
                    var points = section.GetProperty("finalPoints").EnumerateArray().Select(point => new
                    {
                        Id = point.GetProperty("id").GetInt32(),
                        X = point.GetProperty("x").GetDouble(), Y = point.GetProperty("y").GetDouble()
                    }).OrderBy(point => point.Id).ToArray();
                    if (points.Length < 3 || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)
                        || point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)
                        || points.Select(point => point.Id).Distinct().Count() != points.Length
                        || points.Select(point => (point.X, point.Y)).Distinct().Count() != points.Length)
                        throw new JsonException($"{key} 点位重复、缺失或超出原图范围。");
                    foreach ((string metricKey, string name) in fields)
                    {
                        double value = section.GetProperty(metricKey).GetDouble();
                        if (!double.IsFinite(value)) throw new JsonException($"{key}.{metricKey} 不是有限值。");
                        measurements.Add(new(name, value, unit));
                    }
                    // Keep independent groups: the provider may use different final points per metric.
                    // Only markers are emitted; arbitrary provider IDs do not establish grid adjacency.
                    DistortionEvidenceArtifacts.AddPoints(accepted, Id, group, points.Select(point => new AlgorithmPoint(point.X, point.Y)).ToArray(), "#FFFFC107");
                    groups++;
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
            {
                error = "服务端畸变结果无效：" + ex.Message;
            }
        }
        return new AlgorithmResult
        {
            AlgorithmId = new(Id), Status = error == null ? AlgorithmResultStatus.Succeeded : AlgorithmResultStatus.Failed,
            Artifacts = artifacts, Failures = error == null ? [] : [new(code == 1 ? "InvalidDistortionResult" : code == 2 ? "NativePartialSuccess" : "NativeCallFailed", error)]
        };
    }

    private sealed record NativeReply(int Code, string Json);

    private sealed class NativeRuntime
    {
        private readonly IntPtr _module;
        private readonly CreateHandle _create;
        private readonly ReleaseHandle _release;
        private readonly Calculate _calculate;
        private readonly object _gate = new();
        internal object Library { get; }

        internal NativeRuntime(string path)
        {
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("服务端畸变 DLL 需要 x64 进程。");
            Library = DistortionEvidenceArtifacts.LibraryFile(path);
            _module = NativeLibrary.Load(path, typeof(ServerDistortionEvidenceAlgorithm).Assembly,
                DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
            try
            {
                _create = Load<CreateHandle>("CV_Ali_creatHandle");
                _release = Load<ReleaseHandle>("CV_Ali_releaseHandle");
                _calculate = Load<Calculate>("CV_Ali_calcDistortionSocket");
                Load<Initialize>("CV_Ali_initial")();
            }
            catch { NativeLibrary.Free(_module); throw; }
            // Keep validated provider modules loaded until process exit, matching the AA adapter.
        }

        private T Load<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));

        internal NativeReply Run(Mat pixels, int depth, string parameters)
        {
            lock (_gate)
            {
                IntPtr handle = _create();
                if (handle == IntPtr.Zero) throw new InvalidOperationException("无法创建服务端畸变算法句柄。");
                try
                {
                    byte[] output = new byte[4 * 1024 * 1024];
                    int length = output.Length, showDepth = 0, showChannels = 0;
                    int code = _calculate(handle, pixels.Width, pixels.Height, depth, pixels.Channels(), pixels.Data,
                        parameters, output, ref length, ref showDepth, ref showChannels, IntPtr.Zero);
                    // The installed DLL leaves resultLength at capacity on success. Its JSON is NUL-terminated.
                    int end = Array.IndexOf(output, (byte)0);
                    if (end < 0) throw new InvalidDataException("服务端畸变返回未终止的 JSON；结果缓冲区可能不足。");
                    return new(code, Encoding.UTF8.GetString(output, 0, end));
                }
                finally { _release(handle); }
            }
        }

        // Source ABI: AlgorithmPlugin/Features/ARVR_V2/CV_Algorithm.h and API/CV_Algorithm_Proxy.cs.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void Initialize();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr CreateHandle();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseHandle(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int Calculate(IntPtr handle, int width, int height, int depth, int channels, IntPtr data,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters, [Out] byte[] output, ref int length,
            ref int showDepth, ref int showChannels, IntPtr showData);
    }
}
