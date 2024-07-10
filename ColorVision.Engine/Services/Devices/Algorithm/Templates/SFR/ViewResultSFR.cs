using ColorVision.Common.MVVM;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class ViewResultSFR : ViewModelBase, IViewResult
    {
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
