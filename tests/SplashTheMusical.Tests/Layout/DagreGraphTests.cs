using AwesomeAssertions;
using SplashTheMusical.Layout.Dagre;

namespace SplashTheMusical.Tests.Layout;

/// <summary>The graphlib port's iteration order, which follows JavaScript's object key order.</summary>
public class DagreGraphTests
{
	[Test]
	[Arguments("0", 0L)]
	[Arguments("17", 17L)]
	[Arguments("4294967294", 4294967294L)]
	[Arguments("4294967295", -1L)]
	[Arguments("017", -1L)]
	[Arguments("-1", -1L)]
	[Arguments("1.5", -1L)]
	[Arguments("", -1L)]
	[Arguments("a1", -1L)]
	public void ReadsArrayIndexKeys(string key, long expected) => JsKeys.ArrayIndex(key).Should().Be(expected);

	[Test]
	public void ListsArrayIndexNamesFirstInNumericOrder()
	{
		var g = new Graph<object, object>();
		foreach (var name in new[] { "b", "10", "a", "2", "02" })
		{
			g.SetNode(name, new object());
		}

		Names(g, g.Nodes()).Should().Equal("2", "10", "b", "a", "02");
	}

	[Test]
	public void MovesANodeAddedAgainToTheEnd()
	{
		var g = new Graph<object, object>();
		foreach (var name in new[] { "a", "b", "c" })
		{
			g.SetNode(name, new object());
		}

		g.RemoveNode(g.NodeHandle("a"));
		g.SetNode("a", new object());

		Names(g, g.Nodes()).Should().Equal("b", "c", "a");
	}

	[Test]
	public void OrientsUndirectedEdgesByName()
	{
		var g = new Graph<object, object>(directed: false);
		var a = g.SetNode("a", new object());
		var b = g.SetNode("b", new object());
		var c = g.SetNode("c", new object());
		var one = g.SetNode("1", new object());

		var e = g.SetEdge(b, a, new object());
		g.SetEdge(b, c, new object());
		g.SetEdge(b, one, new object());

		g.EdgeV(e).Should().Be(a);
		g.FindEdge(a, b).Should().Be(e);
		Names(g, g.Neighbors(b)).Should().Equal("1", "a", "c");
	}

	[Test]
	public void AddsAParentListedAfterItsChildWithTheChild()
	{
		var g = DagreLayout.BuildLayoutGraph(new DagreInput(
			new(),
			[new("a", "C", 1, 1), new("b", null, 1, 1), new("C", null, 0, 0)],
			[]));

		Names(g, g.Nodes()).Should().Equal("a", "C", "b");
		Names(g, g.Children()).Should().Equal("b", "C");
		Names(g, g.Children(g.NodeHandle("C"))).Should().Equal("a");
	}

	[Test]
	public void KeepsTheOrderOfManyKeysAcrossRemovals()
	{
		var order = new KeyOrder();
		for (var key = 0; key < 40; key++)
		{
			order.Add(key, -1);
		}

		for (var key = 0; key < 40; key += 2)
		{
			order.Remove(key);
		}

		order.Add(0, -1);
		order.Add(1, -1).Should().BeFalse();

		var expected = Enumerable.Range(0, 40).Where(key => key % 2 == 1).Append(0).ToArray();
		order.ToArray().Should().Equal(expected);
		order.First().Should().Be(1);
	}

	private static IEnumerable<string> Names<TNode, TEdge>(Graph<TNode, TEdge> g, int[] nodes)
		where TNode : class
		where TEdge : class => nodes.Select(g.Name);
}
