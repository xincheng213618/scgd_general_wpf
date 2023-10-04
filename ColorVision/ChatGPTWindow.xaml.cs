using ChatGPT.Net;
using ChatGPT.Net.DTO.ChatGPTUnofficial;
using ColorVision.Draw;
using ColorVision.MVVM;
using ColorVision.Util.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision
{
    public static partial class GlobalConst
    {
        public const string ChatGPTConfig = "ChatGPT";
    }



    public class ChatGPTConfig
    {
        public static ChatGPTConfig Current { get; set; } = JsonHelper.ReadConfig<ChatGPTConfig>(GlobalConst.ChatGPTConfig)?? new ChatGPTConfig();
        public string BaseUrl { get; set; } = "https://nb.nextweb.fun/api/proxy";
        public string APiKey { get; set; } = "";
    }




    public class ChatMsg:ViewModelBase
    {
        
    };

    public class ChatMsgSend: ChatMsg
    {
        public string Content { get => _Content; set { _Content = value; NotifyPropertyChanged(); } }
        private string _Content = string.Empty;
    }
    public class ChatMsgReturn : ChatMsg
    {
        public string Content { get => _Content; set { _Content = value; NotifyPropertyChanged(); } }
        private string _Content =string.Empty;
    }


    public class MsgTemplateSelector : DataTemplateSelector
    {
        public MsgTemplateSelector()
        {

        }

        public DataTemplate ChatMsgSend { get; set; }
        public DataTemplate ChatMsgReturn { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMsgSend)
                return ChatMsgSend;
            if (item is ChatMsgReturn)
                return ChatMsgReturn;
            return base.SelectTemplate(item, container);
        }
    }



    public partial class ChatGPTWindow : Window
    {
        public ChatGPTWindow()
        {
            InitializeComponent();
        }

        public ChatGpt bot { get; set; }

        public ObservableCollection<ChatMsg> ChatMsgs { get; set; }
        private ChatMsgReturn ChatMsgReturn { get; set; }
        private async void Window_Initialized(object sender, EventArgs e)
        {
            ChatMsgs = new ObservableCollection<ChatMsg>();
            ListViewContent.ItemsSource = ChatMsgs;
            bot = new ChatGpt(ChatGPTConfig.Current.APiKey, new ChatGPT.Net.DTO.ChatGPT.ChatGptOptions() { BaseUrl = ChatGPTConfig.Current.BaseUrl });
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            await bot.AskStream(Show, "你现在是ColorVision的专属定制机器人，可以帮助用户解决一些专业问题，请使用专业的口吻回答问题,理解的话请回答 有什么可以帮你的吗");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string content = TextInput.Text;
            ChatMsgs.Add(new ChatMsgSend() { Content =content});
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            Task.Run(() => { ASK(content); });
            //Task.Run(() => Test());
            TextInput.Text = string.Empty;
        }

        int i = 0;
        string msg = string.Empty;
        public async void Test()
        {
            i = 0;
            while (i<5000)
            {
                if (msg != string.Empty)
                {
                    msg = string.Empty;
                    i = 0;
                }
                i++;
                await Task.Delay(10);
            }
        }

        public async void ASK(string content)
        {
            string response =  await bot.AskStream(Show,content);
        }

        public void Show(string response)
        {
            ChatMsgReturn.Content += response;
        }




        private void TextInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                //TextInput.Text += Environment.NewLine;
                //e.Handled = true;
            };
        }
    }
}
