namespace CVCommCore.CVImage;

public struct CVCIEFileInfo
{
	public SrcFrameInfo FrameInfo;

	public int Depth { get; set; }

	public int NDPort { get; set; }

	public float Gain { get; set; }

	public float Scale { get; set; }

	public float[] exp { get; set; }

	public byte[] data { get; set; }

	public string srcFileName { get; set; }

	public override string ToString()
	{
		return $"width={FrameInfo.width},height={FrameInfo.height},bpp={FrameInfo.bpp},channels={FrameInfo.channels},NDPort={NDPort}";
	}
}
