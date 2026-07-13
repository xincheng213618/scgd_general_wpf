#pragma warning disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using cvColorVision.Properties;

namespace cvColorVision
{
    public class ChannelCalibration
    {
        public CalibrationItem DarkNoiseCheck;
        //暗噪声校正参数
        public CalibrationItem dsnuCheck;
        //均匀场校正文件
        public CalibrationItem uniformityCheck;
        //缺陷点校正文件
        public CalibrationItem defectWPCheck;
        public CalibrationItem defectBPCheck;
        //畸变校正
        public CalibrationItem distortionCheck;

        public CalibrationItem defectCheck;

    }
    public class ChannelParam
    {
        //曝光时间，单位毫秒
        public float exp { set; get; }
        //滤色轮端口
        public int cfwport { set; get; }
        //滤色片类型
        public ImageChannelType channelType { set; get; }
        //校正
        public ChannelCalibration check { set; get; }
        //文件名
        public string fileName;
    }
    public class ExpTimeCfg
    {
        public float autoExpTimeBegin { set; get; }
        public bool autoExpFlag { set; get; }
        //自动同步频率
        public float autoExpSyncFreq { set; get; }
        public float autoExpSaturation { set; get; }
        public UInt16 autoExpSatMaxAD { set; get; }
        //
        public double autoExpMaxPecentage { set; get; }
        //误差值
        public float autoExpSatDev { set; get; }
        //最大/小曝光
        public float maxExpTime { set; get; }
        public float minExpTime { set; get; }
        //burst的阈值
        public float burstThreshold { set; get; }
    }

    public class CameraCfg
    {
        public int ob { set; get; }

        public int obR { set; get; }

        public int obB { set; get; }

        public int obT { set; get; }

        public bool tempCtlChecked { set; get; }

        public float targetTemp { set; get; }

        public float usbTraffic { set; get; }

        public int offset { set; get; }

        public int gain { set; get; }

        public int ex { set; get; }

        public int ey { set; get; }

        public int ew { set; get; }

        public int eh { set; get; }
    }
    public class ProjectSysCfg
    {
        [LocalizedCategory(nameof(Resources.ExpTimeConfigCategory)), TypeConverter(typeof(ExpandableObjectConverter)), LocalizedDisplayName(nameof(Resources.ExposureParameters))]
        public ExpTimeCfg expTimeCfg { set; get; }

        [LocalizedCategory(nameof(Resources.CameraConfigCategory)), TypeConverter(typeof(ExpandableObjectConverter)), LocalizedDisplayName(nameof(Resources.CameraParameters))]
        public CameraCfg cameraCfg { set; get; }

        [LocalizedCategory(nameof(Resources.CalibrationLibraryConfigCategory)), LocalizedDisplayName(nameof(Resources.CalibrationParameters))]
        public List<CalibrationItem> calibrationLibCfg { set; get; }

        [LocalizedCategory(nameof(Resources.ChannelConfigCategory)), LocalizedDisplayName(nameof(Resources.ChannelParameters))]
        public List<ChannelCfg> channelCfg { set; get; }

        public ProjectSysCfg()
        {
        }
    }

    public class CalibrationItem
    {
        public string title { set; get; }
        public CalibrationType type { set; get; }
        public bool enable { set; get; }
        public string doc { set; get; }

        public CalibrationItem()
        {
            type = CalibrationType.DefectWPoint;
            enable = false;
            title = "";
            doc = "";
        }
        public CalibrationItem(CalibrationType type, bool enable, string title, string fileName)
        {
            this.type = type;
            this.enable = enable;
            this.title = title;
            this.doc = fileName;
        }
        public override string ToString()
        {
            return string.Format("{0}", type);
        }
    }
}
