#pragma warning disable CA1707,CA1861
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public class UniversalSortTests
{
	[Fact]
	public void SortByProperty_UsesLogicalStringOrder()
	{
		var items = new ObservableCollection<SortSample>
		{
			new() { Name = "file10" },
			new() { Name = "file2" },
			new() { Name = "file1" }
		};

		items.SortByProperty(nameof(SortSample.Name));

		Assert.Equal(new[] { "file1", "file2", "file10" }, items.Select(item => item.Name));
	}

	[Fact]
	public void SortByProperty_SortsNullableValues()
	{
		var items = new ObservableCollection<SortSample>
		{
			new() { NullableNumber = 2 },
			new() { NullableNumber = null },
			new() { NullableNumber = 1 }
		};

		items.SortByProperty(nameof(SortSample.NullableNumber));

		Assert.Equal(new int?[] { null, 1, 2 }, items.Select(item => item.NullableNumber));
	}

	[Fact]
	public void SortByProperty_SortsNestedPropertyPathDescending()
	{
		var items = new ObservableCollection<SortSample>
		{
			new() { Child = new SortChild { Rank = 2 } },
			new() { Child = null },
			new() { Child = new SortChild { Rank = 3 } }
		};

		items.SortByProperty("Child.Rank", descending: true);

		Assert.Equal(new int?[] { 3, 2, null }, items.Select(item => item.Child?.Rank));
	}

	private sealed class SortSample
	{
		public string Name { get; set; } = string.Empty;

		public int? NullableNumber { get; set; }

		public SortChild? Child { get; set; }
	}

	private sealed class SortChild
	{
		public int Rank { get; set; }
	}
}
