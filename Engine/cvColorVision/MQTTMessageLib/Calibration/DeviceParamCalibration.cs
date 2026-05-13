using System.Collections.Generic;
using Newtonsoft.Json;

namespace MQTTMessageLib.Calibration;

public class DeviceParamCalibration
{
	public CVTemplateParam TemplateParam { get; set; }

	public string ImgFileName { get; set; }

	public float gain { get; set; }

	public float[] exp { get; set; }

	[JsonIgnore]
	public CalibrationHandler calibrationHandler { get; private set; }

	public void InitParam(Dictionary<string, KeyValuePair<string, string>> valuePairs)
	{
		calibrationHandler = new CalibrationHandler();
		CalibrationParamConst.InitGroup(valuePairs, calibrationHandler);
		CalibrationParamConst.InitParam(valuePairs, calibrationHandler.ItemList);
		CalibrationParamConst.InitColorParam(valuePairs, calibrationHandler.CIEItemList);
	}
}
