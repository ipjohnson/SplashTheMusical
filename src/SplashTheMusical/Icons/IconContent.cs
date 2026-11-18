using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Icons;

/// <summary>An attribute of a sanitized icon. An <c>id</c>, or a <c>url(#id)</c> reference, holds the bare id, which the renderer prefixes.</summary>
internal readonly record struct IconAttribute(string Name, string Value, bool IsIdReference);

/// <summary>An element of a sanitized icon, or a text node when <see cref="Name"/> is <c>null</c>.</summary>
internal sealed record IconElement(string? Name, ImmutableArray<IconAttribute> Attributes, ImmutableArray<IconElement> Children, string? Text);

/// <summary>
/// Sanitizes an icon's body once, when the icon is made. Each <c>style</c> declaration of a presentation attribute the
/// writer allows becomes that attribute, as the browser would apply it over the attribute. The result then goes through
/// <see cref="SvgSanitizer"/> with <see cref="SvgValues.WriterElements"/> and <see cref="SvgValues.WriterAttributes"/>, so
/// <c>script</c>, <c>foreignObject</c>, handlers, links and anything else the writer refuses are removed, and the renderer
/// writes what is left through <see cref="SvgWriter"/>.
/// </summary>
internal static class IconContent
{
	/// <summary>The longest body an icon may have, in characters.</summary>
	public const int MaxBodyLength = 1 << 20;

	private static readonly FrozenSet<string> StyleProperties = FrozenSet.Create(
		StringComparer.Ordinal,
		"fill", "stroke", "stroke-width", "stroke-dasharray", "stroke-dashoffset", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
		"stroke-opacity", "fill-opacity", "fill-rule", "clip-rule", "opacity", "font-family", "font-size", "font-weight", "font-style",
		"text-anchor", "dominant-baseline", "letter-spacing", "color", "stop-color", "stop-opacity", "display", "visibility");

	private static readonly FrozenSet<string> ReferenceAttributes = FrozenSet.Create(StringComparer.Ordinal, "fill", "stroke", "marker-start", "marker-mid", "marker-end");

	public static (ImmutableArray<IconElement> Content, string Body, IReadOnlyList<SvgViolation> Removed) Sanitize(string body)
	{
		ArgumentNullException.ThrowIfNull(body);
		if (body.Length > MaxBodyLength)
		{
			return ([], "", [new SvgViolation("document", "too-long")]);
		}

		var removed = new List<SvgViolation>();
		var svg = "<svg xmlns=\"" + SvgValues.SvgNamespace + "\">" + body + "</svg>";
		var document = Load(svg);
		if (document?.Root is not { } root)
		{
			return ([], "", [new SvgViolation("document", "malformed-xml")]);
		}

		foreach (var element in root.Descendants())
		{
			if (element.Attribute("style") is { } style)
			{
				ApplyStyle(element, style.Value);
			}
		}

		var result = SvgSanitizer.Sanitize(document.ToString(SaveOptions.DisableFormatting), SvgValues.WriterElements, SvgValues.WriterAttributes);
		foreach (var violation in result.Violations)
		{
			// The style attributes were applied above, so removing them loses nothing the icon draws.
			if (!(violation.Kind == "attribute" && violation.Name == "style"))
			{
				removed.Add(violation);
			}
		}

		var clean = result.HasViolations ? Load(result.Svg)?.Root : root;
		if (clean is null)
		{
			return ([], "", removed);
		}

		var content = Convert(clean.Nodes());
		return (content, Write(content), removed);
	}

	private static XDocument? Load(string svg)
	{
		var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
		try
		{
			using var stringReader = new StringReader(svg);
			using var reader = XmlReader.Create(stringReader, settings);
			return XDocument.Load(reader);
		}
		catch (XmlException)
		{
			return null;
		}
	}

	/// <summary>The declarations of <paramref name="style"/> whose property and value are safe, set as attributes.</summary>
	private static void ApplyStyle(XElement element, string style)
	{
		foreach (var declaration in style.Split(';'))
		{
			var colon = declaration.IndexOf(':', StringComparison.Ordinal);
			if (colon <= 0)
			{
				continue;
			}

			var name = declaration[..colon].Trim().ToLowerInvariant();
			var value = declaration[(colon + 1)..].Trim();
			if (value.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
			{
				value = value[..^"!important".Length].TrimEnd();
			}

			if (StyleProperties.Contains(name) && IsSafeValue(value) && SvgValues.IsAllowedValue(name, value))
			{
				element.SetAttributeValue(name, value);
			}
		}
	}

	/// <summary>A value with no brace, backslash, angle bracket or function other than a local <c>url(#id)</c>.</summary>
	private static bool IsSafeValue(string value) =>
		value.Length is > 0 and <= 256 && value.IndexOfAny(['{', '}', '\\', '<', '>', '@']) < 0
		&& (!value.Contains('(', StringComparison.Ordinal) || SvgValues.IsAllowedPaint(value));

	private static ImmutableArray<IconElement> Convert(IEnumerable<XNode> nodes)
	{
		var list = ImmutableArray.CreateBuilder<IconElement>();
		foreach (var node in nodes)
		{
			switch (node)
			{
				case XElement element:
					list.Add(Convert(element));
					break;
				case XText text when text.Value.Length > 0:
					list.Add(new IconElement(null, [], [], text.Value));
					break;
			}
		}

		return list.ToImmutable();
	}

	private static IconElement Convert(XElement element)
	{
		var attributes = ImmutableArray.CreateBuilder<IconAttribute>();
		foreach (var attribute in element.Attributes())
		{
			if (attribute.IsNamespaceDeclaration || attribute.Name.NamespaceName.Length > 0)
			{
				continue;
			}

			var name = attribute.Name.LocalName;
			var value = attribute.Value;
			if (name == "id")
			{
				attributes.Add(new IconAttribute(name, value, IsIdReference: true));
			}
			else if (ReferenceAttributes.Contains(name) && value.StartsWith("url(#", StringComparison.Ordinal))
			{
				attributes.Add(new IconAttribute(name, value[5..^1], IsIdReference: true));
			}
			else
			{
				attributes.Add(new IconAttribute(name, value, IsIdReference: false));
			}
		}

		return new IconElement(element.Name.LocalName, attributes.ToImmutable(), Convert(element.Nodes()), null);
	}

	/// <summary>The content as markup, with its ids as the icon holds them.</summary>
	private static string Write(ImmutableArray<IconElement> content)
	{
		var builder = new StringBuilder();
		foreach (var element in content)
		{
			Write(builder, element);
		}

		return builder.ToString();
	}

	private static void Write(StringBuilder builder, IconElement element)
	{
		if (element.Name is null)
		{
			builder.Append(new XText(element.Text ?? "").ToString());
			return;
		}

		builder.Append('<').Append(element.Name);
		foreach (var attribute in element.Attributes)
		{
			var value = attribute.IsIdReference && attribute.Name != "id" ? "url(#" + attribute.Value + ")" : attribute.Value;
			builder.Append(' ').Append(new XAttribute(attribute.Name, value).ToString());
		}

		if (element.Children.IsEmpty)
		{
			builder.Append("/>");
			return;
		}

		builder.Append('>');
		foreach (var child in element.Children)
		{
			Write(builder, child);
		}

		builder.Append("</").Append(element.Name).Append('>');
	}
}
