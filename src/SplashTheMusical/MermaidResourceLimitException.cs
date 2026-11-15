using System.Globalization;

namespace SplashTheMusical;

/// <summary>Thrown when a diagram exceeds one of its <see cref="ResourceLimits"/>.</summary>
public sealed class MermaidResourceLimitException : MermaidParseException
{
	public MermaidResourceLimitException(string limitName, long observedValue, long limitValue, int line = 0, int column = 0)
		: base(new MermaidDiagnostic(DiagnosticSeverity.Error, MessageFor(limitName, observedValue, limitValue), line, column))
	{
		LimitName = limitName;
		ObservedValue = observedValue;
		LimitValue = limitValue;
	}

	/// <summary>The name of the <see cref="ResourceLimits"/> property that was exceeded.</summary>
	public string LimitName { get; }

	public long ObservedValue { get; }

	public long LimitValue { get; }

	private static string MessageFor(string limitName, long observed, long limit) =>
		string.Create(
			CultureInfo.InvariantCulture,
			$"Resource limit exceeded. {limitName}: {observed:N0} > {limit:N0}. Raise the limit, or use ResourceLimits.Unlimited for trusted input.");
}
