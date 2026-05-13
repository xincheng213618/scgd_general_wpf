using Newtonsoft.Json;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceParamSFR : ParamDicBase
{
	public struct SFRParamDis
	{
		public int X;

		public int Y;

		public int Width;

		public int Height;

		public double Gamma;
	}

	private int _x;

	private int _y;

	private int _cx;

	private int _cy;

	private double _gamma;

	public int X
	{
		get
		{
			return GetValue(_x, "X");
		}
		set
		{
			SetProperty(ref _x, value, "X");
		}
	}

	public int Y
	{
		get
		{
			return GetValue(_y, "Y");
		}
		set
		{
			SetProperty(ref _y, value, "Y");
		}
	}

	public int Width
	{
		get
		{
			return GetValue(_cx, "Width");
		}
		set
		{
			SetProperty(ref _cx, value, "Width");
		}
	}

	public int Height
	{
		get
		{
			return GetValue(_cy, "Height");
		}
		set
		{
			SetProperty(ref _cy, value, "Height");
		}
	}

	public double Gamma
	{
		get
		{
			return GetValue(_gamma, "Gamma");
		}
		set
		{
			SetProperty(ref _gamma, value, "Gamma");
		}
	}

	public bool HasROIData()
	{
		if (X >= 0 && Y >= 0 && Width > 0)
		{
			return Height > 0;
		}
		return false;
	}

	public CRECT GetROI()
	{
		return new CRECT
		{
			x = X,
			y = Y,
			cx = Width,
			cy = Height
		};
	}

	public override string ToJsonCfg()
	{
		AddParameters(JsonConvert.SerializeObject(new SFRParamDis
		{
			X = X,
			Y = Y,
			Width = Width,
			Height = Height,
			Gamma = Gamma
		}));
		return base.ToJsonCfg();
	}
}
