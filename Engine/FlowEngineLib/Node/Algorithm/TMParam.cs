using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class TMParam : AlgorithmImageParam
{
	public string TemplateFile { get; set; }

	public TMParam(string templateFile)
	{
		TemplateFile = templateFile;
	}
}
