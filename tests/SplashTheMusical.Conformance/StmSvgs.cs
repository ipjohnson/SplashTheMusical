namespace SplashTheMusical.Conformance;

/// <summary>
/// Writes the SVG a provider draws for each corpus case, for tools/oracle/raster.mjs to rasterize at L4.
/// </summary>
public static class StmSvgs
{
	/// <summary>The environment variable that names the folder <c>mise run stm-svg</c> writes to.</summary>
	public const string FolderVariable = "STM_SVG_DIR";

	/// <summary>
	/// Empties the folder, then writes <c>&lt;name&gt;.svg</c> for each case the provider draws, in the case's subfolder.
	/// </summary>
	/// <returns>The number of SVGs written.</returns>
	public static int Write(string corpus, IFactsProvider provider, string folder)
	{
		if (Directory.Exists(folder))
		{
			Directory.Delete(folder, recursive: true);
		}

		Directory.CreateDirectory(folder);
		var written = 0;
		foreach (var name in CorpusCheck.Cases(corpus))
		{
			if (provider.Facts(CorpusComparison.Source(corpus, name)).Svg is not { } svg)
			{
				continue;
			}

			var path = Path.Combine(folder, name + ".svg");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, svg);
			written++;
		}

		return written;
	}
}
