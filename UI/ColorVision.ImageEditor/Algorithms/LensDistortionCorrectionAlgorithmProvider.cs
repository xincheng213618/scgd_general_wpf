using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic local Brown-Conrady undistortion with explicit calibration provenance.</summary>
    public sealed class LensDistortionCorrectionAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.geometry.lens-distortion-correction/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.lens-distortion-correction.cpu",
            "ColorVision Lens Distortion Correction CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            126,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.LensDistortionCorrection, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.LensDistortionCorrection
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer source = context.Inputs[0].Image;
            LensDistortionCorrectionParameters parameters = (LensDistortionCorrectionParameters)context.Parameters;
            double principalX = parameters.PrincipalPointMode == LensDistortionPrincipalPointMode.ImageCenter
                ? (source.Width - 1) * 0.5
                : parameters.PrincipalPointX;
            double principalY = parameters.PrincipalPointMode == LensDistortionPrincipalPointMode.ImageCenter
                ? (source.Height - 1) * 0.5
                : parameters.PrincipalPointY;
            double[] inputCamera =
            [
                parameters.FxPixels, 0, principalX,
                0, parameters.FyPixels, principalY,
                0, 0, 1,
            ];
            double[] coefficients = [parameters.K1, parameters.K2, parameters.P1, parameters.P2, parameters.K3, parameters.K4, parameters.K5, parameters.K6];
            double[] outputCamera = (double[])inputCamera.Clone();
            Rect optimalValidRoi = new(0, 0, source.Width, source.Height);
            AlgorithmImageBuffer? corrected = null;
            AlgorithmImageBuffer? validityMask = null;
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                MapStatistics statistics;
                if (parameters.OutputCameraMode == LensDistortionOutputCameraMode.PreserveCalibratedIntrinsics
                    && coefficients.All(value => value == 0))
                {
                    context.Progress?.Report(new AlgorithmProgress(0.2, "lens-distortion.identity"));
                    cancellationToken.ThrowIfCancellationRequested();
                    corrected = source.Clone();
                    byte[] fullMask = new byte[checked(source.Width * source.Height)];
                    Array.Fill(fullMask, byte.MaxValue);
                    validityMask = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, fullMask, source.DpiX, source.DpiY);
                    statistics = new MapStatistics((long)source.Width * source.Height, 0, 0, new Rect(0, 0, source.Width, source.Height));
                }
                else
                {
                    using Mat inputMatrix = Matrix(inputCamera, 3, 3);
                    using Mat coefficientMatrix = Matrix(coefficients, 1, coefficients.Length);
                    if (parameters.OutputCameraMode == LensDistortionOutputCameraMode.OptimalNewCameraMatrix)
                    {
                        using Mat optimal = Cv2.GetOptimalNewCameraMatrix(
                            inputMatrix,
                            coefficientMatrix,
                            new Size(source.Width, source.Height),
                            parameters.OptimalAlpha,
                            new Size(source.Width, source.Height),
                            out optimalValidRoi,
                            parameters.CenterOptimalPrincipalPoint);
                        outputCamera = ReadMatrix(optimal, 3, 3);
                    }
                    using Mat outputMatrix = Matrix(outputCamera, 3, 3);
                    using Mat rectification = Matrix([1, 0, 0, 0, 1, 0, 0, 0, 1], 3, 3);
                    using Mat mapX = new();
                    using Mat mapY = new();
                    context.Progress?.Report(new AlgorithmProgress(0.08, "lens-distortion.map"));
                    cancellationToken.ThrowIfCancellationRequested();
                    Cv2.InitUndistortRectifyMap(
                        inputMatrix,
                        coefficientMatrix,
                        rectification,
                        outputMatrix,
                        new Size(source.Width, source.Height),
                        MatType.CV_32FC1,
                        mapX,
                        mapY);
                    cancellationToken.ThrowIfCancellationRequested();
                    context.Progress?.Report(new AlgorithmProgress(0.3, "lens-distortion.mask"));
                    byte[] mask;
                    MapStatistics mapStatistics;
                    try
                    {
                        (mask, mapStatistics) = BuildValidityMask(mapX, mapY, source.Width, source.Height, cancellationToken, context.Progress);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return ValueTask.FromResult(Failure(context, "lens_distortion_map_invalid", exception.Message, "parameters"));
                    }
                    statistics = mapStatistics;
                    if (statistics.ValidPixels < (long)Math.Ceiling(parameters.MinimumValidFraction * source.Width * source.Height))
                    {
                        return ValueTask.FromResult(Failure(
                            context,
                            "lens_distortion_valid_fraction_too_low",
                            $"The undistortion map valid fraction {statistics.ValidPixels / (double)(source.Width * source.Height):G8} is below the configured minimum {parameters.MinimumValidFraction:G8}.",
                            "parameters.MinimumValidFraction"));
                    }
                    validityMask = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, mask, source.DpiX, source.DpiY);
                    using AlgorithmImageMatLease input = AlgorithmImageInterop.BorrowReadOnly(source);
                    using Mat output = new();
                    context.Progress?.Report(new AlgorithmProgress(0.76, "lens-distortion.remap"));
                    cancellationToken.ThrowIfCancellationRequested();
                    Cv2.Remap(
                        input.Mat,
                        output,
                        mapX,
                        mapY,
                        parameters.Interpolation == GeometricTransformInterpolation.Nearest ? InterpolationFlags.Nearest : InterpolationFlags.Linear,
                        parameters.Border == GeometricTransformBorder.Replicate ? BorderTypes.Replicate : BorderTypes.Constant,
                        BorderScalar(source.Format, parameters));
                    cancellationToken.ThrowIfCancellationRequested();
                    corrected = AlgorithmImageInterop.FromMat(output, source.DpiX, source.DpiY);
                }

                long totalPixels = (long)source.Width * source.Height;
                artifacts.Add(new AlgorithmImageArtifact("corrected-image", "primary", corrected,
                    new Dictionary<string, string>
                    {
                        ["cameraModel"] = "brown-conrady-pinhole",
                        ["calibrationSource"] = parameters.CalibrationSource,
                        ["calibrationVersion"] = parameters.CalibrationVersion,
                        ["calibrationChecksum"] = parameters.CalibrationChecksum,
                        ["presetId"] = context.Invocation.PresetId ?? string.Empty,
                    }));
                corrected = null;
                artifacts.Add(new AlgorithmImageArtifact("valid-region-mask", "validity-mask", validityMask,
                    new Dictionary<string, string> { ["valid"] = "255", ["invalid"] = "0" }));
                validityMask = null;
                artifacts.Add(BuildMeasurements(parameters, statistics, totalPixels));
                artifacts.Add(BuildCameraMatrixTable(inputCamera, outputCamera));
                artifacts.Add(BuildCoefficientTable(coefficients));
                if (statistics.ValidBounds.Width > 0 && statistics.ValidBounds.Height > 0)
                {
                    Rect bounds = statistics.ValidBounds;
                    artifacts.Add(new AlgorithmGeometryArtifact("lens-distortion-valid-region", AlgorithmCoordinateSpace.Pixel,
                    [
                        new AlgorithmGeometry("valid-region-bounds", AlgorithmGeometryKind.Polygon,
                        [
                            new AlgorithmPoint(bounds.Left, bounds.Top),
                            new AlgorithmPoint(bounds.Right - 1, bounds.Top),
                            new AlgorithmPoint(bounds.Right - 1, bounds.Bottom - 1),
                            new AlgorithmPoint(bounds.Left, bounds.Bottom - 1),
                        ]),
                    ]));
                }
                artifacts.Add(new AlgorithmStructuredDataArtifact("lens-distortion-correction", ResultSchema, AlgorithmJson.ToElement(new
                {
                    cameraModel = "brown-conrady-pinhole",
                    matrixSemantics = "pixel-center coordinates; undistorted output pixel maps through the Brown-Conrady model into distorted source pixels",
                    inputCameraMatrix = inputCamera,
                    outputCameraMatrix = outputCamera,
                    distortionCoefficients = new { parameters.K1, parameters.K2, parameters.P1, parameters.P2, parameters.K3, parameters.K4, parameters.K5, parameters.K6 },
                    principalPointMode = parameters.PrincipalPointMode.ToString(),
                    outputCameraMode = parameters.OutputCameraMode.ToString(),
                    parameters.OptimalAlpha,
                    parameters.CenterOptimalPrincipalPoint,
                    interpolation = parameters.Interpolation.ToString(),
                    border = parameters.Border.ToString(),
                    source = new { source.Width, source.Height, format = source.Format.ToString(), source.DpiX, source.DpiY },
                    output = new
                    {
                        source.Width,
                        source.Height,
                        validPixels = statistics.ValidPixels,
                        invalidPixels = totalPixels - statistics.ValidPixels,
                        validFraction = statistics.ValidPixels / (double)totalPixels,
                        statistics.MeanDisplacementPixels,
                        statistics.MaximumDisplacementPixels,
                        validBounds = RectData(statistics.ValidBounds),
                        optimalValidRoi = RectData(optimalValidRoi),
                    },
                    calibration = new
                    {
                        source = parameters.CalibrationSource,
                        version = parameters.CalibrationVersion,
                        checksum = parameters.CalibrationChecksum,
                        qualityAvailable = parameters.HasCalibrationQuality,
                        rmsErrorPixels = parameters.CalibrationRmsErrorPixels,
                        confidence = parameters.CalibrationConfidence,
                    },
                    presetId = context.Invocation.PresetId,
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "lens-distortion.complete"));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = new AlgorithmExecutionDiagnostics
                    {
                        Messages = statistics.ValidPixels == totalPixels
                            ? []
                            : [new AlgorithmDiagnosticMessage("lens_distortion_contains_invalid_output", $"{totalPixels - statistics.ValidPixels} corrected pixel centers map outside the source image.", "warning")],
                    },
                });
            }
            catch
            {
                corrected?.Dispose();
                validityMask?.Dispose();
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }
        }

        private static Mat Matrix(double[] values, int rows, int columns)
        {
            Mat matrix = new(rows, columns, MatType.CV_64FC1);
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    matrix.Set(row, column, values[row * columns + column]);
            return matrix;
        }

        private static double[] ReadMatrix(Mat matrix, int rows, int columns)
        {
            double[] values = new double[checked(rows * columns)];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    values[row * columns + column] = matrix.At<double>(row, column);
            return values;
        }

        private static Scalar BorderScalar(AlgorithmImageFormat format, LensDistortionCorrectionParameters parameters)
        {
            double peak = format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            return new Scalar(
                parameters.BorderChannel0 * peak,
                parameters.BorderChannel1 * peak,
                parameters.BorderChannel2 * peak,
                parameters.BorderChannel3 * peak);
        }

        private static unsafe (byte[] Mask, MapStatistics Statistics) BuildValidityMask(
            Mat mapX,
            Mat mapY,
            int width,
            int height,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            if (mapX.Rows != height || mapX.Cols != width || mapY.Rows != height || mapY.Cols != width
                || mapX.Type() != MatType.CV_32FC1 || mapY.Type() != MatType.CV_32FC1)
                throw new InvalidOperationException("OpenCV returned an invalid lens-distortion map shape or type.");
            byte[] mask = new byte[checked(width * height)];
            long valid = 0;
            double displacementSum = 0;
            double maximumDisplacement = 0;
            int minimumX = width;
            int minimumY = height;
            int maximumX = -1;
            int maximumY = -1;
            const double tolerance = 1e-5;
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.3 + 0.4 * y / Math.Max(1, height), "lens-distortion.mask"));
                }
                ReadOnlySpan<float> xRow = new((void*)mapX.Ptr(y), width);
                ReadOnlySpan<float> yRow = new((void*)mapY.Ptr(y), width);
                int offset = y * width;
                for (int x = 0; x < width; x++)
                {
                    double sourceX = xRow[x];
                    double sourceY = yRow[x];
                    if (!double.IsFinite(sourceX) || !double.IsFinite(sourceY))
                        throw new InvalidOperationException("The lens-distortion map contains non-finite coordinates.");
                    double displacement = Math.Sqrt((sourceX - x) * (sourceX - x) + (sourceY - y) * (sourceY - y));
                    displacementSum += displacement;
                    maximumDisplacement = Math.Max(maximumDisplacement, displacement);
                    if (sourceX < -tolerance || sourceY < -tolerance
                        || sourceX > width - 1 + tolerance || sourceY > height - 1 + tolerance) continue;
                    mask[offset + x] = byte.MaxValue;
                    valid++;
                    minimumX = Math.Min(minimumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumX = Math.Max(maximumX, x);
                    maximumY = Math.Max(maximumY, y);
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            Rect bounds = valid == 0
                ? new Rect()
                : new Rect(minimumX, minimumY, maximumX - minimumX + 1, maximumY - minimumY + 1);
            return (mask, new MapStatistics(valid, displacementSum / ((long)width * height), maximumDisplacement, bounds));
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            LensDistortionCorrectionParameters parameters,
            MapStatistics statistics,
            long totalPixels)
            => new("lens-distortion-summary",
            [
                new("lens-distortion.valid_pixel_count", statistics.ValidPixels, "px"),
                new("lens-distortion.invalid_pixel_count", totalPixels - statistics.ValidPixels, "px"),
                new("lens-distortion.valid_fraction", statistics.ValidPixels / (double)totalPixels, "ratio"),
                new("lens-distortion.mean_displacement", statistics.MeanDisplacementPixels, "px"),
                new("lens-distortion.maximum_displacement", statistics.MaximumDisplacementPixels, "px"),
                new("lens-distortion.calibration_quality_available", parameters.HasCalibrationQuality ? 1 : 0, "boolean"),
                new("lens-distortion.calibration_rms_error", parameters.CalibrationRmsErrorPixels, "px"),
                new("lens-distortion.calibration_confidence", parameters.CalibrationConfidence, "ratio"),
            ]);

        private static AlgorithmTableArtifact BuildCameraMatrixTable(double[] input, double[] output)
        {
            List<IReadOnlyDictionary<string, JsonElement>> rows = new(3);
            for (int row = 0; row < 3; row++)
            {
                int offset = row * 3;
                rows.Add(new Dictionary<string, JsonElement>
                {
                    ["Row"] = AlgorithmJson.ToElement(row + 1),
                    ["InputM1"] = AlgorithmJson.ToElement(input[offset]),
                    ["InputM2"] = AlgorithmJson.ToElement(input[offset + 1]),
                    ["InputM3"] = AlgorithmJson.ToElement(input[offset + 2]),
                    ["OutputM1"] = AlgorithmJson.ToElement(output[offset]),
                    ["OutputM2"] = AlgorithmJson.ToElement(output[offset + 1]),
                    ["OutputM3"] = AlgorithmJson.ToElement(output[offset + 2]),
                });
            }
            return new AlgorithmTableArtifact("lens-distortion-camera-matrices",
            [
                new("Row", "integer"), new("InputM1", "number"), new("InputM2", "number"), new("InputM3", "number"),
                new("OutputM1", "number"), new("OutputM2", "number"), new("OutputM3", "number"),
            ], rows);
        }

        private static AlgorithmTableArtifact BuildCoefficientTable(double[] coefficients)
        {
            string[] names = ["K1", "K2", "P1", "P2", "K3", "K4", "K5", "K6"];
            IReadOnlyDictionary<string, JsonElement>[] rows = names.Select((name, index) =>
                (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
                {
                    ["Name"] = AlgorithmJson.ToElement(name),
                    ["Value"] = AlgorithmJson.ToElement(coefficients[index]),
                }).ToArray();
            return new AlgorithmTableArtifact("lens-distortion-coefficients",
                [new("Name", "string"), new("Value", "number")], rows);
        }

        private static object RectData(Rect rect) => new { rect.X, rect.Y, rect.Width, rect.Height };

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        private readonly record struct MapStatistics(
            long ValidPixels,
            double MeanDisplacementPixels,
            double MaximumDisplacementPixels,
            Rect ValidBounds);
    }
}
