using System;
using System.Collections.Generic;
using System.Linq;
using CVCommCore;
using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.CVMath;

public class CVMathJND : CVPOIMath<POIResultDataJND>
{
	public override Dictionary<DataMetricsModel, POIResultDataJND> DoCalc(List<POIResultCIE<POIResultDataJND>> Data)
	{
		Dictionary<DataMetricsModel, POIResultDataJND> dictionary = new Dictionary<DataMetricsModel, POIResultDataJND>();
		int num = 0;
		float num2 = Data.Max((POIResultCIE<POIResultDataJND> a) => a.Data.h_jnd);
		float num3 = Data.Max((POIResultCIE<POIResultDataJND> a) => a.Data.v_jnd);
		float num4 = Data.Min((POIResultCIE<POIResultDataJND> a) => a.Data.h_jnd);
		float num5 = Data.Min((POIResultCIE<POIResultDataJND> a) => a.Data.v_jnd);
		POIResultDataJND pOIResultDataJND = new POIResultDataJND
		{
			h_jnd = Data.Average((POIResultCIE<POIResultDataJND> a) => a.Data.h_jnd),
			v_jnd = Data.Average((POIResultCIE<POIResultDataJND> a) => a.Data.v_jnd)
		};
		dictionary.Add(DataMetricsModel.Mean, pOIResultDataJND);
		double num6 = 0.0;
		double num7 = 0.0;
		foreach (POIResultCIE<POIResultDataJND> Datum in Data)
		{
			num6 += Math.Pow(Datum.Data.h_jnd - pOIResultDataJND.h_jnd, 2.0);
			num7 += Math.Pow(Datum.Data.v_jnd - pOIResultDataJND.v_jnd, 2.0);
		}
		num = Data.Count - 1;
		POIResultDataJND pOIResultDataJND2 = new POIResultDataJND
		{
			h_jnd = (float)(num6 / (double)num),
			v_jnd = (float)(num7 / (double)num)
		};
		dictionary.Add(DataMetricsModel.Variance, pOIResultDataJND2);
		POIResultDataJND value = new POIResultDataJND
		{
			h_jnd = (float)Math.Sqrt(pOIResultDataJND2.h_jnd),
			v_jnd = (float)Math.Sqrt(pOIResultDataJND2.v_jnd)
		};
		dictionary.Add(DataMetricsModel.StandardDeviation, value);
		POIResultDataJND value2 = new POIResultDataJND
		{
			h_jnd = num4 / num2,
			v_jnd = num5 / num3
		};
		dictionary.Add(DataMetricsModel.Uniformity, value2);
		POIResultDataJND value3 = new POIResultDataJND
		{
			h_jnd = num2 - num4,
			v_jnd = num3 - num5
		};
		dictionary.Add(DataMetricsModel.Repeatability, value3);
		return dictionary;
	}
}
