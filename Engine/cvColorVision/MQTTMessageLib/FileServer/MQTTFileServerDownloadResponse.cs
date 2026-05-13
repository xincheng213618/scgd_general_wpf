namespace MQTTMessageLib.FileServer;

public class MQTTFileServerDownloadResponse : MQTTCVBaseResponse<DeviceFileUpdownParam>
{
	public MQTTFileServerDownloadResponse(MQTTCVRequestHeader request, DeviceFileServerDownloadResponse respone)
		: base(request, new MQTTCVResponseStatus(respone.Code, respone.Desc), respone.Data)
	{
	}
}
