using System.Collections.Generic;
using System.Reflection;

namespace ColorVision.UI
{
    public interface IAssemblyService
    {
        void RegisterAssembly(Assembly assembly);
        Assembly[] GetAssemblies();
        Assembly[] RefreshAssemblies();

        abstract List<T> LoadImplementations<T>(params object?[]? args) where T : class;
    }


}
