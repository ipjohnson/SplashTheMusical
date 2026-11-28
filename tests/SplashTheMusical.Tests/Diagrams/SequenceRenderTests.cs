using SplashTheMusical.Diagrams.Sequence;
using SplashTheMusical.Rendering;
using SplashTheMusical.Tests.Rendering;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The sequence renderer: the elements, classes and markers <c>svgDraw.js</c> and <c>drawMessage</c> write, their
/// order, and the stylesheet of each theme. Every document goes through <see cref="SvgDocuments.Checked"/>.
/// </summary>
public class SequenceRenderTests
{
	private static readonly string[] ThemeNames =
		["base", "dark", "default", "forest", "neutral", "neo", "neo-dark", "redux", "redux-dark", "redux-color", "redux-dark-color"];

	[Test]
	public void EachParticipantTypeDrawsItsShapeAndLifeline()
	{
		var svg = Render("""
			sequenceDiagram
			participant P
			actor A
			participant B@{ "type": "boundary" }
			participant C@{ "type": "control" }
			participant E@{ "type": "entity" }
			participant D@{ "type": "database" }
			participant S@{ "type": "collections" }
			participant Q@{ "type": "queue" }
			P->>Q: hi
			""", look: "classic");

		// One lifeline per participant, numbered in order.
		for (var i = 0; i < 8; i++)
		{
			Verify(svg.Contains($"<line id=\"actor{i}\"", StringComparison.Ordinal), $"lifeline {i}");
		}

		foreach (var type in new[] { "participant", "actor", "boundary", "control", "entity", "database", "collections", "queue" })
		{
			Verify(svg.Contains($"data-type=\"{type}\"", StringComparison.Ordinal), $"{type}");
		}

		Verify(() => Count(svg, "class=\"actor actor-top\"") >= 2 && Count(svg, "class=\"actor actor-bottom\"") >= 2);
		Verify(() => svg.Contains("id=\"actor-man-torso1\"", StringComparison.Ordinal) && svg.Contains("id=\"actor-man-arms2\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("id=\"mermaid-filled-head-control\"", StringComparison.Ordinal));
		Verify(() => svg.Contains(" a ", StringComparison.Ordinal), $"the cylinders are arcs");

		// Classic draws the entity and boundary figures moved down, and the stacked rect of collections.
		Verify(() => svg.Contains("transform=\"translate(0, 21)\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("transform=\"translate(0, 6)\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("class=\"actor\"", StringComparison.Ordinal));
		var drawing = svg[svg.IndexOf("</style>", StringComparison.Ordinal)..];
		Verify(() => !drawing.Contains("data-look", StringComparison.Ordinal) && !drawing.Contains("drop-shadow", StringComparison.Ordinal));
	}

	[Test]
	public void TheGroupsMermaidLowersComeFirst()
	{
		var svg = Render("""
			sequenceDiagram
			box Aqua Team
			participant A
			end
			participant B
			rect rgb(200, 150, 255)
			A->>B: hi
			end
			""");
		var box = svg.IndexOf("fill=\"Aqua\"", StringComparison.Ordinal);
		var background = svg.IndexOf("fill=\"rgb(200, 150, 255)\"", StringComparison.Ordinal);
		var footer = svg.IndexOf("actor actor-bottom", StringComparison.Ordinal);
		var top = svg.IndexOf("actor actor-top", StringComparison.Ordinal);
		var markers = svg.IndexOf("id=\"mermaid-arrowhead\"", StringComparison.Ordinal);
		var message = svg.IndexOf("class=\"messageLine0\"", StringComparison.Ordinal);
		Verify(() => box < background && background < footer && footer < top && top < markers && markers < message);

		// The last participant is lowered last, so it comes first.
		Verify(() => svg.IndexOf("id=\"actor1\"", StringComparison.Ordinal) < svg.IndexOf("id=\"actor0\"", StringComparison.Ordinal));
		Verify(() => svg.Contains(">Team</tspan>", StringComparison.Ordinal));
	}

	[Test]
	[Arguments("->>", "messageLine0", null, "arrowhead")]
	[Arguments("-->>", "messageLine1", null, "arrowhead")]
	[Arguments("-x", "messageLine0", null, "crosshead")]
	[Arguments("--x", "messageLine1", null, "crosshead")]
	[Arguments("-)", "messageLine0", null, "filled-head")]
	[Arguments("--)", "messageLine1", null, "filled-head")]
	[Arguments("->", "messageLine0", null, null)]
	[Arguments("-->", "messageLine1", null, null)]
	[Arguments("<<->>", "messageLine0", "arrowhead", "arrowhead")]
	[Arguments("<<-->>", "messageLine1", "arrowhead", "arrowhead")]
	[Arguments("-|\\", "messageLine0", null, "solidTopArrowHead")]
	[Arguments("--|/", "messageLine1", null, "solidBottomArrowHead")]
	[Arguments("-\\\\", "messageLine0", null, "stickTopArrowHead")]
	[Arguments("--//", "messageLine1", null, "stickBottomArrowHead")]
	[Arguments("/|-", "messageLine0", "solidBottomArrowHead", null)]
	[Arguments("\\|--", "messageLine1", "solidTopArrowHead", null)]
	[Arguments("//-", "messageLine0", "stickBottomArrowHead", null)]
	[Arguments("\\\\--", "messageLine1", "stickTopArrowHead", null)]
	public void EachArrowDrawsItsLineAndMarkers(string arrow, string cls, string? start, string? end)
	{
		var svg = Render($"sequenceDiagram\nA{arrow}B: m\n");
		var line = Element(svg, "data-et=\"message\"");
		Verify(line.Contains($"class=\"{cls}\"", StringComparison.Ordinal), $"{line}");
		Verify(start is null ? !line.Contains("marker-start", StringComparison.Ordinal) : line.Contains($"marker-start=\"url(#mermaid-{start})\"", StringComparison.Ordinal), $"{line}");
		Verify(end is null ? !line.Contains("marker-end", StringComparison.Ordinal) : line.Contains($"marker-end=\"url(#mermaid-{end})\"", StringComparison.Ordinal), $"{line}");
		Verify(svg.Contains($"id=\"mermaid-{start ?? end ?? "arrowhead"}\"", StringComparison.Ordinal));
	}

	[Test]
	public void MessageNumbersSelfMessagesAndCentralConnections()
	{
		var svg = Render("""
			sequenceDiagram
			autonumber 10 5
			A->>B: one
			B->>B: self
			A->>()B: central
			""");
		Verify(() => svg.Contains("class=\"sequenceNumber\">10</text>", StringComparison.Ordinal) && svg.Contains(">15</text>", StringComparison.Ordinal) && svg.Contains(">20</text>", StringComparison.Ordinal));
		Verify(() => Count(svg, "marker-start=\"url(#mermaid-sequencenumber)\"") == 3);
		var self = Element(svg, "data-id=\"i2\"");
		Verify(self.StartsWith("<path d=\"M ", StringComparison.Ordinal) && self.Contains(" C ", StringComparison.Ordinal), $"{self}");
		Verify(() => svg.Contains("r=\"5\" width=\"10\" height=\"10\"", StringComparison.Ordinal));
	}

	[Test]
	public void NotesActivationsAndBlocks()
	{
		var svg = Render("""
			sequenceDiagram
			A->>+B: go
			Note right of B: thinking
			alt yes
			B-->>A: ok
			else no
			B-->>A: not ok
			end
			B->>-A: done
			""", look: "neo");
		Verify(() => svg.Contains("class=\"note\"", StringComparison.Ordinal) && svg.Contains("<tspan x=", StringComparison.Ordinal) && svg.Contains(">thinking</tspan>", StringComparison.Ordinal));
		Verify(() => svg.Contains("class=\"activation0\" data-look=\"neo\"", StringComparison.Ordinal));
		Verify(() => svg.Contains("data-et=\"control-structure\"", StringComparison.Ordinal));
		Verify(() => Count(svg, "class=\"loopLine\"") == 5);
		Verify(() => svg.Contains("class=\"labelBox\"", StringComparison.Ordinal) && svg.Contains("class=\"labelText\">alt</text>", StringComparison.Ordinal));
		Verify(() => svg.Contains(">[yes]</tspan>", StringComparison.Ordinal) && svg.Contains("class=\"sectionTitle\">[no]</text>", StringComparison.Ordinal));

		// The activation's group is added at its start, before the note, and the block after both.
		var activation = svg.IndexOf("activation0", StringComparison.Ordinal);
		Verify(() => activation < svg.IndexOf("class=\"note\"", StringComparison.Ordinal) && activation < svg.IndexOf("control-structure", StringComparison.Ordinal));
	}

	[Test]
	public void CreatedAndDestroyedParticipants()
	{
		var svg = Render("""
			sequenceDiagram
			A->>B: hi
			create participant C
			A->>C: make
			destroy B
			A-xB: bye
			""");
		var layout = SequenceLayoutTests.Layout("sequenceDiagram\nA->>B: hi\ncreate participant C\nA->>C: make\ndestroy B\nA-xB: bye\n");
		var c = layout.Actors.Single(a => a.Name == "C");
		var b = layout.Actors.Single(a => a.Name == "B");
		Verify(() => c.StartY > layout.Actors[0].StartY);
		Verify(() => b.LifelineEndY == b.StopY);
		Verify(svg.Contains(FormattableString.Invariant($"y2=\"{b.StopY}\" class=\"actor-line 200\""), StringComparison.Ordinal), $"{svg}");
	}

	[Test]
	public void TheTitleIsAText()
	{
		var svg = Render("---\ntitle: Hello\n---\nsequenceDiagram\nA->>B: hi\n");
		Verify(() => svg.Contains("y=\"-25\">Hello</text>", StringComparison.Ordinal));
	}

	[Test]
	public void TheColourThemesPaintEachParticipantFromThePalette()
	{
		var svg = Render("%%{init: {\"theme\": \"redux-color\"}}%%\nsequenceDiagram\nA->>+B: hi\nB->>-A: ok\n");
		Verify(() => svg.Contains("class=\"actor actor-top stm-style-", StringComparison.Ordinal));
		Verify(() => svg.Contains("class=\"activation0 stm-style-", StringComparison.Ordinal));
		Verify(() => svg.Contains(".stm-style-0{stroke:", StringComparison.Ordinal));

		var plain = Render("%%{init: {\"theme\": \"default\"}}%%\nsequenceDiagram\nA->>B: hi\n");
		Verify(() => !plain.Contains("stm-style", StringComparison.Ordinal));
	}

	[Test]
	public void EveryThemeAndLookWritesItsStylesheetThroughTheTypedRules()
	{
		foreach (var theme in ThemeNames)
		{
			foreach (var look in new[] { "classic", "neo" })
			{
				var svg = Render($"%%{{init: {{\"theme\": \"{theme}\", \"look\": \"{look}\"}}}}%%\nsequenceDiagram\nactor A\nparticipant B@{{ \"type\": \"queue\" }}\nA->>B: hi\nNote over A: n\nloop x\nB-->>A: y\nend\n");
				Verify(svg.Contains("#mermaid .messageLine1{", StringComparison.Ordinal), $"{theme} {look}");
				Verify(svg.Contains("#mermaid [id$=\"-arrowhead\"] path{", StringComparison.Ordinal), $"{theme} {look}");
				Verify(look == "neo" == svg.Contains("id=\"mermaid-drop-shadow\"", StringComparison.Ordinal), $"{theme} {look}");
			}
		}
	}

	[Test]
	public void AnEndsWithAttributeSelectorIsAllowed()
	{
		Verify(() => CssSelector.Parse("[id$=\"-arrowhead\"] path").Text == "[id$=\"-arrowhead\"] path");
		Throws<ArgumentException>(() => CssSelector.Parse("[id^=\"x\"]"));
		Throws<ArgumentException>(() => CssSelector.Parse("[id$=x]"));
	}

	private static string Render(string source, string? look = null)
	{
		var text = look is null ? source : $"%%{{init: {{\"look\": \"{look}\", \"theme\": \"default\"}}}}%%\n" + source;
		return SvgDocuments.Checked(Mermaid.Default.RenderSvg(text));
	}

	private static int Count(string text, string part)
	{
		var count = 0;
		for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
		{
			count++;
		}

		return count;
	}

	/// <summary>The start tag of the first element holding <paramref name="part"/>.</summary>
	private static string Element(string svg, string part)
	{
		var at = svg.IndexOf(part, StringComparison.Ordinal);
		var start = svg.LastIndexOf('<', at);
		return svg[start..(svg.IndexOf('>', at) + 1)];
	}
}
