using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVisonChat
{
    public class ChatGPTConfig : ViewModelBase,IConfig
    {
        public static ChatGPTConfig Instance => ConfigService.Instance.GetRequiredService<ChatGPTConfig>();

        public RelayCommand EditCommand { get; set; }

        public event EventHandler? ConfigChanged;

        public ChatGPTConfig()
        {
            EditCommand = new RelayCommand(a =>  new ColorVision.UI.PropertyEditor.PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog() );
        }

        public string APiKey { get => _APiKey; set { _APiKey = value; NotifyPropertyChanged(); } }
        private string _APiKey; 

        public string BaseUrl { get => _BaseUrl; set { _BaseUrl = value; NotifyPropertyChanged(); } }
        private string _BaseUrl = "https://api.openai.com/";

        public string Model { get => _Model; set { _Model = value; NotifyPropertyChanged(); } }
        private string _Model = "gpt-4o";

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
        private static ILog log = LogManager.GetLogger(typeof(ChatGPTWindow));
        public ChatGPTWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        public ChatClient ChatClient { get; set; } 

        public ObservableCollection<ChatMsg> ChatMsgs { get; set; }
        private ChatMsgReturn ChatMsgReturn { get; set; }
        private async void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ChatGPTConfig.Instance;
            ChatMsgs = new ObservableCollection<ChatMsg>();
            ListViewContent.ItemsSource = ChatMsgs;
            if (ChatGPTConfig.Instance.APiKey != null)
            {
                Init();
            }
        }


        private async void Init()
        {
            ChatClient = new ChatClient(ChatGPTConfig.Instance.Model, ChatGPTConfig.Instance.APiKey);
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            try
            {

                AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = ChatClient.CompleteChatStreamingAsync("你现在是ColorVision的专属定制机器人，可以帮助用户解决一些专业问题，请使用专业的口吻回答问题,理解的话请回答 有什么可以帮你的吗");
                await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
                {
                    if (completionUpdate.ContentUpdate.Count > 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ChatMsgReturn.Content += completionUpdate.ContentUpdate[0].Text;
                        });
                    }
                }

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
            ChatMsgs.Add(new ChatMsgSend() { Content = content});
            ChatMsgReturn = new ChatMsgReturn();
            ChatMsgs.Add(ChatMsgReturn);
            Task.Run(()=> ASK(content));
            TextInput1.Text = string.Empty;
        }

        public async void ASK(string content)
        {
            AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates = ChatClient.CompleteChatStreamingAsync(content);
            Console.Write($"[ASSISTANT]: ");
            try
            {
                await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
                {
                    if (completionUpdate.ContentUpdate.Count > 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ChatMsgReturn.Content += completionUpdate.ContentUpdate[0].Text;
                        });
                    }
                }
            }
            catch (Exception ex)
            {

            }

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

        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            ChatClient.CompleteChatStreaming(ChatMsgReturn.Content);
        }
    }
}
