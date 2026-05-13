using System;
using System.Reflection;

namespace CVCommCore;

public abstract class ReflectionSingleton<T> where T : class
{
	protected static T _Intance;

	public static T Instance
	{
		get
		{
			if (_Intance == null)
			{
				_Intance = null;
				ConstructorInfo[] constructors = typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
				foreach (ConstructorInfo constructorInfo in constructors)
				{
					ParameterInfo[] parameters = constructorInfo.GetParameters();
					if (parameters.Length == 0)
					{
						_Intance = (T)constructorInfo.Invoke(null);
						break;
					}
				}
				if (_Intance == null)
				{
					throw new NotSupportedException("No NonPublic constructor without 0 parameter");
				}
			}
			return _Intance;
		}
	}

	public static void Destroy()
	{
		_Intance = null;
	}
}
