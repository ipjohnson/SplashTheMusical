using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.State;

namespace SplashTheMusical.Tests.Generation;

/// <summary>The documents of <see cref="StateGenerator"/> hold only what mermaid.js 12.3.0 accepts.</summary>
public class StateGeneratorTests
{
	[Test]
	public void EachNoteIsOnAStateAnEarlierStatementCreates()
	{
		var notes = 0;
		StateGenerator.Document.Sample(document =>
		{
			var created = new HashSet<string>(StringComparer.Ordinal);
			Walk(document.Statements);

			void Walk(List<StateStatement> statements)
			{
				foreach (var statement in statements)
				{
					switch (statement)
					{
						case StateNote note:
							created.Should().Contain(note.Id, "mermaid.js refuses a note on a state no earlier statement creates");
							notes++;
							break;
						case StateDeclaration declaration:
							created.Add(declaration.State.Id);
							break;
						case StateTransition transition:
							created.Add(transition.From.Id);
							created.Add(transition.To.Id);
							break;
						case StateBlock { Kind: StateKind.Default, Description: null, Statements: null }:
							// A plain state id gives the state no type.
							break;
						case StateBlock block:
							if (block.Statements is { } inner)
							{
								Walk(inner);
							}

							created.Add(block.Id);
							break;
					}
				}
			}
		}, iter: 1_000, threads: 1);

		notes.Should().BePositive();
	}
}
