#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ColorVision.Engine.Services.Devices.Algorithm
{

    public static class CollectionExtensions
    {
        public static ObservableCollection<IViewResult> ToViewResults<T>(this IEnumerable<T> source) where T : IViewResult
        {
            var viewResults = new ObservableCollection<IViewResult>();

            foreach (var item in source)
            {
                viewResults.Add(item);
            }

            return viewResults;
        }

        public static ObservableCollection<T> ToSpecificViewResults<T>(this IEnumerable<IViewResult> source) where T : IViewResult
        {
            var viewResults = new ObservableCollection<T>();

            foreach (var item in source)
            {
                if (item is T t)
                {
                    viewResults.Add(t);
                }
            }

            return viewResults;
        }
    }

    public interface IViewResult
    {

    }
}
