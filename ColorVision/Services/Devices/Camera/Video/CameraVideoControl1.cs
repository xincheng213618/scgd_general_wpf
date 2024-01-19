#pragma warning disable CS8603  
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OpenH264Lib;

namespace ColorVision.Services.Devices.Camera.Video
{
    public class CameraVideoControl1
    {
        private const string H264DllName = "openh264-2.3.1-win64.dll";
        public OpenH264Lib.Decoder Decoder { get; set; }

        public event CameraVideoFrameHandler CameraVideoFrameReceived;
        public CameraVideoControl1()
        {
            Decoder = new OpenH264Lib.Decoder(H264DllName);

        }
        private UdpClient UdpClient { get; set; }

        private Dictionary<int, byte[]> packets;
        private int headLen = 9;

        bool OpenVideo;
        public bool Open(string Host,int Port)
        {
            try
            {
                if (UdpClient != null)
                {
                    UdpClient?.Dispose();
                }
                IPAddress locateIp = IPAddress.Parse(Host);
                IPEndPoint locatePoint = new IPEndPoint(locateIp, Port);
                UdpClient = new UdpClient(locatePoint);
                OpenVideo = true;
                headLen = 9;
                packets = new Dictionary<int, byte[]>();

                Task.Run(() =>
                {
                    while (OpenVideo)
                    {
                        try
                        {
                            if (UdpClient != null)
                            {
                                IPEndPoint remotePoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1);
                                var received = UdpClient.Receive(ref remotePoint);
                                if (received.Length > 0)
                                {
                                    var bytes = AddPacket(received);
                                    if (bytes != null)
                                    {
                                        var bmp = Decoder.Decode(bytes, bytes.Length);
                                        if (bmp != null)
                                        {
                                            Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                CameraVideoFrameReceived?.Invoke(bmp);
                                            });
                                            bmp.Dispose();
                                        }
                                    }
                                }
                            }
                            Task.Delay(1);
                        }
                        catch
                        {
                            OpenVideo = false;
                        }

                    }
                });
                return true;
            }
            catch 
            {
                OpenVideo = false;
                return false;
            }
        }

        public byte[] AddPacket(byte[] buffer)
        {
            if (buffer.Length < headLen)
                return null;

            int nBufLen = BitConverter.ToInt32(buffer, 0);     //单次数据长度
            int nConextLen = nBufLen - headLen;

            if (nConextLen <= 0 || buffer.Length < nBufLen)
                return null;

            bool bEnd = buffer[4] == 0 ? false : true;     // 结束标志位
            int nID = BitConverter.ToInt32(buffer, 5);  // 单次数据ID

            if (nID == 0)
            {
                packets.Clear();
            }

            if (packets.Count == nID)
            {
                int len = nBufLen - headLen;

                byte[] bytes;

                bytes = new byte[len];

                Buffer.BlockCopy(buffer, headLen, bytes, 0, len);
                packets.Add(nID, bytes);
            }
            else
                packets.Clear();

            if (packets.Count > 0 && bEnd)
            {
                int totalLen = 0;
                foreach (var item in packets)
                {
                    totalLen += item.Value.Length;
                }

                byte[] buf = new byte[totalLen];

                int pos = 0;
                for (int i = 0; i < packets.Count; i++)
                {
                    Buffer.BlockCopy(packets[i], 0, buf, pos, packets[i].Length);
                    pos += packets[i].Length;
                }
                packets.Clear();
                return buf;
            }
            else
            {
                return null;
            }
        }

        public void Close()
        {
            OpenVideo = false;
            if (UdpClient != null)
            {
                UdpClient?.Dispose();
            }
        }

    }
}
