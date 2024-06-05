namespace ColorVision.Util.Controls
{
    public enum UploadStatus
    {
        Uploading,    // 上传中
        Waiting,      // 等待中
        Completed,    // 上传完成
        Failed,       // 失败
        CheckingMD5   // 检查 MD5
    }
}
