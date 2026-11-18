using System.Text;

namespace SplashTheMusical.Diagrams.Class;

/// <summary>
/// mermaid.js's <c>ClassMember</c> of <c>classTypes.ts</c>: a member line read as a method or an attribute, its
/// visibility, its classifier, and the text the class box draws for it.
/// </summary>
internal sealed class ClassMemberText
{
	private ClassMemberText(bool isMethod) => IsMethod = isMethod;

	public bool IsMethod { get; }

	/// <summary><c>#</c>, <c>+</c>, <c>~</c>, <c>-</c> or empty.</summary>
	public string Visibility { get; private set; } = "";

	public string Id { get; private set; } = "";

	public string Parameters { get; private set; } = "";

	public string ReturnType { get; private set; } = "";

	/// <summary><c>$</c> for static, <c>*</c> for abstract, or empty.</summary>
	public string Classifier { get; private set; } = "";

	/// <summary>
	/// The db's <c>text</c>: the visibility after a backslash, the name and, for a method, its parameters and return
	/// type, each with <c>~</c> pairs read as generics, and <c>&lt;</c> and <c>&gt;</c> written as entities.
	/// </summary>
	public string Text { get; private set; } = "";

	/// <summary>
	/// Whether <paramref name="member"/> reads as a method, as it has a <c>)</c> after its first character, but the
	/// method expression of <c>parseMember</c> does not match it, so mermaid.js throws.
	/// </summary>
	public static bool IsUnreadableMethod(string member)
	{
		var text = Trim(member);
		var isAnnotation = text.StartsWith("<<", StringComparison.Ordinal) && text.EndsWith(">>", StringComparison.Ordinal);
		return !isAnnotation && text.IndexOf(')', StringComparison.Ordinal) > 0 && Method(text) is null;
	}

	/// <summary>
	/// <c>parseMember</c> of a method, <c>/([#+~-])?(.+)\((.*)\)([\s$*])?(.*)([$*])?/</c>: the name runs to the last
	/// <c>(</c> before the last <c>)</c>, the parameters to that <c>)</c>, and the return type to the end. <c>null</c>
	/// when no <c>(</c> after the first character comes before a <c>)</c>.
	/// </summary>
	public static ClassMemberText? Method(string input)
	{
		// The member is one line, so . matches every character of it.
		var close = input.LastIndexOf(')');
		var open = close < 0 ? -1 : input.LastIndexOf('(', close);
		if (open < 1)
		{
			return null;
		}

		var member = new ClassMemberText(isMethod: true);
		var nameStart = 0;
		if (open >= 2 && input[0] is '#' or '+' or '~' or '-')
		{
			member.Visibility = input[..1];
			nameStart = 1;
		}

		member.Id = input[nameStart..open];
		member.Parameters = Trim(input[(open + 1)..close]);
		var rest = input[(close + 1)..];
		var classifier = "";
		if (rest.Length > 0 && (ClassLexer.IsSpace(rest[0]) || rest[0] is '$' or '*'))
		{
			classifier = Trim(rest[..1]);
			rest = rest[1..];
		}

		member.ReturnType = Trim(rest);
		if (classifier.Length == 0 && member.ReturnType.Length > 0 && member.ReturnType[^1] is '$' or '*')
		{
			classifier = member.ReturnType[^1..];
			member.ReturnType = member.ReturnType[..^1];
		}

		member.Classifier = classifier;
		member.Finish();
		return member;
	}

	/// <summary><c>parseMember</c> of an attribute: a visibility first and a classifier last, each one character.</summary>
	public static ClassMemberText Attribute(string input)
	{
		var member = new ClassMemberText(isMethod: false);
		if (input.Length > 0 && input[0] is '#' or '+' or '~' or '-')
		{
			member.Visibility = input[..1];
		}

		if (input.Length > 0 && input[^1] is '$' or '*')
		{
			member.Classifier = input[^1..];
		}

		var start = member.Visibility.Length;
		var end = input.Length - member.Classifier.Length;
		member.Id = end > start ? input[start..end] : "";
		member.Finish();
		return member;
	}

	private void Finish()
	{
		// Preserve one space only.
		Id = Id.StartsWith(' ') ? " " + Trim(Id) : Trim(Id);
		var text = new StringBuilder();
		if (Visibility.Length > 0)
		{
			text.Append('\\').Append(Visibility);
		}

		text.Append(GenericTypes(Id));
		if (IsMethod)
		{
			text.Append('(').Append(GenericTypes(Parameters)).Append(')');
			if (ReturnType.Length > 0)
			{
				text.Append(" : ").Append(GenericTypes(ReturnType));
			}
		}

		var combined = text.Replace("<", "&lt;").Replace(">", "&gt;").ToString();
		if (combined.StartsWith("\\&lt;", StringComparison.Ordinal))
		{
			combined = string.Concat("~", combined.AsSpan(5));
		}

		Text = combined;
	}

	/// <summary>
	/// <c>parseGenericTypes</c> of <c>common.ts</c>: the text between commas, joined again where a comma splits a pair
	/// of <c>~</c>, with each pair of <c>~</c> from the outside in read as <c>&lt;</c> and <c>&gt;</c>.
	/// </summary>
	public static string GenericTypes(string input)
	{
		if (!input.Contains('~', StringComparison.Ordinal))
		{
			return input;
		}

		// input.split(/(,)/) keeps each comma as a set of its own.
		var sets = new List<string>();
		var start = 0;
		for (var i = 0; i < input.Length; i++)
		{
			if (input[i] == ',')
			{
				sets.Add(input[start..i]);
				sets.Add(",");
				start = i + 1;
			}
		}

		sets.Add(input[start..]);
		var output = new List<string>();
		for (var i = 0; i < sets.Count; i++)
		{
			var set = sets[i];
			if (set == "," && i > 0 && i + 1 < sets.Count && Count(sets[i - 1]) == 1 && Count(sets[i + 1]) == 1)
			{
				set = sets[i - 1] + "," + sets[i + 1];
				i++;
				output.RemoveAt(output.Count - 1);
			}

			output.Add(ProcessSet(set));
		}

		return string.Concat(output);

		static int Count(string text) => text.AsSpan().Count('~');
	}

	private static string ProcessSet(string input)
	{
		var count = input.AsSpan().Count('~');
		if (count <= 1)
		{
			return input;
		}

		var startingTilde = false;
		if (count % 2 != 0 && input.StartsWith('~'))
		{
			input = input[1..];
			startingTilde = true;
		}

		var chars = input.ToCharArray();
		var first = Array.IndexOf(chars, '~');
		var last = Array.LastIndexOf(chars, '~');
		while (first != -1 && last != -1 && first != last)
		{
			chars[first] = '<';
			chars[last] = '>';
			first = Array.IndexOf(chars, '~');
			last = Array.LastIndexOf(chars, '~');
		}

		return startingTilde ? "~" + new string(chars) : new string(chars);
	}

	/// <summary>The class box's style for the classifier: italic for abstract and underlined for static.</summary>
	public string ClassifierStyle => Classifier switch
	{
		"*" => "font-style:italic;",
		"$" => "text-decoration:underline;",
		_ => "",
	};

	/// <summary>JavaScript's <c>trim</c>.</summary>
	public static string Trim(string text)
	{
		var trimmed = ClassLexer.Trim(text);
		return trimmed.Length == text.Length ? text : trimmed.ToString();
	}
}
