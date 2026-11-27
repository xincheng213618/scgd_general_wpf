using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic local affine/projective warp with explicit source-to-destination matrix semantics.</summary>
    public sealed class GeometricTransformAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.geometry.transform/v1";
        private const double MatrixTolerance = 1e-12;
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.geometric-transform.cpu",
            "ColorVision Geometric Transform CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            125,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.GeometricTransform, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.GeometricTransform
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer source = context.Inputs[0].Image;
            GeometricTransformParameters parameters = (GeometricTransformParameters)context.Parameters;
            double[] requested = parameters.Matrix;
            if (!TryInvert(requested, out double[] requestedInverse, out double requestedDeterminant))
                return ValueTask.FromResult(Failure(context, "transform_singular", "The requested transform matrix is singular.", "parameters.Matrix"));
            double requestedCondition = ConditionNumber(requested, requestedInverse);
            if (!double.IsFinite(requestedCondition) || requestedCondition > parameters.MaximumConditionNumber)
            {
                return ValueTask.FromResult(Failure(context, "transform_ill_conditioned",
                    $"The requested transform condition number {requestedCondition.ToString("G10", CultureInfo.InvariantCulture)} exceeds the configured limit.",
                    "parameters.MaximumConditionNumber"));
            }
            if (CrossesProjectiveHorizon(requested, source.Width, source.Height))
                return ValueTask.FromResult(Failure(context, "transform_crosses_projective_horizon", "The projective denominator crosses zero within the source image.", "parameters.Matrix"));

            if (!TryResolveCanvas(source, parameters, requested, out int width, out int height, out double[] effective, out string? canvasFailure))
                return ValueTask.FromResult(Failure(context, canvasFailure ?? "transform_canvas_invalid", "The requested output canvas is invalid.", "parameters.Canvas"));
            long outputPixels = (long)width * height;
            if (outputPixels > parameters.MaximumOutputPixels)
            {
                return ValueTask.FromResult(Failure(context, "transform_output_limit_exceeded",
                    $"The output contains {outputPixels.ToString(CultureInfo.InvariantCulture)} pixels; the configured limit is {parameters.MaximumOutputPixels.ToString(CultureInfo.InvariantCulture)}.",
                    "parameters.MaximumOutputPixels"));
            }
            if (!TryInvert(effective, out double[] inverse, out double determinant))
                return ValueTask.FromResult(Failure(context, "effective_transform_singular", "The effective canvas-adjusted transform is singular.", "parameters.Matrix"));
            double condition = ConditionNumber(effective, inverse);
            if (!double.IsFinite(condition) || condition > parameters.MaximumConditionNumber)
                return ValueTask.FromResult(Failure(context, "effective_transform_ill_conditioned", "The canvas-adjusted transform exceeds the configured condition-number limit.", "parameters.Canvas"));

            context.Progress?.Report(new AlgorithmProgress(0.05, "transform.prepare"));
            AlgorithmImageBuffer? transformed = null;
            AlgorithmImageBuffer? validityMask = null;
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                using AlgorithmImageMatLease input = AlgorithmImageInterop.BorrowReadOnly(source);
                int stride = checked(width * source.Format.BytesPerPixel());
                byte[] outputData = GC.AllocateUninitializedArray<byte>(checked(stride * height));
                using Mat output = Mat.FromPixelData(height, width, input.Mat.Type(), outputData, stride);
                IntPtr outputAddress = output.Data;
                using Mat matrix = CreateMatrix(effective, parameters.Kind);
                InterpolationFlags interpolation = parameters.Interpolation == GeometricTransformInterpolation.Nearest
                    ? InterpolationFlags.Nearest
                    : InterpolationFlags.Linear;
                BorderTypes border = parameters.Border == GeometricTransformBorder.Replicate
                    ? BorderTypes.Replicate
                    : BorderTypes.Constant;
                Scalar borderValue = BorderScalar(source.Format, parameters);
                cancellationToken.ThrowIfCancellationRequested();
                if (parameters.Kind == GeometricTransformKind.Affine)
                    Cv2.WarpAffine(input.Mat, output, matrix, new Size(width, height), interpolation, border, borderValue);
                else
                    Cv2.WarpPerspective(input.Mat, output, matrix, new Size(width, height), interpolation, border, borderValue);
                cancellationToken.ThrowIfCancellationRequested();
                context.Progress?.Report(new AlgorithmProgress(0.78, "transform.mask"));
                (byte[] mask, long validPixels) = BuildValidityMask(width, height, source.Width, source.Height, inverse, cancellationToken, context.Progress);
                // Constant and replicate borders fill the destination. Honor a native reallocation if it occurs.
                transformed = output.Data == outputAddress && output.Step() == stride
                    ? new AlgorithmImageBuffer(width, height, stride, source.Format, outputData, source.DpiX, source.DpiY)
                    : AlgorithmImageInterop.FromMat(output, source.DpiX, source.DpiY, cancellationToken);
                validityMask = new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, mask, source.DpiX, source.DpiY);
                artifacts.Add(new AlgorithmImageArtifact("transformed-image", "primary", transformed,
                    new Dictionary<string, string>
                    {
                        ["matrixSemantics"] = "source-pixel-center-to-destination-pixel-center",
                        ["presetId"] = context.Invocation.PresetId ?? string.Empty,
                    }));
                transformed = null;
                artifacts.Add(new AlgorithmImageArtifact("valid-region-mask", "validity-mask", validityMask,
                    new Dictionary<string, string> { ["valid"] = "255", ["invalid"] = "0" }));
                validityMask = null;
                double inverseResidual = InverseResidual(effective, inverse);
                AlgorithmPoint[] footprint = SourceFootprint(effective, source.Width, source.Height);
                artifacts.Add(BuildMeasurements(width, height, validPixels, outputPixels, requestedDeterminant, determinant, condition, inverseResidual));
                artifacts.Add(BuildMatrixTable(effective, inverse));
                artifacts.Add(new AlgorithmGeometryArtifact("geometric-transform", AlgorithmCoordinateSpace.Pixel,
                [
                    new AlgorithmGeometry("source-to-destination", AlgorithmGeometryKind.Transform, [], Matrix: effective, Residual: inverseResidual,
                        Confidence: 1 / (1 + Math.Log10(Math.Max(1, condition)))),
                    new AlgorithmGeometry("transformed-source-footprint", AlgorithmGeometryKind.Polygon, footprint),
                ]));
                artifacts.Add(new AlgorithmStructuredDataArtifact("geometric-transform", ResultSchema, AlgorithmJson.ToElement(new
                {
                    kind = parameters.Kind.ToString(),
                    canvas = parameters.Canvas.ToString(),
                    interpolation = parameters.Interpolation.ToString(),
                    border = parameters.Border.ToString(),
                    matrixSemantics = "source-pixel-center-to-destination-pixel-center",
                    requestedMatrix = requested,
                    effectiveMatrix = effective,
                    inverseMatrix = inverse,
                    source = new { source.Width, source.Height, format = source.Format.ToString(), source.DpiX, source.DpiY },
                    output = new { width, height, validPixels, invalidPixels = outputPixels - validPixels, validFraction = validPixels / (double)outputPixels },
                    validityMask = new { valid = 255, invalid = 0, rule = "inverse-mapped output pixel center lies inside the closed source pixel-center bounds" },
                    determinant,
                    conditionNumber = condition,
                    inverseResidual,
                    presetId = context.Invocation.PresetId,
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "transform.complete"));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = new AlgorithmExecutionDiagnostics
                    {
                        Messages = validPixels == outputPixels
                            ? []
                            : [new AlgorithmDiagnosticMessage("transform_contains_invalid_output", $"{outputPixels - validPixels} output pixel centers map outside the source image.", "warning")],
                    },
                });
            }
            catch
            {
                transformed?.Dispose();
                validityMask?.Dispose();
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }
        }

        private static bool TryResolveCanvas(
            AlgorithmImageBuffer source,
            GeometricTransformParameters parameters,
            double[] requested,
            out int width,
            out int height,
            out double[] effective,
            out string? failure)
        {
            width = source.Width;
            height = source.Height;
            effective = (double[])requested.Clone();
            failure = null;
            if (parameters.Canvas == GeometricTransformCanvas.ExplicitSize)
            {
                width = parameters.OutputWidth;
                height = parameters.OutputHeight;
                return width > 0 && height > 0;
            }
            if (parameters.Canvas == GeometricTransformCanvas.SourceSize) return true;

            AlgorithmPoint[] corners = SourceFootprint(requested, source.Width, source.Height);
            if (corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
            {
                failure = "transform_bounds_nonfinite";
                return false;
            }
            double minimumX = Math.Floor(corners.Min(point => point.X)) - parameters.FitPaddingPixels;
            double minimumY = Math.Floor(corners.Min(point => point.Y)) - parameters.FitPaddingPixels;
            double maximumX = Math.Ceiling(corners.Max(point => point.X)) + parameters.FitPaddingPixels;
            double maximumY = Math.Ceiling(corners.Max(point => point.Y)) + parameters.FitPaddingPixels;
            double widthValue = maximumX - minimumX + 1;
            double heightValue = maximumY - minimumY + 1;
            if (!double.IsFinite(widthValue) || !double.IsFinite(heightValue)
                || widthValue < 1 || heightValue < 1 || widthValue > int.MaxValue || heightValue > int.MaxValue)
            {
                failure = "transform_bounds_exceed_integer_canvas";
                return false;
            }
            width = checked((int)widthValue);
            height = checked((int)heightValue);
            double[] translation = [1, 0, -minimumX, 0, 1, -minimumY, 0, 0, 1];
            effective = Multiply(translation, requested);
            return true;
        }

        internal static Mat CreateMatrix(double[] matrix, GeometricTransformKind kind)
        {
            int rows = kind == GeometricTransformKind.Affine ? 2 : 3;
            Mat result = new(rows, 3, MatType.CV_64FC1);
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < 3; column++)
                    result.Set(row, column, matrix[row * 3 + column]);
            return result;
        }

        private static Scalar BorderScalar(AlgorithmImageFormat format, GeometricTransformParameters parameters)
        {
            double peak = format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            return new Scalar(
                parameters.BorderChannel0 * peak,
                parameters.BorderChannel1 * peak,
                parameters.BorderChannel2 * peak,
                parameters.BorderChannel3 * peak);
        }

        internal static (byte[] Mask, long ValidPixels) BuildValidityMask(
            int width,
            int height,
            int sourceWidth,
            int sourceHeight,
            double[] inverse,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            byte[] mask = new byte[checked(width * height)];
            long valid = 0;
            const double tolerance = 1e-9;
            bool axesAligned = inverse[1] == 0 && inverse[3] == 0;
            bool axesSwapped = !axesAligned && inverse[0] == 0 && inverse[4] == 0;
            double constantDenominator = inverse[8];
            bool separable = (axesAligned || axesSwapped) && inverse[6] == 0 && inverse[7] == 0
                && inverse.All(double.IsFinite) && Math.Abs(constantDenominator) > DenominatorTolerance(inverse, 0, 0);
            int columnSourceLength = axesSwapped ? sourceHeight : sourceWidth;
            int rowSourceLength = axesSwapped ? sourceWidth : sourceHeight;
            double columnScale = inverse[axesSwapped ? 3 : 0];
            double columnOffset = inverse[axesSwapped ? 5 : 2];
            double rowScale = inverse[axesSwapped ? 1 : 4];
            double rowOffset = inverse[axesSwapped ? 2 : 5];
            int vectorColumns = !separable && Vector256.IsHardwareAccelerated && width >= 128
                ? width - width % Vector256<double>.Count
                : 0;
            int workers = Math.Min(height / 16, Math.Min(Environment.ProcessorCount, 4));
            if (!separable && workers > 1 && mask.Length >= 2048 * 2048)
                return (mask, WriteProjectedMaskBands(mask, width, height, sourceWidth, sourceHeight,
                    inverse, tolerance, vectorColumns, workers, cancellationToken, progress));

            int firstColumn = 0;
            int lastColumn = 0;
            for (int y = 0; y < height; y++)
            {
                if ((y & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.78 + 0.18 * y / Math.Max(1, height), "transform.mask"));
                }
                int row = y * width;
                if (separable)
                {
                    // Each source coordinate is monotone in one destination axis. Find the
                    // contiguous column interval once using the same pixel-center comparisons.
                    if (y == 0)
                    {
                        while (firstColumn < width && !IsInsideAxis(firstColumn, columnScale, columnOffset, columnSourceLength))
                            firstColumn++;
                        lastColumn = firstColumn;
                        while (lastColumn < width && IsInsideAxis(lastColumn, columnScale, columnOffset, columnSourceLength))
                            lastColumn++;
                    }

                    if (IsInsideAxis(y, rowScale, rowOffset, rowSourceLength))
                    {
                        int length = lastColumn - firstColumn;
                        mask.AsSpan(row + firstColumn, length).Fill(byte.MaxValue);
                        valid += length;
                    }
                    continue;
                }
                valid += WriteProjectedMaskRow(mask.AsSpan(row, width), y, sourceWidth, sourceHeight, inverse, tolerance, vectorColumns);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return (mask, valid);

            bool IsInsideAxis(int coordinate, double scale, double offset, int length)
            {
                double mapped = (scale * coordinate + offset) / constantDenominator;
                return mapped >= -tolerance && mapped <= length - 1 + tolerance;
            }
        }

        private static long WriteProjectedMaskBands(byte[] mask, int width, int height, int sourceWidth, int sourceHeight,
            double[] inverse, double tolerance, int vectorColumns, int workers,
            CancellationToken cancellationToken, IProgress<AlgorithmProgress>? progress)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new AlgorithmProgress(0.78, "transform.mask"));
            int[] validByBand = new int[workers];
            Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = cancellationToken }, band =>
            {
                int start = (int)((long)height * band / workers);
                int end = (int)((long)height * (band + 1) / workers);
                int bandValid = 0;
                for (int y = start; y < end; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bandValid += WriteProjectedMaskRow(mask.AsSpan(y * width, width), y, sourceWidth, sourceHeight, inverse, tolerance, vectorColumns);
                }
                validByBand[band] = bandValid;
            });
            progress?.Report(new AlgorithmProgress(0.96, "transform.mask"));
            cancellationToken.ThrowIfCancellationRequested();
            return validByBand.Sum(count => (long)count);
        }

        private static int WriteProjectedMaskRow(Span<byte> row, int y, int sourceWidth, int sourceHeight,
            double[] inverse, double tolerance, int vectorColumns)
        {
            int valid = vectorColumns == 0 ? 0
                : WriteProjectedMaskColumns(row[..vectorColumns], y, sourceWidth, sourceHeight, inverse, tolerance);
            for (int x = vectorColumns; x < row.Length; x++)
            {
                double denominator = inverse[6] * x + inverse[7] * y + inverse[8];
                if (!double.IsFinite(denominator) || Math.Abs(denominator) <= DenominatorTolerance(inverse, x, y)) continue;
                double sourceX = (inverse[0] * x + inverse[1] * y + inverse[2]) / denominator;
                double sourceY = (inverse[3] * x + inverse[4] * y + inverse[5]) / denominator;
                if (sourceX < -tolerance || sourceY < -tolerance
                    || sourceX > sourceWidth - 1 + tolerance || sourceY > sourceHeight - 1 + tolerance) continue;
                row[x] = byte.MaxValue;
                valid++;
            }
            return valid;
        }

        private static int WriteProjectedMaskColumns(Span<byte> row, int y, int sourceWidth, int sourceHeight, double[] inverse, double tolerance)
        {
            double rowToSourceX = inverse[1] * y, rowToSourceY = inverse[4] * y, rowToDenominator = inverse[7] * y;
            double maximumX = sourceWidth - 1 + tolerance, maximumY = sourceHeight - 1 + tolerance;
            int count = 0;
            // Store a byte for each lane in the same order as the scalar scan.
            ReadOnlySpan<uint> maskBytes =
            [
                0x00000000, 0x000000ff, 0x0000ff00, 0x0000ffff,
                0x00ff0000, 0x00ff00ff, 0x00ffff00, 0x00ffffff,
                0xff000000, 0xff0000ff, 0xff00ff00, 0xff00ffff,
                0xffff0000, 0xffff00ff, 0xffffff00, 0xffffffff,
            ];
            Vector256<double> columns = Vector256.Create(0d, 1d, 2d, 3d);
            Vector256<double> step = Vector256.Create(4d);
            Vector256<double> xScale = Vector256.Create(inverse[0]), yScale = Vector256.Create(inverse[3]);
            Vector256<double> xRow = Vector256.Create(rowToSourceX), yRow = Vector256.Create(rowToSourceY);
            Vector256<double> xOffset = Vector256.Create(inverse[2]), yOffset = Vector256.Create(inverse[5]);
            Vector256<double> denominatorScale = Vector256.Create(inverse[6]);
            Vector256<double> denominatorRow = Vector256.Create(rowToDenominator), denominatorOffset = Vector256.Create(inverse[8]);
            Vector256<double> absoluteRow = Vector256.Create(Math.Abs(rowToDenominator)), absoluteOffset = Vector256.Create(Math.Abs(inverse[8]));
            Vector256<double> relativeTolerance = Vector256.Create(MatrixTolerance);
            Vector256<double> minimum = Vector256.Create(-tolerance);
            Vector256<double> upperX = Vector256.Create(maximumX), upperY = Vector256.Create(maximumY);
            Vector256<double> largest = Vector256.Create(double.MaxValue);
            for (int x = 0; x < row.Length; x += Vector256<double>.Count, columns += step)
            {
                // Preserve the operation order at the scalar tolerance boundaries.
                Vector256<double> columnDenominator = denominatorScale * columns;
                Vector256<double> denominator = (columnDenominator + denominatorRow) + denominatorOffset;
                Vector256<double> absolute = Vector256.Abs(denominator);
                Vector256<double> threshold = ((Vector256.Abs(columnDenominator) + absoluteRow) + absoluteOffset) * relativeTolerance;
                Vector256<double> validDenominator = Vector256.LessThanOrEqual(absolute, largest)
                    & ~Vector256.LessThanOrEqual(absolute, threshold);
                Vector256<double> sourceX = ((xScale * columns + xRow) + xOffset) / denominator;
                Vector256<double> sourceY = ((yScale * columns + yRow) + yOffset) / denominator;
                Vector256<double> outside = Vector256.LessThan(sourceX, minimum) | Vector256.LessThan(sourceY, minimum)
                    | Vector256.GreaterThan(sourceX, upperX) | Vector256.GreaterThan(sourceY, upperY);
                uint flags = (validDenominator & ~outside).ExtractMostSignificantBits();
                BinaryPrimitives.WriteUInt32LittleEndian(row[x..], maskBytes[(int)flags]);
                count += BitOperations.PopCount(flags);
            }
            return count;
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            int width,
            int height,
            long validPixels,
            long outputPixels,
            double requestedDeterminant,
            double effectiveDeterminant,
            double condition,
            double inverseResidual)
            => new("geometric-transform-summary",
            [
                new("transform.output_width", width, "px"),
                new("transform.output_height", height, "px"),
                new("transform.valid_pixel_count", validPixels, "px"),
                new("transform.invalid_pixel_count", outputPixels - validPixels, "px"),
                new("transform.valid_fraction", validPixels / (double)outputPixels, "ratio"),
                new("transform.requested_determinant", requestedDeterminant),
                new("transform.effective_determinant", effectiveDeterminant),
                new("transform.condition_number", condition),
                new("transform.inverse_residual", inverseResidual),
            ]);

        private static AlgorithmTableArtifact BuildMatrixTable(double[] matrix, double[] inverse)
        {
            List<IReadOnlyDictionary<string, JsonElement>> rows = new(3);
            for (int row = 0; row < 3; row++)
            {
                int offset = row * 3;
                rows.Add(new Dictionary<string, JsonElement>
                {
                    ["Row"] = AlgorithmJson.ToElement(row + 1),
                    ["M1"] = AlgorithmJson.ToElement(matrix[offset]),
                    ["M2"] = AlgorithmJson.ToElement(matrix[offset + 1]),
                    ["M3"] = AlgorithmJson.ToElement(matrix[offset + 2]),
                    ["InverseM1"] = AlgorithmJson.ToElement(inverse[offset]),
                    ["InverseM2"] = AlgorithmJson.ToElement(inverse[offset + 1]),
                    ["InverseM3"] = AlgorithmJson.ToElement(inverse[offset + 2]),
                });
            }
            return new AlgorithmTableArtifact("geometric-transform-matrix",
            [
                new("Row", "integer"), new("M1", "number"), new("M2", "number"), new("M3", "number"),
                new("InverseM1", "number"), new("InverseM2", "number"), new("InverseM3", "number"),
            ], rows);
        }

        internal static AlgorithmPoint[] SourceFootprint(double[] matrix, int width, int height)
            =>
            [
                Transform(matrix, 0, 0),
                Transform(matrix, width - 1, 0),
                Transform(matrix, width - 1, height - 1),
                Transform(matrix, 0, height - 1),
            ];

        internal static AlgorithmPoint Transform(double[] matrix, double x, double y)
        {
            double denominator = matrix[6] * x + matrix[7] * y + matrix[8];
            return Math.Abs(denominator) <= DenominatorTolerance(matrix, x, y)
                ? new AlgorithmPoint(double.NaN, double.NaN)
                : new AlgorithmPoint(
                    (matrix[0] * x + matrix[1] * y + matrix[2]) / denominator,
                    (matrix[3] * x + matrix[4] * y + matrix[5]) / denominator);
        }

        internal static bool CrossesProjectiveHorizon(double[] matrix, int width, int height)
        {
            double[] values =
            [
                matrix[8],
                matrix[6] * (width - 1) + matrix[8],
                matrix[7] * (height - 1) + matrix[8],
                matrix[6] * (width - 1) + matrix[7] * (height - 1) + matrix[8],
            ];
            double minimum = values.Min();
            double maximum = values.Max();
            double scale = values.Max(Math.Abs);
            double tolerance = MatrixTolerance * scale;
            return values.Any(value => Math.Abs(value) <= tolerance) || minimum < 0 && maximum > 0;
        }

        private static double DenominatorTolerance(double[] matrix, double x, double y)
            => MatrixTolerance * (Math.Abs(matrix[6] * x) + Math.Abs(matrix[7] * y) + Math.Abs(matrix[8]));

        internal static bool TryInvert(double[] matrix, out double[] inverse, out double determinant)
        {
            double scale = matrix.Max(value => Math.Abs(value));
            if (!double.IsFinite(scale) || scale <= 0)
            {
                inverse = [];
                determinant = 0;
                return false;
            }
            double[] normalized = matrix.Select(value => value / scale).ToArray();
            double normalizedDeterminant = normalized[0] * (normalized[4] * normalized[8] - normalized[5] * normalized[7])
                - normalized[1] * (normalized[3] * normalized[8] - normalized[5] * normalized[6])
                + normalized[2] * (normalized[3] * normalized[7] - normalized[4] * normalized[6]);
            determinant = normalizedDeterminant * scale * scale * scale;
            if (!double.IsFinite(normalizedDeterminant) || Math.Abs(normalizedDeterminant) <= 1e-18)
            {
                inverse = [];
                return false;
            }
            double reciprocal = 1 / normalizedDeterminant / scale;
            inverse =
            [
                (normalized[4] * normalized[8] - normalized[5] * normalized[7]) * reciprocal,
                (normalized[2] * normalized[7] - normalized[1] * normalized[8]) * reciprocal,
                (normalized[1] * normalized[5] - normalized[2] * normalized[4]) * reciprocal,
                (normalized[5] * normalized[6] - normalized[3] * normalized[8]) * reciprocal,
                (normalized[0] * normalized[8] - normalized[2] * normalized[6]) * reciprocal,
                (normalized[2] * normalized[3] - normalized[0] * normalized[5]) * reciprocal,
                (normalized[3] * normalized[7] - normalized[4] * normalized[6]) * reciprocal,
                (normalized[1] * normalized[6] - normalized[0] * normalized[7]) * reciprocal,
                (normalized[0] * normalized[4] - normalized[1] * normalized[3]) * reciprocal,
            ];
            return inverse.All(double.IsFinite);
        }

        private static double[] Multiply(double[] left, double[] right)
        {
            double[] result = new double[9];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    for (int index = 0; index < 3; index++)
                        result[row * 3 + column] += left[row * 3 + index] * right[index * 3 + column];
            return result;
        }

        internal static double ConditionNumber(double[] matrix, double[] inverse)
            => InfinityNorm(matrix) * InfinityNorm(inverse);

        private static double InfinityNorm(double[] matrix)
            => Enumerable.Range(0, 3).Max(row => Math.Abs(matrix[row * 3]) + Math.Abs(matrix[row * 3 + 1]) + Math.Abs(matrix[row * 3 + 2]));

        internal static double InverseResidual(double[] matrix, double[] inverse)
        {
            double[] product = Multiply(matrix, inverse);
            double maximum = 0;
            for (int index = 0; index < product.Length; index++)
            {
                double expected = index is 0 or 4 or 8 ? 1 : 0;
                maximum = Math.Max(maximum, Math.Abs(product[index] - expected));
            }
            return maximum;
        }

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };
    }
}
