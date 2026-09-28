using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Mtf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ColorVision.UI.Tests;

/// <summary>Test-only reference for the provider MTF 2.0 ABI. Production uses StripeMtfAnalyzer.</summary>
internal static class VendorMtfReference
{
    private static readonly object Sync = new();
    private static Runtime? runtime;

    internal static void ValidateRegions(IReadOnlyList<MtfRoi> regions, int width, int height)
    {
        if (width <= 0 || height <= 0 || regions.Count == 0) throw new InvalidOperationException("MTF 缺少图像尺寸或矩形关注点。");
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (MtfRoi region in regions)
        {
            if (string.IsNullOrWhiteSpace(region.name) || !names.Add(region.name))
                throw new InvalidOperationException("MTF 关注点名称必须非空且唯一，以保持横竖结果对应关系。");
            if (region.x < 0 || region.y < 0 || region.w <= 0 || region.h <= 0
                || (long)region.x + region.w > width || (long)region.y + region.h > height)
                throw new InvalidOperationException($"MTF 关注点超出图像范围或尺寸无效：{region.name}。");
        }
    }

    internal static JObject ParseParameters(string json)
    {
        JObject parameters = JObject.Parse(json);
        if (parameters["pattern"]?.Type != JTokenType.Integer || parameters["CalcMethod"]?.Type != JTokenType.Integer
            || parameters.Value<int>("CalcMethod") is not (0 or 1))
            throw new InvalidOperationException("MTF 模板必须包含 pattern 和 CalcMethod（0 或 1）。");
        if (parameters.Value<int>("pattern") == 5)
        {
            JObject options = parameters["nV1"] as JObject ?? throw new InvalidOperationException("四部图案缺少 nV1 参数。");
            foreach (string key in new[] { "AAminSize", "rectWidth", "rectHeight", "distanceToRect" })
                if (options[key]?.Type != JTokenType.Integer || options.Value<int>(key) <= 0)
                    throw new InvalidOperationException($"MTF 参数 nV1.{key} 必须是正整数。");
            if (options["firstIsHor"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("MTF 四部图案必须指定 firstIsHor。");
        }
        return parameters;
    }

    public static JObject Calculate(HImage image, string algorithmDirectory, JObject parameters, IReadOnlyList<MtfRoi> regions)
    {
        ValidateRegions(regions, image.cols, image.rows);
        if (image.pData == IntPtr.Zero || image.depth is not (8 or 16) || image.channels is not (1 or 3)
            || image.stride != checked(image.cols * image.channels * (image.depth / 8)))
            throw new NotSupportedException("本地 MTF 2.0 需要连续的 8/16 位、1/3 通道 RAW 图像。");
        parameters = ParseParameters(parameters.ToString(Formatting.None));
        string directory = string.IsNullOrWhiteSpace(algorithmDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Algorithms", "MTF") : Path.GetFullPath(algorithmDirectory.Trim());
        string library = Path.GetFullPath(Path.Combine(directory, "CV_algorithm.dll"));
        lock (Sync)
        {
            if (runtime != null && !string.Equals(runtime.LibraryPath, library, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("本进程已加载其他目录的 MTF 算法；修改算法目录后请重新启动 ColorVision。");
            runtime ??= new Runtime(library);
            IntPtr handle = runtime.CreateHandle();
            if (handle == IntPtr.Zero) throw new InvalidOperationException("MTF 原生算法未能创建句柄。");
            try
            {
                // The provider leaves resultLength unchanged on success. Read the terminating NUL instead.
                byte[] output = new byte[Math.Max(1024 * 1024, checked(regions.Count * 16384))];
                int length = output.Length, showBpp = 0, showChannels = 0;
                JsonSerializerSettings settings = new() { StringEscapeHandling = StringEscapeHandling.EscapeNonAscii };
                int code = runtime.Calculate(handle, image.cols, image.rows, image.depth, image.channels, image.pData,
                    JsonConvert.SerializeObject(parameters, settings), JsonConvert.SerializeObject(new { RoiRects = regions }, settings),
                    output, ref length, ref showBpp, ref showChannels, IntPtr.Zero);
                if (code != 1) throw new InvalidOperationException($"MTF 原生算法失败：{code}（1=成功，0=失败，2=部分成功，-1=缓冲区不足，-2=文件错误，-3=JSON 错误）。");
                int end = Array.IndexOf(output, (byte)0);
                if (end <= 0) throw new InvalidOperationException("MTF 原生结果为空或缺少结束标记。");
                JObject result = JObject.Parse(new UTF8Encoding(false, true).GetString(output, 0, end));
                ValidateResult(result, parameters.Value<int>("pattern"), regions, image.cols, image.rows);
                return result;
            }
            finally { runtime.ReleaseHandle(handle); }
        }
    }

    internal static void ValidateResult(JObject result, int pattern, IReadOnlyList<MtfRoi> regions, int width, int height)
    {
        JArray rectangles = result["result"] as JArray ?? throw new InvalidOperationException("MTF 结果缺少 result。");
        int perRegion = pattern == 5 ? 4 : 1;
        if (rectangles.Count != checked(regions.Count * perRegion)) throw new InvalidOperationException("MTF 返回的有效矩形数与关注点数不一致。");
        foreach (MtfRoi roi in regions)
        {
            JToken[] items = rectangles.Where(item => item.Value<string>("name") == roi.name).ToArray();
            if (items.Length != perRegion) throw new InvalidOperationException($"MTF 结果缺少关注点：{roi.name}。");
            foreach (JToken item in items)
            {
                ValidateValue(item, "mtfValue");
                ValidateRectangle(item, width, height);
            }
            if (pattern != 5) continue;
            if (!items.Select(item => item.Value<int?>("id")).Order().SequenceEqual(new int?[] { 0, 1, 2, 3 }))
                throw new InvalidOperationException($"MTF 四部矩形编号无效：{roi.name}。");
            JToken[] groups = (result["resultChild"] as JArray ?? new JArray()).Where(item => item.Value<string>("name") == roi.name).ToArray();
            if (groups.Length != 1 || groups[0]["childRects"] is not JArray children || children.Count != 4)
                throw new InvalidOperationException($"MTF 缺少完整横竖分组：{roi.name}。");
            foreach (string metric in new[] { "Average", "horizontalAverage", "verticalAverage" }) ValidateValue(groups[0], metric);
            foreach (JToken child in children)
            {
                ValidateValue(child, "mtfValue");
                ValidateRectangle(child, width, height);
                JToken? flat = items.SingleOrDefault(item => item.Value<int?>("id") == child.Value<int?>("id"));
                if (flat == null || new[] { "x", "y", "w", "h", "mtfValue" }.Any(key => !JToken.DeepEquals(flat[key], child[key])))
                    throw new InvalidOperationException($"MTF 分组与矩形结果不一致：{roi.name}。");
            }
            if (children.Select(item => item.Value<int?>("id")).Distinct().Count() != 4)
                throw new InvalidOperationException($"MTF 分组矩形编号重复：{roi.name}。");
        }
        if (pattern == 5 && (result["resultChild"] as JArray)?.Count != regions.Count)
            throw new InvalidOperationException("MTF 横竖分组数不一致。");
    }

    private static void ValidateValue(JToken item, string key)
    {
        JToken? value = item[key];
        if (value?.Type is not (JTokenType.Float or JTokenType.Integer) || !double.IsFinite(value.Value<double>()) || value.Value<double>() < 0)
            throw new InvalidOperationException($"MTF 结果 {key} 无效，不能以零代替失败。");
    }

    private static void ValidateRectangle(JToken item, int width, int height)
    {
        if (new[] { "x", "y", "w", "h" }.Any(key => item[key]?.Type != JTokenType.Integer))
            throw new InvalidOperationException("MTF 结果矩形坐标不完整。");
        long x = item.Value<long>("x"), y = item.Value<long>("y"), w = item.Value<long>("w"), h = item.Value<long>("h");
        if (x < 0 || y < 0 || w <= 0 || h <= 0 || x > width - w || y > height - h)
            throw new InvalidOperationException("MTF 结果矩形超出图像范围。");
    }

    // Keep the module alive for the process lifetime: delegates and provider-global state must not outlive it.
    private sealed class Runtime
    {
        public string LibraryPath { get; }
        public CreateHandleDelegate CreateHandle { get; }
        public ReleaseHandleDelegate ReleaseHandle { get; }
        public CalculateDelegate Calculate { get; }

        public Runtime(string library)
        {
            if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("本地 MTF 算法需要 x64 进程。");
            if (!File.Exists(library)) throw new FileNotFoundException("请选择包含 CV_algorithm.dll 和 opencv_world401.dll 的算法目录。", library);
            IntPtr module = NativeLibrary.Load(library, typeof(VendorMtfReference).Assembly,
                DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
            try
            {
                T Load<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module, name));
                CreateHandle = Load<CreateHandleDelegate>("CV_Ali_creatHandle");
                ReleaseHandle = Load<ReleaseHandleDelegate>("CV_Ali_releaseHandle");
                Calculate = Load<CalculateDelegate>("CV_Ali_calcMtf");
                Load<InitializeDelegate>("CV_Ali_initial")();
                LibraryPath = library;
            }
            catch { NativeLibrary.Free(module); throw; }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void InitializeDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr CreateHandleDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ReleaseHandleDelegate(IntPtr handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CalculateDelegate(IntPtr handle, int width, int height, int bpp, int channels, IntPtr pixels,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters, [MarshalAs(UnmanagedType.LPUTF8Str)] string regions,
        [Out] byte[] result, ref int resultLength, ref int showBpp, ref int showChannels, IntPtr showData);
}
