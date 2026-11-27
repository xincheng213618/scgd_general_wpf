using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ThreeAxisControl
{
    internal sealed class Pmc100Controller : IMotionController
    {
        private const int ReplyTimeoutMs = 350;
        private const int MaxReplyBufferLength = 4096;
        private static readonly char[] LineEndings = { '\r', '\n' };
        private readonly SerialPort _port;
        private readonly int?[] _positions = new int?[2];
        private string _replyBuffer = string.Empty;
        private int? _pendingMoveAxis;
        private string _pendingMoveCommand;
        private string _pendingMoveReply;
        private bool _stopRequired;

        public Pmc100Controller(string portName, int baudRate)
        {
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                Encoding = Encoding.ASCII,
                Handshake = Handshake.None,
                ReadTimeout = ReplyTimeoutMs,
                WriteTimeout = ReplyTimeoutMs
            };
        }

        public void Connect()
        {
            _port.Open();
            _port.DiscardInBuffer();
            for (int axis = 0; axis < _positions.Length; axis++)
                _positions[axis] = QueryPosition(GetControllerAxisName(axis));
        }

        public AxisState ReadAxis(int axis)
        {
            char axisName = GetControllerAxisName(axis);
            ReadPendingMoveReply();

            // The move and position inquiry use identical reply formats. Keep
            // queries off the wire until the outstanding move reply arrives.
            if (!_pendingMoveAxis.HasValue)
                _positions[axis] = QueryPosition(axisName);

            return new AxisState(_positions[axis], null);
        }

        public void MoveRelative(int axis, int pulses, MotionProfile profile, CancellationToken cancellationToken)
        {
            char axisName = GetControllerAxisName(axis);
            if (pulses == 0 || pulses == int.MinValue)
                throw new ArgumentOutOfRangeException(nameof(pulses));

            cancellationToken.ThrowIfCancellationRequested();
            ReadPendingMoveReply();
            if (_stopRequired)
                throw new InvalidOperationException("上一条 XY 指令未确认，请先停止 XY。");
            if (_pendingMoveAxis.HasValue)
                throw new InvalidOperationException("上一条 XY 移动指令的位置回复尚未收到，可先停止 XY。");

            if (profile.StartSpeed.HasValue)
                ConfigureMotion(FormattableString.Invariant($"#F{axisName} {profile.StartSpeed.Value}#"), cancellationToken,
                    $"OK_{axisName}BeginSpeed", "OK_VF", "OK_VC");
            if (profile.MaxSpeed.HasValue)
                ConfigureMotion(FormattableString.Invariant($"#V{axisName} {profile.MaxSpeed.Value}#"), cancellationToken,
                    $"OK_{axisName}FinalSpeed", $"OK_V{axisName}", "OK_VC");
            if (profile.Acceleration.HasValue)
                ConfigureMotion(FormattableString.Invariant($"#A{axisName} {profile.Acceleration.Value}#"), cancellationToken,
                    $"OK_{axisName}AccelerationSpeed", "OK_VA");

            char direction = pulses > 0 ? '+' : '-';
            string moveCommand = FormattableString.Invariant($"#{direction}{axisName} {Math.Abs(pulses)}#");
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _port.Write(moveCommand);
            }
            finally
            {
                // A write failure can leave a command on the wire with unknown delivery.
                _pendingMoveAxis = axis;
                _pendingMoveCommand = moveCommand;
                _positions[axis] = null;
            }
        }

        public void StopAll()
        {
            // $sss interrupts the current command on the XY controller. Any
            // position reply interleaved with the stop response is still parsed.
            _stopRequired = true;
            SendAndReadReply("$sss", reply => ReplyEquals(reply, "STOP") || ReplyEquals(reply, "EN"));
            ClearPendingMove();
            _stopRequired = false;
        }

        public void Dispose()
        {
            _port.Dispose();
        }

        private int QueryPosition(char axisName)
        {
            int position = 0;
            SendAndReadReply($"#?{axisName}#", reply => TryParsePosition(reply, axisName, out position));
            return position;
        }

        private void ConfigureMotion(string command, CancellationToken cancellationToken, params string[] acknowledgements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stopRequired = true;
            SendAndReadReply(command, reply => Array.Exists(acknowledgements, expected => ReplyEquals(reply, expected)), cancellationToken);
            _stopRequired = false;
        }

        private void SendAndReadReply(string command, Predicate<string> matches, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.Write(command);
            var elapsed = Stopwatch.StartNew();
            string unexpectedReply = null;

            while (elapsed.ElapsedMilliseconds < ReplyTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryReadLine(out string line))
                {
                    Thread.Sleep(5);
                    continue;
                }

                if (AcceptMoveReply(line))
                    continue;
                if (matches(line))
                    return;
                if (IsReply(line, command))
                    unexpectedReply = line;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string reply = unexpectedReply ?? _replyBuffer.Trim();
            if (reply.Length > 0)
                throw new IOException($"XY 指令 {command} 回复未识别：{DescribeReply(reply)}。");
            throw new TimeoutException($"XY 指令 {command} 超时，未收到回复。");
        }

        private void ReadPendingMoveReply()
        {
            if (!_pendingMoveAxis.HasValue)
                return;

            AppendAvailableInput();
            while (TryTakeBufferedLine(out string line))
            {
                if (AcceptMoveReply(line))
                    return;
                if (IsReply(line, _pendingMoveCommand))
                    _pendingMoveReply = line;
            }

            if (_pendingMoveReply != null)
                throw new IOException($"XY 移动指令 {_pendingMoveCommand} 回复未识别：{DescribeReply(_pendingMoveReply)}。可先停止 XY。");
        }

        private bool AcceptMoveReply(string line)
        {
            if (!_pendingMoveAxis.HasValue)
                return false;

            int axis = _pendingMoveAxis.Value;
            if (!TryParsePosition(line, GetControllerAxisName(axis), out int position))
                return false;

            _positions[axis] = position;
            ClearPendingMove();
            return true;
        }

        private void ClearPendingMove()
        {
            _pendingMoveAxis = null;
            _pendingMoveCommand = null;
            _pendingMoveReply = null;
        }

        private bool TryReadLine(out string line)
        {
            if (TryTakeBufferedLine(out line))
                return true;

            AppendAvailableInput();
            return TryTakeBufferedLine(out line);
        }

        private void AppendAvailableInput()
        {
            _replyBuffer += _port.ReadExisting();
            if (_replyBuffer.Length <= MaxReplyBufferLength)
                return;

            _replyBuffer = string.Empty;
            throw new IOException("XY 串口返回的数据超出回复缓冲区，请检查串口参数。");
        }

        private bool TryTakeBufferedLine(out string line)
        {
            int newline = _replyBuffer.IndexOfAny(LineEndings);
            if (newline < 0)
            {
                line = null;
                return false;
            }

            line = _replyBuffer.Substring(0, newline).Trim();
            _replyBuffer = _replyBuffer.Substring(newline + 1);
            return true;
        }

        private static bool TryParsePosition(string line, char axis, out int position)
        {
            position = 0;
            if (line.StartsWith(">>", StringComparison.Ordinal))
                line = line.Substring(1);
            return line.StartsWith($">{axis}:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(line.Substring(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out position);
        }

        private static bool ReplyEquals(string line, string expected)
        {
            if (line.StartsWith(">", StringComparison.Ordinal))
                line = line.Substring(1).TrimStart();
            return string.Equals(line, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReply(string line, string command) => line.Length > 0 &&
            !string.Equals(line, command, StringComparison.OrdinalIgnoreCase);

        private static string DescribeReply(string reply)
        {
            const int limit = 160;
            return reply.Length > limit ? reply.Substring(0, limit) + "…" : reply;
        }

        private static char GetControllerAxisName(int axis)
        {
            switch (axis)
            {
                case 0: return 'Y'; // Panel X (front/back).
                case 1: return 'X'; // Panel Y (left/right).
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }
    }
}
