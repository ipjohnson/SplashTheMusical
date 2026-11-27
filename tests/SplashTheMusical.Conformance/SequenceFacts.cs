using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Diagrams.Sequence;

namespace SplashTheMusical.Conformance;

/// <summary>
/// <c>extract.mjs</c>'s sequence extractor over <see cref="SequenceDb"/>, the port of <c>SequenceDB</c>: each
/// participant with its description and type, each box with its name and sorted members, then the db's messages in
/// order: each signal of the ten arrow types the extractor knows with its line and head, each note with its placement
/// and participants, each block start with its kind and text, and each divider. Ids are the db's, with the
/// placeholders <c>encodeEntities</c> gives entity codes. Other text is what the extractor's <c>decode</c> makes of the
/// db's: entity codes and HTML read as HTML.
/// </summary>
internal static class SequenceFacts
{
	public static void Add(List<string> facts, SequenceDocument document)
	{
		var db = SequenceDb.From(document);
		foreach (var actor in db.Actors)
		{
			facts.Add(ConfigLines.Line("actor", Encode(actor.Name), Decode(actor.Description), actor.Type));
		}

		foreach (var box in db.Boxes)
		{
			var keys = box.ActorKeys.Select(Encode).ToList();
			keys.Sort(StringComparer.Ordinal);
			facts.Add(ConfigLines.Line("box", Decode(box.Name ?? ""), string.Join(',', keys)));
		}

		foreach (var message in db.Messages)
		{
			if (Signal(message.Type) is var (line, head))
			{
				facts.Add(ConfigLines.Line("message", Encode(message.From!), Encode(message.To!), line, head, Decode(message.Text)));
			}
			else if (message.Type == SequenceLineType.Note)
			{
				var actors = message.From == message.To ? Encode(message.From!) : Encode(message.From!) + "," + Encode(message.To!);
				var placement = message.Placement switch
				{
					SequenceNotePlacement.LeftOf => "left",
					SequenceNotePlacement.RightOf => "right",
					_ => "over",
				};
				facts.Add(ConfigLines.Line("note", placement, actors, Decode(message.Text)));
			}
			else if (Block(message.Type) is { } kind)
			{
				facts.Add(ConfigLines.Line("block", kind, message.Type == SequenceLineType.RectStart ? "" : Decode(message.Text)));
			}
			else if (message.Type is SequenceLineType.AltElse or SequenceLineType.ParAnd or SequenceLineType.CriticalOption)
			{
				facts.Add(ConfigLines.Line("divider", Decode(message.Text)));
			}
		}
	}

	/// <summary>The extractor's <c>message</c> table: the line and the head of each arrow type it knows.</summary>
	private static (string Line, string Head)? Signal(int type) => (SequenceArrow)type switch
	{
		SequenceArrow.Solid => ("solid", "filled"),
		SequenceArrow.Dotted => ("dashed", "filled"),
		SequenceArrow.SolidCross => ("solid", "cross"),
		SequenceArrow.DottedCross => ("dashed", "cross"),
		SequenceArrow.SolidOpen => ("solid", "none"),
		SequenceArrow.DottedOpen => ("dashed", "none"),
		SequenceArrow.SolidPoint => ("solid", "open"),
		SequenceArrow.DottedPoint => ("dashed", "open"),
		SequenceArrow.BidirectionalSolid => ("solid", "filled-both"),
		SequenceArrow.BidirectionalDotted => ("dashed", "filled-both"),
		_ => null,
	};

	/// <summary>The extractor's <c>blockStart</c> table.</summary>
	private static string? Block(int type) => type switch
	{
		SequenceLineType.LoopStart => "loop",
		SequenceLineType.AltStart => "alt",
		SequenceLineType.OptStart => "opt",
		SequenceLineType.ParStart or SequenceLineType.ParOverStart => "par",
		SequenceLineType.RectStart => "rect",
		SequenceLineType.CriticalStart => "critical",
		SequenceLineType.BreakStart => "break",
		_ => null,
	};

	private static string Decode(string text) => FlowchartLabels.HtmlTextOf(text);

	private static string Encode(string text) => SequenceWriter.Encode(text);
}
