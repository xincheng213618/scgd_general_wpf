using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class BlackMuraParam : AlgorithmImageParam
{
	public int[] OIndex { get; set; }

	public CVTemplateParam SavePOITemplate { get; set; }

	public SMUResultData SMUData { get; set; }

	public BlackMuraParam(string poiTempName, int[] oIndex)
	{
		SavePOITemplate = new CVTemplateParam
		{
			ID = -1,
			Name = poiTempName
		};
		OIndex = oIndex;
	}
}
