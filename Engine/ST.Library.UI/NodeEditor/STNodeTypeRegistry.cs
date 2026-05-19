using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ST.Library.UI.NodeEditor;

internal static class STNodeTypeRegistry
{
	private static readonly Type NodeType = typeof(STNode);
	private static readonly string NodeAssemblyName = NodeType.Assembly.GetName().Name;
	private static readonly object SyncRoot = new object();
	private static readonly HashSet<Type> NodeTypes = new HashSet<Type>();
	private static readonly Dictionary<string, Type> GuidTypes = new Dictionary<string, Type>();
	private static readonly Dictionary<string, Type> ModelTypes = new Dictionary<string, Type>();
	private static readonly Dictionary<Assembly, List<Type>> AssemblyTypes = new Dictionary<Assembly, List<Type>>();
	private static bool _initialized;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}
		lock (SyncRoot)
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				RegisterAssemblyCore(assembly);
			}
		}
	}

	public static int LoadAssemblies(IEnumerable<Assembly> assemblies)
	{
		Initialize();
		int count = 0;
		lock (SyncRoot)
		{
			foreach (Assembly assembly in assemblies)
			{
				RegisterAssemblyCore(assembly);
				if (AssemblyTypes.TryGetValue(assembly, out List<Type> types) && types.Count > 0)
				{
					count++;
				}
			}
		}
		return count;
	}

	public static bool LoadAssembly(string strFile)
	{
		Assembly assembly = Assembly.LoadFrom(strFile);
		return LoadAssembly(assembly);
	}

	public static bool LoadAssembly(Assembly assembly)
	{
		if (assembly == null)
		{
			return false;
		}
		Initialize();
		lock (SyncRoot)
		{
			RegisterAssemblyCore(assembly);
			return AssemblyTypes.TryGetValue(assembly, out List<Type> types) && types.Count > 0;
		}
	}

	public static Type[] GetTypes()
	{
		Initialize();
		lock (SyncRoot)
		{
			return NodeTypes.ToArray();
		}
	}

	public static Type[] GetTypes(Assembly assembly)
	{
		Initialize();
		lock (SyncRoot)
		{
			if (assembly != null && AssemblyTypes.TryGetValue(assembly, out List<Type> types))
			{
				return types.ToArray();
			}
			return Array.Empty<Type>();
		}
	}

	public static Assembly[] GetAssemblies()
	{
		Initialize();
		lock (SyncRoot)
		{
			return AssemblyTypes
				.Where(pair => pair.Value.Count > 0)
				.Select(pair => pair.Key)
				.ToArray();
		}
	}

	public static bool TryGetNodeType(string guid, string model, out Type type)
	{
		Initialize();
		lock (SyncRoot)
		{
			if (!string.IsNullOrEmpty(guid) && GuidTypes.TryGetValue(guid, out type))
			{
				return true;
			}
			if (!string.IsNullOrEmpty(model) && ModelTypes.TryGetValue(model, out type))
			{
				return true;
			}
			type = null;
			return false;
		}
	}

	public static string GetModelByType(Type type)
	{
		return $"{type.Module.Name}|{type.FullName}";
	}

	private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
	{
		lock (SyncRoot)
		{
			RegisterAssemblyCore(args.LoadedAssembly);
		}
	}

	private static void RegisterAssemblyCore(Assembly assembly)
	{
		if (!ShouldScanAssembly(assembly))
		{
			return;
		}

		Type[] types = GetLoadableTypes(assembly);
		if (!AssemblyTypes.TryGetValue(assembly, out List<Type> registeredTypes))
		{
			registeredTypes = new List<Type>();
			AssemblyTypes.Add(assembly, registeredTypes);
		}

		foreach (Type type in types)
		{
			if (!IsNodeType(type) || !NodeTypes.Add(type))
			{
				continue;
			}

			registeredTypes.Add(type);
			string guid = type.GUID.ToString();
			if (!GuidTypes.ContainsKey(guid))
			{
				GuidTypes.Add(guid, type);
			}

			string model = GetModelByType(type);
			if (!ModelTypes.ContainsKey(model))
			{
				ModelTypes.Add(model, type);
			}
		}
	}

	private static bool ShouldScanAssembly(Assembly assembly)
	{
		if (assembly == null || assembly.IsDynamic)
		{
			return false;
		}
		if (assembly == NodeType.Assembly)
		{
			return true;
		}
		try
		{
			foreach (AssemblyName referencedAssembly in assembly.GetReferencedAssemblies())
			{
				if (string.Equals(referencedAssembly.Name, NodeAssemblyName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static Type[] GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(type => type != null).ToArray();
		}
		catch
		{
			return Array.Empty<Type>();
		}
	}

	private static bool IsNodeType(Type type)
	{
		return type != null
			&& type.IsClass
			&& !type.IsAbstract
			&& NodeType.IsAssignableFrom(type);
	}
}
