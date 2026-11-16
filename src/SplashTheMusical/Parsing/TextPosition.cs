namespace SplashTheMusical.Parsing;

internal static class TextPosition
{
	/// <summary>The 1-based line and column of an offset, with CRLF, CR and LF each one line break.</summary>
	public static (int Line, int Column) Of(ReadOnlySpan<char> source, int offset)
	{
		var line = 1;
		var lineStart = 0;
		for (var i = 0; i < offset && i < source.Length; i++)
		{
			if (source[i] == '\n' || (source[i] == '\r' && (i + 1 >= source.Length || source[i + 1] != '\n')))
			{
				line++;
				lineStart = i + 1;
			}
		}

		return (line, offset - lineStart + 1);
	}
}
