using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class YamlReaderTests
{
	[Test]
	[Arguments("")]
	[Arguments("\n\n")]
	[Arguments("# only a comment\n   \n")]
	public void AnEmptyDocumentIsNull(string yaml) => Json(yaml).Should().Be("null");

	[Test]
	public void ReadsMermaidFrontMatter()
	{
		const string yaml = """
			title: Hello
			displayMode: compact
			config:
			  theme: forest
			  themeVariables:
			    primaryColor: "#ff0000"
			  flowchart:
			    curve: basis
			    htmlLabels: false
			    nodeSpacing: 30
			""";

		Json(yaml).Should().Be(
			"""{"title":"Hello","displayMode":"compact","config":{"theme":"forest","themeVariables":{"primaryColor":"#ff0000"},"flowchart":{"curve":"basis","htmlLabels":false,"nodeSpacing":30}}}""");
	}

	[Test]
	public void AMappingKeepsItsKeysInSourceOrder()
	{
		var value = Read("z: 1\na: 2\nm: 3");
		value.Entries.Keys.Should().Equal("z", "a", "m");
	}

	[Test]
	public void AMappingKeyWithoutAValueIsNull() => Json("a:\nb: 1").Should().Be("""{"a":null,"b":1}""");

	[Test]
	public void QuotedKeysAreUnquoted() => Json("\"a b\": 1\n'c''d': 2").Should().Be("""{"a b":1,"c'd":2}""");

	[Test]
	public void ReadsABlockSequence() => Json("- a\n- 1\n-\n- true").Should().Be("""["a",1,null,true]""");

	[Test]
	public void ASequenceMayShareItsKeysIndentation() =>
		Json("items:\n- a\n- b\nnext: 1").Should().Be("""{"items":["a","b"],"next":1}""");

	[Test]
	public void ASequenceMayBeIndentedUnderItsKey() =>
		Json("items:\n  - a\n  - b").Should().Be("""{"items":["a","b"]}""");

	[Test]
	public void ReadsCompactMappingsInASequence() =>
		Json("- a: 1\n  b: 2\n- c: 3").Should().Be("""[{"a":1,"b":2},{"c":3}]""");

	[Test]
	public void ReadsCompactNestedSequences() => Json("- - a\n  - b\n- c").Should().Be("""[["a","b"],"c"]""");

	[Test]
	public void ReadsAFlowMapping() =>
		Json("""{ shape: rect, label: "Hi", "w": 10 }""").Should().Be("""{"shape":"rect","label":"Hi","w":10}""");

	[Test]
	public void ReadsAFlowSequence() => Json("[a, 'b', 1.5, [c], {d: e}]").Should().Be("""["a","b",1.5,["c"],{"d":"e"}]""");

	[Test]
	public void AFlowCollectionMaySpanLinesWithCommentsAndATrailingComma() =>
		Json("a: [\n  x, # first\n  y,\n]\nb: { k: v,\n  n }").Should().Be("""{"a":["x","y"],"b":{"k":"v","n":null}}""");

	[Test]
	public void AFlowPlainScalarMayHoldAColonWithoutASpace() =>
		Json("[http://a.b, a:b]").Should().Be("""["http://a.b","a:b"]""");

	[Test]
	[Arguments("~", "null")]
	[Arguments("null", "null")]
	[Arguments("Null", "null")]
	[Arguments("NULL", "null")]
	[Arguments("true", "true")]
	[Arguments("True", "true")]
	[Arguments("FALSE", "false")]
	[Arguments("yes", "\"yes\"")]
	[Arguments("42", "42")]
	[Arguments("-17", "-17")]
	[Arguments("+5", "5")]
	[Arguments("1_000", "1000")]
	[Arguments("012", "12")]
	[Arguments("0x1F", "31")]
	[Arguments("0o17", "15")]
	[Arguments("0b101", "5")]
	[Arguments("1.5", "1.5")]
	[Arguments("-2.5e3", "-2500")]
	[Arguments("1.", "1")]
	[Arguments(".5", "0.5")]
	[Arguments(".inf", "Infinity")]
	[Arguments("-.Inf", "-Infinity")]
	[Arguments(".NaN", "NaN")]
	[Arguments("-.5", "\"-.5\"")]
	[Arguments("1_", "\"1_\"")]
	[Arguments("0x", "\"0x\"")]
	[Arguments("0x1G", "\"0x1G\"")]
	[Arguments("1e", "\"1e\"")]
	[Arguments("12px", "\"12px\"")]
	public void ResolvesPlainScalarsWithJsYamlsJsonSchema(string plain, string json) => Json("a: " + plain).Should().Be($$"""{"a":{{json}}}""");

	[Test]
	public void APlainScalarMayHoldAColonOrAHashWithoutASpace() =>
		Json("url: http://example.com/a#b").Should().Be("""{"url":"http://example.com/a#b"}""");

	[Test]
	public void ACommentEndsAPlainScalar() => Json("a: b c   # comment\n# another\nd: e").Should().Be("""{"a":"b c","d":"e"}""");

	[Test]
	public void QuotedScalarsAreStrings() => Json("a: '1'\nb: \"true\"\nc: ''").Should().Be("""{"a":"1","b":"true","c":""}""");

	[Test]
	public void ASingleQuotedScalarDoublesItsQuote() => Read("a: 'it''s # here'")["a"]!.String.Should().Be("it's # here");

	[Test]
	public void ADoubleQuotedScalarDecodesItsEscapes() =>
		Read("""a: "q\" b\\ n\n t\t x\x41 u\u00e9 U\U0001F600 s\/ " """)["a"]!.String
			.Should().Be("q\" b\\ n\n t\t xA u\u00e9 U\U0001F600 s/ ");

	[Test]
	[Arguments("|\n  a\n   b\n\n  c\n\n", "a\n b\n\nc\n")]
	[Arguments("|-\n  a\n  b\n\n", "a\nb")]
	[Arguments("|+\n  a\n  b\n\n", "a\nb\n\n")]
	[Arguments("|\n\n  a", "\na\n")]
	public void ReadsALiteralBlockScalar(string scalar, string expected) => Read("a: " + scalar)["a"]!.String.Should().Be(expected);

	[Test]
	[Arguments(">\n  a\n  b\n\n  c\n", "a b\nc\n")]
	[Arguments(">-\n  a\n  b\n", "a b")]
	[Arguments(">\n  a\n    more\n  b\n", "a\n  more\nb\n")]
	[Arguments(">\n  a\n\n\n  b\n", "a\n\nb\n")]
	public void ReadsAFoldedBlockScalar(string scalar, string expected) => Read("a: " + scalar)["a"]!.String.Should().Be(expected);

	[Test]
	public void ABlockScalarEndsAtALessIndentedLine() =>
		Json("a: |\n  x\n  y\nb: 1").Should().Be("""{"a":"x\ny\n","b":1}""");

	[Test]
	public void ABlockScalarInASequence() => Json("- |\n  x\n- y").Should().Be("""["x\n","y"]""");

	[Test]
	[Arguments("a: b\n  c", """{"a":"b c"}""")]
	[Arguments("- b\n  c", """["b c"]""")]
	[Arguments("- a\n  - b", """["a - b"]""")]
	[Arguments("a: b\n\n\n  c # d\ne: f", """{"a":"b\n\nc","e":"f"}""")]
	[Arguments("a:\n  b\n  c\nd: e", """{"a":"b c","d":"e"}""")]
	public void APlainScalarFoldsTheMoreIndentedLinesBelowIt(string yaml, string json) => Json(yaml).Should().Be(json);

	[Test]
	[Arguments("a: \"b\n c\"", """{"a":"b c"}""")]
	[Arguments("a: 'b\n c'", """{"a":"b c"}""")]
	[Arguments("a: \"x  \n\n   y\\\n   z\"", """{"a":"x\nyz"}""")]
	[Arguments("a: 'x \n\n y''z'", """{"a":"x\ny'z"}""")]
	public void AQuotedScalarFoldsItsLines(string yaml, string json) => Json(yaml).Should().Be(json);

	[Test]
	public void ATopLevelScalarIsItsValue() => Json("hello world").Should().Be("\"hello world\"");

	[Test]
	public void CrlfLineBreaksAreRead() => Json("a: 1\r\nb:\r\n  - x\r\n").Should().Be("""{"a":1,"b":["x"]}""");

	[Test]
	[Arguments("a: &x 1", "Anchors are not supported.", 1, 4)]
	[Arguments("a: *x", "Aliases are not supported.", 1, 4)]
	[Arguments("a: !!str 1", "Tags are not supported.", 1, 4)]
	[Arguments("? a\n: b", "Complex mapping keys are not supported.", 1, 1)]
	[Arguments("a: {[x]: 1}", "Complex mapping keys are not supported.", 1, 5)]
	[Arguments("%YAML 1.2\na: 1", "YAML directives are not supported.", 1, 1)]
	[Arguments("---\na: 1", "Document markers are not supported.", 1, 1)]
	[Arguments("a: 1\n---\nb: 2", "Document markers are not supported.", 2, 1)]
	[Arguments("a: 1\n...", "Document markers are not supported.", 2, 1)]
	[Arguments("[a\n b]", "Plain scalars that continue on the next line are not supported.", 2, 2)]
	[Arguments("[a: 1]", "Single-pair mappings inside a flow sequence are not supported.", 1, 3)]
	[Arguments("a: |2\n  x", "Indentation indicators on block scalars are not supported.", 1, 5)]
	[Arguments("a: >-1\n  x", "Indentation indicators on block scalars are not supported.", 1, 6)]
	public void AnUnsupportedConstructIsAnError(string yaml, string message, int line, int column) =>
		Error(yaml).Should().Be(new MermaidDiagnostic(DiagnosticSeverity.Error, message, line, column));

	[Test]
	[Arguments("a: 1\na: 2", "Duplicate mapping key 'a'.", 2, 1)]
	[Arguments("{a: 1, a: 2}", "Duplicate mapping key 'a'.", 1, 8)]
	[Arguments("a:\n\tb: 1", "Tabs cannot be used for indentation.", 2, 2)]
	[Arguments("a: [1, 2", "The flow collection has no closing ']'.", 1, 4)]
	[Arguments("a: {b: 1", "The flow collection has no closing '}'.", 1, 4)]
	[Arguments("a: [\"1\" 2]", "Expected ',' or ']'.", 1, 9)]
	[Arguments("a: [1,,2]", "A plain scalar cannot start with ','.", 1, 7)]
	[Arguments("a: \"b", "The quoted scalar has no closing quote.", 1, 4)]
	[Arguments("a: \"\\q\"", "Unknown escape sequence '\\q'.", 1, 4)]
	[Arguments("a: \"\\x4\"", "The escape sequence '\\x' needs 2 hexadecimal digits of a valid character.", 1, 4)]
	[Arguments("a: b: c", "A mapping value is not allowed here.", 1, 5)]
	[Arguments("a: \"b\" c", "Unexpected content after the value.", 1, 8)]
	[Arguments("a: 1\n  b: 2", "Bad indentation of a mapping entry.", 2, 3)]
	[Arguments("a: b # c\n  d", "Bad indentation of a mapping entry.", 2, 3)]
	[Arguments("a: - b", "A block sequence cannot start on the line of its key.", 1, 4)]
	[Arguments("a: 1\nb", "Expected a mapping entry.", 2, 1)]
	[Arguments("- a\nb: 1", "Unexpected content after the document.", 2, 1)]
	[Arguments("a: @b", "A plain scalar cannot start with '@'.", 1, 4)]
	[Arguments("a: `b", "A plain scalar cannot start with '`'.", 1, 4)]
	public void InvalidYamlIsAnError(string yaml, string message, int line, int column) =>
		Error(yaml).Should().Be(new MermaidDiagnostic(DiagnosticSeverity.Error, message, line, column));

	[Test]
	public void OnlyTheFirstErrorIsReported()
	{
		var diagnostics = new List<MermaidDiagnostic>();
		YamlReader.Read("a: &x 1\nb: *y\nc: !t", diagnostics).Should().BeNull();
		diagnostics.Should().ContainSingle();
	}

	[Test]
	public void DiagnosticsAreOffsetByTheSourcePosition()
	{
		var diagnostics = new List<MermaidDiagnostic>();
		YamlReader.Read("{ a: &x }", diagnostics, firstLine: 3, firstColumn: 10);
		diagnostics.Should().Equal(new MermaidDiagnostic(DiagnosticSeverity.Error, "Anchors are not supported.", 3, 15));

		diagnostics.Clear();
		YamlReader.Read("a: 1\nb: &x", diagnostics, firstLine: 2, firstColumn: 10);
		diagnostics.Should().Equal(new MermaidDiagnostic(DiagnosticSeverity.Error, "Anchors are not supported.", 3, 4));
	}

	[Test]
	public void NestingDeeperThanTheLimitThrows()
	{
		var limits = new ResourceLimits { MaxRecursionDepth = 3 };
		Json("a: [[1]]", limits).Should().Be("""{"a":[[1]]}""");

		var act = () => YamlReader.Read("a: [[[1]]]", [], limits);
		act.Should().Throw<MermaidResourceLimitException>()
			.Which.LimitName.Should().Be(nameof(ResourceLimits.MaxRecursionDepth));

		var block = () => YamlReader.Read("a:\n b:\n  c:\n   d: 1", [], limits);
		block.Should().Throw<MermaidResourceLimitException>();
	}

	[Test]
	public void DeepFlowNestingThrowsBeforeTheStackOverflows()
	{
		var deep = new string('[', 100_000) + new string(']', 100_000);
		var act = () => YamlReader.Read(deep, []);
		act.Should().Throw<MermaidResourceLimitException>();
	}

	private static MermaidValue Read(string yaml, ResourceLimits? limits = null)
	{
		var diagnostics = new List<MermaidDiagnostic>();
		var value = YamlReader.Read(yaml, diagnostics, limits);
		diagnostics.Should().BeEmpty();
		return value!;
	}

	private static string Json(string yaml, ResourceLimits? limits = null) => ValueJson.Write(Read(yaml, limits));

	private static MermaidDiagnostic Error(string yaml)
	{
		var diagnostics = new List<MermaidDiagnostic>();
		YamlReader.Read(yaml, diagnostics).Should().BeNull();
		return diagnostics.Should().ContainSingle().Subject;
	}
}
