using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Util
{
    public class UdpStateEventArgs : EventArgs 
    { 
        public IPEndPoint RemoteEndPoint { get; set; }
        public byte[] Buffer { get; set; }
    }

    public delegate void UDPReceivedEvent(UdpStateEventArgs args);

    public class UDPClientRecv:IDisposable
    {
        private UdpClient udpClient;
        public event UDPReceivedEvent UDPMessageReceived;

        public UDPClientRecv(string locateIP, int locatePort)
        {
            IPAddress locateIp = IPAddress.Parse(locateIP);
            IPEndPoint locatePoint = new IPEndPoint(locateIp, locatePort);
            udpClient = new UdpClient(locatePoint);

            //监听创建好后，创建一个线程，开始接收信息
            Task.Run(() =>
            {
                while (true)
                {
                    UdpStateEventArgs udpReceiveState = new UdpStateEventArgs();

                    if (udpClient != null)
                    {
                        IPEndPoint remotePoint = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 1);
                        var received = udpClient.Receive(ref remotePoint);
                        udpReceiveState.RemoteEndPoint = remotePoint;
                        udpReceiveState.Buffer = received;
                        UDPMessageReceived?.Invoke(udpReceiveState);
                    }
                    else
                    {
                        break;
                    }
                }
            });
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
