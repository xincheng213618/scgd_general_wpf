using System.ComponentModel;

namespace ColorVision.Engine.Messages
{
    public enum MsgRecordState
    {
        [Description("Initial")]
        Initial,   // 新增初始状态
        [Description("Sended")]
        Sended,
        [Description("Success")]
        Success,
        [Description("Failure")]
        Fail,
        [Description("Timeout")]
        Timeout
    }



}
