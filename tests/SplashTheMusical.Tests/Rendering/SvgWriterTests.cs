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

		var failure = Throws<ArgumentException>(() => writer.StartElement(name));
		Verify(failure.Message.Contains($"<{name}>", StringComparison.Ordinal), $"{failure.Message}");
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

		var failure = Throws<ArgumentException>(() => writer.WriteAttribute(name, "x"));
		Verify(failure.Message.Contains($"'{name}'", StringComparison.Ordinal), $"{failure.Message}");
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

		Throws<ArgumentException>(() => writer.WriteAttribute(name, value));
	}

	[Test]
	public void TheRootMustBeSvg() =>
		Throws<ArgumentException>(() => new SvgWriter(new ArrayBufferWriter<byte>()).StartElement("g"));

	[Test]
	public void AnAttributeCannotBeWrittenTwice()
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");
		writer.WriteAttribute("width", 10);

		Throws<ArgumentException>(() => writer.WriteAttribute("width", 20));
	}

	[Test]
	public void AnAttributeCannotFollowContent()
	{
		var writer = new SvgWriter(new ArrayBufferWriter<byte>());
		writer.StartElement("svg");
		writer.StartElement("g");
		writer.EndElement();

		Throws<InvalidOperationException>(() => writer.WriteAttribute("width", 20));
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

		Throws<InvalidOperationException>(() => writer.StartElement("text"));
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

		Verify(svg ==
			Root + " aria-label=\"a&lt;b &amp; &quot;c&quot; &lt;script&gt;alert('x')&lt;/script&gt;\">"
			+ "<text class=\"a&lt;b &amp; &quot;c&quot; &lt;script&gt;alert('x')&lt;/script&gt;\">"
			+ "a&lt;b &amp; \"c\" &lt;script&gt;alert('x')&lt;/script&gt;</text></svg>");
		Verify(System.Xml.Linq.XDocument.Parse(svg).Root!.Value == Hostile);
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

		Verify(svg == Root + " data-x=\"a&#9;b&#10;c&#13;d\">e&#13;\nf</svg>");
		var root = System.Xml.Linq.XDocument.Parse(svg).Root!;
		Verify(root.Attribute("data-x")!.Value == "a\tb\nc\rd");
		Verify(root.Value == "e\r\nf");
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

		Verify(svg == Root + " data-x=\"a�b�c�\">�d�</svg>");
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

		Verify(svg == Root + "><g class=\"nodes\"><rect width=\"10\"/></g></svg>");
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

		Verify(svg.Contains("marker-end=\"url(#mermaid-1_flowchart-pointEnd)\"", StringComparison.Ordinal));
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

			Verify(svg == Root + " width=\"" + JsNumber.ToString(value) + "\"/>");
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

			Verify((bytes["tr-TR"]).SequenceEqual(bytes["de-DE"]));
			Verify((bytes["tr-TR"]).SequenceEqual(bytes[""]));
			var svg = Encoding.UTF8.GetString(bytes["tr-TR"]);
			Verify(svg.StartsWith(Root + " id=\"DIAGRAM-I\" width=\"1234.5\" height=\"0.000001\"><style>#DIAGRAM-I{font-family:", StringComparison.Ordinal));
			Verify(svg.Contains("<rect x=\"1e+21\" y=\"0.30000000000000004\" rx=\"1.5e-7\" stroke-width=\"-1234567.891\" class=\"IiIı\"/>", StringComparison.Ordinal));
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

		Verify(writer.IsComplete);
		var svg = SvgDocuments.Checked(Encoding.UTF8.GetString(buffer.WrittenSpan));
		Verify(System.Xml.Linq.XDocument.Parse(svg).Root!.DescendantsAndSelf().Count() == 100);
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

		Verify(SvgDocuments.Checked(Encoding.UTF8.GetString(buffer.WrittenSpan)) == Root + "/>");
	}

	[Test]
	public void TheStringOverloadThrowsWhenAnElementIsLeftOpen() =>
		Throws<InvalidOperationException>(() => SvgWriter.Write(0, static (writer, _) => writer.StartElement("svg")));

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

		Verify(svg.Contains("<style>#mermaid-7{", StringComparison.Ordinal));
		Verify(svg.Contains("filter:url(#mermaid-7-drop-shadow)", StringComparison.Ordinal));
	}

	[Test]
	public void TheStyleElementNeedsTheRootsIdAndIsWrittenOnceAsItsChild()
	{
		var rules = new List<CssRule> { new CssStyleRule(CssSelector.Root, CssDeclaration.FromTheme("fill", "red")) };
		var noId = new SvgWriter(new ArrayBufferWriter<byte>());
		noId.StartElement("svg");
		Throws<InvalidOperationException>(() => noId.WriteStyle(rules));

		var nested = new SvgWriter(new ArrayBufferWriter<byte>());
		nested.StartElement("svg");
		nested.WriteAttribute("id", "a");
		nested.StartElement("g");
		Throws<InvalidOperationException>(() => nested.WriteStyle(rules));

		var twice = new SvgWriter(new ArrayBufferWriter<byte>());
		twice.StartElement("svg");
		twice.WriteAttribute("id", "a");
		twice.WriteStyle(rules);
		Throws<InvalidOperationException>(() => twice.WriteStyle(rules));

		var notIdentifier = new SvgWriter(new ArrayBufferWriter<byte>());
		notIdentifier.StartElement("svg");
		notIdentifier.WriteAttribute("id", "1a");
		Throws<ArgumentException>(() => notIdentifier.WriteStyle(rules));
	}
}
