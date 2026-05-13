using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceParamPOIFilter : ParamDicBase
{
	private bool _ThresholdUsePercent;

	private bool _Enable;

	private bool _XYZEnable;

	private bool _NoAreaEnable;

	private float _Threshold;

	private int _XYZType;

	private float _MaxPercent;

	public bool ThresholdUsePercent
	{
		get
		{
			return GetValue(_ThresholdUsePercent, "ThresholdUsePercent");
		}
		set
		{
			SetProperty(ref _ThresholdUsePercent, value, "ThresholdUsePercent");
		}
	}

	public bool Enable
	{
		get
		{
			return GetValue(_Enable, "Enable");
		}
		set
		{
			SetProperty(ref _Enable, value, "Enable");
		}
	}

	public bool XYZEnable
	{
		get
		{
			return GetValue(_XYZEnable, "XYZEnable");
		}
		set
		{
			SetProperty(ref _XYZEnable, value, "XYZEnable");
		}
	}

	public bool NoAreaEnable
	{
		get
		{
			return GetValue(_NoAreaEnable, "NoAreaEnable");
		}
		set
		{
			SetProperty(ref _NoAreaEnable, value, "NoAreaEnable");
		}
	}

	public float Threshold
	{
		get
		{
			return GetValue(_Threshold, "Threshold");
		}
		set
		{
			SetProperty(ref _Threshold, value, "Threshold");
		}
	}

	public int XYZType
	{
		get
		{
			return GetValue(_XYZType, "XYZType");
		}
		set
		{
			SetProperty(ref _XYZType, value, "XYZType");
		}
	}

	public float MaxPercent
	{
		get
		{
			return GetValue(_MaxPercent, "MaxPercent");
		}
		set
		{
			SetProperty(ref _MaxPercent, value, "MaxPercent");
		}
	}

	public POIFilterItem ToFilterItem()
	{
		POIFilterType fType = POIFilterType.None;
		if (Enable)
		{
			fType = POIFilterType.ValueEnable;
		}
		else if (XYZEnable)
		{
			fType = POIFilterType.XYZEnable;
		}
		else if (NoAreaEnable)
		{
			fType = POIFilterType.NoAreaEnable;
		}
		return new POIFilterItem
		{
			FType = fType,
			ThresholdUsePercent = ThresholdUsePercent,
			MaxPercent = MaxPercent,
			XYZType = XYZType,
			Threshold = Threshold
		};
	}
}
