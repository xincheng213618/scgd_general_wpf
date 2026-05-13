using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.SMU;

public class DeviceParamScan : ParamDicBase
{
	private double _SrcRng;

	private double _LmtRng;

	private bool _IsAutoRng = true;

	private SMUChannelType _Channel;

	private bool _IsSourceV;

	private double _StartMeasureVal;

	private double _StopMeasureVal;

	private double _LimitVal;

	private int _Number;

	public double SrcRng
	{
		get
		{
			return GetValue(_SrcRng, "SrcRng");
		}
		set
		{
			SetProperty(ref _SrcRng, value, "SrcRng");
		}
	}

	public double LmtRng
	{
		get
		{
			return GetValue(_LmtRng, "LmtRng");
		}
		set
		{
			SetProperty(ref _LmtRng, value, "LmtRng");
		}
	}

	public bool IsAutoRng
	{
		get
		{
			return GetValue(_IsAutoRng, "IsAutoRng");
		}
		set
		{
			SetProperty(ref _IsAutoRng, value, "IsAutoRng");
		}
	}

	public SMUChannelType Channel
	{
		get
		{
			return GetValue(_Channel, "Channel");
		}
		set
		{
			SetProperty(ref _Channel, value, "Channel");
		}
	}

	public bool IsSourceV
	{
		get
		{
			return GetValue(_IsSourceV, "IsSourceV");
		}
		set
		{
			SetProperty(ref _IsSourceV, value, "IsSourceV");
		}
	}

	public double BeginValue
	{
		get
		{
			return GetValue(_StartMeasureVal, "BeginValue");
		}
		set
		{
			SetProperty(ref _StartMeasureVal, value, "BeginValue");
		}
	}

	public double EndValue
	{
		get
		{
			return GetValue(_StopMeasureVal, "EndValue");
		}
		set
		{
			SetProperty(ref _StopMeasureVal, value, "EndValue");
		}
	}

	public double LimitValue
	{
		get
		{
			return GetValue(_LimitVal, "LimitValue");
		}
		set
		{
			SetProperty(ref _LimitVal, value, "LimitValue");
		}
	}

	public int Points
	{
		get
		{
			return GetValue(_Number, "Points");
		}
		set
		{
			SetProperty(ref _Number, value, "Points");
		}
	}

	public DeviceParamScan()
	{
		CreateEmptyParams();
	}
}
