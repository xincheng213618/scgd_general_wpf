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
            handler.Cancelling += delegate
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    handler.Close();
                    Application.Current.MainWindow.Cursor = temp;
                });
            };
            MsgRecordStateChangedHandler msgRecordStateChangedHandler = (e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
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
                        Application.Current.MainWindow.Cursor = temp;
                    }
                });


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
