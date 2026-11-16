namespace SplashTheMusical.Config;

/// <summary>
/// The key names a directive may set: every key name at any depth of mermaid.js's default config, apart from keys whose
/// value is an array.
/// </summary>
internal static partial class ConfigKeys
{
	public static bool Contains(string name) => Array.BinarySearch(Names, name, StringComparer.Ordinal) >= 0;

	internal static IReadOnlyList<string> All => Names;
}
