namespace FlowEngineLib;

public class CADMappingParam
{
	public int CAD_MasterId { get; set; }

	public string CAD_PosFileName { get; set; }

	public CADMappingParam(string CADFileName)
	{
		CAD_MasterId = -1;
		CAD_PosFileName = CADFileName;
	}
}
