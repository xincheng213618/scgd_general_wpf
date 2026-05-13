using System.Collections.Generic;

namespace MQTTMessageLib.PG;

public class PGRequestExecCmdParam
{
	public string Name { get; set; }

	public Dictionary<string, object> Params { get; set; }
}
