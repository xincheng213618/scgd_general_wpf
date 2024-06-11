namespace ColorVision.Util.Controls
{
    public enum UploadStatus
    {
        Waiting,      // 等待中
        Uploading,    // 上传中
        Completed,    // 上传完成
        Failed,       // 失败
        CheckingMD5   // 检查 MD5
    }
}
