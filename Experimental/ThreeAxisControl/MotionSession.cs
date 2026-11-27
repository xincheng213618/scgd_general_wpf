using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThreeAxisControl
{
    internal sealed class MotionSession
    {
        private const int HoldPollIntervalMs = 100;
        private readonly int axisCount;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private volatile IMotionController controller;
        private int stopVersion;

        public MotionSession(int axisCount)
        {
            if (axisCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(axisCount));

            this.axisCount = axisCount;
        }

        public bool IsConnected => controller != null;

        public Task ConnectAsync(IMotionController nextController)
        {
            if (nextController == null)
                throw new ArgumentNullException(nameof(nextController));

            return RunAsync(() =>
            {
                try
                {
                    if (controller != null)
                        throw new InvalidOperationException("控制器已连接。");

                    nextController.Connect();
                    controller = nextController;
                }
                catch
                {
                    nextController.Dispose();
                    throw;
                }
            });
        }

        public Task DisconnectAsync()
        {
            Interlocked.Increment(ref stopVersion);
            return RunAsync(() =>
            {
                if (controller == null)
                    return;

                try
                {
                    controller.StopAll();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"连接已断开，但控制器未确认停止。{ex.Message}", ex);
                }
                finally
                {
                    var current = controller;
                    controller = null;
                    current.Dispose();
                }
            });
        }

        public async Task<AxisState[]> ReadAxesAsync()
        {
            if (!gate.Wait(0))
                return null;

            try
            {
                if (controller == null)
                    return null;

                int readVersion = Volatile.Read(ref stopVersion);
                return await Task.Run(() =>
                {
                    var states = new AxisState[axisCount];
                    for (int axis = 0; axis < states.Length; axis++)
                    {
                        if (readVersion != Volatile.Read(ref stopVersion))
                            return null;
                        states[axis] = controller.ReadAxis(axis);
                    }
                    return states;
                }).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<int> MoveRelativeAsync(int axis, int direction, int step, MotionProfile profile,
            MotionLimits limits, CancellationToken cancellationToken)
        {
            ValidateMoveArguments(axis, direction, step);

            int requestedVersion = Volatile.Read(ref stopVersion);
            var move = await TryMoveRelativeAsync(axis, direction, step, profile, limits, requestedVersion,
                cancellationToken).ConfigureAwait(false);
            return move.Value.Pulses;
        }

        public async Task MoveWhileHeldAsync(int axis, int direction, int step, MotionProfile profile,
            MotionLimits limits, CancellationToken cancellationToken, IProgress<int> started)
        {
            bool hasBoundary = (direction > 0 ? limits.Maximum : limits.Minimum).HasValue;
            int? segment = hasBoundary ? (int?)null : step;
            ValidateMoveArguments(axis, direction, segment);

            int requestedVersion = Volatile.Read(ref stopVersion);
            int? previousTarget = null;
            while (true)
            {
                var move = await TryMoveRelativeAsync(axis, direction, segment, profile, limits, requestedVersion,
                    cancellationToken, requirePosition: true, previousTarget: previousTarget).ConfigureAwait(false);
                if (move.HasValue)
                {
                    if (!previousTarget.HasValue)
                        started?.Report(move.Value.Pulses);
                    if (hasBoundary)
                        return;

                    previousTarget = move.Value.Target;
                }

                await Task.Delay(HoldPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        private void ValidateMoveArguments(int axis, int direction, int? step)
        {
            if (axis < 0 || axis >= axisCount)
                throw new ArgumentOutOfRangeException(nameof(axis));
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step));
        }

        private async Task<(int Pulses, int? Target)?> TryMoveRelativeAsync(int axis, int direction, int? step,
            MotionProfile profile, MotionLimits limits, int requestedVersion, CancellationToken cancellationToken,
            bool requirePosition = false, int? previousTarget = null)
        {
            (int Pulses, int? Target)? move = null;
            await RunAsync(() =>
            {
                ThrowIfStopped(requestedVersion);
                cancellationToken.ThrowIfCancellationRequested();
                if (controller == null)
                    throw new InvalidOperationException("请先连接控制器。");

                var state = controller.ReadAxis(axis);
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfStopped(requestedVersion);
                if (state.Alarm == true)
                    throw new InvalidOperationException("轴处于报警状态。");
                if ((direction > 0 && state.PositiveLimit == true) || (direction < 0 && state.NegativeLimit == true))
                    throw new InvalidOperationException("该方向限位已触发。");
                if (previousTarget.HasValue && (state.Position != previousTarget || state.Moving == true))
                    return;
                if (state.Moving == true)
                    throw new InvalidOperationException("轴正在移动，请等待或先停止。");
                if (requirePosition && !state.Position.HasValue)
                    throw new InvalidOperationException("尚未读取该轴位置，无法开始持续运动。");

                int pulses = ResolveTravel(state.Position, direction, step, limits);
                ThrowIfStopped(requestedVersion);
                cancellationToken.ThrowIfCancellationRequested();
                controller.MoveRelative(axis, pulses, profile, cancellationToken);
                move = (pulses, state.Position + pulses);
            }, cancellationToken).ConfigureAwait(false);
            return move;
        }

        private static int ResolveTravel(int? position, int direction, int? step, MotionLimits limits)
        {
            int? boundary = direction > 0 ? limits.Maximum : limits.Minimum;
            long travel = step.GetValueOrDefault(int.MaxValue);
            if (boundary.HasValue)
            {
                if (!position.HasValue)
                    throw new InvalidOperationException("尚未读取该轴位置，无法判断软限位。");

                long remaining = ((long)boundary.Value - position.Value) * direction;
                if (remaining <= 0)
                    throw new InvalidOperationException("已到达该方向的软限位。");
                if (!step.HasValue && remaining > int.MaxValue)
                    throw new InvalidOperationException("到软限位的距离超出单次移动的脉冲范围。");
                travel = Math.Min(travel, remaining);
            }

            long target = position.GetValueOrDefault() + direction * travel;
            if (target < int.MinValue || target > int.MaxValue)
                throw new InvalidOperationException("移动目标超出可表示的脉冲范围。");
            return direction * (int)travel;
        }

        public Task StopAsync()
        {
            Interlocked.Increment(ref stopVersion);
            return RunAsync(() => controller?.StopAll());
        }

        private void ThrowIfStopped(int requestedVersion)
        {
            if (requestedVersion != Volatile.Read(ref stopVersion))
                throw new OperationCanceledException("移动指令已取消。");
        }

        private async Task RunAsync(Action action, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(action).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
