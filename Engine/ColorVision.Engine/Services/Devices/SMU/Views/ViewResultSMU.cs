using ColorVision.Common.MVVM;
using ColorVision.UI.Sorts;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using Newtonsoft.Json;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ScottPlot.DataSources;
using ScottPlot.Plottables;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class ViewResultSMU : ViewModelBase
    {
        public ViewResultSMU(SmuScanModel item)
        {
            Id = item.Id;
            CreateTime = item.CreateDate;
            BatchID = item.BatchId;
            MeasurementType = item.IsSourceV ? MeasurementType.Voltage : MeasurementType.Current;
            LimitStart = item.SrcBegin;
            LimitEnd = item.SrcEnd;

            VList = JsonConvert.DeserializeObject<double[]>(item.VResult??string.Empty)?? Array.Empty<Double>();
            IList = JsonConvert.DeserializeObject<double[]>(item.IResult ?? string.Empty) ?? Array.Empty<Double>();
            if (VList != null && IList != null)
            {
                for (int i = 0; i < VList.Length; i++)
                {
                    SMUDatas.Add(new SMUData() { Voltage = VList[i], Current = IList[i] });
                }
            }
            Gen();
        }

        public ViewResultSMU(MeasurementType measurementType , float LimitEnd ,double[] VList, double[] IList)
        {
            Id = 0;

            this.VList = VList;
            this.IList = IList;
            MeasurementType = measurementType;
            this.LimitEnd = LimitEnd;
            Gen();
        }



        public double[] VList { get; set; }
        public double[] IList { get; set; }

        public double xMin { get;set; }
        public double xMax { get; set; }
        public double yMin { get; set; }
        public double yMax { get; set; }


        public int IdShow { get; set; }

        public void Gen()
        {

            List<double> listV = new();
            List<double> listI = new();
            double VMax = 0, IMax = 0, VMin = 10000, IMin = 10000;
            for (int i = 0; i < VList.Length; i++)
            {
                if (VList[i] > VMax) VMax = VList[i];
                if (IList[i] > IMax) IMax = IList[i];
                if (VList[i] < VMin) VMin = VList[i];
                if (IList[i] < IMin) IMin = IList[i];

                listV.Add(VList[i]);
                listI.Add(IList[i]);
            }
            double endVal = LimitEnd;
            int step = 10;
            xMin = 0;
            xMax = VMax + VMax / step;
            yMin = 0 - IMax / step;
            yMax = IMax + IMax / step;
            double[] xs, ys;
            if (MeasurementType == MeasurementType.Voltage)
            {
                xMin = VMin - VMin / step;
                xMax = endVal + VMax / step;
                yMin = IMin - IMin / step;
                yMax = IMax + IMax / step;
                if (VMax < endVal)
                {
                    double addPointStep = (endVal - VMax) / 2.0;
                    listV.Add(VMax + addPointStep);
                    listV.Add(endVal);
                    listI.Add(IMax);
                    listI.Add(IMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();

                ScatterSourceDoubleArray source = new(xs, ys);
                ScatterPlot = new Scatter(source)
                {
                    Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                    LineWidth = 1,
                    MarkerSize = 1,
                    LegendText = string.Empty,
                    MarkerShape = MarkerShape.None,
                };




            }
            else
            {
                endVal = endVal / 1000;
                xMin = IMin - IMin / step;
                xMax = endVal + IMax / step;
                yMin = VMin - VMin / step;
                yMax = VMax + VMax / step;
                if (IMax < endVal)
                {
                    double addPointStep = (endVal - IMax) / 2.0;
                    listI.Add(IMax + addPointStep);
                    listI.Add(endVal);
                    listV.Add(VMax);
                    listV.Add(VMax);
                }
                xs = listV.ToArray();
                ys = listI.ToArray();

                ScatterSourceDoubleArray source = new(xs, ys);
                ScatterPlot = new Scatter(source)
                {
                    Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                    LineWidth = 1,
                    MarkerSize = 1,
                    LegendText = string.Empty,
                    MarkerShape = MarkerShape.None,
                };

            }
        }


        public Scatter ScatterPlot { get; set; }


        public int Id { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;
        public DateTime? CreateTime { get; set; }
        public string? Batch { get; set; }
        public int? BatchID { get; set; }
        public ObservableCollection<SMUData> SMUDatas { get; set; } = new ObservableCollection<SMUData>();


        public MeasurementType MeasurementType { get; set; }

        public  bool IsSourceV { get => MeasurementType == MeasurementType.Voltage; }

        public float LimitStart { get; set; }
        public float LimitEnd { get; set; }





    }
}
