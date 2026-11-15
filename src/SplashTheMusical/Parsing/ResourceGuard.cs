namespace SplashTheMusical.Parsing;

/// <summary>
/// Enforces <see cref="ResourceLimits"/> during one call. A parser creates it with <see cref="Start"/> and calls
/// <see cref="Statement"/> after each statement, so the limits cost a few comparisons per statement.
/// </summary>
internal struct ResourceGuard
{
	private readonly ResourceLimits _limits;
	private readonly long _startTimestamp;
	private int _statements;

	private ResourceGuard(ResourceLimits limits)
	{
		_limits = limits;
		_startTimestamp = limits.RenderDeadline is null ? 0 : limits.TimeProvider.GetTimestamp();
	}

	public readonly ResourceLimits Limits => _limits;

	/// <summary>Checks <see cref="ResourceLimits.MaxInputLength"/> and starts the deadline.</summary>
	public static ResourceGuard Start(ReadOnlySpan<char> source, ResourceLimits limits)
	{
		if (source.Length > limits.MaxInputLength)
		{
			throw new MermaidResourceLimitException(nameof(ResourceLimits.MaxInputLength), source.Length, limits.MaxInputLength);
		}

		return new ResourceGuard(limits);
	}

	/// <summary>
	/// Counts one statement and checks <see cref="ResourceLimits.MaxLines"/>, <see cref="ResourceLimits.MaxLineLength"/>
	/// for the lines the cursor has reached, and <see cref="ResourceLimits.RenderDeadline"/>.
	/// </summary>
	public void Statement(scoped in SourceCursor cursor)
	{
		_statements++;
		if (_statements > _limits.MaxLines)
		{
			throw Exceeded(nameof(ResourceLimits.MaxLines), _statements, _limits.MaxLines, cursor);
		}

		var longestLine = cursor.LongestLine;
		if (longestLine > _limits.MaxLineLength)
		{
			throw Exceeded(nameof(ResourceLimits.MaxLineLength), longestLine, _limits.MaxLineLength, cursor);
		}

		if (_limits.RenderDeadline is { } deadline)
		{
			var elapsed = _limits.TimeProvider.GetElapsedTime(_startTimestamp);
			if (elapsed > deadline)
			{
				throw Exceeded(
					nameof(ResourceLimits.RenderDeadline),
					(long)elapsed.TotalMilliseconds,
					(long)deadline.TotalMilliseconds,
					cursor);
			}
		}
	}

	public readonly void Elements(int count, scoped in SourceCursor cursor)
	{
		if (count > _limits.MaxElements)
		{
			throw Exceeded(nameof(ResourceLimits.MaxElements), count, _limits.MaxElements, cursor);
		}
	}

	public readonly void Depth(int depth, scoped in SourceCursor cursor)
	{
		if (depth > _limits.MaxRecursionDepth)
		{
			throw Exceeded(nameof(ResourceLimits.MaxRecursionDepth), depth, _limits.MaxRecursionDepth, cursor);
		}
	}

	public static void NodesAfterLayout(int count, ResourceLimits limits)
	{
		if (count > limits.MaxNodesAfterLayout)
		{
			throw new MermaidResourceLimitException(nameof(ResourceLimits.MaxNodesAfterLayout), count, limits.MaxNodesAfterLayout);
		}
	}

	public static void OutputLength(long length, ResourceLimits limits)
	{
		if (length > limits.MaxOutputLength)
		{
			throw new MermaidResourceLimitException(nameof(ResourceLimits.MaxOutputLength), length, limits.MaxOutputLength);
		}
	}

	private static MermaidResourceLimitException Exceeded(string name, long observed, long limit, scoped in SourceCursor cursor) =>
		new(name, observed, limit, cursor.Line, cursor.Column);
}
