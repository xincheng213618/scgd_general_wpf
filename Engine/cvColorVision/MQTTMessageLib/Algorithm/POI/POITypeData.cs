using System;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;

namespace MQTTMessageLib.Algorithm.POI;

public struct POITypeData
{
	public POIPointTypes PointType { get; set; }

	public float Width { get; set; }

	[JsonIgnore]
	public int WidthAsInt => Convert.ToInt32(Width);

	public float Height { get; set; }

	[JsonIgnore]
	public int HeightAsInt => Convert.ToInt32(Height);
}
