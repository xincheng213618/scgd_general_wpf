namespace CVCommCore;

public struct ValidateRule
{
	public float? Max { get; set; }

	public float? Min { get; set; }

	public string Equal { get; set; }

	public ushort Radix { get; set; }

	public ValidateRuleType RType { get; set; }
}
