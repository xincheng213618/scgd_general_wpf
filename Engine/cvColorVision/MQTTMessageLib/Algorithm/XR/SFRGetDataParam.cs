using System.Collections.Generic;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public class SFRGetDataParam : DeviceAlgorithmParam
{
	public DeviceParamSFR DeviceParam { get; set; }

	public SFRGetDataParam()
	{
		DeviceParam = new DeviceParamSFR();
		base.POIPoints = new List<POIPointOnly>();
	}
}
