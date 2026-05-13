using CVCommCore.CVImage;
using MQTTMessageLib.Algorithm.POI;

namespace MQTTMessageLib.Calibration;

public class DeviceCalibrationRequest : DeviceCVBaseRequest<CalibrationRequestType, CalibrationGetDataParam>, IDevCalibrationRequest, IDeviceRequest
{
	public bool IsImageFilePreRequest { get; set; }

	public IDeviceResponse CameraResponse { get; set; }

	public CVCIEFileInfo DataBuffer { get; set; }

	public DevicePOIRequest POIRequest { get; private set; }

	public bool IsNotSaveImg { get; set; }

	public bool IsSaveCIE { get; set; }

	public bool IsNeedPersistence { get; set; } = true;

	public int? NDPort { get; set; }

	public DeviceCalibrationRequest(string deviceName, string serialNumber, int zindex, CalibrationGetDataParam param)
		: base(deviceName, serialNumber, zindex, CalibrationRequestType.Calibration_GetData, param)
	{
		IsImageFilePreRequest = true;
		IsNotSaveImg = false;
		IsSaveCIE = true;
		NDPort = null;
	}

	public void SetPOIRequest(DevicePOIRequest POIRequest, bool isNotSaveImg, bool isSaveCIE)
	{
		this.POIRequest = POIRequest;
		IsNotSaveImg = isNotSaveImg;
		IsSaveCIE = isSaveCIE;
	}

	public DevicePOIRequest BuildPOIRequest()
	{
		CalibrationGetDataParam calibrationGetDataParam = base.Params;
		POIGetDataParam param = new POIGetDataParam
		{
			TemplateParam = calibrationGetDataParam.POIParam.POI,
			FilterTemplate = calibrationGetDataParam.POIParam.Filter,
			ReviseTemplate = calibrationGetDataParam.POIParam.Revise
		};
		DevicePOIRequest devicePOIRequest = new DevicePOIRequest(base.DeviceCode, base.SerialNumber, base.ZIndex, param, isImageFilePreRequest: false);
		SetPOIRequest(devicePOIRequest, isNotSaveImg: false, isSaveCIE: false);
		return devicePOIRequest;
	}
}
