namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumZeroCalibration : DeviceCVBaseRequest<SPRequestType, SpectrumInitDarkParam>, IDevSpectrumRequest, IDeviceRequest
{
	public DeviceSpectrumZeroCalibration(string deviceName, string serialNumber, SpectrumInitDarkParam param)
		: base(deviceName, serialNumber, SPRequestType.ZeroCalibration, param)
	{
	}
}
