using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace YoloObjectDetection;

public record Detection(
    Rect BoundingBox,
    int ClassId,
    string ClassName,
    float Confidence);

public sealed class YoloDetector : IDisposable
{
    private readonly InferenceSession _session;
    private readonly string[] _classNames;
    private readonly int _inputWidth;
    private readonly int _inputHeight;
    private readonly int _inputPixels; // width * height

    private const int DefaultInputSize = 640;
    private const int MaxCandidates = 300;
    private const int MaxDetections = 100;

    public int InputWidth => _inputWidth;
    public int InputHeight => _inputHeight;
    public int ClassCount => _classNames.Length;
    public string OutputShapeText { get; private set; } = "未推理";

    public YoloDetector(string modelPath, IReadOnlyList<string> classNames)
    {
        if (classNames.Count == 0)
            throw new ArgumentException("至少需要一个类别名。", nameof(classNames));

        _classNames = classNames.ToArray();

        var options = new SessionOptions();
        options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

        // CPU execution — for GPU, add Microsoft.ML.OnnxRuntime.DirectML package
        // and call: options.AppendExecutionProvider_DML(0);

        _session = new InferenceSession(modelPath, options);

        var inputShape = _session.InputMetadata[_session.InputNames[0]].Dimensions.ToArray();
        _inputHeight = inputShape.Length >= 4 && inputShape[2] > 0 ? inputShape[2] : DefaultInputSize;
        _inputWidth = inputShape.Length >= 4 && inputShape[3] > 0 ? inputShape[3] : DefaultInputSize;
        _inputPixels = _inputWidth * _inputHeight;
    }

    public unsafe IReadOnlyList<Detection> Detect(Mat image, float confThreshold = 0.4f, float iouThreshold = 0.5f)
    {
        int origWidth = image.Width;
        int origHeight = image.Height;

        // -- Step 1: letterbox resize + BGR to RGB + normalize --
        float ratio = Math.Min((float)_inputWidth / origWidth, (float)_inputHeight / origHeight);
        int newW = (int)(origWidth * ratio);
        int newH = (int)(origHeight * ratio);
        int padX = (_inputWidth - newW) / 2;
        int padY = (_inputHeight - newH) / 2;

        using Mat resized = new();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(newW, newH));

        // BGR → RGB (YOLO was trained on RGB images)
        using Mat rgb = new();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // Letterbox padding with gray (114,114,114)
        using Mat padded = new(_inputHeight, _inputWidth, MatType.CV_8UC3, Scalar.All(114));
        rgb.CopyTo(padded[new Rect(padX, padY, newW, newH)]);

        // Convert to float32 [0, 1]
        using Mat floatMat = new();
        padded.ConvertTo(floatMat, MatType.CV_32FC3, 1.0 / 255.0);

        // -- Step 2: HWC → CHW via unsafe pointer copy --
        float[] inputData = new float[3 * _inputPixels];
        int wh = _inputPixels;
        byte* src = (byte*)floatMat.Data;
        long srcStep = floatMat.Step();

        fixed (float* dst = inputData)
        {
            float* r = dst;
            float* g = dst + wh;
            float* b = dst + wh * 2;

            for (int y = 0; y < _inputHeight; y++)
            {
                float* row = (float*)(src + y * srcStep);
                for (int x = 0; x < _inputWidth; x++)
                {
                    r[y * _inputWidth + x] = row[x * 3 + 0];
                    g[y * _inputWidth + x] = row[x * 3 + 1];
                    b[y * _inputWidth + x] = row[x * 3 + 2];
                }
            }
        }

        // -- Step 3: inference --
        var inputTensor = new DenseTensor<float>(inputData, [1, 3, _inputHeight, _inputWidth]);
        using var inputOrt = OrtValue.CreateTensorValueFromMemory(
            OrtMemoryInfo.DefaultInstance, inputTensor.Buffer, [1, 3, _inputHeight, _inputWidth]);

        using var results = _session.Run(
            new RunOptions(),
            [_session.InputNames[0]],
            [inputOrt],
            _session.OutputNames);

        GC.KeepAlive(inputTensor);

        // -- Step 4: post-process --
        var outputShape = results[0].GetTensorTypeAndShape().Shape.ToArray();
        OutputShapeText = FormatShape(outputShape);
        ReadOnlySpan<float> output = results[0].GetTensorDataAsSpan<float>();

        return PostProcess(output, outputShape, _classNames, origWidth, origHeight, ratio, padX, padY, confThreshold, iouThreshold);
    }

    private static List<Detection> PostProcess(
        ReadOnlySpan<float> output, long[] outputShape, string[] classNames, int origWidth, int origHeight,
        float ratio, int padX, int padY,
        float confThreshold, float iouThreshold)
    {
        int numClasses = classNames.Length;
        var layout = ResolveOutputLayout(outputShape, output.Length, numClasses);

        var candidates = new List<Detection>();

        for (int i = 0; i < layout.PredictionCount; i++)
        {
            float objectness = layout.HasObjectness ? GetOutputValue(output, layout, i, 4) : 1.0f;
            if (objectness < confThreshold) continue;

            float maxScore = 0;
            int bestClass = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = objectness * GetOutputValue(output, layout, i, layout.ClassOffset + c);
                if (score > maxScore)
                {
                    maxScore = score;
                    bestClass = c;
                }
            }

            if (maxScore < confThreshold) continue;

            float cx = GetOutputValue(output, layout, i, 0);
            float cy = GetOutputValue(output, layout, i, 1);
            float w = GetOutputValue(output, layout, i, 2);
            float h = GetOutputValue(output, layout, i, 3);

            if (w <= 1 || h <= 1) continue;

            float x1 = (cx - w / 2 - padX) / ratio;
            float y1 = (cy - h / 2 - padY) / ratio;
            float x2 = (cx + w / 2 - padX) / ratio;
            float y2 = (cy + h / 2 - padY) / ratio;

            x1 = Math.Clamp(x1, 0, origWidth);
            y1 = Math.Clamp(y1, 0, origHeight);
            x2 = Math.Clamp(x2, 0, origWidth);
            y2 = Math.Clamp(y2, 0, origHeight);

            int boxWidth = (int)Math.Round(x2 - x1);
            int boxHeight = (int)Math.Round(y2 - y1);
            if (boxWidth <= 1 || boxHeight <= 1) continue;

            candidates.Add(new Detection(
                new Rect((int)Math.Round(x1), (int)Math.Round(y1), boxWidth, boxHeight),
                bestClass, classNames[bestClass], maxScore));
        }

        return NMS(candidates, iouThreshold);
    }

    private static string FormatShape(long[] shape)
    {
        return shape.Length == 0 ? "[]" : $"[{string.Join(", ", shape)}]";
    }

    private static OutputLayout ResolveOutputLayout(long[] shape, int outputLength, int numClasses)
    {
        int yoloV8Width = numClasses + 4;
        int yoloV5Width = numClasses + 5;

        if (shape.Length == 3)
        {
            int first = (int)shape[1];
            int second = (int)shape[2];

            if (first == yoloV8Width || first == yoloV5Width)
                return new OutputLayout(second, first, true, first == yoloV5Width);

            if (second == yoloV8Width || second == yoloV5Width)
                return new OutputLayout(first, second, false, second == yoloV5Width);
        }

        if (outputLength % yoloV8Width == 0)
            return new OutputLayout(outputLength / yoloV8Width, yoloV8Width, true, false);

        if (outputLength % yoloV5Width == 0)
            return new OutputLayout(outputLength / yoloV5Width, yoloV5Width, false, true);

        throw new NotSupportedException($"不支持的 YOLO 输出形状: [{string.Join(", ", shape)}], length={outputLength}");
    }

    private static float GetOutputValue(ReadOnlySpan<float> output, OutputLayout layout, int predictionIndex, int valueIndex)
    {
        return layout.Transposed
            ? output[valueIndex * layout.PredictionCount + predictionIndex]
            : output[predictionIndex * layout.ValueCount + valueIndex];
    }

    private static List<Detection> NMS(List<Detection> detections, float iouThreshold)
    {
        if (detections.Count == 0) return detections;

        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        if (detections.Count > MaxCandidates)
            detections.RemoveRange(MaxCandidates, detections.Count - MaxCandidates);

        var keep = new bool[detections.Count];
        Array.Fill(keep, true);

        for (int i = 0; i < detections.Count; i++)
        {
            if (!keep[i]) continue;
            for (int j = i + 1; j < detections.Count; j++)
            {
                if (!keep[j]) continue;
                if (detections[i].ClassId != detections[j].ClassId) continue;
                if (ComputeIoU(detections[i].BoundingBox, detections[j].BoundingBox) > iouThreshold)
                    keep[j] = false;
            }
        }

        var result = new List<Detection>();
        for (int i = 0; i < detections.Count; i++)
        {
            if (!keep[i]) continue;
            result.Add(detections[i]);
            if (result.Count >= MaxDetections) break;
        }

        return result;
    }

    private readonly record struct OutputLayout(
        int PredictionCount,
        int ValueCount,
        bool Transposed,
        bool HasObjectness)
    {
        public int ClassOffset => HasObjectness ? 5 : 4;
    }

    private static float ComputeIoU(Rect a, Rect b)
    {
        int interX1 = Math.Max(a.X, b.X);
        int interY1 = Math.Max(a.Y, b.Y);
        int interX2 = Math.Min(a.X + a.Width, b.X + b.Width);
        int interY2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

        int interW = Math.Max(0, interX2 - interX1);
        int interH = Math.Max(0, interY2 - interY1);
        float interArea = interW * interH;

        float areaA = a.Width * a.Height;
        float areaB = b.Width * b.Height;
        float unionArea = areaA + areaB - interArea;

        return unionArea > 0 ? interArea / unionArea : 0;
    }

    public void Dispose() => _session.Dispose();
}
