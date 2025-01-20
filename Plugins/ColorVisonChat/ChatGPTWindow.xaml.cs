using ChatGPT.Net;
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVisonChat
{
    public static partial class GlobalConst
    {
        public const string ChatGPTConfig = "ChatGPT";
        public const string BaseUrl = "https://api.openai.com/";
    }

    public class ExportColorVisonChat : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "ColorVisionChat";

        public int Order => 6;

        public string? Header => "ColorVisionChat";

        public string? InputGestureText => null;

        public object? Icon => null;

        public ICommand Command => new RelayCommand(A => Execute());
        public Visibility Visibility => Visibility.Visible;

        private static void Execute()
        {
            new ChatGPTWindow() { Owner = Application.Current.GetActiveWindow()}.Show();
        }
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
            this.ApplyCaption();
        }

        public ChatGpt bot { get; set; }

        public ObservableCollection<ChatMsg> ChatMsgs { get; set; }
        private ChatMsgReturn ChatMsgReturn { get; set; }
        private async void Window_Initialized(object sender, EventArgs e)
        {
            ChatMsgs = new ObservableCollection<ChatMsg>();
            ListViewContent.ItemsSource = ChatMsgs;
            bot = new ChatGpt(ChatGPTConfig.Instance.APiKey, new ChatGPT.Net.DTO.ChatGPT.ChatGptOptions() { BaseUrl = GlobalConst.BaseUrl ,Model = "gpt-4o-2024-05-13" });
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            try
            {
                await bot.AskStream(Show, "你现在是ColorVision的专属定制机器人，可以帮助用户解决一些专业问题，请使用专业的口吻回答问题,理解的话请回答 有什么可以帮你的吗");
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ChatMsgs.Add(new ChatMsgSend() { Content = ex.Message });

                });

            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string content = TextInput1.Text;
            ChatMsgs.Add(new ChatMsgSend() { Content =content});
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            Task.Run(() => { ASK(content); });
            TextInput1.Text = string.Empty;
        }

        public async void ASK(string content)
        {
            string response =  await bot.AskStream(Show,content);
        }

        public void Show(string response)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                await Task.Delay(100);
                ChatMsgReturn.Content += response;
            });
        }




        private void TextInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                //TextInput.Text += Environment.NewLine;
                //e.Handled = true;
            };
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            new ChatGPTConfigSetting().Show();
        }
    }
}
