using ColorVision.Core;
using ColorVision.ImageEditor.Realtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal readonly record struct VideoCrossGuideRequest(
        RoiRect Roi,
        Point StandardCenter,
        int Transform,
        int IntervalMs,
        double ThresholdRatio,
        double MinCoverageRatio,
        double TolerancePx);

    internal readonly record struct VideoCrossGuideResult(
        bool Found,
        int FrameWidth,
        int FrameHeight,
        Rect Roi,
        Point StandardCenter,
        Point CrossCenter,
        double OffsetX,
        double OffsetY,
        double Distance,
        double RowCoverage,
        double ColumnCoverage,
        double Threshold,
        double TolerancePx,
        double RotationZDeg,
        double XRotationDeg,
        double YRotationDeg,
        bool XAxisFound,
        Point XAxisStart,
        Point XAxisEnd,
        bool YAxisFound,
        Point YAxisStart,
        Point YAxisEnd,
        string Message)
    {
        public bool IsPass => Found && Math.Abs(OffsetX) <= TolerancePx && Math.Abs(OffsetY) <= TolerancePx;
    }

    internal sealed class VideoCrossGuideProcessor : IDisposable
    {
        private readonly object _gate = new();
        private readonly AutoResetEvent _frameReady = new(false);
        private readonly CancellationTokenSource _cts = new();
        private readonly Action<VideoCrossGuideResult> _resultHandler;
        private readonly Task _workerTask;

        private HImage? _pendingFrame;
        private HImage? _workingFrame;
        private int _pendingCapacity;
        private int _workingCapacity;
        private VideoCrossGuideRequest _pendingRequest;
        private RoiRect _pendingSourceRoi;
        private RoiRect _workingSourceRoi;
        private int _pendingFrameWidth;
        private int _pendingFrameHeight;
        private int _workingFrameWidth;
        private int _workingFrameHeight;
        private bool _hasPendingFrame;
        private bool _disposed;
        private long _lastSubmittedTick;

        public VideoCrossGuideProcessor(Action<VideoCrossGuideResult> resultHandler)
        {
            ArgumentNullException.ThrowIfNull(resultHandler);
            _resultHandler = resultHandler;
            _workerTask = Task.Factory.StartNew(WorkerLoop, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void SubmitFrame(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, VideoCrossGuideRequest request)
        {
            if (sourcePointer == IntPtr.Zero || length <= 0 || _disposed) return;
            if (width <= 0 || height <= 0 || channels <= 0 || depth <= 0 || depth % 8 != 0) return;

            int interval = Math.Max(50, request.IntervalMs);
            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastSubmittedTick);
            if (now - last < interval) return;
            Interlocked.Exchange(ref _lastSubmittedTick, now);

            RoiRect sourceRoi = VideoCrossGuideDetector.NormalizeRoi(request.Roi, width, height);
            if (sourceRoi.Width <= 0 || sourceRoi.Height <= 0) return;

            SubmitFrameCore(sourcePointer, length, width, height, channels, depth, stride, sourceRoi, request);
        }

        private unsafe void SubmitFrameCore(IntPtr sourcePointer, int length, int width, int height, int channels, int depth, int stride, RoiRect sourceRoi, VideoCrossGuideRequest request)
        {
            int bytesPerSample = depth / 8;
            int pixelBytes = channels * bytesPerSample;
            int rowBytes = sourceRoi.Width * pixelBytes;
            int requiredLength = rowBytes * sourceRoi.Height;
            int sourceRowBytesNeeded = (sourceRoi.X + sourceRoi.Width) * pixelBytes;
            if (rowBytes <= 0 || requiredLength <= 0 || stride < sourceRowBytesNeeded || length < stride * height) return;

            lock (_gate)
            {
                EnsureBuffer(ref _pendingFrame, ref _pendingCapacity, sourceRoi.Width, sourceRoi.Height, channels, depth, rowBytes, requiredLength);
                byte* sourceBase = (byte*)sourcePointer;
                byte* targetBase = (byte*)_pendingFrame!.Value.pData;
                for (int y = 0; y < sourceRoi.Height; y++)
                {
                    byte* sourceRow = sourceBase + (sourceRoi.Y + y) * stride + sourceRoi.X * pixelBytes;
                    byte* targetRow = targetBase + y * rowBytes;
                    Buffer.MemoryCopy(sourceRow, targetRow, rowBytes, rowBytes);
                }

                _pendingRequest = request;
                _pendingSourceRoi = sourceRoi;
                _pendingFrameWidth = width;
                _pendingFrameHeight = height;
                _hasPendingFrame = true;
            }

            _frameReady.Set();
        }

        private void WorkerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                _frameReady.WaitOne(100);
                if (_cts.IsCancellationRequested) break;

                HImage workingFrame;
                VideoCrossGuideRequest request;
                RoiRect sourceRoi;
                int frameWidth;
                int frameHeight;

                lock (_gate)
                {
                    if (!_hasPendingFrame || _pendingFrame == null) continue;

                    (_pendingFrame, _workingFrame) = (_workingFrame, _pendingFrame);
                    (_pendingCapacity, _workingCapacity) = (_workingCapacity, _pendingCapacity);
                    (_pendingSourceRoi, _workingSourceRoi) = (_workingSourceRoi, _pendingSourceRoi);
                    (_pendingFrameWidth, _workingFrameWidth) = (_workingFrameWidth, _pendingFrameWidth);
                    (_pendingFrameHeight, _workingFrameHeight) = (_workingFrameHeight, _pendingFrameHeight);
                    request = _pendingRequest;
                    _pendingRequest = default;
                    _hasPendingFrame = false;
                    workingFrame = _workingFrame!.Value;
                    sourceRoi = _workingSourceRoi;
                    frameWidth = _workingFrameWidth;
                    frameHeight = _workingFrameHeight;
                }

                VideoCrossGuideResult result = VideoCrossGuideDetector.ProcessFrame(workingFrame, request, frameWidth, frameHeight, sourceRoi);

                try
                {
                    if (!_cts.IsCancellationRequested)
                        _resultHandler(result);
                }
                catch
                {
                }
            }
        }

        private static void EnsureBuffer(ref HImage? buffer, ref int capacity, int width, int height, int channels, int depth, int stride, int requiredLength)
        {
            bool needsAllocation = buffer == null
                || capacity < requiredLength
                || buffer.Value.cols != width
                || buffer.Value.rows != height
                || buffer.Value.channels != channels
                || buffer.Value.depth != depth
                || buffer.Value.stride != stride;

            if (needsAllocation)
            {
                buffer?.Dispose();
                buffer = new HImage
                {
                    rows = height,
                    cols = width,
                    channels = channels,
                    depth = depth,
                    stride = stride,
                    pData = Marshal.AllocCoTaskMem(requiredLength)
                };
                capacity = requiredLength;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _hasPendingFrame = false;
                _pendingRequest = default;
            }

            Interlocked.Exchange(ref _lastSubmittedTick, 0);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _cts.Cancel();
            _frameReady.Set();

            try
            {
                _workerTask.Wait();
            }
            catch (AggregateException)
            {
            }

            _pendingFrame?.Dispose();
            _workingFrame?.Dispose();
            _frameReady.Dispose();
            _cts.Dispose();
        }
    }

    internal static class VideoCrossGuideDetector
    {
        private const int MaxAngleSamples = 30000;

        private readonly record struct BrightPixel(double X, double Y, double Weight);

        private readonly record struct AngleEstimate(bool Found, double AngleDeg, double Score, double StartProjection, double EndProjection);

        private readonly record struct AxisSegment(bool Found, Point Start, Point End);

        public static VideoCrossGuideResult ProcessFrame(HImage frame, VideoCrossGuideRequest request, int frameWidth, int frameHeight, RoiRect sourceRoi)
        {
            Rect displayRoi = TransformRoiToDisplay(sourceRoi, frameWidth, frameHeight, request.Transform);

            if (frame.pData == IntPtr.Zero || frameWidth <= 0 || frameHeight <= 0 || frame.cols <= 0 || frame.rows <= 0 || frame.channels <= 0 || frame.depth <= 0)
                return NotFound(frameWidth, frameHeight, request, displayRoi, Properties.Resources.VideoCrossGuide_NoValidFrame);

            if (sourceRoi.Width <= 2 || sourceRoi.Height <= 2)
                return NotFound(frameWidth, frameHeight, request, displayRoi, Properties.Resources.VideoCrossGuide_InvalidRoi);

            if (frame.depth != 8 && frame.depth != 16)
                return NotFound(frameWidth, frameHeight, request, displayRoi, string.Format(Properties.Resources.VideoCrossGuide_UnsupportedBitDepth, frame.depth));

            double thresholdRatio = Clamp(request.ThresholdRatio, 0.05, 0.95);
            double minCoverageRatio = Clamp(request.MinCoverageRatio, 0.01, 0.95);

            RoiRect localRoi = new(0, 0, frame.cols, frame.rows);
            double maxGray = FindMaxGray(frame, localRoi);
            if (maxGray < 8)
                return NotFound(frameWidth, frameHeight, request, displayRoi, Properties.Resources.VideoCrossGuide_CrossNotFoundLowBrightness);

            double threshold = Math.Max(4, maxGray * thresholdRatio);
            double[] rowSum = new double[localRoi.Height];
            double[] columnSum = new double[localRoi.Width];
            int[] rowHits = new int[localRoi.Height];
            int[] columnHits = new int[localRoi.Width];
            List<BrightPixel> brightPixels = new(Math.Min(MaxAngleSamples, Math.Max(0, localRoi.Width * localRoi.Height / 64)));

            Accumulate(frame, localRoi, threshold, rowSum, columnSum, rowHits, columnHits, brightPixels);

            int rowPeak = IndexOfMax(rowSum);
            int columnPeak = IndexOfMax(columnSum);
            if (rowPeak < 0 || columnPeak < 0 || rowSum[rowPeak] <= 0 || columnSum[columnPeak] <= 0)
                return NotFound(frameWidth, frameHeight, request, displayRoi, Properties.Resources.VideoCrossGuide_BrightCrossLinesNotFound);

            double rowCoverage = (double)rowHits[rowPeak] / localRoi.Width;
            double columnCoverage = (double)columnHits[columnPeak] / localRoi.Height;

            double centerY = sourceRoi.Y + WeightedPeakCenter(rowSum, rowPeak);
            double centerX = sourceRoi.X + WeightedPeakCenter(columnSum, columnPeak);
            double localCenterX = centerX - sourceRoi.X;
            double localCenterY = centerY - sourceRoi.Y;
            AngleEstimate horizontalAngle = EstimateLineAngle(brightPixels, localCenterX, localCenterY, 0);
            AngleEstimate verticalAngle = EstimateLineAngle(brightPixels, localCenterX, localCenterY, 90);
            if (!horizontalAngle.Found && !verticalAngle.Found && rowCoverage < minCoverageRatio && columnCoverage < minCoverageRatio)
                return NotFound(frameWidth, frameHeight, request, displayRoi, string.Format(Properties.Resources.VideoCrossGuide_InsufficientCoverage, rowCoverage, columnCoverage));

            double rotationZDeg = ResolveRotationZ(horizontalAngle, verticalAngle);
            double displayRotationZDeg = TransformAngleToDisplay(rotationZDeg, request.Transform);
            double displayXRotationDeg = TransformAngleToDisplay(horizontalAngle.Found ? NormalizeLineAngle(horizontalAngle.AngleDeg) : 0, request.Transform);
            double displayYRotationDeg = TransformAngleToDisplay(verticalAngle.Found ? NormalizeLineAngle(verticalAngle.AngleDeg - 90) : 0, request.Transform);
            AxisSegment xAxisSegment = CreateDisplayAxisSegment(horizontalAngle, localCenterX, localCenterY, sourceRoi, frameWidth, frameHeight, request.Transform);
            AxisSegment yAxisSegment = CreateDisplayAxisSegment(verticalAngle, localCenterX, localCenterY, sourceRoi, frameWidth, frameHeight, request.Transform);
            Point displayCenter = TransformPointToDisplay(new Point(centerX, centerY), frameWidth, frameHeight, request.Transform);
            Point standardCenter = ResolveStandardCenter(request.StandardCenter, frameWidth, frameHeight);

            double dx = displayCenter.X - standardCenter.X;
            double dy = displayCenter.Y - standardCenter.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            return new VideoCrossGuideResult(
                true,
                frameWidth,
                frameHeight,
                displayRoi,
                standardCenter,
                displayCenter,
                dx,
                dy,
                distance,
                rowCoverage,
                columnCoverage,
                threshold,
                request.TolerancePx,
                displayRotationZDeg,
                displayXRotationDeg,
                displayYRotationDeg,
                xAxisSegment.Found,
                xAxisSegment.Start,
                xAxisSegment.End,
                yAxisSegment.Found,
                yAxisSegment.Start,
                yAxisSegment.End,
                string.Empty);
        }

        private static VideoCrossGuideResult NotFound(int frameWidth, int frameHeight, VideoCrossGuideRequest request, Rect displayRoi, string message)
        {
            Point standardCenter = ResolveStandardCenter(request.StandardCenter, frameWidth, frameHeight);
            return new VideoCrossGuideResult(
                false,
                Math.Max(0, frameWidth),
                Math.Max(0, frameHeight),
                displayRoi,
                standardCenter,
                new Point(),
                0,
                0,
                0,
                0,
                0,
                0,
                request.TolerancePx,
                0,
                0,
                0,
                false,
                new Point(),
                new Point(),
                false,
                new Point(),
                new Point(),
                message);
        }

        public static RoiRect NormalizeRoi(RoiRect roi, int width, int height)
        {
            if (width <= 0 || height <= 0) return new RoiRect();

            if (roi.Width <= 0 || roi.Height <= 0)
                return new RoiRect(0, 0, width, height);

            int x = Math.Clamp(roi.X, 0, width - 1);
            int y = Math.Clamp(roi.Y, 0, height - 1);
            int right = Math.Clamp(roi.X + roi.Width, x + 1, width);
            int bottom = Math.Clamp(roi.Y + roi.Height, y + 1, height);
            return new RoiRect(x, y, right - x, bottom - y);
        }

        private static Point ResolveStandardCenter(Point standardCenter, int width, int height)
        {
            double x = standardCenter.X > 0 ? standardCenter.X : width / 2.0;
            double y = standardCenter.Y > 0 ? standardCenter.Y : height / 2.0;
            return new Point(x, y);
        }

        private static Rect TransformRoiToDisplay(RoiRect sourceRoi, int width, int height, int transform)
        {
            Rect rect = new(sourceRoi.X, sourceRoi.Y, sourceRoi.Width, sourceRoi.Height);
            bool flipVertical = (transform & RealtimeFramePresenter.TransformFlipX) != 0;
            bool flipHorizontal = (transform & RealtimeFramePresenter.TransformFlipY) != 0;

            double x = flipHorizontal ? width - rect.Right : rect.X;
            double y = flipVertical ? height - rect.Bottom : rect.Y;
            return new Rect(x, y, rect.Width, rect.Height);
        }

        public static RoiRect TransformDisplayRoiToSource(Rect displayRoi, int width, int height, int transform)
        {
            if (displayRoi.Width <= 0 || displayRoi.Height <= 0)
                return new RoiRect(0, 0, 0, 0);

            Rect rect = ClampRect(displayRoi, width, height);
            bool flipVertical = (transform & RealtimeFramePresenter.TransformFlipX) != 0;
            bool flipHorizontal = (transform & RealtimeFramePresenter.TransformFlipY) != 0;

            double x = flipHorizontal ? width - rect.Right : rect.X;
            double y = flipVertical ? height - rect.Bottom : rect.Y;
            return new RoiRect((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
        }

        private static Point TransformPointToDisplay(Point sourcePoint, int width, int height, int transform)
        {
            double x = (transform & RealtimeFramePresenter.TransformFlipY) != 0 ? width - 1 - sourcePoint.X : sourcePoint.X;
            double y = (transform & RealtimeFramePresenter.TransformFlipX) != 0 ? height - 1 - sourcePoint.Y : sourcePoint.Y;
            return new Point(x, y);
        }

        private static Rect ClampRect(Rect rect, int width, int height)
        {
            double x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
            double y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
            double right = Math.Clamp(rect.Right, x + 1, width);
            double bottom = Math.Clamp(rect.Bottom, y + 1, height);
            return new Rect(x, y, right - x, bottom - y);
        }

        private static unsafe double FindMaxGray(HImage frame, RoiRect roi)
        {
            double max = 0;
            for (int y = roi.Y; y < roi.Y + roi.Height; y++)
            {
                byte* row = (byte*)frame.pData + y * frame.stride;
                for (int x = roi.X; x < roi.X + roi.Width; x++)
                {
                    double gray = ReadGray(row, x, frame.channels, frame.depth);
                    if (gray > max) max = gray;
                }
            }

            return max;
        }

        private static unsafe void Accumulate(HImage frame, RoiRect roi, double threshold, double[] rowSum, double[] columnSum, int[] rowHits, int[] columnHits, List<BrightPixel> brightPixels)
        {
            for (int y = roi.Y; y < roi.Y + roi.Height; y++)
            {
                int rowIndex = y - roi.Y;
                byte* row = (byte*)frame.pData + y * frame.stride;
                for (int x = roi.X; x < roi.X + roi.Width; x++)
                {
                    double gray = ReadGray(row, x, frame.channels, frame.depth);
                    if (gray < threshold) continue;

                    int columnIndex = x - roi.X;
                    rowSum[rowIndex] += gray;
                    columnSum[columnIndex] += gray;
                    rowHits[rowIndex]++;
                    columnHits[columnIndex]++;
                    if (brightPixels.Count < MaxAngleSamples)
                        brightPixels.Add(new BrightPixel(columnIndex, rowIndex, gray));
                }
            }
        }

        private static unsafe double ReadGray(byte* row, int x, int channels, int depth)
        {
            int bytesPerSample = depth / 8;
            byte* pixel = row + x * channels * bytesPerSample;

            if (depth == 8)
            {
                if (channels == 1) return pixel[0];
                double b = pixel[0];
                double g = channels > 1 ? pixel[1] : b;
                double r = channels > 2 ? pixel[2] : g;
                return (0.114 * b) + (0.587 * g) + (0.299 * r);
            }

            ushort* p16 = (ushort*)pixel;
            if (channels == 1) return p16[0] / 257.0;
            double b16 = p16[0] / 257.0;
            double g16 = channels > 1 ? p16[1] / 257.0 : b16;
            double r16 = channels > 2 ? p16[2] / 257.0 : g16;
            return (0.114 * b16) + (0.587 * g16) + (0.299 * r16);
        }

        private static int IndexOfMax(double[] values)
        {
            if (values.Length == 0) return -1;

            int index = 0;
            double max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] <= max) continue;

                index = i;
                max = values[i];
            }

            return index;
        }

        private static double WeightedPeakCenter(double[] values, int peakIndex)
        {
            double peak = values[peakIndex];
            double minValue = peak * 0.5;
            int left = peakIndex;
            int right = peakIndex;

            while (left > 0 && values[left - 1] >= minValue) left--;
            while (right < values.Length - 1 && values[right + 1] >= minValue) right++;

            double weighted = 0;
            double sum = 0;
            for (int i = left; i <= right; i++)
            {
                weighted += i * values[i];
                sum += values[i];
            }

            return sum <= 0 ? peakIndex : weighted / sum;
        }

        private static AngleEstimate EstimateLineAngle(IReadOnlyList<BrightPixel> pixels, double centerX, double centerY, double targetAngleDeg)
        {
            if (pixels.Count < 4) return new AngleEstimate(false, 0, 0, 0, 0);

            double bestAngle = targetAngleDeg;
            double bestScore = double.MinValue;
            const double searchHalfRangeDeg = 15;
            const double stepDeg = 0.25;
            const double sigma = 4.0;
            const double maxDistance = sigma * 3;

            for (double angleDeg = targetAngleDeg - searchHalfRangeDeg; angleDeg <= targetAngleDeg + searchHalfRangeDeg + 0.0001; angleDeg += stepDeg)
            {
                double angleRad = angleDeg * Math.PI / 180.0;
                double sin = Math.Sin(angleRad);
                double cos = Math.Cos(angleRad);
                double score = 0;

                foreach (BrightPixel pixel in pixels)
                {
                    double dx = pixel.X - centerX;
                    double dy = pixel.Y - centerY;
                    double distance = Math.Abs((-sin * dx) + (cos * dy));
                    if (distance > maxDistance) continue;

                    double normalized = distance / sigma;
                    score += pixel.Weight * Math.Exp(-0.5 * normalized * normalized);
                }

                if (score <= bestScore) continue;

                bestScore = score;
                bestAngle = angleDeg;
            }

            if (bestScore <= 0) return new AngleEstimate(false, 0, 0, 0, 0);

            return BuildAngleEstimate(pixels, centerX, centerY, bestAngle, bestScore, sigma);
        }

        private static AngleEstimate BuildAngleEstimate(IReadOnlyList<BrightPixel> pixels, double centerX, double centerY, double angleDeg, double score, double sigma)
        {
            double angleRad = angleDeg * Math.PI / 180.0;
            double sin = Math.Sin(angleRad);
            double cos = Math.Cos(angleRad);
            double maxDistance = sigma * 3;
            double startProjection = double.MaxValue;
            double endProjection = double.MinValue;
            int supportCount = 0;

            foreach (BrightPixel pixel in pixels)
            {
                double dx = pixel.X - centerX;
                double dy = pixel.Y - centerY;
                double distance = Math.Abs((-sin * dx) + (cos * dy));
                if (distance > maxDistance) continue;

                double projection = (cos * dx) + (sin * dy);
                startProjection = Math.Min(startProjection, projection);
                endProjection = Math.Max(endProjection, projection);
                supportCount++;
            }

            if (supportCount < 4 || endProjection - startProjection < 2)
                return new AngleEstimate(false, 0, 0, 0, 0);

            return new AngleEstimate(true, angleDeg, score, startProjection, endProjection);
        }

        private static double ResolveRotationZ(AngleEstimate horizontalAngle, AngleEstimate verticalAngle)
        {
            double weightedAngle = 0;
            double totalWeight = 0;

            if (horizontalAngle.Found)
            {
                weightedAngle += NormalizeLineAngle(horizontalAngle.AngleDeg) * horizontalAngle.Score;
                totalWeight += horizontalAngle.Score;
            }

            if (verticalAngle.Found)
            {
                weightedAngle += NormalizeLineAngle(verticalAngle.AngleDeg - 90) * verticalAngle.Score;
                totalWeight += verticalAngle.Score;
            }

            return totalWeight <= 0 ? 0 : weightedAngle / totalWeight;
        }

        private static AxisSegment CreateDisplayAxisSegment(AngleEstimate estimate, double localCenterX, double localCenterY, RoiRect sourceRoi, int frameWidth, int frameHeight, int transform)
        {
            if (!estimate.Found) return new AxisSegment(false, new Point(), new Point());

            double angleRad = estimate.AngleDeg * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);
            Point sourceStart = new(sourceRoi.X + localCenterX + cos * estimate.StartProjection, sourceRoi.Y + localCenterY + sin * estimate.StartProjection);
            Point sourceEnd = new(sourceRoi.X + localCenterX + cos * estimate.EndProjection, sourceRoi.Y + localCenterY + sin * estimate.EndProjection);
            return new AxisSegment(
                true,
                TransformPointToDisplay(sourceStart, frameWidth, frameHeight, transform),
                TransformPointToDisplay(sourceEnd, frameWidth, frameHeight, transform));
        }

        private static double TransformAngleToDisplay(double angleDeg, int transform)
        {
            bool flipVertical = (transform & RealtimeFramePresenter.TransformFlipX) != 0;
            bool flipHorizontal = (transform & RealtimeFramePresenter.TransformFlipY) != 0;
            return flipVertical ^ flipHorizontal ? -angleDeg : angleDeg;
        }

        private static double NormalizeLineAngle(double angleDeg)
        {
            while (angleDeg > 90) angleDeg -= 180;
            while (angleDeg <= -90) angleDeg += 180;
            return angleDeg;
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return min;
            return Math.Min(max, Math.Max(min, value));
        }
    }
}
