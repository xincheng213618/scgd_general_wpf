namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumEQEMeasure : DeviceCVBaseRequest<SPRequestType, SpectrumEQEMeasureParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumEQEMeasure(string deviceName, string serialNumber, int zindex, SpectrumEQEMeasureParam param)
		: base(deviceName, serialNumber, zindex, SPRequestType.MeasureEQE, param)
	{
	}
}
