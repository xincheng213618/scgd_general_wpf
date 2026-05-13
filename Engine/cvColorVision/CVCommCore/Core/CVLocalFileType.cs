using System.IO;

namespace CVCommCore.Core;

public class CVLocalFileType : ICVLocalFileType
{
	public CVFileExtType FileType { get; set; }

	public string LocalFileName { get; set; }

	public static CVFileExtType GetFileType(string imgFileName)
	{
		CVFileExtType result = CVFileExtType.None;
		if (!string.IsNullOrEmpty(imgFileName))
		{
			string text = Path.GetExtension(imgFileName).ToLower();
			result = (text.Contains("tif") ? CVFileExtType.Tif : ((!text.Contains("cvraw")) ? ((!text.Contains("cvcie")) ? CVFileExtType.Tif : CVFileExtType.CIE) : CVFileExtType.Raw));
		}
		return result;
	}
}
