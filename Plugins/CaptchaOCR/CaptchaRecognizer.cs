using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace CaptchaOCR
{
    /// <summary>
    /// 验证码识别器 - 基于 ONNX Runtime
    /// </summary>
    public class CaptchaRecognizer : IDisposable
    {
        private const string Charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const int NumPositions = 4;
        private const int NumClasses = 62;
        private const int InputWidth = 160;
        private const int InputHeight = 60;

        private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

        private readonly InferenceSession? _session;
        private readonly string _inputName;
        private readonly string _outputName;
        private bool _disposed;

        public bool IsLoaded => _session != null;
        public string? ErrorMessage { get; private set; }

        public CaptchaRecognizer(string modelPath)
        {
            try
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
                };

                _session = new InferenceSession(modelPath, options);
                _inputName = _session.InputMetadata.Keys.First();
                _outputName = _session.OutputMetadata.Keys.First();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _session = null;
                _inputName = "input";
                _outputName = "output";
            }
        }

        public RecognitionResult? Recognize(Bitmap bitmap)
        {
            if (_session == null) return null;

            try
            {
                var inputTensor = PreprocessImage(bitmap);
                var inputs = new NamedOnnxValue[]
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>();
                return DecodeOutput(output);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return null;
            }
        }

        private DenseTensor<float> PreprocessImage(Bitmap bitmap)
        {
            using var resized = new Bitmap(bitmap, InputWidth, InputHeight);
            var rect = new Rectangle(0, 0, InputWidth, InputHeight);
            var bitmapData = resized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                var tensor = new DenseTensor<float>(new[] { 1, 3, InputHeight, InputWidth });
                var stride = bitmapData.Stride;
                var ptr = bitmapData.Scan0;

                for (int y = 0; y < InputHeight; y++)
                {
                    for (int x = 0; x < InputWidth; x++)
                    {
                        int offset = y * stride + x * 3;
                        byte b = Marshal.ReadByte(ptr, offset);
                        byte g = Marshal.ReadByte(ptr, offset + 1);
                        byte r = Marshal.ReadByte(ptr, offset + 2);

                        tensor[0, 0, y, x] = (r / 255.0f - Mean[0]) / Std[0];
                        tensor[0, 1, y, x] = (g / 255.0f - Mean[1]) / Std[1];
                        tensor[0, 2, y, x] = (b / 255.0f - Mean[2]) / Std[2];
                    }
                }

                return tensor;
            }
            finally
            {
                resized.UnlockBits(bitmapData);
            }
        }

        private RecognitionResult DecodeOutput(Tensor<float> output)
        {
            var chars = new char[NumPositions];
            var confidences = new float[NumPositions];

            for (int pos = 0; pos < NumPositions; pos++)
            {
                var logits = new float[NumClasses];
                for (int c = 0; c < NumClasses; c++)
                {
                    logits[c] = output[0, pos, c];
                }

                var probs = Softmax(logits);
                int bestIdx = 0;
                float bestProb = probs[0];
                for (int c = 1; c < NumClasses; c++)
                {
                    if (probs[c] > bestProb)
                    {
                        bestProb = probs[c];
                        bestIdx = c;
                    }
                }

                chars[pos] = Charset[bestIdx];
                confidences[pos] = bestProb;
            }

            return new RecognitionResult
            {
                Text = new string(chars),
                Confidences = confidences,
                AverageConfidence = confidences.Average()
            };
        }

        private float[] Softmax(float[] logits)
        {
            float maxLogit = logits.Max();
            var expValues = logits.Select(l => MathF.Exp(l - maxLogit)).ToArray();
            float sumExp = expValues.Sum();
            return expValues.Select(v => v / sumExp).ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _session?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }

    public class RecognitionResult
    {
        public string Text { get; set; } = "";
        public float[] Confidences { get; set; } = Array.Empty<float>();
        public float AverageConfidence { get; set; }
    }
}
