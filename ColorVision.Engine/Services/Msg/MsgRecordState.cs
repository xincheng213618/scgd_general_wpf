using System.ComponentModel;

namespace ColorVision.Engine.Services.Msg
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
