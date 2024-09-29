using ColorVision.Engine.Media;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Net;
using MQTTMessageLib.Algorithm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.BuildPoi
{
    public class BuildPoiFileHandle : IResultHandle
    {
        public AlgorithmResultType ResultType => AlgorithmResultType.BuildPOI_File;

        public void Handle(AlgorithmView view, AlgorithmResult result)
        {
            view.ImageView.ImageShow.Clear();
            List<string> header = new();
            List<string> bdHeader = new();

            string filepath = "C:\\Users\\17917\\Desktop\\20240927T171615.9133690_1000ND.cvraw";
            CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filepath);
            HImage hImage = cVCIEFile.ToWriteableBitmap().ToHImage();

            int[] ints = new int[50];
            for (int i = 0; i < 50; i++)
            {
                ints[i] = 50 * i;
            }
            int ret = OpenCVMediaHelper.M_DrawPoiImage(hImage, out HImage hImageProcessed, 30, ints, 50);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ret == 0)
                {
                    var image = hImageProcessed.ToWriteableBitmap();

                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;
                    view.ImageView.PseudoImage = image;
                    view.ImageView.ImageShow.Source = view.ImageView.PseudoImage;
                }
            });

            if (File.Exists(result.FilePath))
            {
                view.ImageView.OpenImage(result.FilePath);





            }
            if (result.ViewResults == null)
            {
                result.ViewResults = new ObservableCollection<IViewResult>();
                List<PoiCieFileModel> models = PoiCieFileDao.Instance.GetAllByPid(result.Id);
                foreach (var item in models)
                {
                    result.ViewResults.Add(item);
                }
                header = new List<string> { "id", "file_name", "file_url", "fileType" };
                bdHeader = new List<string> { "Id", "FileName", "FileUrl", "file_type" };
            }
            if (view.listViewSide.View is GridView gridView)
            {
                view.LeftGridViewColumnVisibilitys.Clear();
                gridView.Columns.Clear();
                for (int i = 0; i < header.Count; i++)
                    gridView.Columns.Add(new GridViewColumn() { Header = header[i], DisplayMemberBinding = new Binding(bdHeader[i]) });
                view.listViewSide.ItemsSource = result.ViewResults;
            }

        }
    }
}
