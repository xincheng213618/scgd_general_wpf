namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumInitAutoDarkPendingResponse : MQTTCVBaseResponse<Init_Auto_Dark_PendingMsg>
{
	public MQTTSpectrumInitAutoDarkPendingResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, Init_Auto_Dark_PendingMsg data)
		: base(request, status, data)
	{
	}

	public MQTTSpectrumInitAutoDarkPendingResponse(MQTTCVRequestHeader request, DeviceSpectrumSelfAdaptionInitDarkPendingResponse devResponse)
		: base(request, new MQTTCVResponseStatus(devResponse), devResponse.Result)
	{
	}
}
