using System.Diagnostics;
using System.Text;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>tools/oracle/elk-probe.mjs --serve</c> under the <c>node</c> on the path, started on the first request. A graph
/// the probe takes longer than the timeout on stops the process, and the next request starts another.
/// </summary>
internal sealed class ElkProbeProcess(string repositoryRoot, TimeSpan timeout) : IDisposable
{
	private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(1);

	private Process? _process;

	/// <summary>
	/// ELK's layout of the first layout of a graph file, with the input the probe gave ELK. Null with an
	/// <paramref name="error"/> when elkjs refuses the graph or the probe times out.
	/// </summary>
	public ElkRecord? Layout(string graph, out string? error)
	{
		var process = _process ??= Start();
		process.StandardInput.Write(Convert.ToBase64String(Encoding.UTF8.GetBytes(graph)) + "\n");
		process.StandardInput.Flush();
		var lines = new List<string>();
		while (ReadLine(process, timeout) is { } line)
		{
			if (line == ".")
			{
				if (lines.FirstOrDefault(l => l.StartsWith("# error ", StringComparison.Ordinal)) is { } message)
				{
					error = message["# error ".Length..];
					return null;
				}

				error = null;
				return ElkRecords.Parse(lines, "elk-probe").FirstOrDefault();
			}

			lines.Add(line);
		}

		Stop(kill: true);
		error = $"the probe gave no answer within {timeout.TotalSeconds} seconds";
		return null;
	}

	public void Dispose() => Stop(kill: false);

	private Process Start()
	{
		var info = new ProcessStartInfo("node", [Path.Combine(repositoryRoot, "tools", "oracle", "elk-probe.mjs"), "--serve"])
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
			throw new InvalidOperationException("tools/oracle/elk-probe.mjs did not start. Run npm ci --prefix tools/oracle.");
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
