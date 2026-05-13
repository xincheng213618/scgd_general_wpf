namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumGetParam : DeviceCVBaseNoParamRequest<SPRequestType>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumGetParam(string deviceName, string serialNumber)
		: base(deviceName, serialNumber, SPRequestType.GetSysParam)
	{
	}
}
