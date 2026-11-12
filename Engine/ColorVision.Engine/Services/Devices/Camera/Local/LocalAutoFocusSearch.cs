using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed record LocalAutoFocusSettings(EvaFunc Evaluation, FindFuncModel Method, int Minimum, int Maximum,
    int InitialStep, double FineStep, double Reduction, int StopStep, double MinimumScore, int Timeout)
{
    internal bool FineOnly { get; init; }

    // 0 disables early stopping. Otherwise a full-range coarse scan ends once the peak is bracketed:
    // an earlier sample and EarlyStopCount consecutive later samples score below EarlyStopRatio × peak.
    internal double EarlyStopRatio { get; init; }
    internal int EarlyStopCount { get; init; } = 2;

    internal static LocalAutoFocusSettings FromTemplate(AutoFocusParam template, MotorConfig motor)
    {
        ArgumentNullException.ThrowIfNull(template);
        var settings = new LocalAutoFocusSettings(template.EvaFunc, motor.FindFuncModel, template.MinPosition, template.MaxPosition,
            template.CurStep, template.Forwardparam, template.Curtailparam, template.StopStep, template.MinValue, template.nTimeout)
        {
            EarlyStopRatio = motor.FocusEarlyStopRatio,
            EarlyStopCount = motor.FocusEarlyStopCount
        };
        settings.Validate();
        return settings;
    }

    internal void Validate()
    {
        if (!Enum.IsDefined(Evaluation) || !Enum.IsDefined(Method)) throw new ArgumentException("自动对焦评价算法或搜索方式无效。");
        if (Minimum >= Maximum)
            throw new ArgumentException("自动对焦范围下限必须小于上限。");
        if (StopStep < 1 || InitialStep <= StopStep || InitialStep > (long)Maximum - Minimum)
            throw new ArgumentException("自动对焦初始步长必须大于停止步长且不超过搜索范围，停止步长至少为 1。");
        if (!double.IsFinite(FineStep) || FineStep < 1 || FineStep > (long)Maximum - Minimum
            || !double.IsFinite(Reduction) || Reduction <= 0 || Reduction >= 1)
            throw new ArgumentException("自动对焦摆动步长须在搜索范围内，缩减系数必须大于 0 且小于 1。");
        if (!double.IsFinite(MinimumScore) || MinimumScore < 0 || Timeout <= 0)
            throw new ArgumentException("自动对焦最低评价值必须为非负有限值，超时必须大于 0。");
        if (!double.IsFinite(EarlyStopRatio) || EarlyStopRatio < 0 || EarlyStopRatio >= 1 || EarlyStopCount < 1)
            throw new ArgumentException("粗扫提前停止比例须在 0（关闭）到 1 之间，连续低点数至少为 1。");
    }

    internal int Remaining(long elapsedMilliseconds, CancellationToken token, string operation = "搜索")
    {
        token.ThrowIfCancellationRequested();
        long remaining = Timeout - elapsedMilliseconds;
        if (remaining <= 0)
            throw new TimeoutException($"本地自动对焦已达到模板总超时 {Timeout} ms（{operation}），已停止后续移动和取图。请增加自动对焦模板的超时或缩小搜索范围。");
        return (int)Math.Min(remaining, int.MaxValue);
    }
}

internal sealed record LocalAutoFocusResult(int Position, double Score, int Samples, long TotalTime)
{
    internal double? VidPosition { get; init; }
}

// Each delegate returns only after its native call finishes. Cancellation/deadline checks
// never abandon an in-flight device call or release its camera/motor handles early.
internal static class LocalAutoFocusSearch
{
    internal static LocalAutoFocusResult Run(LocalAutoFocusSettings settings, Func<int, int, double> sample,
        Action<int, int> move, CancellationToken token = default, Func<long>? elapsedMilliseconds = null, int? initialPosition = null)
    {
        settings.Validate();
        var watch = Stopwatch.StartNew();
        Func<long> elapsed = elapsedMilliseconds ?? (() => watch.ElapsedMilliseconds);
        int Remaining() => settings.Remaining(elapsed(), token);

        int bestPosition = settings.Minimum, samples = 0;
        int? motorPosition = initialPosition;
        double bestScore = double.NegativeInfinity;
        var measured = new SortedDictionary<int, double>();
        double Sample(int position)
        {
            position = Math.Clamp(position, settings.Minimum, settings.Maximum);
            int remaining = Remaining();
            if (measured.TryGetValue(position, out double previous)) return previous;
            double score = sample(position, remaining);
            motorPosition = position;
            Remaining();
            if (!double.IsFinite(score)) throw new InvalidOperationException("自动对焦返回了无效清晰度评价值。");
            samples++;
            measured[position] = score;
            if (score > bestScore) { bestScore = score; bestPosition = position; }
            return score;
        }
        int? InterpolatedPeak()
        {
            var points = measured.ToArray();
            int peak = Array.FindIndex(points, point => point.Key == bestPosition);
            if (peak <= 0 || peak + 1 >= points.Length) return null;
            double left = (long)points[peak - 1].Key - bestPosition, right = (long)points[peak + 1].Key - bestPosition;
            double slopeLeft = (points[peak - 1].Value - bestScore) / left;
            double slopeRight = (points[peak + 1].Value - bestScore) / right;
            double a = (slopeRight - slopeLeft) / (right - left), b = slopeLeft - a * left;
            double offset = -b / (2 * a);
            return a < 0 && double.IsFinite(offset) && offset >= left && offset <= right
                ? (int)Math.Clamp(Math.Round(bestPosition + offset), settings.Minimum, settings.Maximum) : null;
        }
        bool ConfirmInterpolatedPeak(int minimum, int maximum)
        {
            int? candidate = InterpolatedPeak();
            if (!candidate.HasValue) return false;
            double coarseScore = bestScore;
            double score = Sample(candidate.Value);
            // A fitted position must be measured and bracketed at the requested
            // stopping resolution. A poor fit keeps the complete fine scan.
            if (score < coarseScore || bestPosition != candidate.Value) return false;
            int left = (int)Math.Max(minimum, (long)candidate.Value - settings.StopStep);
            int right = (int)Math.Min(maximum, (long)candidate.Value + settings.StopStep);
            double leftScore = Sample(left), rightScore = Sample(right);
            return score > leftScore && score > rightScore;
        }
        // The grid is fixed by minimum and step; only the visiting order starts from the end nearer the motor.
        void Scan(int minimum, int maximum, int step, bool coarse = false)
        {
            long span = (long)maximum - minimum;
            long lastIndex = (span + step - 1) / step;
            bool reverse = motorPosition is int current && Math.Abs((long)maximum - current) < Math.Abs((long)current - minimum);
            bool earlyStop = coarse && settings.EarlyStopRatio > 0;
            double peak = double.NegativeInfinity, lowest = double.PositiveInfinity, lowestBeforePeak = double.PositiveInfinity;
            int falling = 0;
            for (long offset = 0; offset <= lastIndex; offset++)
            {
                long index = reverse ? lastIndex - offset : offset;
                int position = index == lastIndex ? maximum : (int)(minimum + index * step);
                double score = Sample(position);
                if (!earlyStop) continue;
                if (score > peak)
                {
                    lowestBeforePeak = lowest;
                    peak = score;
                    falling = 0;
                }
                else falling = score < settings.EarlyStopRatio * peak ? falling + 1 : 0;
                lowest = Math.Min(lowest, score);
                if (falling >= settings.EarlyStopCount && peak > settings.MinimumScore && lowestBeforePeak < settings.EarlyStopRatio * peak) break;
            }
        }

        int FirstFineStep() => (int)Math.Clamp(Math.Floor(settings.FineStep * settings.Reduction), settings.StopStep, settings.InitialStep - 1d);

        void RefinePeak(int center)
        {
            int minimum = (int)Math.Max(settings.Minimum, (long)center - settings.InitialStep);
            int maximum = (int)Math.Min(settings.Maximum, (long)center + settings.InitialStep);
            Scan(minimum, maximum, FirstFineStep());
            int? candidate = InterpolatedPeak();
            if (candidate.HasValue) Sample(candidate.Value);
        }

        void ClimbNearby(int start)
        {
            int minimum = (int)Math.Max(settings.Minimum, (long)start - settings.InitialStep);
            int maximum = (int)Math.Min(settings.Maximum, (long)start + settings.InitialStep);
            int step = FirstFineStep(), direction = 1;
            Sample(start);
            while (true)
            {
                int center = bestPosition;
                Sample((int)Math.Clamp((long)center + direction * (long)step, minimum, maximum));
                if (bestPosition != center) continue;

                Sample((int)Math.Clamp((long)center - direction * (long)step, minimum, maximum));
                if (bestPosition != center)
                {
                    direction = -direction;
                    continue;
                }

                if (ConfirmInterpolatedPeak(minimum, maximum) || step == settings.StopStep) break;
                step = Math.Max(settings.StopStep, Math.Min(step - 1, (int)Math.Floor(step * settings.Reduction)));
            }
        }

        if (settings.FineOnly)
        {
            if (initialPosition is not int current || current < settings.Minimum || current > settings.Maximum)
                throw new InvalidOperationException(EngineLocalization.Get("CameraPanel_FineFocusOutsideRange"));
            ClimbNearby(current);
        }
        else if (settings.Method == FindFuncModel.FindMaxDiff)
        {
            Scan(settings.Minimum, settings.Maximum, settings.InitialStep, coarse: true);
            if (!ConfirmInterpolatedPeak(settings.Minimum, settings.Maximum)) RefinePeak(bestPosition);
        }
        else if (settings.Method == FindFuncModel.FindDichotomy)
        {
            int current = Math.Clamp(initialPosition ?? settings.Minimum, settings.Minimum, settings.Maximum);
            int direction = current == settings.Maximum ? -1 : 1, step = settings.InitialStep;
            double currentScore = Sample(current);
            while (step > settings.StopStep)
            {
                int next = (int)Math.Clamp((long)current + direction * (long)step, settings.Minimum, settings.Maximum);
                if (next == current) { direction = -direction; step = Math.Max(settings.StopStep, step / 2); continue; }
                double score = Sample(next);
                if (score > currentScore) { current = next; currentScore = score; }
                else { direction = -direction; step = Math.Max(settings.StopStep, step / 2); }
            }
        }
        else
        {
            int minimum = settings.Minimum, maximum = settings.Maximum, step = settings.InitialStep;
            double fineStep = settings.FineStep;
            for (bool coarse = true; ; coarse = false)
            {
                Scan(minimum, maximum, step, coarse);
                if (step <= settings.StopStep) break;
                minimum = (int)Math.Max(settings.Minimum, (long)bestPosition - step);
                maximum = (int)Math.Min(settings.Maximum, (long)bestPosition + step);
                step = Math.Max(settings.StopStep, Math.Min(step - 1, (int)Math.Max(1, Math.Floor(fineStep * settings.Reduction))));
                fineStep *= settings.Reduction;
            }
        }
        if (bestScore <= settings.MinimumScore) throw new InvalidOperationException($"自动对焦未达到最低评价值 {settings.MinimumScore}，最高评价值为 {bestScore}。");
        move(bestPosition, Remaining());
        Remaining();
        return new(bestPosition, bestScore, samples, elapsed());
    }
}
