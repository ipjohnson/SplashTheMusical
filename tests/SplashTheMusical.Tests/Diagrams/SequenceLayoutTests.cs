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

	/// <summary>
	/// <c>mermaider-spec-sequence-nested-blocks-starting-at-the-same-message</c>: each block grows by a box margin per
	/// block inside it, and a branch or a nested block moves the messages after it down.
	/// </summary>
	[Test]
	public void NestedBlocksAndTheirBranchesFrameTheirMessages()
	{
		var layout = Layout("""
			sequenceDiagram
			participant A
			participant B
			participant C
			loop Every minute
			par Ping B
			A->>B: ping
			and Ping C
			A->>C: ping
			end
			C-->>A: pong
			end
			alt Retry
			opt Same messages
			A->>B: again
			end
			else Give up
			A->>C: stop
			end
			""");

		Verify(layout.Blocks.Select(b => b.Kind).SequenceEqual(["par", "loop", "opt", "alt"]));
		Verify(layout.Blocks.Select(b => b.Text).SequenceEqual(["Ping B", "Every minute", "Same messages", "Retry"]));
		Verify(IsBox(layout.Blocks[0].Bounds, 64, 128, 422, 178));
		Verify(IsBox(layout.Blocks[1].Bounds, 54, 83, 442, 277));
		Verify(IsBox(layout.Blocks[2].Bounds, 64, 415, 222, 89));
		Verify(IsBox(layout.Blocks[3].Bounds, 54, 370, 432, 233));
		Verify(layout.Blocks[0].Sections.Count == 1 && layout.Blocks[3].Sections.Count == 1);
		Verify(layout.Blocks[0].Title.Single().Text == "[Ping B]");
		Verify(layout.Blocks[0].SectionTitles.Single().Text == "[Ping C]");
		Verify(layout.Messages.Select(m => m.LineY).SequenceEqual([207.0, 296.0, 350.0, 494.0, 593.0]));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 650, 747));
	}

	/// <summary><c>docs-sequenceDiagram-030</c>: a <c>critical</c> block with two <c>option</c> branches of self messages.</summary>
	[Test]
	public void ACriticalBlockHoldsItsOptions()
	{
		var layout = Layout("""
			sequenceDiagram
			    critical Establish a connection to the DB
			        Service-->DB: connect
			    option Network timeout
			        Service-->Service: Log error
			    option Credentials rejected
			        Service-->Service: Log different error
			    end
			""");

		var block = layout.Blocks.Single();
		Verify(() => block.Kind == "critical" && block.Sections.Count == 2);
		Verify(IsBox(block.Bounds, -9, 83, 295, 357));
		Verify(layout.Messages.Select(m => m.LineY).SequenceEqual([162.0, 251.0, 370.0]));
		Verify(() => layout.ViewBox == new SvgViewBox(-59, -10, 459, 584));
	}

	/// <summary><c>mermaider-spec-sequence-break-block</c>.</summary>
	[Test]
	public void ABreakBlockFramesItsMessage()
	{
		var layout = Layout("sequenceDiagram\nparticipant Client\nparticipant Server\nClient->>Server: Request\nbreak When rate limited\nServer-->>Client: 429 Too Many Requests\nend\nServer-->>Client: 200 OK\n");

		var block = layout.Blocks.Single();
		Verify(() => block.Kind == "break" && block.Text == "When rate limited");
		Verify(IsBox(block.Bounds, 64, 127, 264, 89));
		Verify(layout.Messages.Select(m => m.LineY).SequenceEqual([117.0, 206.0, 260.0]));
	}

	/// <summary>
	/// <c>mermaider-spec-sequence-rect-highlight</c>: a <c>rect</c> is a background, not a drawn block, and its margins
	/// move the messages in and after it.
	/// </summary>
	[Test]
	public void ARectIsABackgroundAroundItsMessages()
	{
		var layout = Layout("sequenceDiagram\nparticipant A\nparticipant B\nrect rgb(200, 220, 100)\nA->>B: Inside highlight\nB-->>A: Reply\nend\nA->>B: Outside highlight\n");

		Verify(() => layout.Blocks.Count == 0);
		var background = layout.Backgrounds.Single();
		Verify(() => background.Fill == "rgb(200, 220, 100)");
		Verify(IsBox(background.Rect, 64, 83, 222, 108));
		Verify(layout.Messages.Select(m => m.LineY).SequenceEqual([137.0, 181.0, 235.0]));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 450, 379));
	}

	/// <summary>
	/// Notes of each placement: <c>parser-sequencediagram-083</c> right of, <c>parser-sequencediagram-081</c> over one,
	/// <c>docs-sequenceDiagram-023</c> over two and <c>parser-sequencediagram-092</c> left of a participant.
	/// </summary>
	[Test]
	public void NotesSitByTheirParticipants()
	{
		var right = Layout("sequenceDiagram\nparticipant Alice\nNote right of Alice: Alice thinks\n");
		Verify(IsBox(right.Notes.Single().Bounds, 100, 83, 150, 37));
		Verify(() => right.ViewBox == new SvgViewBox(-50, -10, 350, 264));

		var over = Layout("sequenceDiagram\nparticipant Alice\nNote over Alice: Alice thinks\n");
		Verify(IsBox(over.Notes.Single().Bounds, 0, 83, 150, 37));
		Verify(() => over.ViewBox == new SvgViewBox(-50, -10, 250, 264));

		var two = Layout("sequenceDiagram\n    Alice->John: Hello John, how are you?\n    Note over Alice,John: A typical interaction\n");
		Verify(IsBox(two.Notes.Single().Bounds, 50, 127, 301, 37));
		Verify(() => two.Notes.Single().Text == "A typical interaction");

		var left = Layout("sequenceDiagram\nAlice->>Bob:wrap: Hello Bob, how are you? If you are not available right now, I can leave you a message. Please get back to me as soon as you can!\nNote left of Alice: Bob thinks\nBob->>Alice: Fine!\n");
		Verify(IsBox(left.Notes.Single().Bounds, -100, 195, 150, 37));
		Verify(left.Messages.Select(m => m.LineY).SequenceEqual([185.0, 276.0]));
		Verify(() => left.ViewBox == new SvgViewBox(-150, -10, 553, 420));
	}

	/// <summary>
	/// <c>docs-sequenceDiagram-021</c>: a second activation of John stacks half an activation width right of the first,
	/// the messages start and end at the outer edges, and each activation is drawn when it ends.
	/// </summary>
	[Test]
	public void StackedActivationsMoveTheMessageEnds()
	{
		var layout = Layout("""
			sequenceDiagram
			    Alice->>+John: Hello John, how are you?
			    Alice->>+John: John, can you hear me?
			    John-->>-Alice: Hi Alice, I can hear you!
			    John-->>-Alice: I feel great!
			""");

		Verify(layout.Messages.Select(m => (m.Line.X, m.Line.Right, m.LineY)).SequenceEqual([(76.0, 315.0, 117.0), (76.0, 315.0, 161.0), (82.0, 321.0, 205.0), (82.0, 321.0, 249.0)]));
		Verify(layout.Activations.Select(a => (a.Actor, a.Rect, a.Level)).SequenceEqual([
			("John", new SequenceRect(326, 163, 10, 42), 1),
			("John", new SequenceRect(321, 117, 10, 132), 0)]));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 501, 393));
	}

	/// <summary>An activation that ends right after it starts is drawn 18 high, and the bounds reach its bottom.</summary>
	[Test]
	public void AShortActivationIsDrawnEighteenHigh()
	{
		var layout = Layout("sequenceDiagram\nAlice->>John: hi\nactivate John\ndeactivate John\n");

		var activation = layout.Activations.Single();
		Verify(() => activation.Actor == "John");
		Verify(IsBox(activation.Rect, 270, 111, 10, 18));
	}

	/// <summary>
	/// <c>mermaider-spec-sequence-create-and-destroy-actors</c>: a created participant starts at the message that
	/// creates it, which ends at its box, and a destroyed one's bottom box sits at the message that destroys it.
	/// </summary>
	[Test]
	public void CreatedAndDestroyedParticipantsStartAndEndAtTheirMessages()
	{
		var layout = Layout("sequenceDiagram\nparticipant Alice\nAlice->>Bob: Hello\ncreate participant Carol\nBob->>Carol: Hi Carol\nCarol-->>Bob: Thanks\ndestroy Carol\nBob-xCarol: Goodbye\n");

		var carol = layout.Actors.Single(a => a.Name == "Carol");
		Verify(IsBox(carol.Top.Bounds, 475, 124.5, 150, 73));
		Verify(IsBox(carol.Bottom!.Shape, 475, 249, 150, 73));
		Verify(layout.Actors.Where(a => a.Name != "Carol").All(a => Near(a.Bottom!.Shape.Y, 342)));
		Verify(layout.Messages.Select(m => (m.Line.X, m.Line.Width, m.LineY)).SequenceEqual([(76.0, 192.0, 117.0), (276.0, 189.0, 161.0), (282.0, 267.0, 241.5), (276.0, 189.0, 285.5)]));
		Verify(() => layout.ViewBox == new SvgViewBox(-50, -10, 725, 466));
	}

	private static bool IsBox(SequenceRect rect, double x, double y, double width, double height) =>
		Near(rect.X, x) && Near(rect.Y, y) && Near(rect.Width, width) && Near(rect.Height, height);

	private static bool Near(double actual, double expected, double tolerance = 0.01) => Math.Abs(actual - expected) <= tolerance;

	private static SequenceLayout Layout(string source)
	{
		var document = (SequenceDocument)new Mermaid().Parse(source);
		var config = MermaidConfig.Resolve(null, document, document.DiagramType);
		var context = new DiagramRenderContext(config, "mermaid", TextMeasurer.Default, NoIcons.Instance, [], ResourceLimits.Unlimited);
		return new SequenceModule().Layout(document, context);
	}
}
