namespace MQTTMessageLib.Camera;

public class DeviceParamCameraOneChannel : CameraRunParam
{
	private int _ExpTime;

	public int ExpTime
	{
		get
		{
			return GetValue(_ExpTime, "ExpTime");
		}
		set
		{
			SetProperty(ref _ExpTime, value, "ExpTime");
		}
	}

	public override float[] ExportExpTime()
	{
		return new float[1] { ExpTime };
	}
}
