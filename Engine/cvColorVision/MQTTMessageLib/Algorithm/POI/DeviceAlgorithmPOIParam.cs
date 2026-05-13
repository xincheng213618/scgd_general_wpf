using System.Collections.Generic;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceAlgorithmPOIParam : DeviceAlgorithmParam
{
	public POIFilterItem FilterItem { get; set; }

	public POIReviseItem ReviseItem { get; set; }

	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public DeviceParamPOIOutput OutputDeviceParam { get; set; }

	public DeviceParamPOIFilter FilterDeviceParam { get; set; }

	public DeviceParamPOIRevise ReviseDeviceParam { get; set; }

	public List<POIPoint> Data { get; set; }

	public POIHeaderInfo POIHeader { get; set; }

	public List<POIOutputItem> OutputItem { get; set; }

	public POIStorageModel POIStorageType { get; set; }

	public string POIPointFileName { get; set; }

	public string POIReviseFileName { get; set; }

	public bool IsTwoRevise { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }

	public DeviceAlgorithmPOIParam()
	{
		Data = new List<POIPoint>();
		POIStorageType = POIStorageModel.Db;
		IsTwoRevise = false;
		IsSubPixel = false;
		IsCCTWave = true;
		OutputDeviceParam = new DeviceParamPOIOutput();
		FilterDeviceParam = new DeviceParamPOIFilter();
		ReviseDeviceParam = new DeviceParamPOIRevise();
		ReviseItem = POIReviseItem.Disable();
		FilterItem = POIFilterItem.Disable();
	}
}
