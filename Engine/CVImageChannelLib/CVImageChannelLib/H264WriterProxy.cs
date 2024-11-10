using System;

namespace CVImageChannelLib;

public class H264WriterProxy : CVEndpointProxy
{
	protected OpenH264OutputStream H264Stream { get; set; }

	public H264WriterProxy(OpenH264OutputStream stream)
		: base(stream)
	{
		H264Stream = stream;
	}

	public void Publish(CVImagePacket packet)
	{
		if (H264Stream != null)
		{
			H264Stream.Encode(packet);
		}
	}

	public override void Dispose()
	{
		H264Stream.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }

    public void SetRemotePoint(string remoteIP, int remotePort)
	{
		H264Stream.SetRemotePoint(remoteIP, remotePort);
	}

	public void Setup(int width, int height, float resizeRatio)
	{
		H264Stream.Setup(width, height, resizeRatio);
	}
}
