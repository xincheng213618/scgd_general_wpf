namespace CVCommCore;

public abstract class CVSingleton<T> where T : class, new()
{
	protected static T _Instance;

	public static T Instance
	{
		get
		{
			if (_Instance == null)
			{
				_Instance = new T();
			}
			return _Instance;
		}
	}

	protected CVSingleton()
	{
		ConstructorInit();
	}

	public virtual void ConstructorInit()
	{
	}
}
