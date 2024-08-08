namespace CVImageChannelLib;

public class H264ReaderProxy : CVEndpointProxy
{
	protected OpenH264InputStream h264Stream;

	public event H264PacketHandler H264PacketHandler;

	public H264ReaderProxy(string localIp, int localPort)
		: this(new OpenH264InputStream(localIp, localPort))
	{
	}

	public H264ReaderProxy(OpenH264InputStream stream)
		: base(stream)
	{
		h264Stream = stream;
		h264Stream.H264Received += H264Stream_H264Received;
	}

	private void H264Stream_H264Received(H264StateEventArgs args)
	{
		this.H264PacketHandler?.Invoke(new H264Packet
		{
			len = args.packet.Length,
			data = args.packet
		});
	}

	public int GetLocalPort()
	{
		return h264Stream.GetUdp().GetLocalPort();
	}

	public H264Packet Subscribe()
	{
		byte[] array = h264Stream.ReadBuffer();
		H264Packet result = null;
		if (array != null)
		{
			result = new H264Packet
			{
				data = array,
				len = array.Length
			};
		}
		return result;
	}

	public override void Dispose()
	{
		h264Stream.Dispose();
		base.Dispose();
	}
}
