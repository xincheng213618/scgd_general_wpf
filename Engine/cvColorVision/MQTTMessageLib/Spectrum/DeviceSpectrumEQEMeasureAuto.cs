namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumEQEMeasureAuto : DeviceCVBaseRequest<SPRequestType, SpectrumEQEMeasureParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumEQEMeasureAuto(string deviceName, string serialNumber, int zindex, SpectrumEQEMeasureParam param)
		: base(deviceName, serialNumber, zindex, SPRequestType.EQEMeasureAuto, param)
	{
	}
}
