using System.Collections.Frozen;
using System.Xml;
using System.Xml.Linq;
using SplashTheMusical.Rendering;

namespace SplashTheMusical;

/// <summary>An element, attribute or node the sanitizer removed.</summary>
/// <param name="Kind"><c>element</c>, <c>attribute</c>, <c>node</c>, <c>content</c> or <c>document</c>.</param>
public readonly record struct SvgViolation(string Kind, string Name, string? ParentElement = null)
{
	public override string ToString() => ParentElement is not null ? $"{Kind} '{Name}' on <{ParentElement}>" : $"{Kind} <{Name}>";
}

/// <summary>The result of <see cref="SvgSanitizer.Sanitize(string)"/>.</summary>
public sealed record SvgSanitizeResult
{
	/// <summary>The input when it had no violations, and the cleaned SVG otherwise.</summary>
	public required string Svg { get; init; }

	public required bool HasViolations { get; init; }

	public required IReadOnlyList<SvgViolation> Violations { get; init; }
}

/// <summary>What <see cref="SvgSanitizer"/> does with a violation.</summary>
public enum SanitizeMode
{
	/// <summary>Remove it and return the rest.</summary>
	Strip,

	/// <summary>Throw <see cref="MermaidSvgException"/>.</summary>
	Block,
}

/// <summary>
/// Sanitizes SVG from outside STM, such as user-supplied icons. It keeps only allowlisted elements and attributes, so
/// <c>script</c>, <c>foreignObject</c>, <c>on*</c> handlers, <c>style</c> and every <c>href</c> are removed because
/// they are not in the lists. The one <c>href</c> it keeps is a base64 PNG or clean SVG data URI on an <c>image</c>.
/// The XML is read with <see cref="DtdProcessing.Prohibit"/> and no resolver.
/// </summary>
public static class SvgSanitizer
{
	private const int MaxNestedImageDepth = 4;

	/// <summary>The elements the sanitizer keeps.</summary>
	public static FrozenSet<string> DefaultAllowedElements => SvgValues.Elements;

	/// <summary>The attributes without a namespace the sanitizer keeps, besides <c>data-*</c> and <c>aria-*</c>.</summary>
	public static FrozenSet<string> DefaultAllowedAttributes => SvgValues.Attributes;

	/// <summary>Removes what the default allowlists do not allow.</summary>
	public static SvgSanitizeResult Sanitize(string svg) => Sanitize(svg, SvgValues.Elements, SvgValues.Attributes, allowStyle: false, depth: 0);

	/// <summary>Removes what the default allowlists do not allow, or throws in <see cref="SanitizeMode.Block"/>.</summary>
	/// <exception cref="MermaidSvgException">The mode is <see cref="SanitizeMode.Block"/> and the SVG has a violation.</exception>
	public static SvgSanitizeResult Sanitize(string svg, SanitizeMode mode) => ApplyMode(Sanitize(svg), mode);

	/// <summary>
	/// Removes what <paramref name="allowedElements"/> and <paramref name="allowedAttributes"/> do not allow. They can only
	/// narrow the default lists. A name outside them is never kept.
	/// </summary>
	public static SvgSanitizeResult Sanitize(string svg, FrozenSet<string> allowedElements, FrozenSet<string> allowedAttributes) =>
		Sanitize(svg, allowedElements, allowedAttributes, allowStyle: false, depth: 0);

	/// <inheritdoc cref="Sanitize(string, FrozenSet{string}, FrozenSet{string})"/>
	/// <exception cref="MermaidSvgException">The mode is <see cref="SanitizeMode.Block"/> and the SVG has a violation.</exception>
	public static SvgSanitizeResult Sanitize(string svg, FrozenSet<string> allowedElements, FrozenSet<string> allowedAttributes, SanitizeMode mode) =>
		ApplyMode(Sanitize(svg, allowedElements, allowedAttributes), mode);

	/// <summary>
	/// Sanitizes STM's own output. Besides what <see cref="Sanitize(string)"/> keeps, it keeps one <c>style</c> child of
	/// the root whose text fits the grammar <see cref="SvgWriter.StyleText"/> writes for the root's id.
	/// </summary>
	internal static SvgSanitizeResult SanitizeRendererOutput(string svg) => Sanitize(svg, SvgValues.Elements, SvgValues.Attributes, allowStyle: true, depth: 0);

	private static SvgSanitizeResult ApplyMode(SvgSanitizeResult result, SanitizeMode mode)
	{
		if (mode is not (SanitizeMode.Strip or SanitizeMode.Block))
		{
			throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown SVG sanitization mode.");
		}

		return mode == SanitizeMode.Block && result.HasViolations ? throw new MermaidSvgException(result.Violations) : result;
	}

	private static SvgSanitizeResult Sanitize(string svg, FrozenSet<string> allowedElements, FrozenSet<string> allowedAttributes, bool allowStyle, int depth)
	{
		ArgumentNullException.ThrowIfNull(svg);
		ArgumentNullException.ThrowIfNull(allowedElements);
		ArgumentNullException.ThrowIfNull(allowedAttributes);

		var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
		XDocument doc;
		try
		{
			using var stringReader = new StringReader(svg);
			using var reader = XmlReader.Create(stringReader, settings);
			doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
		}
		catch (XmlException)
		{
			return Rejected(new SvgViolation("document", "malformed-xml"));
		}

		if (doc.Root is not { } root)
		{
			return new SvgSanitizeResult { Svg = svg, HasViolations = false, Violations = [] };
		}

		var rootNamespace = root.Name.NamespaceName;
		if (root.Name.LocalName != "svg" || !allowedElements.Contains("svg") || rootNamespace is not ("" or SvgValues.SvgNamespace)
			|| (rootNamespace == SvgValues.SvgNamespace && root.GetDefaultNamespace().NamespaceName != SvgValues.SvgNamespace))
		{
			return Rejected(new SvgViolation("element", root.Name.LocalName));
		}

		var violations = new List<SvgViolation>();
		if (doc.Declaration is not null)
		{
			violations.Add(new SvgViolation("node", "xml-declaration"));
			doc.Declaration = null;
		}

		foreach (var instruction in doc.DescendantNodes().OfType<XProcessingInstruction>().ToList())
		{
			violations.Add(new SvgViolation("node", instruction.Target));
			instruction.Remove();
		}

		var style = allowStyle ? root.Elements().FirstOrDefault(e => IsRendererStyle(e, root)) : null;
		var elementsToRemove = root.DescendantsAndSelf()
			.Where(e => e.Name.NamespaceName != rootNamespace
				|| (e.Name.LocalName == "style" ? !ReferenceEquals(e, style) : !allowedElements.Contains(e.Name.LocalName) || !SvgValues.Elements.Contains(e.Name.LocalName)))
			.ToList();
		var removed = elementsToRemove.ToHashSet();
		foreach (var element in elementsToRemove)
		{
			violations.Add(new SvgViolation("element", element.Name.LocalName));
			if (!element.Ancestors().Any(removed.Contains))
			{
				element.Remove();
			}
		}

		var attributesToRemove = root.DescendantsAndSelf()
			.SelectMany(e => e.Attributes().Select(a => (Element: e, Attribute: a)))
			.Where(pair => !IsAllowedAttribute(pair.Element, pair.Attribute, root, allowedAttributes, depth))
			.ToList();
		foreach (var (element, attribute) in attributesToRemove)
		{
			violations.Add(new SvgViolation("attribute", attribute.Name.LocalName, element.Name.LocalName));
			attribute.Remove();
		}

		// An image is an empty element, and title and desc hold only text, whatever their children would be alone.
		foreach (var element in root.DescendantsAndSelf().Where(e => e.Name.LocalName is "image" or "title" or "desc").ToList())
		{
			var image = element.Name.LocalName == "image";
			foreach (var child in element.Nodes().Where(n => image || n is not XText).ToList())
			{
				violations.Add(child is XElement childElement
					? new SvgViolation("element", childElement.Name.LocalName, element.Name.LocalName)
					: new SvgViolation("content", child.NodeType.ToString(), element.Name.LocalName));
				child.Remove();
			}
		}

		return violations.Count == 0
			? new SvgSanitizeResult { Svg = svg, HasViolations = false, Violations = [] }
			: new SvgSanitizeResult { Svg = doc.ToString(SaveOptions.DisableFormatting), HasViolations = true, Violations = violations };
	}

	private static SvgSanitizeResult Rejected(SvgViolation violation) =>
		new() { Svg = SvgValues.EmptySvg, HasViolations = true, Violations = [violation] };

	// There is no list of names to refuse: an attribute is kept only when a rule below allows it.
	private static bool IsAllowedAttribute(XElement element, XAttribute attribute, XElement root, FrozenSet<string> allowed, int depth)
	{
		var name = attribute.Name.LocalName;

		// A namespace declaration can change what an allowed local name means, so only the SVG default namespace and the
		// xlink namespace of an image href are kept, on the root.
		if (attribute.IsNamespaceDeclaration)
		{
			return ReferenceEquals(element, root)
				&& ((name == "xmlns" && attribute.Value == SvgValues.SvgNamespace) || (name == "xlink" && attribute.Value == SvgValues.XLinkNamespace));
		}

		if (name == "href")
		{
			return attribute.Name.NamespaceName is "" or SvgValues.XLinkNamespace && element.Name.LocalName == "image"
				&& SvgValues.DecodeImageDataUri(attribute.Value, svg => depth < MaxNestedImageDepth
					&& !Sanitize(svg, SvgValues.Elements, SvgValues.Attributes, allowStyle: false, depth + 1).HasViolations) is not null;
		}

		// Checked before data-* and aria-*, so a namespaced name cannot pass as one of them.
		if (attribute.Name.NamespaceName.Length > 0)
		{
			return false;
		}

		if (name.StartsWith("data-", StringComparison.Ordinal) || name.StartsWith("aria-", StringComparison.Ordinal))
		{
			return true;
		}

		return allowed.Contains(name) && SvgValues.Attributes.Contains(name) && SvgValues.IsAllowedValue(name, attribute.Value);
	}

	private static bool IsRendererStyle(XElement element, XElement root) =>
		element.Name.LocalName == "style" && !element.HasAttributes && element.Nodes().All(n => n is XText)
		&& root.Attribute("id") is { } id && StyleTextCheck.IsAllowed(element.Value, id.Value);
}

/// <summary>Thrown by <see cref="SanitizeMode.Block"/> when SVG has a violation or is not well-formed XML.</summary>
public sealed class MermaidSvgException : Exception
{
	public MermaidSvgException(IReadOnlyList<SvgViolation> violations)
		: base(CreateMessage(violations))
	{
		if (violations.Count == 0)
		{
			throw new ArgumentException("At least one SVG violation is required.", nameof(violations));
		}

		Violations = Array.AsReadOnly(violations.ToArray());
	}

	/// <summary>A copy of every violation the sanitizer found.</summary>
	public IReadOnlyList<SvgViolation> Violations { get; }

	private static string CreateMessage(IReadOnlyList<SvgViolation> violations)
	{
		ArgumentNullException.ThrowIfNull(violations);
		if (violations.Count == 0)
		{
			return "SVG sanitization was rejected.";
		}

		var first = violations[0];
		return first is { Kind: "document", Name: "malformed-xml" }
			? "SVG sanitization failed: input is not well-formed SVG/XML."
			: $"SVG sanitization failed: disallowed {first}.";
	}
}
