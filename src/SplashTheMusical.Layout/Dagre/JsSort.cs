namespace SplashTheMusical.Layout.Dagre;

/// <summary>
/// V8's <c>Array.prototype.sort</c> for the arrays dagre sorts with a comparator. V8 runs TimSort, which sorts an
/// array shorter than 64 by taking its first run and binary-inserting the rest. This port does that for every length.
/// It gives V8's result for any comparator below 64 elements, and for a consistent comparator at any length.
/// </summary>
internal static class JsSort
{
	public static void Sort<T>(List<T> items, Func<T, T, double> compare)
	{
		var length = items.Count;
		if (length < 2)
		{
			return;
		}

		var run = CountAndMakeRun(items, length, compare);
		for (var start = run; start < length; start++)
		{
			var pivot = items[start];
			var left = 0;
			var right = start;
			while (left < right)
			{
				var mid = left + ((right - left) >> 1);
				if (Order(compare(pivot, items[mid])) < 0)
				{
					right = mid;
				}
				else
				{
					left = mid + 1;
				}
			}

			for (var p = start; p > left; p--)
			{
				items[p] = items[p - 1];
			}

			items[left] = pivot;
		}
	}

	/// <summary>The length of the run at the start: ascending, or strictly descending and then reversed.</summary>
	private static int CountAndMakeRun<T>(List<T> items, int length, Func<T, T, double> compare)
	{
		if (length == 1)
		{
			return 1;
		}

		var runLength = 2;
		var previous = items[1];
		var descending = Order(compare(previous, items[0])) < 0;
		for (var i = 2; i < length; i++)
		{
			var current = items[i];
			var order = Order(compare(current, previous));
			if (descending ? order >= 0 : order < 0)
			{
				break;
			}

			previous = current;
			runLength++;
		}

		if (descending)
		{
			items.Reverse(0, runLength);
		}

		return runLength;
	}

	/// <summary>V8 reads a comparator's NaN as 0.</summary>
	private static double Order(double value) => double.IsNaN(value) ? 0 : value;
}
