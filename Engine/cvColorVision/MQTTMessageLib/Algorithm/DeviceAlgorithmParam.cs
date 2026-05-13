using System.Collections.Generic;
using CVCommCore.Core;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm.POI;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmParam : DeviceAlgorithmBaseInputParam
{
	public string TargetDevicePath { get; set; }

	public string DeviceCode { get; set; }

	public string DeviceType { get; set; }

	public CVImageFileSourceType ImageFileSource { get; set; }

	public List<POIPointOnly> POIPoints { get; set; }

	public string POICanvasJsonConfig { get; set; }

	public POICanvasRefConfig POICanvasRef { get; set; }

	public DeviceAlgorithmParam()
	{
		POICanvasRef = null;
	}

	public DeviceAlgorithmParam(int templateId, string templateName)
		: base(templateId, templateName)
	{
		POICanvasRef = null;
	}
}
