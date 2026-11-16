using System.Buffers;
using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>
/// Writes SVG as UTF-8 to an <see cref="IBufferWriter{T}"/>. Its stylesheet comes only from typed rules, so a value from
/// the diagram source or the config cannot add a rule, leave the SVG's scope or load a resource.
/// </summary>
internal sealed class SvgWriter
{
	private readonly IBufferWriter<byte> _output;

	public SvgWriter(IBufferWriter<byte> output)
	{
		ArgumentNullException.ThrowIfNull(output);
		_output = output;
	}

	/// <summary>
	/// A <c>style</c> element with <paramref name="rules"/> as stylis writes them in mermaid's <c>compileCSS</c>: each
	/// selector after <c>#<paramref name="svgId"/></c>, with no whitespace a browser does not need. A style rule left with
	/// no declarations is not written.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="svgId"/> is not a CSS identifier.</exception>
	public void WriteStyle(string svgId, IEnumerable<CssRule> rules)
	{
		ArgumentNullException.ThrowIfNull(rules);
		var text = StyleText(svgId, rules);
		Encoding.UTF8.GetBytes("<style>", _output);
		WriteText(text);
		Encoding.UTF8.GetBytes("</style>", _output);
	}

	/// <summary>The text of the <c>style</c> element <see cref="WriteStyle"/> writes, before XML escaping.</summary>
	public static string StyleText(string svgId, IEnumerable<CssRule> rules)
	{
		ArgumentNullException.ThrowIfNull(svgId);
		ArgumentNullException.ThrowIfNull(rules);
		if (!CssText.IsIdentifier(svgId))
		{
			throw new ArgumentException($"'{svgId}' is not a CSS identifier.", nameof(svgId));
		}

		var css = new StringBuilder();
		foreach (var rule in rules)
		{
			switch (rule)
			{
				case CssStyleRule style when style.Declarations.Count > 0:
					for (var i = 0; i < style.Selectors.Count; i++)
					{
						css.Append(i == 0 ? "#" : ",#").Append(svgId);
						if (!style.Selectors[i].IsRoot)
						{
							css.Append(' ').Append(style.Selectors[i].Text);
						}
					}

					AppendBlock(css, svgId, style.Declarations);
					break;
				case CssKeyframesRule keyframes:
					css.Append("@keyframes ").Append(keyframes.Name).Append('{');
					foreach (var (frame, declarations) in keyframes.Frames)
					{
						css.Append(frame);
						AppendBlock(css, svgId, declarations);
					}

					css.Append('}');
					break;
			}
		}

		return css.ToString();
	}

	private static void AppendBlock(StringBuilder css, string svgId, IReadOnlyList<CssDeclaration> declarations)
	{
		css.Append('{');
		foreach (var declaration in declarations)
		{
			css.Append(declaration.Property).Append(':').Append(declaration.Value);
			if (declaration.ReferenceSuffix is not null)
			{
				css.Append("url(#").Append(svgId).Append(declaration.ReferenceSuffix).Append(')').Append(declaration.After);
			}

			css.Append(declaration.Important ? "!important;" : ";");
		}

		css.Append('}');
	}

	// Text content: & < and > are escaped, as a browser serializes the text of an SVG element.
	private void WriteText(string text)
	{
		var start = 0;
		for (var i = 0; i < text.Length; i++)
		{
			var entity = text[i] switch
			{
				'&' => "&amp;",
				'<' => "&lt;",
				'>' => "&gt;",
				_ => null,
			};
			if (entity is null)
			{
				continue;
			}

			Encoding.UTF8.GetBytes(text.AsSpan(start, i - start), _output);
			Encoding.UTF8.GetBytes(entity, _output);
			start = i + 1;
		}

		Encoding.UTF8.GetBytes(text.AsSpan(start), _output);
	}
}
