using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SplashTheMusical.Parsing;

/// <summary>
/// Reads a <c>%%{ ... }%%</c> directive the way mermaid.js's <c>detectDirective</c> does in
/// <c>packages/mermaid/src/utils.ts</c>. Its pattern is
/// <c>%%{\s*(?:(\w+)\s*:|(\w+))\s*(?:(\w+)|((?:(?!}%%).|\r?\n)*))?\s*(?:}%%)?</c>. One word after the colon is kept as a
/// string. Anything else up to <c>}%%</c> has every <c>'</c> replaced with <c>"</c> and is parsed as JSON, here with
/// <see cref="Utf8JsonReader"/>. The closing <c>}%%</c> is optional, as in the pattern.
/// </summary>
internal static class DirectiveReader
{
	private const string Close = "}%%";

	/// <summary>
	/// Reads the directive at the cursor, which is at <c>%%{</c>. Returns <c>false</c>, without moving, when no type name
	/// follows <c>%%{</c>, because mermaid.js does not treat that as a directive. Otherwise moves past the directive and
	/// returns <c>true</c>. When the arguments are not valid JSON, which mermaid.js logs and ignores,
	/// <paramref name="directive"/> holds them in <see cref="MermaidDirective.InvalidArguments"/> and
	/// <paramref name="diagnostics"/> gets a warning.
	/// </summary>
	/// <exception cref="MermaidResourceLimitException">The JSON nests deeper than <see cref="ResourceLimits.MaxRecursionDepth"/>.</exception>
	public static bool TryRead(
		ref SourceCursor cursor,
		List<MermaidDiagnostic> diagnostics,
		ResourceLimits limits,
		out MermaidDirective? directive)
	{
		directive = null;
		var source = cursor.Remaining;
		var typeStart = SkipSpace(source, 3);
		var typeEnd = SkipWord(source, typeStart);
		if (typeEnd == typeStart)
		{
			return false;
		}

		var type = source[typeStart..typeEnd].ToString();
		var afterType = SkipSpace(source, typeEnd);
		if (afterType < source.Length && source[afterType] == ':')
		{
			afterType++;
		}

		var argumentsStart = SkipSpace(source, afterType);
		var wordEnd = SkipWord(source, argumentsStart);
		var line = cursor.Line;
		var column = cursor.Column;
		MermaidValue? arguments = null;
		int end;
		if (wordEnd > argumentsStart)
		{
			arguments = MermaidValue.FromString(source[argumentsStart..wordEnd].ToString());
			end = SkipSpace(source, wordEnd);
		}
		else
		{
			var close = source[argumentsStart..].IndexOf(Close, StringComparison.Ordinal);
			end = close < 0 ? source.Length : argumentsStart + close;
			var json = source[argumentsStart..end].Trim();
			if (!json.IsEmpty && !TryParseJson(json, limits, line, column, out arguments, out var error))
			{
				diagnostics.Add(new MermaidDiagnostic(
					DiagnosticSeverity.Warning,
					$"The '{type}' directive is ignored because its arguments are not valid JSON. {error}",
					line,
					column));
				cursor.Advance(EndAfterClose(source, end));
				directive = new MermaidDirective(type, null) { InvalidArguments = json.ToString() };
				return true;
			}
		}

		cursor.Advance(EndAfterClose(source, end));
		directive = new MermaidDirective(type, arguments);
		return true;
	}

	private static int EndAfterClose(ReadOnlySpan<char> source, int end) =>
		source[end..].StartsWith(Close, StringComparison.Ordinal) ? end + Close.Length : end;

	/// <summary>JavaScript's <c>\s*</c>.</summary>
	private static int SkipSpace(ReadOnlySpan<char> source, int start)
	{
		var i = start;
		while (i < source.Length && (char.IsWhiteSpace(source[i]) || source[i] == (char)0xFEFF))
		{
			i++;
		}

		return i;
	}

	/// <summary>JavaScript's <c>\w+</c>, which is ASCII only.</summary>
	private static int SkipWord(ReadOnlySpan<char> source, int start)
	{
		var i = start;
		while (i < source.Length && (char.IsAsciiLetterOrDigit(source[i]) || source[i] == '_'))
		{
			i++;
		}

		return i;
	}

	private static bool TryParseJson(
		ReadOnlySpan<char> json,
		ResourceLimits limits,
		int line,
		int column,
		out MermaidValue? value,
		out string? error)
	{
		var rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(json.Length));
		try
		{
			var utf8 = rented.AsSpan(0, Encoding.UTF8.GetBytes(json, rented));
			utf8.Replace((byte)'\'', (byte)'"');
			var maxDepth = limits.MaxRecursionDepth;
			var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = maxDepth == int.MaxValue ? maxDepth : maxDepth + 1 });
			reader.Read();
			value = ReadValue(ref reader, 0, maxDepth, line, column);

			// Content after the value throws here.
			reader.Read();
			error = null;
			return true;
		}
		catch (Exception exception) when (exception is JsonException or InvalidOperationException)
		{
			value = null;
			error = exception.Message;
			return false;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	private static MermaidValue ReadValue(ref Utf8JsonReader reader, int depth, int maxDepth, int line, int column)
	{
		switch (reader.TokenType)
		{
			case JsonTokenType.StartObject:
				Enter(depth + 1, maxDepth, line, column);
				var entries = new OrderedDictionary<string, MermaidValue>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					var key = reader.GetString()!;
					reader.Read();
					entries[key] = ReadValue(ref reader, depth + 1, maxDepth, line, column);
				}

				return MermaidValue.FromEntries(entries);
			case JsonTokenType.StartArray:
				Enter(depth + 1, maxDepth, line, column);
				var items = new List<MermaidValue>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					items.Add(ReadValue(ref reader, depth + 1, maxDepth, line, column));
				}

				return MermaidValue.FromItems(items);
			case JsonTokenType.String:
				return MermaidValue.FromString(reader.GetString()!);
			case JsonTokenType.Number:
				return MermaidValue.FromNumber(
					reader.TryGetDouble(out var number)
						? number
						: double.Parse(reader.ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture));
			case JsonTokenType.True:
				return MermaidValue.True;
			case JsonTokenType.False:
				return MermaidValue.False;
			default:
				return MermaidValue.Null;
		}
	}

	private static void Enter(int depth, int maxDepth, int line, int column)
	{
		if (depth > maxDepth)
		{
			throw new MermaidResourceLimitException(nameof(ResourceLimits.MaxRecursionDepth), depth, maxDepth, line, column);
		}
	}
}
