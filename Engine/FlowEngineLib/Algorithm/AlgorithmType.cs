namespace FlowEngineLib.Algorithm;

public enum AlgorithmType
{
	// Values 7 (FocusPoints) and 8 (OLED.GetRIAandPT) retired on 2026-10-11.
	// Keep persisted numeric IDs stable; see ColorVision.Engine/Templates/FindLightArea/RETIREMENT.md.
	MTF = 0,
	SFR = 1,
	FOV = 2,
	鬼影 = 3,
	畸变 = 4,
	灯珠检测 = 5,
	灯带检测 = 6,
	JND = 9,
	图像裁剪 = 10,
	双目融合 = 11,
	SFR_FindROI = 12,
	AA布点 = 13,
	ImageCompound = 14,
	十字计算 = 15,
}
