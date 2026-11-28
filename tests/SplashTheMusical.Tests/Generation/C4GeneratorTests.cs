using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.C4;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// <see cref="C4Generator"/> reaches every header and macro, nested boundaries, named and empty arguments, and each
/// form of the title and the description, and its documents hold only what mermaid.js 12.3.0 draws.
/// </summary>
public class C4GeneratorTests
{
	[Test]
	public void ReachesEveryHeaderMacroAndTextForm()
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		C4Generator.Document.Sample(document =>
		{
			seen.Add($"header {document.Kind}");
			var text = Mermaid.Default.Write(document);
			foreach (var form in new[] { "\n  title ", "\n  accTitle: ", "\n  accDescription ", "\n  accDescr: ", "\n  accDescr {\n" })
			{
				if (text.Contains(form, StringComparison.Ordinal))
				{
					seen.Add($"form {form.Trim()}");
				}
			}

			Walk(document.Statements, 0);
		}, iter: 2_000, threads: 1);

		var expected = new List<string>();
		expected.AddRange(Enum.GetValues<C4DiagramKind>().Select(k => $"header {k}"));
		expected.AddRange(Enum.GetValues<C4ElementKind>().Select(k => $"element {k}"));
		expected.AddRange(Enum.GetValues<C4BoundaryKind>().Select(k => $"boundary {k}"));
		expected.AddRange(Enum.GetValues<C4RelationKind>().Select(k => $"relation {k}"));
		expected.AddRange(Enum.GetValues<C4UpdateKind>().Select(k => $"update {k}"));
		expected.AddRange(["depth 2", "named", "empty", "line break", "entity code",
			"form title", "form accTitle:", "form accDescription", "form accDescr:", "form accDescr {"]);
		seen.Should().Contain(expected);

		void Walk(List<C4Statement> statements, int depth)
		{
			foreach (var statement in statements)
			{
				seen.Add(statement switch
				{
					C4Element element => $"element {element.Kind}",
					C4Boundary boundary => $"boundary {boundary.Kind}",
					C4Relation relation => $"relation {relation.Kind}",
					C4Update update => $"update {update.Kind}",
					_ => "other",
				});
				foreach (var argument in statement.Arguments)
				{
					seen.Add(argument.Name is not null ? "named" : argument.Value.Length == 0 ? "empty" : "positional");
					if (argument.Value.Contains('\n', StringComparison.Ordinal))
					{
						seen.Add("line break");
					}

					if (argument.Value.Contains("#quot;", StringComparison.Ordinal))
					{
						seen.Add("entity code");
					}
				}

				if (statement is C4Boundary { Statements: var inner })
				{
					seen.Add($"depth {depth + 1}");
					Walk(inner, depth + 1);
				}
			}
		}
	}

	[Test]
	public void EveryRelationEndIsAnAliasTheDiagramDefinesAndEveryBoundaryAliasIsItsOwn()
	{
		C4Generator.Document.Sample(document =>
		{
			var db = C4Db.From(document);
			db.RelWithUnknownEnd().Should().BeNull("mermaid.js fails to draw a relation whose end is no element or boundary");
			var aliases = new List<string>();
			Boundaries(document.Statements, aliases);
			aliases.Should().OnlyHaveUniqueItems("mermaid.js recurses for ever on a boundary inside one of its own alias");
		}, iter: 1_000, threads: 1);

		static void Boundaries(List<C4Statement> statements, List<string> aliases)
		{
			foreach (var boundary in statements.OfType<C4Boundary>())
			{
				aliases.Add(boundary.Arguments[0].Value);
				Boundaries(boundary.Statements, aliases);
			}
		}
	}

	// mermaid.js 12.3.0 refuses each of the inputs with "svg element not in render tree", and draws each output.
	[Test]
	[Arguments("<br>\n<br>", "<br><br>")]
	[Arguments("q<br> <br/>q", "q<br><br/>q")]
	[Arguments("<br>\t<br/>\n \n<br>", "<br><br/><br>")]
	[Arguments("q<br> q", "q<br> q")]
	[Arguments("q \n q", "q \n q")]
	public void WhitespaceBetweenTwoLineBreaksIsRemoved(string text, string expected) =>
		GenText.WithoutBlankBreaks(text).Should().Be(expected);

	[Test]
	public void NoRelationTextHasOnlyWhitespaceBetweenTwoLineBreaks()
	{
		C4Generator.Document.Sample(document =>
		{
			foreach (var relation in Relations(document.Statements))
			{
				foreach (var argument in relation.Arguments)
				{
					argument.Value.Should().Be(GenText.WithoutBlankBreaks(argument.Value), "mermaid.js fails to draw the relation");
				}
			}
		}, iter: 1_000, threads: 1);

		static IEnumerable<C4Relation> Relations(List<C4Statement> statements) =>
			statements.SelectMany(s => s switch
			{
				C4Relation relation => [relation],
				C4Boundary boundary => Relations(boundary.Statements),
				_ => Array.Empty<C4Relation>(),
			});
	}
}
