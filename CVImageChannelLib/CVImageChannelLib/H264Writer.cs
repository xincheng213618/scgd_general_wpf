#pragma warning disable CA1816,CA2201,CS8604

using System;
using System.Collections.Generic;
using log4net;

namespace CVImageChannelLib;

public class H264Writer : CVImageWriterProxy
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(H264Writer));

	private H264WriterProxy publisher;

	public bool IsValid => publisher != null;

	public H264Writer(string localIp, int localPort)
	{
		publisher = new H264WriterProxy(new OpenH264OutputStream(localIp, localPort));
	}

	public void Setup(int width, int height, float resizeRatio)
	{
		if (publisher != null)
		{
			publisher.Setup(width, height, resizeRatio);
		}
	}

	public void SetRemotePoint(string remoteIP, int remotePort)
	{
		if (publisher != null)
		{
			publisher.SetRemotePoint(remoteIP, remotePort);
			return;
		}
		throw new Exception("Notsetup");
	}

	public void Setup(string localIp, int localPort, int width, int height, float resizeRatio)
	{
		publisher = new H264WriterProxy(new OpenH264OutputStream(localIp, localPort, width, height, resizeRatio));
	}

	public override void Publish(CVImagePacket packet)
	{
		if (publisher != null)
		{
			publisher.Publish(packet);
		}
	}

	public override bool Setup(Dictionary<string, object> cfg)
	{
		if (cfg.TryGetValue("width", out var value) && cfg.TryGetValue("height", out var value2) && cfg.TryGetValue("resizeRatio", out var value3))
		{
			Setup(Convert.ToInt32(value), Convert.ToInt32(value2), Convert.ToSingle(value3));
			if (cfg.TryGetValue("remoteIP", out var value4) && cfg.TryGetValue("remotePort", out var value5))
			{
				SetRemotePoint(Convert.ToString(value4), Convert.ToInt32(value5));
				return true;
			}
		}
		return false;
	}

	public override void Dispose()
	{
		if (publisher != null)
		{
			publisher.Dispose();
		}
		base.Dispose();
	}
}
