using ColorVision.Services.Msg;
using ColorVision.Services.OnlineLicensing;
using ColorVision.Services.Terminal;
using MQTTMessageLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Devices.Camera
{
    public class MQTTTerminalCamera : MQTTServiceTerminalBase<TerminalServiceConfig>
    {

        public MQTTTerminalCamera(TerminalServiceConfig Config) :base(Config)
        {
            Connected += (s, e) => GetAllSnID();
            if (MQTTControl.IsConnect)
                GetAllSnID();     
        }

        public MsgRecord GetAllSnID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
    }
}
