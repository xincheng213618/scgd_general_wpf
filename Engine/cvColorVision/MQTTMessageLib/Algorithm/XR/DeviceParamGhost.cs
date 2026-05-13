namespace MQTTMessageLib.Algorithm.XR;

public class DeviceParamGhost : ParamDicBase
{
	private int _Ghost_radius;

	private int _Ghost_cols;

	private int _Ghost_rows;

	private float _Ghost_ratioH;

	private float _Ghost_ratioL;

	public int Ghost_radius
	{
		get
		{
			return GetValue(_Ghost_radius, "Ghost_radius");
		}
		set
		{
			SetProperty(ref _Ghost_radius, value, "Ghost_radius");
		}
	}

	public int Ghost_cols
	{
		get
		{
			return GetValue(_Ghost_cols, "Ghost_cols");
		}
		set
		{
			SetProperty(ref _Ghost_cols, value, "Ghost_cols");
		}
	}

	public int Ghost_rows
	{
		get
		{
			return GetValue(_Ghost_rows, "Ghost_rows");
		}
		set
		{
			SetProperty(ref _Ghost_rows, value, "Ghost_rows");
		}
	}

	public float Ghost_ratioH
	{
		get
		{
			return GetValue(_Ghost_ratioH, "Ghost_ratioH");
		}
		set
		{
			SetProperty(ref _Ghost_ratioH, value, "Ghost_ratioH");
		}
	}

	public float Ghost_ratioL
	{
		get
		{
			return GetValue(_Ghost_ratioL, "Ghost_ratioL");
		}
		set
		{
			SetProperty(ref _Ghost_ratioL, value, "Ghost_ratioL");
		}
	}
}
