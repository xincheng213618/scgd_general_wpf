using CVCommCore;
using CVCommCore.Core;
using CVCommCore.CVImage;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Calibration;

public class CalibrationGetDataParam : MasterResult
{
	public int Bpp { get; set; } = 16;

	public CVImageFlipMode FlipMode { get; set; } = CVImageFlipMode.None;

	public string ImgFileName { get; set; }

	[JsonConverter(typeof(StringEnumConverter))]
	public CVFileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public CVTemplateParam ExpTemplateParam { get; set; }

	public DeviceParamCalibration DeviceParam { get; set; }

	public CVResultType PreResultType => (CVResultType)base.MasterResultType;

	public int OrderIndex { get; set; }

	public POITemplateParam POIParam { get; set; }

	public CalibrationGetDataParam()
	{
		Bpp = 16;
		FlipMode = CVImageFlipMode.None;
		ImgFileName = string.Empty;
		TemplateParam = new CVTemplateParam();
		ExpTemplateParam = new CVTemplateParam();
		FileType = CVFileExtType.Tif;
		DeviceParam = new DeviceParamCalibration();
	}
}
