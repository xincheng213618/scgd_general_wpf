﻿#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Algorithm.Views
{
    public class GhostPointResultData : ViewModelBase
    {
        public GhostPointResultData(PointFloat centerPoint, float ledBlobGray, float ghostAvrGray)
        {
            CenterPoint = centerPoint;
            LedBlobGray = ledBlobGray;
            GhostAvrGray = ghostAvrGray;
        }

        public PointFloat CenterPoint { get; set; }
        public string CenterPointDis
        {
            get
            {
                return string.Format("{0},{1}", CenterPoint.X, CenterPoint.Y);
            }
        }
        public float LedBlobGray { get; set; }
        public float GhostAvrGray { get; set; }
    }
}
