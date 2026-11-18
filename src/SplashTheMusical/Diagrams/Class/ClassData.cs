using SplashTheMusical.Diagrams.Flowchart;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// What mermaid.js's <c>ClassDB</c> holds after it reads a <see cref="ClassDocument"/>: the port of the actions
/// <c>classDiagram.jison</c> calls, in the order it calls them. Text is the db's text with the source's entity codes,
/// before <c>sanitizeText</c>, which the label code applies when it draws it.
/// </summary>
internal sealed class ClassData
{
	private const string DomIdPrefix = "classId-";

	private readonly Stack<string> _namespaceStack = new();

	private ClassData()
	{
	}

	/// <summary>The classes by id, in the order the db adds them.</summary>
	public OrderedDictionary<string, ClassDataNode> Classes { get; } = new(StringComparer.Ordinal);

	public List<ClassDataRelation> Relations { get; } = [];

	public List<ClassDataNote> Notes { get; } = [];

	public List<ClassDataInterface> Interfaces { get; } = [];

	/// <summary>The namespaces by their dotted id, in the order the db adds them.</summary>
	public OrderedDictionary<string, ClassDataNamespace> Namespaces { get; } = new(StringComparer.Ordinal);

	/// <summary>The <c>classDef</c> classes by name.</summary>
	public OrderedDictionary<string, ClassDataStyleClass> StyleClasses { get; } = new(StringComparer.Ordinal);

	public string Direction { get; private set; } = "TB";

	/// <summary>The message of the error the db throws while it reads the document, or <c>null</c>.</summary>
	public string? Error { get; private set; }

	/// <summary>
	/// Whether <see cref="From"/> may find an error: the document has an annotation statement, which needs its class,
	/// a method mermaid.js cannot read, or a blank class name or label, which mermaid.js fails to draw.
	/// </summary>
	public static bool MayFail(ClassDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		return MayFail(document.Statements);

		static bool MayFail(List<ClassStatement> statements)
		{
			foreach (var statement in statements)
			{
				switch (statement)
				{
					case ClassAnnotation:
						return true;
					case ClassMemberStatement { Member: { } member } when ClassMemberText.IsUnreadableMethod(member):
						return true;
					case ClassDefinition definition when IsBlank(definition.Label) || IsBlank(definition.Name):
						return true;
					case ClassRelation relation when IsBlank(relation.From) || IsBlank(relation.To):
						return true;
					case ClassMemberStatement member when IsBlank(member.ClassName):
						return true;
					case ClassDefinition { Members: { } members }:
						foreach (var member in members)
						{
							if (ClassMemberText.IsUnreadableMethod(member))
							{
								return true;
							}
						}

						break;
					case ClassNamespace space when MayFail(space.Statements):
						return true;
				}
			}

			return false;
		}
	}

	public static ClassData From(ClassDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var data = new ClassData();
		if (data.Read(document.Statements))
		{
			foreach (var node in data.Classes.Values)
			{
				if (ClassLexer.Trim(node.Text).IsEmpty)
				{
					data.Error = $"The class {node.Id} has a blank name, which mermaid.js cannot draw.";
					break;
				}
			}
		}

		return data;
	}

	private static bool IsBlank(string? text) => text is not null && ClassLexer.Trim(text).IsEmpty;

	private bool Read(List<ClassStatement> statements)
	{
		foreach (var statement in statements)
		{
			if (!Read(statement))
			{
				return false;
			}
		}

		return true;
	}

	private bool Read(ClassStatement statement)
	{
		switch (statement)
		{
			case ClassRelation relation:
				AddRelation(relation);
				return true;
			case ClassDefinition definition:
				return ReadDefinition(definition);
			case ClassMemberStatement { Member: { } member } memberStatement:
				return AddMember(memberStatement.ClassName, member);
			case ClassMemberStatement:
				return true;
			case ClassAnnotation annotation:
				if (!Classes.TryGetValue(SplitClassName(annotation.ClassName).Name, out var annotated))
				{
					Error = "Cannot read properties of undefined (reading 'annotations')";
					return false;
				}

				annotated.Annotations.Add(annotation.Annotation);
				return true;
			case ClassNamespace space:
				return ReadNamespace(space);
			case ClassNote note:
				AddNote(note);
				return true;
			case ClassInteraction interaction:
				ReadInteraction(interaction);
				return true;
			case ClassStyle style:
				if (Classes.TryGetValue(style.Id, out var styled))
				{
					foreach (var s in style.Styles)
					{
						styled.Styles.AddRange(s.Split(','));
					}
				}

				return true;
			case ClassStyleDefinition definition:
				DefineClass(definition);
				return true;
			case ClassCssClass cssClass:
				SetCssClass(cssClass.Ids, cssClass.ClassName);
				return true;
			case ClassDirection direction:
				Direction = direction.Direction;
				return true;
			default:
				return true;
		}
	}

	private bool ReadDefinition(ClassDefinition definition)
	{
		AddClass(definition.Name);
		if (definition.Label is { } label)
		{
			var node = Classes[SplitClassName(definition.Name).Name];
			node.Label = label;
			node.Text = node.Type.Length > 0 ? $"{label}<{node.Type}>" : label;
		}

		if (definition.CssClass is { } cssClass)
		{
			SetCssClass(definition.Name, cssClass);
		}

		if (definition.Annotation is { } annotation)
		{
			Classes[SplitClassName(definition.Name).Name].Annotations.Add(annotation);
		}

		if (definition.Members is { } members)
		{
			foreach (var member in members)
			{
				if (!AddMember(definition.Name, member))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool ReadNamespace(ClassNamespace space)
	{
		var id = AddNamespace(space.Name, space.Label);
		var classes = new List<string>();
		var notes = new List<string>();
		foreach (var statement in space.Statements)
		{
			var noteCount = Notes.Count;
			if (!Read(statement))
			{
				return false;
			}

			switch (statement)
			{
				case ClassDefinition definition:
					classes.Add(definition.Name);
					break;
				case ClassNote when Notes.Count > noteCount:
					notes.Add(Notes[^1].Id);
					break;
			}
		}

		var target = Namespaces[id];
		foreach (var name in classes)
		{
			var node = Classes[SplitClassName(name).Name];
			node.Parent = id;
			target.Classes.Add(node.Id);
		}

		foreach (var noteId in notes)
		{
			var note = Notes.Find(n => n.Id == noteId)!;
			note.Parent = id;
			target.Notes.Add(noteId);
		}

		_ = _namespaceStack.Pop();
		return true;
	}

	/// <summary><c>splitClassNameAndType</c>: the name before the first <c>~</c> and the generic type after it.</summary>
	public static (string Name, string Type) SplitClassName(string id)
	{
		var tilde = id.IndexOf('~', StringComparison.Ordinal);
		if (tilde <= 0)
		{
			return (id, "");
		}

		var next = id.IndexOf('~', tilde + 1);
		return (id[..tilde], next < 0 ? id[(tilde + 1)..] : id[(tilde + 1)..next]);
	}

	private void AddClass(string id)
	{
		var (name, type) = SplitClassName(id);
		if (Classes.ContainsKey(name))
		{
			return;
		}

		Classes[name] = new ClassDataNode(name)
		{
			Type = type,
			Label = name,
			Text = type.Length > 0 ? $"{name}&lt;{type}&gt;" : name,
		};
	}

	private bool AddMember(string className, string member)
	{
		AddClass(className);
		var node = Classes[SplitClassName(className).Name];
		var text = ClassMemberText.Trim(member);
		if (text.StartsWith("<<", StringComparison.Ordinal) && text.EndsWith(">>", StringComparison.Ordinal) && text.Length >= 4)
		{
			node.Annotations.Add(text[2..^2]);
		}
		else if (text.IndexOf(')', StringComparison.Ordinal) > 0)
		{
			if (ClassMemberText.Method(text) is not { } method)
			{
				Error = "Cannot read properties of undefined (reading 'startsWith')";
				return false;
			}

			node.Methods.Add(method);
		}
		else if (text.Length > 0)
		{
			node.Attributes.Add(ClassMemberText.Attribute(text));
		}

		return true;
	}

	private void AddRelation(ClassRelation relation)
	{
		var id1 = relation.From;
		var id2 = relation.To;
		var type1 = relation.FromEnd;
		var type2 = relation.ToEnd;
		if (type1 == ClassRelationEnd.Lollipop && type2 == ClassRelationEnd.None)
		{
			AddClass(id2);
			Interfaces.Add(new ClassDataInterface($"interface{Interfaces.Count}", id1, id2));
			id1 = Interfaces[^1].Id;
		}
		else if (type2 == ClassRelationEnd.Lollipop && type1 == ClassRelationEnd.None)
		{
			AddClass(id1);
			Interfaces.Add(new ClassDataInterface($"interface{Interfaces.Count}", id2, id1));
			id2 = Interfaces[^1].Id;
		}
		else
		{
			AddClass(id1);
			AddClass(id2);
		}

		Relations.Add(new ClassDataRelation(SplitClassName(id1).Name, SplitClassName(id2).Name)
		{
			Title1 = relation.FromCardinality is { } from ? ClassMemberText.Trim(from) : "none",
			Title2 = relation.ToCardinality is { } to ? ClassMemberText.Trim(to) : "none",
			Type1 = type1,
			Type2 = type2,
			Line = relation.Line,
			Title = relation.Label,
		});
	}

	private void AddNote(ClassNote note)
	{
		var index = Notes.Count;
		Notes.Add(new ClassDataNote($"note{index}", note.Text, index) { Class = note.ClassName });
	}

	private void ReadInteraction(ClassInteraction interaction)
	{
		switch (interaction.Kind)
		{
			case ClassInteractionKind.Callback or ClassInteractionKind.ClickCall:
				foreach (var id in interaction.ClassName.Split(','))
				{
					if (Classes.TryGetValue(SplitClassName(id).Name, out var node))
					{
						node.HaveCallback = true;
					}
				}

				SetCssClass(interaction.ClassName, "clickable");
				break;
			default:
				foreach (var id in interaction.ClassName.Split(','))
				{
					if (Classes.TryGetValue(SplitClassName(DigitPrefixed(id)).Name, out var node))
					{
						node.Link = interaction.Target;
						node.LinkTarget = interaction.LinkTarget ?? "_blank";
					}
				}

				SetCssClass(interaction.ClassName, "clickable");
				break;
		}

		if (interaction.Tooltip is { } tooltip)
		{
			foreach (var id in interaction.ClassName.Split(','))
			{
				if (Classes.TryGetValue(SplitClassName(id).Name, out var node))
				{
					node.Tooltip = tooltip;
				}
			}
		}
	}

	private static string DigitPrefixed(string id) => id.Length > 0 && char.IsAsciiDigit(id[0]) ? DomIdPrefix + id : id;

	private void SetCssClass(string ids, string className)
	{
		foreach (var id in ids.Split(','))
		{
			if (Classes.TryGetValue(SplitClassName(DigitPrefixed(id)).Name, out var node))
			{
				node.CssClasses += " " + className;
			}
		}
	}

	private void DefineClass(ClassStyleDefinition definition)
	{
		foreach (var id in definition.Names)
		{
			if (!StyleClasses.TryGetValue(id, out var styleClass))
			{
				StyleClasses[id] = styleClass = new ClassDataStyleClass(id);
			}

			foreach (var style in definition.Styles)
			{
				if (style.Contains("color", StringComparison.Ordinal))
				{
					var at = style.IndexOf("fill", StringComparison.Ordinal);
					styleClass.TextStyles.Add(at < 0 ? style : string.Concat(style.AsSpan(0, at), "bgFill", style.AsSpan(at + 4)));
				}

				styleClass.Styles.Add(style);
			}

			foreach (var node in Classes.Values)
			{
				if (node.CssClasses.Contains(id, StringComparison.Ordinal))
				{
					foreach (var style in definition.Styles)
					{
						node.Styles.AddRange(style.Split(','));
					}
				}
			}
		}
	}

	/// <summary><c>addNamespace</c>: the dotted id inside the namespaces around it, with each namespace of its path.</summary>
	private string AddNamespace(string id, string? label)
	{
		var qualified = _namespaceStack.TryPeek(out var prefix) ? $"{prefix}.{id}" : id;
		_namespaceStack.Push(qualified);
		if (Namespaces.TryGetValue(qualified, out var existing))
		{
			existing.Explicit = true;
			if (!string.IsNullOrEmpty(label))
			{
				existing.Label = label;
			}

			return qualified;
		}

		var parts = qualified.Split('.');
		var ancestor = parts[0];
		string? parent = null;
		for (var i = 0; i < parts.Length; i++)
		{
			if (i > 0)
			{
				ancestor = $"{ancestor}.{parts[i]}";
			}

			var leaf = i == parts.Length - 1;
			if (!Namespaces.TryGetValue(ancestor, out var node))
			{
				Namespaces[ancestor] = node = new ClassDataNamespace(ancestor, leaf && !string.IsNullOrEmpty(label) ? label : parts[i])
				{
					Parent = parent,
					Explicit = leaf,
				};
			}
			else if (leaf)
			{
				node.Explicit = true;
			}

			if (parent is not null && Namespaces.TryGetValue(parent, out var parentNode))
			{
				if (!parentNode.Children.Contains(ancestor))
				{
					parentNode.Children.Add(ancestor);
				}

				node.Parent ??= parent;
			}

			parent = ancestor;
		}

		return qualified;
	}
}

/// <summary>A class of <see cref="ClassData"/>, the db's <c>ClassNode</c>.</summary>
internal sealed class ClassDataNode(string id)
{
	public string Id { get; } = id;

	/// <summary>The generic type of <c>name~type~</c>, or empty.</summary>
	public string Type { get; init; } = "";

	public string Label { get; set; } = "";

	/// <summary>The text drawn as the class's name: its label and <c>&amp;lt;type&amp;gt;</c>.</summary>
	public string Text { get; set; } = "";

	public string CssClasses { get; set; } = "default";

	public List<ClassMemberText> Methods { get; } = [];

	/// <summary>The db's <c>members</c>.</summary>
	public List<ClassMemberText> Attributes { get; } = [];

	public List<string> Annotations { get; } = [];

	public List<string> Styles { get; } = [];

	/// <summary>The dotted id of the namespace that holds the class, or <c>null</c>.</summary>
	public string? Parent { get; set; }

	public string? Link { get; set; }

	public string? LinkTarget { get; set; }

	public bool HaveCallback { get; set; }

	public string? Tooltip { get; set; }
}

/// <summary>A relation of <see cref="ClassData"/>, by the ids of the classes or interfaces at its ends.</summary>
internal sealed class ClassDataRelation(string id1, string id2)
{
	public string Id1 { get; } = id1;

	public string Id2 { get; } = id2;

	/// <summary>The cardinality at the start, trimmed, or <c>none</c>.</summary>
	public string Title1 { get; init; } = "none";

	/// <summary>The cardinality at the end, trimmed, or <c>none</c>.</summary>
	public string Title2 { get; init; } = "none";

	public ClassRelationEnd Type1 { get; init; }

	public ClassRelationEnd Type2 { get; init; }

	public ClassLineStyle Line { get; init; }

	public string? Title { get; init; }
}

/// <summary>A note of <see cref="ClassData"/>.</summary>
internal sealed class ClassDataNote(string id, string text, int index)
{
	public string Id { get; } = id;

	public string Text { get; } = text;

	public int Index { get; } = index;

	/// <summary>The class name of <c>note for</c> as written, or <c>null</c>.</summary>
	public string? Class { get; init; }

	public string? Parent { get; set; }
}

/// <summary>The interface a lollipop relation adds, named <c>interfaceN</c>, with the label written at its end.</summary>
internal sealed record ClassDataInterface(string Id, string Label, string ClassId);

/// <summary>A namespace of <see cref="ClassData"/>, by its dotted id.</summary>
internal sealed class ClassDataNamespace(string id, string label)
{
	public string Id { get; } = id;

	public string Label { get; set; } = label;

	public string? Parent { get; set; }

	/// <summary>Whether a statement names it, rather than only a dotted name inside it.</summary>
	public bool Explicit { get; set; }

	public List<string> Children { get; } = [];

	public List<string> Classes { get; } = [];

	public List<string> Notes { get; } = [];
}

/// <summary>A <c>classDef</c> class.</summary>
internal sealed class ClassDataStyleClass(string id)
{
	public string Id { get; } = id;

	public List<string> Styles { get; } = [];

	public List<string> TextStyles { get; } = [];
}
