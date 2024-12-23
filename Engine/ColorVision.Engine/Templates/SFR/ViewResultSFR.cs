using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.SFR
{
    public class ViewResultSFR : ViewModelBase, IViewResult
    {
        public static void SaveCsv(ObservableCollection<ViewResultSFR> ViewResultSFRs, string FileName)
        {
            var csvBuilder = new StringBuilder();
            List<string> properties = new() { "pdfrequency", "pdomainSamplingData" };
            // 写入列头
            csvBuilder.AppendLine(string.Join(",", properties));
            // 写入数据行
            foreach (var item in ViewResultSFRs)
            {
                List<string> values = new()
                {
                    item.pdfrequency.ToString(),
                    item.pdomainSamplingData.ToString(),
                };

                csvBuilder.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(FileName, csvBuilder.ToString(), Encoding.UTF8);
        }


        public ViewResultSFR(float pdfrequency, float pdomainSamplingData)
        {
            this.pdfrequency = pdfrequency;
            this.pdomainSamplingData = pdomainSamplingData;
        }
        public float pdfrequency { get; set; }
        public float pdomainSamplingData { get; set; }

        public static bool IsSupportGridView => true;

        public static IEnumerable<GridViewColumn> GetGridViewColumns()
        {
            List<GridViewColumn> gridViewColumns = new List<GridViewColumn>();
            List<string> bdheadersSFR = new() { "pdfrequency", "pdomainSamplingData" };
            List<string> headersSFR = new() { "pdfrequency", "pdomainSamplingData" };

            for (int i = 0; i < headersSFR.Count; i++)
                gridViewColumns.Add(new GridViewColumn() { Header = headersSFR[i], DisplayMemberBinding = new Binding(bdheadersSFR[i]) });
            return gridViewColumns;
        }
    }
}
