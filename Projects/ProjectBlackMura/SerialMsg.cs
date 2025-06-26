using ColorVision.Common.MVVM;
using System.Text;

namespace ProjectBlackMura
{
    public enum SerialStatus
    {
        Send,
        Receive
    }

    public class SerialMsg : ViewModelBase
    {
        public SerialStatus SerialStatus { get; set; }
        public byte[] Bytes { get; set; }
        public string Hex => BitConverter.ToString(Bytes).Replace("-", " ");
        public DateTime SendTime { get; set; } = DateTime.Now;

        public string ASCII => Encoding.UTF8.GetString(Bytes);

    }
}
