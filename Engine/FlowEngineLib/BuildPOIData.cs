using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.POI;

namespace FlowEngineLib;

public class BuildPOIData : AlgorithmPreStepParam
{
	public POIBuildType BuildType { get; set; }

	public POIPointTypes POILayoutReq { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public CVTemplateParam POITemplateParam { get; set; }

	public CVTemplateParam RePOITemplateParam { get; set; }

	public CADMappingParam CADMappingParam { get; set; }

	public string ImgFileName { get; set; }

	public string PrefixName { get; set; }

	public POIStorageModel POIStorageType { get; set; }

	public string OutputFileName { get; set; }

	public POITypeData POITypeData { get; set; }

	public int BufferLen { get; set; }

	public CVTemplateParam SavePOITemplate { get; set; }

	public BuildPOIData(string imgFile, int tempId, string tempName, POIStorageModel POIOutput, string outputFileName, string prefixName, POIBuildType buildType, POITypeData poiData, string poiSaveTempName, string layoutROITemplate, int bufLen)
	{
		ImgFileName = imgFile;
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
		POILayoutReq = POIPointTypes.None;
		POIStorageType = POIOutput;
		OutputFileName = outputFileName;
		PrefixName = prefixName;
		BuildType = buildType;
		POITypeData = poiData;
		SavePOITemplate = new CVTemplateParam
		{
			ID = -1,
			Name = poiSaveTempName
		};
		POITemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = layoutROITemplate
		};
		BufferLen = bufLen;
	}

	public BuildPOIData(string imgFile, CADMappingParam cad, int tempId, string tempName, POIStorageModel POIOutput, string outputFileName, string prefixName, POIBuildType buildType, POITypeData poiData, string poiSaveTempName, string layoutROITemplate, int bufLen)
		: this(imgFile, tempId, tempName, POIOutput, outputFileName, prefixName, buildType, poiData, poiSaveTempName, layoutROITemplate, bufLen)
	{
		CADMappingParam = cad;
	}

	public BuildPOIData(string imgFile, int tempId, string tempName, POIStorageModel POIOutput, string outputFileName, string prefixName, POIBuildType buildType, string poiReTempName, POITypeData poiData, string poiSaveTempName, string layoutROITemplate, int bufLen)
		: this(imgFile, tempId, tempName, POIOutput, outputFileName, prefixName, buildType, poiData, poiSaveTempName, layoutROITemplate, bufLen)
	{
		RePOITemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = poiReTempName
		};
	}
}
