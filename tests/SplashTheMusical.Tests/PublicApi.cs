using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace SplashTheMusical.Tests;

/// <summary>
/// Writes the public API of an assembly as text: each public type with its base types, and each public or protected
/// member as a C# declaration with its nullability. Types and members are sorted, so the text changes only when the API
/// does.
/// </summary>
internal static class PublicApi
{
	private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

	public static string Write(System.Reflection.Assembly assembly)
	{
		var text = new StringBuilder();
		text.Append("# The public API of ").Append(assembly.GetName().Name).Append(". PublicApiTests fails when it is out of date.\n");
		foreach (var group in assembly.GetExportedTypes().GroupBy(t => t.Namespace ?? "").OrderBy(g => g.Key, StringComparer.Ordinal))
		{
			text.Append('\n').Append("namespace ").Append(group.Key).Append('\n');
			foreach (var type in group.OrderBy(t => TypeName(t, null), StringComparer.Ordinal))
			{
				text.Append(Header(type)).Append('\n');
				foreach (var member in Members(type).Order(StringComparer.Ordinal))
				{
					text.Append("  ").Append(member).Append('\n');
				}
			}
		}

		return text.ToString();
	}

	private static string Header(Type type)
	{
		var name = TypeName(type, null);
		if (type.IsEnum)
		{
			return $"public enum {name} : {TypeName(Enum.GetUnderlyingType(type), null)}";
		}

		if (type.IsInterface)
		{
			return $"public interface {name}{Bases(type, null)}";
		}

		if (typeof(Delegate).IsAssignableFrom(type))
		{
			var invoke = type.GetMethod("Invoke")!;
			return $"public delegate {TypeName(invoke.ReturnType, Nullability(invoke.ReturnParameter))} {name}({Parameters(invoke.GetParameters())})";
		}

		var isRecord = type.GetMethod("<Clone>$") is not null || (type.IsValueType && type.GetMethod("PrintMembers", Declared) is not null);
		var kind = type.IsValueType ? (isRecord ? "record struct" : "struct") : isRecord ? "record" : "class";
		var modifiers = type switch
		{
			{ IsValueType: true } when type.IsDefined(typeof(IsReadOnlyAttribute)) => "readonly ",
			{ IsValueType: true } => "",
			{ IsAbstract: true, IsSealed: true } => "static ",
			{ IsAbstract: true } => "abstract ",
			{ IsSealed: true } => "sealed ",
			_ => "",
		};
		if (type.IsValueType && type.IsByRefLike)
		{
			modifiers += "ref ";
		}

		var baseType = type.BaseType is { } b && b != typeof(object) && b != typeof(ValueType) ? b : null;
		return $"public {modifiers}{kind} {name}{Bases(type, baseType)}";
	}

	private static string Bases(Type type, Type? baseType)
	{
		var inherited = baseType?.GetInterfaces() ?? [];
		var interfaces = type.GetInterfaces()
			.Where(i => !inherited.Contains(i) && (i.IsPublic || i.IsNestedPublic))
			.Where(i => !type.GetInterfaces().Any(other => other != i && other.GetInterfaces().Contains(i)))
			.Select(i => TypeName(i, null))
			.Order(StringComparer.Ordinal);
		var bases = (baseType is null ? [] : new[] { TypeName(baseType, null) }).Concat(interfaces).ToList();
		return bases.Count == 0 ? "" : " : " + string.Join(", ", bases);
	}

	private static IEnumerable<string> Members(Type type)
	{
		if (type.IsEnum)
		{
			foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				yield return $"{field.Name} = {Convert.ToString(field.GetRawConstantValue(), CultureInfo.InvariantCulture)}";
			}

			yield break;
		}

		if (typeof(Delegate).IsAssignableFrom(type))
		{
			yield break;
		}

		foreach (var member in type.GetMembers(Declared))
		{
			if (member.Name.Contains('<', StringComparison.Ordinal) || member.IsDefined(typeof(CompilerGeneratedAttribute)))
			{
				continue;
			}

			var line = member switch
			{
				ConstructorInfo c when Visible(c) && !c.IsStatic => $"{Access(c)} {TypeName(type, null).Split('<')[0]}({Parameters(c.GetParameters())})",
				MethodInfo m when Visible(m) && (!m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal)) => Method(type, m),
				PropertyInfo p when Accessors(p).Any(Visible) => Property(type, p),
				FieldInfo f when Visible(f) => Field(f),
				EventInfo e when e.AddMethod is { } add && Visible(add) => $"{Access(add)} {Modifiers(type, add)}event {TypeName(e.EventHandlerType!, null)} {e.Name}",
				_ => null,
			};
			if (line is not null)
			{
				yield return line;
			}
		}
	}

	private static string Method(Type type, MethodInfo method)
	{
		var generic = method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">" : "";
		var name = method.Name.Contains('.', StringComparison.Ordinal) ? method.Name : method.Name + generic;
		return $"{Access(method)} {Modifiers(type, method)}{TypeName(method.ReturnType, Nullability(method.ReturnParameter))} {name}({Parameters(method.GetParameters())})";
	}

	private static string Property(Type type, PropertyInfo property)
	{
		var accessor = Accessors(property).First(Visible);
		var nullability = new NullabilityInfoContext().Create(property);
		var name = property.GetIndexParameters() is { Length: > 0 } index ? $"this[{Parameters(index)}]" : property.Name;
		var parts = new List<string>();
		if (property.GetMethod is { } get && Visible(get))
		{
			parts.Add(Access(get) == Access(accessor) ? "get;" : Access(get) + " get;");
		}

		if (property.SetMethod is { } set && Visible(set))
		{
			var init = set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) ? "init;" : "set;";
			parts.Add(Access(set) == Access(accessor) ? init : Access(set) + " " + init);
		}

		var required = property.IsDefined(typeof(RequiredMemberAttribute)) ? "required " : "";
		return $"{Access(accessor)} {required}{Modifiers(type, accessor)}{TypeName(property.PropertyType, nullability)} {name} {{ {string.Join(' ', parts)} }}";
	}

	private static string Field(FieldInfo field)
	{
		var type = TypeName(field.FieldType, new NullabilityInfoContext().Create(field));
		if (field.IsLiteral)
		{
			return $"{Access(field)} const {type} {field.Name} = {Literal(field.GetRawConstantValue())}";
		}

		var modifiers = (field.IsStatic ? "static " : "") + (field.IsInitOnly ? "readonly " : "");
		return $"{Access(field)} {modifiers}{type} {field.Name}";
	}

	private static IEnumerable<MethodInfo> Accessors(PropertyInfo property) =>
		new[] { property.GetMethod, property.SetMethod }.OfType<MethodInfo>();

	private static bool Visible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

	private static bool Visible(FieldInfo field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

	private static string Access(MethodBase method) => method.IsPublic ? "public" : "protected";

	private static string Access(FieldInfo field) => field.IsPublic ? "public" : "protected";

	private static string Modifiers(Type type, MethodInfo method)
	{
		if (method.IsStatic)
		{
			return "static ";
		}

		if (type.IsInterface)
		{
			return "";
		}

		if (method.IsAbstract)
		{
			return "abstract ";
		}

		var isOverride = method.GetBaseDefinition().DeclaringType != method.DeclaringType;
		if (isOverride)
		{
			return method.IsFinal ? "sealed override " : "override ";
		}

		return method.IsVirtual && !method.IsFinal ? "virtual " : "";
	}

	private static string Parameters(ParameterInfo[] parameters) =>
		string.Join(", ", parameters.Select(Parameter));

	private static string Parameter(ParameterInfo parameter)
	{
		var type = parameter.ParameterType;
		var modifier = "";
		if (type.IsByRef)
		{
			modifier = parameter.IsOut ? "out " : parameter.IsDefined(typeof(IsReadOnlyAttribute)) ? "in " : "ref ";
		}
		else if (parameter.IsDefined(typeof(ParamArrayAttribute)) || parameter.IsDefined(typeof(ParamCollectionAttribute)))
		{
			modifier = "params ";
		}

		if (parameter.IsDefined(typeof(ScopedRefAttribute)))
		{
			modifier = "scoped " + modifier;
		}

		var text = $"{modifier}{TypeName(type, Nullability(parameter))} {parameter.Name}";
		return parameter.HasDefaultValue ? text + " = " + Literal(parameter.RawDefaultValue) : text;
	}

	private static NullabilityInfo? Nullability(ParameterInfo parameter)
	{
		try
		{
			return new NullabilityInfoContext().Create(parameter);
		}
		catch (NotSupportedException)
		{
			return null;
		}
	}

	private static string Literal(object? value) => value switch
	{
		null => "null",
		string s => "\"" + s.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\"",
		bool b => b ? "true" : "false",
		char c => "'" + c + "'",
		IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		_ => value.ToString() ?? "",
	};

	private static string TypeName(Type type, NullabilityInfo? nullability)
	{
		if (type.IsByRef)
		{
			return TypeName(type.GetElementType()!, nullability?.ElementType ?? nullability);
		}

		if (type.IsArray)
		{
			return TypeName(type.GetElementType()!, nullability?.ElementType) + "[]" + Question(type, nullability);
		}

		if (Nullable.GetUnderlyingType(type) is { } underlying)
		{
			return TypeName(underlying, null) + "?";
		}

		if (type.IsGenericParameter)
		{
			return type.Name + Question(type, nullability);
		}

		var alias = Type.GetTypeCode(type) switch
		{
			TypeCode.Boolean => "bool",
			TypeCode.Byte => "byte",
			TypeCode.Char => "char",
			TypeCode.Double => "double",
			TypeCode.Int16 => "short",
			TypeCode.Int32 => "int",
			TypeCode.Int64 => "long",
			TypeCode.Single => "float",
			TypeCode.String => "string",
			_ when type == typeof(object) => "object",
			_ when type == typeof(void) => "void",
			_ => null,
		};
		if (alias is not null && !type.IsEnum)
		{
			return alias + Question(type, nullability);
		}

		var name = type.IsNested && !type.IsGenericParameter ? TypeName(type.DeclaringType!, null).Split('<')[0] + "." + type.Name : type.Name;
		var tick = name.IndexOf('`', StringComparison.Ordinal);
		if (tick >= 0)
		{
			name = name[..tick];
		}

		if (type.IsGenericType)
		{
			var arguments = type.GetGenericArguments();
			var argumentNullability = nullability?.GenericTypeArguments;
			name += "<" + string.Join(", ", arguments.Select((a, i) => TypeName(a, argumentNullability is { Length: > 0 } n && i < n.Length ? n[i] : null))) + ">";
		}

		return name + Question(type, nullability);
	}

	private static string Question(Type type, NullabilityInfo? nullability) =>
		!type.IsValueType && nullability is { ReadState: NullabilityState.Nullable } ? "?" : "";
}
