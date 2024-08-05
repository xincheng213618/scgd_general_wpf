namespace CVImageChannelLib;

public class H264WriterProxy : CVEndpointProxy
{
	protected OpenH264OutputStream h264Stream;

	public H264WriterProxy(OpenH264OutputStream stream)
		: base(stream)
	{
		h264Stream = stream;
	}

	public void Publish(CVImagePacket packet)
	{
		if (h264Stream != null)
		{
			h264Stream.Encode(packet);
		}
	}

	public override void Dispose()
	{
		h264Stream.Dispose();
		base.Dispose();
	}

	public void SetRemotePoint(string remoteIP, int remotePort)
	{
		h264Stream.SetRemotePoint(remoteIP, remotePort);
	}

	public void Setup(int width, int height, float resizeRatio)
	{
		h264Stream.Setup(width, height, resizeRatio);
	}
}
