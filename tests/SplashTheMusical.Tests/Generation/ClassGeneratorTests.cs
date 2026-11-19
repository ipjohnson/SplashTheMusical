using AwesomeAssertions;
using CsCheck;
using SplashTheMusical.Diagrams.Class;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// The coverage of <see cref="ClassGenerator"/>: every statement, every relation end and line, every interaction, the
/// forms of a class statement, nested namespaces and hard text.
/// </summary>
public class ClassGeneratorTests
{
	private static readonly Lazy<List<ClassDocument>> Sample = new(() =>
	{
		var documents = new List<ClassDocument>();
		ClassGenerator.Document.Sample(d => { lock (documents) { documents.Add(d); } }, iter: 5_000);
		return documents;
	});

	private static readonly Lazy<List<ClassStatement>> Statements = new(() => [.. Sample.Value.SelectMany(d => All(d.Statements))]);

	[Test]
	public void TheDocumentsUseEveryStatement() =>
		Statements.Value.Select(s => s.GetType()).Distinct().Should().BeEquivalentTo(
			typeof(ClassStatement).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(ClassStatement)) && !t.IsAbstract));

	[Test]
	public void TheRelationsUseEveryEndAndLine()
	{
		var relations = Statements.Value.OfType<ClassRelation>().ToList();
		relations.Select(r => r.FromEnd).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClassRelationEnd>());
		relations.Select(r => r.ToEnd).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClassRelationEnd>());
		relations.Select(r => r.Line).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClassLineStyle>());
		relations.Should().Contain(r => r.FromCardinality != null && r.ToCardinality != null && r.Label != null);
	}

	[Test]
	public void TheInteractionsUseEveryKind()
	{
		var interactions = Statements.Value.OfType<ClassInteraction>().ToList();
		interactions.Select(i => i.Kind).Distinct().Should().BeEquivalentTo(Enum.GetValues<ClassInteractionKind>());
		interactions.Should().Contain(i => i.Tooltip != null && i.LinkTarget != null);
		interactions.Should().Contain(i => i.Arguments != null);
	}

	[Test]
	public void TheClassesUseEveryFormAndName()
	{
		var definitions = Statements.Value.OfType<ClassDefinition>().ToList();
		definitions.Should().Contain(d => d.Label != null && d.CssClass != null && d.Members != null && d.Members.Count == 0);
		definitions.Should().Contain(d => d.Annotation != null && d.Members != null && d.Members.Count > 1);
		definitions.Should().Contain(d => d.Members != null && d.Members.Any(m => m.Contains(')', StringComparison.Ordinal)));
		definitions.Select(d => d.Name).Should().Contain(["List~int~", "my class", "Ünïcödé"]);
	}

	[Test]
	public void TheNamespacesNest() =>
		Statements.Value.OfType<ClassNamespace>().Should().Contain(
			n => n.Statements.OfType<ClassNamespace>().Any(m => m.Statements.OfType<ClassNamespace>().Any()),
			"a namespace holds a namespace that holds a namespace");

	[Test]
	public void TheTextHoldsHardCharacters()
	{
		var quoted = Statements.Value.OfType<ClassNote>().Select(n => n.Text)
			.Concat(Statements.Value.OfType<ClassDefinition>().Select(d => d.Label).OfType<string>()).ToList();
		foreach (var text in new[] { "'", "[", "]", "{", "}", "#quot;", "&amp;", "<br>", "\n", "日本語", "😀" })
		{
			quoted.Should().Contain(label => label.Contains(text, StringComparison.Ordinal), $"a quoted text holds {text}");
		}

		Sample.Value.Select(d => d.Keyword).Distinct().Should().BeEquivalentTo(["classDiagram", "classDiagram-v2"]);
		Sample.Value.Should().Contain(d => d.AccessibilityTitle != null && d.AccessibilityDescription != null && d.AccessibilityDescription.Contains('\n'));
	}

	private static IEnumerable<ClassStatement> All(List<ClassStatement> statements) =>
		statements.SelectMany(s => s is ClassNamespace n ? [s, .. All(n.Statements)] : new[] { s });
}
