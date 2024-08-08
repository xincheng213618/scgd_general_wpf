using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using log4net;

namespace CVImageChannelLib;

public class MMFWriterEx : CVImageWriterProxy
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(MMFWriterEx));

	private MMFEndpointProxy<CVImagePacket> publisher;

	public string Name { get; set; }

	public MMFWriterEx()
	{
	}


	public void Setup(string mapName)
	{
		MemoryMappedFile memoryMappedFile = null;
		Name = mapName;
		try
		{
			memoryMappedFile = MemoryMappedFile.OpenExisting(mapName);
		}
		catch (Exception ex)
		{
			logger.Error(ex.Message);
			return;
		}
		publisher = new MMFEndpointProxy<CVImagePacket>(memoryMappedFile);
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
		if (cfg.TryGetValue("mapNamePrefix", out var value))
		{
			Setup(Convert.ToString(value));
			return true;
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
