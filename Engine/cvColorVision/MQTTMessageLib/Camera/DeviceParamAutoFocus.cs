using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.Camera;

public class DeviceParamAutoFocus : ParamDicBase
{
	private EvaFunc _EvaFunc;

	private double _Forwardparam;

	private int _CurStep;

	private double _Curtailparam;

	private int _StopStep;

	private int _MinPosition;

	private int _MaxPosition;

	private double _MinValue;

	private int _nTimeout;

	public EvaFunc EvaFunc
	{
		get
		{
			return GetValue(_EvaFunc, "EvaFunc");
		}
		set
		{
			SetProperty(ref _EvaFunc, value, "EvaFunc");
		}
	}

	public double Forwardparam
	{
		get
		{
			return GetValue(_Forwardparam, "Forwardparam");
		}
		set
		{
			SetProperty(ref _Forwardparam, value, "Forwardparam");
		}
	}

	public int CurStep
	{
		get
		{
			return GetValue(_CurStep, "CurStep");
		}
		set
		{
			SetProperty(ref _CurStep, value, "CurStep");
		}
	}

	public double Curtailparam
	{
		get
		{
			return GetValue(_Curtailparam, "Curtailparam");
		}
		set
		{
			SetProperty(ref _Curtailparam, value, "Curtailparam");
		}
	}

	public int StopStep
	{
		get
		{
			return GetValue(_StopStep, "StopStep");
		}
		set
		{
			SetProperty(ref _StopStep, value, "StopStep");
		}
	}

	public int MinPosition
	{
		get
		{
			return GetValue(_MinPosition, "MinPosition");
		}
		set
		{
			SetProperty(ref _MinPosition, value, "MinPosition");
		}
	}

	public int MaxPosition
	{
		get
		{
			return GetValue(_MaxPosition, "MaxPosition");
		}
		set
		{
			SetProperty(ref _MaxPosition, value, "MaxPosition");
		}
	}

	public double MinValue
	{
		get
		{
			return GetValue(_MinValue, "MinValue");
		}
		set
		{
			SetProperty(ref _MinValue, value, "MinValue");
		}
	}

	public int nTimeout
	{
		get
		{
			return GetValue(_nTimeout, "nTimeout");
		}
		set
		{
			SetProperty(ref _nTimeout, value, "nTimeout");
		}
	}

	public AutoFocusCfg ToSysCfg()
	{
		return new AutoFocusCfg
		{
			forwardparam = Forwardparam,
			eEvaFunc = EvaFunc,
			curtailparam = Curtailparam,
			curStep = CurStep,
			stopStep = StopStep,
			minPosition = MinPosition,
			maxPosition = MaxPosition,
			dMinValue = MinValue
		};
	}
}
