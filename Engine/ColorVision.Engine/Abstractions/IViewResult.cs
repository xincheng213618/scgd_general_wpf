using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Abstractions
{
    public interface IViewResult
    {

    }


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
            if (source !=null)
            {
                foreach (var item in source)
                {
                    if (item is T t)
                    {
                        viewResults.Add(t);
                    }
                }
            }

            return viewResults;
        }
    }



}
