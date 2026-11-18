namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// Reads a langium file and the files it imports into a <see cref="Grammar"/> whose start is the <c>entry</c> rule.
/// Parser and terminal rules both become rules. A keyword is its own text, and a regular expression is a token with a
/// <see cref="PatternSampler"/>. Actions, interfaces and types are left out, and a cross reference stands for its token.
/// </summary>
public static class LangiumGrammar
{
	/// <param name="path">The <c>.langium</c> file.</param>
	public static Grammar Read(string path)
	{
		var rules = new Dictionary<string, GrammarNode>(StringComparer.Ordinal);
		var tokens = new Dictionary<string, IReadOnlyList<Func<FuzzRandom, string>>>(StringComparer.Ordinal);
		var entry = ReadFile(Path.GetFullPath(path), rules, tokens, new HashSet<string>(StringComparer.Ordinal));
		return new Grammar(entry ?? throw new FormatException($"{path} has no entry rule."), rules, tokens);
	}

	/// <returns>The file's entry rule, or null when it has none.</returns>
	private static string? ReadFile(string path, Dictionary<string, GrammarNode> rules, Dictionary<string, IReadOnlyList<Func<FuzzRandom, string>>> tokens, HashSet<string> read)
	{
		if (!read.Add(path))
		{
			return null;
		}

		var parser = new Parser(Tokenize(File.ReadAllText(path)), tokens);
		string? entry = null;
		var imports = new List<string>();
		while (!parser.AtEnd)
		{
			var word = parser.Next();
			switch (word)
			{
				case "grammar":
					parser.Next();
					continue;
				case "import":
					imports.Add(parser.Next()[1..^1]);
					parser.Skip(";");
					continue;
				case "interface":
					parser.SkipPast("}");
					continue;
				case "type":
					parser.SkipPast(";");
					continue;
			}

			var isEntry = false;
			while (word is "entry" or "fragment" or "hidden" or "terminal")
			{
				isEntry |= word == "entry";
				word = parser.Next();
			}

			var name = word;
			while (parser.Peek != ":")
			{
				parser.Next();
			}

			parser.Next();
			// A rule of the importing file wins over one of the same name in an imported file.
			_ = rules.TryAdd(name, parser.Alternatives());
			parser.Skip(";");
			if (isEntry)
			{
				entry ??= name;
			}
		}

		foreach (var import in imports)
		{
			_ = ReadFile(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, import + ".langium")), rules, tokens, read);
		}

		return entry;
	}

	private sealed class Parser(List<string> tokens, Dictionary<string, IReadOnlyList<Func<FuzzRandom, string>>> samplers)
	{
		private int _position;

		public bool AtEnd => _position >= tokens.Count;

		public string Peek => AtEnd ? "" : tokens[_position];

		public string Next() => tokens[_position++];

		public void Skip(string token)
		{
			if (Peek == token)
			{
				_position++;
			}
		}

		public void SkipPast(string token)
		{
			while (!AtEnd && Next() != token)
			{
			}
		}

		public GrammarNode Alternatives()
		{
			var branches = new List<GrammarNode> { Group() };
			while (Peek == "|")
			{
				_position++;
				branches.Add(Group());
			}

			return branches.Count == 1 ? branches[0] : new GrammarChoice(branches);
		}

		private GrammarSequence Group()
		{
			var items = new List<GrammarNode>();
			while (!AtEnd && Peek is not ("|" or ")" or ";"))
			{
				if (_position + 1 < tokens.Count && tokens[_position + 1] is "=" or "+=" or "?=")
				{
					_position += 2;
				}

				var item = Primary();
				items.Add(Peek switch
				{
					"?" => new GrammarRepeat(item, 0, 1),
					"*" => new GrammarRepeat(item, 0, -1),
					"+" => new GrammarRepeat(item, 1, -1),
					_ => item,
				});
				if (Peek is "?" or "*" or "+")
				{
					_position++;
				}
			}

			return new GrammarSequence(items);
		}

		private GrammarNode Primary()
		{
			var token = Next();
			switch (token[0])
			{
				case '(':
					{
						var inner = Alternatives();
						Skip(")");
						return inner;
					}

				case '{':
					SkipPast("}");
					return new GrammarSequence([]);
				case '[':
					{
						var target = Next();
						if (Peek == ":")
						{
							_position++;
							target = Next();
						}

						SkipPast("]");
						return new GrammarReference(target);
					}

				case '!' or '&':
					return Primary();
				case '"' or '\'':
					return new GrammarText(token[1..^1]);
				case '/':
					{
						var pattern = token[1..token.LastIndexOf('/')];
						if (!samplers.ContainsKey(token))
						{
							samplers[token] = [PatternSampler.Parse(pattern, jison: false).Sample];
						}

						return new GrammarReference(token);
					}

				default:
					return new GrammarReference(token);
			}
		}
	}

	/// <summary>
	/// Names, keywords, regular expressions and punctuation, without comments. A <c>/</c> after <c>:</c>, <c>|</c> or
	/// <c>(</c> starts a regular expression.
	/// </summary>
	private static List<string> Tokenize(string text)
	{
		var tokens = new List<string>();
		var i = 0;
		while (i < text.Length)
		{
			var c = text[i];
			if (char.IsWhiteSpace(c))
			{
				i++;
			}
			else if (text.AsSpan(i).StartsWith("//"))
			{
				var end = text.IndexOf('\n', i);
				i = end < 0 ? text.Length : end;
			}
			else if (text.AsSpan(i).StartsWith("/*"))
			{
				i = text.IndexOf("*/", i + 2, StringComparison.Ordinal) + 2;
			}
			else if (c == '/' && tokens.Count > 0 && tokens[^1] is ":" or "|" or "(")
			{
				var start = i++;
				var inClass = false;
				while (text[i] != '/' || inClass)
				{
					inClass = text[i] == '[' || (inClass && text[i] != ']');
					i += text[i] == '\\' ? 2 : 1;
				}

				i++;
				while (i < text.Length && char.IsAsciiLetter(text[i]))
				{
					i++;
				}

				tokens.Add(text[start..i]);
			}
			else if (c is '"' or '\'')
			{
				var start = i++;
				while (text[i] != c)
				{
					i += text[i] == '\\' ? 2 : 1;
				}

				tokens.Add(text[start..++i]);
			}
			else if (char.IsLetter(c) || c == '_')
			{
				var start = i;
				while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
				{
					i++;
				}

				tokens.Add(text[start..i]);
			}
			else if (i + 1 < text.Length && text.AsSpan(i, 2) is "+=" or "?=" or "->")
			{
				tokens.Add(text.Substring(i, 2));
				i += 2;
			}
			else
			{
				tokens.Add(c.ToString());
				i++;
			}
		}

		return tokens;
	}
}
