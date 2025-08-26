namespace FlowEngineLib.Algorithm;

public class DeviceFlowInentify
{
	public string DeviceCode { get; set; }

	public string SerialNumber { get; set; }

	public int ZIndex { get; set; }

	public DeviceFlowInentify()
	{
	}

	public DeviceFlowInentify(string serialNumber, int zIndex)
	{
		SerialNumber = serialNumber;
		ZIndex = zIndex;
	}

	public DeviceFlowInentify(string deviceCode, string serialNumber, int zIndex)
		: this(serialNumber, zIndex)
	{
		DeviceCode = deviceCode;
	}
}
