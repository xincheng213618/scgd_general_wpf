using System;
using CVCommCore.CVImage;
using Newtonsoft.Json;

namespace MQTTMessageLib.Camera;

public class CameraImageInfoResult
{
	public int? Id { get; set; }

	public string ImgName { get; set; }

	public string FullPathFile { get; set; }

	public int? ResultCode { get; set; }

	public int? ZIndex { get; set; }

	public int? NDPort { get; set; }

	public SrcFrameInfo ImgInfo { get; set; }

	public string DeviceCode { get; set; }

	public DateTime? CreateDateTime { get; set; }

	public CameraImageInfoResult()
	{
	}

	public CameraImageInfoResult(CameraImageResult result)
	{
		Id = result.Id;
		ImgName = result.RawFile;
		FullPathFile = result.FullPathFile;
		ResultCode = result.ResultCode;
		ZIndex = result.ZIndex;
		NDPort = result.NDPort;
		ImgInfo = JsonConvert.DeserializeObject<SrcFrameInfo>(result.FileData);
		DeviceCode = result.DeviceCode;
		CreateDateTime = result.CreateDateTime;
	}
}
