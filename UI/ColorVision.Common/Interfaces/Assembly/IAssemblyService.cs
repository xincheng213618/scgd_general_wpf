using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.UI
{
    public interface IAssemblyService
    {
        void RegisterAssembly(Assembly assembly);
        Assembly[] GetAssemblies();
        Assembly[] RefreshAssemblies();

        abstract List<T> LoadImplementations<T>(params object?[]? args) where T : class;

        /// <summary>Raised after the discovered assembly set changes; type queries must be repeated.</summary>
        event EventHandler? AssembliesChanged { add { } remove { } }

        /// <summary>Returns the loadable types of a discovered assembly; load failures yield an empty list.</summary>
        IReadOnlyList<Type> GetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>().ToArray(); }
            catch { return Array.Empty<Type>(); }
        }

        /// <summary>
        /// Returns concrete, closed classes assignable to <paramref name="serviceType"/> across discovered assemblies.
        /// Constructor shape is not checked; callers decide how to instantiate.
        /// </summary>
        IReadOnlyList<Type> GetImplementationTypes(Type serviceType) => GetAssemblies()
            .SelectMany(GetTypes)
            .Where(type => type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters && serviceType.IsAssignableFrom(type))
            .ToArray();
    }


}
