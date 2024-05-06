using System.ComponentModel;

namespace ColorVision.Services.Msg
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
