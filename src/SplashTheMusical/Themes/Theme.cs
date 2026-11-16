using System.Globalization;

namespace SplashTheMusical.Themes;

/// <summary>
/// The <c>Theme</c> class of each file of mermaid's <c>packages/mermaid/src/themes</c>. There a theme is a JavaScript
/// object: the constructor sets its base variables and <c>updateColors</c> derives the rest, most of them only when they
/// are falsy. The subclasses port the two methods line by line, so <see cref="Get"/> and <see cref="Set"/> stand for
/// <c>this[name]</c>. A variable that holds <c>undefined</c> is present with a <c>null</c> value.
/// </summary>
internal abstract class Theme
{
	private readonly OrderedDictionary<string, MermaidValue?> _variables = new(StringComparer.Ordinal);

	/// <summary>The theme's key in <c>packages/mermaid/src/themes/index.js</c>.</summary>
	public abstract string Name { get; }

	/// <summary>
	/// <c>getThemeVariables</c> of the theme named <paramref name="name"/> with <paramref name="overrides"/>, the
	/// <c>themeVariables</c> of the config, or <c>null</c> when no theme has the name. Variables that hold
	/// <c>undefined</c> are left out.
	/// </summary>
	/// <exception cref="FormatException">A colour the theme passes to khroma does not parse.</exception>
	/// <exception cref="InvalidOperationException">The overrides are <see cref="MermaidValue.Null"/>.</exception>
	public static MermaidValue? Variables(string name, MermaidValue? overrides)
	{
		Theme? theme = name switch
		{
			"base" => new BaseTheme(),
			"dark" => new DarkTheme(),
			"default" => new DefaultTheme(),
			"forest" => new ForestTheme(),
			"neutral" => new NeutralTheme(),
			"neo" => new NeoTheme(),
			"neo-dark" => new NeoDarkTheme(),
			"redux" => new ReduxTheme(),
			"redux-dark" => new ReduxDarkTheme(),
			"redux-color" => new ReduxColorTheme(),
			"redux-dark-color" => new ReduxDarkColorTheme(),
			_ => null,
		};
		if (theme is null)
		{
			return null;
		}

		theme.Construct();
		theme.Calculate(overrides);
		return theme.ToValue();
	}

	/// <summary>The constructor of the JavaScript class.</summary>
	protected abstract void Construct();

	/// <summary><c>updateColors</c>.</summary>
	protected abstract void UpdateColors();

	/// <summary>What <c>calculate</c> does before it reads the overrides.</summary>
	protected virtual void BeforeCalculate()
	{
	}

	/// <summary>What <c>calculate</c> does after it applies the overrides a second time.</summary>
	protected virtual void AfterCalculate(MermaidValue overrides)
	{
	}

	/// <summary>
	/// <c>calculate</c>: the overrides are copied in, <c>updateColors</c> derives the variables, and the overrides are
	/// applied again so an override of a derived variable wins. An override of an object variable is merged over it.
	/// </summary>
	private void Calculate(MermaidValue? overrides)
	{
		BeforeCalculate();
		if (overrides is null || overrides.Kind is not (MermaidValueKind.Mapping or MermaidValueKind.Sequence or MermaidValueKind.Null))
		{
			UpdateColors();
			return;
		}

		if (overrides.Kind == MermaidValueKind.Null)
		{
			throw new InvalidOperationException("Cannot convert undefined or null to object");
		}

		var entries = overrides.Kind == MermaidValueKind.Mapping
			? [.. overrides.Entries]
			: overrides.Items.Select((item, i) => KeyValuePair.Create(i.ToString(CultureInfo.InvariantCulture), item)).ToList();
		foreach (var (key, value) in entries)
		{
			Set(key, value);
		}

		UpdateColors();
		foreach (var (key, value) in entries)
		{
			Set(key, ApplyOverride(Get(key), value));
		}

		AfterCalculate(overrides);
	}

	private MermaidValue ToValue()
	{
		var entries = new OrderedDictionary<string, MermaidValue>(_variables.Count, StringComparer.Ordinal);
		foreach (var (key, value) in _variables)
		{
			if (value is not null)
			{
				entries[key] = value;
			}
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary><c>applyOverride</c> in <c>theme-helpers.js</c>: two objects merge, and otherwise the override wins.</summary>
	private static MermaidValue ApplyOverride(MermaidValue? generated, MermaidValue value)
	{
		if (generated is not { Kind: MermaidValueKind.Mapping } || value.Kind != MermaidValueKind.Mapping)
		{
			return value;
		}

		var merged = new OrderedDictionary<string, MermaidValue>(generated.Entries, StringComparer.Ordinal);
		foreach (var (key, item) in value.Entries)
		{
			merged[key] = item;
		}

		return MermaidValue.FromEntries(merged);
	}

	protected IEnumerable<string> Names => _variables.Keys;

	protected MermaidValue? Get(string name) => _variables.TryGetValue(name, out var value) ? value : null;

	protected void Set(string name, MermaidValue? value) => _variables[name] = value;

	/// <summary><c>prefix + i</c>.</summary>
	protected static string Key(string prefix, int i) => prefix + i.ToString(CultureInfo.InvariantCulture);

	protected static MermaidValue Str(string value) => MermaidValue.FromString(value);

	protected static MermaidValue Num(double value) => MermaidValue.FromNumber(value);

	protected static MermaidValue Bool(bool value) => MermaidValue.FromBoolean(value);

	/// <summary>An object literal. An <c>undefined</c> property is left out.</summary>
	protected static MermaidValue Obj(params ReadOnlySpan<(string Key, MermaidValue? Value)> properties)
	{
		var entries = new OrderedDictionary<string, MermaidValue>(properties.Length, StringComparer.Ordinal);
		foreach (var (key, value) in properties)
		{
			if (value is not null)
			{
				entries[key] = value;
			}
		}

		return MermaidValue.FromEntries(entries);
	}

	/// <summary>An array literal.</summary>
	protected static MermaidValue Arr(params ReadOnlySpan<MermaidValue> items) => MermaidValue.FromItems([.. items]);

	/// <summary>The left side of <c>||</c>: the value when it is truthy, so <c>??</c> takes the right side otherwise.</summary>
	protected static MermaidValue? Or(MermaidValue? value) => Truthy(value) ? value : null;

	/// <summary>The left side of <c>??</c>: the value unless it is <c>null</c> or <c>undefined</c>.</summary>
	protected static MermaidValue? Nullish(MermaidValue? value) => value is null || value.Kind == MermaidValueKind.Null ? null : value;

	/// <summary>JavaScript's truthiness, with <c>undefined</c> as <c>null</c>.</summary>
	protected static bool Truthy(MermaidValue? value) => value?.Kind switch
	{
		null or MermaidValueKind.Null => false,
		MermaidValueKind.Boolean => value.Boolean,
		MermaidValueKind.Number => value.Number != 0 && !double.IsNaN(value.Number),
		MermaidValueKind.String => value.String.Length > 0,
		_ => true,
	};

	/// <summary><c>===</c>. Objects are equal only when they are the same object.</summary>
	protected static bool StrictEquals(MermaidValue? left, MermaidValue? right)
	{
		if (left is null || right is null)
		{
			return left is null && right is null;
		}

		return left.Kind is MermaidValueKind.Mapping or MermaidValueKind.Sequence ? ReferenceEquals(left, right) : left.Equals(right);
	}

	/// <summary><c>value?.[key]</c>.</summary>
	protected static MermaidValue? Member(MermaidValue? value, string key) => value is { Kind: MermaidValueKind.Mapping } ? value[key] : null;

	/// <summary><c>value[index]</c> with a number.</summary>
	protected static MermaidValue? Index(MermaidValue? value, double index)
	{
		if (value is { Kind: MermaidValueKind.Sequence } && index >= 0 && index < value.Items.Count && index == Math.Floor(index))
		{
			return value.Items[(int)index];
		}

		return value is { Kind: MermaidValueKind.Mapping } ? value[JsNumber.ToString(index)] : null;
	}

	/// <summary><c>value.length</c>.</summary>
	protected static double Length(MermaidValue? value) => value switch
	{
		null => throw new InvalidOperationException("Cannot read properties of undefined (reading 'length')"),
		{ Kind: MermaidValueKind.Null } => throw new InvalidOperationException("Cannot read properties of null (reading 'length')"),
		{ Kind: MermaidValueKind.Sequence } => value.Items.Count,
		{ Kind: MermaidValueKind.String } => value.String.Length,
		_ => double.NaN,
	};

	/// <summary>JavaScript's <c>Number(value)</c>.</summary>
	protected static double ToNumber(MermaidValue? value) => value switch
	{
		null => double.NaN,
		{ Kind: MermaidValueKind.Null } => 0,
		{ Kind: MermaidValueKind.Boolean } => value.Boolean ? 1 : 0,
		{ Kind: MermaidValueKind.Number } => value.Number,
		{ Kind: MermaidValueKind.String } => StringToNumber(value.String),
		{ Kind: MermaidValueKind.Sequence } when value.Items.Count == 0 => 0,
		{ Kind: MermaidValueKind.Sequence } when value.Items.Count == 1 => ToNumber(value.Items[0]),
		_ => double.NaN,
	};

	protected static MermaidValue Adjust(MermaidValue? color, KhromaChannels channels) => Str(Khroma.Adjust(Color(color), channels));

	protected static MermaidValue Darken(MermaidValue? color, double amount) => Str(Khroma.Darken(Color(color), amount));

	protected static MermaidValue Lighten(MermaidValue? color, double amount) => Str(Khroma.Lighten(Color(color), amount));

	protected static MermaidValue Invert(MermaidValue? color) => Str(Khroma.Invert(Color(color)));

	protected static bool IsDark(MermaidValue? color) => Khroma.IsDark(Color(color));

	protected static MermaidValue MkBorder(MermaidValue? color, bool darkMode) => Str(Khroma.MkBorder(Color(color), darkMode));

	protected static MermaidValue Rgba(MermaidValue? color, double alpha) => Str(Khroma.Rgba(Color(color), alpha));

	protected static MermaidValue Rgba(double r, double g, double b, double a) => Str(Khroma.Rgba(r, g, b, a));

	private static string Color(MermaidValue? color) =>
		color is { Kind: MermaidValueKind.String } ? color.String : throw new FormatException("Unsupported color format: \"" + Describe(color) + "\"");

	private static string Describe(MermaidValue? value) => value switch
	{
		null => "undefined",
		{ Kind: MermaidValueKind.Null } => "null",
		{ Kind: MermaidValueKind.Boolean } => value.Boolean ? "true" : "false",
		{ Kind: MermaidValueKind.Number } => JsNumber.ToString(value.Number),
		_ => "[object Object]",
	};

	/// <summary>JavaScript's <c>Number</c> of a string: blank is 0, and text that is not a decimal number is NaN.</summary>
	private static double StringToNumber(string text)
	{
		var trimmed = text.AsSpan().Trim();
		if (trimmed.IsEmpty)
		{
			return 0;
		}

		if (trimmed is "Infinity" or "+Infinity")
		{
			return double.PositiveInfinity;
		}

		if (trimmed is "-Infinity")
		{
			return double.NegativeInfinity;
		}

		foreach (var c in trimmed)
		{
			if (!(char.IsAsciiDigit(c) || c is '.' or 'e' or 'E' or '+' or '-'))
			{
				return double.NaN;
			}
		}

		return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
	}
}
