namespace SplashTheMusical.Layout.Dagre;

/// <summary>The rules of JavaScript's property keys that decide graphlib's iteration order.</summary>
internal static class JsKeys
{
	private const long MaxArrayIndex = 4294967294;

	/// <summary>
	/// The key's value when it is an array index: the canonical decimal form of an integer from 0 to 2^32 - 2, such as
	/// <c>"0"</c> or <c>"17"</c> but not <c>"017"</c> or <c>"-1"</c>. Otherwise -1.
	/// </summary>
	public static long ArrayIndex(string key)
	{
		if (key.Length == 0 || key.Length > 10 || (key[0] == '0' && key.Length > 1))
		{
			return -1;
		}

		long value = 0;
		foreach (var c in key)
		{
			if (c is < '0' or > '9')
			{
				return -1;
			}

			value = (value * 10) + (c - '0');
		}

		return value <= MaxArrayIndex ? value : -1;
	}
}
