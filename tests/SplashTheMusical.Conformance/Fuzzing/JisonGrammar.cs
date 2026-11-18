using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// Reads a jison file into a <see cref="Grammar"/>. Each lexer rule that returns a token gives that token a sampler of
/// its pattern. A rule of an exclusive start state that another rule enters without returning a token, such as a string
/// body after <c>"</c>, gets that rule's text before it, and the text of the state's rule that leaves it without a token
/// after it. The productions become rules, and a quoted symbol in one is its own text.
/// </summary>
public static class JisonGrammar
{
	private sealed record LexRule(IReadOnlyList<string> States, PatternSampler Pattern, string? Token, string? Enters, bool Leaves);

	public static Grammar Read(string text)
	{
		var lexStart = text.IndexOf("%lex", StringComparison.Ordinal);
		var lexEnd = text.IndexOf("/lex", StringComparison.Ordinal);
		var rules = lexStart < 0 ? [] : LexRules(text[(lexStart + 4)..lexEnd]);
		var tokens = Samplers(rules);
		var (start, productions) = Productions(lexEnd < 0 ? text : text[(lexEnd + 4)..], tokens);
		return new Grammar(start, productions, tokens);
	}

	private static Dictionary<string, IReadOnlyList<Func<FuzzRandom, string>>> Samplers(List<LexRule> rules)
	{
		var samplers = new Dictionary<string, List<Func<FuzzRandom, string>>>(StringComparer.Ordinal);
		foreach (var rule in rules)
		{
			if (rule.Token is null)
			{
				continue;
			}

			var exclusive = rule.States.Count > 0 && !rule.States.Contains("*") && !rule.States.Contains("INITIAL");
			var enter = exclusive ? rules.Find(r => r.Token is null && r.Enters is { } s && rule.States.Contains(s)) : null;
			var leave = enter is null ? null : rules.Find(r => r.Token is null && r.Leaves && r.States.Contains(enter.Enters!));
			var pattern = rule.Pattern;
			Func<FuzzRandom, string> sampler = enter is null
				? pattern.Sample
				: random => enter.Pattern.Sample(random) + pattern.Sample(random) + (leave?.Pattern.Sample(random) ?? "");
			if (!samplers.TryGetValue(rule.Token, out var list))
			{
				samplers[rule.Token] = list = [];
			}

			list.Add(sampler);
		}

		return samplers.ToDictionary(p => p.Key, p => (IReadOnlyList<Func<FuzzRandom, string>>)p.Value, StringComparer.Ordinal);
	}

	/// <summary>The rules after the lexer's own <c>%%</c>.</summary>
	private static List<LexRule> LexRules(string lex)
	{
		var rules = new List<LexRule>();
		var marker = lex.IndexOf("\n%%", StringComparison.Ordinal);
		var i = marker < 0 ? 0 : marker + 3;
		while (i < lex.Length)
		{
			if (char.IsWhiteSpace(lex[i]))
			{
				i++;
				continue;
			}

			if (lex.AsSpan(i).StartsWith("/*"))
			{
				i = lex.IndexOf("*/", i + 2, StringComparison.Ordinal) + 2;
				continue;
			}

			if (lex.AsSpan(i).StartsWith("//") || lex[i] == '%')
			{
				i = LineEnd(lex, i);
				continue;
			}

			IReadOnlyList<string> states = [];
			if (lex[i] == '<' && !lex.AsSpan(i).StartsWith("<<EOF>>"))
			{
				var close = lex.IndexOf('>', i);
				states = lex[(i + 1)..close].Split(',', StringSplitOptions.TrimEntries);
				i = close + 1;
			}

			var pattern = Pattern(lex, ref i);
			while (i < lex.Length && lex[i] is ' ' or '\t')
			{
				i++;
			}

			string action;
			if (i < lex.Length && lex[i] == '{')
			{
				var end = BlockEnd(lex, i);
				action = lex[i..end];
				i = LineEnd(lex, end);
			}
			else
			{
				var end = LineEnd(lex, i);
				action = lex[i..end];
				i = end;
			}

			PatternSampler sampler;
			try
			{
				sampler = PatternSampler.Parse(pattern == "<<EOF>>" ? "" : pattern, jison: true);
			}
			catch (Exception e) when (e is FormatException or ArgumentException or IndexOutOfRangeException)
			{
				continue;
			}

			rules.Add(new LexRule(states, sampler, Returned(action), Entered(action), action.Contains("popState", StringComparison.Ordinal)));
		}

		return rules;
	}

	/// <summary>
	/// A pattern ends at a space outside quotes and brackets. One that ends in <c>|</c> goes on at the next line, as
	/// flow.jison's <c>UNICODE_TEXT</c> does.
	/// </summary>
	private static string Pattern(string lex, ref int i)
	{
		var builder = new StringBuilder();
		char? quote = null;
		var inClass = false;
		while (i < lex.Length)
		{
			var c = lex[i];
			if (quote is null && !inClass && char.IsWhiteSpace(c))
			{
				if (builder.Length > 0 && builder[^1] == '|' && c is '\r' or '\n')
				{
					while (i < lex.Length && char.IsWhiteSpace(lex[i]))
					{
						i++;
					}

					continue;
				}

				break;
			}

			builder.Append(c);
			i++;
			if (c == '\\' && i < lex.Length)
			{
				builder.Append(lex[i++]);
			}
			else if (quote is { } q)
			{
				quote = c == q ? null : quote;
			}
			else if (inClass)
			{
				inClass = c != ']';
			}
			else if (c == '[')
			{
				inClass = true;
				if (i < lex.Length && lex[i] == ']')
				{
					builder.Append(lex[i++]);
				}
			}
			else if (c is '"' or '\'')
			{
				quote = c;
			}
		}

		return builder.ToString();
	}

	private static string? Returned(string action)
	{
		var at = action.IndexOf("return", StringComparison.Ordinal);
		while (at >= 0)
		{
			var i = at + "return".Length;
			while (i < action.Length && action[i] is ' ' or '\t')
			{
				i++;
			}

			if (i < action.Length && action[i] is '\'' or '"')
			{
				var close = action.IndexOf(action[i], i + 1);
				return action[(i + 1)..close];
			}

			at = action.IndexOf("return", i, StringComparison.Ordinal);
		}

		return null;
	}

	private static string? Entered(string action)
	{
		foreach (var call in new[] { "begin(", "pushState(" })
		{
			var at = action.IndexOf(call, StringComparison.Ordinal);
			if (at >= 0)
			{
				var i = at + call.Length;
				var close = action.IndexOf(action[i], i + 1);
				return action[(i + 1)..close];
			}
		}

		return null;
	}

	/// <summary>The productions after the declarations' <c>%%</c>, up to the next <c>%%</c>, and the start rule.</summary>
	private static (string Start, Dictionary<string, GrammarNode> Rules) Productions(string text, Dictionary<string, IReadOnlyList<Func<FuzzRandom, string>>> lexed)
	{
		var startAt = text.IndexOf("%start", StringComparison.Ordinal);
		string? start = startAt < 0 ? null : text[(startAt + 6)..LineEnd(text, startAt)].Trim();
		var body = text.IndexOf("%%", StringComparison.Ordinal);
		var tokens = GrammarTokens(text, body + 2);
		var rules = new Dictionary<string, GrammarNode>(StringComparer.Ordinal);
		var i = 0;
		while (i + 1 < tokens.Count)
		{
			var name = tokens[i];
			if (tokens[i + 1] != ":")
			{
				i++;
				continue;
			}

			start ??= name;
			i += 2;
			var branches = new List<GrammarNode>();
			var items = new List<GrammarNode>();
			while (i < tokens.Count && tokens[i] != ";" && !(i + 1 < tokens.Count && tokens[i + 1] == ":" && tokens[i] != "|"))
			{
				var token = tokens[i++];
				if (token == "|")
				{
					branches.Add(new GrammarSequence(items));
					items = [];
				}
				else if (token == "%prec")
				{
					i++;
				}
				else if (token[0] is '\'' or '"')
				{
					var literal = token[1..^1];
					items.Add(lexed.ContainsKey(literal) ? new GrammarReference(literal) : new GrammarText(literal));
				}
				else if (token != "%empty")
				{
					items.Add(new GrammarReference(token));
				}
			}

			branches.Add(new GrammarSequence(items));
			rules[name] = branches.Count == 1 ? branches[0] : new GrammarChoice(branches);
			if (i < tokens.Count && tokens[i] == ";")
			{
				i++;
			}
		}

		return (start ?? throw new FormatException("The grammar has no productions."), rules);
	}

	/// <summary>The names, quoted symbols and <c>: | ;</c> of the productions, without actions and comments.</summary>
	private static List<string> GrammarTokens(string text, int i)
	{
		var tokens = new List<string>();
		while (i < text.Length)
		{
			var c = text[i];
			if (char.IsWhiteSpace(c))
			{
				i++;
			}
			else if (text.AsSpan(i).StartsWith("%%"))
			{
				break;
			}
			else if (text.AsSpan(i).StartsWith("/*"))
			{
				i = text.IndexOf("*/", i + 2, StringComparison.Ordinal) + 2;
			}
			else if (text.AsSpan(i).StartsWith("//"))
			{
				i = LineEnd(text, i);
			}
			else if (c == '{')
			{
				i = BlockEnd(text, i);
			}
			else if (c == '[')
			{
				i = text.IndexOf(']', i) + 1;
			}
			else if (c is ':' or '|' or ';')
			{
				tokens.Add(c.ToString());
				i++;
			}
			else if (c is '\'' or '"')
			{
				var close = text.IndexOf(c, i + 1);
				tokens.Add(text[i..(close + 1)]);
				i = close + 1;
			}
			else
			{
				var start = i;
				while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not (':' or '|' or ';' or '{' or '[' or '\'' or '"'))
				{
					i++;
				}

				tokens.Add(text[start..i]);
			}
		}

		return tokens;
	}

	/// <summary>The index after the brace that closes the one at <paramref name="i"/>, past strings and comments.</summary>
	private static int BlockEnd(string text, int i)
	{
		var depth = 0;
		while (i < text.Length)
		{
			var c = text[i];
			if (c is '\'' or '"' or '`')
			{
				i++;
				while (i < text.Length && text[i] != c)
				{
					i += text[i] == '\\' ? 2 : 1;
				}
			}
			else if (text.AsSpan(i).StartsWith("/*"))
			{
				i = text.IndexOf("*/", i + 2, StringComparison.Ordinal) + 1;
			}
			else if (text.AsSpan(i).StartsWith("//"))
			{
				i = LineEnd(text, i) - 1;
			}
			else if (c == '{')
			{
				depth++;
			}
			else if (c == '}' && --depth == 0)
			{
				return i + 1;
			}

			i++;
		}

		return text.Length;
	}

	private static int LineEnd(string text, int i)
	{
		var end = text.IndexOf('\n', i);
		return end < 0 ? text.Length : end;
	}
}
