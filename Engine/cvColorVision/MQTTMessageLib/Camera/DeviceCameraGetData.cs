using System.Collections.Generic;
using MQTTMessageLib.Algorithm.POI;
using MQTTMessageLib.Calibration;

namespace MQTTMessageLib.Camera;

public class DeviceCameraGetData : DeviceCVBaseRequest<CameraRequestType, CameraGetDataParam>, IDevCameraRequest, IDeviceRequest
{
	public DeviceCalibrationRequest CalibrationRequest { get; set; }

	public DevicePOIRequest POIRequest { get; private set; }

	public DeviceParamCameraAutoExpTime AutoExpTimeCfg { get; set; }

	public Dictionary<int, string> NDCalibrationMap { get; set; }

	public bool IsNotSaveImg { get; private set; }

	public DeviceCameraGetData(string deviceCode, string serialNumber, int zindex, CameraGetDataParam param, DeviceCalibrationRequest calibrationRequest, Dictionary<int, string> _NDCalibrationMap)
		: base(deviceCode, serialNumber, zindex, CameraRequestType.GetData, param)
	{
		POIRequest = null;
		CalibrationRequest = calibrationRequest;
		IsNotSaveImg = param.ImageSaveBpp == -1;
		NDCalibrationMap = _NDCalibrationMap;
	}

	public DeviceCameraGetData(string deviceCode, string serialNumber, int zindex, CameraGetDataParam param)
		: this(deviceCode, serialNumber, zindex, param, null, new Dictionary<int, string>())
	{
	}

	public void SetPOIRequest(DevicePOIRequest POIRequest, bool isNotSaveImg)
	{
		this.POIRequest = POIRequest;
		IsNotSaveImg = isNotSaveImg;
	}

	public DevicePOIRequest BuildPOIRequest()
	{
		CameraGetDataParam cameraGetDataParam = base.Params;
		POIGetDataParam param = new POIGetDataParam
		{
			TemplateParam = cameraGetDataParam.POIParam.POI,
			FilterTemplate = cameraGetDataParam.POIParam.Filter,
			ReviseTemplate = cameraGetDataParam.POIParam.Revise
		};
		DevicePOIRequest devicePOIRequest = new DevicePOIRequest(base.DeviceCode, base.SerialNumber, base.ZIndex, param, isImageFilePreRequest: false);
		SetPOIRequest(devicePOIRequest, isNotSaveImg: true);
		return devicePOIRequest;
	}
}
