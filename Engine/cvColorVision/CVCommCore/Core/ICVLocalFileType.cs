namespace CVCommCore.Core;

public interface ICVLocalFileType
{
	CVFileExtType FileType { get; }

	string LocalFileName { get; }
}
