using System.Drawing;
using System.Windows.Media.Imaging;

namespace CVImageChannelLib;

public class H264Reader : CVImageReaderProxy
{
	private OpenH264Coder Decoder;

	private H264ReaderProxy subscriber;

	public event H264ReaderRecvHandler H264ReaderRecv;

	public H264Reader(string localIp, int localPort)
	{
		Decoder = new OpenH264Coder();
		subscriber = new H264ReaderProxy(localIp, localPort);
		subscriber.H264PacketHandler += Subscriber_H264PacketHandler;
	}

	private void Subscriber_H264PacketHandler(H264Packet args)
	{
        Bitmap bitmap = Decoder.Decode(args.data, args.len);
		if (bitmap != null)
		{
			this.H264ReaderRecv?.Invoke(bitmap);
		}
	}

	public override Bitmap Subscribe()
	{
		return null;
	}

	public int GetLocalPort()
	{
		return subscriber.GetLocalPort();
	}

	public override void Dispose()
	{
		subscriber.Dispose();
		Decoder.Dispose();
		base.Dispose();
	}
}
