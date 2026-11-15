using AwesomeAssertions;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Tests.Parsing;

public class DirectiveReaderTests
{
	[Test]
	public void ReadsAnInitDirective()
	{
		var (directive, rest) = Read("""%%{init: {"theme": "dark", "flowchart": {"curve": "basis", "padding": 8.5}}}%%""" + "\ngraph TD");
		directive!.Type.Should().Be("init");
		ValueJson.Write(directive.Arguments!).Should().Be("""{"theme":"dark","flowchart":{"curve":"basis","padding":8.5}}""");
		rest.Should().Be("\ngraph TD");
	}

	[Test]
	public void SingleQuotesBecomeDoubleQuotes()
	{
		var (directive, _) = Read("%%{ initialize : { 'theme': 'forest', 'list': [1, true, null] } }%%");
		directive!.Type.Should().Be("initialize");
		ValueJson.Write(directive.Arguments!).Should().Be("""{"theme":"forest","list":[1,true,null]}""");
	}

	[Test]
	public void ADirectiveWithoutArgumentsHasNone()
	{
		var (directive, rest) = Read("%%{wrap}%% A");
		directive.Should().Be(new MermaidDirective("wrap", null));
		rest.Should().Be(" A");
	}

	[Test]
	public void AColonWithoutArgumentsHasNone() => Read("%%{init: }%%").Directive.Should().Be(new MermaidDirective("init", null));

	[Test]
	public void OneWordArgumentIsAString()
	{
		var (directive, rest) = Read("%%{init: true }%%x");
		directive!.Arguments!.String.Should().Be("true");
		rest.Should().Be("x");
	}

	[Test]
	public void TheArgumentsMaySpanLines()
	{
		var cursor = new SourceCursor("%%{init: {\n  \"theme\": \"base\"\n}}%%\nA");
		DirectiveReader.TryRead(ref cursor, [], ResourceLimits.Default, out var directive).Should().BeTrue();
		ValueJson.Write(directive!.Arguments!).Should().Be("""{"theme":"base"}""");
		cursor.Line.Should().Be(3);
		cursor.Column.Should().Be(5);
	}

	[Test]
	public void TheClosingDelimiterIsOptional()
	{
		var (directive, rest) = Read("""%%{init: {"a": 1}""");
		ValueJson.Write(directive!.Arguments!).Should().Be("""{"a":1}""");
		rest.Should().BeEmpty();
	}

	[Test]
	public void ADuplicateKeyKeepsTheLastValue() =>
		ValueJson.Write(Read("""%%{init: {"a": 1, "b": 2, "a": 3}}%%""").Directive!.Arguments!).Should().Be("""{"a":3,"b":2}""");

	[Test]
	public void ANumberTooLargeForADoubleIsInfinity() =>
		Read("%%{init: [1e400]}%%").Directive!.Arguments!.Items[0].Number.Should().Be(double.PositiveInfinity);

	[Test]
	[Arguments("%%{init: {theme: \"dark\"}}%%")]
	[Arguments("%%{init: {\"title\": \"it's\"}}%%")]
	[Arguments("%%{init: {\"a\": 1,}}%%")]
	[Arguments("%%{init: {\"a\": 1} extra}%%")]
	public void InvalidJsonIsIgnoredWithAWarning(string source)
	{
		var cursor = new SourceCursor(source + "\nA");
		var diagnostics = new List<MermaidDiagnostic>();
		DirectiveReader.TryRead(ref cursor, diagnostics, ResourceLimits.Default, out var directive).Should().BeTrue();
		directive.Should().BeNull();
		cursor.Remaining.ToString().Should().Be("\nA");
		var warning = diagnostics.Should().ContainSingle().Subject;
		warning.Severity.Should().Be(DiagnosticSeverity.Warning);
		warning.Message.Should().StartWith("The 'init' directive is ignored because its arguments are not valid JSON.");
		(warning.Line, warning.Column).Should().Be((1, 1));
	}

	[Test]
	[Arguments("%%{}%%")]
	[Arguments("%%{ : 1}%%")]
	public void WithoutATypeItIsNotADirective(string source)
	{
		var cursor = new SourceCursor(source);
		var diagnostics = new List<MermaidDiagnostic>();
		DirectiveReader.TryRead(ref cursor, diagnostics, ResourceLimits.Default, out var directive).Should().BeFalse();
		directive.Should().BeNull();
		cursor.Position.Should().Be(0);
		diagnostics.Should().BeEmpty();
	}

	[Test]
	public void NestingDeeperThanTheLimitThrows()
	{
		var limits = new ResourceLimits { MaxRecursionDepth = 2 };
		var cursor = new SourceCursor("%%{init: [[1]]}%%");
		DirectiveReader.TryRead(ref cursor, [], limits, out _).Should().BeTrue();

		var act = () =>
		{
			var deep = new SourceCursor("%%{init: [[[1]]]}%%");
			DirectiveReader.TryRead(ref deep, [], limits, out _);
		};
		act.Should().Throw<MermaidResourceLimitException>()
			.Which.LimitName.Should().Be(nameof(ResourceLimits.MaxRecursionDepth));
	}

	private static (MermaidDirective? Directive, string Remaining) Read(string source)
	{
		var cursor = new SourceCursor(source);
		var diagnostics = new List<MermaidDiagnostic>();
		DirectiveReader.TryRead(ref cursor, diagnostics, ResourceLimits.Default, out var directive).Should().BeTrue();
		diagnostics.Should().BeEmpty();
		return (directive, cursor.Remaining.ToString());
	}
}
