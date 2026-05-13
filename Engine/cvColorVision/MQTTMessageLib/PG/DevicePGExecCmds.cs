using System.Collections.Generic;

namespace MQTTMessageLib.PG;

public class DevicePGExecCmds : DeviceCVBaseRequest<PGRequestType, List<PGRequestExecCmdParam>>, IDevPGRequest, IDeviceRequest
{
	public DevicePGExecCmds(string deviceName, string serialNumber, List<PGRequestExecCmdParam> param)
		: base(deviceName, serialNumber, PGRequestType.ExecCmds, param)
	{
	}
}
