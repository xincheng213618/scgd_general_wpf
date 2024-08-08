using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CVImageChannelLib;

public class UDPClientRecv
{
	private bool isStop;

	public UdpClient udpClient { get; set; }

	public event UDPReceivedEventHandler UDPMessageReceived;

	public UDPClientRecv(string locateIP, int locatePort)
	{
		IPAddress address = IPAddress.Parse(locateIP);
		IPEndPoint localEP = new IPEndPoint(address, locatePort);
		udpClient = new UdpClient(localEP);
		isStop = false;
		Task.Run(delegate
		{
			if (udpClient != null)
			{
				while (!isStop)
				{
					UdpStateEventArgs udpStateEventArgs = new UdpStateEventArgs();
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("1.1.1.1"), 1);
					byte[] buffer = udpClient.Receive(ref remoteEP);
					udpStateEventArgs.remoteEndPoint = remoteEP;
					udpStateEventArgs.buffer = buffer;
					this.UDPMessageReceived?.Invoke(udpStateEventArgs);
					Task.Delay(1);
				}
				udpClient.Close();
				udpClient.Dispose();
				udpClient = null;
			}
		});
	}

	public void Close()
	{
		isStop = true;
		Thread.Sleep(500);
	}

	public int Send(byte[] bytes, int len, IPEndPoint remotePoint)
	{
		if (udpClient == null)
		{
			return -1;
		}
		return udpClient.Send(bytes, len, remotePoint);
	}

	public int GetLocalPort()
	{
		return (udpClient.Client.LocalEndPoint is IPEndPoint iPEndPoint) ? iPEndPoint.Port : (-1);
	}

	public void Dispose()
	{
		if (udpClient != null)
		{
			Close();
		}
	}
}
