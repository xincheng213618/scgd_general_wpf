using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceParamFOV : ParamDicBase
{
	public struct FOVParamDis
	{
		public double Radio;

		public double CameraDegrees;

		public int ThresholdValus;

		public double DFovDist;

		public FovPattern FovPattern;

		public FovType FovType;

		public int Xc;

		public int Yc;

		public int Xp;

		public int Yp;
	}

	private double _radio;

	private double _cameraDegrees;

	private int _thresholdValus;

	private double _dFovDist;

	private FovPattern _fovPattern;

	private FovType _fovType;

	private int _Xc;

	private int _Yc;

	private int _Xp;

	private int _Yp;

	public double Radio
	{
		get
		{
			return GetValue(_radio, "Radio");
		}
		set
		{
			SetProperty(ref _radio, value, "Radio");
		}
	}

	public double CameraDegrees
	{
		get
		{
			return GetValue(_cameraDegrees, "CameraDegrees");
		}
		set
		{
			SetProperty(ref _cameraDegrees, value, "CameraDegrees");
		}
	}

	public int ThresholdValus
	{
		get
		{
			return GetValue(_thresholdValus, "ThresholdValus");
		}
		set
		{
			SetProperty(ref _thresholdValus, value, "ThresholdValus");
		}
	}

	public double DFovDist
	{
		get
		{
			return GetValue(_dFovDist, "DFovDist");
		}
		set
		{
			SetProperty(ref _dFovDist, value, "DFovDist");
		}
	}

	public FovPattern FovPattern
	{
		get
		{
			return GetValue(_fovPattern, "FovPattern");
		}
		set
		{
			SetProperty(ref _fovPattern, value, "FovPattern");
		}
	}

	public FovType FovType
	{
		get
		{
			return GetValue(_fovType, "FovType");
		}
		set
		{
			SetProperty(ref _fovType, value, "FovType");
		}
	}

	public int Xc
	{
		get
		{
			return GetValue(_Xc, "Xc");
		}
		set
		{
			SetProperty(ref _Xc, value, "Xc");
		}
	}

	public int Yc
	{
		get
		{
			return GetValue(_Yc, "Yc");
		}
		set
		{
			SetProperty(ref _Yc, value, "Yc");
		}
	}

	public int Xp
	{
		get
		{
			return GetValue(_Xp, "Xp");
		}
		set
		{
			SetProperty(ref _Xp, value, "Xp");
		}
	}

	public int Yp
	{
		get
		{
			return GetValue(_Yp, "Yp");
		}
		set
		{
			SetProperty(ref _Yp, value, "Yp");
		}
	}

	public bool PointHasData()
	{
		if (Xc == 0 && Yc == 0 && Xp == 0)
		{
			return Yp != 0;
		}
		return true;
	}

	public override string ToJsonCfg()
	{
		FOVParamDis fOVParamDis = ToFOVParamDis();
		AddParameters(JsonConvert.SerializeObject(fOVParamDis, new StringEnumConverter()));
		return base.ToJsonCfg();
	}

	public FOVParamDis ToFOVParamDis()
	{
		return new FOVParamDis
		{
			CameraDegrees = CameraDegrees,
			DFovDist = DFovDist,
			FovPattern = FovPattern,
			FovType = FovType,
			Radio = Radio,
			ThresholdValus = ThresholdValus,
			Xc = Xc,
			Yc = Yc,
			Xp = Xp,
			Yp = Yp
		};
	}
}
