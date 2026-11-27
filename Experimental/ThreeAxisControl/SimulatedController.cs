using System;
using System.Diagnostics;
using System.Threading;

namespace ThreeAxisControl
{
    internal sealed class SimulatedController : IMotionController
    {
        private readonly SimulatedAxis[] axes;
        private readonly Stopwatch clock = Stopwatch.StartNew();

        public SimulatedController(int axisCount)
        {
            axes = new SimulatedAxis[axisCount];
        }

        public void Connect()
        {
            Array.Clear(axes, 0, axes.Length);
        }

        public AxisState ReadAxis(int axis)
        {
            var movement = axes[axis];
            double now = clock.Elapsed.TotalSeconds;
            return new AxisState(movement.GetPosition(now), movement.EndTime > now, false, false, false);
        }

        public void MoveRelative(int axis, int pulses, MotionProfile profile, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double now = clock.Elapsed.TotalSeconds;
            int position = axes[axis].GetPosition(now);
            int target = checked(position + pulses);
            axes[axis] = new SimulatedAxis(position, target, now,
                now + Math.Abs((double)pulses) / profile.MaxSpeed.GetValueOrDefault(100));
        }

        public void StopAll()
        {
            double now = clock.Elapsed.TotalSeconds;
            for (int axis = 0; axis < axes.Length; axis++)
            {
                int position = axes[axis].GetPosition(now);
                axes[axis] = new SimulatedAxis(position, position, now, now);
            }
        }

        public void Dispose() { }

        private struct SimulatedAxis
        {
            public SimulatedAxis(int start, int target, double startTime, double endTime)
            {
                Start = start;
                Target = target;
                StartTime = startTime;
                EndTime = endTime;
            }

            public int Start { get; }
            public int Target { get; }
            public double StartTime { get; }
            public double EndTime { get; }

            public int GetPosition(double now)
            {
                if (now >= EndTime)
                    return Target;

                double fraction = (now - StartTime) / (EndTime - StartTime);
                return (int)Math.Round(Start + (Target - (double)Start) * fraction);
            }
        }
    }
}
