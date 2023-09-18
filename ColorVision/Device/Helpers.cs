using System.Windows;
using Panuon.WPF.UI;
using ColorVision.MQTT;
using ColorVision.Extension;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ColorVision.Device
{
    internal static class Helpers
    {
        static IPendingHandler handler { get; set; }
        public static IPendingHandler SendCommand(MsgRecord msgRecord,string Msg)
        {
            handler = PendingBox.Show(Application.Current.MainWindow, Msg, true);
            handler.Cancelling += delegate
            {
                handler.Close();
            };
            MsgRecordStateChangedHandler msgRecordStateChangedHandler = async (e) =>
            {
                try
                {
                    handler?.UpdateMessage(e.ToDescription());
                    if (e != MsgRecordState.Send)
                    {
                        await Task.Delay(500);
                        handler?.Close();
                    }
                }
                catch { }
            };
            msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
            return handler;
        }


        public static void SendCommand(Button button, MsgRecord msgRecord)
        {
            var temp = button.Content;
            button.Content = msgRecord.MsgRecordState.ToDescription();
            MsgRecordStateChangedHandler msgRecordStateChangedHandler = async (e) =>
            {
                button.Content = e.ToDescription();
                if (e != MsgRecordState.Send)
                {
                    await Task.Delay(1000);
                    button.Content = temp;
                }
            };
            msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
        }


    }
}
