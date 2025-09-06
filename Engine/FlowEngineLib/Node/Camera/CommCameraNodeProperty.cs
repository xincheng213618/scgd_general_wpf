using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Camera;

public class CommCameraNodeProperty : ILoopNodeProperty
{
	public string CamTempName { get; set; }

	public string IsAutoExpTime { get; set; }

	public string ExpTempName { get; set; }

	public string IsAutoFocus { get; set; }

	public string FocusTempName { get; set; }

	public string CaliTempName { get; set; }

	public string POITempName { get; set; }

	public string POIFilterTempName { get; set; }

	public string POIReviseTempName { get; set; }

	public string[] ToItemArray(int no)
	{
		return new string[10]
		{
			no.ToString(),
			CamTempName,
			IsAutoExpTime,
			ExpTempName,
			IsAutoFocus,
			FocusTempName,
			CaliTempName,
			POITempName,
			POIFilterTempName,
			POIReviseTempName
		};
	}
}
