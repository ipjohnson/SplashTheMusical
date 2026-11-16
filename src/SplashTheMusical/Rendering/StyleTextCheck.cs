using System.Buffers;

namespace SplashTheMusical.Rendering;

/// <summary>
/// Checks a stylesheet against the grammar <see cref="SvgWriter.StyleText"/> writes, so <see cref="SvgSanitizer"/> can
/// keep the <c>style</c> element of STM's own output and nothing else.
/// </summary>
internal static class StyleTextCheck
{
	/// <summary>
	/// Whether <paramref name="css"/> is a run of style rules whose selectors each start with <c>#<paramref name="svgId"/></c>
	/// and of <c>@keyframes</c> rules, and whose declarations each pass <see cref="CssDeclaration.IsAllowedText"/>.
	/// </summary>
	public static bool IsAllowed(ReadOnlySpan<char> css, string svgId)
	{
		if (!CssText.IsIdentifier(svgId))
		{
			return false;
		}

		while (!css.IsEmpty)
		{
			var open = css.IndexOf('{');
			if (open < 0)
			{
				return false;
			}

			var prelude = css[..open];
			css = css[(open + 1)..];
			if (prelude.StartsWith("@keyframes ", StringComparison.Ordinal))
			{
				if (!CssText.IsIdentifier(prelude["@keyframes ".Length..]) || !Keyframes(ref css, svgId))
				{
					return false;
				}
			}
			else if (!Selectors(prelude, svgId) || !Block(ref css, svgId))
			{
				return false;
			}
		}

		return true;
	}

	private static bool Keyframes(ref ReadOnlySpan<char> css, string svgId)
	{
		while (!css.IsEmpty)
		{
			if (css[0] == '}')
			{
				css = css[1..];
				return true;
			}

			var open = css.IndexOf('{');
			if (open < 0 || !IsFrame(css[..open]))
			{
				return false;
			}

			css = css[(open + 1)..];
			if (!Block(ref css, svgId))
			{
				return false;
			}
		}

		return false;
	}

	private static bool IsFrame(ReadOnlySpan<char> frame) =>
		frame is "from" or "to"
		|| (frame.Length > 1 && frame[^1] == '%' && !frame[..^1].ContainsAnyExcept(Percentage) && frame.Count('.') <= 1 && frame[0] != '.');

	private static readonly SearchValues<char> Percentage = SearchValues.Create("0123456789.");

	private static bool Selectors(ReadOnlySpan<char> prelude, string svgId)
	{
		foreach (var range in prelude.Split(','))
		{
			var selector = prelude[range];
			if (selector.Length < svgId.Length + 1 || selector[0] != '#' || !selector[1..].StartsWith(svgId, StringComparison.Ordinal))
			{
				return false;
			}

			var rest = selector[(svgId.Length + 1)..];
			if (!rest.IsEmpty && (rest[0] != ' ' || !CssSelector.IsAllowed(rest[1..])))
			{
				return false;
			}
		}

		return true;
	}

	// The declarations of a block, each ended by ;, up to and past its }.
	private static bool Block(ref ReadOnlySpan<char> css, string svgId)
	{
		var close = css.IndexOf('}');
		if (close < 0)
		{
			return false;
		}

		var body = css[..close];
		css = css[(close + 1)..];
		while (!body.IsEmpty)
		{
			var end = body.IndexOf(';');
			if (end < 0 || !CssDeclaration.IsAllowedText(body[..end], svgId))
			{
				return false;
			}

			body = body[(end + 1)..];
		}

		return true;
	}
}
