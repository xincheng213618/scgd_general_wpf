using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;

namespace MQTTMessageLib.Algorithm.POI;

public class RealPOIGetDataParam : DeviceAlgorithmPOIParam
{
	public POITypeData POITypeData { get; set; }

	public int CIE_MasterId { get; set; }

	public bool IsResultAdd { get; set; }

	public bool DoPOIValidate(ref string Reason)
	{
		if (POITypeData.PointType == POIPointTypes.Rect || POITypeData.PointType == POIPointTypes.Circle)
		{
			if (!SetPOIDataType())
			{
				Reason = $"POI Point Type is invalid.=>{JsonConvert.SerializeObject(POITypeData)}";
				return false;
			}
			string err = string.Empty;
			if (!CheckPOIData(ref err))
			{
				Reason = $"There are illegal POI points present.{err}";
				return false;
			}
		}
		return true;
	}

	private bool CheckPOIData(ref string err)
	{
		foreach (POIPoint datum in base.Data)
		{
			if (!datum.IsValid())
			{
				err = $"POI Point => {JsonConvert.SerializeObject(datum)}";
				return false;
			}
		}
		return true;
	}

	private bool SetPOIDataType()
	{
		bool result = true;
		switch (POITypeData.PointType)
		{
		case POIPointTypes.Circle:
		case POIPointTypes.Rect:
			result = SetPOIDataSize(POITypeData);
			break;
		}
		return result;
	}

	private bool SetPOIDataSize(POITypeData poiTypeData)
	{
		if (poiTypeData.Width > 0f && poiTypeData.Height > 0f)
		{
			foreach (POIPoint datum in base.Data)
			{
				datum.PointType = poiTypeData.PointType;
				datum.Width = poiTypeData.Width;
				datum.Height = poiTypeData.Height;
			}
			return true;
		}
		return false;
	}
}
