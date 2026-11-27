using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.Block;

namespace SplashTheMusical.Tests.Generation;

/// <summary><see cref="BlockGenerator"/> reaches every shape, stroke, arrowhead, statement and composite header.</summary>
public class BlockGeneratorTests
{
	[Test]
	public void ReachesEveryPartOfTheModel()
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		BlockGenerator.Document.Sample(
			document =>
			{
				lock (seen)
				{
					Visit(document.Statements, seen);
				}
			},
			iter: 3_000);

		var expected = Enum.GetValues<BlockShape>().Select(s => $"shape {s}")
			.Concat(Enum.GetValues<BlockStroke>().Select(s => $"stroke {s}"))
			.Concat(Enum.GetValues<BlockArrowHead>().Select(h => $"start {h}"))
			.Concat(Enum.GetValues<BlockArrowHead>().Select(h => $"end {h}"))
			.Concat(["BlockNode", "BlockEdge", "BlockComposite", "BlockColumns", "BlockSpace", "BlockClassDefinition", "BlockClassAssignment", "BlockStyle"])
			.Concat(["header none", "header BlockNode", "header BlockEdge", "chain", "labelled link", "nested composite", "columns auto"]);
		expected.Should().BeSubsetOf(seen);
	}

	private static void Visit(List<BlockStatement> statements, HashSet<string> seen, int depth = 0)
	{
		foreach (var statement in statements)
		{
			_ = seen.Add(statement.GetType().Name);
			switch (statement)
			{
				case BlockNode node:
					_ = seen.Add($"shape {node.Shape}");
					break;
				case BlockEdge edge:
					foreach (var node in edge.Nodes)
					{
						_ = seen.Add($"shape {node.Shape}");
					}

					foreach (var link in edge.Links)
					{
						_ = seen.Add($"stroke {link.Stroke}");
						_ = seen.Add($"start {link.Start}");
						_ = seen.Add($"end {link.End}");
						if (link.Label is not null)
						{
							_ = seen.Add("labelled link");
						}
					}

					if (edge.Nodes.Count > 2)
					{
						_ = seen.Add("chain");
					}

					break;
				case BlockComposite composite:
					_ = seen.Add($"header {composite.Header?.GetType().Name ?? "none"}");
					if (depth > 0)
					{
						_ = seen.Add("nested composite");
					}

					Visit(composite.Statements, seen, depth + 1);
					break;
				case BlockColumns { Columns: null }:
					_ = seen.Add("columns auto");
					break;
			}
		}
	}
}
