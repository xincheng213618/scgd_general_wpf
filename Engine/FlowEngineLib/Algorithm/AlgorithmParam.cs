namespace FlowEngineLib.Algorithm;

public class AlgorithmParam : AlgorithmImageParam
{
	public string OutputFileName { get; set; }

	public bool IsInversion { get; set; }

	public int BufferLen { get; set; }

	public SMUResultData SMUData { get; set; }

	public AlgorithmParam()
	{
		base.FileType = FileExtType.None;
		base.TemplateParam = new CVTemplateParam
		{
			ID = -1
		};
		SMUData = null;
	}

	public AlgorithmParam(AlgorithmPreStepParam param, string globalVariableName)
		: base(param)
	{
		SMUData = null;
	}
}
