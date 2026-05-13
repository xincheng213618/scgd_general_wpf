using System.Collections.Generic;

namespace CVCommCore.CVCamera;

public struct CFW
{
	public List<SysChannelCfg> ChannelCfgs { get; set; }

	public bool IsCOM { get; set; }

	public string SzComName { get; set; }

	public uint BaudRate { get; set; }

	public bool IsNDPort { get; set; }

	public double NDMaxExpTime { get; set; }

	public double NDMinExpTime { get; set; }

	public int[] NDRate { get; set; }

	public string[] NDCaliNameGroups { get; set; }

	public bool EnableResetND { get; set; }

	public string NDBindDeviceCode { get; set; }
}
