#pragma warning disable
using System.Collections.Generic;

namespace cvColorVision
{
    public class GetFrameParam
    {
        //滤色片数量
        public int channelCount { set; get; }
        //测量次数
        public int measureCount { set; get; }
        //ob
        public int ob { set; get; }
        public int obR { set; get; }
        public int obT { set; get; }
        public int obB { set; get; }
        //startBurst
        public uint startBurst { set; get; }
        //endBurst
        public uint endBurst { set; get; }
        public uint posBurst { set; get; }
        //测量标题名称
        public string title { set; get; }
        //自动曝光标识
        public bool autoExpFlag { set; get; }
        //滤色片通道
        public List<ChannelParam> channels;

        // An empty list requests packed RAW.
        public object[] calibrationlist;

        public GetFrameParam()
        {
            channels = new List<ChannelParam>();
        }

        public void BuildChannelsFileName(string path, string ext)
        {
            foreach (ChannelParam item in channels)
            {
                item.fileName = path + "\\" + title + ext;
            }
        }
    }
}
