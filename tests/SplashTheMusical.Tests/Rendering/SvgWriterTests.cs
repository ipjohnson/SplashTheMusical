using System.Buffers;
using System.Globalization;
using System.Text;
using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Config;
using SplashTheMusical.Rendering;

namespace SplashTheMusical.Tests.Rendering;

public class SvgWriterTests
{
	private const string Root = "<svg xmlns=\"http://www.w3.org/2000/svg\"";

	[Test]
	[Arguments("script")]
	[Arguments("foreignObject")]
	[Arguments("style")]
	[Arguments("image")]
	[Arguments("a")]
	[Arguments("use")]
	[Arguments("animate")]
	[Arguments("SVG")]
	[Arguments("")]
	public void AnElementOutsideTheAllowlistThrows(string name)
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");

		FluentActions.Invoking(() => writer.StartElement(name)).Should().Throw<ArgumentException>().WithMessage($"*<{name}>*");
	}

	[Test]
	[Arguments("onclick")]
	[Arguments("onload")]
	[Arguments("href")]
	[Arguments("xlink:href")]
	[Arguments("style")]
	[Arguments("xmlns")]
	[Arguments("xmlns:e")]
	[Arguments("data-")]
	[Arguments("data-a b")]
	[Arguments("aria-x\"")]
	[Arguments("e:data-value")]
	public void AnAttributeOutsideTheAllowlistThrows(string name)
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");

		FluentActions.Invoking(() => writer.WriteAttribute(name, "x")).Should().Throw<ArgumentException>().WithMessage($"*'{name}'*");
	}

	[Test]
	[Arguments("fill", "url(https://attacker.invalid/x)")]
	[Arguments("stroke", "red;position:fixed")]
	[Arguments("color", "expression(alert(1))")]
	[Arguments("marker-end", "url(#a) url(https://attacker.invalid/x)")]
	[Arguments("marker-start", "url(data:image/svg+xml;base64,AAAA)")]
	public void AValueOutsideItsAttributesGrammarThrows(string name, string value)
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");

		FluentActions.Invoking(() => writer.WriteAttribute(name, value)).Should().Throw<ArgumentException>();
	}

	[Test]
	public void TheRootMustBeSvg() =>
		FluentActions.Invoking(() => new SvgWriter(new ArrayBufferWriter<byte>()).StartElement("g")).Should().Throw<ArgumentException>();

	[Test]
	public void AnAttributeCannotBeWrittenTwice()
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");
		writer.WriteAttribute("width", 10);

		FluentActions.Invoking(() => writer.WriteAttribute("width", 20)).Should().Throw<ArgumentException>();
	}

	[Test]
	public void AnAttributeCannotFollowContent()
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");
		writer.StartElement("g");
		writer.EndElement();

		FluentActions.Invoking(() => writer.WriteAttribute("width", 20)).Should().Throw<InvalidOperationException>();
	}

	[Test]
	[Arguments("title")]
	[Arguments("desc")]
	public void TitleAndDescHoldOnlyText(string name)
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");
		writer.StartElement(name);
		writer.WriteText("ok");

		FluentActions.Invoking(() => writer.StartElement("text")).Should().Throw<InvalidOperationException>();
	}

	[Test]
	public void TextAndAttributesAreEscaped()
	{
		const string Hostile = "a<b & \"c\" <script>alert('x')</script>";

		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.WriteAttribute("aria-label", Hostile);
			writer.StartElement("text");
			writer.WriteAttribute("class", Hostile);
			writer.WriteText(Hostile);
			writer.EndElement();
			writer.EndElement();
		});

		svg.Should().Be(
			Root + " aria-label=\"a&lt;b &amp; &quot;c&quot; &lt;script&gt;alert('x')&lt;/script&gt;\">"
			+ "<text class=\"a&lt;b &amp; &quot;c&quot; &lt;script&gt;alert('x')&lt;/script&gt;\">"
			+ "a&lt;b &amp; \"c\" &lt;script&gt;alert('x')&lt;/script&gt;</text></svg>");
		System.Xml.Linq.XDocument.Parse(svg).Root!.Value.Should().Be(Hostile);
	}

	[Test]
	public void AttributesKeepTabsAndNewLinesAndTextKeepsCarriageReturns()
	{
		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.WriteAttribute("data-x", "a\tb\nc\rd");
			writer.WriteText("e\r\nf");
			writer.EndElement();
		});

		svg.Should().Be(Root + " data-x=\"a&#9;b&#10;c&#13;d\">e&#13;\nf</svg>");
		var root = System.Xml.Linq.XDocument.Parse(svg).Root!;
		root.Attribute("data-x")!.Value.Should().Be("a\tb\nc\rd");
		root.Value.Should().Be("e\r\nf");
	}

	[Test]
	public void CharactersXmlCannotHoldBecomeTheReplacementCharacter()
	{
		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.WriteAttribute("data-x", "a\0b\u001Fc￾");
			writer.WriteText("\uD800d\u0008");
			writer.EndElement();
		});

		svg.Should().Be(Root + " data-x=\"a�b�c�\">�d�</svg>");
	}

	[Test]
	public void AnElementWithoutContentIsAnEmptyElementTag()
	{
		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.StartElement("g");
			writer.WriteAttribute("class", "nodes");
			writer.StartElement("rect");
			writer.WriteAttribute("width", 10);
			writer.EndElement();
			writer.EndElement();
			writer.EndElement();
		});

		svg.Should().Be(Root + "><g class=\"nodes\"><rect width=\"10\"/></g></svg>");
	}

	[Test]
	public void PaintAndMarkerValuesOfTheirGrammarAreWritten()
	{
		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.StartElement("path");
			writer.WriteAttribute("d", "M0 0L1 1");
			writer.WriteAttribute("fill", "none");
			writer.WriteAttribute("stroke", "rgb(1, 2, 3)");
			writer.WriteAttribute("color", "#ffcc00");
			writer.WriteAttribute("marker-end", "url(#mermaid-1_flowchart-pointEnd)");
			writer.EndElement();
			writer.EndElement();
		});

		svg.Should().Contain("marker-end=\"url(#mermaid-1_flowchart-pointEnd)\"");
	}

	[Test]
	public void NumbersAreWrittenAsJavaScriptWritesThem() =>
		Gen.Double.Sample(value =>
		{
			var svg = SvgDocuments.Write(writer =>
			{
				writer.StartElement("svg");
				writer.WriteAttribute("width", value);
				writer.EndElement();
			});

			svg.Should().Be(Root + " width=\"" + JsNumber.ToString(value) + "\"/>");
		});

	[Test]
	public void TheDocumentIsTheSameBytesUnderEveryCulture()
	{
		var original = CultureInfo.CurrentCulture;
		try
		{
			var bytes = new Dictionary<string, byte[]>();
			foreach (var name in new[] { "tr-TR", "de-DE", "" })
			{
				CultureInfo.CurrentCulture = new CultureInfo(name);
				bytes[name] = SvgDocuments.WriteBytes(CultureDocument);
			}

			bytes["tr-TR"].Should().Equal(bytes["de-DE"]);
			bytes["tr-TR"].Should().Equal(bytes[""]);
			var svg = Encoding.UTF8.GetString(bytes["tr-TR"]);
			svg.Should().StartWith(Root + " id=\"DIAGRAM-I\" width=\"1234.5\" height=\"0.000001\"><style>#DIAGRAM-I{font-family:");
			svg.Should().Contain("<rect x=\"1e+21\" y=\"0.30000000000000004\" rx=\"1.5e-7\" stroke-width=\"-1234567.891\" class=\"IiIı\"/>");
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	private static void CultureDocument(SvgWriter writer)
	{
		writer.StartElement("svg");
		writer.WriteAttribute("id", "DIAGRAM-I");
		writer.WriteAttribute("width", 1234.5);
		writer.WriteAttribute("height", 0.000001);
		writer.WriteStyle(MermaidStylesheet.Build(new MermaidConfig { ThemeVariables = Themes.Theme.Variables("forest", null) }, [], []));
		writer.StartElement("rect");
		writer.WriteAttribute("x", 1e21);
		writer.WriteAttribute("y", 0.1 + 0.2);
		writer.WriteAttribute("rx", 1.5e-7);
		writer.WriteAttribute("stroke-width", -1234567.891);
		writer.WriteAttribute("class", "IiIı");
		writer.EndElement();
		writer.StartElement("text");
		writer.WriteText("İi 1.5");
		writer.EndElement();
		writer.EndElement();
	}

	[Test]
	public void WritingAHundredElementsToAPresizedBufferAllocatesNothing()
	{
		var buffer = new ArrayBufferWriter<byte>(64 * 1024);
		var writer = new SvgWriter(buffer);
		Allocations.ShouldAllocateExactly(0, () =>
		{
			buffer.ResetWrittenCount();
			HundredElements(writer);
		});

		writer.IsComplete.Should().BeTrue();
		var svg = SvgDocuments.Checked(Encoding.UTF8.GetString(buffer.WrittenSpan));
		System.Xml.Linq.XDocument.Parse(svg).Root!.DescendantsAndSelf().Should().HaveCount(100);
	}

	private static void HundredElements(SvgWriter writer)
	{
		writer.StartElement("svg");
		writer.WriteAttribute("id", "mermaid-1");
		writer.WriteAttribute("viewBox", "0 0 400 300");
		writer.StartElement("g");
		writer.WriteAttribute("class", "nodes");
		for (var i = 0; i < 49; i++)
		{
			writer.StartElement("rect");
			writer.WriteAttribute("x", i * 1.5);
			writer.WriteAttribute("y", -i / 3.0);
			writer.WriteAttribute("width", 120);
			writer.WriteAttribute("fill", "#ECECFF");
			writer.WriteAttribute("data-id", "node<&>");
			writer.EndElement();
			writer.StartElement("text");
			writer.WriteAttribute("text-anchor", "middle");
			writer.WriteText("A & B < C — ü");
			writer.EndElement();
		}

		writer.EndElement();
		writer.EndElement();
	}

	[Test]
	public void TheWriterStartsTheNextDocumentOnceTheRootEnds()
	{
		var buffer = new ArrayBufferWriter<byte>();
		var writer = new SvgWriter(buffer);
		writer.StartElement("svg");
		writer.EndElement();
		buffer.ResetWrittenCount();
		writer.StartElement("svg");
		writer.EndElement();

		SvgDocuments.Checked(Encoding.UTF8.GetString(buffer.WrittenSpan)).Should().Be(Root + "/>");
	}

	[Test]
	public void TheStringOverloadThrowsWhenAnElementIsLeftOpen() =>
		FluentActions.Invoking(() => SvgWriter.Write(0, static (writer, _) => writer.StartElement("svg"))).Should().Throw<InvalidOperationException>();

	[Test]
	public void TheStyleElementIsScopedToTheRootsId()
	{
		var svg = SvgDocuments.Write(writer =>
		{
			writer.StartElement("svg");
			writer.WriteAttribute("id", "mermaid-7".AsSpan());
			writer.WriteStyle(MermaidStylesheet.Build(new MermaidConfig { ThemeVariables = Themes.Theme.Variables("redux", null) }, [], []));
			writer.EndElement();
		});

		svg.Should().Contain("<style>#mermaid-7{").And.Contain("filter:url(#mermaid-7-drop-shadow)");
	}

	[Test]
	public void TheStyleElementNeedsTheRootsIdAndIsWrittenOnceAsItsChild()
	{
		var rules = new List<CssRule> { new CssStyleRule(CssSelector.Root, CssDeclaration.FromTheme("fill", "red")) };
		var noId = new SvgWriter(new ArrayBufferWriter<byte>());
		noId.StartElement("svg");
		FluentActions.Invoking(() => noId.WriteStyle(rules)).Should().Throw<InvalidOperationException>();

		var nested = new SvgWriter(new ArrayBufferWriter<byte>());
		nested.StartElement("svg");
		nested.WriteAttribute("id", "a");
		nested.StartElement("g");
		FluentActions.Invoking(() => nested.WriteStyle(rules)).Should().Throw<InvalidOperationException>();

		var twice = new SvgWriter(new ArrayBufferWriter<byte>());
		twice.StartElement("svg");
		twice.WriteAttribute("id", "a");
		twice.WriteStyle(rules);
		FluentActions.Invoking(() => twice.WriteStyle(rules)).Should().Throw<InvalidOperationException>();

		var notIdentifier = new SvgWriter(new ArrayBufferWriter<byte>());
		notIdentifier.StartElement("svg");
		notIdentifier.WriteAttribute("id", "1a");
		FluentActions.Invoking(() => notIdentifier.WriteStyle(rules)).Should().Throw<ArgumentException>();
	}
}
