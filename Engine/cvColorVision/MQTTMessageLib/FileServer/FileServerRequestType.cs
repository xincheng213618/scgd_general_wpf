using System.ComponentModel;

namespace MQTTMessageLib.FileServer;

public enum FileServerRequestType
{
	[Description("获取所有文件")]
	GetAllFiles,
	[Description("下载文件")]
	DownloadFile,
	[Description("上传文件")]
	UploadFile,
	[Description("获取通道")]
	GetChannel
}
