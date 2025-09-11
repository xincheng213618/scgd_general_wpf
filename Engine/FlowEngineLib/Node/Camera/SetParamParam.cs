using System.Collections.Generic;

namespace FlowEngineLib.Node.Camera;

public class SetParamParam
{
	public List<SetParamFuncData> Func { get; set; }

	public SetParamParam(List<SetParamFuncData> data)
	{
		Func = data;
	}
}
