using ColorVision.Algorithms;
using ColorVision.Core;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.ImageEditor.Algorithms.Evidence;

/// <summary>Calls the service's AA DLL locally, without its MQTT, database or POI side effects.</summary>
public static class ServerAaLuminousEvidenceAlgorithm
{
    public const string Id = "luminous.server-aa";
    private static readonly Dictionary<string, NativeRuntime> Runtimes = new(StringComparer.OrdinalIgnoreCase);

    public static ImageEvidenceAlgorithm Create(string libraryPath, string parameterJson, string parameterSource) =>
        new(Id, "发光区 · 服务端 AA", image => Evaluate(image, libraryPath, parameterJson, parameterSource));

    private static AlgorithmResult Evaluate(HImage image, string libraryPath, string parameterJson, string parameterSource)
    {
        JsonObject parameters = JsonNode.Parse(parameterJson) as JsonObject
            ?? throw new InvalidDataException("AA 参数必须是 JSON 对象。");
        // Evidence runs never request the provider's debug-image file output.
        if (parameters["debugCfg"] is not JsonObject debug) parameters["debugCfg"] = debug = new JsonObject();
        debug["Debug"] = false;
        string effectiveJson = parameters.ToJsonString();
        if (string.IsNullOrWhiteSpace(libraryPath)) throw new FileNotFoundException("未找到服务端 AA DLL，请选择 CV_algorithm.dll 及其配套依赖所在目录。");
        string path = Path.GetFullPath(libraryPath);
        NativeRuntime runtime;
        lock (Runtimes)
        {
            if (!Runtimes.TryGetValue(path, out runtime!)) Runtimes.Add(path, runtime = new NativeRuntime(path));
        }
        using Mat pixels = CopyInput(image);
        NativeReply reply = runtime.Find(pixels, image.depth, effectiveJson);
        return ConvertReply(reply.Code, reply.Json, pixels.Width, pixels.Height, new
        {
            LibraryPath = path, LibrarySha256 = runtime.Sha256, EntryPoint = "CV_Ali_findPointsSocket",
            ParameterSource = parameterSource, Parameters = parameters,
            Input = new { image.depth, Channels = pixels.Channels(), ColorOrder = pixels.Channels() == 1 ? "Gray" : "BGR" }
        });
    }

    internal static Mat CopyInput(HImage image)
    {
        if (image.pData == IntPtr.Zero || image.rows <= 0 || image.cols <= 0)
            throw new ArgumentException("AA 输入图像为空。");
        MatType type = (image.depth, image.channels) switch
        {
            (8, 1) => MatType.CV_8UC1, (8, 3) => MatType.CV_8UC3, (8, 4) => MatType.CV_8UC4,
            (16, 1) => MatType.CV_16UC1, (16, 3) => MatType.CV_16UC3, (16, 4) => MatType.CV_16UC4,
            _ => throw new NotSupportedException("服务端 AA 仅接受 8/16 位 Gray、BGR 图像；不自动缩放浮点像素。")
        };
        int rowBytes = checked(image.cols * image.channels * (image.depth / 8));
        int stride = image.stride == 0 ? rowBytes : image.stride;
        if (stride < rowBytes) throw new ArgumentException("AA 输入图像行跨度无效。");
        using Mat source = Mat.FromPixelData(image.rows, image.cols, type, image.pData, stride);
        // The old ABI has no stride or const qualifier. Give it private, tightly packed pixels.
        if (image.channels != 4) return source.Clone();
        Mat bgr = new();
        try { Cv2.CvtColor(source, bgr, ColorConversionCodes.BGRA2BGR); return bgr; }
        catch { bgr.Dispose(); throw; }
    }

    internal static AlgorithmResult ConvertReply(int code, string json, int width, int height, object provenance)
    {
        List<AlgorithmArtifact> artifacts =
        [
            new AlgorithmStructuredDataArtifact("parameters", "ColorVision.ServerAaEvidence.v1", JsonSerializer.SerializeToElement(provenance)),
            new AlgorithmStructuredDataArtifact("diagnostics", "ColorVision.ServerAaReply.v1", JsonSerializer.SerializeToElement(new { NativeReturnCode = code, RawJson = json }))
        ];
        string? error = code == 1 ? null : $"服务端 AA 调用失败，返回码 {code}。";
        LuminousAreaPoint[] corners = [];
        if (error == null)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                corners = document.RootElement.GetProperty("corner").EnumerateArray()
                    .Select(point => new LuminousAreaPoint(point.GetProperty("x").GetDouble(), point.GetProperty("y").GetDouble())).ToArray();
                if (!LuminousAreaResultParser.TryValidateOrderedCorners(corners, out string geometryError)) error = geometryError;
                else if (corners.Any(point => point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)) error = "服务端 AA 四角超出原图范围。";
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                error = "服务端 AA 四角结果无效：" + ex.Message;
            }
        }
        if (error == null)
        {
            artifacts.Add(new AlgorithmGeometryArtifact("boundary", AlgorithmCoordinateSpace.Pixel,
                [new AlgorithmGeometry("boundary", AlgorithmGeometryKind.Polygon, corners.Select(point => new AlgorithmPoint(point.X, point.Y)).ToArray())]));
            artifacts.Add(new AlgorithmOverlayArtifact("evidence." + Id, AlgorithmOverlayLifetime.Transient,
                [new AlgorithmOverlayItem("boundary", new AlgorithmOverlayStyle("#FFFFC107", null, 2))]));
        }
        return new AlgorithmResult
        {
            AlgorithmId = new AlgorithmId(Id), Status = error == null ? AlgorithmResultStatus.Succeeded : AlgorithmResultStatus.Failed,
            Artifacts = artifacts, Failures = error == null ? [] : [new AlgorithmFailure(code == 1 ? "InvalidCorners" : "NativeCallFailed", error)]
        };
    }

    private sealed record NativeReply(int Code, string Json);

    private sealed class NativeRuntime
    {
        private readonly IntPtr _module;
        private readonly CreateHandle _create;
        private readonly ReleaseHandle _release;
        private readonly FindPoints _find;
        private readonly object _gate = new();
        public string Sha256 { get; }

        public NativeRuntime(string path)
        {
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("服务端 AA DLL 需要 x64 进程。");
            using (FileStream file = File.OpenRead(path)) Sha256 = Convert.ToHexString(SHA256.HashData(file));
            // Resolve opencv_world401.dll beside this DLL; never change the process DLL search path.
            _module = NativeLibrary.Load(path, typeof(ServerAaLuminousEvidenceAlgorithm).Assembly,
                DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
            try
            {
                _create = Load<CreateHandle>("CV_Ali_creatHandle");
                _release = Load<ReleaseHandle>("CV_Ali_releaseHandle");
                _find = Load<FindPoints>("CV_Ali_findPointsSocket");
                Load<Initialize>("CV_Ali_initial")();
            }
            catch { NativeLibrary.Free(_module); throw; }
            // Validated modules remain loaded until process exit, as the provider owns global state.
        }

        private T Load<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));

        public NativeReply Find(Mat pixels, int depth, string parameterJson)
        {
            lock (_gate)
            {
                IntPtr handle = _create();
                if (handle == IntPtr.Zero) throw new InvalidOperationException("无法创建服务端 AA 算法句柄。");
                try
                {
                    // Bound output memory. The service's AliResult.SUCCESS is 1, ERR_LENGTH is -1.
                    byte[] output = new byte[1024 * 1024];
                    int capacity = output.Length, showDepth = 0, showChannels = 0;
                    int code = _find(handle, pixels.Width, pixels.Height, depth, pixels.Channels(), pixels.Data,
                        parameterJson, output, ref capacity, ref showDepth, ref showChannels, IntPtr.Zero);
                    int end = Array.IndexOf(output, (byte)0);
                    return new NativeReply(code, Encoding.UTF8.GetString(output, 0, end < 0 ? output.Length : end));
                }
                finally { _release(handle); }
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void Initialize();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr CreateHandle();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReleaseHandle(IntPtr handle);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate int FindPoints(IntPtr handle, int width, int height, int depth, int channels, IntPtr data,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters, [Out] byte[] output, ref int length,
            ref int showDepth, ref int showChannels, IntPtr showData);
    }
}
