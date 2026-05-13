using System;

namespace MQTTMessageLib.Spectrum;

public class DeviceSpectrumSelfAdaptionInitDark : DeviceCVBaseRequest<SPRequestType, SpectrumSelfAdaptionInitDark>, IDevSpectrumRequest, IDeviceRequest
{
	public DateTime BeginTime { get; set; } = DateTime.Now;

	public DeviceSpectrumSelfAdaptionInitDark(string deviceName, string serialNumber, SpectrumSelfAdaptionInitDark param)
		: base(deviceName, serialNumber, SPRequestType.SelfAdaptionInitDark, param)
	{
	}
}
