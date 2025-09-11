namespace FlowEngineLib;

public class FocusPosData
{
	public int nPosition { get; set; }

	public bool bAbs { get; set; }

	public FocusPosData(int nPosition, bool bAbs)
	{
		this.nPosition = nPosition;
		this.bAbs = bAbs;
	}
}
