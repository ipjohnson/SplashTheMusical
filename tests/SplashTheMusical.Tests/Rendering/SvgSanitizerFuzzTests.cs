using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Tests.Rendering;

/// <summary>
/// Property tests of <see cref="SvgSanitizer.Sanitize(string)"/>, ported from ipjohnson/mermaider's
/// <c>SvgSanitizerFuzzTests</c>. One generator mutates seed documents, the other combines elements, attributes and
/// values. Every result must parse, hold only allowed names and values, and come back unchanged from a second pass.
/// </summary>
public class SvgSanitizerFuzzTests
{
	private static readonly HashSet<string> PaintAttributes = ["color", "fill", "stroke", "stop-color", "flood-color"];
	private static readonly HashSet<string> LocalReferenceAttributes = ["marker-start", "marker-mid", "marker-end"];

	private static readonly string[] Corpus =
	[
		"""<svg xmlns="http://www.w3.org/2000/svg"><rect width="10" height="10"/></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg"><foreignObject><body xmlns="http://www.w3.org/1999/xhtml">x</body></foreignObject></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg"><image href="javascript:alert(1)"/></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg"><rect fill="url(https://attacker.invalid/a.svg#x)"/></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg"><style>body{display:none}</style></svg>""",
		"""<?xml-stylesheet href="https://attacker.invalid/x.css"?><svg xmlns="http://www.w3.org/2000/svg"/>""",
		"""<html><svg><rect/></svg></html>""",
		"""<svg><title><text>nested</text></title><image><rect/></image></svg>""",
		"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="data:image/svg+xml;base64,PHN2ZyAvPg=="/></svg>""",
	];

	private static readonly string[] MutationTokens =
	[
		"<script>", "</script>", "<style>", "</style>", "<foreignObject>", " onclick=\"alert(1)\"", " onload=\"alert(1)\"", " href=\"javascript:alert(1)\"",
		" style=\"background:url(https://attacker.invalid/x)\"", "url(#local)", "url(https://attacker.invalid/x)", "data:image/svg+xml;base64,PHN2ZyAvPg==",
		"&lt;", "&amp;", "<![CDATA[", "]]>", "<!--", "-->", "<?xml-stylesheet?>", "xmlns:e=\"https://attacker.invalid/ns\"", "e:data-value=\"x\"", "\0",
		"\uD800",
	];

	// Positions are fractions of the current length, so an edit stays in range whatever the earlier edits did.
	private readonly record struct Mutation(int Kind, double At, double From, double Length, int Token);

	private static readonly Gen<Mutation> GenMutation = Gen.Select(
		Gen.Int[0, 4], Gen.Double[0, 1], Gen.Double[0, 1], Gen.Double[0, 1], Gen.Int[0, MutationTokens.Length - 1],
		(kind, at, from, length, token) => new Mutation(kind, at, from, length, token));

	[Test]
	public void Mutational_fuzzer_preserves_the_safety_invariant() =>
		Gen.Select(Gen.Int[0, Corpus.Length - 1], GenMutation.Array[1, 6], (seed, mutations) => Mutate(Corpus[seed], mutations))
			.Sample(input => AssertSafeAndIdempotent(input, SvgSanitizer.Sanitize(input)), iter: 2_500, print: input => input);

	private static readonly string[] Elements =
		["g", "rect", "path", "text", "title", "desc", "image", "style", "script", "foreignObject", "animate", "use", "a", "iframe", "object"];

	private static readonly string[] Attributes =
		["fill", "stroke", "marker-end", "style", "href", "xlink:href", "onclick", "onload", "data-value", "aria-label", "unknown"];

	private static readonly string[] Values =
	[
		"#fff", "red", "rgb(1, 2, 3)", "var(--_text)", "url(#local)", "url(https://attacker.invalid/x)", "javascript:alert(1)",
		"data:image/svg+xml;base64,PHN2ZyAvPg==", "data:image/png;base64,iVBORw0KGgo=", "\"/><script>alert(1)</script>",
		"u\\72l(https://attacker.invalid/x)", "red;position:fixed", "<>&\"'",
	];

	[Test]
	public void Structured_fuzzer_covers_element_attribute_and_value_cross_product() =>
		Gen.Select(
				Gen.OneOf(Elements), Gen.OneOf(Attributes), Gen.OneOf(Values), Gen.OneOf(MutationTokens),
				(element, attribute, value, text) =>
					$"<svg xmlns=\"{SvgValues.SvgNamespace}\" xmlns:xlink=\"{SvgValues.XLinkNamespace}\"><{element} {attribute}=\"{EscapeAttribute(value)}\">{EscapeText(text)}</{element}></svg>")
			.Sample(input => AssertSafeAndIdempotent(input, SvgSanitizer.Sanitize(input)), iter: 1_500, print: input => input);

	private static int Scale(double fraction, int count) => Math.Min((int)(fraction * count), count - 1);

	private static string Mutate(string seed, Mutation[] mutations)
	{
		var value = new StringBuilder(seed);
		foreach (var m in mutations)
		{
			var token = MutationTokens[m.Token];
			switch (m.Kind)
			{
				case 0:
					value.Insert(Scale(m.At, value.Length + 1), token);
					break;
				case 1 when value.Length > 0:
					{
						var start = Scale(m.From, value.Length);
						value.Remove(start, 1 + Scale(m.Length, Math.Min(24, value.Length - start)));
						break;
					}

				case 2 when value.Length > 0:
					value[Scale(m.At, value.Length)] = token[0];
					break;
				case 3 when value.Length > 0:
					{
						var start = Scale(m.From, value.Length);
						var length = Math.Min(1 + Scale(m.Length, 16), value.Length - start);
						value.Insert(Scale(m.At, value.Length + 1), value.ToString(start, length));
						break;
					}

				default:
					value.Append(token);
					break;
			}

			if (value.Length > 4_096)
			{
				value.Remove(4_096, value.Length - 4_096);
			}
		}

		return value.ToString();
	}

	private static void AssertSafeAndIdempotent(string input, SvgSanitizeResult result)
	{
		Verify(result.HasViolations == result.Violations.Count > 0);
		if (!result.HasViolations)
		{
			Verify(ReferenceEquals(result.Svg, input));
		}

		var secondPass = SvgSanitizer.Sanitize(result.Svg);
		Verify(!secondPass.HasViolations);
		Verify(ReferenceEquals(secondPass.Svg, result.Svg));

		var doc = XDocument.Parse(result.Svg);
		Verify(doc.Declaration is null);
		var root = doc.Root!;
		Verify(root.Name.LocalName == "svg");
		Verify(root.Name.NamespaceName == "" || root.Name.NamespaceName == SvgValues.SvgNamespace);
		Verify(root.DescendantsAndSelf().All(e => SvgSanitizer.DefaultAllowedElements.Contains(e.Name.LocalName)));
		Verify(!root.DescendantsAndSelf().Any(e => e.Name.LocalName == "style"));
		Verify(!root.DescendantNodes().Any(node => node is XProcessingInstruction));

		foreach (var element in root.DescendantsAndSelf())
		{
			if (element.Name.LocalName == "image")
			{
				Verify(!element.Nodes().Any());
			}

			if (element.Name.LocalName is "title" or "desc")
			{
				Verify(element.Nodes().All(node => node is XText));
			}

			foreach (var attribute in element.Attributes())
			{
				if (attribute.IsNamespaceDeclaration)
				{
					Verify(attribute.Value == SvgValues.SvgNamespace || attribute.Value == SvgValues.XLinkNamespace);
					continue;
				}

				if (attribute.Name.LocalName == "href")
				{
					Verify(element.Name.LocalName == "image");
					Verify(attribute.Value.StartsWith("data:image/svg+xml;base64,", StringComparison.Ordinal)
						|| attribute.Value.StartsWith("data:image/png;base64,", StringComparison.Ordinal));
					continue;
				}

				Verify(!attribute.Name.NamespaceName.Any());
				var name = attribute.Name.LocalName;
				Verify((SvgSanitizer.DefaultAllowedAttributes.Contains(name) || name.StartsWith("data-", StringComparison.Ordinal) || name.StartsWith("aria-", StringComparison.Ordinal)));
				if (PaintAttributes.Contains(name))
				{
					Verify(SvgValues.IsAllowedPaint(attribute.Value));
				}

				if (LocalReferenceAttributes.Contains(name))
				{
					Verify(SvgValues.IsAllowedLocalReference(attribute.Value));
				}
			}
		}
	}

	private static string EscapeAttribute(string value) =>
		value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal)
			.Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

	private static string EscapeText(string value) =>
		value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
}
