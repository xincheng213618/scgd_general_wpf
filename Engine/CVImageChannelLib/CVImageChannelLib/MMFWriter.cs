#pragma warning disable CA1816,CS8604

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.AccessControl;
using System.Security.Principal;
using log4net;

namespace CVImageChannelLib;

public class MMFWriter : CVImageWriterProxy
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(MMFWriter));

	private MMFEndpointProxy<CVImagePacket> publisher;

	private bool IsNeedResize;

	private float ResizeRatio;

	public string Name { get; set; }

	public bool IsValid => publisher != null;

	public MMFWriter()
	{
		IsNeedResize = false;
	}

	public MMFWriter(string mapNamePrefix, long capacity)
		: this()
	{
		SetupService(mapNamePrefix, capacity);
	}

	public void Setup(string mapName, long capacity)
	{
		MemoryMappedFile memoryMappedFile = null;
		Name = mapName;
		try
		{
			memoryMappedFile = MemoryMappedFile.CreateNew(mapName, capacity);
		}
		catch (Exception ex)
		{
			logger.Error(ex.Message);
			return;
		}
		publisher = new MMFEndpointProxy<CVImagePacket>(memoryMappedFile);
	}

	private void SetupService(string mapNamePrefix, long capacity)
	{
        string mapName = mapNamePrefix + typeof(CVImagePacket).Name;
        MemoryMappedFile memoryMappedFile = null;

        try
        {
            // 尝试打开现有的内存映射文件
            memoryMappedFile = MemoryMappedFile.OpenExisting(mapName);
            logger.InfoFormat("MMF OpenExisting {0}", mapName);
        }
        catch (Exception ex)
        {
            logger.Error(ex.Message);
            return;
        }
        // 获取文件路径（假设内存映射文件是基于文件系统的）
        string filePath = "path_to_your_memory_mapped_file"; // 需要替换为实际路径

        // 设置文件系统级别的访问控制
        var fileSecurity = new FileSecurity();
        var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        fileSecurity.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.FullControl, AccessControlType.Allow));

        // 使用 FileInfo 类设置访问控制
        var fileInfo = new FileInfo(filePath);
        fileInfo.SetAccessControl(fileSecurity);

        // 使用你的自定义 MMFEndpointProxy 类
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
		if (cfg.TryGetValue("mapNamePrefix", out var value) && cfg.TryGetValue("capacity", out var value2))
		{
			Setup(Convert.ToString(value), Convert.ToInt64(value2));
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

	public void Setup(int width, int height, float resizeRatio)
	{
		ResizeRatio = resizeRatio;
		IsNeedResize = (double)Math.Abs(resizeRatio) > 9E-07 && Math.Abs((double)resizeRatio - 1.0) > 9E-07;
		logger.DebugFormat("IsNeedResize={0},ResizeRatio={1}", IsNeedResize, ResizeRatio);
	}
}
