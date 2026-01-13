using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class KBParam : AlgorithmImageParam
{
	public CVTemplateParam CaliTemplate { get; set; }

	public KBParam(string caliTemplate)
	{
		CaliTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = caliTemplate
		};
	}
}
