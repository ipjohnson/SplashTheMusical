using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>The db's <c>parseMessage</c> and <c>extractWrap</c>, over spans.</summary>
internal static class SequenceText
{
	/// <summary>
	/// <c>parseMessage</c>: the text trimmed, without the <c>/^:?(?:no)?wrap:/</c> at its start, trimmed again.
	/// <paramref name="wrap"/> is <c>true</c> for <c>wrap:</c>, <c>false</c> for <c>nowrap:</c> and <c>null</c> otherwise.
	/// </summary>
	public static ReadOnlySpan<char> ParseMessage(ReadOnlySpan<char> text, out bool? wrap)
	{
		text = StateLexer.Trim(text);
		var prefix = WrapPrefixLength(text, out wrap);
		return prefix == 0 ? text : StateLexer.Trim(text[prefix..]);
	}

	/// <summary>The length of <c>/^:?(?:no)?wrap:/</c> at the start of <paramref name="text"/>, or 0.</summary>
	public static int WrapPrefixLength(ReadOnlySpan<char> text, out bool? wrap)
	{
		wrap = null;
		var at = text.StartsWith(':') ? 1 : 0;
		if (text[at..].StartsWith("wrap:", StringComparison.Ordinal))
		{
			wrap = true;
			return at + 5;
		}

		if (text[at..].StartsWith("nowrap:", StringComparison.Ordinal))
		{
			wrap = false;
			return at + 7;
		}

		return 0;
	}
}
