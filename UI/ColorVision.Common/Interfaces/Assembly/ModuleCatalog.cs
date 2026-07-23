using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.UI
{
    public enum ModuleKind
    {
        BuiltIn,
        Plugin
    }

    public sealed record ModuleRegistration(string Id, ModuleKind Kind, Assembly Assembly);

    /// <summary>
    /// Records the assemblies that intentionally contribute application modules.
    /// Built-in modules register through compile-time type references; external
    /// plugins register after their assemblies have been loaded by PluginLoader.
    /// </summary>
    public sealed class ModuleCatalog
    {
        private readonly IAssemblyService _assemblyService;
        private readonly Dictionary<string, ModuleRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();
        private bool _isSealed;

        public ModuleCatalog(IAssemblyService assemblyService)
        {
            _assemblyService = assemblyService ?? throw new ArgumentNullException(nameof(assemblyService));
        }

        public bool IsSealed
        {
            get
            {
                lock (_lock)
                    return _isSealed;
            }
        }

        public IReadOnlyList<ModuleRegistration> Registrations
        {
            get
            {
                lock (_lock)
                    return _registrations.Values.ToArray();
            }
        }

        public void AddBuiltIn(string id, Assembly assembly) => Add(id, ModuleKind.BuiltIn, assembly);

        public void AddPlugin(string id, Assembly assembly) => Add(id, ModuleKind.Plugin, assembly);

        public void Seal()
        {
            lock (_lock)
                _isSealed = true;
        }

        private void Add(string id, ModuleKind kind, Assembly assembly)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentNullException.ThrowIfNull(assembly);

            string key = $"{kind}:{id}";
            lock (_lock)
            {
                if (_isSealed)
                    throw new InvalidOperationException("The module catalog is sealed and cannot accept additional modules.");

                if (_registrations.TryGetValue(key, out ModuleRegistration? existing))
                {
                    if (!ReferenceEquals(existing.Assembly, assembly))
                        throw new InvalidOperationException($"Module '{id}' is already registered with a different assembly.");

                    return;
                }

                _assemblyService.RegisterAssembly(assembly);
                _registrations.Add(key, new ModuleRegistration(id, kind, assembly));
            }
        }
    }
}
