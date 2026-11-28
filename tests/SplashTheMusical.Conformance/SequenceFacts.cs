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

	/// <summary>
	/// The extractor's sequence boxes from the module's layout: each participant's top drawing and the shape at its
	/// bottom, each titled box, and each message of the ten arrow types the extractor knows with the last line of its
	/// text, named by its ends, each note by its text, and each block <c>drawLoop</c> draws by its kind and text. The
	/// canvas is the viewBox.
	/// </summary>
	public static string Boxes(List<string> boxes, SequenceLayout layout)
	{
		var names = new Dictionary<string, int>(StringComparer.Ordinal);
		void Add(string kind, string name, SequenceRect rect)
		{
			var key = kind + "\t" + Norm(name);
			var n = names.GetValueOrDefault(key) + 1;
			names[key] = n;
			boxes.Add(StmFactsProvider.Box(kind, n > 1 ? $"{Norm(name)} #{n}" : name, rect.X, rect.Y, rect.Width, rect.Height));
		}

		foreach (var actor in layout.Actors)
		{
			var id = Encode(actor.Name);
			Add("actor", id, actor.Top.Bounds);
			if (actor.Bottom is { } bottom && BottomBox(actor.Type, bottom) is { } rect)
			{
				Add("actor-bottom", id, rect);
			}
		}

		foreach (var box in layout.Boxes)
		{
			var name = Decode(box.Name ?? "");
			if (name.Any(char.IsLetterOrDigit))
			{
				Add("box", name, box.Rect);
			}
		}

		foreach (var message in layout.Messages)
		{
			if (Signal(message.Type) is null)
			{
				continue;
			}

			var name = $"{Encode(message.From)} --> {Encode(message.To)}";
			Add("message", name, message.Line);
			if (message.Text.Count > 0)
			{
				Add("message-text", name, message.Text[^1].Box);
			}
		}

		foreach (var note in layout.Notes)
		{
			Add("note", Decode(note.Text), note.Bounds);
		}

		foreach (var block in layout.Blocks)
		{
			if (Block(block.StartType) is { } kind)
			{
				Add("block", $"{kind} {Decode(block.Text)}".Trim(), block.Bounds);
			}
		}

		var viewBox = layout.ViewBox!.Value;
		return string.Join('\t', "canvas", StmFactsProvider.Number(viewBox.X), StmFactsProvider.Number(viewBox.Y), StmFactsProvider.Number(viewBox.Width), StmFactsProvider.Number(viewBox.Height));
	}

	/// <summary>
	/// What <c>.actor-bottom[name]</c> finds at the bottom: the rectangle of a participant or collections, the group of
	/// an icon, and nothing for a queue or a database, whose bottom drawing has no <c>name</c>.
	/// </summary>
	private static SequenceRect? BottomBox(string type, SequenceActorDrawing bottom) => type switch
	{
		"queue" or "database" => null,
		"participant" or "collections" => bottom.Shape,
		_ => bottom.Bounds,
	};

	private static string Norm(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
