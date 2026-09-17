using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Bounded, read-only display-pattern measurements. Each analysis documents its acquisition contract.</summary>
public sealed partial class DisplayMetrologyProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
{
    public const int MaximumFramePixels = 8_388_608;
    public const int MaximumRgbCrossPixels = 67_108_864;
    public const long MaximumTotalPixels = 33_554_432;
    private const int MaximumComponents = 2048;
    public AlgorithmProviderMetadata Metadata { get; } = new("colorvision.display-metrology.cpu", "ColorVision Display Metrology",
        AlgorithmProviderKind.Cpu, AlgorithmExecutionPlane.Local, 150,
        DisplayMetrologyCatalog.Capabilities | AlgorithmHostCapabilities.MultiInput,
        Enum.GetValues<AlgorithmImageFormat>().ToHashSet(), "1.0.0");

    public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        => StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, DisplayMetrologyIds.All, out reason);

    public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
    {
        bool valid = CanExecuteDescriptor(descriptor, out reason) && inputs.Count >= descriptor.MinimumInputCount
            && inputs.Count <= descriptor.MaximumInputCount && inputs.All(i => descriptor.SupportedFormats.Contains(i.Image.Format));
        if (!valid) reason ??= "display_input_contract_mismatch";
        return valid;
    }

    public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
    {
        var artifacts = new List<AlgorithmArtifact>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = (DisplayMetrologyParameters)context.Parameters;
            if (!p.Validate().IsValid) throw new MeasurementException("invalid_parameters", "参数未通过校验。");
            var first = context.Inputs[0].Image;
            int frameBudget = context.Descriptor.Id == DisplayMetrologyIds.RgbCrossRegistration ? MaximumRgbCrossPixels : MaximumFramePixels;
            long totalBudget = context.Descriptor.Id == DisplayMetrologyIds.RgbCrossRegistration ? MaximumRgbCrossPixels : MaximumTotalPixels;
            if (context.Inputs.Any(i => i.Image.Width < 32 || i.Image.Height < 32
                || (long)i.Image.Width * i.Image.Height > frameBudget)
                || context.Inputs.Sum(i => (long)i.Image.Width * i.Image.Height) > totalBudget
                || context.Inputs.Sum(i => (long)i.Image.Stride * i.Image.Height) > 512L * 1024 * 1024)
                throw new MeasurementException("image_budget_exceeded", $"图像最小为 32×32，单帧最多 {frameBudget} 像素，总计最多 {totalBudget} 像素 / 512 MiB。");
            if (context.Invocation.Roi != null) throw new MeasurementException("roi_unsupported", "请先裁到完整测试图案；本算法不隐式裁剪 ROI。");
            if (context.Inputs.Any(i => i.Image.Width != first.Width || i.Image.Height != first.Height || i.Image.Format != first.Format))
                throw new MeasurementException("input_mismatch", "多帧必须具有相同尺寸和格式，不自动缩放或对齐。");
            if (context.Inputs.Count > 1 && (context.Inputs.Any(i => string.IsNullOrWhiteSpace(i.ColorSpace))
                || context.Inputs.Any(i => !string.Equals(i.ColorSpace, context.Inputs[0].ColorSpace, StringComparison.OrdinalIgnoreCase))))
                throw new MeasurementException("color_space_mismatch", "多帧必须明确使用相同的信号编码。");

            if (context.Descriptor.Id == DisplayMetrologyIds.RgbRegistration) MeasureRgb(context, (RgbRegistrationParameters)p, artifacts, cancellationToken);
            else if (context.Descriptor.Id == DisplayMetrologyIds.RgbCrossRegistration) MeasureRgbCross(context, (RgbCrossRegistrationParameters)p, artifacts, cancellationToken);
            else if (context.Descriptor.Id == DisplayMetrologyIds.Binocular) MeasureBinocular(context, (BinocularQualityParameters)p, artifacts, cancellationToken);
            else if (context.Descriptor.Id == DisplayMetrologyIds.Eyebox) MeasureEyebox(context, (EyeboxScanParameters)p, artifacts, cancellationToken);
            else
            {
                float[] signal = ReadSignal(first, p.Channel, p.DecodeExponent, cancellationToken);
                if (context.Descriptor.Id == DisplayMetrologyIds.Ghost) MeasureGhost(signal, first.Width, first.Height, (GhostMeasurementParameters)p, artifacts, cancellationToken);
                else if (context.Descriptor.Id == DisplayMetrologyIds.Defects) MeasureDefects(signal, first.Width, first.Height, (DisplayDefectParameters)p, artifacts, cancellationToken);
                else if (context.Descriptor.Id == DisplayMetrologyIds.FieldSfr) MeasureFieldSfr(signal, first.Width, first.Height, (FieldSfrParameters)p, artifacts, cancellationToken);
                else throw new MeasurementException("unsupported_algorithm", "不支持的显示计量算法。");
            }
            artifacts.Add(new AlgorithmStructuredDataArtifact("measurement-context", "colorvision.display.context/v1", AlgorithmJson.ToElement(new
            {
                measurementDomain = "decoded-relative-device-signal", spatialUnit = "pixel", validationScope = "analytical-and-synthetic",
                parameters = context.Invocation.Parameters,
                inputs = context.Inputs.Select(i => new { i.Name, i.SourceUri, i.SourceRevision, i.ColorSpace }),
                note = "测量结果不构成现场检出率、绝对亮色度、标准符合性或产品 PASS/FAIL 结论。",
            })));
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId, AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version, Status = AlgorithmResultStatus.Succeeded, Artifacts = artifacts,
            });
        }
        catch (MeasurementException error)
        {
            DisposeImages(artifacts);
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId, AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version, Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(error.Code, error.Message)],
            });
        }
        catch { DisposeImages(artifacts); throw; }
    }

    private static void DisposeImages(IEnumerable<AlgorithmArtifact> artifacts)
    {
        foreach (var image in artifacts.OfType<AlgorithmImageArtifact>()) image.Dispose();
    }

    private sealed class MeasurementException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private static float[] ReadSignal(AlgorithmImageBuffer image, int channel, double exponent, CancellationToken token)
    {
        int channels = image.Format.Channels();
        if (channels == 1) channel = 0;
        int bytes = image.Format.BytesPerPixel() / channels;
        var signal = new float[checked(image.Width * image.Height)];
        ReadOnlySpan<byte> data = image.Data.Span;
        for (int y = 0; y < image.Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < image.Width; x++)
            {
                int offset = y * image.Stride + x * channels * bytes;
                for (int c = 0; c < channels; c++)
                {
                    double value = bytes == 1 ? data[offset + c] / 255.0
                        : bytes == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + c * bytes, 2)) / 65535.0
                        : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + c * bytes, 4)));
                    if (!double.IsFinite(value) || value < 0 || value > 1)
                        throw new MeasurementException("invalid_signal", $"像素 ({x},{y}) 通道 {c} 不在有限 [0,1] 标称范围内。");
                    if (channels == 4 && c == 3 && value != 1)
                        throw new MeasurementException("transparent_input", "显示计量要求不透明图像，不能把 Alpha 当作有效测量区。");
                    if (c == channel) signal[y * image.Width + x] = (float)Math.Pow(value, exponent);
                }
            }
        }
        return signal;
    }

    private static IEnumerable<(int Index, Rect Bounds)> Grid(int width, int height, DisplayGridParameters p)
    {
        if (width / p.Columns < 24 || height / p.Rows < 24)
            throw new MeasurementException("grid_too_small", "每格至少需要 24×24 像素。");
        for (int r = 0; r < p.Rows; r++)
            for (int c = 0; c < p.Columns; c++)
                yield return (r * p.Columns + c, new Rect(c * width / p.Columns, r * height / p.Rows,
                    (c + 1) * width / p.Columns - c * width / p.Columns, (r + 1) * height / p.Rows - r * height / p.Rows));
    }

    private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
        => values.ToDictionary(v => v.Name, v => AlgorithmJson.ToElement(v.Value));

    private static void Table(List<AlgorithmArtifact> artifacts, string name, string[] columns, List<IReadOnlyDictionary<string, JsonElement>> rows)
        => artifacts.Add(new AlgorithmTableArtifact(name, columns.Select(c => new AlgorithmTableColumn(c, "value")).ToArray(), rows));

    private static void Metrics(List<AlgorithmArtifact> artifacts, params (string Name, double Value, string Unit)[] values)
        => artifacts.Add(new AlgorithmMeasurementArtifact($"summary-{artifacts.OfType<AlgorithmMeasurementArtifact>().Count() + 1}", values.Select(v => new AlgorithmMeasurement(v.Name, v.Value, v.Unit)).ToArray()));

    private static void Overlay(List<AlgorithmArtifact> artifacts, List<AlgorithmGeometry> shapes)
    {
        artifacts.Add(new AlgorithmGeometryArtifact("display-regions", AlgorithmCoordinateSpace.Pixel, shapes));
        artifacts.Add(new AlgorithmOverlayArtifact("display-measurements", AlgorithmOverlayLifetime.Transient,
            shapes.Select(s => new AlgorithmOverlayItem(s.Id, new AlgorithmOverlayStyle(Label: s.Id))).ToArray()));
    }

    private static AlgorithmGeometry Box(string id, Rect bounds) => new(id, AlgorithmGeometryKind.Rectangle,
        [new AlgorithmPoint(bounds.X, bounds.Y), new AlgorithmPoint(bounds.Right, bounds.Bottom)]);

    private sealed record Component(Rect Bounds, int Area, double Sum, double Peak, double X, double Y);

    // Bounded four-connected flood fill. A budget failure rejects the run, never silently reports a partial defect count.
    private static List<Component> Components(byte[] mask, float[] signal, int width, int height, CancellationToken token)
    {
        var components = new List<Component>();
        var queue = new int[mask.Length];
        for (int start = 0; start < mask.Length; start++)
        {
            if ((start & 4095) == 0) token.ThrowIfCancellationRequested();
            if (mask[start] == 0) continue;
            if (components.Count == MaximumComponents) throw new MeasurementException("component_budget_exceeded", "候选区域超过 2048，请检查图案、阈值和噪声水平。");
            int head = 0, tail = 1, minX = width, minY = height, maxX = 0, maxY = 0;
            double sum = 0, peak = 0, sx = 0, sy = 0;
            queue[0] = start; mask[start] = 0;
            while (head < tail)
            {
                if ((head & 4095) == 0) token.ThrowIfCancellationRequested();
                int index = queue[head++], x = index % width, y = index / width;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                double value = Math.Abs(signal[index]); sum += value; peak = Math.Max(peak, value); sx += value * x; sy += value * y;
                if (x > 0) Visit(index - 1);
                if (x + 1 < width) Visit(index + 1);
                if (y > 0) Visit(index - width);
                if (y + 1 < height) Visit(index + width);
            }
            components.Add(new Component(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), tail, sum, peak,
                sum > 0 ? sx / sum : (minX + maxX) / 2.0, sum > 0 ? sy / sum : (minY + maxY) / 2.0));
            void Visit(int index)
            {
                if (mask[index] == 0) return;
                mask[index] = 0; queue[tail++] = index;
            }
        }
        return components;
    }
}
