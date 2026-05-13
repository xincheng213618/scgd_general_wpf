using System.Collections.Generic;

namespace MQTTMessageLib.PG;

public class MQTTPGExecCmd : MQTTCVBaseRequest<List<PGRequestExecCmdParam>>
{
	public MQTTPGExecCmd(string serviceName, string serialNumber, List<PGRequestExecCmdParam> data)
		: base(serviceName, "ExecCmd", serialNumber, data)
	{
	}
}
