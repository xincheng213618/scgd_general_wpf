using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using Newtonsoft.Json;
using ScottPlot;
using System;
using System.Collections.ObjectModel;
using ScottPlot.DataSources;
using ScottPlot.Plottables;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class ViewResultSMU : ViewModelBase
    {
        public ViewResultSMU(SmuScanModel item)
        {
            LocalDetails = item.LocalDetails;
            Type = "Scan";
            Id = item.Id;
            CreateTime = item.CreateDate;
            TotalTime = item.TotalTime;
            BatchID = item.BatchId;
            ChannelType = item.ChannelType;
            if (item.IsSourceV!=null)
                MeasurementType = item.IsSourceV ==true ? MeasurementType.Voltage : MeasurementType.Current;
            LimitStart = item.SrcBegin ?? 0;
            LimitEnd = item.SrcEnd??0;

            VList = JsonConvert.DeserializeObject<double[]>(item.VResult??string.Empty)?? Array.Empty<Double>();
            IList = JsonConvert.DeserializeObject<double[]>(item.IResult ?? string.Empty) ?? Array.Empty<Double>();
            if (VList != null && IList != null)
            {
                for (int i = 0; i < Math.Min(VList.Length, IList.Length); i++)
                {
                    SMUDatas.Add(new SMUData() { Voltage = VList[i], Current = DisplayCurrent(IList[i]) });
                }
            }
            Gen();
        }


        /// <summary>
        /// Constructor for single-point measurement data from SMUResultModel.
        /// Unlike SmuScanModel (which has arrays of V/I data), SMUResultModel contains only one VI point.
        /// VList and IList arrays are sized to 1 element to match the single measurement point.
        /// </summary>
        public ViewResultSMU(SMUResultModel item)
        {
            LocalDetails = item.LocalDetails;
            Type = "GetData";
            Id = item.Id;
            CreateTime = item.CreateDate;
            TotalTime = item.TotalTime;
            BatchID = item.BatchId;
            ChannelType = item.ChannelType;
            MeasurementType = item.IsSourceV == true ? MeasurementType.Voltage : MeasurementType.Current;
            LimitStart = item.SrcValue ?? 0;
            LimitEnd = item.LimitValue ?? 0;
            VResult = item.VResult ?? 0;
            IResult = item.IResult ?? 0;
            // For single-point measurement (SMUResultModel), use actual VResult/IResult as the single data point
            VList = new double[] { VResult };
            IList = new double[] { IResult };
            SMUDatas.Add(new SMUData() { Voltage = VResult, Current = IResult });
            Gen();
        }


        public ViewResultSMU(MeasurementType measurementType , float LimitEnd ,double[] VList, double[] IList)
        {
            Type = "Scan";
            Id = 0;

            this.VList = VList;
            this.IList = IList;
            MeasurementType = measurementType;
            this.LimitEnd = LimitEnd;
            if (VList != null && IList != null)
            {
                for (int i = 0; i < Math.Min(VList.Length, IList.Length); i++)
                {
                    SMUDatas.Add(new SMUData() { Voltage = VList[i], Current = DisplayCurrent(IList[i]) });
                }
            }
            Gen();
        }

        public string Type { get; set; }

        public string? LocalDetails { get; private set; }
        private void ApplyLocalDetails()
        {
            if (string.IsNullOrWhiteSpace(LocalDetails)) return;
            try
            {
                var details = JsonConvert.DeserializeObject<Local.LocalSmuDetails>(LocalDetails);
                if (details?.Settings?.Measurement != Local.SmuMeasurement.Resistance || details.Resistance == null) return;
                for (int index = 0; index < Math.Min(SMUDatas.Count, details.Resistance.Length); index++)
                { SMUDatas[index].HasNativeResistance = true; SMUDatas[index].NativeResistance = details.Resistance[index]; }
            }
            catch (JsonException ex) { log4net.LogManager.GetLogger(typeof(ViewResultSMU)).Warn("无法读取源表历史结果的本地模式信息", ex); }
        }
        public double VResult { get; set; }
        public double IResult { get; set; }

        public double[] VList { get; set; }
        public double[] IList { get; set; }

        public double xMin { get;set; }
        public double xMax { get; set; }
        public double yMin { get; set; }
        public double yMax { get; set; }


        public int IdShow { get; set; }

        public void Gen()
        {
            ApplyLocalDetails();
            int count = Math.Min(VList?.Length ?? 0, IList?.Length ?? 0);
            double[] xs = new double[count], ys = new double[count];
            double minimumX = double.PositiveInfinity, maximumX = double.NegativeInfinity;
            double minimumY = double.PositiveInfinity, maximumY = double.NegativeInfinity;
            for (int index = 0; index < count; index++)
            {
                double current = DisplayCurrent(IList![index]);
                xs[index] = MeasurementType == MeasurementType.Voltage ? VList![index] : current;
                ys[index] = MeasurementType == MeasurementType.Voltage ? current : VList![index];
                if (!double.IsFinite(xs[index]) || !double.IsFinite(ys[index])) continue;
                minimumX = Math.Min(minimumX, xs[index]); maximumX = Math.Max(maximumX, xs[index]);
                minimumY = Math.Min(minimumY, ys[index]); maximumY = Math.Max(maximumY, ys[index]);
            }
            (xMin, xMax) = PaddedRange(minimumX, maximumX);
            (yMin, yMax) = PaddedRange(minimumY, maximumY);
            ScatterPlot = new Scatter(new ScatterSourceDoubleArray(xs, ys))
            {
                Color = Color.FromColor(System.Drawing.Color.DarkGoldenrod),
                LineWidth = 1,
                MarkerSize = 1,
                LegendText = string.Empty,
                MarkerShape = MarkerShape.None,
            };
        }

        // Scan storage/payloads retain A; the chart and table use mA, like single-point results.
        private double DisplayCurrent(double current) => Type == "Scan" ? current * 1000 : current;

        private static (double Minimum, double Maximum) PaddedRange(double minimum, double maximum)
        {
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum)) return (0, 1);
            double padding = minimum < maximum ? (maximum - minimum) * 0.1 : minimum == 0 ? 1 : Math.Abs(minimum) * 0.1;
            return (minimum - padding, maximum + padding);
        }

        public Scatter ScatterPlot { get; set; }


        public int Id { get => _ID; set { _ID = value; OnPropertyChanged(); } }
        private int _ID;
        public DateTime? CreateTime { get; set; }
        public int? TotalTime { get; set; }
        public string? Batch { get; set; }
        public int? BatchID { get; set; }
        public ObservableCollection<SMUData> SMUDatas { get; set; } = new ObservableCollection<SMUData>();

        public MeasurementType MeasurementType { get; set; }

        public SMUChannelType ChannelType { get; set; } = SMUChannelType.A;

        public  bool IsSourceV { get => MeasurementType == MeasurementType.Voltage; }

        public float LimitStart { get; set; }
        public float LimitEnd { get; set; }





    }
}
