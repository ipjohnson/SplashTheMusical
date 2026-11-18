using System.Text;

namespace SplashTheMusical.Rendering;

/// <summary>The kind of a <see cref="MarkupNode"/>.</summary>
internal enum MarkupKind
{
	Element,
	Text,
	CData,
	Comment,
	ProcessingInstruction,
}

/// <summary>An attribute of a <see cref="MarkupNode"/>, with its namespace resolved.</summary>
internal sealed class MarkupAttribute(string prefix, string localName, string namespaceName, string value, bool isNamespaceDeclaration)
{
	/// <summary>The prefix as written, or empty. A namespace declaration <c>xmlns:p</c> has prefix <c>xmlns</c>.</summary>
	public string Prefix { get; } = prefix;

	/// <summary>The local name. A default namespace declaration has the local name <c>xmlns</c>, as XLinq gives it.</summary>
	public string LocalName { get; } = localName;

	public string NamespaceName { get; } = namespaceName;

	public string Value { get; set; } = value;

	public bool IsNamespaceDeclaration { get; } = isNamespaceDeclaration;
}

/// <summary>An element, text, CDATA section, comment or processing instruction of a <see cref="MarkupDocument"/>.</summary>
internal sealed class MarkupNode
{
	private MarkupNode(MarkupKind kind, string localName, string namespaceName, string value)
	{
		Kind = kind;
		LocalName = localName;
		NamespaceName = namespaceName;
		Value = value;
	}

	public MarkupKind Kind { get; }

	/// <summary>The local name of an element, or the target of a processing instruction.</summary>
	public string LocalName { get; }

	public string NamespaceName { get; }

	/// <summary>The text of a text, CDATA or comment node.</summary>
	public string Value { get; }

	public MarkupNode? Parent { get; internal set; }

	public List<MarkupAttribute> Attributes { get; } = [];

	public List<MarkupNode> Children { get; } = [];

	/// <summary>Whether the node is text, which a CDATA section also is, as an <c>XCData</c> is an <c>XText</c>.</summary>
	public bool IsText => Kind is MarkupKind.Text or MarkupKind.CData;

	/// <summary>The name XLinq's <c>XNodeType</c> gives the node, for a violation.</summary>
	public string NodeTypeName => Kind switch
	{
		MarkupKind.Element => "Element",
		MarkupKind.Text => "Text",
		MarkupKind.CData => "CDATA",
		MarkupKind.Comment => "Comment",
		_ => "ProcessingInstruction",
	};

	public static MarkupNode Element(string localName, string namespaceName) => new(MarkupKind.Element, localName, namespaceName, "");

	public static MarkupNode Leaf(MarkupKind kind, string nameOrValue) =>
		kind == MarkupKind.ProcessingInstruction ? new(kind, nameOrValue, "", "") : new(kind, "", "", nameOrValue);

	public void Add(MarkupNode child)
	{
		child.Parent = this;
		Children.Add(child);
	}

	public void Remove()
	{
		if (Parent is { } parent)
		{
			parent.Children.Remove(this);
			Parent = null;
		}
	}

	/// <summary>The attribute with no namespace and <paramref name="localName"/>, or <c>null</c>.</summary>
	public MarkupAttribute? Attribute(string localName)
	{
		foreach (var attribute in Attributes)
		{
			if (!attribute.IsNamespaceDeclaration && attribute.NamespaceName.Length == 0 && attribute.LocalName == localName)
			{
				return attribute;
			}
		}

		return null;
	}

	/// <summary>Sets the value of the attribute with no namespace and <paramref name="localName"/>, adding it last when there is none.</summary>
	public void SetAttributeValue(string localName, string value)
	{
		if (Attribute(localName) is { } attribute)
		{
			attribute.Value = value;
		}
		else
		{
			Attributes.Add(new MarkupAttribute("", localName, "", value, isNamespaceDeclaration: false));
		}
	}

	/// <summary>The concatenated text of the node's descendants.</summary>
	public string TextValue()
	{
		var builder = new StringBuilder();
		foreach (var node in DescendantNodesAndSelf())
		{
			if (node.IsText)
			{
				builder.Append(node.Value);
			}
		}

		return builder.ToString();
	}

	/// <summary>The node and every node below it, in document order, without recursion.</summary>
	public List<MarkupNode> DescendantNodesAndSelf()
	{
		var list = new List<MarkupNode>();
		var stack = new Stack<MarkupNode>();
		stack.Push(this);
		while (stack.Count > 0)
		{
			var node = stack.Pop();
			list.Add(node);
			for (var i = node.Children.Count - 1; i >= 0; i--)
			{
				stack.Push(node.Children[i]);
			}
		}

		return list;
	}

	/// <summary>The element and every element below it, in document order.</summary>
	public List<MarkupNode> DescendantsAndSelf() => DescendantNodesAndSelf().FindAll(n => n.Kind == MarkupKind.Element);

	public bool HasAncestorIn(HashSet<MarkupNode> set)
	{
		for (var node = Parent; node is not null; node = node.Parent)
		{
			if (set.Contains(node))
			{
				return true;
			}
		}

		return false;
	}
}

/// <summary>A parsed document: its root element, and the comments and processing instructions around it.</summary>
internal sealed class MarkupDocument(bool hasDeclaration, List<MarkupNode> nodes, MarkupNode root)
{
	public bool HasDeclaration { get; set; } = hasDeclaration;

	/// <summary>The top-level nodes, which hold <see cref="Root"/>.</summary>
	public List<MarkupNode> Nodes { get; } = nodes;

	public MarkupNode Root { get; } = root;
}

/// <summary>
/// Reads and writes the XML of SVG documents and icon bodies in one pass over the source, without
/// <c>System.Xml</c>, which would add megabytes to the native AOT <c>stm</c>. It accepts a subset of the documents
/// <c>XmlReader</c> accepts with <c>DtdProcessing.Prohibit</c> and namespaces on: names are ASCII, a document type
/// declaration is refused, and so is anything not well-formed, an undeclared prefix or a duplicate attribute. Line
/// ends and attribute white space are normalized as XML 1.0 says. What it writes is well-formed, and reads back the same.
/// </summary>
internal static class SvgMarkup
{
	public const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

	public const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

	private sealed class MarkupException : Exception;

	private readonly record struct RawAttribute(string Prefix, string LocalName, string Value);

	/// <summary>
	/// The document <paramref name="source"/> holds, or <c>null</c> when it is not well-formed. With
	/// <paramref name="preserveWhitespace"/> off, text nodes of only white space are dropped, as <c>XDocument.Load</c> does.
	/// </summary>
	public static MarkupDocument? Parse(string source, bool preserveWhitespace)
	{
		ArgumentNullException.ThrowIfNull(source);
		try
		{
			return new Reader(source, preserveWhitespace).ReadDocument();
		}
		catch (MarkupException)
		{
			return null;
		}
	}

	private sealed class Reader(string source, bool preserveWhitespace)
	{
		private readonly string _s = source;
		private readonly bool _preserveWhitespace = preserveWhitespace;
		private readonly List<(string Prefix, string Uri)> _bindings = [("xml", XmlNamespace)];
		private int _i;

		public MarkupDocument ReadDocument()
		{
			CheckCharacters();
			var hasDeclaration = false;
			if (StartsWith("<?xml") && _s.Length > 5 && IsWhitespace(_s[5]))
			{
				ReadDeclaration();
				hasDeclaration = true;
			}

			var nodes = new List<MarkupNode>();
			MarkupNode? root = null;
			while (true)
			{
				SkipWhitespace();
				if (_i >= _s.Length)
				{
					break;
				}

				if (StartsWith("<!--"))
				{
					nodes.Add(ReadComment());
				}
				else if (StartsWith("<?"))
				{
					nodes.Add(ReadProcessingInstruction());
				}
				else if (root is null && _s[_i] == '<' && !StartsWith("<!") && !StartsWith("</"))
				{
					root = ReadElement();
					nodes.Add(root);
				}
				else
				{
					throw new MarkupException();
				}
			}

			return root is null ? throw new MarkupException() : new MarkupDocument(hasDeclaration, nodes, root);
		}

		// Every character is an XML Char: no C0 control but tab and line ends, no lone surrogate, no U+FFFE or U+FFFF.
		private void CheckCharacters()
		{
			for (var i = 0; i < _s.Length; i++)
			{
				var c = _s[i];
				if (char.IsHighSurrogate(c))
				{
					if (i + 1 >= _s.Length || !char.IsLowSurrogate(_s[i + 1]))
					{
						throw new MarkupException();
					}

					i++;
				}
				else if (!IsChar(c))
				{
					throw new MarkupException();
				}
			}
		}

		private static bool IsChar(char c) =>
			c is '\t' or '\n' or '\r' or (>= ' ' and <= '퟿') or (>= '' and <= '�');

		private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';

		private bool StartsWith(string text) => _s.AsSpan(_i).StartsWith(text, StringComparison.Ordinal);

		private bool SkipWhitespace()
		{
			var start = _i;
			while (_i < _s.Length && IsWhitespace(_s[_i]))
			{
				_i++;
			}

			return _i > start;
		}

		private void Expect(string text)
		{
			if (!StartsWith(text))
			{
				throw new MarkupException();
			}

			_i += text.Length;
		}

		// <?xml version="1.0" encoding="utf-8" standalone="yes"?>, each part in that order, and only at the start.
		private void ReadDeclaration()
		{
			_i = 5;
			SkipWhitespace();
			if (ReadPseudoAttribute("version") != "1.0")
			{
				throw new MarkupException();
			}

			var space = SkipWhitespace();
			if (space && StartsWith("encoding"))
			{
				var encoding = ReadPseudoAttribute("encoding");
				if (!(encoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase) || encoding.Equals("utf-16", StringComparison.OrdinalIgnoreCase)))
				{
					throw new MarkupException();
				}

				space = SkipWhitespace();
			}

			if (space && StartsWith("standalone"))
			{
				if (ReadPseudoAttribute("standalone") is not ("yes" or "no"))
				{
					throw new MarkupException();
				}

				SkipWhitespace();
			}

			Expect("?>");
		}

		private string ReadPseudoAttribute(string name)
		{
			Expect(name);
			SkipWhitespace();
			Expect("=");
			SkipWhitespace();
			if (_i >= _s.Length || _s[_i] is not ('"' or '\''))
			{
				throw new MarkupException();
			}

			var quote = _s[_i++];
			var end = _s.IndexOf(quote, _i);
			if (end < 0)
			{
				throw new MarkupException();
			}

			var value = _s[_i..end];
			_i = end + 1;
			return value;
		}

		private MarkupNode ReadComment()
		{
			_i += 4;
			var end = _s.IndexOf("--", _i, StringComparison.Ordinal);
			if (end < 0 || end + 2 >= _s.Length || _s[end + 2] != '>')
			{
				throw new MarkupException();
			}

			var node = MarkupNode.Leaf(MarkupKind.Comment, Normalize(_s.AsSpan(_i, end - _i)));
			_i = end + 3;
			return node;
		}

		private MarkupNode ReadProcessingInstruction()
		{
			_i += 2;
			var target = ReadName(allowColon: false);
			if (target.Equals("xml", StringComparison.OrdinalIgnoreCase))
			{
				throw new MarkupException();
			}

			if (!StartsWith("?>") && !SkipWhitespace())
			{
				throw new MarkupException();
			}

			var end = _s.IndexOf("?>", _i, StringComparison.Ordinal);
			if (end < 0)
			{
				throw new MarkupException();
			}

			_i = end + 2;
			return MarkupNode.Leaf(MarkupKind.ProcessingInstruction, target);
		}

		private static bool IsNameStart(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';

		private static bool IsNameChar(char c) => IsNameStart(c) || c is (>= '0' and <= '9') or '-' or '.';

		// An ASCII NCName, or with allowColon a QName: two NCNames joined by one colon.
		private string ReadName(bool allowColon)
		{
			var start = _i;
			ReadNcName();
			if (allowColon && _i < _s.Length && _s[_i] == ':')
			{
				_i++;
				ReadNcName();
			}

			return _s[start.._i];
		}

		private void ReadNcName()
		{
			if (_i >= _s.Length || !IsNameStart(_s[_i]))
			{
				throw new MarkupException();
			}

			while (_i < _s.Length && IsNameChar(_s[_i]))
			{
				_i++;
			}
		}

		private static (string Prefix, string Local) Split(string name)
		{
			var colon = name.IndexOf(':', StringComparison.Ordinal);
			return colon < 0 ? ("", name) : (name[..colon], name[(colon + 1)..]);
		}

		private string Lookup(string prefix)
		{
			for (var i = _bindings.Count - 1; i >= 0; i--)
			{
				if (_bindings[i].Prefix == prefix)
				{
					return _bindings[i].Uri;
				}
			}

			return prefix.Length == 0 ? "" : throw new MarkupException();
		}

		// Elements nest on an explicit stack, so a deep document cannot overflow the call stack.
		private MarkupNode ReadElement()
		{
			var open = new Stack<(MarkupNode Node, string Name, int Bindings)>();
			var (root, rootName, rootBindings, rootEmpty) = ReadStartTag();
			if (rootEmpty)
			{
				_bindings.RemoveRange(rootBindings, _bindings.Count - rootBindings);
				return root;
			}

			open.Push((root, rootName, rootBindings));
			while (open.Count > 0)
			{
				var (parent, parentName, parentBindings) = open.Peek();
				if (_i >= _s.Length)
				{
					throw new MarkupException();
				}

				if (StartsWith("</"))
				{
					_i += 2;
					if (ReadName(allowColon: true) != parentName)
					{
						throw new MarkupException();
					}

					SkipWhitespace();
					Expect(">");
					_bindings.RemoveRange(parentBindings, _bindings.Count - parentBindings);
					open.Pop();
				}
				else if (StartsWith("<!--"))
				{
					parent.Add(ReadComment());
				}
				else if (StartsWith("<![CDATA["))
				{
					_i += 9;
					var end = _s.IndexOf("]]>", _i, StringComparison.Ordinal);
					if (end < 0)
					{
						throw new MarkupException();
					}

					parent.Add(MarkupNode.Leaf(MarkupKind.CData, Normalize(_s.AsSpan(_i, end - _i))));
					_i = end + 3;
				}
				else if (StartsWith("<?"))
				{
					parent.Add(ReadProcessingInstruction());
				}
				else if (StartsWith("<!"))
				{
					throw new MarkupException();
				}
				else if (_s[_i] == '<')
				{
					var (child, name, bindings, empty) = ReadStartTag();
					parent.Add(child);
					if (empty)
					{
						_bindings.RemoveRange(bindings, _bindings.Count - bindings);
					}
					else
					{
						open.Push((child, name, bindings));
					}
				}
				else
				{
					var text = ReadText();
					if (_preserveWhitespace || !string.IsNullOrEmpty(text.Trim([' ', '\t', '\r', '\n'])))
					{
						parent.Add(MarkupNode.Leaf(MarkupKind.Text, text));
					}
				}
			}

			return root;
		}

		private (MarkupNode Node, string Name, int Bindings, bool Empty) ReadStartTag()
		{
			_i++;
			var name = ReadName(allowColon: true);
			var raw = new List<RawAttribute>();
			bool empty;
			while (true)
			{
				var space = SkipWhitespace();
				if (StartsWith("/>"))
				{
					_i += 2;
					empty = true;
					break;
				}

				if (StartsWith(">"))
				{
					_i++;
					empty = false;
					break;
				}

				if (!space)
				{
					throw new MarkupException();
				}

				var attributeName = ReadName(allowColon: true);
				SkipWhitespace();
				Expect("=");
				SkipWhitespace();
				var (prefix, local) = Split(attributeName);
				foreach (var other in raw)
				{
					if (other.Prefix == prefix && other.LocalName == local)
					{
						throw new MarkupException();
					}
				}

				raw.Add(new RawAttribute(prefix, local, ReadAttributeValue()));
			}

			var bindings = _bindings.Count;
			foreach (var attribute in raw)
			{
				if (attribute.Prefix.Length == 0 && attribute.LocalName == "xmlns")
				{
					if (attribute.Value is XmlNamespace or XmlnsNamespace)
					{
						throw new MarkupException();
					}

					_bindings.Add(("", attribute.Value));
				}
				else if (attribute.Prefix == "xmlns")
				{
					var valid = attribute.LocalName switch
					{
						"xmlns" => false,
						"xml" => attribute.Value == XmlNamespace,
						_ => attribute.Value.Length > 0 && attribute.Value is not (XmlNamespace or XmlnsNamespace),
					};
					if (!valid)
					{
						throw new MarkupException();
					}

					_bindings.Add((attribute.LocalName, attribute.Value));
				}
			}

			var (elementPrefix, elementLocal) = Split(name);
			if (elementPrefix == "xmlns")
			{
				throw new MarkupException();
			}

			var node = MarkupNode.Element(elementLocal, Lookup(elementPrefix));
			foreach (var attribute in raw)
			{
				MarkupAttribute resolved;
				if (attribute.Prefix.Length == 0 && attribute.LocalName == "xmlns")
				{
					resolved = new MarkupAttribute("", "xmlns", XmlnsNamespace, attribute.Value, isNamespaceDeclaration: true);
				}
				else if (attribute.Prefix == "xmlns")
				{
					resolved = new MarkupAttribute("xmlns", attribute.LocalName, XmlnsNamespace, attribute.Value, isNamespaceDeclaration: true);
				}
				else
				{
					var namespaceName = attribute.Prefix.Length == 0 ? "" : Lookup(attribute.Prefix);
					foreach (var other in node.Attributes)
					{
						if (!other.IsNamespaceDeclaration && other.NamespaceName == namespaceName && other.LocalName == attribute.LocalName)
						{
							throw new MarkupException();
						}
					}

					resolved = new MarkupAttribute(attribute.Prefix, attribute.LocalName, namespaceName, attribute.Value, isNamespaceDeclaration: false);
				}

				node.Attributes.Add(resolved);
			}

			return (node, name, bindings, empty);
		}

		private string ReadAttributeValue()
		{
			if (_i >= _s.Length || _s[_i] is not ('"' or '\''))
			{
				throw new MarkupException();
			}

			var quote = _s[_i++];
			var builder = new StringBuilder();
			while (true)
			{
				if (_i >= _s.Length)
				{
					throw new MarkupException();
				}

				var c = _s[_i];
				if (c == quote)
				{
					_i++;
					return builder.ToString();
				}

				switch (c)
				{
					case '<':
						throw new MarkupException();
					case '&':
						ReadReference(builder);
						break;
					case '\r':
						builder.Append(' ');
						_i += _i + 1 < _s.Length && _s[_i + 1] == '\n' ? 2 : 1;
						break;
					case '\t' or '\n':
						builder.Append(' ');
						_i++;
						break;
					default:
						builder.Append(c);
						_i++;
						break;
				}
			}
		}

		private string ReadText()
		{
			var builder = new StringBuilder();
			while (_i < _s.Length && _s[_i] != '<')
			{
				var c = _s[_i];
				if (c == '&')
				{
					ReadReference(builder);
				}
				else if (c == '\r')
				{
					builder.Append('\n');
					_i += _i + 1 < _s.Length && _s[_i + 1] == '\n' ? 2 : 1;
				}
				else if (c == '>' && _i >= 2 && _s[_i - 1] == ']' && _s[_i - 2] == ']')
				{
					throw new MarkupException();
				}
				else
				{
					builder.Append(c);
					_i++;
				}
			}

			return builder.ToString();
		}

		private void ReadReference(StringBuilder builder)
		{
			var end = _s.IndexOf(';', _i);
			if (end < 0)
			{
				throw new MarkupException();
			}

			var name = _s.AsSpan(_i + 1, end - _i - 1);
			_i = end + 1;
			switch (name)
			{
				case "lt":
					builder.Append('<');
					return;
				case "gt":
					builder.Append('>');
					return;
				case "amp":
					builder.Append('&');
					return;
				case "apos":
					builder.Append('\'');
					return;
				case "quot":
					builder.Append('"');
					return;
			}

			if (name.Length < 2 || name[0] != '#')
			{
				throw new MarkupException();
			}

			var hex = name[1] == 'x';
			var digits = hex ? name[2..] : name[1..];
			if (digits.Length is 0 or > 8)
			{
				throw new MarkupException();
			}

			var code = 0;
			foreach (var d in digits)
			{
				int value = d switch
				{
					>= '0' and <= '9' => d - '0',
					>= 'a' and <= 'f' when hex => d - 'a' + 10,
					>= 'A' and <= 'F' when hex => d - 'A' + 10,
					_ => throw new MarkupException(),
				};
				code = code * (hex ? 16 : 10) + value;
			}

			if (code > 0xFFFF)
			{
				if (code > 0x10FFFF)
				{
					throw new MarkupException();
				}

				builder.Append(char.ConvertFromUtf32(code));
			}
			else if (IsChar((char)code) && !char.IsSurrogate((char)code))
			{
				builder.Append((char)code);
			}
			else
			{
				throw new MarkupException();
			}
		}

		// Line ends of a comment or CDATA section, which hold no references.
		private static string Normalize(ReadOnlySpan<char> text)
		{
			if (!text.Contains('\r'))
			{
				return text.ToString();
			}

			var builder = new StringBuilder(text.Length);
			for (var i = 0; i < text.Length; i++)
			{
				if (text[i] == '\r')
				{
					builder.Append('\n');
					if (i + 1 < text.Length && text[i + 1] == '\n')
					{
						i++;
					}
				}
				else
				{
					builder.Append(text[i]);
				}
			}

			return builder.ToString();
		}
	}

	/// <summary>
	/// Writes <paramref name="document"/>'s comments and root, without its declaration or processing instructions.
	/// Elements are written by local name, in the root's default namespace, which is the only namespace an element the
	/// sanitizer keeps can have. An attribute in the xlink namespace is written as <c>xlink:href</c>, and the root
	/// declares <c>xlink</c> when one is.
	/// </summary>
	public static string Write(MarkupDocument document)
	{
		var builder = new StringBuilder();
		var usesXLink = document.Root.DescendantsAndSelf()
			.Exists(e => e.Attributes.Exists(a => !a.IsNamespaceDeclaration && a.NamespaceName == SvgValues.XLinkNamespace));
		var declaresXLink = document.Root.Attributes
			.Exists(a => a.IsNamespaceDeclaration && a.Prefix == "xmlns" && a.LocalName == "xlink" && a.Value == SvgValues.XLinkNamespace);
		foreach (var node in document.Nodes)
		{
			if (node.Kind == MarkupKind.Comment)
			{
				builder.Append("<!--").Append(node.Value).Append("-->");
			}
			else if (node.Kind == MarkupKind.Element)
			{
				WriteElement(builder, node, usesXLink && !declaresXLink);
			}
		}

		return builder.ToString();
	}

	private static void WriteElement(StringBuilder builder, MarkupNode root, bool declareXLink)
	{
		// Each entry is a node to start, or with End set, an element to close.
		var stack = new Stack<(MarkupNode Node, bool End)>();
		stack.Push((root, false));
		while (stack.Count > 0)
		{
			var (node, end) = stack.Pop();
			if (end)
			{
				builder.Append("</").Append(node.LocalName).Append('>');
				continue;
			}

			switch (node.Kind)
			{
				case MarkupKind.Text or MarkupKind.CData:
					AppendEscaped(builder, node.Value, attribute: false);
					continue;
				case MarkupKind.Comment:
					builder.Append("<!--").Append(node.Value).Append("-->");
					continue;
				case MarkupKind.ProcessingInstruction:
					continue;
			}

			builder.Append('<').Append(node.LocalName);
			foreach (var attribute in node.Attributes)
			{
				builder.Append(' ');
				if (attribute.IsNamespaceDeclaration)
				{
					builder.Append(attribute.Prefix.Length == 0 ? "xmlns" : "xmlns:" + attribute.LocalName);
				}
				else
				{
					builder.Append(attribute.NamespaceName == SvgValues.XLinkNamespace ? "xlink:" : "").Append(attribute.LocalName);
				}

				builder.Append("=\"");
				AppendEscaped(builder, attribute.Value, attribute: true);
				builder.Append('"');
			}

			if (ReferenceEquals(node, root) && declareXLink)
			{
				builder.Append(" xmlns:xlink=\"").Append(SvgValues.XLinkNamespace).Append('"');
			}

			if (node.Children.Count == 0)
			{
				builder.Append("/>");
				continue;
			}

			builder.Append('>');
			stack.Push((node, true));
			for (var i = node.Children.Count - 1; i >= 0; i--)
			{
				stack.Push((node.Children[i], false));
			}
		}
	}

	/// <summary>Appends <paramref name="value"/> escaped as text, or with <paramref name="attribute"/> as a double-quoted attribute value.</summary>
	public static void AppendEscaped(StringBuilder builder, string value, bool attribute)
	{
		foreach (var c in value)
		{
			switch (c)
			{
				case '&':
					builder.Append("&amp;");
					break;
				case '<':
					builder.Append("&lt;");
					break;
				case '>':
					builder.Append("&gt;");
					break;
				case '\r':
					builder.Append("&#xD;");
					break;
				case '"' when attribute:
					builder.Append("&quot;");
					break;
				case '\n' when attribute:
					builder.Append("&#xA;");
					break;
				case '\t' when attribute:
					builder.Append("&#x9;");
					break;
				default:
					builder.Append(c);
					break;
			}
		}
	}
}
