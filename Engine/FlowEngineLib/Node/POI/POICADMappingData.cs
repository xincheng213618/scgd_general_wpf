using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.POI;

public class POICADMappingData : AlgorithmPreStepParam
{
	public POIBuildType BuildType { get; set; }

	public string CAD_PosFileName { get; set; }

	public int CAD_MasterId { get; set; }

	public string PrefixName { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public POICADMappingData(string tempName, string prefix, string cad_PosFileName, POIBuildType _MappingType)
	{
		CAD_PosFileName = cad_PosFileName;
		PrefixName = prefix;
		BuildType = _MappingType;
		CAD_MasterId = -1;
		base.MasterId = -1;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}

	public POICADMappingData(string tempName, string prefix, POIBuildType _MappingType)
	{
		CAD_MasterId = -1;
		PrefixName = prefix;
		base.MasterId = -1;
		BuildType = _MappingType;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
