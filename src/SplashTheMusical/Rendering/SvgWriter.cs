using System.Buffers;
using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>
/// Writes SVG as UTF-8 to an <see cref="IBufferWriter{T}"/>. It writes only the elements of
/// <see cref="SvgValues.WriterElements"/> and the attributes of <see cref="SvgValues.WriterAttributes"/>, <c>data-*</c> and
/// <c>aria-*</c>, and escapes every text and attribute value, so everything it writes passes <see cref="SvgSanitizer"/>
/// unchanged. Its stylesheet comes only from typed rules, so a value from the diagram source or the config cannot add a
/// rule, leave the SVG's scope or load a resource. Numbers are written as JavaScript writes them, whatever the culture.
/// </summary>
/// <remarks>
/// The writer writes one <c>svg</c> document at a time. Once the root element ends it can start the next. Writing to a
/// buffer with room for the output allocates nothing, except for <see cref="WriteStyle"/>.
/// </remarks>
public sealed class SvgWriter
{
	private static readonly SearchValues<char> TextEscapes = SearchValues.Create(Escapes("&<>\r"));

	private static readonly SearchValues<char> AttributeEscapes = SearchValues.Create(Escapes("&<>\"\t\n\r"));

	private readonly IBufferWriter<byte> _output;
	private string[] _elements = new string[16];
	private string[] _attributes = new string[16];
	private int _depth;
	private int _attributeCount;
	private bool _startTagOpen;
	private string? _rootId;
	private bool _styleWritten;

	public SvgWriter(IBufferWriter<byte> output)
	{
		ArgumentNullException.ThrowIfNull(output);
		_output = output;
	}

	/// <summary>Whether every element the writer started has ended.</summary>
	public bool IsComplete => _depth == 0;

	/// <summary>
	/// Renders the document <paramref name="write"/> writes through a pooled buffer and returns it as a string.
	/// </summary>
	/// <exception cref="InvalidOperationException"><paramref name="write"/> left an element open.</exception>
	internal static string Write<TState>(TState state, Action<SvgWriter, TState> write)
	{
		ArgumentNullException.ThrowIfNull(write);
		using var buffer = new PooledBufferWriter();
		var writer = new SvgWriter(buffer);
		write(writer, state);
		if (!writer.IsComplete)
		{
			throw new InvalidOperationException("The document has an element that was not ended.");
		}

		return Encoding.UTF8.GetString(buffer.WrittenSpan);
	}

	/// <summary>
	/// Starts the element <paramref name="name"/>. The root is <c>svg</c>, and the writer gives it the SVG namespace.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="name"/> is not in the allowlist, or the root is not <c>svg</c>.</exception>
	/// <exception cref="InvalidOperationException">The parent is a <c>title</c> or <c>desc</c>, which hold only text.</exception>
	public void StartElement(string name)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (!SvgValues.WriterElements.Contains(name))
		{
			throw new ArgumentException($"<{name}> is not an element SvgWriter writes.", nameof(name));
		}

		if (_depth == 0 && name != "svg")
		{
			throw new ArgumentException($"The root element is <svg>, not <{name}>.", nameof(name));
		}

		if (_depth > 0 && _elements[_depth - 1] is "title" or "desc")
		{
			throw new InvalidOperationException($"<{_elements[_depth - 1]}> holds only text.");
		}

		CloseStartTag();
		_output.Write("<"u8);
		WriteAscii(name);
		if (_depth == 0)
		{
			_output.Write(" xmlns=\"http://www.w3.org/2000/svg\""u8);
			_rootId = null;
			_styleWritten = false;
		}

		if (_depth == _elements.Length)
		{
			Array.Resize(ref _elements, _depth * 2);
		}

		_elements[_depth++] = name;
		_startTagOpen = true;
		_attributeCount = 0;
	}

	/// <summary>Writes an attribute of the element just started.</summary>
	/// <exception cref="ArgumentException">
	/// <paramref name="name"/> is not in the allowlist or was already written, or <paramref name="value"/> does not fit
	/// the attribute's grammar, such as a <c>fill</c> that is not a colour or a reference to the same document.
	/// </exception>
	/// <exception cref="InvalidOperationException">The element's start tag is already closed.</exception>
	public void WriteAttribute(string name, string value)
	{
		ArgumentNullException.ThrowIfNull(value);
		if (WriteAttributeCore(name, value))
		{
			_rootId = value;
		}
	}

	/// <inheritdoc cref="WriteAttribute(string, string)"/>
	public void WriteAttribute(string name, ReadOnlySpan<char> value)
	{
		if (WriteAttributeCore(name, value))
		{
			_rootId = value.ToString();
		}
	}

	/// <summary>Writes a number attribute as JavaScript's <c>String</c> writes the number.</summary>
	/// <inheritdoc cref="WriteAttribute(string, string)"/>
	public void WriteAttribute(string name, double value)
	{
		Span<char> text = stackalloc char[JsNumber.MaxLength];
		WriteAttribute(name, (ReadOnlySpan<char>)text[..JsNumber.Format(value, text)]);
	}

	/// <summary>Writes text inside the current element.</summary>
	/// <exception cref="InvalidOperationException">No element is open.</exception>
	public void WriteText(ReadOnlySpan<char> text)
	{
		if (_depth == 0)
		{
			throw new InvalidOperationException("Text must be inside an element.");
		}

		CloseStartTag();
		WriteEscaped(text, TextEscapes);
	}

	/// <summary>Ends the current element, as an empty-element tag when it has no content.</summary>
	/// <exception cref="InvalidOperationException">No element is open.</exception>
	public void EndElement()
	{
		if (_depth == 0)
		{
			throw new InvalidOperationException("No element is open.");
		}

		var name = _elements[--_depth];
		if (_startTagOpen)
		{
			_output.Write("/>"u8);
			_startTagOpen = false;
		}
		else
		{
			_output.Write("</"u8);
			WriteAscii(name);
			_output.Write(">"u8);
		}
	}

	/// <summary>
	/// A <c>style</c> element with <paramref name="rules"/>, scoped to the root's id as <see cref="StyleText"/> writes them.
	/// It is a child of the root, and a document has at most one.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The current element is not the root, the root has no id, or the document already has a stylesheet.
	/// </exception>
	/// <exception cref="ArgumentException">The root's id is not a CSS identifier.</exception>
	internal void WriteStyle(IEnumerable<CssRule> rules)
	{
		ArgumentNullException.ThrowIfNull(rules);
		if (_depth != 1)
		{
			throw new InvalidOperationException("The style element is a child of the root.");
		}

		if (_rootId is null)
		{
			throw new InvalidOperationException("The root has no id to scope the stylesheet with.");
		}

		if (_styleWritten)
		{
			throw new InvalidOperationException("The document already has a style element.");
		}

		var text = StyleText(_rootId, rules);
		CloseStartTag();
		_output.Write("<style>"u8);
		WriteEscaped(text, TextEscapes);
		_output.Write("</style>"u8);
		_styleWritten = true;
	}

	/// <summary>
	/// The text of the <c>style</c> element <see cref="WriteStyle"/> writes, before XML escaping: <paramref name="rules"/>
	/// as stylis writes them in mermaid's <c>compileCSS</c>, each selector after <c>#<paramref name="svgId"/></c>, with no
	/// whitespace a browser does not need. A style rule left with no declarations is not written.
	/// </summary>
	/// <exception cref="ArgumentException"><paramref name="svgId"/> is not a CSS identifier.</exception>
	internal static string StyleText(string svgId, IEnumerable<CssRule> rules)
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

	// Whether the attribute is the root's id, which WriteStyle scopes the stylesheet with.
	private bool WriteAttributeCore(string name, ReadOnlySpan<char> value)
	{
		ArgumentNullException.ThrowIfNull(name);
		if (!_startTagOpen)
		{
			throw new InvalidOperationException($"The attribute '{name}' does not follow a start tag.");
		}

		if (!SvgValues.WriterAttributes.Contains(name) && !SvgValues.IsDataOrAriaName(name))
		{
			throw new ArgumentException($"'{name}' is not an attribute SvgWriter writes.", nameof(name));
		}

		for (var i = 0; i < _attributeCount; i++)
		{
			if (string.Equals(_attributes[i], name, StringComparison.Ordinal))
			{
				throw new ArgumentException($"<{_elements[_depth - 1]}> already has the attribute '{name}'.", nameof(name));
			}
		}

		if (!SvgValues.IsAllowedValue(name, value))
		{
			throw new ArgumentException($"The value of '{name}' is not one SvgWriter writes.", nameof(value));
		}

		if (_attributeCount == _attributes.Length)
		{
			Array.Resize(ref _attributes, _attributeCount * 2);
		}

		_attributes[_attributeCount++] = name;
		_output.Write(" "u8);
		WriteAscii(name);
		_output.Write("=\""u8);
		WriteEscaped(value, AttributeEscapes);
		_output.Write("\""u8);
		return _depth == 1 && name == "id";
	}

	private void CloseStartTag()
	{
		if (_startTagOpen)
		{
			_output.Write(">"u8);
			_startTagOpen = false;
		}
	}

	// Names come from the allowlists, so every character is ASCII.
	private void WriteAscii(string text)
	{
		var span = _output.GetSpan(text.Length);
		for (var i = 0; i < text.Length; i++)
		{
			span[i] = (byte)text[i];
		}

		_output.Advance(text.Length);
	}

	private void WriteEscaped(ReadOnlySpan<char> text, SearchValues<char> escapes)
	{
		while (true)
		{
			var at = text.IndexOfAny(escapes);
			WriteUtf8(at < 0 ? text : text[..at]);
			if (at < 0)
			{
				return;
			}

			_output.Write(Entity(text[at]));
			text = text[(at + 1)..];
		}
	}

	// A lone surrogate becomes U+FFFD, the replacement fallback of Encoding.UTF8.
	private void WriteUtf8(ReadOnlySpan<char> text)
	{
		if (!text.IsEmpty)
		{
			var span = _output.GetSpan(Encoding.UTF8.GetMaxByteCount(text.Length));
			_output.Advance(Encoding.UTF8.GetBytes(text, span));
		}
	}

	// Tab, line feed and carriage return are written as references so an attribute keeps them. XML 1.0 has no way to
	// write the other C0 controls, U+FFFE or U+FFFF, so they become U+FFFD.
	private static ReadOnlySpan<byte> Entity(char c) => c switch
	{
		'&' => "&amp;"u8,
		'<' => "&lt;"u8,
		'>' => "&gt;"u8,
		'"' => "&quot;"u8,
		'\t' => "&#9;"u8,
		'\n' => "&#10;"u8,
		'\r' => "&#13;"u8,
		_ => "�"u8,
	};

	private static string Escapes(string markup)
	{
		var builder = new StringBuilder(markup);
		for (var c = '\0'; c < ' '; c++)
		{
			if (c is not ('\t' or '\n' or '\r'))
			{
				builder.Append(c);
			}
		}

		return builder.Append('￾').Append('￿').ToString();
	}
}
