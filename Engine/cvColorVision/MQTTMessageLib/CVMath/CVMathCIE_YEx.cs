using System;
using System.Collections.Generic;
using System.Linq;
using CVCommCore;
using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.CVMath;

public class CVMathCIE_YEx : CVPOIMath<POIResultDataCIEYEx>
{
	public override Dictionary<DataMetricsModel, POIResultDataCIEYEx> DoCalc(List<POIResultCIE<POIResultDataCIEYEx>> Data)
	{
		Dictionary<DataMetricsModel, POIResultDataCIEYEx> dictionary = new Dictionary<DataMetricsModel, POIResultDataCIEYEx>();
		int count = Data[0].Data.Y.Count;
		double[] array = new double[count];
		float[] array2 = new float[count];
		float[] array3 = new float[count];
		int num = 0;
		List<float> list = new List<float>();
		int i;
		for (i = 0; i < count; i++)
		{
			array2[i] = Data.Max((POIResultCIE<POIResultDataCIEYEx> a) => a.Data.Y[i]);
			array3[i] = Data.Min((POIResultCIE<POIResultDataCIEYEx> a) => a.Data.Y[i]);
			list.Add(Data.Average((POIResultCIE<POIResultDataCIEYEx> a) => a.Data.Y[i]));
		}
		POIResultDataCIEYEx pOIResultDataCIEYEx = new POIResultDataCIEYEx(list);
		dictionary.Add(DataMetricsModel.Mean, pOIResultDataCIEYEx);
		for (int num2 = 0; num2 < count; num2++)
		{
			array[num2] = 0.0;
			foreach (POIResultCIE<POIResultDataCIEYEx> Datum in Data)
			{
				array[num2] += Math.Pow(Datum.Data.Y[num2] - pOIResultDataCIEYEx.Y[num2], 2.0);
			}
		}
		num = Data.Count - 1;
		list = new List<float>();
		for (int num3 = 0; num3 < count; num3++)
		{
			list.Add((float)(array[num3] / (double)num));
		}
		POIResultDataCIEYEx pOIResultDataCIEYEx2 = new POIResultDataCIEYEx(list);
		dictionary.Add(DataMetricsModel.Variance, pOIResultDataCIEYEx2);
		list = new List<float>();
		for (int num4 = 0; num4 < count; num4++)
		{
			list.Add((float)Math.Sqrt(pOIResultDataCIEYEx2.Y[num4]));
		}
		POIResultDataCIEYEx value = new POIResultDataCIEYEx(list);
		dictionary.Add(DataMetricsModel.StandardDeviation, value);
		list = new List<float>();
		for (int num5 = 0; num5 < count; num5++)
		{
			list.Add(array3[num5] / array2[num5]);
		}
		POIResultDataCIEYEx value2 = new POIResultDataCIEYEx(list);
		dictionary.Add(DataMetricsModel.Uniformity, value2);
		list = new List<float>();
		for (int num6 = 0; num6 < count; num6++)
		{
			list.Add((array2[num6] - array3[num6]) / pOIResultDataCIEYEx.Y[num6]);
		}
		POIResultDataCIEYEx value3 = new POIResultDataCIEYEx(list);
		dictionary.Add(DataMetricsModel.Repeatability, value3);
		return dictionary;
	}
}
