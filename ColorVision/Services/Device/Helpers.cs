using System.Windows;
using Panuon.WPF.UI;
using ColorVision.Extension;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Services.Msg;

namespace ColorVision.Device
{
    internal static class Helpers
    {
        static IPendingHandler handler { get; set; }
        public static IPendingHandler SendCommand(MsgRecord msgRecord,string Msg)
        {
            handler = PendingBox.Show(Application.Current.MainWindow, Msg, true);
            var temp = Application.Current.MainWindow.Cursor;
            Application.Current.MainWindow.Cursor = Cursors.Wait;

            MsgRecordStateChangedHandler msgRecordStateChangedHandler;
            msgRecordStateChangedHandler = (e) =>
            {
                try
                {
                    handler?.UpdateMessage(e.ToDescription());
                    if (e != MsgRecordState.Send)
                    {
                        handler?.Close();
                    }
                }
                catch
                {
                }
                finally
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Application.Current.MainWindow.Cursor = temp;
                    });
                }



            };
            msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
            handler.Cancelling += delegate
            {
                msgRecord.MsgRecordStateChanged -= msgRecordStateChangedHandler;
                handler.Close();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.MainWindow.Cursor = temp;
                });
            };
            return handler;
        }


        public static void SendCommand(Button button, MsgRecord msgRecord,bool Reserve = true)
        {
            var temp = button.Content;
            button.Content = msgRecord.MsgRecordState.ToDescription();
            MsgRecordStateChangedHandler msgRecordStateChangedHandler = async (e) =>
            {
                button.Content = e.ToDescription();
                await Task.Delay(100);
                if (e != MsgRecordState.Send)
                {
                    button.Content = temp;
                }
            };
            if (Reserve)
                msgRecord.MsgRecordStateChanged += msgRecordStateChangedHandler;
        }


    }
}
