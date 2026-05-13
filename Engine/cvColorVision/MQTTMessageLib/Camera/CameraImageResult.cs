using System;

namespace MQTTMessageLib.Camera;

public class CameraImageResult
{
	public int? Id { get; set; }

	public CameraFileType FileType { get; set; }

	public string RawFile { get; set; }

	public string FullPathFile { get; set; }

	public int? ZIndex { get; set; }

	public int? ResultCode { get; set; }

	public int? NDPort { get; set; }

	public string FileData { get; set; }

	public string DeviceCode { get; set; }

	public DateTime? CreateDateTime { get; set; }
}
