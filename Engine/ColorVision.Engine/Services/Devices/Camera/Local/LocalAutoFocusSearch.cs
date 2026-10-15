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
    internal static LocalAutoFocusSettings FromTemplate(AutoFocusParam template, MotorConfig motor)
    {
        ArgumentNullException.ThrowIfNull(template);
        var settings = new LocalAutoFocusSettings(template.EvaFunc, motor.FindFuncModel, template.MinPosition, template.MaxPosition,
            template.CurStep, template.Forwardparam, template.Curtailparam, template.StopStep, template.MinValue, template.nTimeout);
        settings.Validate(motor.MinPosition, motor.MaxPosition);
        return settings;
    }

    internal void Validate(int motorMinimum, int motorMaximum)
    {
        if (!Enum.IsDefined(Evaluation) || !Enum.IsDefined(Method)) throw new ArgumentException("自动对焦评价算法或搜索方式无效。");
        if (Minimum < motorMinimum || Maximum > motorMaximum || Minimum >= Maximum)
            throw new ArgumentException($"自动对焦范围 [{Minimum}, {Maximum}] 必须位于电机行程 [{motorMinimum}, {motorMaximum}] 内，且下限小于上限。");
        if (StopStep < 1 || InitialStep <= StopStep || InitialStep > (long)Maximum - Minimum)
            throw new ArgumentException("自动对焦初始步长必须大于停止步长且不超过搜索范围，停止步长至少为 1。");
        if (!double.IsFinite(FineStep) || FineStep < 1 || FineStep > (long)Maximum - Minimum
            || !double.IsFinite(Reduction) || Reduction <= 0 || Reduction >= 1)
            throw new ArgumentException("自动对焦摆动步长须在搜索范围内，缩减系数必须大于 0 且小于 1。");
        if (!double.IsFinite(MinimumScore) || MinimumScore < 0 || Timeout <= 0)
            throw new ArgumentException("自动对焦最低评价值必须为非负有限值，超时必须大于 0。");
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
        Action<int, int> move, CancellationToken token = default, Func<long>? elapsedMilliseconds = null)
    {
        settings.Validate(settings.Minimum, settings.Maximum);
        var watch = Stopwatch.StartNew();
        Func<long> elapsed = elapsedMilliseconds ?? (() => watch.ElapsedMilliseconds);
        int Remaining()
        {
            token.ThrowIfCancellationRequested();
            long left = settings.Timeout - elapsed();
            if (left <= 0) throw new TimeoutException("本地自动对焦超时，已停止后续移动；当前设备调用已返回。");
            return (int)Math.Min(left, int.MaxValue);
        }

        int bestPosition = settings.Minimum, samples = 0;
        double bestScore = double.NegativeInfinity;
        var measured = new SortedDictionary<int, double>();
        double Sample(int position)
        {
            position = Math.Clamp(position, settings.Minimum, settings.Maximum);
            double score = sample(position, Remaining());
            Remaining();
            if (!double.IsFinite(score)) throw new InvalidOperationException("自动对焦返回了无效清晰度评价值。");
            samples++;
            measured[position] = score;
            if (score > bestScore) { bestScore = score; bestPosition = position; }
            return score;
        }
        void Scan(int minimum, int maximum, int step)
        {
            int position = minimum;
            while (true)
            {
                Sample(position);
                if (position == maximum) break;
                position = (int)Math.Min((long)position + step, maximum);
            }
        }

        if (settings.Method == FindFuncModel.FindDichotomy)
        {
            int current = settings.Minimum, direction = 1, step = settings.InitialStep;
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
            while (true)
            {
                Scan(minimum, maximum, step);
                if (step <= settings.StopStep) break;
                minimum = (int)Math.Max(settings.Minimum, (long)bestPosition - step);
                maximum = (int)Math.Min(settings.Maximum, (long)bestPosition + step);
                step = Math.Max(settings.StopStep, Math.Min(step - 1, (int)Math.Max(1, Math.Floor(fineStep * settings.Reduction))));
                fineStep *= settings.Reduction;
                if (settings.Method == FindFuncModel.FindMaxDiff)
                {
                    Scan(minimum, maximum, step);
                    // Fit a local parabola around the sampled peak, using relative positions
                    // to avoid loss of precision for large motor coordinates.
                    var points = measured.ToArray();
                    int peak = Array.FindIndex(points, point => point.Key == bestPosition);
                    if (peak > 0 && peak + 1 < points.Length)
                    {
                        double left = (long)points[peak - 1].Key - bestPosition, right = (long)points[peak + 1].Key - bestPosition;
                        double slopeLeft = (points[peak - 1].Value - bestScore) / left;
                        double slopeRight = (points[peak + 1].Value - bestScore) / right;
                        double a = (slopeRight - slopeLeft) / (right - left), b = slopeLeft - a * left;
                        double offset = -b / (2 * a);
                        if (a < 0 && double.IsFinite(offset) && offset >= left && offset <= right)
                            Sample((int)Math.Clamp(Math.Round(bestPosition + offset), minimum, maximum));
                    }
                    break;
                }
            }
        }
        if (bestScore <= settings.MinimumScore) throw new InvalidOperationException($"自动对焦未达到最低评价值 {settings.MinimumScore}，最高评价值为 {bestScore}。");
        move(bestPosition, Remaining());
        Remaining();
        return new(bestPosition, bestScore, samples, elapsed());
    }
}
