namespace CVCommCore.CVAlgorithm.CVArchived;

public struct SMUArchInput
{
	public bool IsSourceV { get; set; }

	public float SrcValue { get; set; }

	public SMUArchInput(sbyte isSourceV, float srcValue)
	{
		IsSourceV = isSourceV == 1;
		SrcValue = srcValue;
	}
}
