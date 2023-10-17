using System.ComponentModel;

namespace ColorVision.Services.Msg
{
    public enum MsgRecordState
    {
        [Description("已经发送")]
        Send,
        [Description("命令成功")]
        Success,
        [Description("命令失败")]
        Fail,
        [Description("命令超时")]
        Timeout
    }



}
