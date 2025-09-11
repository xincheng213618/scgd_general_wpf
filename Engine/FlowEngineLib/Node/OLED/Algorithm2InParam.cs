using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class Algorithm2InParam : AlgorithmPreStepParam
{
	public int OrderIndex { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public int POI_MasterId { get; set; }

	public bool IsAdd { get; set; }

	public int BufferLen { get; set; }

	public Algorithm2InParam(string tempName, bool isAdd, int poi_mid, int orderIndex, int bufferLen)
	{
		POI_MasterId = poi_mid;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
		IsAdd = isAdd;
		OrderIndex = orderIndex;
		BufferLen = bufferLen;
	}
}
