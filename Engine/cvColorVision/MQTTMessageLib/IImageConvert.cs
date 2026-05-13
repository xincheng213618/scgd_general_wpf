using CVCommCore.CVImage;

namespace MQTTMessageLib;

public interface IImageConvert
{
	bool ConvertToMat(ref CVCIEFileInfo fileInfo);

	bool ConvertToMat(CVCIEFileInfo inFileInfo, out CVCIEFileInfo outFileInfo);
}
