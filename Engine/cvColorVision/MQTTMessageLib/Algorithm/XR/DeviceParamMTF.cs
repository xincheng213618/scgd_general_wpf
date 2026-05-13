using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceParamMTF : ParamDicBase
{
	public struct MTFParamDis
	{
		public double MTF_dRatio;

		public EvaFunc EvaFunc;

		public int dx;

		public int dy;

		public int ksize;
	}

	private double _MTF_dRatio = 0.01;

	private EvaFunc _EvaFunc = EvaFunc.CalResol;

	private int _dx;

	private int _dy = 1;

	private int _ksize = 5;

	public double MTF_dRatio
	{
		get
		{
			return GetValue(_MTF_dRatio, "MTF_dRatio");
		}
		set
		{
			SetProperty(ref _MTF_dRatio, value, "MTF_dRatio");
		}
	}

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

	public int dx
	{
		get
		{
			return GetValue(_dx, "dx");
		}
		set
		{
			SetProperty(ref _dx, value, "dx");
		}
	}

	public int dy
	{
		get
		{
			return GetValue(_dy, "dy");
		}
		set
		{
			SetProperty(ref _dy, value, "dy");
		}
	}

	public int ksize
	{
		get
		{
			return GetValue(_ksize, "ksize");
		}
		set
		{
			SetProperty(ref _ksize, value, "ksize");
		}
	}

	public override string ToJsonCfg()
	{
		MTFParamDis mTFParamDis = ToMTFParamDis();
		AddParameters(JsonConvert.SerializeObject(mTFParamDis, new StringEnumConverter()));
		return base.ToJsonCfg();
	}

	public MTFParamDis ToMTFParamDis()
	{
		return new MTFParamDis
		{
			dx = dx,
			dy = dy,
			EvaFunc = EvaFunc,
			ksize = ksize,
			MTF_dRatio = MTF_dRatio
		};
	}
}
