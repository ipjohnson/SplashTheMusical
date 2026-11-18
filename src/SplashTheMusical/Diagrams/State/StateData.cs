namespace SplashTheMusical.Diagrams.State;

/// <summary>
/// The nodes and edges of a state diagram as mermaid.js's <c>StateDB.getData</c> gives them: the port of
/// <c>stateDb.ts</c>'s <c>docTranslator</c> and <c>extract</c>, and of <c>dataFetcher.ts</c>, at mermaid 12.3.0.
/// </summary>
/// <remarks>
/// <c>docTranslator</c> names each <c>[*]</c> after the document it is in, <c>&lt;id&gt;_start</c> or
/// <c>&lt;id&gt;_end</c>, and splits a composite state at its <c>--</c> dividers into regions named
/// <c>divider-id-N</c> in source order. mermaid.js names the last region with <c>generateId</c>, which counts across the
/// page, so this port names it <c>id-stm-N</c>, with N one past the dividers before it. <c>dataFetcher</c> keeps one
/// node per id in the order ids are first met, and a later statement about the same id changes that node.
/// </remarks>
internal sealed class StateData
{
	public const string Root = "root";
	private const string NoteSuffix = "----note";
	private const string ParentSuffix = "----parent";

	private const string StateClass = "statediagram-state";
	private const string ClusterClass = "statediagram-cluster";
	private const string ClusterAltClass = "statediagram-cluster-alt";
	private const string NoteClass = "statediagram-note";

	private readonly Dictionary<string, DbNode> _nodeDb = new(StringComparer.Ordinal);
	private readonly Dictionary<string, int> _nodeIndex = new(StringComparer.Ordinal);
	private readonly Dictionary<string, DbState> _states = new(StringComparer.Ordinal);
	private readonly OrderedDictionary<string, StateStyleClass> _classes = new(StringComparer.Ordinal);
	private readonly Dictionary<string, int?> _containerColorIndex = new(StringComparer.Ordinal);
	private readonly string? _look;
	private int _graphItemCount;
	private int _dividers;
	private int _generated;
	private int _nextColorIndex;

	private StateData(string? look) => _look = look;

	/// <summary>The nodes, in the order <c>dataFetcher</c> inserts them.</summary>
	public List<StateDataNode> Nodes { get; } = [];

	public List<StateDataEdge> Edges { get; } = [];

	/// <summary><c>getClasses</c>: the <c>classDef</c> classes of the top-level document, in the order they are first defined.</summary>
	public IEnumerable<StateStyleClass> Classes => _classes.Values;

	/// <summary><c>getDir</c> of the root document: the last direction statement at the top level, or <c>TB</c>.</summary>
	public string RootDirection { get; private set; } = "TB";

	/// <summary>The error <c>extract</c> throws, which refuses the diagram, or <c>null</c>.</summary>
	public string? Error { get; private set; }

	/// <param name="look">
	/// The <c>look</c> of the config the diagram is parsed with: the site config, the front matter and the directives.
	/// A transition ends in <c>arrow_barb_neo</c> when it is <c>neo</c>.
	/// </param>
	public static StateData From(StateDocument document, string? look)
	{
		ArgumentNullException.ThrowIfNull(document);
		var data = new StateData(look);
		var root = new Stmt { Kind = StmtKind.Root, Id = Root, Doc = data.Statements(document.Statements) };
		data.Translate(root, root, true);
		data.Extract(document.Statements);
		data.RootDirection = Direction(root);
		data.Fetch(null, root, true);
		data.Labels();
		return data;
	}

	/// <summary>
	/// The part of <c>extract</c> that reads the top-level statements into the root document's states and classes:
	/// the classes a transition's <c>:::</c> gives its ends, <c>classDef</c>, <c>style</c> and <c>class</c>. Statements
	/// inside a composite state do not reach them.
	/// </summary>
	private void Extract(List<StateStatement> statements)
	{
		foreach (var statement in statements)
		{
			switch (statement)
			{
				case StateDeclaration declaration:
					_ = AddState(declaration.State.Id);
					break;
				case StateBlock block:
					_ = AddState(block.Id);
					break;
				case StateNote note:
					_ = AddState(note.Id);
					break;
				case StateTransition transition:
					AddRelationEnd(transition.From);
					AddRelationEnd(transition.To);
					break;
				case StateClassDefinition definition:
					AddStyleClass(definition.Name.Trim(), definition.Styles);
					break;
				case StateStyle style:
					foreach (var id in style.Ids.Trim().Split(','))
					{
						var state = AddState(id);
						state.Styles.Clear();
						foreach (var declaration in style.Styles.Split(','))
						{
							state.Styles.Add(declaration.Replace(";", "", StringComparison.Ordinal).Trim());
						}
					}

					break;
				case StateClassAssignment assignment:
					foreach (var id in assignment.Ids.Trim().Split(','))
					{
						AddState(id).Classes.Add(assignment.ClassName.Trim());
					}

					break;
			}
		}
	}

	/// <summary><c>addRelationObjs</c>: a <c>[*]</c> end is a numbered start or end state no node is named after.</summary>
	private void AddRelationEnd(StateRef end)
	{
		if (end.Id == StateRef.StartOrEnd)
		{
			return;
		}

		var state = AddState(end.Id);
		if (end.ClassName is { } className)
		{
			state.Classes.Add(className.Trim());
		}
	}

	private DbState AddState(string id)
	{
		var trimmed = StateLexer.Trim(id).ToString();
		if (!_states.TryGetValue(trimmed, out var state))
		{
			_states[trimmed] = state = new DbState();
		}

		return state;
	}

	/// <summary>
	/// <c>addStyleClass</c>: each comma-separated declaration without its first <c>;</c>, and as a text style, with
	/// <c>fill</c> made <c>bgFill</c> and then <c>color</c> made <c>fill</c>, when it names <c>color</c>.
	/// </summary>
	private void AddStyleClass(string id, string attributes)
	{
		if (!_classes.TryGetValue(id, out var found))
		{
			_classes[id] = found = new StateStyleClass(id);
		}

		if (attributes.Length == 0)
		{
			return;
		}

		foreach (var attribute in attributes.Split(','))
		{
			var semicolon = attribute.IndexOf(';', StringComparison.Ordinal);
			var fixedAttribute = (semicolon < 0 ? attribute : attribute.Remove(semicolon, 1)).Trim();
			if (attribute.Contains("color", StringComparison.Ordinal))
			{
				found.TextStyles.Add(ReplaceFirst(ReplaceFirst(fixedAttribute, "fill", "bgFill"), "color", "fill"));
			}

			found.Styles.Add(fixedAttribute);
		}
	}

	private static string ReplaceFirst(string text, string find, string replacement)
	{
		var at = text.IndexOf(find, StringComparison.Ordinal);
		return at < 0 ? text : string.Concat(text.AsSpan(0, at), replacement, text.AsSpan(at + find.Length));
	}

	/// <summary>Whether the db throws for <paramref name="document"/>, which only a composite state with descriptions can make it do.</summary>
	public static bool MayFail(StateDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var composite = false;
		var description = false;
		Scan(document.Statements, ref composite, ref description);
		return composite && description;

		static void Scan(List<StateStatement> statements, ref bool composite, ref bool description)
		{
			foreach (var statement in statements)
			{
				switch (statement)
				{
					case StateDeclaration { Description: not null }:
						description = true;
						break;
					case StateBlock block:
						description |= block.Description is not null;
						if (block.Statements is { } children)
						{
							composite = true;
							Scan(children, ref composite, ref description);
						}

						break;
				}
			}
		}
	}

	/// <summary>The objects the grammar's actions build, with the divider ids <c>getDividerId</c> gives in source order.</summary>
	private List<Stmt> Statements(List<StateStatement> statements)
	{
		var result = new List<Stmt>(statements.Count);
		foreach (var statement in statements)
		{
			result.Add(statement switch
			{
				StateDeclaration declaration => State(declaration.State.Id, declaration.Description ?? ""),
				StateTransition transition => new Stmt
				{
					Kind = StmtKind.Relation,
					State1 = State(transition.From.Id, ""),
					State2 = State(transition.To.Id, ""),
					Description = transition.Label,
				},
				StateBlock block => Block(block),
				StateDivider => new Stmt { Kind = StmtKind.State, Id = $"divider-id-{++_dividers}", Type = "divider" },
				StateNote note => new Stmt
				{
					Kind = StmtKind.State,
					Id = StateLexer.Trim(note.Id).ToString(),
					Type = null,
					Note = note,
				},
				StateDirection direction => new Stmt { Kind = StmtKind.Direction, Id = direction.Direction },
				_ => new Stmt { Kind = StmtKind.Other },
			});
		}

		return result;
	}

	private static Stmt State(string id, string description) =>
		new() { Kind = StmtKind.State, Id = StateLexer.Trim(id).ToString(), Type = "default", Description = description };

	private Stmt Block(StateBlock block)
	{
		if (block.Kind != StateKind.Default)
		{
			var type = block.Kind switch
			{
				StateKind.Fork => "fork",
				StateKind.Join => "join",
				_ => "choice",
			};
			return new Stmt { Kind = StmtKind.State, Id = block.Id, Type = type };
		}

		if (block.Statements is { } children)
		{
			return new Stmt
			{
				Kind = StmtKind.State,
				Id = block.Id,
				Type = "default",
				Description = block.Description ?? "",
				Doc = Statements(children),
			};
		}

		if (block.Description is not { } description)
		{
			// COMPOSIT_STATE alone has no action, so its token's text goes into the document and the db skips it.
			return new Stmt { Kind = StmtKind.Other };
		}

		// STATE_DESCR AS ID: an id with a colon is split, and the text after it is a second description.
		var colon = block.Id.IndexOf(':', StringComparison.Ordinal);
		if (colon < 0)
		{
			return new Stmt { Kind = StmtKind.State, Id = block.Id, Type = "default", Description = StateLexer.Trim(description).ToString() };
		}

		var next = block.Id.IndexOf(':', colon + 1);
		return new Stmt
		{
			Kind = StmtKind.State,
			Id = block.Id[..colon],
			Type = "default",
			Descriptions = [StateLexer.Trim(description).ToString(), next < 0 ? block.Id[(colon + 1)..] : block.Id[(colon + 1)..next]],
		};
	}

	/// <summary><c>docTranslator</c>.</summary>
	private void Translate(Stmt parent, Stmt node, bool first)
	{
		if (node.Kind == StmtKind.Relation)
		{
			Translate(parent, node.State1!, true);
			Translate(parent, node.State2!, false);
			return;
		}

		if (node.Kind == StmtKind.State)
		{
			if (node.Id == StateRef.StartOrEnd)
			{
				node.Id = parent.Id + (first ? "_start" : "_end");
				node.Start = first;
			}
			else
			{
				node.Id = StateLexer.Trim(node.Id).ToString();
			}
		}

		if ((node.Kind != StmtKind.Root && node.Kind != StmtKind.State) || node.Doc is null)
		{
			return;
		}

		var doc = new List<Stmt>();
		var current = new List<Stmt>();
		foreach (var statement in node.Doc)
		{
			if (statement.Type == "divider")
			{
				var region = statement.Clone();
				region.Doc = Clone(current);
				doc.Add(region);
				current = [];
			}
			else
			{
				current.Add(statement);
			}
		}

		if (doc.Count > 0 && current.Count > 0)
		{
			_generated++;
			doc.Add(new Stmt { Kind = StmtKind.State, Id = $"id-stm-{_dividers + _generated}", Type = "divider", Doc = Clone(current) });
			node.Doc = doc;
		}

		foreach (var child in node.Doc)
		{
			Translate(node, child, true);
		}
	}

	private static List<Stmt> Clone(List<Stmt> statements) => [.. statements.Select(s => s.Clone())];

	/// <summary><c>dataFetcher</c>.</summary>
	private void Fetch(Stmt? parent, Stmt item, bool altFlag)
	{
		var itemId = item.Id;
		var dbState = _states.GetValueOrDefault(itemId);
		var classStr = dbState is null ? "" : string.Join(' ', dbState.Classes);
		IReadOnlyList<string> style = dbState is null ? [] : [.. dbState.Styles];
		var userStyled = classStr.Trim().Length > 0 || style.Count > 0;
		if (itemId != Root)
		{
			var shape = "rect";
			if (item.Start == true)
			{
				shape = "stateStart";
			}
			else if (item.Start == false)
			{
				shape = "stateEnd";
			}

			if (item.Type != "default")
			{
				shape = item.Type;
			}

			if (!_nodeDb.TryGetValue(itemId, out var node))
			{
				_nodeDb[itemId] = node = new DbNode { Shape = shape, Description = itemId, CssClasses = classStr + " " + StateClass, CssStyles = style };
			}

			if (item.Descriptions is not null || !string.IsNullOrEmpty(item.Description))
			{
				if (node.Descriptions is { } list)
				{
					node.Shape = "rectWithTitle";
					AddDescription(list, item);
				}
				else if (node.Description is { Length: > 0 } existing)
				{
					node.Shape = "rectWithTitle";
					node.Descriptions = existing == itemId ? [] : [existing];
					node.Description = null;
					AddDescription(node.Descriptions, item);
				}
				else
				{
					node.Shape = "rect";
					node.Description = item.Description;
					node.Descriptions = item.Descriptions is null ? null : [.. item.Descriptions];
				}
			}

			if (node.Descriptions is { Count: 1 } && node.Shape == "rectWithTitle")
			{
				node.Shape = node.Type == "group" ? "roundedWithTitle" : "rect";
			}

			if (node.Type is null && item.Doc is not null)
			{
				node.Type = "group";
				node.Direction = Direction(item);
				node.Shape = item.Type == "divider" ? "divider" : "roundedWithTitle";
				node.ColorIndex = ColorSlot(node.Shape, itemId, parent, userStyled);
				node.CssClasses = node.CssClasses + " " + ClusterClass + " " + (altFlag ? ClusterAltClass : "");
			}

			var count = _graphItemCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
			var data = new StateDataNode(itemId)
			{
				Shape = node.Shape,
				Label = node.Shape == "divider" ? [""] : node.Descriptions is { } descriptions ? [.. descriptions] : [node.Description ?? ""],
				IsGroup = node.Type == "group",
				Type = node.Type,
				Direction = node.Direction,
				ParentId = parent is not null && parent.Id != Root ? parent.Id : null,
				DomId = "state-" + itemId + "-" + count,
				CssClasses = node.CssClasses,
				CssStyles = node.CssStyles,
				ColorIndex = node.ColorIndex,
				Padding = 8,
			};

			if (item.Note is { } note)
			{
				var noteId = itemId + NoteSuffix + "-" + count;
				var groupId = itemId + ParentSuffix;
				_graphItemCount++;
				var position = note.Position == StateNotePosition.LeftOf ? "left of" : "right of";
				Insert(new StateDataNode(groupId)
				{
					Shape = "noteGroup",
					Label = [note.Text],
					IsGroup = true,
					Type = "group",
					DomId = "state-" + itemId + ParentSuffix + "-" + count,
					CssClasses = node.CssClasses,
					Padding = 16,
					Position = position,
				});
				Insert(new StateDataNode(noteId)
				{
					Shape = "note",
					Label = [StateLexer.Trim(note.Text).ToString()],
					ParentId = groupId,
					NoteOf = itemId,
					Type = "node",
					DomId = "state-" + itemId + NoteSuffix + "-" + count,
					CssClasses = NoteClass,
					Padding = null,
					Position = position,
				});
				Insert(data);
				var leftOf = note.Position == StateNotePosition.LeftOf;
				var from = leftOf ? noteId : itemId;
				var to = leftOf ? itemId : noteId;
				Edges.Add(new StateDataEdge(from + "-" + to, from, to, "") { IsNoteEdge = true });
			}
			else
			{
				Insert(data);
			}
		}

		if (item.Doc is { } doc)
		{
			foreach (var child in doc)
			{
				if (child.Kind == StmtKind.State)
				{
					Fetch(item, child, !altFlag);
				}
				else if (child.Kind == StmtKind.Relation)
				{
					Fetch(item, child.State1!, !altFlag);
					Fetch(item, child.State2!, !altFlag);
					var arrow = _look == "neo" ? "arrow_barb_neo" : "arrow_barb";
					Edges.Add(new StateDataEdge("edge" + _graphItemCount, child.State1!.Id, child.State2!.Id, arrow) { Label = child.Description ?? "" });
					_graphItemCount++;
				}
			}
		}
	}

	private static void AddDescription(List<string> list, Stmt item)
	{
		if (item.Descriptions is { } descriptions)
		{
			list.AddRange(descriptions);
		}
		else
		{
			list.Add(item.Description!);
		}
	}

	/// <summary><c>getDir</c>: the last direction statement of the item's own statements, or <c>TB</c>.</summary>
	private static string Direction(Stmt item)
	{
		var direction = "TB";
		foreach (var statement in item.Doc!)
		{
			if (statement.Kind == StmtKind.Direction)
			{
				direction = statement.Id;
			}
		}

		return direction;
	}

	/// <summary>
	/// <c>colorSlotFor</c>: a container takes the next palette slot, or none when the author styled it, and a
	/// concurrent region takes its composite's.
	/// </summary>
	private int? ColorSlot(string shape, string itemId, Stmt? parent, bool userStyled)
	{
		if (shape == "divider" && parent is not null && _containerColorIndex.TryGetValue(parent.Id, out var inherited))
		{
			_containerColorIndex[itemId] = inherited;
			return inherited;
		}

		var slot = _nextColorIndex++;
		int? effective = userStyled ? null : slot;
		_containerColorIndex[itemId] = effective;
		return effective;
	}

	/// <summary>
	/// <c>insertOrUpdateNode</c>: the node takes the styles of each <c>classDef</c> its classes name, and a node with a
	/// known id takes the new values, and keeps its parent when the new one has none.
	/// </summary>
	private void Insert(StateDataNode node)
	{
		if (node.Id.Length == 0 || node.Id is "</join></fork>" or "</choice>")
		{
			return;
		}

		if (node.CssClasses.Length > 0)
		{
			var compiled = new List<string>();
			foreach (var name in node.CssClasses.Split(' '))
			{
				if (_classes.TryGetValue(name, out var definition))
				{
					compiled.AddRange(definition.Styles);
				}
			}

			node.CssCompiledStyles = compiled;
		}

		if (_nodeIndex.TryGetValue(node.Id, out var index))
		{
			var existing = Nodes[index];
			node.ParentId ??= existing.ParentId;
			Nodes[index] = node;
			return;
		}

		_nodeIndex[node.Id] = Nodes.Count;
		Nodes.Add(node);
	}

	/// <summary>The end of <c>extract</c>: a node's first label is its label and the rest are its descriptions, which a group may not have.</summary>
	private void Labels()
	{
		foreach (var node in Nodes)
		{
			if (node.Label.Count > 1 && node.IsGroup)
			{
				Error ??= $"Group nodes can only have label. Remove the additional description for node [{node.Id}]";
			}
		}
	}

	private enum StmtKind
	{
		Root,
		State,
		Relation,
		Direction,
		Other,
	}

	/// <summary>A statement as the grammar's actions build it, which <c>docTranslator</c> changes in place.</summary>
	private sealed class Stmt
	{
		public StmtKind Kind;
		public string Id = "";
		public string? Type;
		public string? Description;
		public string[]? Descriptions;
		public List<Stmt>? Doc;
		public StateNote? Note;
		public bool? Start;
		public Stmt? State1;
		public Stmt? State2;

		/// <summary>The JSON copy <c>clone</c> makes.</summary>
		public Stmt Clone() => new()
		{
			Kind = Kind,
			Id = Id,
			Type = Type,
			Description = Description,
			Descriptions = Descriptions,
			Doc = Doc is null ? null : StateData.Clone(Doc),
			Note = Note,
			Start = Start,
			State1 = State1?.Clone(),
			State2 = State2?.Clone(),
		};
	}

	/// <summary>The entry of <c>dataFetcher</c>'s <c>nodeDb</c> for one id.</summary>
	private sealed class DbNode
	{
		public string? Shape;

		/// <summary>The description while it is one string, or <c>null</c> once it is a list.</summary>
		public string? Description;

		public List<string>? Descriptions;
		public string? Type;
		public string? Direction;
		public string CssClasses = "";
		public IReadOnlyList<string> CssStyles = [];
		public int? ColorIndex;
	}

	/// <summary>A state of the root document's <c>states</c>, with the classes and styles <c>extract</c> gives it.</summary>
	private sealed class DbState
	{
		public List<string> Classes { get; } = [];

		public List<string> Styles { get; } = [];
	}
}

/// <summary>A <c>classDef</c> of a state diagram: its styles, and the text styles of those that name <c>color</c>.</summary>
internal sealed class StateStyleClass(string id)
{
	public string Id { get; } = id;

	public List<string> Styles { get; } = [];

	public List<string> TextStyles { get; } = [];
}

/// <summary>A node of <see cref="StateData"/>.</summary>
internal sealed class StateDataNode(string id)
{
	public string Id { get; } = id;

	/// <summary>
	/// The shape name: <c>rect</c>, <c>rectWithTitle</c>, <c>stateStart</c>, <c>stateEnd</c>, <c>fork</c>, <c>join</c>,
	/// <c>choice</c>, <c>divider</c>, <c>roundedWithTitle</c>, <c>note</c> or <c>noteGroup</c>. <c>null</c> for a state a
	/// note adds.
	/// </summary>
	public string? Shape { get; init; }

	/// <summary>The label and then the descriptions. The label is the first.</summary>
	public IReadOnlyList<string> Label { get; init; } = [""];

	public bool IsGroup { get; init; }

	/// <summary><c>group</c>, <c>node</c> for a note, or <c>null</c>.</summary>
	public string? Type { get; init; }

	/// <summary>The direction of a group.</summary>
	public string? Direction { get; init; }

	public string? ParentId { get; set; }

	/// <summary>The id of the state a note node belongs to.</summary>
	public string? NoteOf { get; init; }

	/// <summary><c>stateDomId</c>: <c>state-&lt;id&gt;-N</c>, with <c>----note</c> or <c>----parent</c> before N for a note and its group.</summary>
	public string DomId { get; init; } = "";

	/// <summary><c>cssClasses</c>: the state's classes, then <c>statediagram-state</c>, and the cluster classes of a group.</summary>
	public string CssClasses { get; init; } = "";

	/// <summary><c>cssStyles</c>: the declarations of the state's <c>style</c> statement.</summary>
	public IReadOnlyList<string> CssStyles { get; init; } = [];

	/// <summary><c>cssCompiledStyles</c>: the styles of the <c>classDef</c> classes the node's classes name.</summary>
	public IReadOnlyList<string> CssCompiledStyles { get; set; } = [];

	/// <summary><c>colorIndex</c>: a container's palette slot, or <c>null</c>.</summary>
	public int? ColorIndex { get; init; }

	/// <summary><c>padding</c>, or <c>null</c> for a note, which takes <c>flowchart.padding</c>.</summary>
	public double? Padding { get; init; }

	/// <summary>A note's and its group's <c>position</c>: <c>left of</c> or <c>right of</c>.</summary>
	public string? Position { get; init; }
}

/// <summary>An edge of <see cref="StateData"/>.</summary>
internal sealed class StateDataEdge(string id, string start, string end, string arrowTypeEnd)
{
	public string Id { get; } = id;

	public string Start { get; } = start;

	public string End { get; } = end;

	/// <summary><c>arrow_barb</c>, <c>arrow_barb_neo</c>, or empty for a note's edge.</summary>
	public string ArrowTypeEnd { get; } = arrowTypeEnd;

	/// <summary>The label, or empty.</summary>
	public string Label { get; init; } = "";

	public bool IsNoteEdge { get; init; }
}
