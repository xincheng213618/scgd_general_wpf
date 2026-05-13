using System.Collections.Generic;

namespace CVCommCore.CVCamera;

public struct PhyCameraSDKSysConfigFile
{
	public SysCameraSDKCfg cameraCfg { get; set; }

	public List<SysChannelCfg> channelCfg { get; set; }
}
