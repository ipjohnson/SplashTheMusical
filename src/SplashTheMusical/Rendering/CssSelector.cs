using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>
/// One complex selector of a rule, held as stylis writes it. <see cref="SvgWriter"/> puts the SVG's id selector in
/// front of it, as mermaid's <c>compileCSS</c> scopes every rule. <see cref="Root"/> is the id selector alone.
/// </summary>
internal readonly struct CssSelector : IEquatable<CssSelector>
{
	private CssSelector(string text) => Text = text;

	/// <summary>The SVG's own id selector, mermaid's <c>&amp;</c>.</summary>
	public static CssSelector Root { get; } = new(string.Empty);

	/// <summary>The selector after the SVG's id, or empty for <see cref="Root"/>.</summary>
	public string Text { get; }

	public bool IsRoot => Text.Length == 0;

	/// <summary>
	/// A selector the renderer writes, such as <c>.node .neo-node</c> or <c>[data-look="neo"].node rect</c>. It may hold
	/// names, <c>.</c>, <c>#</c>, <c>*</c>, <c>:</c>, the combinators <c>&gt;</c>, <c>+</c>, <c>~</c> and space,
	/// attribute selectors whose value is a quoted name, <c>:not()</c> of a compound selector and <c>:nth-of-type()</c>.
	/// </summary>
	/// <exception cref="ArgumentException">The selector holds anything else.</exception>
	public static CssSelector Parse(string selector)
	{
		ArgumentNullException.ThrowIfNull(selector);
		return IsAllowed(selector) ? new(Normalize(selector)) : throw new ArgumentException($"'{selector}' is not an allowed selector.", nameof(selector));
	}

	/// <summary>
	/// <c>.<paramref name="className"/> <paramref name="element"/></c>, the selector mermaid's <c>cssImportantStyles</c>
	/// writes for a <c>classDef</c>. The class name comes from the diagram source, so it is refused unless it is a CSS
	/// identifier.
	/// </summary>
	public static bool TryClass(string className, string element, out CssSelector selector)
	{
		ArgumentNullException.ThrowIfNull(className);
		ArgumentNullException.ThrowIfNull(element);
		if (!CssText.IsIdentifier(className) || !IsAllowed(element))
		{
			selector = default;
			return false;
		}

		selector = new(Normalize("." + className + " " + element));
		return true;
	}

	private static string Normalize(string selector)
	{
		var builder = new StringBuilder(selector.Length);
		CssText.AppendStylis(builder, selector);
		return builder.ToString();
	}

	/// <summary>Whether <paramref name="selector"/> fits the grammar of <see cref="Parse"/>.</summary>
	public static bool IsAllowed(ReadOnlySpan<char> selector)
	{
		var anything = false;
		for (var i = 0; i < selector.Length; i++)
		{
			var c = selector[i];
			if (c == '[')
			{
				var end = selector[i..].IndexOf(']');
				if (end < 0 || !IsAttribute(selector.Slice(i + 1, end - 1)))
				{
					return false;
				}

				i += end;
				anything = true;
			}
			else if (c == '(')
			{
				var end = selector[i..].IndexOf(')');
				if (end < 0 || !IsPseudoArgument(selector[..i], selector.Slice(i + 1, end - 1)))
				{
					return false;
				}

				i += end;
			}
			else if (CssText.IsNameChar(c) || c is '.' or '#' or '*' or ':')
			{
				anything = true;
			}
			else if (!CssText.IsWhitespace(c) && c is not ('>' or '+' or '~'))
			{
				return false;
			}
		}

		return anything;
	}

	// :not( and :nth-of-type( are the only functional pseudo-classes. :not takes a compound of names, classes and ids, and
	// :nth-of-type takes an+b with no spaces.
	private static bool IsPseudoArgument(ReadOnlySpan<char> before, ReadOnlySpan<char> argument)
	{
		if (argument.IsEmpty)
		{
			return false;
		}

		if (before.EndsWith(":not", StringComparison.Ordinal))
		{
			foreach (var c in argument)
			{
				if (!CssText.IsNameChar(c) && c is not ('.' or '#'))
				{
					return false;
				}
			}

			return true;
		}

		if (before.EndsWith(":nth-of-type", StringComparison.Ordinal))
		{
			foreach (var c in argument)
			{
				if (!char.IsAsciiDigit(c) && c is not ('n' or '+' or '-'))
				{
					return false;
				}
			}

			return true;
		}

		return false;
	}

	// name, or name="value" with a value of names and spaces.
	private static bool IsAttribute(ReadOnlySpan<char> attribute)
	{
		var equals = attribute.IndexOf('=');
		if (equals < 0)
		{
			return CssText.IsIdentifier(attribute);
		}

		var value = attribute[(equals + 1)..];
		if (!CssText.IsIdentifier(attribute[..equals]) || value.Length < 2 || value[0] != '"' || value[^1] != '"')
		{
			return false;
		}

		foreach (var c in value[1..^1])
		{
			if (!CssText.IsNameChar(c) && c != ' ')
			{
				return false;
			}
		}

		return true;
	}

	public bool Equals(CssSelector other) => string.Equals(Text, other.Text, StringComparison.Ordinal);

	public override bool Equals(object? obj) => obj is CssSelector other && Equals(other);

	public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text ?? string.Empty);

	public override string ToString() => Text;
}
