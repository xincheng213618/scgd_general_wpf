using Newtonsoft.Json;

namespace CVCommCore.CVCamera;

public class PhysicCameraConfig
{
	public string CameraSN { get; set; }

	public CameraModel CameraModel { get; set; }

	public CameraMode CameraMode { get; set; }

	public TakeImageMode TakeImageMode { get; set; }

	public int ImageBpp { get; set; }

	public int Channel { get; set; }

	public MotorConfig MotorConfig { get; set; }

	public CFW CFW { get; set; }

	public SysCameraSDKCfg cameraCfg { get; set; }

	public FileSeviceConfig FileServerCfg { get; set; }

	[JsonIgnore]
	public bool IsModeBV
	{
		get
		{
			if (CameraMode != CameraMode.BV_MODE)
			{
				return CameraMode == CameraMode.LVTOBV_MODE;
			}
			return true;
		}
	}

	[JsonIgnore]
	public bool IsModeCV => CameraMode == CameraMode.CV_MODE;

	[JsonIgnore]
	public bool IsModeLV => CameraMode == CameraMode.LV_MODE;

	public int FileVersion { get; set; } = 2;
}
