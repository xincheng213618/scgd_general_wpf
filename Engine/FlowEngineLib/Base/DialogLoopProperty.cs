using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FlowEngineLib.Base;

public class DialogLoopProperty<T> where T : ILoopNodeProperty, new()
{
	private DataGridView dataGridView;

	private List<T> properties;

	public DialogLoopProperty(DataGridView dataGridView)
	{
		this.dataGridView = dataGridView;
	}

	public void Load(string jsonValue)
	{
		dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
		dataGridView.MultiSelect = false;
		if (!string.IsNullOrEmpty(jsonValue))
		{
			properties = JsonConvert.DeserializeObject<List<T>>(jsonValue);
		}
		else
		{
			properties = new List<T>();
		}
		foreach (T property in properties)
		{
			int no = dataGridView.Rows.Count + 1;
			string[] array = property.ToItemArray(no);
			DataGridViewRowCollection rows = dataGridView.Rows;
			object[] values = array;
			rows.Add(values);
		}
	}

	public void Add(T pm)
	{
		properties.Add(pm);
		int no = dataGridView.Rows.Count + 1;
		string[] array = pm.ToItemArray(no);
		DataGridViewRowCollection rows = dataGridView.Rows;
		object[] values = array;
		rows.Add(values);
		ResetNo();
	}

	public void Insert(T pm)
	{
		if (dataGridView.SelectedRows.Count == 1)
		{
			int index = dataGridView.SelectedRows[0].Index;
			properties.Insert(index, pm);
			int no = dataGridView.Rows.Count + 1;
			string[] array = pm.ToItemArray(no);
			DataGridViewRowCollection rows = dataGridView.Rows;
			object[] values = array;
			rows.Insert(index, values);
			ResetNo();
		}
		else
		{
			MessageBox.Show("请先选择");
		}
	}

	public void Remove()
	{
		if (dataGridView.SelectedRows.Count > 0)
		{
			int index = dataGridView.SelectedRows[0].Index;
			dataGridView.Rows.RemoveAt(index);
			properties.RemoveAt(index);
			ResetNo();
		}
		else
		{
			MessageBox.Show("请先选择");
		}
	}

	public string Save()
	{
		JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings();
		jsonSerializerSettings.Converters = new StringEnumConverter[1]
		{
			new StringEnumConverter()
		};
		JsonSerializerSettings settings = jsonSerializerSettings;
		return JsonConvert.SerializeObject(properties, settings);
	}

	private void ResetNo()
	{
		int num = 1;
		foreach (DataGridViewRow item in (IEnumerable)dataGridView.Rows)
		{
			item.Cells[0].Value = num.ToString();
			num++;
		}
	}
}
