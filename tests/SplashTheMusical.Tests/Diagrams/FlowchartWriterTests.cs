using SplashTheMusical.Tests.Generation;
using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Tests.Diagrams;

public class FlowchartWriterTests
{
	[Test]
	public void WritesNodesThenEdgesThenSubgraphsThenClasses()
	{
		const string Source = """
			flowchart LR
			  subgraph group [The group]
			    A[Start] -->|go| B{Is it?}
			  end
			  B -.-> C((Done)):::done
			  style A fill:#f9f
			  linkStyle 1 stroke:red
			  classDef done fill:#0f0,color:#fff
			""";

		var written = Mermaid.Default.Write(Mermaid.Default.Parse(Source));

		Verify(written == """
			flowchart LR
			    A[Start]
			    B{Is it?}
			    C((Done))
			    style A fill:#f9f
			    class C done
			    A -->|go| B
			    B -.-> C
			    linkStyle 1 stroke:red,fill:none
			    subgraph group[The group]
			        B
			        A
			    end
			    classDef done fill:#0f0,color:#fff

			""".ReplaceLineEndings("\n"));
	}

	[Test]
	[Arguments("flowchart TD\nA[Text] --> B(\"A string\") --> C[\"`**Markdown**`\"]")]
	[Arguments("flowchart TD\nA[\"line one\nline two\"]")]
	[Arguments("flowchart TD\nA[\"#quot;quoted#quot; #9829;\"]")]
	[Arguments("flowchart TD\nA(( )) --> B([stadium]) --> C[[sub]] --> D[(db)] --> E{{hex}} --> F>odd] --> G(((double)))")]
	[Arguments("flowchart TD\nA[/trap\\] --> B[\\inv/] --> C[/right/] --> D[\\left\\]")]
	[Arguments("flowchart TD\nA[|borders:lt|Label]")]
	[Arguments("flowchart TD\nA@{ shape: hex, label: 'He said \"hi\" [twice]' }")]
	[Arguments("flowchart TD\nA@{ label: \"a [b] | c\", labelType: \"text\" }")]
	[Arguments("flowchart TD\nA[Shown]@{ shape: hex }")]
	[Arguments("flowchart TD\nA@{ icon: \"fa:user\", form: square, label: \"User\", pos: t, h: 60 }\nB@{ icon: \"fa:car\" }")]
	[Arguments("flowchart TD\nA@{ img: \"https://example.com/a.png\", w: 60, constraint: on }")]
	[Arguments("flowchart TD\nA --- B ---- C ==> D -.-> E -..- F ~~~ G")]
	[Arguments("flowchart TD\nA <--> B x==x C o-.-o D --o E --x F")]
	[Arguments("flowchart TD\nA -- a|b --> B\nB == c(d) ==> C")]
	[Arguments("flowchart TD\nA o-- text --> B\nB --> C")]
	[Arguments("flowchart TD\nA e1@--> B\nA --> B\nA --> B\ne1@{ animate: true, animation: slow, curve: linear, step: 2 }")]
	[Arguments("flowchart TD\nA --> B\nlinkStyle default interpolate basis\nB --> C\nlinkStyle default stroke:blue\nlinkStyle 1 interpolate step stroke:red,fill:blue")]
	[Arguments("flowchart TD\nclick Z href \"https://example.com\" \"Before the node\"\nZ\nA\nB\nC\nclick A href \"https://example.com\" \"tip\" _blank\nclick B call cb(1, 2) \"t2\"\nclick C cb\nclick A call again()")]
	[Arguments("flowchart TD\nA --> B\nA:::c\nsubgraph A\nB\nend\nclass A d\nclass L_A_B_0 e")]
	[Arguments("flowchart TD\nsubgraph O\ndirection LR\nsubgraph I\nA\nend\nB\nend\nsubgraph one two\nC\nend\nsubgraph s [\"`**md**`\"]\nD\nend")]
	[Arguments("flowchart TD\nsubgraph S\nA\nend\nS@{ view: collapsed, n: 2, list: [a, 1] }")]
	[Arguments("flowchart TD\naccTitle: Title\naccDescr {\nOne\nTwo\n}\nsubgraph S\naccTitle: Title\nA\nend")]
	[Arguments("flowchart TD\nclassDef c font-family:a\\,b,color:red,fill:#fff\nstyle A stroke:#333,stroke-width:2px")]
	[Arguments("flowchart TD\nsubgraph S\nend\nsubgraph S\nsubgraph T\ny\nend\nend")]
	[Arguments("flowchart TD\nsubgraph S[\" \"]\nC\nend")]
	[Arguments("flowchart TD\nsubgraph S[\"`  `\"]\nC\nend")]
	[Arguments("flowchart TD\nsubgraph \"a ] b\"\nC\nend")]
	[Arguments("flowchart TD\nsubgraph S[ ]\nC\nend\nsubgraph \"a]b\"\nD\nend\nsubgraph \"`x ] y`\"\nsubgraph \"in ] ner\"\nE\nend\nend")]
	[Arguments("---\ntitle: A title\n---\n%%{init: {\"theme\": \"dark\"}}%%\ngraph RL\nA")]
	public void AWrittenFlowchartParsesBackTheSame(string source)
	{
		var document = (FlowchartDocument)Mermaid.Default.Parse(source);

		var written = Mermaid.Default.Write(document);

		Verify(Mermaid.Default.TryParse(written, out var again, out var diagnostics), $"{string.Join("; ", diagnostics)}\n{written}");
		SequenceAssert.Equal(DocumentDump.Lines(again!), DocumentDump.Lines(document).ToArray());
	}

	[Test]
	public void AnEdgeWhoseHalvesDisagreeIsWrittenWithDisagreeingHalves()
	{
		var document = (FlowchartDocument)Mermaid.Default.Parse("flowchart TD\nA o-- text --> B");

		Verify(document.Edges.Single().Type == FlowchartEdgeType.Invalid);
		Verify(Mermaid.Default.Write(document).EndsWith("    A x-- text --> B\n", StringComparison.Ordinal));
	}

	[Test]
	public void ALabelBracketsCannotHoldGoesInTheDataBlock()
	{
		var document = new FlowchartDocument { Nodes = { new FlowchartNode("A") { Label = "a ] b", Shape = "round" } } };

		Verify(Mermaid.Default.Write(document) == "flowchart TB\n    A(\"_\")@{ label: \"a ] b\", labelType: text }\n");
	}

	[Test]
	public void ATitleBracketsCannotHoldIsWrittenAsTheSubgraphsOnlyText()
	{
		var document = (FlowchartDocument)Mermaid.Default.Parse("flowchart TD\nsubgraph A\nend\nsubgraph \"a ] b\"\nC\nend");

		Verify(Mermaid.Default.Write(document).EndsWith("    subgraph A\n    end\n    subgraph \"a ] b\"\n        C\n    end\n", StringComparison.Ordinal));
	}

	[Test]
	public void ATitleNeitherBracketsNorTheBareFormReadBackIsRefused()
	{
		var document = new FlowchartDocument { Subgraphs = { new FlowchartSubgraph("S", "a ] b") } };

		var writing = () => Mermaid.Default.Write(document);

		Verify(Throws<ArgumentException>(() => writing()).Message.Contains("'S'", StringComparison.Ordinal));
	}

	[Test]
	public void ADataStringEscapesWhatTheLexerWouldRead()
	{
		var document = new FlowchartDocument { Nodes = { new FlowchartNode("A") { Label = "say \"hi\" 100%\nnow", LabelType = FlowchartLabelType.Markdown } } };

		var written = Mermaid.Default.Write(document);

		Verify(written == "flowchart TB\n    A@{ label: \"say \\x22hi\\x22 100\\x25\\nnow\", labelType: markdown }\n");
		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}

	// The cases below are ones the round trip property of RoundTripPropertyTests found.

	[Test]
	public void ATagALabelOpensIsEscapedSoItCannotReachTheNextStatements()
	{
		var document = new FlowchartDocument
		{
			Nodes =
			{
				new FlowchartNode("A") { Label = "<b>ok</b> <a~", Shape = "cyl" },
				new FlowchartNode("B") { Label = "=", LabelType = FlowchartLabelType.String, Shape = "cyl" },
			},
			Edges = { new FlowchartEdge("A", "B", "L_A_B_0") { Type = FlowchartEdgeType.DoublePoint } },
		};

		var written = Mermaid.Default.Write(document);

		Verify(written.Contains("label: \"<b>ok</b> \\x3Ca~\"", StringComparison.Ordinal));
		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}

	[Test]
	public void ALabelWithAnAtSignGoesInTheDataBlock()
	{
		var document = new FlowchartDocument { Nodes = { new FlowchartNode("A") { Label = ",@!", Shape = "odd" } } };

		var written = Mermaid.Default.Write(document);

		Verify(written == "flowchart TB\n    A>\"_\"]@{ label: \",@!\", labelType: text }\n");
		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}

	[Test]
	public void AStringLabelWithABraceBeforeALineBreakAndAPercentPairIsWrittenAsAString()
	{
		var document = new FlowchartDocument
		{
			Nodes = { new FlowchartNode("A"), new FlowchartNode("B") },
			Edges = { new FlowchartEdge("A", "B", "L_A_B_0") { Label = "a}\nb %% c", LabelType = FlowchartLabelType.String } },
		};

		var written = Mermaid.Default.Write(document);

		Verify(written.Contains("A -->|\"a}\nb %% c\"| B", StringComparison.Ordinal));
		SequenceAssert.Equal(DocumentDump.Lines(Mermaid.Default.Parse(written)), DocumentDump.Lines(document).ToArray());
	}
}
