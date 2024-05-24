namespace ColorVisonChat
{
    public class ChatMsgSend: ChatMsg
    {
        public string Content { get => _Content; set { _Content = value; NotifyPropertyChanged(); } }
        private string _Content = string.Empty;
    }
}
