using SplashTheMusical.Config;
using SplashTheMusical.Diagrams;
using SplashTheMusical.Diagrams.Sequence;
using SplashTheMusical.Icons;
using SplashTheMusical.Text;

namespace SplashTheMusical.Tests.Diagrams;

/// <summary>
/// The sequence layout of participants and messages. The expected numbers are mermaid.js 12.3.0's, from the boxes of
/// the corpus cases named in each test.
/// </summary>
public class SequenceLayoutTests
{
	private const double Tolerance = 1e-9;

	/// <summary><c>docs-sequenceDiagram-001</c>: a long message widens the margin between the two participants.</summary>
	[Test]
	public void PlacesParticipantsMessagesAndTheCanvas()
	{
		var layout = Layout("""
			sequenceDiagram
			    Alice->>John: Hello John, how are you?
			    John-->>Alice: Great!
			    Alice-)John: See you later!
			""");

		Verify(() => layout.Look == "neo");
		Verify(layout.Actors.Select(a => (a.Name, a.X, a.Width, a.Height)).SequenceEqual([("Alice", 0.0, 150.0, 73.0), ("John", 251.0, 150.0, 73.0)]));
		Verify(layout.Actors.All(a => a.Bottom is { } bottom && Near(bottom.Shape.Y, 225)));
		Verify(layout.Messages.Select(m => (m.StartX, m.StopX, m.LineY)).SequenceEqual([(76.0, 319.0, 117.0), (325.0, 82.0, 161.0), (76.0, 319.0, 205.0)]));
		Verify(layout.Messages[0].Text.Single() is { X: 198, Y: 88 });
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 501, 349));
	}

	/// <summary><c>parser-sequencediagram-100</c>: an empty message adds no height, and its text is a zero width space.</summary>
	[Test]
	public void AnEmptyMessageTakesNoHeight()
	{
		var layout = Layout("sequenceDiagram\nAlice->>Bob:\nBob->>Alice:Got it!\n");

		Verify(layout.Messages.Select(m => m.LineY).SequenceEqual([83.0, 127.0]));
		var empty = layout.Messages[0].Text.Single().Box;
		Verify(Near(empty.Width, 0) && Near(empty.Height, 17));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 450, 271));
	}

	/// <summary>
	/// <c>docs-sequenceDiagram-013</c>: under <c>neo</c> the icon types sit on the lifeline with their labels below
	/// them, and a database has no box at the bottom.
	/// </summary>
	[Test]
	public void DrawsIconParticipantsOnTheBandModel()
	{
		var layout = Layout("""
			sequenceDiagram
			    participant API@{ "type": "boundary" } as Public API
			    actor DB@{ "type": "database" } as User Database
			    participant Svc@{ "type": "control" } as Auth Service
			    API->>Svc: Authenticate
			    Svc->>DB: Query user
			    DB-->>Svc: User data
			    Svc-->>API: Token
			""");

		var boundary = layout.Actors[0].Top.Bounds;
		Verify(Near(boundary.X, 20) && Near(boundary.Y, 0) && Near(boundary.Height, 67));
		Verify(Near(layout.Actors[1].Top.Bounds.Width, 106.71875));
		Verify(Near(layout.Actors[2].X, 400));
		Verify(Near(layout.Actors[0].Bottom!.Bounds.Y, 275));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 650, 393));
	}

	/// <summary><c>parser-sequencediagram-111</c>: a self message loops right of the lifeline, and its text centers on it.</summary>
	[Test]
	public void ASelfMessageLoopsBesideTheLifeline()
	{
		var layout = Layout("sequenceDiagram\n participant C@{ \"type\" : \"control\" }\nC->C: test\n");

		var message = layout.Messages.Single();
		Verify(Near(message.Line.X, 76) && Near(message.Line.Y, 115.381) && Near(message.Line.Width, 45) && Near(message.Line.Height, 23.238));
		var text = message.Text.Single().Box;
		Verify(Near(text.X + (text.Width / 2), 76));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 251, 291));
	}

	[Test]
	public void AutonumberMovesTheLineStartPastTheNumber()
	{
		var plain = Layout("sequenceDiagram\nA->>B: one\n").Messages.Single();
		var numbered = Layout("sequenceDiagram\nautonumber 10 5\nA->>B: one\nA->>B: two\n").Messages;

		Verify(Near(numbered[0].Line.X, plain.Line.X + 6));
		Verify(numbered.Select(m => m.Number).SequenceEqual([10.0, 15.0]));
		Verify(() => plain.Number == null);
	}

	/// <summary><c>mirrorActors: false</c> draws no participants at the bottom, and the canvas ends below the last message.</summary>
	[Test]
	public void MirrorActorsOffLeavesTheBottomOut()
	{
		var layout = Layout("---\nconfig:\n  sequence:\n    mirrorActors: false\n---\nsequenceDiagram\nA->>B: one\n");

		Verify(layout.Actors.All(a => a.Bottom is null));
		Verify(Near(layout.ViewBox!.Value.Height, 73 + 44 + 20 + 30));
	}

	[Test]
	public void ABoxHoldsItsParticipantsAndPushesThemDown()
	{
		var layout = Layout("""
			sequenceDiagram
			  box Aqua Group
			    participant A
			    participant B
			  end
			  participant C
			  A->>B: hi
			""");

		var box = layout.Boxes.Single();
		Verify(() => box.Name == "Group");
		Verify(layout.Actors.Select(a => a.StartY).All(y => y > 10));
		Verify(box.Rect.X < layout.Actors[0].X && box.Rect.Right > layout.Actors[1].X + layout.Actors[1].Width);
		Verify(box.Rect.Right < layout.Actors[2].X);
	}

	[Test]
	public void AWrappedMessageBreaksIntoLinesThatPushTheLineDown()
	{
		var single = Layout("sequenceDiagram\nA->>B: one two three four five six seven eight nine ten eleven twelve\n").Messages.Single();
		var wrapped = Layout("sequenceDiagram\nA->>B: wrap: one two three four five six seven eight nine ten eleven twelve\n").Messages.Single();

		Verify(() => single.Text.Count == 1);
		Verify(() => wrapped.Text.Count > 1);
		Verify(Near(wrapped.LineY - single.LineY, 17 * (wrapped.Text.Count - 1)));
	}

	/// <summary><c>parser-sequencediagram-071</c>: the canvas grows to hold the tallest <c>links</c> menu.</summary>
	[Test]
	public void LinksMenusGrowTheCanvas()
	{
		var layout = Layout("""
			sequenceDiagram
			participant a as Alice
			participant b as Bob
			participant c as Charlie
			links a: { "Repo": "https://repo.contoso.com/", "Dashboard": "https://dashboard.contoso.com/" }
			links b: { "Dashboard": "https://dashboard.contoso.com/" }
			links a: { "On-Call": "https://oncall.contoso.com/?svc=alice" }
			link a: Endpoint @ https://alice.contoso.com
			link a: Swagger @ https://swagger.contoso.com
			link a: Tests @ https://tests.contoso.com/?svc=alice@contoso.com
			""");

		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 650, 314));
	}

	[Test]
	public void ClassicLookUsesTheConfiguredRowHeight()
	{
		var layout = Layout("---\nconfig:\n  look: classic\n---\nsequenceDiagram\nactor A\nA->>B: hi\n");

		Verify(() => layout.Look == "classic");
		Verify(Near(layout.Messages.Single().LineY, 65 + 44));
		Verify(Near(layout.Actors[0].Top.Shape.Height, 65));
	}

	[Test]
	public void TheDbLinksEachActorToTheOnesBeforeAndAfterIt()
	{
		var db = SequenceDb.From((SequenceDocument)new Mermaid().Parse("sequenceDiagram\nparticipant A\nparticipant B\nA->>C: hi\nlinks B: {\"2\": \"x\", \"b\": \"y\", \"1\": \"z\"}\n"));

		Verify(db.Actors.Select(a => (a.Name, a.PrevActor, a.NextActor)).SequenceEqual([("A", (string?)null, (string?)"B"), ("B", "A", "C"), ("C", "B", null)]));
		Verify(db.Actor("B")!.Links.SequenceEqual(["1", "2", "b"]));
	}

	/// <summary>The self loop's curve reaches past both its ends, as the 23.238 high box of <c>parser-sequencediagram-111</c> shows.</summary>
	[Test]
	public void TheCubicBoundsHoldTheCurveBetweenItsEnds()
	{
		var box = SequenceLayoutStep.CubicBounds(0, 0, 60, -10, 60, 30, 0, 20);
		Verify(Near(box.X, 0, Tolerance) && Near(box.Width, 45, Tolerance));
		Verify(Near(box.Y, -1.619, 1e-3) && Near(box.Bottom, 21.619, 1e-3));
	}

	private static bool Near(double actual, double expected, double tolerance = 0.01) => Math.Abs(actual - expected) <= tolerance;

	private static SequenceLayout Layout(string source)
	{
		var document = (SequenceDocument)new Mermaid().Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new SequenceModule().Layout(document, context);
	}
}
