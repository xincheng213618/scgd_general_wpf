using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CVImageChannelLib;
using log4net;
using OpenCvSharp.Extensions;
using OpenH264Lib;

namespace ColorVision.Device.Camera.Video
{

    public delegate void CameraVideoFrameHandler(System.Drawing.Bitmap bitmap);

    public class CameraVideoControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CameraVideoControl));

        public SoftwareConfig SoftwareConfig { get; set; }

        private const string H264DllName = "openh264-2.3.1-win64.dll";
        public OpenH264Lib.Decoder Decoder { get; set; }

        public event CameraVideoFrameHandler CameraVideoFrameReceived;
        private CVImageReaderProxy cvImgReader;
        public CameraVideoControl()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            Decoder = new OpenH264Lib.Decoder(H264DllName);
            OpenH264Lib.Encoder encoder = new OpenH264Lib.Encoder(H264DllName);
        }
        private UdpClient UdpClient { get; set; }

        private Dictionary<int, List<byte[]>> packets;
        private int headLen;

        bool OpenVideo;
        public int Open(string Host, int Port, string name)
        {
            int ret = 0;
            if (Host == "127.0.0.1") ret = OpenMMF(name);
            else ret = OpenH264(Host, Port);
            return ret;
        }
        private int OpenMMF(string DevName)
        {
            OpenVideo = true;
            cvImgReader = new MMFReader(DevName);
            return 1;
        }
        private int OpenH264(string Host, int Port)
        {
            //try
            //{
            //    H264Reader reader = new H264Reader(Host, Port);
            //    cvImgReader = reader;
            //    OpenVideo = true;
            //    return reader.GetLocalPort();
            //}
            //catch (Exception ex)
            //{
            //    OpenVideo = false;
            //    log.Error(ex.Message);
            //    return -1;
            //}
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
                headLen = 4;
                packets = new Dictionary<int, List<byte[]>>();
                var ep = UdpClient.Client.LocalEndPoint as IPEndPoint;
                return ep.Port;
            }
            catch (Exception ex)
            {
                OpenVideo = false;
                log.Error(ex);
                return -1;
            }
        }

        private void StartH264()
        {
            while (OpenVideo)
            {
                try
                {
                    if (UdpClient != null)
                    {
                        IPEndPoint remotePoint = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 1);
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
        }

        public void Start()
        {
            Task.Run(() =>
            {
                if (UdpClient != null) StartH264();
                else StartMMF();
            });
        }

        private void StartMMF()
        {
            while (OpenVideo)
            {
                try
                {
                    var bmp = cvImgReader.Subscribe();
                    if (bmp != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CameraVideoFrameReceived?.Invoke(bmp);
                        });
                        bmp.Dispose();
                    }
                    Task.Delay(1);
                }
                catch (Exception ex)
                {
                    //OpenVideo = false;
                    //break;
                }
            }
        }


        public byte[]? AddPacket(byte[] buffer)
        {
            byte[] bytes;
            Console.WriteLine("{0} => UDPMessageReceived={1}", DateTime.Now.ToString("mm:ss.ffff"), buffer[3]);
            if (buffer[0] == 0)
            {
                int len = buffer.Length - headLen;
                bytes = new byte[len];
                Buffer.BlockCopy(buffer, headLen, bytes, 0, len);
                return bytes;
            }
            else
            {
                int len = buffer.Length - headLen;
                bytes = new byte[len];
                Buffer.BlockCopy(buffer, headLen, bytes, 0, len);
                if (!packets.ContainsKey(buffer[0]))
                {
                    packets.Add(buffer[0], new List<byte[]>());
                }
                packets[buffer[0]].Add(bytes);
                //Console.WriteLine("key={0},list={1},head={2}/{3}", args.Buffer[0], Data[args.Buffer[0]].Count, args.Buffer[1], args.Buffer[2]);
                if (packets[buffer[0]].Count == buffer[1])
                {
                    int totalLen = 0;
                    foreach (var item in packets[buffer[0]])
                    {
                        totalLen += item.Length;
                    }

                    bytes = new byte[totalLen];
                    int pos = 0;
                    for (int i = 0; i < buffer[1]; i++)
                    {
                        Buffer.BlockCopy(packets[buffer[0]][i], 0, bytes, pos, packets[buffer[0]][i].Length);
                        pos += packets[buffer[0]][i].Length;
                    }
                    //////////////////////////////////////
                    packets.Remove(buffer[0]);
                    Console.WriteLine("Remove key={0}", buffer[0]);
                    return bytes;
                }
            }

            return null;
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
