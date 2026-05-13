using System.Collections.Generic;
using MQTTMessageLib.Algorithm.Compliance;

namespace MQTTMessageLib.CVMath;

public abstract class CVContrastMath<T>
{
	public virtual List<T> DoCalc(List<T> Data1, List<T> Data2, OperationType OpType)
	{
		List<T> result = null;
		switch (OpType)
		{
		case OperationType.Subtract:
			result = DoSubtract(Data1, Data2);
			break;
		case OperationType.Divide:
			result = DoDivide(Data1, Data2);
			break;
		}
		return result;
	}

	protected virtual List<T> DoSubtract(List<T> data1, List<T> data2)
	{
		List<T> list = new List<T>();
		for (int i = 0; i < data1.Count; i++)
		{
			T d = data1[i];
			T d2 = data2[i];
			T item = DoSubtractItem(d, d2);
			list.Add(item);
		}
		return list;
	}

	protected abstract T DoSubtractItem(T d1, T d2);

	protected virtual List<T> DoDivide(List<T> data1, List<T> data2)
	{
		List<T> list = new List<T>();
		for (int i = 0; i < data1.Count; i++)
		{
			T d = data1[i];
			T d2 = data2[i];
			T item = DoDivideItem(d, d2);
			list.Add(item);
		}
		return list;
	}

	protected abstract T DoDivideItem(T d1, T d2);
}
