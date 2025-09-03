using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Camera;

public class CameraNodeProperty : ILoopNodeProperty
{
	public string EnableFocus { get; set; }

	public string Focus { get; set; }

	public string Aperture { get; set; }

	public string Gain { get; set; }

	public string AvgCount { get; set; }

	public string Temp { get; set; }

	public string CaliTempName { get; set; }

	public string POITempName { get; set; }

	public string POIFilterTempName { get; set; }

	public string POIReviseTempName { get; set; }

	public string[] ToItemArray(int no)
	{
		return new string[11]
		{
			no.ToString(),
			EnableFocus,
			Focus,
			Aperture,
			AvgCount,
			Gain,
			Temp,
			CaliTempName,
			POITempName,
			POIFilterTempName,
			POIReviseTempName
		};
	}
}
