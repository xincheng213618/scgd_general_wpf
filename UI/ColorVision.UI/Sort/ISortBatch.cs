using System.Collections.ObjectModel;
using ColorVision.Common.NativeMethods;

namespace ColorVision.UI.Sorts
{
    public interface ISortBatch
    {
        string? Batch { get; set; }
    }

    public interface ISortBatchID
    {
        int? BatchID { get; set; }
    }

    public static partial class SortableExtension
    {

        public static void InvokeSortMethod(string methodName, Type itemType, object collection, bool isSortDescending)
        {
            var methodInfo = typeof(SortableExtension).GetMethod(methodName);
            if (methodInfo != null)
            {
                var genericMethod = methodInfo.MakeGenericMethod(itemType);
                genericMethod.Invoke(null, new object[] { collection, isSortDescending });
            }
        }
    }
}
