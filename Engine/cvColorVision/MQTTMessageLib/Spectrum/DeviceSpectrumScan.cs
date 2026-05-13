namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumScan : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumScan(string deviceName, string serialNumber, int zindex)
		: base(deviceName, serialNumber, zindex, SPRequestType.Scan)
	{
		base.NeedAuth = false;
	}
}
