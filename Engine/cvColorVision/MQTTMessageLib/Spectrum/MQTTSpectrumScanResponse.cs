using System.Collections.Generic;

namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumScanResponse : MQTTCVBaseResponse<List<string>>
{
	public MQTTSpectrumScanResponse(MQTTCVRequestHeader request, DeviceSpectrumScanResponse response)
		: base(request, new MQTTCVResponseStatus(response), response.Result)
	{
	}
}
