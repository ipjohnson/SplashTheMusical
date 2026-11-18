using System.Globalization;
using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// Makes text for a token from its pattern: a JavaScript regular expression, as a langium terminal writes it, or a jison
/// lexer pattern, which may also quote literal text. Lookarounds and anchors match nothing, so a sample can miss what
/// they require. A repeat takes at most two more than its minimum.
/// </summary>
public sealed class PatternSampler
{
	private readonly Node _root;

	private PatternSampler(Node root) => _root = root;

	/// <param name="jison">Whether text in double quotes is literal, as in a jison lexer rule.</param>
	public static PatternSampler Parse(string pattern, bool jison)
	{
		var parser = new Parser(pattern, jison);
		var root = parser.Alternation();
		return parser.AtEnd ? new PatternSampler(root) : throw new FormatException($"Unexpected '{pattern[parser.Position]}' at {parser.Position} of {pattern}");
	}

	public string Sample(FuzzRandom random)
	{
		var builder = new StringBuilder();
		_root.Write(builder, random);
		return builder.ToString();
	}

	private abstract class Node
	{
		public abstract void Write(StringBuilder builder, FuzzRandom random);
	}

	private sealed class Text(string value) : Node
	{
		public string Value => value;

		public override void Write(StringBuilder builder, FuzzRandom random) => builder.Append(value);
	}

	private sealed class Sequence(List<Node> items) : Node
	{
		public override void Write(StringBuilder builder, FuzzRandom random)
		{
			foreach (var item in items)
			{
				item.Write(builder, random);
			}
		}
	}

	private sealed class Choice(List<Node> branches) : Node
	{
		public override void Write(StringBuilder builder, FuzzRandom random) => random.Pick(branches).Write(builder, random);
	}

	private sealed class Repeat(Node item, int min, int max) : Node
	{
		public override void Write(StringBuilder builder, FuzzRandom random)
		{
			var count = random.Between(min, max);
			for (var i = 0; i < count; i++)
			{
				item.Write(builder, random);
			}
		}
	}

	/// <summary>A character class, or an escape such as <c>\s</c>, as ranges of characters.</summary>
	private sealed class CharSet(List<(char Low, char High)> ranges, bool negated) : Node
	{
		private const string Candidates = "a1 _-xA.:";

		public List<(char Low, char High)> Ranges => ranges;

		public bool Negated => negated;

		public override void Write(StringBuilder builder, FuzzRandom random)
		{
			if (!negated)
			{
				if (ranges.Count > 0)
				{
					var (low, high) = random.Pick(ranges);
					builder.Append((char)random.Between(low, high));
				}

				return;
			}

			var start = random.Next(Candidates.Length);
			for (var i = 0; i < Candidates.Length; i++)
			{
				var c = Candidates[(start + i) % Candidates.Length];
				if (!ranges.Exists(r => c >= r.Low && c <= r.High))
				{
					builder.Append(c);
					return;
				}
			}
		}
	}

	private sealed class Parser(string pattern, bool jison)
	{
		private int _position;

		public int Position => _position;

		public bool AtEnd => _position >= pattern.Length;

		private char Peek => pattern[_position];

		public Node Alternation()
		{
			var branches = new List<Node> { Sequence() };
			while (!AtEnd && Peek == '|')
			{
				_position++;
				branches.Add(Sequence());
			}

			return branches.Count == 1 ? branches[0] : new Choice(branches);
		}

		private Node Sequence()
		{
			var items = new List<Node>();
			while (!AtEnd && Peek is not ('|' or ')'))
			{
				var atom = Atom();
				items.Add(Quantified(atom));
			}

			return items.Count == 1 ? items[0] : new Sequence(items);
		}

		private Node Quantified(Node atom)
		{
			while (!AtEnd)
			{
				int min;
				int max;
				switch (Peek)
				{
					case '*':
						(min, max) = (0, 2);
						_position++;
						break;
					case '+':
						(min, max) = (1, 3);
						_position++;
						break;
					case '?':
						(min, max) = (0, 1);
						_position++;
						break;
					case '{' when _position + 1 < pattern.Length && char.IsAsciiDigit(pattern[_position + 1]):
						(min, max) = Braces();
						break;
					default:
						return atom;
				}

				if (!AtEnd && Peek == '?')
				{
					_position++;
				}

				atom = new Repeat(atom, min, max);
			}

			return atom;
		}

		private (int Min, int Max) Braces()
		{
			var close = pattern.IndexOf('}', _position);
			var body = pattern[(_position + 1)..close];
			_position = close + 1;
			var comma = body.IndexOf(',', StringComparison.Ordinal);
			if (comma < 0)
			{
				var exact = int.Parse(body, CultureInfo.InvariantCulture);
				return (exact, exact);
			}

			var min = int.Parse(body[..comma], CultureInfo.InvariantCulture);
			return (min, comma == body.Length - 1 ? min + 2 : int.Parse(body[(comma + 1)..], CultureInfo.InvariantCulture));
		}

		private Node Atom()
		{
			var c = pattern[_position++];
			switch (c)
			{
				case '(':
					{
						var lookaround = false;
						if (!AtEnd && Peek == '?')
						{
							_position++;
							if (Peek == '<' && _position + 1 < pattern.Length && pattern[_position + 1] is '=' or '!')
							{
								_position++;
							}

							lookaround = Peek is '=' or '!';
							_position++;
						}

						var inner = Alternation();
						Expect(')');
						return lookaround ? new Text("") : inner;
					}

				case '[':
					return Class();
				case '\\':
					return Escape(inClass: false) is { } set ? set : new Text("");
				case '.':
					return new Text("a");
				case '^' or '$':
					return new Text("");
				case '"' when jison:
				case '\'' when jison:
					{
						var builder = new StringBuilder();
						while (pattern[_position] != c)
						{
							if (pattern[_position] == '\\')
							{
								_position++;
							}

							builder.Append(pattern[_position++]);
						}

						_position++;
						return new Text(builder.ToString());
					}

				default:
					return new Text(c.ToString());
			}
		}

		private CharSet Class()
		{
			var negated = !AtEnd && Peek == '^';
			if (negated)
			{
				_position++;
			}

			var ranges = new List<(char, char)>();
			var first = true;
			while (Peek != ']' || first)
			{
				first = false;
				char low;
				if (Peek == '\\')
				{
					_position++;
					var escape = Escape(inClass: true);
					if (escape is CharSet set)
					{
						if (!set.Negated)
						{
							ranges.AddRange(set.Ranges);
						}

						continue;
					}

					low = escape is Text { Value.Length: > 0 } text ? text.Value[0] : 'a';
				}
				else
				{
					low = pattern[_position++];
				}

				var high = low;
				if (Peek == '-' && _position + 1 < pattern.Length && pattern[_position + 1] != ']')
				{
					_position++;
					if (Peek == '\\')
					{
						_position++;
						high = Escape(inClass: true) is Text { } text ? text.Value[0] : low;
					}
					else
					{
						high = pattern[_position++];
					}
				}

				ranges.Add((low, high));
			}

			_position++;
			return new CharSet(ranges, negated);
		}

		/// <summary>The escape after a backslash: a <see cref="CharSet"/> for a class escape and a <see cref="Text"/> otherwise.</summary>
		private Node Escape(bool inClass)
		{
			var c = pattern[_position++];
			return c switch
			{
				's' => new CharSet([(' ', ' '), ('\t', '\t'), ('\n', '\n'), ('\r', '\r')], false),
				'S' => new CharSet([(' ', ' '), ('\t', '\t'), ('\n', '\n'), ('\r', '\r')], true),
				'd' => new CharSet([('0', '9')], false),
				'D' => new CharSet([('0', '9')], true),
				'w' => new CharSet([('a', 'z'), ('A', 'Z'), ('0', '9'), ('_', '_')], false),
				'W' => new CharSet([('a', 'z'), ('A', 'Z'), ('0', '9'), ('_', '_')], true),
				'b' when inClass => new Text("\b"),
				'b' or 'B' => new Text(""),
				'n' => new Text("\n"),
				'r' => new Text("\r"),
				't' => new Text("\t"),
				'f' => new Text("\f"),
				'v' => new Text("\v"),
				'u' when _position + 4 <= pattern.Length => new Text(((char)int.Parse(Take(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()),
				'x' when _position + 2 <= pattern.Length => new Text(((char)int.Parse(Take(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()),
				_ => new Text(c.ToString()),
			};
		}

		private string Take(int length)
		{
			var text = pattern.Substring(_position, length);
			_position += length;
			return text;
		}

		private void Expect(char c)
		{
			if (AtEnd || Peek != c)
			{
				throw new FormatException($"Expected '{c}' at {_position} of {pattern}");
			}

			_position++;
		}
	}
}
