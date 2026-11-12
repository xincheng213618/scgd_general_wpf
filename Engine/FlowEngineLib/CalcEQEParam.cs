using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class CalcEQEParam : AlgorithmBaseParam
{
	public SMUResultData SMUData { get; set; } = new();

	// Older algorithm services still read this field from the request.
	public int SMU_MasterId
	{
		get => SMUData?.MasterId ?? 0;
		set
		{
			SMUData ??= new SMUResultData();
			SMUData.MasterId = value;
		}
	}
}
