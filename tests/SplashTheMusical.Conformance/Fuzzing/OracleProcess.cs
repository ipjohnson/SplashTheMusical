using System.Diagnostics;
using System.Text;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <summary>
/// <c>tools/oracle/serve.mjs</c> under the <c>node</c> on the path, started on the first request. A source the oracle
/// takes longer than the timeout on stops the process, and the next request starts another.
/// </summary>
public sealed class OracleProcess(string repositoryRoot, TimeSpan timeout) : IDisposable
{
	/// <summary>How long the oracle may take to start Chromium and load mermaid.js.</summary>
	private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);

	private Process? _process;

	/// <summary>The oracle's facts of the source, or null when it has no extractor for its type, fails or times out.</summary>
	/// <param name="name">The case's name, which seeds the page's <c>Math.random</c>.</param>
	public CaseFacts? Facts(string name, string source)
	{
		var process = _process ??= Start();
		process.StandardInput.Write(name + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(source)) + "\n");
		process.StandardInput.Flush();
		var lines = new List<string>();
		while (ReadLine(process, timeout) is { } line)
		{
			if (line == ".")
			{
				return Parse(lines);
			}

			lines.Add(line);
		}

		Stop(kill: true);
		return null;
	}

	/// <summary>The facts of the lines of a facts file, or null for serve.mjs's <c>unknown-type</c> and <c>oracle-error</c> answers.</summary>
	public static CaseFacts? Parse(IReadOnlyList<string> lines)
	{
		var accepted = false;
		string? error = null;
		var facts = new List<string>();
		foreach (var line in lines)
		{
			if (line.StartsWith("# unknown-type ", StringComparison.Ordinal) || line.StartsWith("# oracle-error ", StringComparison.Ordinal))
			{
				return null;
			}

			if (line.StartsWith("# error ", StringComparison.Ordinal))
			{
				error = line["# error ".Length..];
			}
			else if (line == "accept\tyes")
			{
				accepted = true;
			}
			else if (line.Length > 0 && line[0] != '#' && line != "accept\tno")
			{
				facts.Add(line);
			}
		}

		return new CaseFacts(accepted, error, facts, []);
	}

	public void Dispose() => Stop(kill: false);

	private Process Start()
	{
		var info = new ProcessStartInfo("node", [Path.Combine(repositoryRoot, "tools", "oracle", "serve.mjs")])
		{
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			StandardInputEncoding = new UTF8Encoding(false),
			StandardOutputEncoding = Encoding.UTF8,
			UseShellExecute = false,
			WorkingDirectory = repositoryRoot,
		};
		var process = Process.Start(info) ?? throw new InvalidOperationException("node did not start.");
		if (ReadLine(process, StartTimeout) != "ready")
		{
			process.Kill(entireProcessTree: true);
			process.Dispose();
			throw new InvalidOperationException("tools/oracle/serve.mjs did not start. Run npm ci --prefix tools/oracle.");
		}

		return process;
	}

	private static string? ReadLine(Process process, TimeSpan limit)
	{
		using var cancel = new CancellationTokenSource(limit);
		try
		{
			return process.StandardOutput.ReadLineAsync(cancel.Token).AsTask().GetAwaiter().GetResult();
		}
		catch (OperationCanceledException)
		{
			return null;
		}
	}

	private void Stop(bool kill)
	{
		if (_process is null)
		{
			return;
		}

		try
		{
			_process.StandardInput.Close();
			if (kill || !_process.WaitForExit(TimeSpan.FromSeconds(10)))
			{
				_process.Kill(entireProcessTree: true);
			}
		}
		catch (Exception e) when (e is IOException or InvalidOperationException)
		{
			_process.Kill(entireProcessTree: true);
		}

		_process.Dispose();
		_process = null;
	}
}
