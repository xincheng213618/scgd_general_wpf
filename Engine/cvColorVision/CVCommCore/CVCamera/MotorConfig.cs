namespace CVCommCore.CVCamera;

public struct MotorConfig
{
	public bool IsCameraLinkage { get; set; }

	public bool IsUseMotor { get; set; }

	public FOCUS_COMMUN eFOCUSCOMMUN { get; set; }

	public string SzComName { get; set; }

	public uint BaudRate { get; set; }

	public FindFuncModel FindFuncModel { get; set; }

	public int DwTimeOut { get; set; }

	public int Home_nAcc { get; set; }

	public int Home_nHighSpeed { get; set; }

	public int Home_nLowSpeed { get; set; }

	public GoHome_WAY GoHomeWay { get; set; }

	public uint HomeTimeout { get; set; }

	public int Run_nSpeed { get; set; }

	public int Run_nAcc { get; set; }

	public int Run_ndec { get; set; }

	public int MinPosition { get; set; }

	public int MaxPosition { get; set; }

	public MotorVID VID { get; set; }

	public int AutoFocusSaveImageNum { get; set; }
}
