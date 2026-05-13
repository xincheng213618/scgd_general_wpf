using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEOnly<T>
{
	public POIPointOnly Point { get; set; }

	public T Data { get; set; }

	public POIResultCIEOnly(POIPointOnly point, T data)
	{
		Point = point;
		Data = data;
	}
}
