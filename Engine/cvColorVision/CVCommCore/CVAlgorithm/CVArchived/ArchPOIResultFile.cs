namespace CVCommCore.CVAlgorithm.CVArchived;

public class ArchPOIResultFile
{
	public string FileName { get; set; }

	public string FilePath { get; set; }

	public string FileType { get; set; }

	public ArchPOIResultFile(string fileName, string filePath, string fileType)
	{
		FileName = fileName;
		FilePath = filePath;
		FileType = fileType;
	}
}
