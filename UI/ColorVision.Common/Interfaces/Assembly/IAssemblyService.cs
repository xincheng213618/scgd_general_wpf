using System.Collections.Generic;
using System.Reflection;

namespace ColorVision.UI
{
    public interface IAssemblyService
    {
        Assembly[] GetAssemblies();
        Assembly[] RefreshAssemblies();

        abstract List<T> LoadImplementations<T>(params object?[]? args) where T : class;
    }


}
