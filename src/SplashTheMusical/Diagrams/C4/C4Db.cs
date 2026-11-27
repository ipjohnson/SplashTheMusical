using System.Globalization;
using SplashTheMusical.Parsing;

namespace SplashTheMusical.Diagrams.C4;

/// <summary>
/// The port of <c>c4Db.ts</c>: the shapes, boundaries and relations that the actions of <c>c4Diagram.jison</c> give a
/// <see cref="C4Document"/>'s calls, run in source order. The db keeps each shape, boundary and relation as a JavaScript
/// object whose fields a named argument can set by any name, so each is a <see cref="C4DbItem"/> of named
/// <see cref="C4Value"/>s.
/// </summary>
internal sealed class C4Db
{
	/// <summary>The fields the db stores as <c>{ text }</c> when a named argument sets them.</summary>
	private static readonly HashSet<string> TextFields = new(["label", "descr", "techn", "type"], StringComparer.Ordinal);

	private readonly List<C4Value?> _boundaryParseStack = [C4Value.String("")];
	private C4Value _currentBoundaryParse = C4Value.String("global");
	private C4Value? _parentBoundaryParse = C4Value.String("");

	private C4Db(C4Document document)
	{
		Kind = document.Kind;
		Title = document.DiagramTitle ?? "";
		var global = new C4DbItem();
		global["alias"] = C4Value.String("global");
		global["label"] = C4Value.TextObject("global");
		global["type"] = C4Value.TextObject("global");
		global["parentBoundary"] = C4Value.String("");
		Boundaries.Add(global);
	}

	public C4DiagramKind Kind { get; }

	/// <summary>The db's <c>title</c>, empty when the diagram has none.</summary>
	public string Title { get; }

	/// <summary><c>c4ShapeArray</c>: the persons, systems, containers and components in the order the db added them.</summary>
	public List<C4DbItem> Shapes { get; } = [];

	/// <summary><c>boundaries</c>: the <c>global</c> boundary, then the boundaries and deployment nodes in the order the db added them.</summary>
	public List<C4DbItem> Boundaries { get; } = [];

	/// <summary><c>rels</c>, one per pair of ends, in the order the db added them.</summary>
	public List<C4DbItem> Rels { get; } = [];

	/// <summary><c>c4ShapeInRow</c>, 4 unless <c>UpdateLayoutConfig</c> sets it.</summary>
	public double ShapeInRow { get; private set; } = 4;

	/// <summary><c>c4BoundaryInRow</c>, 2 unless <c>UpdateLayoutConfig</c> sets it.</summary>
	public double BoundaryInRow { get; private set; } = 2;

	public static C4Db From(C4Document document)
	{
		ArgumentNullException.ThrowIfNull(document);
		var db = new C4Db(document);
		db.Run(document.Statements);
		return db;
	}

	/// <summary><c>getC4Shape</c>: the shape of the alias, or else the boundary, or <c>null</c>.</summary>
	public C4DbItem? Shape(C4Value alias) => Find(Shapes, alias) ?? Find(Boundaries, alias);

	/// <summary>
	/// The first relation whose end <c>getC4Shape</c> does not find, which <c>drawRels</c> throws on, or <c>null</c>.
	/// </summary>
	public C4DbItem? RelWithUnknownEnd()
	{
		foreach (var rel in Rels)
		{
			if (Shape(rel["from"] ?? default) is null || Shape(rel["to"] ?? default) is null)
			{
				return rel;
			}
		}

		return null;
	}

	private void Run(List<C4Statement> statements)
	{
		foreach (var statement in statements)
		{
			var args = Arguments(statement.Arguments);
			switch (statement)
			{
				case C4Element element:
					AddElement(element.Kind, args);
					break;
				case C4Boundary boundary:
					AddBoundary(boundary.Kind, args);
					Run(boundary.Statements);
					PopBoundaryParseStack();
					break;
				case C4Relation relation:
					if (relation.Kind == C4RelationKind.RelIndex && args.Count > 0)
					{
						args.RemoveAt(0);
					}

					AddRel(RelType(relation.Kind), args);
					break;
				case C4Update { Kind: C4UpdateKind.ElementStyle }:
					UpdateElStyle(args);
					break;
				case C4Update { Kind: C4UpdateKind.RelStyle }:
					UpdateRelStyle(args);
					break;
				case C4Update:
					UpdateLayoutConfig(args);
					break;
			}
		}
	}

	/// <summary>The grammar's <c>attributes</c>: a string for each positional argument and a one-entry object for a named one.</summary>
	private static List<C4Value?> Arguments(List<C4Argument> arguments)
	{
		var values = new List<C4Value?>(arguments.Count + 1);
		foreach (var argument in arguments)
		{
			values.Add(argument.Name is null ? C4Value.String(argument.Value) : C4Value.Object(argument));
		}

		return values;
	}

	private static C4Value? At(List<C4Value?> args, int index) => index < args.Count ? args[index] : null;

	private static string ShapeType(C4ElementKind kind) => kind switch
	{
		C4ElementKind.Person => "person",
		C4ElementKind.PersonExt => "external_person",
		C4ElementKind.System => "system",
		C4ElementKind.SystemDb => "system_db",
		C4ElementKind.SystemQueue => "system_queue",
		C4ElementKind.SystemExt => "external_system",
		C4ElementKind.SystemDbExt => "external_system_db",
		C4ElementKind.SystemQueueExt => "external_system_queue",
		C4ElementKind.Container => "container",
		C4ElementKind.ContainerDb => "container_db",
		C4ElementKind.ContainerQueue => "container_queue",
		C4ElementKind.ContainerExt => "external_container",
		C4ElementKind.ContainerDbExt => "external_container_db",
		C4ElementKind.ContainerQueueExt => "external_container_queue",
		C4ElementKind.Component => "component",
		C4ElementKind.ComponentDb => "component_db",
		C4ElementKind.ComponentQueue => "component_queue",
		C4ElementKind.ComponentExt => "external_component",
		C4ElementKind.ComponentDbExt => "external_component_db",
		_ => "external_component_queue",
	};

	private static string RelType(C4RelationKind kind) => kind switch
	{
		C4RelationKind.BiRel => "birel",
		C4RelationKind.RelUp or C4RelationKind.RelU => "rel_u",
		C4RelationKind.RelDown or C4RelationKind.RelD => "rel_d",
		C4RelationKind.RelLeft or C4RelationKind.RelL => "rel_l",
		C4RelationKind.RelRight or C4RelationKind.RelR => "rel_r",
		C4RelationKind.RelBack => "rel_b",
		_ => "rel",
	};

	/// <summary>
	/// <c>addPersonOrSystem(type, alias, label, descr, sprite, tags, link)</c>, and <c>addContainer</c> and
	/// <c>addComponent</c>, which take the technology before the description.
	/// </summary>
	private void AddElement(C4ElementKind kind, List<C4Value?> args)
	{
		var alias = At(args, 0) ?? default;
		var shape = Find(Shapes, alias);
		if (shape is null)
		{
			shape = new C4DbItem();
			shape["alias"] = alias;
			Shapes.Add(shape);
		}

		shape["label"] = C4Value.Wrap(At(args, 1) ?? C4Value.String(""));
		var withTechn = kind >= C4ElementKind.Container;
		var next = 2;
		if (withTechn)
		{
			SetText(shape, "techn", At(args, next++));
		}

		SetText(shape, "descr", At(args, next++));
		AssignAttribute(shape, "sprite", At(args, next++));
		AssignAttribute(shape, "tags", At(args, next++));
		AssignAttribute(shape, "link", At(args, next));
		shape["typeC4Shape"] = C4Value.TextObject(ShapeType(kind));
		shape["parentBoundary"] = _currentBoundaryParse;
	}

	/// <summary>
	/// <c>addPersonOrSystemBoundary(alias, label, type, tags, link)</c>, <c>addContainerBoundary</c> with the same
	/// arguments, and <c>addDeploymentNode(nodeType, alias, label, type, descr, sprite, tags, link)</c>, which assigns no
	/// sprite. The grammar inserts the type of <c>Enterprise_Boundary</c>, <c>System_Boundary</c> and
	/// <c>Container_Boundary</c> as the third argument with <c>splice(2, 0, …)</c>.
	/// </summary>
	private void AddBoundary(C4BoundaryKind kind, List<C4Value?> args)
	{
		var inserted = kind switch
		{
			C4BoundaryKind.EnterpriseBoundary => "ENTERPRISE",
			C4BoundaryKind.SystemBoundary => "SYSTEM",
			C4BoundaryKind.ContainerBoundary => "CONTAINER",
			_ => null,
		};
		if (inserted is not null)
		{
			args.Insert(Math.Min(2, args.Count), C4Value.String(inserted));
		}

		var alias = At(args, 0) ?? default;
		var boundary = Find(Boundaries, alias);
		if (boundary is null)
		{
			boundary = new C4DbItem();
			boundary["alias"] = alias;
			Boundaries.Add(boundary);
		}

		boundary["label"] = C4Value.Wrap(At(args, 1) ?? C4Value.String(""));
		var node = kind is C4BoundaryKind.DeploymentNode or C4BoundaryKind.Node or C4BoundaryKind.NodeL or C4BoundaryKind.NodeR;
		var type = At(args, 2);
		if (type is null)
		{
			boundary["type"] = C4Value.TextObject(kind == C4BoundaryKind.ContainerBoundary ? "container" : node ? "node" : "system");
		}
		else
		{
			SetNamedOr(boundary, "type", type.Value);
		}

		if (node)
		{
			SetText(boundary, "descr", At(args, 3));
			AssignAttribute(boundary, "tags", At(args, 5));
			AssignAttribute(boundary, "link", At(args, 6));
			boundary["nodeType"] = C4Value.String(kind switch
			{
				C4BoundaryKind.NodeL => "nodeL",
				C4BoundaryKind.NodeR => "nodeR",
				_ => "node",
			});
		}
		else
		{
			AssignAttribute(boundary, "tags", At(args, 3));
			AssignAttribute(boundary, "link", At(args, 4));
		}

		boundary["parentBoundary"] = _currentBoundaryParse;
		_parentBoundaryParse = _currentBoundaryParse;
		_currentBoundaryParse = alias;
		_boundaryParseStack.Add(_parentBoundaryParse);
	}

	private void PopBoundaryParseStack()
	{
		_currentBoundaryParse = _parentBoundaryParse ?? default;
		Pop();
		_parentBoundaryParse = Pop();
		_boundaryParseStack.Add(_parentBoundaryParse);
	}

	/// <summary>JavaScript's <c>pop</c>, which gives <c>undefined</c> on an empty array.</summary>
	private C4Value? Pop()
	{
		if (_boundaryParseStack.Count == 0)
		{
			return null;
		}

		var last = _boundaryParseStack[^1];
		_boundaryParseStack.RemoveAt(_boundaryParseStack.Count - 1);
		return last;
	}

	/// <summary><c>addRel(type, from, to, label, techn, descr, sprite, tags, link)</c>, which needs the first four.</summary>
	private void AddRel(string type, List<C4Value?> args)
	{
		if (At(args, 0) is not { } from || At(args, 1) is not { } to || At(args, 2) is not { } label)
		{
			return;
		}

		C4DbItem? rel = null;
		foreach (var old in Rels)
		{
			if (C4Value.Same(old["from"], from) && C4Value.Same(old["to"], to))
			{
				rel = old;
				break;
			}
		}

		if (rel is null)
		{
			rel = new C4DbItem();
			Rels.Add(rel);
		}

		rel["type"] = C4Value.String(type);
		rel["from"] = from;
		rel["to"] = to;
		rel["label"] = C4Value.Wrap(label);
		SetText(rel, "techn", At(args, 3));
		SetText(rel, "descr", At(args, 4));
		AssignAttribute(rel, "sprite", At(args, 5));
		AssignAttribute(rel, "tags", At(args, 6));
		AssignAttribute(rel, "link", At(args, 7));
	}

	private static readonly string[] ElementStyleFields =
		["bgColor", "fontColor", "borderColor", "shadowing", "shape", "sprite", "techn", "legendText", "legendSprite"];

	/// <summary>
	/// <c>updateElStyle(type, elementName, bgColor, fontColor, borderColor, shadowing, shape, sprite, techn, legendText,
	/// legendSprite)</c>, on the shape of the name or else the boundary. It stores every value as the plain string,
	/// <c>techn</c> too.
	/// </summary>
	private void UpdateElStyle(List<C4Value?> args)
	{
		var name = At(args, 0) ?? default;
		var item = Find(Shapes, name) ?? Find(Boundaries, name);
		if (item is null)
		{
			return;
		}

		for (var i = 0; i < ElementStyleFields.Length; i++)
		{
			if (At(args, i + 1) is { } value)
			{
				SetRaw(item, ElementStyleFields[i], value, number: false);
			}
		}
	}

	/// <summary><c>updateRelStyle(type, from, to, textColor, lineColor, offsetX, offsetY)</c>, which reads the offsets with <c>parseInt</c>.</summary>
	private void UpdateRelStyle(List<C4Value?> args)
	{
		var from = At(args, 0) ?? default;
		var to = At(args, 1) ?? default;
		C4DbItem? rel = null;
		foreach (var old in Rels)
		{
			if (C4Value.Same(old["from"], from) && C4Value.Same(old["to"], to))
			{
				rel = old;
				break;
			}
		}

		if (rel is null)
		{
			return;
		}

		if (At(args, 2) is { } textColor)
		{
			SetRaw(rel, "textColor", textColor, number: false);
		}

		if (At(args, 3) is { } lineColor)
		{
			SetRaw(rel, "lineColor", lineColor, number: false);
		}

		if (At(args, 4) is { } offsetX)
		{
			SetRaw(rel, "offsetX", offsetX, number: true);
		}

		if (At(args, 5) is { } offsetY)
		{
			SetRaw(rel, "offsetY", offsetY, number: true);
		}
	}

	/// <summary>
	/// <c>updateLayoutConfig(type, c4ShapeInRow, c4BoundaryInRow)</c>: a named argument gives its value whatever its
	/// name, and a value below 1, or none, leaves the setting alone.
	/// </summary>
	private void UpdateLayoutConfig(List<C4Value?> args)
	{
		var shapes = ParseInt(At(args, 0));
		var boundaries = ParseInt(At(args, 1));
		if (shapes >= 1)
		{
			ShapeInRow = shapes;
		}

		if (boundaries >= 1)
		{
			BoundaryInRow = boundaries;
		}
	}

	private static double ParseInt(C4Value? value) => value switch
	{
		null => double.NaN,
		{ Argument: { } argument } => JsParseInt(argument.Value),
		{ } v => JsParseInt(v.Text ?? ""),
	};

	/// <summary>
	/// A text field of a positional slot: <c>{ text: value }</c>, or, for a named argument, the field it names, wrapped
	/// when it is a text field. With no argument the field keeps its value or becomes <c>{ text: '' }</c>.
	/// </summary>
	private static void SetText(C4DbItem item, string field, C4Value? value)
	{
		if (value is null)
		{
			item[field] ??= C4Value.TextObject("");
		}
		else
		{
			SetNamedOr(item, field, value.Value);
		}
	}

	/// <summary><c>{ text: value }</c> in the field, or the field a named argument names.</summary>
	private static void SetNamedOr(C4DbItem item, string field, C4Value value)
	{
		if (value.Argument is { } argument)
		{
			item[argument.Name!] = TextFields.Contains(argument.Name!) ? C4Value.TextObject(argument.Value) : C4Value.String(argument.Value);
		}
		else
		{
			item[field] = C4Value.Wrap(value);
		}
	}

	/// <summary><c>assignAttributes</c>: the plain value in the field, or the field a named argument names.</summary>
	private static void AssignAttribute(C4DbItem item, string field, C4Value? value)
	{
		if (value is not { } v)
		{
			return;
		}

		if (v.Argument is { } argument)
		{
			item[argument.Name!] = TextFields.Contains(argument.Name!) ? C4Value.TextObject(argument.Value) : C4Value.String(argument.Value);
		}
		else
		{
			item[field] = v;
		}
	}

	/// <summary>The value in the field, or in the field a named argument names, as a string or through <c>parseInt</c>.</summary>
	private static void SetRaw(C4DbItem item, string field, C4Value value, bool number)
	{
		var name = value.Argument?.Name ?? field;
		var text = value.Argument?.Value ?? value.Text ?? "";
		item[name] = number ? C4Value.Number(JsParseInt(text)) : C4Value.String(text);
	}

	private static C4DbItem? Find(List<C4DbItem> items, C4Value alias)
	{
		foreach (var item in items)
		{
			if (C4Value.Same(item["alias"], alias))
			{
				return item;
			}
		}

		return null;
	}

	/// <summary>
	/// JavaScript's <c>parseInt</c> with no radix: the space at the start is skipped, a sign is read, <c>0x</c> reads hex,
	/// and the digits up to the first other character give the number, or <c>NaN</c> when there are none.
	/// </summary>
	internal static double JsParseInt(string text)
	{
		var at = 0;
		while (at < text.Length && PreambleParser.IsSpace(text[at]))
		{
			at++;
		}

		var sign = 1.0;
		if (at < text.Length && text[at] is '+' or '-')
		{
			sign = text[at] == '-' ? -1 : 1;
			at++;
		}

		if (at + 1 < text.Length && text[at] == '0' && text[at + 1] is 'x' or 'X')
		{
			at += 2;
			var start = at;
			var hex = 0.0;
			while (at < text.Length && char.IsAsciiHexDigit(text[at]))
			{
				hex = (hex * 16) + (char.IsAsciiDigit(text[at]) ? text[at] - '0' : (text[at] | 0x20) - 'a' + 10);
				at++;
			}

			return at == start ? double.NaN : sign * hex;
		}

		var digits = at;
		while (digits < text.Length && char.IsAsciiDigit(text[digits]))
		{
			digits++;
		}

		return digits == at ? double.NaN : sign * double.Parse(text.AsSpan(at, digits - at), NumberStyles.None, CultureInfo.InvariantCulture);
	}
}

/// <summary>A shape, boundary or relation of the db: a JavaScript object of named fields.</summary>
internal sealed class C4DbItem
{
	private readonly Dictionary<string, C4Value> _fields = new(StringComparer.Ordinal);

	/// <summary>The field, or <c>null</c> when it is <c>undefined</c>.</summary>
	public C4Value? this[string name]
	{
		get => _fields.TryGetValue(name, out var value) ? value : null;
		set
		{
			if (value is { } v)
			{
				_fields[name] = v;
			}
			else
			{
				_ = _fields.Remove(name);
			}
		}
	}

	/// <summary>The <c>text</c> of a <c>{ text }</c> field as JavaScript's <c>String</c> gives it, or <c>null</c>.</summary>
	public string? TextOf(string name) => this[name]?.WrappedText;

	/// <summary>The field as JavaScript's <c>String</c> gives it, or <c>null</c> when it is <c>undefined</c>.</summary>
	public string? StringOf(string name) => this[name]?.Text;
}

/// <summary>
/// A value of the db: a string, a number, the one-entry object the grammar makes of a named argument, or one of these
/// inside <c>{ text }</c>. Objects are the same only when they are one object, as JavaScript's <c>===</c> compares them.
/// </summary>
internal readonly record struct C4Value(string? Str, C4Argument? Argument, double Num, bool IsNumber, bool Wrapped)
{
	public static C4Value String(string text) => new(text, null, 0, false, false);

	public static C4Value Number(double number) => new(null, null, number, true, false);

	public static C4Value Object(C4Argument argument) => new(null, argument, 0, false, false);

	/// <summary><c>{ text: value }</c>.</summary>
	public static C4Value TextObject(string text) => new(text, null, 0, false, true);

	/// <summary><c>{ text: value }</c> of any value.</summary>
	public static C4Value Wrap(C4Value value) => value with { Wrapped = true };

	/// <summary>The value as JavaScript's <c>String</c> gives it: <c>[object Object]</c> for an object.</summary>
	public string Text => Wrapped
		? "[object Object]"
		: Argument is not null ? "[object Object]" : IsNumber ? JsNumber.ToString(Num) : Str ?? "undefined";

	/// <summary>The <c>text</c> of <c>{ text }</c> as JavaScript's <c>String</c> gives it, or <c>null</c> when the value is not wrapped.</summary>
	public string? WrappedText => Wrapped ? (this with { Wrapped = false }).Text : null;

	/// <summary>JavaScript's <c>===</c> on two fields, either <c>undefined</c>.</summary>
	public static bool Same(C4Value? a, C4Value? b)
	{
		if (a is not { } x || b is not { } y)
		{
			return a is null && b is null;
		}

		if (x.Wrapped || y.Wrapped)
		{
			return false;
		}

		if (x.Argument is not null || y.Argument is not null)
		{
			return ReferenceEquals(x.Argument, y.Argument);
		}

		return x.IsNumber == y.IsNumber && (x.IsNumber ? x.Num == y.Num : string.Equals(x.Str, y.Str, StringComparison.Ordinal));
	}
}
