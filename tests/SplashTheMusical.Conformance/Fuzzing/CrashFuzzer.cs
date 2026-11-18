using System.Collections.Concurrent;
using System.Diagnostics;

namespace SplashTheMusical.Conformance.Fuzzing;

/// <param name="Index">The index of the source in the run, which with the run's seed makes it again.</param>
/// <param name="Problem">The exception, or how long the source took.</param>
public sealed record CrashFind(string Type, long Index, string Source, string Problem);

public sealed record CrashRun(string Type, long Sources, IReadOnlyList<CrashFind> Finds);

/// <summary>
/// Section 6's fuzz job: feeds mutated corpus cases to a target on several threads, and keeps each source on which it
/// throws anything but <see cref="MermaidParseException"/>, takes longer than the deadline, or does not return. A source
/// that runs past twice the deadline ends the run, since its thread cannot be stopped.
/// </summary>
public static class CrashFuzzer
{
	/// <summary>The most finds a run keeps before it stops.</summary>
	public const int MaxFinds = 20;

	/// <summary>STM's parser and renderer under <see cref="ResourceLimits.Default"/>.</summary>
	public static Action<string> Stm()
	{
		var stm = new Mermaid(new MermaidOptions { Limits = ResourceLimits.Default });
		return source =>
		{
			if (stm.TryParse(source, out var document, out _))
			{
				_ = stm.RenderSvg(document!);
			}
		};
	}

	/// <param name="targets">Makes the action that runs one source. Each thread makes its own.</param>
	public static CrashRun Run(string type, IReadOnlyList<string> seeds, long count, long seed, Func<Action<string>> targets, TimeSpan deadline, int threads)
	{
		ArgumentOutOfRangeException.ThrowIfZero(seeds.Count);
		var finds = new ConcurrentQueue<CrashFind>();
		var next = -1L;
		var stop = false;
		var started = new long[threads];
		var running = new long[threads];
		var workers = new Thread[threads];
		for (var w = 0; w < threads; w++)
		{
			var worker = w;
			Volatile.Write(ref running[worker], -1);
			workers[w] = new Thread(() =>
			{
				var target = targets();
				for (var index = Interlocked.Increment(ref next); index < count && !Volatile.Read(ref stop); index = Interlocked.Increment(ref next))
				{
					var source = Source(seeds, seed, index);
					Volatile.Write(ref started[worker], Stopwatch.GetTimestamp());
					Volatile.Write(ref running[worker], index);
					string? problem = null;
					try
					{
						target(source);
					}
					catch (MermaidParseException)
					{
					}
					catch (Exception e) when (e is not OutOfMemoryException)
					{
						problem = e.GetType().FullName + ": " + e.Message + "\n" + e.StackTrace;
					}

					var elapsed = Stopwatch.GetElapsedTime(Volatile.Read(ref started[worker]));
					Volatile.Write(ref running[worker], -1);
					if (problem is null && elapsed > deadline)
					{
						problem = $"took {elapsed.TotalSeconds:0.000} s, past the {deadline.TotalSeconds} s deadline";
					}

					if (problem is not null)
					{
						finds.Enqueue(new CrashFind(type, index, source, problem));
						if (finds.Count >= MaxFinds)
						{
							Volatile.Write(ref stop, true);
						}
					}
				}
			})
			{
				IsBackground = true,
				Name = $"fuzz {type} {w}",
			};
			workers[w].Start();
		}

		while (workers.Any(t => t.IsAlive))
		{
			_ = workers[0].Join(TimeSpan.FromMilliseconds(100));
			for (var w = 0; w < threads; w++)
			{
				var index = Volatile.Read(ref running[w]);
				if (index >= 0 && Stopwatch.GetElapsedTime(Volatile.Read(ref started[w])) > deadline * 2 && Volatile.Read(ref running[w]) == index)
				{
					finds.Enqueue(new CrashFind(type, index, Source(seeds, seed, index), $"did not return within {(deadline * 2).TotalSeconds} s"));
					Volatile.Write(ref stop, true);
					return new CrashRun(type, Math.Min(Volatile.Read(ref next), count), [.. finds]);
				}
			}
		}

		return new CrashRun(type, Math.Min(Volatile.Read(ref next), count), [.. finds.OrderBy(f => f.Index).Take(MaxFinds)]);
	}

	/// <summary>Source <paramref name="index"/> of a run with <paramref name="seed"/>.</summary>
	public static string Source(IReadOnlyList<string> seeds, long seed, long index)
	{
		var random = FuzzRandom.For(seed, index);
		return Mutator.Mutate(random.Pick(seeds), random);
	}
}
