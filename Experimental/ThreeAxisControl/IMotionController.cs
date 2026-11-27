using System;
using System.Threading;

namespace ThreeAxisControl
{
    internal interface IMotionController : IDisposable
    {
        void Connect();
        AxisState ReadAxis(int axis);
        void MoveRelative(int axis, int pulses, MotionProfile profile, CancellationToken cancellationToken);
        void StopAll();
    }

    internal readonly struct MotionProfile
    {
        public MotionProfile(int? startSpeed, int? maxSpeed, int? acceleration)
        {
            if (startSpeed < 0 || maxSpeed <= 0 || acceleration <= 0 || startSpeed > maxSpeed)
                throw new ArgumentException("速度或加速度无效，初速度需小于或等于最高速度。");

            StartSpeed = startSpeed;
            MaxSpeed = maxSpeed;
            Acceleration = acceleration;
        }

        public int? StartSpeed { get; }
        public int? MaxSpeed { get; }
        public int? Acceleration { get; }
    }

    internal readonly struct MotionLimits
    {
        public MotionLimits(int? minimum, int? maximum)
        {
            if (minimum >= maximum)
                throw new ArgumentException("负向限位必须小于正向限位。");

            Minimum = minimum;
            Maximum = maximum;
        }

        public int? Minimum { get; }
        public int? Maximum { get; }
    }

    internal sealed class AxisState
    {
        public AxisState(int? position, bool? moving, bool? positiveLimit = null,
            bool? negativeLimit = null, bool? alarm = null)
        {
            Position = position;
            Moving = moving;
            PositiveLimit = positiveLimit;
            NegativeLimit = negativeLimit;
            Alarm = alarm;
        }

        public int? Position { get; }
        public bool? Moving { get; }
        public bool? PositiveLimit { get; }
        public bool? NegativeLimit { get; }
        public bool? Alarm { get; }
    }
}
