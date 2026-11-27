using AwesomeAssertions;
using SplashTheMusical.Diagrams.Block;

namespace SplashTheMusical.Tests.Diagrams;

public class BlockWriterTests
{
	[Test]
	public void WritesOneStatementPerLineWithCompositesIndented()
	{
		var document = new BlockDocument
		{
			Statements =
			[
				new BlockColumns(3),
				new BlockNode("a") { Label = "A", Shape = BlockShape.Cylinder, Width = 2 },
				new BlockSpace(2),
				new BlockNode("arrow") { Label = "go", Shape = BlockShape.BlockArrow, Directions = ["right", "x"] },
				new BlockComposite([new BlockColumns(null), new BlockNode("b")]) { Header = new BlockNode("g") { Label = "G", Shape = BlockShape.Square } },
				new BlockComposite([new BlockNode("c") { Label = "c", Shape = BlockShape.Na }]),
				new BlockEdge(new BlockNode("a"), new BlockLink { Label = "calls", Start = BlockArrowHead.Cross, End = BlockArrowHead.Circle }, new BlockNode("b")),
				new BlockEdge([new BlockNode("a"), new BlockNode("b"), new BlockNode("c")], [new BlockLink { Stroke = BlockStroke.Thick }, new BlockLink { Stroke = BlockStroke.Dotted, End = BlockArrowHead.None }]),
				new BlockClassDefinition("blue", ["fill:#66f", "stroke:#333"]),
				new BlockClassAssignment(["a", " b"], "blue"),
				new BlockStyle("a", ["fill:#f9f", "stroke:#333"]),
			],
		};

		Mermaid.Default.Write(document).Should().Be("""
			block
			  columns 3
			  a[("A")]:2
			  space:2
			  arrow<["go"]>(right, x)
			  block:g["G"]
			    columns auto
			    b
			  end
			  block
			    c["c"/]
			  end
			  a -- "calls" x--o b
			  a ==> b -.- c
			  classDef blue fill:#66f,stroke:#333
			  class a, b blue
			  style a fill:#f9f,stroke:#333

			""".ReplaceLineEndings("\n"));
	}

	[Test]
	public void AddsTheSemicolonEncodeEntitiesTakesOut()
	{
		var document = new BlockDocument { Statements = [new BlockNode("A"), new BlockStyle("A", ["fill:#f9f;"])] };

		var written = Mermaid.Default.Write(document);

		written.Should().Contain("  style A fill:#f9f;;\n");
		Mermaid.Default.TryParse(written, out var again, out _).Should().BeTrue();
		((BlockStyle)((BlockDocument)again!).Statements[1]).Styles.Should().Equal("fill:#f9f;");
	}

	[Test]
	public void BuildsTheCommonCase()
	{
		var document = new BlockBuilder(columns: 3).Block("a", "Frontend").Space().Block("b", "Backend", BlockShape.Cylinder).Edge("a", "b", "calls").Build();

		Mermaid.Default.Write(document).Should().Be("block\n  columns 3\n  a[\"Frontend\"]\n  space\n  b[(\"Backend\")]\n  a -- \"calls\" --> b\n");
	}

	public static IEnumerable<Func<BlockDocument>> Unwritable()
	{
		yield return () => Document(new BlockNode("a") { Label = "say \"hi\"", Shape = BlockShape.Square });
		yield return () => Document(new BlockNode("a") { Label = "`md`", Shape = BlockShape.Square });
		yield return () => Document(new BlockNode("end"));
		yield return () => Document(new BlockNode("class"));
		yield return () => Document(new BlockNode("block.x"));
		yield return () => Document(new BlockNode("a b"));
		yield return () => Document(new BlockNode("a") { Shape = BlockShape.Round });
		yield return () => Document(new BlockNode("a") { Label = "a", Shape = BlockShape.Square, Directions = ["right"] });
		yield return () => Document(new BlockNode("a") { Label = "a", Shape = BlockShape.BlockArrow, Directions = ["right", " x"] });
		yield return () => Document(new BlockEdge(new BlockNode("a"), new BlockLink { Start = BlockArrowHead.Cross }, new BlockNode("b")));
		yield return () => Document(new BlockEdge(new BlockNode("a"), new BlockLink(), new BlockNode("b") { Width = 2 }));
		yield return () => Document(new BlockComposite([]));
		yield return () => Document(new BlockClassDefinition("DEFAULT", ["fill:red"]));
		yield return () => Document(new BlockStyle("a-b", ["fill:red"]));
		yield return () => Document(new BlockNode("a") { Label = "x\n%% comment", Shape = BlockShape.Square });
		yield return () => new BlockDocument { AccessibilityTitle = "t", Statements = [new BlockNode("a")] };
		yield return () => new BlockDocument();
	}

	[Test]
	[MethodDataSource(nameof(Unwritable))]
	public void RefusesWhatDoesNotReadBackAsItself(BlockDocument document)
	{
		var write = () => Mermaid.Default.Write(document);

		write.Should().Throw<ArgumentException>();
	}

	private static BlockDocument Document(BlockStatement statement) => new() { Statements = [statement] };
}
