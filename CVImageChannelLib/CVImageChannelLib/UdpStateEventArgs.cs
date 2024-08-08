using System;
using System.Net;

namespace CVImageChannelLib;

public class UdpStateEventArgs : EventArgs
{
	public IPEndPoint remoteEndPoint;

	public byte[] buffer = null;
}
