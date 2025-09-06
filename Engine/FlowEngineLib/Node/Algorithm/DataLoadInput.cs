namespace FlowEngineLib.Node.Algorithm;

public class DataLoadInput
{
	public string DeviceCode { get; set; }

	public string SerialNumber { get; set; }

	public string ResultType { get; set; }

	public int ZIndex { get; set; }

	public DataLoadInput(string deviceCode, string serialNumber, CVResultType resultType, int zIndex)
	{
		DeviceCode = deviceCode;
		SerialNumber = serialNumber;
		ResultType = resultType.ToString();
		ZIndex = zIndex;
	}
}
