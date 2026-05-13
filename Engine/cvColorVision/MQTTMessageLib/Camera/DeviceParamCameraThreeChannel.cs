namespace MQTTMessageLib.Camera;

public class DeviceParamCameraThreeChannel : CameraRunParam
{
	private int _ExpTimeR;

	private int _ExpTimeG;

	private int _ExpTimeB;

	public int ExpTimeR
	{
		get
		{
			return GetValue(_ExpTimeR, "ExpTimeR");
		}
		set
		{
			SetProperty(ref _ExpTimeR, value, "ExpTimeR");
		}
	}

	public int ExpTimeG
	{
		get
		{
			return GetValue(_ExpTimeG, "ExpTimeG");
		}
		set
		{
			SetProperty(ref _ExpTimeG, value, "ExpTimeG");
		}
	}

	public int ExpTimeB
	{
		get
		{
			return GetValue(_ExpTimeB, "ExpTimeB");
		}
		set
		{
			SetProperty(ref _ExpTimeB, value, "ExpTimeB");
		}
	}

	public override float[] ExportExpTime()
	{
		return new float[3] { ExpTimeR, ExpTimeG, ExpTimeB };
	}
}
