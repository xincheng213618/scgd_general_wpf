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
        public IPEndPoint remoteEndPoint;
        public byte[] buffer = null;
    }

    public delegate void UDPReceivedEventHandler(UdpStateEventArgs args);

    public class UDPClientRecv
    {
        private UdpClient udpClient;
        public event UDPReceivedEventHandler UDPMessageReceived;

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
                        udpReceiveState.remoteEndPoint = remotePoint;
                        udpReceiveState.buffer = received;
                        UDPMessageReceived?.Invoke(udpReceiveState);
                    }
                    else
                    {
                        break;
                    }
                }
            });
        }
    }
}
