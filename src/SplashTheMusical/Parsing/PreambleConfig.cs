namespace SplashTheMusical.Parsing;

/// <summary>
/// The config a document's front matter and directives give, as mermaid.js's <c>preprocessDiagram</c> merges it: the
/// front matter config with <c>displayMode</c> set as <c>gantt.displayMode</c>, then the <c>init</c> directives of
/// <c>detectInit</c> in <c>packages/mermaid/src/utils.ts</c>, merged as lodash's <c>merge</c> does.
/// </summary>
internal static class PreambleConfig
{
	/// <summary>The merged config. <paramref name="diagramType"/> is the id of mermaid.js's detector, such as <c>flowchart-v2</c>.</summary>
	public static MermaidValue Resolve(MermaidDocument document, string diagramType)
	{
		var config = new OrderedDictionary<string, MermaidValue>();
		if (document.FrontMatterConfig is { Kind: MermaidValueKind.Mapping } frontMatter)
		{
			Merge(config, frontMatter.Entries);
		}

		if (document.DisplayMode is not null)
		{
			var gantt = config.TryGetValue("gantt", out var existing) && existing.Kind == MermaidValueKind.Mapping
				? new OrderedDictionary<string, MermaidValue>(existing.Entries)
				: [];
			gantt["displayMode"] = MermaidValue.FromString(document.DisplayMode);
			config["gantt"] = MermaidValue.FromEntries(gantt);
		}

		var init = Init(document.Directives, diagramType);
		Wrap(document.Directives, init);
		Merge(config, init);
		return MermaidValue.FromEntries(config);
	}

	/// <summary>
	/// <c>detectInit</c>. A directive is an <c>init</c> when its type ends with <c>init</c> or <c>initialize</c>, as the
	/// pattern <c>/(?:init\b)|(?:initialize\b)/</c> finds. One invalid <c>init</c> drops them all, because
	/// <c>detectDirective</c> catches the JSON error for the whole text. One is used as written. Several are merged by
	/// <c>assignWithDepth</c>, after <c>sanitizeDirective</c>, which this does not do yet.
	/// </summary>
	private static OrderedDictionary<string, MermaidValue> Init(List<MermaidDirective> directives, string diagramType)
	{
		var inits = new List<MermaidDirective>();
		foreach (var directive in directives)
		{
			if (IsInit(directive.Type))
			{
				if (directive.InvalidArguments is not null)
				{
					return [];
				}

				inits.Add(directive);
			}
		}

		var result = new OrderedDictionary<string, MermaidValue>();
		if (inits.Count == 1)
		{
			if (inits[0].Arguments is { Kind: MermaidValueKind.Mapping } arguments)
			{
				result = new OrderedDictionary<string, MermaidValue>(arguments.Entries);
			}
		}
		else
		{
			foreach (var init in inits)
			{
				if (init.Arguments is { Kind: MermaidValueKind.Mapping } arguments)
				{
					AssignWithDepth(result, arguments.Entries, 2);
				}
			}
		}

		if (result.Remove("config", out var diagramConfig))
		{
			result[diagramType == "flowchart-v2" ? "flowchart" : diagramType] = diagramConfig;
		}

		return result;
	}

	private static bool IsInit(string type) =>
		type.EndsWith("init", StringComparison.Ordinal) || type.EndsWith("initialize", StringComparison.Ordinal);

	/// <summary>
	/// <c>processDirectives</c>: a directive whose type holds <c>wrap</c> is a wrap directive. One wrap directive sets
	/// <c>wrap</c> when its type is <c>wrap</c>. Several set it to whether any of them is <c>wrap</c>.
	/// </summary>
	private static void Wrap(List<MermaidDirective> directives, OrderedDictionary<string, MermaidValue> init)
	{
		var count = 0;
		var any = false;
		foreach (var directive in directives)
		{
			if (!directive.Type.Contains("wrap", StringComparison.Ordinal))
			{
				continue;
			}

			if (directive.InvalidArguments is not null)
			{
				return;
			}

			count++;
			any |= directive.Type == "wrap";
		}

		if (count == 0 || (count == 1 && !any))
		{
			return;
		}

		init["wrap"] = MermaidValue.FromBoolean(any);
	}

	/// <summary>
	/// mermaid.js's <c>assignWithDepth</c> for a mapping source. A mapping or a sequence goes into a missing value or into
	/// a mapping or a sequence, one level down. A null is skipped. A scalar replaces anything but a mapping, a sequence or a
	/// null, because JavaScript's <c>typeof null</c> is <c>object</c>.
	/// </summary>
	private static void AssignWithDepth(OrderedDictionary<string, MermaidValue> target, IReadOnlyDictionary<string, MermaidValue> source, int depth)
	{
		foreach (var (key, value) in source)
		{
			target.TryGetValue(key, out var existing);
			if (value.Kind == MermaidValueKind.Null)
			{
				continue;
			}

			if (IsObject(value))
			{
				if (existing is null)
				{
					target[key] = AssignObject(value.Kind == MermaidValueKind.Sequence ? MermaidValue.FromItems([]) : MermaidValue.FromEntries([]), value, depth - 1);
				}
				else if (IsObject(existing))
				{
					target[key] = AssignObject(existing, value, depth - 1);
				}
			}
			else if (existing is null || !(IsObject(existing) || existing.Kind == MermaidValueKind.Null))
			{
				target[key] = value;
			}
		}
	}

	/// <summary><c>assignWithDepth</c> of a mapping or a sequence into a mapping or a sequence.</summary>
	private static MermaidValue AssignObject(MermaidValue target, MermaidValue source, int depth)
	{
		if (source.Kind == MermaidValueKind.Sequence)
		{
			if (target.Kind == MermaidValueKind.Sequence)
			{
				// Array.prototype.includes compares objects by reference, so only equal scalars are left out.
				var items = new List<MermaidValue>(target.Items);
				foreach (var item in source.Items)
				{
					if (IsObject(item) || !items.Contains(item))
					{
						items.Add(item);
					}
				}

				return MermaidValue.FromItems(items);
			}

			foreach (var item in source.Items)
			{
				if (item.Kind == MermaidValueKind.Mapping)
				{
					target = AssignObject(target, item, depth);
				}
			}

			return target;
		}

		if (target.Kind != MermaidValueKind.Mapping)
		{
			return target;
		}

		var entries = new OrderedDictionary<string, MermaidValue>(target.Entries);
		if (depth <= 0)
		{
			foreach (var (key, item) in source.Entries)
			{
				entries[key] = item;
			}
		}
		else
		{
			AssignWithDepth(entries, source.Entries, depth);
		}

		return MermaidValue.FromEntries(entries);
	}

	private static bool IsObject(MermaidValue value) => value.Kind is MermaidValueKind.Mapping or MermaidValueKind.Sequence;

	/// <summary>lodash's <c>merge</c>: mappings merge key by key, sequences index by index, and anything else replaces.</summary>
	private static void Merge(OrderedDictionary<string, MermaidValue> target, IReadOnlyDictionary<string, MermaidValue> source)
	{
		foreach (var (key, value) in source)
		{
			target.TryGetValue(key, out var existing);
			target[key] = MergeValue(existing, value);
		}
	}

	private static MermaidValue MergeValue(MermaidValue? existing, MermaidValue value)
	{
		switch (value.Kind)
		{
			case MermaidValueKind.Mapping:
				var entries = existing is { Kind: MermaidValueKind.Mapping }
					? new OrderedDictionary<string, MermaidValue>(existing.Entries)
					: [];
				Merge(entries, value.Entries);
				return MermaidValue.FromEntries(entries);
			case MermaidValueKind.Sequence:
				var items = existing is { Kind: MermaidValueKind.Sequence } ? new List<MermaidValue>(existing.Items) : [];
				for (var i = 0; i < value.Items.Count; i++)
				{
					var merged = MergeValue(i < items.Count ? items[i] : null, value.Items[i]);
					if (i < items.Count)
					{
						items[i] = merged;
					}
					else
					{
						items.Add(merged);
					}
				}

				return MermaidValue.FromItems(items);
			default:
				return value;
		}
	}
}
