using FlowEngineLib.Base;
using Newtonsoft.Json;

namespace FlowEngineLib.Node.Spectrum;

public class SpectrumNodeProperty : CVBaseDeviceParam<SPCommCmdType, SpectrumParamData>, ILoopNodeProperty
{
	public string[] ToItemArray(int no)
	{
		if (base.Data == null)
		{
			return new string[2]
			{
				no.ToString(),
				base.Cmd.ToString()
			};
		}
		return new string[3]
		{
			no.ToString(),
			base.Cmd.ToString(),
			JsonConvert.SerializeObject(base.Data)
		};
	}
}
