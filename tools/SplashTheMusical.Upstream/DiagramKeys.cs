namespace SplashTheMusical.Upstream;

/// <summary>
/// Reads <c>MERMAID_CONFIG_DIAGRAM_KEYS</c> from mermaid's .build/jsonSchema.ts: the config keys whose schema defaults
/// mermaid's build writes into the default config. A diagram key that is not listed gets no schema defaults.
/// </summary>
public static class DiagramKeys
{
	private const string Start = "MERMAID_CONFIG_DIAGRAM_KEYS = [";

	public static List<string> Read(string text)
	{
		var start = text.IndexOf(Start, StringComparison.Ordinal);
		var end = start < 0 ? -1 : text.IndexOf(']', start + Start.Length);
		if (end < 0)
		{
			throw new InvalidDataException("MERMAID_CONFIG_DIAGRAM_KEYS is not in .build/jsonSchema.ts.");
		}

		var keys = new List<string>();
		var list = text.AsSpan(start + Start.Length, end - start - Start.Length);
		while (list.IndexOf('\'') is var open and >= 0)
		{
			list = list[(open + 1)..];
			var close = list.IndexOf('\'');
			keys.Add(list[..close].ToString());
			list = list[(close + 1)..];
		}

		return keys;
	}
}
