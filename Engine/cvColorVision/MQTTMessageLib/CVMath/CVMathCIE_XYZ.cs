using System;
using System.Collections.Generic;
using System.Linq;
using CVCommCore;
using log4net;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;

namespace MQTTMessageLib.CVMath;

public class CVMathCIE_XYZ : CVPOIMath<POIResultDataCIExyuv>
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVMathCIE_XYZ));

	public override Dictionary<DataMetricsModel, POIResultDataCIExyuv> DoCalc(List<POIResultCIE<POIResultDataCIExyuv>> Data)
	{
		Dictionary<DataMetricsModel, POIResultDataCIExyuv> dictionary = new Dictionary<DataMetricsModel, POIResultDataCIExyuv>();
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 0.0;
		double num8 = 0.0;
		double num9 = 0.0;
		int num10 = 0;
		POIResultDataCIExyuv pOIResultDataCIExyuv = new POIResultDataCIExyuv
		{
			x = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v),
			X = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			CCT = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave)
		};
		POIResultDataCIExyuv pOIResultDataCIExyuv2 = new POIResultDataCIExyuv
		{
			x = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v),
			X = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			CCT = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave)
		};
		POIResultDataCIExyuv pOIResultDataCIExyuv3 = new POIResultDataCIExyuv
		{
			x = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v),
			X = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			CCT = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave)
		};
		dictionary.Add(DataMetricsModel.Mean, pOIResultDataCIExyuv3);
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("Math calc => CIE_Max={0}, CIE_Min={1}, CIE_Mean={2}", JsonConvert.SerializeObject(pOIResultDataCIExyuv), JsonConvert.SerializeObject(pOIResultDataCIExyuv2), JsonConvert.SerializeObject(pOIResultDataCIExyuv3));
		}
		num = 0.0;
		num2 = 0.0;
		num3 = 0.0;
		num4 = 0.0;
		num5 = 0.0;
		num6 = 0.0;
		num7 = 0.0;
		num8 = 0.0;
		num9 = 0.0;
		foreach (POIResultCIE<POIResultDataCIExyuv> Datum in Data)
		{
			num += Math.Pow(Datum.Data.x - pOIResultDataCIExyuv3.x, 2.0);
			num2 += Math.Pow(Datum.Data.y - pOIResultDataCIExyuv3.y, 2.0);
			num3 += Math.Pow(Datum.Data.u - pOIResultDataCIExyuv3.u, 2.0);
			num4 += Math.Pow(Datum.Data.v - pOIResultDataCIExyuv3.v, 2.0);
			num5 += Math.Pow(Datum.Data.Y - pOIResultDataCIExyuv3.Y, 2.0);
			num6 += Math.Pow(Datum.Data.X - pOIResultDataCIExyuv3.X, 2.0);
			num7 += Math.Pow(Datum.Data.Z - pOIResultDataCIExyuv3.Z, 2.0);
			num8 += Math.Pow(Datum.Data.CCT - pOIResultDataCIExyuv3.CCT, 2.0);
			num9 += Math.Pow(Datum.Data.Wave - pOIResultDataCIExyuv3.Wave, 2.0);
		}
		num10 = Data.Count - 1;
		POIResultDataCIExyuv pOIResultDataCIExyuv4 = new POIResultDataCIExyuv
		{
			x = (float)(num / (double)num10),
			y = (float)(num2 / (double)num10),
			u = (float)(num3 / (double)num10),
			v = (float)(num4 / (double)num10),
			X = (float)(num6 / (double)num10),
			Y = (float)(num5 / (double)num10),
			Z = (float)(num7 / (double)num10),
			CCT = (float)(num8 / (double)num10),
			Wave = (float)(num9 / (double)num10)
		};
		dictionary.Add(DataMetricsModel.Variance, pOIResultDataCIExyuv4);
		POIResultDataCIExyuv value = new POIResultDataCIExyuv
		{
			x = (float)Math.Sqrt(pOIResultDataCIExyuv4.x),
			y = (float)Math.Sqrt(pOIResultDataCIExyuv4.y),
			u = (float)Math.Sqrt(pOIResultDataCIExyuv4.u),
			v = (float)Math.Sqrt(pOIResultDataCIExyuv4.v),
			X = (float)Math.Sqrt(pOIResultDataCIExyuv4.X),
			Y = (float)Math.Sqrt(pOIResultDataCIExyuv4.Y),
			Z = (float)Math.Sqrt(pOIResultDataCIExyuv4.Z),
			CCT = (float)Math.Sqrt(pOIResultDataCIExyuv4.CCT),
			Wave = (float)Math.Sqrt(pOIResultDataCIExyuv4.Wave)
		};
		dictionary.Add(DataMetricsModel.StandardDeviation, value);
		POIResultDataCIExyuv value2 = new POIResultDataCIExyuv
		{
			x = pOIResultDataCIExyuv2.x / pOIResultDataCIExyuv.x,
			y = pOIResultDataCIExyuv2.y / pOIResultDataCIExyuv.y,
			u = pOIResultDataCIExyuv2.u / pOIResultDataCIExyuv.u,
			v = pOIResultDataCIExyuv2.v / pOIResultDataCIExyuv.v,
			X = pOIResultDataCIExyuv2.X / pOIResultDataCIExyuv.X,
			Y = pOIResultDataCIExyuv2.Y / pOIResultDataCIExyuv.Y,
			Z = pOIResultDataCIExyuv2.Z / pOIResultDataCIExyuv.Z,
			CCT = pOIResultDataCIExyuv2.CCT / pOIResultDataCIExyuv.CCT,
			Wave = pOIResultDataCIExyuv2.Wave / pOIResultDataCIExyuv.Wave
		};
		dictionary.Add(DataMetricsModel.Uniformity, value2);
		POIResultDataCIExyuv value3 = new POIResultDataCIExyuv
		{
			x = pOIResultDataCIExyuv.x - pOIResultDataCIExyuv2.x,
			y = pOIResultDataCIExyuv.y - pOIResultDataCIExyuv2.y,
			u = pOIResultDataCIExyuv.u - pOIResultDataCIExyuv2.u,
			v = pOIResultDataCIExyuv.v - pOIResultDataCIExyuv2.v,
			X = pOIResultDataCIExyuv.X - pOIResultDataCIExyuv2.X,
			Y = (pOIResultDataCIExyuv.Y - pOIResultDataCIExyuv2.Y) / pOIResultDataCIExyuv3.Y,
			Z = pOIResultDataCIExyuv.Z - pOIResultDataCIExyuv2.Z,
			CCT = pOIResultDataCIExyuv.CCT - pOIResultDataCIExyuv2.CCT,
			Wave = pOIResultDataCIExyuv.Wave - pOIResultDataCIExyuv2.Wave
		};
		dictionary.Add(DataMetricsModel.Repeatability, value3);
		return dictionary;
	}
}
