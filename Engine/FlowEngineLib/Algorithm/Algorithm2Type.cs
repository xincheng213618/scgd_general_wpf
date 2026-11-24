namespace FlowEngineLib.Algorithm;

public enum Algorithm2Type
{
	MTF = 0,
	// JND and LED strip detection retain their saved names and IDs for historical nodes.
	JND = 1,
	// Value 2 (OLED.GetRIAand) retired on 2026-10-22; do not reuse it.
	SFR = 3,
	// Value 4 is reserved after retirement.
	十字计算 = 5,
	灯带检测 = 6
}
