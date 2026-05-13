namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumSetParam : DeviceCVBaseRequest<SPRequestType, SpectrumSysParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumSetParam(string deviceName, string serialNumber, SpectrumSysParam param)
		: base(deviceName, serialNumber, SPRequestType.SetSysParam, param)
	{
	}
}
