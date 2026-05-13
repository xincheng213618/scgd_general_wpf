using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEData<T> where T : IDataIndex
{
	public POIPoint Point { get; set; }

	public T Data { get; set; }

	public POIResultCIEData(POIPoint point, T data)
	{
		Point = point;
		Data = data;
	}

	public string[] GetRuleNameKey()
	{
		string[] array = null;
		int dataCount = Data.GetDataCount();
		if (dataCount > 0)
		{
			array = new string[dataCount];
			if (dataCount > 1)
			{
				for (int i = 0; i < dataCount; i++)
				{
					array[i] = $"{Point.Name}_{i}";
				}
			}
			else
			{
				array[0] = Point.Name;
			}
		}
		return array;
	}
}
