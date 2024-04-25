using ColorVision.Common.MVVM;
using System;
using System.Text;

namespace ColorVision.Projects
{
    public class SerialMsg : ViewModelBase
    {
        public byte[] Bytes { get; set; }
        public string Hex => BitConverter.ToString(Bytes).Replace("-", " ");
        public DateTime SendTime { get; set; } = DateTime.Now;

        public string ASCII => Encoding.UTF8.GetString(Bytes);

    }
}
