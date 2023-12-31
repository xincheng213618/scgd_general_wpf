﻿#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Algorithm.Views
{
    public class PoiResultCIExyuvData : PoiResultData
    {
        public double CCT { get { return _CCT; } set { _CCT = value; NotifyPropertyChanged(); } }
        public double Wave { get { return _Wave; } set { _Wave = value; NotifyPropertyChanged(); } }
        public double X { get { return _X; } set { _X = value; NotifyPropertyChanged(); } }
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }
        public double Z { get { return _Z; } set { _Z = value; NotifyPropertyChanged(); } }
        public double u { get { return _u; } set { _u = value; NotifyPropertyChanged(); } }
        public double v { get { return _v; } set { _v = value; NotifyPropertyChanged(); } }
        public double x { get { return _x; } set { _x = value; NotifyPropertyChanged(); } }
        public double y { get { return _y; } set { _y = value; NotifyPropertyChanged(); } }

        private double _y;
        private double _x;
        private double _u;
        private double _v;
        private double _X;
        private double _Y;
        private double _Z;
        private double _Wave;
        private double _CCT;

        public PoiResultCIExyuvData(POIPoint point, POIDataCIExyuv data)
        {
            this.Point = point;
            this.u = data.u;
            this.v = data.v;
            this.x = data.x;
            this.y = data.y;
            this.X = data.X;
            this.Y = data.Y;
            this.Z = data.Z;
            this.CCT = data.CCT;
            this.Wave = data.Wave;
        }
    }
}
