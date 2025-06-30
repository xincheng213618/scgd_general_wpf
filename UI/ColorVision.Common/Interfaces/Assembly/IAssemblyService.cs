using System.Collections.Generic;
using System.Reflection;

namespace ColorVision.UI
{
    public interface IAssemblyService
    {
        Assembly[] GetAssemblies();
        abstract List<T> LoadImplementations<T>(params object?[]? args) where T : class;
    }


}
