namespace MQTTMessageLib.FileServer;

public class MQTTFileServerListAllFilesResponse : MQTTCVBaseResponse<DeviceListAllFilesParam>
{
	public MQTTFileServerListAllFilesResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, DeviceListAllFilesParam data)
		: base(request, status, data)
	{
	}

	public MQTTFileServerListAllFilesResponse(MQTTCVRequestHeader request, DeviceFileServerListAllFilesResponse respone)
		: base(request, new MQTTCVResponseStatus(respone.Code, respone.Desc), respone.Data)
	{
	}
}
