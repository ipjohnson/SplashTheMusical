using System.Collections.Frozen;
using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;

namespace SplashTheMusical.Tests.Rendering;

// Ported from ipjohnson/mermaider's tests/Mermaider.Tests/Rendering/SvgSanitizerTests.cs. Its tests of the render
// pipeline, RenderOptions and mermaider's own stylesheet have no counterpart here.
public class SvgSanitizerTests
{
	private const string Empty = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";

	[Test]
	public void Strips_script_element()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script><rect x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<script", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("alert", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
		Verify(result.Violations.Any(v => v.Kind == "element" && v.Name == "script"));
	}

	[Test]
	public void Strips_foreignObject_element()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><foreignObject><body xmlns="http://www.w3.org/1999/xhtml"><div>hack</div></body></foreignObject><rect x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("foreignObject", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("hack", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_onclick_attribute()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10" onclick="alert(1)"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("onclick", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("alert", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
		Verify(result.Violations.Any(v => v.Kind == "attribute" && v.Name == "onclick"));
	}

	[Test]
	public void Strips_onload_attribute()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10" onload="fetch('http://evil.com')"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("onload", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_href_attribute()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><use xlink:href="javascript:alert(1)"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("javascript", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_unsafe_href_from_image_element_but_keeps_the_element()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><image href="http://evil.com/tracker.png"/><rect x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(result.Svg.Contains("<image", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("evil.com", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("href", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_animate_element()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10"><animate attributeName="x" to="100"/></rect></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<animate", StringComparison.Ordinal));
	}

	[Test]
	public void Preserves_allowed_elements_and_attributes()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><g class="node" data-id="A"><rect x="0" y="0" width="50" height="30" rx="6" fill="#fff" stroke="#000" stroke-width="1.5"/><text x="25" y="15" text-anchor="middle" font-size="14">Hello</text></g></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(ReferenceEquals(result.Svg, svg));
	}

	[Test]
	public void Returns_original_string_when_clean()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(ReferenceEquals(result.Svg, svg));
	}

	[Test]
	public void Preserves_data_attributes()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><g data-id="A" data-label="hello" data-shape="rectangle"><rect x="0" y="0" width="10" height="10"/></g></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(result.Svg.Contains("data-id", StringComparison.Ordinal));
		Verify(result.Svg.Contains("data-label", StringComparison.Ordinal));
	}

	[Test]
	public void Preserves_marker_elements()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><defs><marker id="arrow" markerWidth="12" markerHeight="12" refX="12" refY="6" orient="auto" markerUnits="userSpaceOnUse"><polygon points="0 0, 12 6, 0 12" fill="#000"/></marker></defs></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(result.Svg.Contains("<marker", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_style_element_from_standalone_untrusted_svg()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><style>text { font-family: Inter; }</style><rect x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<style", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
	}

	[Test]
	public void Violations_list_contains_all_issues()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>x</script><rect onclick="y" onload="z" x="0" y="0" width="10" height="10"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.Violations.Count >= 3);
	}

	[Test]
	public void Custom_allowlists_restrict_further()
	{
		var onlyRect = FrozenSet.Create(StringComparer.Ordinal, "svg", "rect");
		var onlyBasic = FrozenSet.Create(StringComparer.Ordinal, "xmlns", "x", "y", "width", "height");

		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10" fill="#f00"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg, onlyRect, onlyBasic);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("fill", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
		Verify(result.Violations.Any(v => v.Kind == "attribute" && v.Name == "fill"));
	}

	[Test]
	public void Custom_allowlists_strip_disallowed_elements()
	{
		var noText = FrozenSet.Create(StringComparer.Ordinal, "svg", "rect");

		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10"/><text x="5" y="5">no</text></svg>""";
		var result = SvgSanitizer.Sanitize(svg, noText, SvgSanitizer.DefaultAllowedAttributes);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<text", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
	}

	[Test]
	public void Custom_allowlists_cannot_expand_the_default_safety_ceiling()
	{
		var expandedElements = SvgSanitizer.DefaultAllowedElements.Append("script").ToFrozenSet(StringComparer.Ordinal);
		var expandedAttributes = SvgSanitizer.DefaultAllowedAttributes.Append("onclick").ToFrozenSet(StringComparer.Ordinal);
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script><rect width="10" height="10" onclick="alert(2)"/></svg>""";

		var result = SvgSanitizer.Sanitize(svg, expandedElements, expandedAttributes);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<script", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("onclick", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("alert", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<rect", StringComparison.Ordinal));
	}

	[Test]
	public void Block_throws_on_script_element()
	{
		Action act = () => SvgSanitizer.Sanitize("""<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""", SanitizeMode.Block);

		var failure = ThrowsExactly<MermaidSvgException>(act);
		Verify(failure.Message.Contains("disallowed", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("script", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void Block_throws_on_event_handler()
	{
		Action act = () => SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><rect x="0" y="0" width="10" height="10" onmouseover="alert(1)"/></svg>""",
			SanitizeMode.Block);

		var failure = ThrowsExactly<MermaidSvgException>(act);
		Verify(failure.Message.Contains("disallowed", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("onmouseover", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void Block_throws_on_foreignObject()
	{
		Action act = () => SvgSanitizer.Sanitize("""<svg xmlns="http://www.w3.org/2000/svg"><foreignObject width="100" height="100"/></svg>""", SanitizeMode.Block);

		var failure = ThrowsExactly<MermaidSvgException>(act);
		Verify(failure.Message.Contains("disallowed", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("foreignObject", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void Block_returns_original_when_clean()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><g><rect x="0" y="0" width="10" height="10" fill="#fff"/><text x="5" y="5">ok</text></g></svg>""";

		Verify(ReferenceEquals(SvgSanitizer.Sanitize(svg, SanitizeMode.Block).Svg, svg));
	}

	[Test]
	public void Strip_returns_cleaned_svg_when_the_input_is_rejected()
	{
		var result = SvgSanitizer.Sanitize("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>attack</script></svg>", SanitizeMode.Strip).Svg;

		Verify(!XDocument.Parse(result).Root!.Elements().Any());
	}

	[Test]
	public void Strip_preserves_safe_siblings_while_removing_a_violation()
	{
		var result = SvgSanitizer.Sanitize("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>attack</script><text>Safe</text></svg>", SanitizeMode.Strip).Svg;

		Verify(!result.Contains("<script", StringComparison.Ordinal));
		Verify(!result.Contains("attack", StringComparison.Ordinal));
		Verify(result.Contains("Safe", StringComparison.Ordinal));
	}

	[Test]
	public void The_empty_document_is_clean() =>
		Verify(!SvgSanitizer.Sanitize(Empty).HasViolations);

	[Test]
	public void Allows_image_element_with_safe_base64_svg_data_uri()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><image x="0" y="0" width="10" height="10" href="data:image/svg+xml;base64,PHN2ZyAvPg=="/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(result.Svg.Contains("<image", StringComparison.Ordinal));
		Verify(result.Svg.Contains("data:image/svg+xml;base64,", StringComparison.Ordinal));
	}

	[Test]
	public void Allows_image_element_with_safe_base64_png_data_uri()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"><image x="0" y="0" width="10" height="10" href="data:image/png;base64,iVBORw0KGgo="/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(result.Svg.Contains("data:image/png;base64,", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_http_href_on_image_element()
	{
		var result = SvgSanitizer.Sanitize("""<svg xmlns="http://www.w3.org/2000/svg"><image x="0" y="0" width="10" height="10" href="http://evil.example/x.png"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("evil.example", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_javascript_href_on_image_element()
	{
		var result = SvgSanitizer.Sanitize("""<svg xmlns="http://www.w3.org/2000/svg"><image x="0" y="0" width="10" height="10" href="javascript:alert(1)"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("javascript:", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_non_image_data_uri_on_image_element()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><image x="0" y="0" width="10" height="10" href="data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg=="/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("data:text/html", StringComparison.Ordinal));
	}

	[Test]
	[Arguments("data:image/png;base64,")]
	[Arguments("data:image/png;base64,iVBORw0KGgo")]
	[Arguments("data:image/png;base64,iVBO=w0KGgo=")]
	[Arguments("data:image/png;base64,iVBORw0KGgo===")]
	[Arguments("data:image/png;base64,iVBORw0K Ggo=")]
	[Arguments("DATA:image/png;base64,iVBORw0KGgo=")]
	public void Strips_an_image_data_uri_that_is_not_canonical_base64(string href)
	{
		var result = SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"{href}\"/></svg>");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("href=", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_children_of_image_element_even_when_individually_allowlisted()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><image href="data:image/svg+xml;base64,PHN2ZyAvPg=="><rect x="0" y="0" width="4" height="4"/></image></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(result.HasViolations);
		Verify(result.Svg.Contains("<image", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("<rect", StringComparison.Ordinal));
		Verify(result.Violations.Any(v => v.Kind == "element" && v.Name == "rect" && v.ParentElement == "image"));
	}

	[Test]
	public void Strips_href_on_non_image_element_even_if_it_looks_like_a_safe_data_uri()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><rect xlink:href="data:image/svg+xml;base64,PHN2ZyAvPg==" x="0" y="0" width="10" height="10"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("xlink:href", StringComparison.Ordinal));
	}

	[Test]
	public void Rejects_non_svg_root_without_throwing()
	{
		var result = SvgSanitizer.Sanitize("<html><svg/></html>");

		Verify(result.HasViolations);
		Verify(result.Svg == Empty);
		Verify(result.Violations.Any(v => v.Kind == "element" && v.Name == "html"));
	}

	[Test]
	public void Rejects_prefixed_svg_root_that_would_not_enter_svg_mode_in_html()
	{
		var result = SvgSanitizer.Sanitize("""<s:svg xmlns:s="http://www.w3.org/2000/svg"><s:rect/></s:svg>""");

		Verify(result.HasViolations);
		Verify(result.Svg == Empty);
	}

	[Test]
	public void Strips_allowlisted_local_name_from_a_foreign_namespace()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:e="https://attacker.invalid/ns"><e:rect width="10" height="10"/><circle cx="5" cy="5" r="5"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("e:rect", StringComparison.Ordinal));
		Verify(result.Svg.Contains("<circle", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_unapproved_namespace_declarations_and_namespaced_data_attributes()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:e="https://attacker.invalid/ns"><rect e:data-value="x" width="10" height="10"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("attacker.invalid", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("data-value", StringComparison.Ordinal));
	}

	[Test]
	public void Allows_exact_xlink_namespace_for_a_validated_image_data_uri()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"><image xlink:href="data:image/svg+xml;base64,PHN2ZyAvPg=="/></svg>""");

		Verify(!result.HasViolations);
		Verify(result.Svg.Contains("xlink:href", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_external_paint_server_urls()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><rect fill="url(https://attacker.invalid/paint.svg#x)" stroke="url(data:image/svg+xml;base64,AAAA)"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("attacker.invalid", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("data:image", StringComparison.Ordinal));
	}

	[Test]
	public void Allows_only_same_document_fragment_paint_urls()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><defs><linearGradient id="safe-gradient"><stop offset="0" stop-color="#fff"/></linearGradient></defs><rect fill="url(#safe-gradient)"/></svg>""";
		var result = SvgSanitizer.Sanitize(svg);

		Verify(!result.HasViolations);
		Verify(ReferenceEquals(result.Svg, svg));
	}

	[Test]
	[Arguments("#fff")]
	[Arguments("#ffff")]
	[Arguments("#ffffff")]
	[Arguments("#ffffff80")]
	[Arguments("rebeccapurple")]
	[Arguments(" red ")]
	[Arguments("rgb(1, 2, 3)")]
	[Arguments("HSLA(1 2% 3% / 50%)")]
	[Arguments("none")]
	public void Keeps_a_paint_of_the_colour_grammar(string paint) =>
		Verify(!(SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"{paint}\"/></svg>").HasViolations));

	[Test]
	[Arguments("#ff")]
	[Arguments("#fffff")]
	[Arguments("#ggg")]
	[Arguments("red;position:fixed")]
	[Arguments("u\\72l(https://attacker.invalid/x)")]
	[Arguments("expression(alert(1))")]
	[Arguments("rgb()")]
	[Arguments("rgb(1, 2, (3))")]
	[Arguments("url(#1a)")]
	[Arguments("url(#a b)")]
	[Arguments("")]
	public void Strips_a_paint_outside_the_colour_grammar(string paint) =>
		Verify(SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"{paint}\"/></svg>").HasViolations);

	[Test]
	public void Strips_external_marker_reference()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><path d="M0 0L1 1" marker-end="url(https://attacker.invalid/m.svg#x)"/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("marker-end", StringComparison.Ordinal));
	}

	[Test]
	public void Allows_same_document_marker_reference()
	{
		var svg =
			"""<svg xmlns="http://www.w3.org/2000/svg"><defs><marker id="arrow"><path d="M0 0L1 1"/></marker></defs><path d="M0 0L1 1" marker-end="url(#arrow)"/></svg>""";

		Verify(!SvgSanitizer.Sanitize(svg).HasViolations);
	}

	[Test]
	public void Strips_style_attribute_even_if_a_custom_name_allowlist_includes_it()
	{
		var attributes = SvgSanitizer.DefaultAllowedAttributes.Append("style").ToFrozenSet(StringComparer.Ordinal);
		var result = SvgSanitizer.Sanitize("""<svg xmlns="http://www.w3.org/2000/svg"><rect style="fill:red"/></svg>""", SvgSanitizer.DefaultAllowedElements, attributes);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("style=", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_style_element_even_if_a_custom_name_allowlist_includes_it()
	{
		var elements = SvgSanitizer.DefaultAllowedElements.Append("style").ToFrozenSet(StringComparer.Ordinal);
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><style>body { display: none; }</style></svg>""", elements, SvgSanitizer.DefaultAllowedAttributes);

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<style", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_processing_instructions_and_xml_declaration()
	{
		var result = SvgSanitizer.Sanitize(
			"""<?xml version="1.0"?><?xml-stylesheet href="https://attacker.invalid/x.css"?><svg xmlns="http://www.w3.org/2000/svg"><rect/></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<?xml", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("attacker.invalid", StringComparison.Ordinal));
	}

	[Test]
	public void Strip_returns_empty_svg_for_document_type_declarations()
	{
		var result = SvgSanitizer.Sanitize("""<!DOCTYPE svg [<!ENTITY x "payload">]><svg xmlns="http://www.w3.org/2000/svg"><text>&x;</text></svg>""");

		Verify(result.HasViolations);
		Verify(result.Svg == Empty);
		Verify(result.Violations.Count(v => v.Kind == "document" && v.Name == "malformed-xml") == 1);
	}

	[Test]
	public void Strip_returns_empty_svg_for_an_external_entity()
	{
		var result = SvgSanitizer.Sanitize(
			"""<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]><svg xmlns="http://www.w3.org/2000/svg"><text>&x;</text></svg>""");

		Verify(result.Svg == Empty);
		Verify(result.Violations.Count(v => v.Kind == "document" && v.Name == "malformed-xml") == 1);
	}

	[Test]
	public void Strip_returns_empty_svg_for_malformed_xml()
	{
		var result = SvgSanitizer.Sanitize("<svg><g></svg>", SanitizeMode.Strip);

		Verify(result.HasViolations);
		Verify(result.Svg == Empty);
		Verify(result.Violations.Count(v => v.Kind == "document" && v.Name == "malformed-xml") == 1);
	}

	[Test]
	public void Block_overload_throws_for_malformed_xml()
	{
		Action act = () => SvgSanitizer.Sanitize("<svg><g></svg>", SanitizeMode.Block);

		var failure = ThrowsExactly<MermaidSvgException>(act);
		Verify(failure.Message.Contains("not well-formed SVG/XML", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void Block_overload_throws_for_well_formed_policy_violation()
	{
		Action act = () => SvgSanitizer.Sanitize("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>attack</script></svg>", SanitizeMode.Block);

		var failure = ThrowsExactly<MermaidSvgException>(act);
		Verify(failure.Message.Contains("disallowed", StringComparison.Ordinal), $"{failure.Message}");
		Verify(failure.Message.Contains("script", StringComparison.Ordinal), $"{failure.Message}");
	}

	[Test]
	public void Mode_overload_rejects_unknown_enum_values()
	{
		Action act = () => SvgSanitizer.Sanitize("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", (SanitizeMode)int.MaxValue);

		ThrowsExactly<ArgumentOutOfRangeException>(act);
	}

	[Test]
	public void MermaidSvgException_keeps_an_immutable_snapshot_of_all_violations()
	{
		var source = new List<SvgViolation> { new("element", "script"), new("attribute", "onclick", "rect") };
		var exception = new MermaidSvgException(source);

		source.Clear();

		Verify(exception.Violations.Count() == 2);
		Verify(exception.Violations.Contains(new SvgViolation("element", "script")));
		var collection = (IList<SvgViolation>)exception.Violations;
		Action act = () => collection[0] = new SvgViolation("element", "changed");
		ThrowsExactly<NotSupportedException>(act);
	}

	[Test]
	public void Strips_nested_elements_from_title_and_description()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><title>safe<text>nested</text></title><desc><image href="data:image/svg+xml;base64,PHN2ZyAvPg=="/></desc></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<text", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("<image", StringComparison.Ordinal));
	}

	[Test]
	public void Metadata_elements_retain_text_nodes_only()
	{
		var result = SvgSanitizer.Sanitize(
			"""<svg xmlns="http://www.w3.org/2000/svg"><title>safe<!-- hidden --><![CDATA[ text]]></title><desc>description<!-- hidden --></desc></svg>""");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<!--", StringComparison.Ordinal));
		Verify(XDocument.Parse(result.Svg).Descendants()
			.Where(element => element.Name.LocalName is "title" or "desc")
			.SelectMany(element => element.Nodes()).All(node => node is XText));
	}

	[Test]
	public void Strips_svg_image_data_uri_when_nested_svg_contains_active_content()
	{
		var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>"""));
		var result = SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"data:image/svg+xml;base64,{data}\"/></svg>");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("href=", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_svg_image_data_uri_when_nested_svg_contains_css()
	{
		var data = Convert.ToBase64String(Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg"><style>body{display:none}</style><rect/></svg>"""));
		var result = SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"data:image/svg+xml;base64,{data}\"/></svg>");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("href=", StringComparison.Ordinal));
	}

	[Test]
	public void Strips_svg_image_data_uri_nested_deeper_than_the_limit()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg"/>""";
		for (var i = 0; i < 6; i++)
		{
			svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}\"/></svg>";
		}

		Verify(SvgSanitizer.Sanitize(svg).HasViolations);
	}

	[Test]
	public void Strips_png_data_uri_without_png_signature()
	{
		var fakePng = Convert.ToBase64String(Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
		var result = SvgSanitizer.Sanitize($"<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"data:image/png;base64,{fakePng}\"/></svg>");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("href=", StringComparison.Ordinal));
	}

	[Test]
	public void Renderer_output_keeps_a_style_element_scoped_to_the_root()
	{
		var svg = """<svg xmlns="http://www.w3.org/2000/svg" id="m"><style>#m .a&gt;*{fill:red;stroke:rgb(1, 2, 3)!important;}#m{filter:url(#m-drop-shadow);}@keyframes dash{from{stroke-dashoffset:0;}50.5%{opacity:0.5;}}</style></svg>""";
		var result = SvgSanitizer.SanitizeRendererOutput(svg);

		Verify(!result.HasViolations);
		Verify(ReferenceEquals(result.Svg, svg));
	}

	[Test]
	[Arguments("#m{fill:red;}#n{fill:red;}")]
	[Arguments(".a{fill:red;}")]
	[Arguments("#m .a,.b{fill:red;}")]
	[Arguments("#mm{fill:red;}")]
	[Arguments("#m{fill:red}")]
	[Arguments("#m{fill:url(https://attacker.invalid/x);}")]
	[Arguments("#m{filter:url(#n-drop-shadow);}")]
	[Arguments("#m{fill:u\\72l(https://attacker.invalid/x);}")]
	[Arguments("#m{background-image:red;}")]
	[Arguments("#m .a:has(.b){fill:red;}")]
	[Arguments("#m .a:not([href]){fill:red;}")]
	[Arguments("#m .a:nth-of-type(odd){fill:red;}")]
	[Arguments("#m{fill:expression(alert(1));}")]
	[Arguments("#m{fill:red;/* x */}")]
	[Arguments("@import url(https://attacker.invalid/x.css);#m{fill:red;}")]
	[Arguments("@media screen{#m{fill:red;}}")]
	[Arguments("@keyframes dash{-1%{opacity:0;}}")]
	[Arguments("#m{font-family:\"a;b\";}")]
	[Arguments("#m{fill:red;")]
	public void Renderer_output_strips_a_style_element_outside_the_writers_grammar(string css)
	{
		var result = SvgSanitizer.SanitizeRendererOutput($"<svg xmlns=\"http://www.w3.org/2000/svg\" id=\"m\"><style>{css}</style></svg>");

		Verify(result.HasViolations);
		Verify(!result.Svg.Contains("<style", StringComparison.Ordinal));
	}

	[Test]
	public void Renderer_output_strips_a_style_element_with_attributes_or_markup_or_no_root_id()
	{
		Verify(SvgSanitizer.SanitizeRendererOutput("""<svg xmlns="http://www.w3.org/2000/svg" id="m"><style media="x">#m{fill:red;}</style></svg>""").HasViolations);
		Verify(SvgSanitizer.SanitizeRendererOutput("""<svg xmlns="http://www.w3.org/2000/svg" id="m"><style>#m{fill:red;}<!-- x --></style></svg>""").HasViolations);
		Verify(SvgSanitizer.SanitizeRendererOutput("""<svg xmlns="http://www.w3.org/2000/svg"><style>#m{fill:red;}</style></svg>""").HasViolations);
		Verify(SvgSanitizer.SanitizeRendererOutput("""<svg xmlns="http://www.w3.org/2000/svg" id="m"><g><style>#m{fill:red;}</style></g></svg>""").HasViolations);
	}

	[Test]
	public void Renderer_output_keeps_only_one_style_element()
	{
		var result = SvgSanitizer.SanitizeRendererOutput("""<svg xmlns="http://www.w3.org/2000/svg" id="m"><style>#m{fill:red;}</style><style>#m{fill:blue;}</style></svg>""");

		Verify(result.HasViolations);
		Verify(result.Svg.Contains("red", StringComparison.Ordinal));
		Verify(!result.Svg.Contains("blue", StringComparison.Ordinal));
	}
}
