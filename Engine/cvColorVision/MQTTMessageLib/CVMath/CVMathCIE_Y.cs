using System;
using System.Collections.Generic;
using System.Linq;
using CVCommCore;
using log4net;
using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.CVMath;

public class CVMathCIE_Y : CVPOIMath<POIResultDataCIEY>
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVMathCIE_Y));

	public override Dictionary<DataMetricsModel, POIResultDataCIEY> DoCalc(List<POIResultCIE<POIResultDataCIEY>> Data)
	{
		Dictionary<DataMetricsModel, POIResultDataCIEY> dictionary = new Dictionary<DataMetricsModel, POIResultDataCIEY>();
		float num = Data.Max((POIResultCIE<POIResultDataCIEY> a) => a.Data.Y);
		float num2 = Data.Min((POIResultCIE<POIResultDataCIEY> a) => a.Data.Y);
		POIResultDataCIEY pOIResultDataCIEY = new POIResultDataCIEY(Data.Average((POIResultCIE<POIResultDataCIEY> a) => a.Data.Y));
		dictionary.Add(DataMetricsModel.Mean, pOIResultDataCIEY);
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("Math calc => max_lv={0}, min_lv={1}, mean_lv={2}", num, num2, pOIResultDataCIEY.Y);
		}
		double num3 = 0.0;
		foreach (POIResultCIE<POIResultDataCIEY> Datum in Data)
		{
			num3 += Math.Pow(Datum.Data.Y - pOIResultDataCIEY.Y, 2.0);
		}
		POIResultDataCIEY pOIResultDataCIEY2 = new POIResultDataCIEY((float)(num3 / (double)(Data.Count - 1)));
		dictionary.Add(DataMetricsModel.Variance, pOIResultDataCIEY2);
		POIResultDataCIEY value = new POIResultDataCIEY((float)Math.Sqrt(pOIResultDataCIEY2.Y));
		dictionary.Add(DataMetricsModel.StandardDeviation, value);
		POIResultDataCIEY value2 = new POIResultDataCIEY(num2 / num);
		dictionary.Add(DataMetricsModel.Uniformity, value2);
		POIResultDataCIEY value3 = new POIResultDataCIEY((num - num2) / pOIResultDataCIEY.Y);
		dictionary.Add(DataMetricsModel.Repeatability, value3);
		return dictionary;
	}
}
