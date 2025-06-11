using System.Collections.Generic;
using System.Reflection;

namespace ColorVision.UI
{
    public interface IAssemblyService
    {
        public Assembly[] GetAssemblies();
        public abstract List<T> LoadImplementations<T>(params object?[]? args) where T : class;
    }


}
