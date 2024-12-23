using System.ComponentModel;

namespace ColorVision.Engine.Messages
{
    public enum MsgRecordState
    {
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
