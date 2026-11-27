using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace ThreeAxisControl
{
    internal sealed class Mc06Controller : IMotionController
    {
        private const int PositiveLimitMask = 0x0004 | 0x0020;
        private const int NegativeLimitMask = 0x0008 | 0x0040;
        private const int AlarmMask = 0x0001 | 0x0002 | 0x0010 | 0x0080 | 0x0100;
        private const int EnabledMask = 0x0200;
        private const int MovingMask = 0x0400 | 0x1000;
        private const int MaxPulsesPerSecond = 2000000;
        private const double RampMilliseconds = 250;
        private const ushort ConnectRetries = 5;

        private readonly IPAddress localIp;
        private readonly IPAddress controllerIp;
        private readonly ushort localPort;
        private readonly short channel;
        private readonly int channelMask;
        private bool connected;

        public Mc06Controller(string localIp, string controllerIp, int port, int channel)
        {
            this.localIp = ParseIpv4(localIp, "电脑 IP");
            this.controllerIp = ParseIpv4(controllerIp, "控制器 IP");
            if (this.localIp.Equals(this.controllerIp))
                throw new ArgumentException("Z 轴电脑 IP 和控制器 IP 必须不同。");
            if (port < 1 || port > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port), "电脑 UDP 端口必须在 1 至 65535 之间。");
            if (channel < 1 || channel > 8)
                throw new ArgumentOutOfRangeException(nameof(channel), "Z 轴控制卡通道必须在 1 至 8 之间。");

            localPort = (ushort)port;
            this.channel = (short)channel;
            channelMask = 1 << (channel - 1);
        }

        public void Connect()
        {
            if (connected)
                return;
            if (!Environment.Is64BitProcess)
                throw new InvalidOperationException("Z 轴控制卡需要 64 位程序和 64 位 GAS.dll。");

            string sdkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Native.DllName);
            if (!File.Exists(sdkPath))
                throw new FileNotFoundException("请将 Z 轴厂商提供的 64 位 GAS.dll 放在程序同一目录。", sdkPath);

            bool hasAddress = NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Any(address => localIp.Equals(address.Address));
            if (!hasAddress)
                throw new InvalidOperationException($"电脑当前启用的网卡未配置 Z 轴电脑 IP {localIp}。");

            try
            {
                Check(Native.GA_SetPCEthernetPort(localPort), "设置电脑 UDP 端口");
                // The single-card SDK sends to controller UDP port 60000. ID 0 selects by IP.
                int result = Native.GA_OpenByIP(localIp.ToString(), controllerIp.ToString(), 0, ConnectRetries);
                if (result != 0)
                {
                    Native.GA_Close();
                    Check(result, "连接控制卡");
                }

                connected = true;
                ReadAxis(0);
            }
            catch (BadImageFormatException ex)
            {
                Dispose();
                throw new InvalidOperationException("Z 轴 GAS.dll 加载失败，请检查是否为厂商提供的 64 位版本。", ex);
            }
            catch (DllNotFoundException ex)
            {
                Dispose();
                throw new InvalidOperationException("Z 轴 GAS.dll 或其运行依赖加载失败。", ex);
            }
            catch (EntryPointNotFoundException ex)
            {
                Dispose();
                throw new InvalidOperationException("Z 轴 GAS.dll 缺少控制接口，请检查厂商 SDK 版本。", ex);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public AxisState ReadAxis(int axis)
        {
            EnsureAxis(axis);
            int status = ReadStatus();
            return new AxisState(ReadPosition(), (status & MovingMask) != 0,
                (status & PositiveLimitMask) != 0, (status & NegativeLimitMask) != 0,
                (status & AlarmMask) != 0);
        }

        public void MoveRelative(int axis, int pulses, MotionProfile profile, CancellationToken cancellationToken)
        {
            EnsureAxis(axis);
            if (pulses == 0)
                throw new ArgumentOutOfRangeException(nameof(pulses));

            int maxSpeed = profile.MaxSpeed.GetValueOrDefault();
            int startSpeed = profile.StartSpeed.GetValueOrDefault();
            if (maxSpeed < 1 || maxSpeed > MaxPulsesPerSecond)
                throw new ArgumentOutOfRangeException(nameof(profile), "Z 轴最高速度必须在 1 至 2000000 脉冲/秒之间。");
            if (startSpeed < 0 || startSpeed > maxSpeed)
                throw new ArgumentOutOfRangeException(nameof(profile), "Z 轴起始速度必须在 0 和最高速度之间。");
            if (profile.Acceleration.HasValue && profile.Acceleration.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(profile), "Z 轴加速度必须大于 0 脉冲/秒²。");

            cancellationToken.ThrowIfCancellationRequested();
            int status = ReadStatus();
            if ((status & AlarmMask) != 0)
                throw new InvalidOperationException("Z 轴存在报警或停止信号，请先检查控制器。");
            if ((status & MovingMask) != 0)
                throw new InvalidOperationException("Z 轴正在运动。");
            int limitMask = pulses > 0 ? PositiveLimitMask : NegativeLimitMask;
            if ((status & limitMask) != 0)
                throw new InvalidOperationException("Z 轴在移动方向上的限位已触发。");

            cancellationToken.ThrowIfCancellationRequested();
            int position = ReadPosition();
            int target = checked(position + pulses);
            cancellationToken.ThrowIfCancellationRequested();
            if ((status & EnabledMask) == 0)
                Check(Native.GA_AxisOn(channel), "使能");

            // GAS uses milliseconds. An unspecified acceleration keeps the 250 ms ramp from zero.
            double velocity = maxSpeed / 1000.0;
            double startVelocity = startSpeed / 1000.0;
            double acceleration = profile.Acceleration.HasValue
                ? profile.Acceleration.Value / 1000000.0
                : velocity / RampMilliseconds;
            cancellationToken.ThrowIfCancellationRequested();
            Check(Native.GA_PrfTrap(channel), "选择点位模式");
            cancellationToken.ThrowIfCancellationRequested();
            Check(Native.GA_SetTrapPrmSingle(channel, acceleration, acceleration, startVelocity, 0), "设置起始速度和加减速");
            cancellationToken.ThrowIfCancellationRequested();
            Check(Native.GA_SetVel(channel, velocity), "设置最高速度");
            cancellationToken.ThrowIfCancellationRequested();
            Check(Native.GA_SetPos(channel, target), "设置目标位置");

            cancellationToken.ThrowIfCancellationRequested();
            int result = Native.GA_Update(channelMask);
            if (result == 0 && !cancellationToken.IsCancellationRequested)
                return;

            // A lost acknowledgement or cancellation during the call can leave the drive moving.
            int stopResult = Native.GA_Stop(channelMask, channelMask);
            if (result != 0 && stopResult != 0)
                throw new IOException($"Z 轴移动未确认（{result}），停止也未确认（{stopResult}）。");
            Check(result, "启动移动");
            Check(stopResult, "取消移动后停止");
            cancellationToken.ThrowIfCancellationRequested();
        }

        public void StopAll()
        {
            if (connected)
                Check(Native.GA_Stop(channelMask, channelMask), "停止");
        }

        public void Dispose()
        {
            if (!connected)
                return;

            connected = false;
            Check(Native.GA_Close(), "关闭连接");
        }

        private void EnsureAxis(int axis)
        {
            if (axis != 0)
                throw new ArgumentOutOfRangeException(nameof(axis));
            if (!connected)
                throw new InvalidOperationException("Z 轴控制器尚未连接。");
        }

        private int ReadStatus()
        {
            Check(Native.GA_GetSts(channel, out int status, 1, out _), "读取状态");
            return status;
        }

        private int ReadPosition()
        {
            Check(Native.GA_GetPrfPos(channel, out double position, 1, out _), "读取规划位置");
            double rounded = Math.Round(position, MidpointRounding.AwayFromZero);
            if (double.IsNaN(rounded) || rounded < int.MinValue || rounded > int.MaxValue)
                throw new IOException("Z 轴返回的规划位置超出可读脉冲范围。");
            return (int)rounded;
        }

        private static IPAddress ParseIpv4(string text, string field)
        {
            if (!TryParseIpv4(text, out IPAddress ip))
                throw new ArgumentException($"请输入有效的 Z 轴{field}（IPv4）。");
            return ip;
        }

        internal static bool TryParseIpv4(string text, out IPAddress ip)
        {
            if (!IPAddress.TryParse(text?.Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte firstByte = ip.GetAddressBytes()[0];
            return firstByte > 0 && firstByte < 224;
        }

        private static void Check(int code, string operation)
        {
            if (code == 0)
                return;

            string detail;
            switch (code)
            {
                case -1: detail = "发送失败"; break;
                case -6: detail = "打开失败"; break;
                case -7: detail = "控制卡无响应"; break;
                case 1: detail = "控制卡拒绝执行"; break;
                case 2: detail = "当前控制卡授权不支持"; break;
                case 7: detail = "控制卡拒绝参数"; break;
                default: detail = "控制卡报错"; break;
            }

            throw new IOException($"Z 轴{operation}失败，{detail}（GAS 返回 {code}）。");
        }

        private static class Native
        {
            internal const string DllName = "GAS.dll";
            private const DllImportSearchPath SearchPaths = DllImportSearchPath.AssemblyDirectory;

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_SetPCEthernetPort(ushort port);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, CharSet = CharSet.Ansi)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_OpenByIP(string localIp, string controllerIp, uint id, ushort retries);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_Close();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_GetSts(short channel, out int status, short count, out uint clock);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_GetPrfPos(short channel, out double position, short count, out uint clock);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_AxisOn(short channel);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_PrfTrap(short channel);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_SetTrapPrmSingle(short channel, double acceleration, double deceleration,
                double startVelocity, short smoothingTime);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_SetVel(short channel, double velocity);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_SetPos(short channel, long position);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_Update(int mask);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(SearchPaths)]
            internal static extern int GA_Stop(int mask, int option);
        }
    }
}
