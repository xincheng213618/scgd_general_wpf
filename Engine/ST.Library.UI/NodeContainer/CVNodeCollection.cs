using System;
using System.Collections;
using ST.Library.UI.NodeEditor;

namespace ST.Library.UI.NodeContainer;

public class CVNodeCollection : IList, ICollection, IEnumerable
{
	private int _Count;

	private STNode[] m_nodes;

	private CVNodeContainer m_owner;

	public int Count => _Count;

	public bool IsFixedSize => false;

	public bool IsReadOnly => false;

	public STNode this[int nIndex]
	{
		get
		{
			if (nIndex < 0 || nIndex >= _Count)
			{
				throw new IndexOutOfRangeException("索引越界");
			}
			return m_nodes[nIndex];
		}
		set
		{
			throw new InvalidOperationException("禁止重新赋值元素");
		}
	}

	public bool IsSynchronized => true;

	public object SyncRoot => this;

	bool IList.IsFixedSize => IsFixedSize;

	bool IList.IsReadOnly => IsReadOnly;

	object IList.this[int index]
	{
		get
		{
			return this[index];
		}
		set
		{
			this[index] = (STNode)value;
		}
	}

	int ICollection.Count => _Count;

	bool ICollection.IsSynchronized => IsSynchronized;

	object ICollection.SyncRoot => SyncRoot;

	public CVNodeCollection(CVNodeContainer owner)
	{
		if (owner == null)
		{
			throw new ArgumentNullException("所有者不能为空");
		}
		m_owner = owner;
		m_nodes = new STNode[4];
	}

	public void MoveToEnd(STNode node)
	{
		if (_Count < 1 || m_nodes[_Count - 1] == node)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < _Count - 1; i++)
		{
			if (m_nodes[i] == node)
			{
				flag = true;
			}
			if (flag)
			{
				m_nodes[i] = m_nodes[i + 1];
			}
		}
		m_nodes[_Count - 1] = node;
	}

	public int Add(STNode node)
	{
		if (node == null)
		{
			throw new ArgumentNullException("添加对象不能为空");
		}
		EnsureSpace(1);
		int num = IndexOf(node);
		if (-1 == num)
		{
			num = _Count;
			m_nodes[_Count++] = node;
			m_owner.OnNodeAdded(new STNodeEditorEventArgs(node));
		}
		return num;
	}

	public void AddRange(STNode[] nodes)
	{
		if (nodes == null)
		{
			throw new ArgumentNullException("添加对象不能为空");
		}
		EnsureSpace(nodes.Length);
		foreach (STNode sTNode in nodes)
		{
			if (sTNode == null)
			{
				throw new ArgumentNullException("添加对象不能为空");
			}
			if (-1 == IndexOf(sTNode))
			{
				m_nodes[_Count++] = sTNode;
			}
			m_owner.OnNodeAdded(new STNodeEditorEventArgs(sTNode));
		}
	}

	public void Clear()
	{
		for (int i = 0; i < _Count; i++)
		{
			m_nodes[i].Owner = null;
			foreach (STNodeOption inputOption in m_nodes[i].InputOptions)
			{
				inputOption.DisConnectionAll();
			}
			foreach (STNodeOption outputOption in m_nodes[i].OutputOptions)
			{
				outputOption.DisConnectionAll();
			}
			m_owner.OnNodeRemoved(new STNodeEditorEventArgs(m_nodes[i]));
		}
		_Count = 0;
		m_nodes = new STNode[4];
	}

	public bool Contains(STNode node)
	{
		return IndexOf(node) != -1;
	}

	public int IndexOf(STNode node)
	{
		return Array.IndexOf(m_nodes, node);
	}

	public void Insert(int nIndex, STNode node)
	{
		if (nIndex < 0 || nIndex >= _Count)
		{
			throw new IndexOutOfRangeException("索引越界");
		}
		if (node == null)
		{
			throw new ArgumentNullException("插入对象不能为空");
		}
		EnsureSpace(1);
		for (int num = _Count; num > nIndex; num--)
		{
			m_nodes[num] = m_nodes[num - 1];
		}
		m_nodes[nIndex] = node;
		_Count++;
	}

	public void Remove(STNode node)
	{
		int num = IndexOf(node);
		if (num != -1)
		{
			RemoveAt(num);
		}
	}

	public void RemoveAt(int nIndex)
	{
		if (nIndex < 0 || nIndex >= _Count)
		{
			throw new IndexOutOfRangeException("索引越界");
		}
		m_nodes[nIndex].Owner = null;
		_Count--;
		int i = nIndex;
		for (int count = _Count; i < count; i++)
		{
			m_nodes[i] = m_nodes[i + 1];
		}
	}

	public void CopyTo(Array array, int index)
	{
		if (array == null)
		{
			throw new ArgumentNullException("数组不能为空");
		}
		m_nodes.CopyTo(array, index);
	}

	public IEnumerator GetEnumerator()
	{
		int i = 0;
		for (int Len = _Count; i < Len; i++)
		{
			yield return m_nodes[i];
		}
	}

	private void EnsureSpace(int elements)
	{
		if (elements + _Count > m_nodes.Length)
		{
			STNode[] array = new STNode[Math.Max(m_nodes.Length * 2, elements + _Count)];
			m_nodes.CopyTo(array, 0);
			m_nodes = array;
		}
	}

	int IList.Add(object value)
	{
		return Add((STNode)value);
	}

	void IList.Clear()
	{
		Clear();
	}

	bool IList.Contains(object value)
	{
		return Contains((STNode)value);
	}

	int IList.IndexOf(object value)
	{
		return IndexOf((STNode)value);
	}

	void IList.Insert(int index, object value)
	{
		Insert(index, (STNode)value);
	}

	void IList.Remove(object value)
	{
		Remove((STNode)value);
	}

	void IList.RemoveAt(int index)
	{
		RemoveAt(index);
	}

	void ICollection.CopyTo(Array array, int index)
	{
		CopyTo(array, index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public STNode[] ToArray()
	{
		STNode[] array = new STNode[_Count];
		for (int gZipStream = 0; gZipStream < array.Length; gZipStream++)
		{
			array[gZipStream] = m_nodes[gZipStream];
		}
		return array;
	}
}
