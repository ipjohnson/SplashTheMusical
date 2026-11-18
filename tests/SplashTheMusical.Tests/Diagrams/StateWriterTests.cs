using AwesomeAssertions;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Diagrams;

public class StateWriterTests
{
	[Test]
	public void WritesOneStatementPerLineWithCompositeStatesIndented()
	{
		const string Source = """
			stateDiagram-v2
			accTitle: Title
			direction LR
			[*] --> Idle
			Idle --> Busy:::hot : start
			state "Busy state" as Busy {
			[*] --> A
			--
			[*] --> B
			}
			state f <<fork>>
			state c [[choice]]
			note left of Idle : waiting
			note "floating" as N
			classDef hot fill:#f00
			class Idle,Busy hot
			style Idle fill:#0f0
			hide empty description
			scale 200 width
			click Idle "https://example.com" "tip"
			click Busy href "https://example.org"
			""";

		Write(Source).Should().Be("""
			stateDiagram-v2
			  accTitle: Title
			  direction LR
			  [*] --> Idle
			  Idle --> Busy:::hot : start
			  state "Busy state" as Busy {
			    [*] --> A
			    --
			    [*] --> B
			  }
			  state f <<fork>>
			  state c <<choice>>
			  note left of Idle : waiting
			  note "floating" as N
			  classDef hot fill:#f00
			  class Idle,Busy hot
			  style Idle fill:#0f0
			  hide empty description
			  scale 200 width
			  click Idle "https://example.com" "tip"
			  click Busy href "https://example.org"

			""".ReplaceLineEndings("\n"));
	}

	[Test]
	[Arguments("note right of A\n  a: b\nend note")]
	[Arguments("note right of A\n  a; b\nend note")]
	[Arguments("note right of A\n  one\n  two\nend note")]
	public void ANoteOneLineCannotHoldIsWrittenUpToEndNote(string note)
	{
		var document = Parse("stateDiagram-v2\nA\n" + note + "\n");
		var written = Mermaid.Default.Write(document);

		written.Should().Contain("\n  end note\n");
		StateDump.Lines(Parse(written)).Should().Equal(StateDump.Lines(document));
	}

	[Test]
	[Arguments("stateDiagram\nA --> B")]
	[Arguments("stateDiagram-v2\nstate A {\nstate B {\nC --> D : go\n}\n}\nnote right of A : n")]
	[Arguments("stateDiagram-v2\nA : say #quot;hi#quot; #9829;")]
	[Arguments("stateDiagram-v2\nstate \"desc\" as A:more\nA --> B")]
	[Arguments("stateDiagram-v2\nstate j [[join]]\n[*] --> j\nj --> [*]")]
	public void WrittenSourceParsesToTheSameDocument(string source)
	{
		var document = Parse(source);

		StateDump.Lines(Parse(Mermaid.Default.Write(document))).Should().Equal(StateDump.Lines(document));
	}

	[Test]
	public void WritesWhatTheBuilderBuilds()
	{
		var document = new StateBuilder("LR")
			.Transition("[*]", "Idle")
			.Transition("Idle", "Busy", "start")
			.Composite("Busy", "Working", busy => busy.Transition("[*]", "Step").Divider().State("Other", "waits"))
			.Fork("f")
			.Join("j")
			.Choice("c")
			.Note("Idle", "rests", StateNotePosition.LeftOf)
			.ClassDef("hot", "fill:#f00", "color:white")
			.Class("hot", "Idle", "Busy")
			.Style("Idle", "stroke:#333")
			.Build();

		Mermaid.Default.Write(document).Should().Be("""
			stateDiagram-v2
			  direction LR
			  [*] --> Idle
			  Idle --> Busy : start
			  state "Working" as Busy {
			    [*] --> Step
			    --
			    Other : waits
			  }
			  state f <<fork>>
			  state j <<join>>
			  state c <<choice>>
			  note left of Idle : rests
			  classDef hot fill:#f00,color:white
			  class Idle,Busy hot
			  style Idle stroke:#333

			""".ReplaceLineEndings("\n"));
	}

	[Test]
	public void TheBuilderRefusesAnUnknownDirection() =>
		FluentActions.Invoking(() => new StateBuilder("TD")).Should().Throw<ArgumentException>();

	private static StateDocument Parse(string source) => (StateDocument)Mermaid.Default.Parse(source);

	private static string Write(string source) => Mermaid.Default.Write(Parse(source));
}
