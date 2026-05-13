using System.Collections.Generic;
using System.Linq;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEXYZOnlyList
{
	public POIResultCIEXYZOnly POIThreshold { get; set; }

	public List<POIResultCIEXYZOnly> Data { get; set; }

	public POIResultDataCIExyuv Mean { get; set; }

	public POIResultCIEXYZOnlyList()
	{
		Data = new List<POIResultCIEXYZOnly>();
	}

	public POIResultCIEXYZOnlyList(List<POIResultCIEXYZOnly> data)
	{
		Data = data;
	}

	public void DoDataCalc()
	{
		Mean = new POIResultDataCIExyuv
		{
			CCT = Data.Average((POIResultCIEXYZOnly a) => a.Data.CCT),
			Wave = Data.Average((POIResultCIEXYZOnly a) => a.Data.Wave),
			X = Data.Average((POIResultCIEXYZOnly a) => a.Data.X),
			Y = Data.Average((POIResultCIEXYZOnly a) => a.Data.Y),
			Z = Data.Average((POIResultCIEXYZOnly a) => a.Data.Z),
			x = Data.Average((POIResultCIEXYZOnly a) => a.Data.x),
			y = Data.Average((POIResultCIEXYZOnly a) => a.Data.y),
			u = Data.Average((POIResultCIEXYZOnly a) => a.Data.u),
			v = Data.Average((POIResultCIEXYZOnly a) => a.Data.v)
		};
	}
}
