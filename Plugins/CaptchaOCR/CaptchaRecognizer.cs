using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;

namespace CaptchaOCR
{
    public class CaptchaRecognizer : IDisposable
    {
        private const string Alphanumeric62Charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
        private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };

        private readonly InferenceSession? _session;
        private readonly CaptchaModelInfo _modelInfo;
        private readonly string[] _charset;
        private readonly string _inputName;
        private readonly string? _lengthOutputName;
        private readonly string _primaryOutputName;
        private bool _disposed;

        public int MinLength => _modelInfo.MinLength;
        public int MaxLength => _modelInfo.MaxLength;
        public bool IsLoaded => _session != null;
        public string? ErrorMessage { get; private set; }
        public CaptchaModelInfo ModelInfo => _modelInfo;

        public CaptchaRecognizer(string modelPath)
            : this(new CaptchaModelInfo { Path = modelPath })
        {
        }

        public CaptchaRecognizer(CaptchaModelInfo modelInfo)
        {
            _modelInfo = modelInfo;
            _charset = GetCharset(modelInfo.Charset);

            try
            {
                var options = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
                };

                _session = new InferenceSession(modelInfo.ResolvePath(), options);
                _inputName = _session.InputMetadata.Keys.First();
                var outputNames = _session.OutputMetadata.Keys.ToList();

                if (modelInfo.ModelType == ModelType.LengthAndChars)
                {
                    _lengthOutputName = outputNames.FirstOrDefault(n => n.Contains("length", StringComparison.OrdinalIgnoreCase)) ?? outputNames.ElementAtOrDefault(0);
                    _primaryOutputName = outputNames.FirstOrDefault(n => n.Contains("char", StringComparison.OrdinalIgnoreCase)) ?? outputNames.ElementAtOrDefault(1) ?? outputNames[0];
                }
                else
                {
                    _lengthOutputName = null;
                    _primaryOutputName = outputNames.First();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                _session = null;
                _inputName = "input";
                _lengthOutputName = null;
                _primaryOutputName = "output";
            }
        }

        public RecognitionResult? Recognize(Bitmap bitmap)
        {
            if (_session == null)
                return null;

            try
            {
                var inputValue = NamedOnnxValue.CreateFromTensor(_inputName, PreprocessImage(bitmap));
                using var results = _session.Run(new[] { inputValue });

                return _modelInfo.ModelType switch
                {
                    ModelType.LengthAndChars => DecodeLengthAndChars(results),
                    ModelType.CtcSequence => DecodeCtc(results),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return null;
            }
        }

        private DenseTensor<float> PreprocessImage(Bitmap bitmap)
        {
            return _modelInfo.ModelType switch
            {
                ModelType.LengthAndChars => PreprocessLengthAndChars(bitmap),
                ModelType.CtcSequence => PreprocessCtc(bitmap),
                _ => throw new NotSupportedException($"不支持的模型类型: {_modelInfo.ModelType}")
            };
        }

        private DenseTensor<float> PreprocessLengthAndChars(Bitmap bitmap)
        {
            using var resized = new Bitmap(bitmap, _modelInfo.InputWidth, _modelInfo.InputHeight);
            var rect = new Rectangle(0, 0, _modelInfo.InputWidth, _modelInfo.InputHeight);
            var bitmapData = resized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                var tensor = new DenseTensor<float>(new[] { 1, 3, _modelInfo.InputHeight, _modelInfo.InputWidth });
                var stride = bitmapData.Stride;
                var ptr = bitmapData.Scan0;

                for (int y = 0; y < _modelInfo.InputHeight; y++)
                {
                    for (int x = 0; x < _modelInfo.InputWidth; x++)
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

        private DenseTensor<float> PreprocessCtc(Bitmap bitmap)
        {
            int targetHeight = _modelInfo.InputHeight;
            int targetWidth = _modelInfo.VariableWidth
                ? Math.Max(1, (int)Math.Round(bitmap.Width * (targetHeight / (double)bitmap.Height)))
                : _modelInfo.InputWidth;

            using var resized = new Bitmap(bitmap, targetWidth, targetHeight);
            var rect = new Rectangle(0, 0, targetWidth, targetHeight);
            var bitmapData = resized.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
                var tensor = new DenseTensor<float>(new[] { 1, _modelInfo.InputChannels, targetHeight, targetWidth });
                var stride = bitmapData.Stride;
                var ptr = bitmapData.Scan0;

                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int offset = y * stride + x * 3;
                        byte b = Marshal.ReadByte(ptr, offset);
                        byte g = Marshal.ReadByte(ptr, offset + 1);
                        byte r = Marshal.ReadByte(ptr, offset + 2);
                        float gray = (0.299f * r + 0.587f * g + 0.114f * b) / 255.0f;

                        tensor[0, 0, y, x] = gray;
                    }
                }

                return tensor;
            }
            finally
            {
                resized.UnlockBits(bitmapData);
            }
        }

        private RecognitionResult? DecodeLengthAndChars(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            if (_lengthOutputName == null)
                return null;

            var lengthOutput = results.FirstOrDefault(r => r.Name == _lengthOutputName)?.AsTensor<float>();
            var charOutput = results.FirstOrDefault(r => r.Name == _primaryOutputName)?.AsTensor<float>();
            if (lengthOutput == null || charOutput == null)
                return null;

            var lengthLogits = new float[lengthOutput.Dimensions[1]];
            for (int i = 0; i < lengthLogits.Length; i++)
                lengthLogits[i] = lengthOutput[0, i];

            var lengthProbs = Softmax(lengthLogits);
            int predLengthIdx = ArgMax(lengthProbs);
            int predLength = Math.Clamp(predLengthIdx + MinLength, MinLength, MaxLength);
            int maxPositions = charOutput.Dimensions[1];
            predLength = Math.Min(predLength, maxPositions);

            var chars = new char[predLength];
            var confidences = new float[predLength];

            for (int pos = 0; pos < predLength; pos++)
            {
                int classCount = charOutput.Dimensions[2];
                var logits = new float[classCount];
                for (int c = 0; c < classCount; c++)
                    logits[c] = charOutput[0, pos, c];

                var probs = Softmax(logits);
                int bestIdx = ArgMax(probs);
                chars[pos] = bestIdx < _charset.Length && _charset[bestIdx].Length > 0 ? _charset[bestIdx][0] : '?';
                confidences[pos] = probs[bestIdx];
            }

            return new RecognitionResult
            {
                Text = new string(chars),
                Confidences = confidences,
                AverageConfidence = confidences.Length > 0 ? confidences.Average() : 0f,
                LengthConfidence = lengthProbs[predLengthIdx],
                PredLength = predLength
            };
        }

        private RecognitionResult? DecodeCtc(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            var output = results.FirstOrDefault(r => r.Name == _primaryOutputName)?.AsTensor<float>();
            if (output == null)
                return null;

            var (timeSteps, classCount, valueAt) = CreateCtcAccessor(output);
            if (timeSteps <= 0 || classCount <= 0)
                return null;

            var resultChars = new List<string>();
            var confidences = new List<float>();
            int? prevIdx = null;

            for (int t = 0; t < timeSteps; t++)
            {
                var logits = new float[classCount];
                for (int c = 0; c < classCount; c++)
                    logits[c] = valueAt(t, c);

                var probs = Softmax(logits);
                int idx = ArgMax(probs);

                if (idx == prevIdx)
                    continue;

                prevIdx = idx;
                if (idx == 0)
                    continue;
                if (idx < 0 || idx >= _charset.Length)
                    continue;
                if (string.IsNullOrEmpty(_charset[idx]))
                    continue;

                resultChars.Add(_charset[idx]);
                confidences.Add(probs[idx]);
            }

            if (resultChars.Count > MaxLength)
            {
                resultChars = resultChars.Take(MaxLength).ToList();
                confidences = confidences.Take(MaxLength).ToList();
            }

            return new RecognitionResult
            {
                Text = string.Concat(resultChars),
                Confidences = confidences.ToArray(),
                AverageConfidence = confidences.Count > 0 ? confidences.Average() : 0f,
                LengthConfidence = confidences.Count > 0 ? confidences.Average() : 0f,
                PredLength = resultChars.Count
            };
        }

        private static (int TimeSteps, int ClassCount, Func<int, int, float> ValueAt) CreateCtcAccessor(Tensor<float> output)
        {
            if (output.Dimensions.Length == 3)
            {
                if (output.Dimensions[0] == 1)
                {
                    int timeSteps = output.Dimensions[1];
                    int classCount = output.Dimensions[2];
                    return (timeSteps, classCount, (t, c) => output[0, t, c]);
                }

                if (output.Dimensions[1] == 1)
                {
                    int timeSteps = output.Dimensions[0];
                    int classCount = output.Dimensions[2];
                    return (timeSteps, classCount, (t, c) => output[t, 0, c]);
                }

                int defaultTimeSteps = output.Dimensions[1];
                int defaultClassCount = output.Dimensions[2];
                return (defaultTimeSteps, defaultClassCount, (t, c) => output[0, t, c]);
            }

            if (output.Dimensions.Length == 2)
            {
                int timeSteps = output.Dimensions[0];
                int classCount = output.Dimensions[1];
                return (timeSteps, classCount, (t, c) => output[t, c]);
            }

            throw new NotSupportedException($"不支持的 CTC 输出维度: {string.Join('x', output.Dimensions.ToArray())}");
        }

        private static string[] GetCharset(CharsetType charsetType)
        {
            return charsetType switch
            {
                CharsetType.Alphanumeric62 => Alphanumeric62Charset.Select(c => c.ToString()).ToArray(),
                CharsetType.DdddBeta => DdddCharsets.Beta,
                CharsetType.DdddOld => DdddCharsets.Old,
                _ => Alphanumeric62Charset.Select(c => c.ToString()).ToArray()
            };
        }

        private static int ArgMax(IReadOnlyList<float> values)
        {
            int bestIdx = 0;
            float bestValue = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > bestValue)
                {
                    bestValue = values[i];
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static float[] Softmax(float[] logits)
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
        public float LengthConfidence { get; set; }
        public int PredLength { get; set; }
    }
}
