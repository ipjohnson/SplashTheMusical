using System.Collections;
using System.Globalization;
using System.Reflection;

namespace SplashTheMusical.Tests.Generation;

/// <summary>
/// Every public property of a document, and of the model objects it holds, as lines, so two documents of any diagram
/// type compare line by line and a difference names its path. It reads the model by reflection, which the test
/// project may do, so a new document type needs no dump of its own.
/// </summary>
public static class DocumentDump
{
	public static List<string> Lines(MermaidDocument document)
	{
		var lines = new List<string>();
		Append(lines, document.GetType().Name, document, 0);
		return lines;
	}

	/// <summary>The first line that differs, with both values, or <c>null</c> when the dumps are equal.</summary>
	public static string? FirstDifference(List<string> expected, List<string> actual)
	{
		for (var i = 0; i < Math.Max(expected.Count, actual.Count); i++)
		{
			var e = i < expected.Count ? expected[i] : "(none)";
			var a = i < actual.Count ? actual[i] : "(none)";
			if (e != a)
			{
				return $"expected {e}\n  actual {a}";
			}
		}

		return null;
	}

	private static void Append(List<string> lines, string path, object? value, int depth)
	{
		if (depth > 16)
		{
			throw new InvalidOperationException($"{path} is nested more than 16 levels deep, which a document model is not.");
		}

		switch (value)
		{
			case null:
				lines.Add(path + " = null");
				return;
			case string text:
				lines.Add(path + " = " + Quote(text));
				return;
			case double number:
				lines.Add(path + " = " + number.ToString("R", CultureInfo.InvariantCulture));
				return;
			case MermaidValue mermaidValue:
				lines.Add(path + " = " + Quote(mermaidValue.ToString() ?? ""));
				return;
			case IFormattable formattable:
				lines.Add(path + " = " + formattable.ToString(null, CultureInfo.InvariantCulture));
				return;
			case bool or Enum:
				lines.Add(path + " = " + value);
				return;
			case IEnumerable items:
				var index = 0;
				foreach (var item in items)
				{
					Append(lines, $"{path}[{index++}]", item, depth + 1);
				}

				lines.Add($"{path}.Count = {index}");
				return;
		}

		var type = value.GetType();
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
		{
			Append(lines, path + ".Key", type.GetProperty("Key")!.GetValue(value), depth + 1);
			Append(lines, path + ".Value", type.GetProperty("Value")!.GetValue(value), depth + 1);
			return;
		}

		if (type.Namespace?.StartsWith("SplashTheMusical", StringComparison.Ordinal) != true)
		{
			throw new InvalidOperationException($"{path} is a {type}, which the dump does not know how to compare.");
		}

		foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
		{
			if (property.GetIndexParameters().Length == 0)
			{
				Append(lines, path + "." + property.Name, property.GetValue(value), depth + 1);
			}
		}
	}

	/// <summary>The text with its control characters escaped, so a line break in a value stays on one line.</summary>
	private static string Quote(string text)
	{
		var builder = new System.Text.StringBuilder("\"");
		foreach (var c in text)
		{
			_ = c switch
			{
				'\n' => builder.Append("\\n"),
				'\r' => builder.Append("\\r"),
				'\t' => builder.Append("\\t"),
				'"' => builder.Append("\\\""),
				'\\' => builder.Append("\\\\"),
				_ when char.IsControl(c) => builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)),
				_ => builder.Append(c),
			};
		}

		return builder.Append('"').ToString();
	}
}
