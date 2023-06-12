#pragma warning disable CS8618, CS0649
using ColorVision.MVVM;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Serial
{
    /// <summary>
    /// 新版电机驱动
    /// </summary>
    public class MotorControl : BaseSerialPort, IDisposable
    {
        private static MotorControl _Instance;
        private static readonly object locker = new object();
        public static MotorControl GetInstance()
        {
            lock (locker) { return _Instance ?? new MotorControl(); }
        }
        private Timer movetimer1;


        private MotorControl()
        {
            serialPort = new SerialPort();
            timer = new Timer((s) => TimeRun(s), null, 0, 100);
            movetimer1 = new Timer((s) => moveread(s), null, 0, 100);
        }
        private bool IsMove { get; set; }
        private void moveread(object? s)
        {
            if (IsMove)
                ReadMotorState();
        }


        private void TimeRun(object? s)
        {
            if (serialPort != null && serialPort.IsOpen == false)
            {
                IsOpen = false;
                timer.Dispose();
            }
        }
        public int OpenPort(string PortName)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    serialPort.Dispose();
                    serialPort = new SerialPort { PortName = PortName, BaudRate = 115200 };
                    serialPort.Open();
                    byte[] buffer = new byte[16] { 0x01, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x26, 0x65 };
                    serialPort.Write(buffer, 0, buffer.Length);
                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(16);
                        int bytesread = serialPort.BytesToRead;
                        if (bytesread > 0)
                        {
                            byte[] buff = new byte[bytesread];
                            serialPort.Read(buff, 0, bytesread);
                            if (buff.Length > 1 || buff[1] == 101)
                            {
                                MotorState.SetData(buff);
                                IsOpen = true;
                                serialPort.DataReceived += SerialPort_DataReceived;
                                timer = new Timer((s) => TimeRun(s), null, 0, 100);
                                return 0;
                            }
                        }
                    }

                    serialPort.Close();
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
            catch
            {
                return -2;
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (sender is SerialPort serialPort)
                {
                    Thread.Sleep(50);
                    int bytesread = serialPort.BytesToRead;
                    byte[] buff = new byte[bytesread];
                    serialPort.Read(buff, 0, bytesread);

                    if (buff.Length == 1)
                    {
                        if (buff[0] == 161)
                        {
                            IsMove = false;
                            ReadMotorState();
                        }
                        if (buff[0] == 177)
                        {
                            IsMove = false;
                            ReadMotorState();
                        }
                    }

                    if (buff.Length == 16)
                    {
                        if (Crc16.ValidateModbusCRC16(buff))
                        {
                            if (buff[1] == 101)
                            {
                                MotorState.SetData(buff);
                            }
                        }
                    }
                    //这里做17是因为A1,电机运行截至指令会和查询返回指令粘在一起。
                    else if (buff.Length == 17)
                    {
                        byte[] buff1 = new byte[16];

                        if (buff[0] == 161)
                        {
                            Array.Copy(buff, 1, buff1, 0, buff1.Length);
                            IsMove = false;
                            ReadMotorState();
                        }
                        else if (buff[16] == 161)
                        {
                            Array.Copy(buff, 0, buff1, 0, buff1.Length);
                            IsMove = false;
                            ReadMotorState();
                        }
                        if (buff1[1] == 101)
                        {
                            MotorState.SetData(buff);
                        }
                    }
                }


            }
            catch { }
        }


        public MotorState MotorState { get; set; } = new MotorState();


        public int Initialized()
        {
            string[] TempPortNames = SerialPort.GetPortNames();
            //这种写法不允许有多个串口；
            for (int i = 0; i < TempPortNames.Length; i++)
            {
                if (OpenPort(TempPortNames[i]) == 0)
                {
                    return 0;
                }
            }
            return -1;
        }
        public void Close()
        {
            if (serialPort.IsOpen)
            {
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Dispose();
            }
        }


        public void SendMsg(byte[] msg)
        {
            if (serialPort.IsOpen)
                serialPort.Write(msg, 0, msg.Length);
        }

        public void ReadMotorState()
        {
            byte[] data = new byte[14] { 0x01, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            SendMsg(Crc16.CRC16(data));
        }


        public void Move(int length = 1000, float spe = 30)
        {
            byte[] len = BitConverter.GetBytes(length); // 将编码器值转换为字节数组
            Array.Reverse(len);
            int encoderValue1 = (int)(spe * 16384 / 6000);
            byte[] speed = BitConverter.GetBytes(encoderValue1); // 将编码器值转换为字节数组
            Array.Reverse(speed);

            byte[] data = new byte[14];
            byte[] func = new byte[3] { 0x01, 0x64, 0x01 };
            byte[] over = new byte[3] { 0x00, 0x00, 0x00 };
            Array.Copy(func, 0, data, 0, func.Length);
            Array.Copy(speed, 0, data, func.Length, speed.Length);
            Array.Copy(len, 0, data, func.Length + speed.Length, len.Length);
            Array.Copy(over, 0, data, func.Length + speed.Length + len.Length, over.Length);
            Array.Copy(data, data, data.Length);
            IsMove = true;
            SendMsg(Crc16.CRC16(data));
        }

        public void Moveangle(float angle = 360, float spe = 30)
        {

            int resolution = 16384; // 编码器分辨率
            int encoderValue = (int)(angle * resolution / 360.0); // 计算编码器值
            byte[] len = BitConverter.GetBytes(encoderValue); // 将编码器值转换为字节数组
            Array.Reverse(len);

            int encoderValue1 = (int)(spe * 16384 / 6000);
            byte[] speed = BitConverter.GetBytes(encoderValue1); // 将编码器值转换为字节数组
            Array.Reverse(speed);

            byte[] data = new byte[14];
            byte[] func = new byte[3] { 0x01, 0x64, 0x01 };
            byte[] over = new byte[3] { 0x00, 0x00, 0x00 };
            Array.Copy(func, 0, data, 0, func.Length);
            Array.Copy(speed, 0, data, func.Length, speed.Length);
            Array.Copy(len, 0, data, func.Length + speed.Length, len.Length);
            Array.Copy(over, 0, data, func.Length + speed.Length + len.Length, over.Length);
            Array.Copy(data, data, data.Length);
            IsMove = true;
            SendMsg(Crc16.CRC16(data));
        }

        public void ReturnZero()
        {
            byte[] len = BitConverter.GetBytes(0 - MotorState.RelativePosition); // 将编码器值转换为字节数组
            Array.Reverse(len);

            int encoderValue1 = (int)(80 * 16384 / 6000);
            byte[] speed = BitConverter.GetBytes(encoderValue1); // 将编码器值转换为字节数组
            Array.Reverse(speed);

            byte[] data = new byte[14];
            byte[] func = new byte[3] { 0x01, 0x64, 0x01 };
            byte[] over = new byte[3] { 0x00, 0x00, 0x00 };
            Array.Copy(func, 0, data, 0, func.Length);
            Array.Copy(speed, 0, data, func.Length, speed.Length);
            Array.Copy(len, 0, data, func.Length + speed.Length, len.Length);
            Array.Copy(over, 0, data, func.Length + speed.Length + len.Length, over.Length);
            Array.Copy(data, data, data.Length);
            IsMove = true;
            SendMsg(Crc16.CRC16(data));
        }

        public async Task<Task> CalibrationZero()
        {
            Move(-130000, 80);
            await Task.Delay(11000);
            Move(100, 100);
            await Task.Delay(100);
            RemoveRelateLocation();
            await Task.Delay(100);
            if (Math.Abs(MotorState.RelativePosition) > 100)
            {
                RemoveRelateLocation();
                await Task.Delay(100);
            }
            return Task.CompletedTask;
        }

        public void RemoveRelateLocation()
        {
            byte[] data = new byte[16] { 0x01, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0xE4 };
            SendMsg(data);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class MotorState : ViewModelBase
    {
        public void SetData(byte[] bytes)
        {
            if (bytes.Length == 16 && bytes[1] == 101)
            {
                Address = bytes[0];
                State = bytes[3];
                AbsolutePosition = (bytes[4] << 24) | (bytes[5] << 16) | (bytes[6] << 8) | bytes[7];
                RelativePosition = (bytes[8] << 24) | (bytes[9] << 16) | (bytes[10] << 8) | bytes[11];
                DriveFrequency = (bytes[12] << 8) | bytes[13];
            }
        }


        public int Address { get => _Address; set { _Address = value; NotifyPropertyChanged(); } }
        private int _Address;

        public int State { get => _State; set { _State = value; NotifyPropertyChanged(); } }
        private int _State;

        public int AbsolutePosition { get => _AbsolutePosition; set { _AbsolutePosition = value; NotifyPropertyChanged(); } }
        private int _AbsolutePosition;

        public int RelativePosition { get => _RelativePosition; set { _RelativePosition = value; NotifyPropertyChanged(); } }
        private int _RelativePosition;

        public int DriveFrequency { get => _DriveFrequency; set { _DriveFrequency = value; NotifyPropertyChanged(); } }
        private int _DriveFrequency;

    }


    public static class Crc16
    {
        public static byte[] CRC16(byte[] input)
        {
            byte[] output = new byte[16];
            CalculateCRC(input);
            ushort crc = CalculateCRC(input);
            Array.Copy(input, output, input.Length);
            output[14] = (byte)(crc & 0xFF);
            output[15] = (byte)(crc >> 8);
            return output;
        }

        public static ushort CalculateCRC(byte[] data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }




        public static bool ValidateModbusCRC16(byte[] data)
        {
            ushort crc = 0xFFFF;

            for (int i = 0; i < data.Length - 2; i++)
            {
                crc ^= data[i];

                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) == 0x0001)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }

            return (crc == (data[data.Length - 2] + (data[data.Length - 1] << 8)));
        }


    }


}
