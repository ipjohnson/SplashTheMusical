using SplashTheMusical.Config;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// The port of <c>draw</c> in mermaid's <c>sequenceRenderer.ts</c> for participants, boxes and messages:
/// <c>getMaxMessageWidthPerActor</c>, <c>calculateActorMargins</c>, <c>addActorRenderingData</c>,
/// <c>buildMessageModel</c>, <c>boundMessage</c>, the line ends and texts of <c>drawMessage</c>, the footer of
/// <c>drawActors</c>, and the canvas. mermaid.js computes these positions in its renderer and ignores <c>layout</c>.
/// Notes count toward the participants' margins but are not placed, and blocks and activations do not move anything
/// yet, so the messages after them sit higher than mermaid.js draws them.
/// </summary>
/// <remarks>
/// <c>bounds.init</c> merges the whole config into the sequence config, so the top-level <c>fontFamily</c> and
/// <c>fontSize</c>, which have defaults, replace the actor, note and message families and sizes. The measured text is
/// mermaid's, with the entity placeholders of <see cref="SequenceWriter.Encode"/> in it.
/// </remarks>
internal sealed class SequenceLayoutStep
{
	private const double SequenceNumberRadius = 6;
	private const double CentralConnectionBaseOffset = 4;
	private const double CentralConnectionBidirectionalOffset = 6;

	private readonly SequenceConf _conf;
	private readonly ITextMeasurer _measurer;
	private readonly Dictionary<string, ActorModel> _actors = new(StringComparer.Ordinal);
	private readonly List<(string Actor, double StartX, double StopX)> _activations = [];
	private double? _startX;
	private double? _startY;
	private double? _stopX;
	private double? _stopY;
	private double _verticalPos;

	private SequenceLayoutStep(SequenceConf conf, ITextMeasurer measurer)
	{
		_conf = conf;
		_measurer = measurer;
	}

	public static SequenceLayout Run(SequenceDocument document, DiagramRenderContext context)
	{
		var config = context.Config;
		var db = SequenceDb.From(document, config.Wrap ?? config.Sequence.Wrap ?? false);
		var title = document.DiagramTitle ?? document.Title;
		return new SequenceLayoutStep(SequenceConf.From(config), context.TextMeasurer).Draw(db, string.IsNullOrEmpty(title) ? null : title);
	}

	private SequenceLayout Draw(SequenceDb db, string? title)
	{
		var conf = _conf;
		foreach (var actor in db.Actors)
		{
			_actors[actor.Name] = new ActorModel(actor);
		}

		var boxes = db.Boxes.Select(b => new BoxModel(b)).ToList();
		var boxOf = new Dictionary<SequenceDbBox, BoxModel>(ReferenceEqualityComparer.Instance);
		foreach (var box in boxes)
		{
			boxOf[box.Source] = box;
		}

		foreach (var actor in _actors.Values)
		{
			actor.Box = actor.Source.Box is { } source ? boxOf[source] : null;
		}

		var messages = db.Messages.Select(m => new MessageModel(m)).ToList();
		var maxMessageWidthPerActor = MaxMessageWidthPerActor(messages);
		conf.Height = CalculateActorMargins(maxMessageWidthPerActor, boxes);

		var hasBoxes = boxes.Count > 0;
		if (hasBoxes)
		{
			Bump(conf.BoxMargin);
			if (boxes.Any(b => !string.IsNullOrEmpty(b.Name)))
			{
				Bump(boxes[0].TextMaxHeight);
			}
		}

		var actorKeys = _actors.Keys.ToList();
		if (conf.HideUnusedParticipants)
		{
			var used = new HashSet<string>(StringComparer.Ordinal);
			foreach (var message in messages)
			{
				if (message.Source.From is { } from)
				{
					used.Add(from);
				}

				if (message.Source.To is { } to)
				{
					used.Add(to);
				}
			}

			actorKeys = [.. actorKeys.Where(used.Contains)];
		}

		var drawnBoxes = AddActorRenderingData(actorKeys, db.CreatedActors);
		CalculateLoopBounds(messages);

		var sequenceIndex = 1.0;
		var sequenceIndexStep = 1.0;
		var sequenceVisible = false;
		foreach (var message in messages)
		{
			var type = message.Source.Type;
			if (type == SequenceLineType.Autonumber)
			{
				sequenceIndex = message.Source.Start is { } start && start != 0 ? start : sequenceIndex;
				sequenceIndexStep = message.Source.Step is { } step && step != 0 ? step : sequenceIndexStep;
				sequenceVisible = message.Source.Visible;
			}
			else if (IsSignal(type))
			{
				message.StartY = _verticalPos;
				message.SequenceIndex = sequenceIndex;
				message.SequenceVisible = sequenceVisible;
				message.LineY = BoundMessage(message);
			}

			if (IsSignal(type))
			{
				sequenceIndex = Math.Round((sequenceIndex + sequenceIndexStep) * 100, MidpointRounding.AwayFromZero) / 100;
			}
		}

		var placed = new List<SequenceActorPlace>(actorKeys.Count);
		var tops = actorKeys.Select(key => DrawActor(_actors[key], footer: false)).ToList();
		var bottoms = new List<SequenceActorDrawing?>(actorKeys.Count);
		if (conf.MirrorActors)
		{
			var maxHeight = 0.0;
			Bump(conf.BoxMargin * 2);
			foreach (var key in actorKeys)
			{
				var actor = _actors[key];
				actor.StopY ??= _verticalPos;
				var drawing = DrawActor(actor, footer: true);
				bottoms.Add(drawing);
				maxHeight = Math.Max(maxHeight, actor.FooterHeight);
			}

			Bump(maxHeight + conf.BoxMargin);
		}

		for (var i = 0; i < actorKeys.Count; i++)
		{
			var actor = _actors[actorKeys[i]];
			placed.Add(new SequenceActorPlace(
				actor.Name,
				actor.Type,
				actor.X,
				actor.Width,
				actor.Height,
				actor.StartY,
				actor.StopY,
				tops[i],
				conf.MirrorActors ? bottoms[i] : null));
		}

		var messagePlaces = new List<SequenceMessagePlace>();
		foreach (var message in messages)
		{
			if (message.LineY is { } lineY)
			{
				messagePlaces.Add(PlaceMessage(message, lineY));
			}
		}

		var boxPlaces = new List<SequenceBoxPlace>(drawnBoxes.Count);
		foreach (var box in drawnBoxes)
		{
			box.Height = _verticalPos - box.Y;
			Insert(box.X, box.Y, box.X + box.Width, box.Height);
			var padding = conf.BoxMargin * 2;
			var startX = box.X - padding;
			var startY = box.Y - (padding * 0.25);
			var rect = new SequenceRect(startX, startY, box.Width + (2 * padding), box.Height + (padding * 0.75));
			boxPlaces.Add(new SequenceBoxPlace(box.Name, rect, BoxLabel(box)));
		}

		if (hasBoxes)
		{
			Bump(conf.BoxMargin);
		}

		var (popupWidth, popupHeight) = ActorsPopup(actorKeys);
		var boundsStartX = _startX ?? 0;
		var boundsStartY = _startY ?? 0;
		var boundsStopX = _stopX ?? 0;
		var boundsStopY = _stopY ?? 0;
		var height = Math.Max(boundsStopY - boundsStartY, popupHeight) + (2 * conf.DiagramMarginY);
		if (conf.MirrorActors)
		{
			height = height - conf.BoxMargin + conf.BottomMarginAdj;
		}

		var width = Math.Max(boundsStopX - boundsStartX, popupWidth) + (2 * conf.DiagramMarginX);
		var extraVertForTitle = title is null ? 0 : 40;
		var extraHeightForNeoActors = _actors.Count > 0 && conf.Look == "neo" ? 30 : 0;
		var viewBox = new SvgViewBox(
			boundsStartX - conf.DiagramMarginX,
			-(conf.DiagramMarginY + extraVertForTitle),
			width,
			height + extraVertForTitle + extraHeightForNeoActors);
		return new SequenceLayout(width, height, viewBox, conf.Look, placed, boxPlaces, messagePlaces);
	}

	/// <summary>
	/// <c>drawActorsPopup</c>: the widest right edge and the tallest menu of the actors' <c>links</c> menus, which the
	/// canvas grows to hold. A menu is as wide as the actor's rectangle or its widest label, and as tall as the
	/// rectangle and 30 for each link after the first 20.
	/// </summary>
	private (double Width, double Height) ActorsPopup(List<string> actorKeys)
	{
		var conf = _conf;
		var maxWidth = 0.0;
		var maxHeight = 0.0;
		foreach (var key in actorKeys)
		{
			var actor = _actors[key];
			var links = actor.Source.Links;
			if (links.Count == 0)
			{
				continue;
			}

			var minMenuWidth = 0.0;
			foreach (var label in links)
			{
				minMenuWidth = Math.Max(minMenuWidth, Dimensions(label, conf.ActorFont).Width + (2 * conf.WrapPadding) + (2 * conf.BoxMargin));
			}

			var rectHeight = actor.Type == "collections" && conf.Look == "neo" ? actor.Height - 6 : actor.Height;
			var menuWidth = actor.Width > minMenuWidth ? actor.Width : minMenuWidth;
			maxHeight = Math.Max(maxHeight, rectHeight + 20 + (30 * links.Count));
			maxWidth = Math.Max(maxWidth, menuWidth + actor.X);
		}

		return (maxWidth, maxHeight);
	}

	/// <summary><c>getMaxMessageWidthPerActor</c>, keyed as its object is, with <c>undefined</c> for a missing actor.</summary>
	private Dictionary<string, double> MaxMessageWidthPerActor(List<MessageModel> messages)
	{
		var conf = _conf;
		var widths = new Dictionary<string, double>(StringComparer.Ordinal);
		void Max(string? key, double width)
		{
			key ??= "undefined";
			widths[key] = Math.Max(widths.GetValueOrDefault(key), width);
		}

		foreach (var message in messages)
		{
			var msg = message.Source;
			if (msg.To is null || msg.From is null || !_actors.TryGetValue(msg.To, out var actor) || !_actors.ContainsKey(msg.From))
			{
				continue;
			}

			if (msg.Placement == SequenceNotePlacement.LeftOf && actor.PrevActor is null)
			{
				continue;
			}

			if (msg.Placement == SequenceNotePlacement.RightOf && actor.NextActor is null)
			{
				continue;
			}

			var isNote = msg.Placement is not null;
			var font = isNote ? conf.NoteFont : conf.MessageFont;
			var text = message.Text;
			var wrapped = msg.Wrap ? MermaidText.WrapLabel(text, conf.Width - (2 * conf.WrapPadding), font, _measurer) : text;
			var messageWidth = Dimensions(wrapped, font).Width + (2 * conf.WrapPadding);
			if (!isNote && msg.From == actor.NextActor)
			{
				Max(msg.To, messageWidth);
			}
			else if (!isNote && msg.From == actor.PrevActor)
			{
				Max(msg.From, messageWidth);
			}
			else if (!isNote && msg.From == msg.To)
			{
				Max(msg.From, messageWidth / 2);
				Max(msg.To, messageWidth / 2);
			}
			else if (msg.Placement == SequenceNotePlacement.RightOf)
			{
				Max(msg.From, messageWidth);
			}
			else if (msg.Placement == SequenceNotePlacement.LeftOf)
			{
				Max(actor.PrevActor, messageWidth);
			}
			else if (msg.Placement == SequenceNotePlacement.Over)
			{
				if (actor.PrevActor is not null)
				{
					Max(actor.PrevActor, messageWidth / 2);
				}

				if (actor.NextActor is not null)
				{
					Max(msg.From, messageWidth / 2);
				}
			}
		}

		return widths;
	}

	/// <summary><c>calculateActorMargins</c>: each actor's width, height and margin, and each box's margin. It returns the row height.</summary>
	private double CalculateActorMargins(Dictionary<string, double> actorToMessageWidth, List<BoxModel> boxes)
	{
		var conf = _conf;
		var maxHeight = 0.0;
		foreach (var actor in _actors.Values)
		{
			if (actor.Wrap)
			{
				actor.Description = MermaidText.WrapLabel(actor.Description, conf.Width - (2 * conf.WrapPadding), conf.ActorFont, _measurer);
			}

			var dims = Dimensions(actor.Description, conf.ActorFont);
			actor.Width = actor.Wrap ? conf.Width : Math.Max(conf.Width, dims.Width + (2 * conf.WrapPadding));
			actor.TextHeight = dims.Height;
			actor.Height = conf.Look == "neo"
				? Math.Max(ActorStackHeight(dims.Height), conf.Height)
				: actor.Wrap ? Math.Max(dims.Height, conf.Height) : conf.Height;
			maxHeight = Math.Max(maxHeight, actor.Height);
		}

		foreach (var (key, messageWidth) in actorToMessageWidth)
		{
			if (!_actors.TryGetValue(key, out var actor))
			{
				continue;
			}

			if (actor.NextActor is null || !_actors.TryGetValue(actor.NextActor, out var next))
			{
				actor.Margin = Math.Max(messageWidth + conf.ActorMargin - (actor.Width / 2), conf.ActorMargin);
				continue;
			}

			actor.Margin = Math.Max(messageWidth + conf.ActorMargin - (actor.Width / 2) - (next.Width / 2), conf.ActorMargin);
		}

		var maxBoxHeight = 0.0;
		foreach (var box in boxes)
		{
			var totalWidth = box.ActorKeys.Sum(key => _actors[key].Width + _actors[key].Margin);
			totalWidth += conf.BoxMargin * 8;
			totalWidth -= 2 * conf.BoxTextMargin;
			if (box.Wrap && box.Name is not null)
			{
				box.Name = MermaidText.WrapLabel(box.Name, totalWidth - (2 * conf.WrapPadding), conf.MessageFont, _measurer);
			}

			var dims = Dimensions(box.Name ?? "", conf.MessageFont);
			maxBoxHeight = Math.Max(dims.Height, maxBoxHeight);
			var minWidth = Math.Max(totalWidth, dims.Width + (2 * conf.WrapPadding));
			box.Margin = conf.BoxTextMargin;
			if (totalWidth < minWidth)
			{
				box.Margin += (minWidth - totalWidth) / 2;
			}
		}

		foreach (var box in boxes)
		{
			box.TextMaxHeight = maxBoxHeight;
		}

		return Math.Max(maxHeight, conf.Height);
	}

	/// <summary>
	/// <c>addActorRenderingData</c> for the header: each actor's x, the boxes' x and width, and the actors in the
	/// bounds. It returns the boxes in the order it adds them to the models.
	/// </summary>
	private List<BoxModel> AddActorRenderingData(List<string> actorKeys, Dictionary<string, int> createdActors)
	{
		var conf = _conf;
		var drawn = new List<BoxModel>();
		var prevWidth = 0.0;
		var prevMargin = 0.0;
		BoxModel? prevBox = null;
		var maxHeight = 0.0;
		foreach (var key in actorKeys)
		{
			var actor = _actors[key];
			var box = actor.Box;
			if (prevBox is not null && prevBox != box)
			{
				drawn.Add(prevBox);
				prevMargin += conf.BoxMargin + prevBox.Margin;
			}

			if (box is not null && box != prevBox)
			{
				box.X = prevWidth + prevMargin;
				box.Y = 0;
				prevMargin += box.Margin;
			}

			actor.Width = Math.Max(actor.Width is 0 or double.NaN ? conf.Width : actor.Width, conf.Width);
			actor.Height = Math.Max(actor.Height is 0 or double.NaN ? conf.Height : actor.Height, conf.Height);
			actor.Margin = actor.Margin is 0 or double.NaN ? conf.ActorMargin : actor.Margin;
			maxHeight = Math.Max(maxHeight, actor.Height);
			if (createdActors.TryGetValue(actor.Name, out var created) && created != 0)
			{
				prevMargin += actor.Width / 2;
			}

			actor.X = prevWidth + prevMargin;
			actor.StartY = _verticalPos;
			Insert(actor.X, 0, actor.X + actor.Width, actor.Height);
			prevWidth += actor.Width + prevMargin;
			if (actor.Box is not null)
			{
				actor.Box.Width = prevWidth + box!.Margin - actor.Box.X;
			}

			prevMargin = actor.Margin;
			prevBox = actor.Box;
		}

		if (prevBox is not null)
		{
			drawn.Add(prevBox);
		}

		Bump(maxHeight);
		return drawn;
	}

	/// <summary>
	/// The part of <c>calculateLoopBounds</c> that builds each message's model, with the activations of the messages
	/// before it.
	/// </summary>
	private void CalculateLoopBounds(List<MessageModel> messages)
	{
		foreach (var message in messages)
		{
			var msg = message.Source;
			if (msg.Type == SequenceLineType.ActiveStart && msg.From is not null && _actors.TryGetValue(msg.From, out var actor))
			{
				var stacked = _activations.Count(a => a.Actor == msg.From);
				var x = actor.X + (actor.Width / 2) + ((stacked - 1) * _conf.ActivationWidth / 2);
				_activations.Add((msg.From, x, x + _conf.ActivationWidth));
			}
			else if (msg.Type == SequenceLineType.ActiveEnd)
			{
				var last = _activations.FindLastIndex(a => a.Actor == msg.From);
				if (last < 0)
				{
					last = _activations.Count - 1;
				}

				if (last >= 0)
				{
					_activations.RemoveAt(last);
				}
			}

			if (msg.Placement is null && IsSignal(msg.Type))
			{
				BuildMessageModel(message);
			}
		}

		_activations.Clear();
	}

	/// <summary><c>activationBounds</c>: the left and right of the actor's lifeline and its activations.</summary>
	private (double Left, double Right) ActivationBounds(string name)
	{
		var actor = _actors[name];
		var center = actor.X + (actor.Width / 2);
		var left = center - 1;
		var right = center + 1;
		foreach (var activation in _activations)
		{
			if (activation.Actor == name)
			{
				left = Math.Min(left, activation.StartX);
				right = Math.Max(right, activation.StopX);
			}
		}

		return (left, right);
	}

	/// <summary><c>buildMessageModel</c>.</summary>
	private void BuildMessageModel(MessageModel message)
	{
		var conf = _conf;
		var msg = message.Source;
		var type = msg.Type;
		var (fromLeft, fromRight) = ActivationBounds(msg.From!);
		var (toLeft, toRight) = ActivationBounds(msg.To!);
		var isArrowToRight = fromLeft <= toLeft;
		var startX = isArrowToRight ? fromRight : fromLeft;
		var stopX = isArrowToRight ? toLeft : toRight;
		var bidirectional = type is (int)SequenceArrow.BidirectionalSolid or (int)SequenceArrow.BidirectionalDotted;
		if (conf.Look == "neo")
		{
			const double offset = 3;
			if (type != (int)SequenceArrow.SolidOpen)
			{
				stopX += isArrowToRight ? -offset : offset;
			}

			if (bidirectional)
			{
				startX += isArrowToRight ? offset : -offset;
			}
		}

		startX += CentralConnectionOffset(msg, isArrowToRight);
		var isArrowToActivation = Math.Abs(toLeft - toRight) > 2;
		double Adjust(double value) => isArrowToRight ? -value : value;
		if (msg.From == msg.To)
		{
			stopX = startX;
		}
		else
		{
			if (msg.Activate && !isArrowToActivation)
			{
				stopX += Adjust((conf.ActivationWidth / 2) - 1);
			}

			if (!IsUnshortened(type))
			{
				stopX += Adjust(3);
			}

			if (type is (int)SequenceArrow.BidirectionalSolid or (int)SequenceArrow.BidirectionalDotted
				or (int)SequenceArrow.SolidArrowTopReverseDotted or (int)SequenceArrow.SolidArrowBottomReverseDotted
				or (int)SequenceArrow.SolidArrowTopReverse or (int)SequenceArrow.SolidArrowBottomReverse)
			{
				startX -= Adjust(3);
			}
		}

		var boundedWidth = Math.Abs(startX - stopX);
		if (msg.Wrap && message.Text.Length > 0)
		{
			message.Text = MermaidText.WrapLabel(message.Text, Math.Max(boundedWidth + (2 * conf.WrapPadding), conf.Width), conf.MessageFont, _measurer);
		}

		message.StartX = startX;
		message.StopX = stopX;
		message.FromBounds = Math.Min(Math.Min(fromLeft, fromRight), Math.Min(toLeft, toRight));
		message.ToBounds = Math.Max(Math.Max(fromLeft, fromRight), Math.Max(toLeft, toRight));
		message.Built = true;
	}

	/// <summary><c>boundMessage</c>: the message's height in the bounds. It returns the y of the line.</summary>
	private double BoundMessage(MessageModel message)
	{
		var conf = _conf;
		Bump(10);
		var lines = 0;
		foreach (var _ in MermaidText.SplitBreaks(message.Text))
		{
			lines++;
		}

		var dims = Dimensions(message.Text, conf.MessageFont);
		var lineHeight = dims.Height / lines;
		message.Height += lineHeight;
		Bump(lineHeight);
		var totalOffset = dims.Height - 10;
		double lineStartY;
		if (message.StartX == message.StopX)
		{
			lineStartY = _verticalPos + totalOffset;
			if (!conf.RightAngles)
			{
				totalOffset += conf.BoxMargin;
				lineStartY = _verticalPos + totalOffset;
			}

			totalOffset += 30;
			var dx = Math.Max(dims.Width / 2, conf.Width / 2);
			Insert(message.StartX - dx, _verticalPos - 10 + totalOffset, message.StopX + dx, _verticalPos + 30 + totalOffset);
		}
		else
		{
			totalOffset += conf.BoxMargin;
			lineStartY = _verticalPos + totalOffset;
			Insert(message.StartX, lineStartY - 10, message.StopX, lineStartY);
		}

		Bump(totalOffset);
		message.Height += totalOffset;
		var stopY = message.StartY + message.Height;
		Insert(message.FromBounds, message.StartY, message.ToBounds, stopY);
		return lineStartY;
	}

	/// <summary>The line, the self loop and the text lines <c>drawMessage</c> draws.</summary>
	private SequenceMessagePlace PlaceMessage(MessageModel message, double lineY)
	{
		var conf = _conf;
		var msg = message.Source;
		var type = msg.Type;
		var startX = message.StartX;
		var stopX = message.StopX;
		var font = conf.MessageFont;
		var dims = Dimensions(message.Text, font);

		var textX = Math.Min(startX, stopX);
		var textWidth = Math.Abs(stopX - startX);
		var (anchorX, anchor) = conf.MessageAlign switch
		{
			"left" or "start" => (JsRound(textX + conf.WrapPadding), -1),
			"right" or "end" => (JsRound(textX + textWidth - conf.WrapPadding), 1),
			"middle" or "center" => (JsRound(textX + (textWidth / 2)), 0),
			_ => (textX, -1),
		};
		var text = new List<SequenceTextLine>();
		var textY = message.StartY + 10;
		var drawnHeight = 0.0;
		foreach (var line in MermaidText.SplitBreaks(message.Text))
		{
			var y = JsRound(textY + ((2 * drawnHeight) + conf.WrapPadding) / 2);
			var box = MessageTextBox(line.ToString(), anchorX, y, anchor, font);
			text.Add(new SequenceTextLine(line.ToString(), anchorX, y, box));
			drawnHeight += box.Height;
		}

		var visible = message.SequenceVisible || conf.ShowSequenceNumbers;
		var reverse = IsReverse(type);
		var bidirectional = type is (int)SequenceArrow.BidirectionalSolid or (int)SequenceArrow.BidirectionalDotted;
		var central = msg.CentralConnection != 0;
		var centralAtSource = msg.CentralConnection is SequenceLineType.CentralConnectionDual or SequenceLineType.CentralConnectionReverse;
		SequenceRect lineBox;
		if (startX == stopX)
		{
			var lineStartX = startX + (visible && (reverse || bidirectional) ? 10 : 0);
			lineBox = conf.RightAngles
				? SequenceRect.FromEdges(Math.Min(lineStartX, startX), lineY, Math.Max(lineStartX, startX + Math.Max(conf.Width / 2, dims.Width / 2)), lineY + 25)
				: CubicBounds(lineStartX, lineY, lineStartX + 60, lineY - 10, startX + 60, lineY + 30, startX, lineY + 20);
		}
		else
		{
			var x1 = startX;
			var x2 = stopX;
			if (visible)
			{
				if (bidirectional)
				{
					x1 = startX < stopX
						? startX + (SequenceNumberRadius * 2)
						: startX - SequenceNumberRadius + (central ? -5 : 0) + (centralAtSource ? -7.5 : 0);
				}
				else if (reverse)
				{
					if (stopX > startX)
					{
						x2 = stopX - (2 * SequenceNumberRadius);
					}
					else
					{
						x2 = stopX - SequenceNumberRadius;
						x1 += centralAtSource ? -7.5 : 0;
					}

					x2 += central ? 15 : 0;
				}
				else
				{
					x1 = startX + SequenceNumberRadius;
				}
			}

			lineBox = SequenceRect.FromEdges(Math.Min(x1, x2), lineY, Math.Max(x1, x2), lineY);
		}

		return new SequenceMessagePlace(
			msg.Id,
			msg.From!,
			msg.To!,
			type,
			startX,
			stopX,
			lineY,
			lineBox,
			text,
			visible ? message.SequenceIndex : null);
	}

	/// <summary>
	/// The box Chromium gives a line of <c>drawText</c>: <c>dy="1em"</c> under <c>dominant-baseline: middle</c>, which
	/// puts the baseline half an x-height of Liberation Sans below the em.
	/// </summary>
	private SequenceRect MessageTextBox(string line, double x, double y, int anchor, FontSpec font)
	{
		// drawText writes an empty line as a zero width space.
		var (width, ascent, height) = DrawnLine(line.Length == 0 ? "​" : line, font);
		var baseline = y + font.Size + (font.Size * HalfXHeight);
		var left = anchor switch
		{
			0 => x - (width / 2),
			1 => x - width,
			_ => x,
		};
		return new SequenceRect(left, baseline - ascent, width, height);
	}

	/// <summary>
	/// The box of a drawn line of text: its advance, which Chromium lays out in 64ths of a pixel and rounds up, and its
	/// ascent and height, each rounded to whole pixels. The text is what the SVG shows once mermaid.js has restored its
	/// entity placeholders and the browser has read it as HTML.
	/// </summary>
	private (double Width, double Ascent, double Height) DrawnLine(string line, FontSpec font)
	{
		var shown = FlowchartLabels.HtmlTextOf(SequenceDb.Restore(line));
		var metrics = _measurer.Measure(shown, font);
		var ascent = JsRound(metrics.Ascent);
		return (Math.Ceiling(metrics.Width * 64) / 64, ascent, ascent + JsRound(metrics.Descent));
	}

	/// <summary>Liberation Sans's x-height of 1082 units in 2048, halved.</summary>
	private const double HalfXHeight = 1082.0 / 2048 / 2;

	/// <summary>
	/// The lines <c>byTspan</c> writes for a label in a <paramref name="width"/> by <paramref name="height"/> box at
	/// <paramref name="x"/>, <paramref name="y"/>: each centered with <c>dominant-baseline: central</c>, one font size
	/// apart and centered as a block.
	/// </summary>
	private List<SequenceTextLine> ByTspan(string content, double x, double y, double width, double height)
	{
		var font = _conf.ActorFont;
		var lines = new List<string>();
		foreach (var line in MermaidText.SplitBreaks(content))
		{
			lines.Add(line.ToString());
		}

		var result = new List<SequenceTextLine>(lines.Count);
		var centerX = x + (width / 2);
		for (var i = 0; i < lines.Count; i++)
		{
			var dy = (i * font.Size) - (font.Size * (lines.Count - 1) / 2);
			var lineY = y + (height / 2);
			var (boxWidth, _, boxHeight) = DrawnLine(lines[i], font);
			var box = new SequenceRect(centerX - (boxWidth / 2), lineY + dy - (boxHeight / 2), boxWidth, boxHeight);
			result.Add(new SequenceTextLine(lines[i], centerX, lineY + dy, box));
		}

		return result;
	}

	/// <summary>The label of <c>drawBox</c>.</summary>
	private List<SequenceTextLine> BoxLabel(BoxModel box) =>
		string.IsNullOrEmpty(box.Name) ? [] : ByTspan(box.Name, box.X, box.Y + _conf.BoxTextMargin + (box.TextMaxHeight / 2), box.Width, 0);

	/// <summary>
	/// The drawing of <c>svgDraw.drawActor</c> at the top or the bottom: the box of the type's shape and of its label.
	/// Under <c>classic</c> it sets the actor's height as the shape functions do, from the box of what they drew.
	/// </summary>
	private SequenceActorDrawing DrawActor(ActorModel actor, bool footer)
	{
		var conf = _conf;
		var neo = conf.Look == "neo";
		var actorY = footer ? actor.StopY!.Value : actor.StartY;
		var center = actor.X + (actor.Width / 2);
		var x = actor.X;
		var width = actor.Width;
		var height = actor.Height;
		double glyphBottom = 0;
		double labelCenter = 0;
		if (neo)
		{
			if (footer)
			{
				glyphBottom = actorY + LabelLifelineGap + GlyphBandHeight;
				labelCenter = glyphBottom + GlyphLabelGap + (actor.TextHeight / 2);
			}
			else
			{
				var lifelineStart = actorY + height;
				labelCenter = lifelineStart - LabelLifelineGap - (actor.TextHeight / 2);
				glyphBottom = lifelineStart - LabelLifelineGap - actor.TextHeight - GlyphLabelGap;
			}
		}

		SequenceRect shape;
		List<SequenceTextLine> label;
		var shift = 0.0;
		switch (actor.Type)
		{
			case "actor":
				{
					var scale = neo ? GlyphBandHeight / ActorGlyphHeight : 1;
					double Gy(double offset) => neo ? glyphBottom - ((ActorGlyphBottom - offset) * scale) : actorY + offset;
					shape = SequenceRect.FromEdges(center - (ActorTypeWidth / 2 * scale), Gy(10) - (15 * scale), center + (ActorTypeWidth / 2 * scale), Gy(60));
					if (!neo)
					{
						actor.Height = ActorGlyphHeight;
					}

					label = ByTspan(actor.Description, x, neo ? labelCenter - (actor.Height / 2) : actorY + 35, width, actor.Height);
					actor.FooterHeight = actor.Height;
					break;
				}

			case "boundary":
				{
					const double radius = 22;
					var iconCenterY = neo ? glyphBottom - radius : actorY + 12;
					shape = SequenceRect.FromEdges(center - (radius * 2.5), iconCenterY - radius, center + radius, iconCenterY + radius);
					if (!neo)
					{
						actor.Height = shape.Height + conf.LabelBoxHeight;
					}

					label = ByTspan(actor.Description, x, neo ? labelCenter - (height / 2) : actorY + 15, width, height);
					shift = neo ? 0 : (radius / 2) + 10;
					actor.FooterHeight = actor.Height;
					break;
				}

			case "control":
				{
					const double r = 22;
					var cy = neo ? glyphBottom - 22 : actorY + 32;
					shape = SequenceRect.FromEdges(center - r, cy - r, center + r, cy + r);
					if (!neo)
					{
						actor.Height = shape.Height + (2 * conf.LabelBoxHeight);
					}

					label = ByTspan(actor.Description, x, neo ? labelCenter - (height / 2) : actorY + r + (footer ? 5 : 12), width, height);
					actor.FooterHeight = actor.Height;
					break;
				}

			case "entity":
				{
					const double r = 22;
					var cy = neo ? glyphBottom - 22 : actorY + (footer ? 10 : 25);
					shape = SequenceRect.FromEdges(center - r, cy - r, center + r, cy + r);
					if (!neo)
					{
						actor.Height = shape.Height + conf.LabelBoxHeight;
					}

					label = ByTspan(actor.Description, x, neo ? labelCenter - (height / 2) : actorY + (footer ? 15 : 30), width, height);
					shift = neo ? 0 : footer ? r : (r / 2) - 5;
					actor.FooterHeight = actor.Height;
					break;
				}

			case "database":
				{
					var w = width / 3;
					var rx = w / 2;
					var ry = rx / (2.5 + (w / 50));
					var h = neo ? GlyphBandHeight : width / 3;
					var cylinderY = neo ? glyphBottom - h - ry : actorY;
					shape = SequenceRect.FromEdges(x + w, cylinderY + ry, x + (2 * w), cylinderY + h + ry);
					label = ByTspan(actor.Description, x, neo ? labelCenter - (height / 2) : actorY + 35, width, height);
					if (!neo)
					{
						actor.Height = h + conf.LabelBoxHeight;
					}

					actor.FooterHeight = actor.Height;
					break;
				}

			case "collections":
				{
					const double offset = 6;
					var rectHeight = neo ? height - offset : height;
					shape = new SequenceRect(x, actorY, width, rectHeight);
					label = ByTspan(actor.Description, x - offset, actorY + offset, width, rectHeight);
					actor.FooterHeight = neo ? height : rectHeight;
					var drawing = new SequenceActorDrawing(shape, Union(shape.Union(new SequenceRect(x - offset, actorY + offset, width, rectHeight)), label), label);
					return drawing;
				}

			case "queue":
				shape = new SequenceRect(x, actorY, width, height);
				label = ByTspan(actor.Description, x, actorY, width, height);
				actor.FooterHeight = height;
				break;

			default:
				shape = new SequenceRect(x, actorY, width, height);
				label = ByTspan(actor.Description, x, actorY, width, height);
				actor.FooterHeight = height;
				break;
		}

		var bounds = Union(shape, label);
		if (shift != 0)
		{
			shape = shape with { Y = shape.Y + shift };
			bounds = bounds with { Y = bounds.Y + shift };
			label = [.. label.Select(l => l with { Y = l.Y + shift, Box = l.Box with { Y = l.Box.Y + shift } })];
		}

		return new SequenceActorDrawing(shape, bounds, label);
	}

	private static SequenceRect Union(SequenceRect shape, List<SequenceTextLine> label)
	{
		var bounds = shape;
		foreach (var line in label)
		{
			if (line.Box.Width > 0 || line.Box.Height > 0)
			{
				bounds = bounds.Union(line.Box);
			}
		}

		return bounds;
	}

	/// <summary>The box of a cubic Bézier, at its ends and the roots of its derivative.</summary>
	internal static SequenceRect CubicBounds(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3)
	{
		var (minX, maxX) = CubicExtent(x0, x1, x2, x3);
		var (minY, maxY) = CubicExtent(y0, y1, y2, y3);
		return SequenceRect.FromEdges(minX, minY, maxX, maxY);
	}

	private static (double Min, double Max) CubicExtent(double p0, double p1, double p2, double p3)
	{
		var min = Math.Min(p0, p3);
		var max = Math.Max(p0, p3);
		var a = -p0 + (3 * p1) - (3 * p2) + p3;
		var b = 2 * (p0 - (2 * p1) + p2);
		var c = p1 - p0;
		void At(double t)
		{
			if (t is > 0 and < 1)
			{
				var u = 1 - t;
				var value = (u * u * u * p0) + (3 * u * u * t * p1) + (3 * u * t * t * p2) + (t * t * t * p3);
				min = Math.Min(min, value);
				max = Math.Max(max, value);
			}
		}

		if (Math.Abs(a) < 1e-12)
		{
			if (Math.Abs(b) > 1e-12)
			{
				At(-c / b);
			}
		}
		else
		{
			var discriminant = (b * b) - (4 * a * c);
			if (discriminant >= 0)
			{
				var root = Math.Sqrt(discriminant);
				At((-b + root) / (2 * a));
				At((-b - root) / (2 * a));
			}
		}

		return (min, max);
	}

	/// <summary><c>calculateCentralConnectionOffset</c>.</summary>
	private static double CentralConnectionOffset(SequenceDbMessage msg, bool isArrowToRight)
	{
		var offset = 0.0;
		var atSource = msg.CentralConnection is SequenceLineType.CentralConnectionReverse or SequenceLineType.CentralConnectionDual;
		if (atSource)
		{
			offset += CentralConnectionBaseOffset;
			if (msg.Type is (int)SequenceArrow.BidirectionalSolid or (int)SequenceArrow.BidirectionalDotted)
			{
				offset += isArrowToRight ? 0 : -CentralConnectionBidirectionalOffset;
			}
		}

		return offset;
	}

	/// <summary>The arrow types <c>buildMessageModel</c> does not shorten at the end, because they have no head there.</summary>
	private static bool IsUnshortened(int type) => (SequenceArrow)type is SequenceArrow.SolidOpen or SequenceArrow.DottedOpen
		or SequenceArrow.StickTop or SequenceArrow.StickBottom or SequenceArrow.StickTopDotted or SequenceArrow.StickBottomDotted
		or SequenceArrow.SolidArrowTopReverseDotted or SequenceArrow.SolidArrowBottomReverseDotted
		or SequenceArrow.StickArrowTopReverse or SequenceArrow.StickArrowBottomReverse
		or SequenceArrow.StickArrowTopReverseDotted or SequenceArrow.StickArrowBottomReverseDotted
		or SequenceArrow.SolidArrowTopReverse or SequenceArrow.SolidArrowBottomReverse;

	/// <summary><c>isReverseArrowType</c>.</summary>
	private static bool IsReverse(int type) => type is >= (int)SequenceArrow.SolidArrowTopReverse and <= (int)SequenceArrow.StickArrowBottomReverse
		or >= (int)SequenceArrow.SolidArrowTopReverseDotted and <= (int)SequenceArrow.StickArrowBottomReverseDotted;

	/// <summary>Whether the <c>LINETYPE</c> is one of the message types <c>buildMessageModel</c> places.</summary>
	internal static bool IsSignal(int type) => type is 0 or 1 or 3 or 4 or 5 or 6 or 24 or 25 or 33 or 34 or (>= 41 and <= 48) or (>= 51 and <= 58);

	private TextDimensions Dimensions(string text, FontSpec font) => MermaidText.CalculateTextDimensions(text, font, _measurer);

	/// <summary>JavaScript's <c>Math.round</c>.</summary>
	private static double JsRound(double value) => Math.Floor(value + 0.5);

	private void Bump(double bump)
	{
		_verticalPos += bump;
		_stopY = _stopY is { } stop ? Math.Max(stop, _verticalPos) : _verticalPos;
	}

	/// <summary><c>bounds.insert</c>. It has no loops or activations to widen yet.</summary>
	private void Insert(double startX, double startY, double stopX, double stopY)
	{
		static double Update(double? current, double value, Func<double, double, double> pick) => current is { } c ? pick(value, c) : value;
		_startX = Update(_startX, Math.Min(startX, stopX), Math.Min);
		_startY = Update(_startY, Math.Min(startY, stopY), Math.Min);
		_stopX = Update(_stopX, Math.Max(startX, stopX), Math.Max);
		_stopY = Update(_stopY, Math.Max(startY, stopY), Math.Max);
	}

	/// <summary>The stack an icon actor needs above the lifeline under <c>neo</c>: glyph, gap, label, gap, from <c>actorBands.ts</c>.</summary>
	private static double ActorStackHeight(double textHeight) => GlyphBandHeight + GlyphLabelGap + textHeight + LabelLifelineGap;

	private const double GlyphBandHeight = 44;
	private const double GlyphLabelGap = 6;
	private const double LabelLifelineGap = 6;
	private const double ActorTypeWidth = 36;
	private const double ActorGlyphTop = -5;
	private const double ActorGlyphBottom = 60;
	private const double ActorGlyphHeight = ActorGlyphBottom - ActorGlyphTop;

	/// <summary>A participant as the renderer mutates it.</summary>
	private sealed class ActorModel(SequenceDbActor source)
	{
		public SequenceDbActor Source { get; } = source;

		public string Name => Source.Name;

		public string Type => Source.Type;

		public bool Wrap => Source.Wrap;

		public string? PrevActor => Source.PrevActor;

		public string? NextActor => Source.NextActor;

		public string Description { get; set; } = SequenceWriter.Encode(source.Description);

		public BoxModel? Box { get; set; }

		public double X { get; set; }

		public double Width { get; set; }

		public double Height { get; set; }

		public double Margin { get; set; }

		public double TextHeight { get; set; }

		public double StartY { get; set; }

		public double? StopY { get; set; }

		/// <summary>The height <c>drawActor</c> returned for the footer.</summary>
		public double FooterHeight { get; set; }
	}

	/// <summary>A box as the renderer mutates it.</summary>
	private sealed class BoxModel(SequenceDbBox source)
	{
		public SequenceDbBox Source { get; } = source;

		public string? Name { get; set; } = source.Name is null ? null : SequenceWriter.Encode(source.Name);

		public bool Wrap => Source.Wrap;

		public List<string> ActorKeys => Source.ActorKeys;

		public double Margin { get; set; }

		public double TextMaxHeight { get; set; }

		public double X { get; set; }

		public double Y { get; set; }

		public double Width { get; set; }

		public double Height { get; set; }
	}

	/// <summary>A message and the <c>msgModel</c> of a signal.</summary>
	private sealed class MessageModel(SequenceDbMessage source)
	{
		public SequenceDbMessage Source { get; } = source;

		public string Text { get; set; } = SequenceWriter.Encode(source.Text);

		public bool Built { get; set; }

		public double StartX { get; set; }

		public double StopX { get; set; }

		public double StartY { get; set; }

		public double Height { get; set; }

		public double FromBounds { get; set; }

		public double ToBounds { get; set; }

		public double SequenceIndex { get; set; }

		public bool SequenceVisible { get; set; }

		public double? LineY { get; set; }
	}
}

/// <summary>The sequence config as <c>bounds.init</c> leaves it, with the top-level look and fonts merged in.</summary>
internal sealed class SequenceConf
{
	public required string Look { get; init; }

	public required double Width { get; init; }

	/// <summary>The row height. <c>draw</c> sets it to what <c>calculateActorMargins</c> returns.</summary>
	public required double Height { get; set; }

	public required double ActorMargin { get; init; }

	public required double BoxMargin { get; init; }

	public required double BoxTextMargin { get; init; }

	public required double NoteMargin { get; init; }

	public required double WrapPadding { get; init; }

	public required double ActivationWidth { get; init; }

	public required double DiagramMarginX { get; init; }

	public required double DiagramMarginY { get; init; }

	public required bool MirrorActors { get; init; }

	public required double BottomMarginAdj { get; init; }

	public required bool RightAngles { get; init; }

	public required bool ShowSequenceNumbers { get; init; }

	public required bool HideUnusedParticipants { get; init; }

	public required string MessageAlign { get; init; }

	public required double LabelBoxHeight { get; init; }

	public required FontSpec ActorFont { get; init; }

	public required FontSpec NoteFont { get; init; }

	public required FontSpec MessageFont { get; init; }

	public static SequenceConf From(MermaidConfig config)
	{
		var sequence = config.Sequence;
		FontSpec Font(string? family, MermaidValue? size, MermaidValue? weight) => new(
			(config.FontFamily ?? family ?? "sans-serif").TrimEnd(';'),
			config.FontSize ?? Size(size) ?? 16,
			Weight(weight));
		return new SequenceConf
		{
			Look = config.Look ?? "classic",
			Width = sequence.Width ?? 150,
			Height = sequence.Height ?? 65,
			ActorMargin = sequence.ActorMargin ?? 50,
			BoxMargin = sequence.BoxMargin ?? 10,
			BoxTextMargin = sequence.BoxTextMargin ?? 5,
			NoteMargin = sequence.NoteMargin ?? 10,
			WrapPadding = sequence.WrapPadding ?? 10,
			ActivationWidth = sequence.ActivationWidth ?? 10,
			DiagramMarginX = sequence.DiagramMarginX ?? 50,
			DiagramMarginY = sequence.DiagramMarginY ?? 10,
			MirrorActors = sequence.MirrorActors ?? true,
			BottomMarginAdj = sequence.BottomMarginAdj ?? 1,
			RightAngles = sequence.RightAngles ?? false,
			ShowSequenceNumbers = sequence.ShowSequenceNumbers ?? false,
			HideUnusedParticipants = sequence.HideUnusedParticipants ?? false,
			MessageAlign = sequence.MessageAlign ?? "center",
			LabelBoxHeight = sequence.LabelBoxHeight ?? 20,
			ActorFont = Font(sequence.ActorFontFamily, sequence.ActorFontSize, sequence.ActorFontWeight),
			NoteFont = Font(sequence.NoteFontFamily, sequence.NoteFontSize, sequence.NoteFontWeight),
			MessageFont = Font(sequence.MessageFontFamily, sequence.MessageFontSize, sequence.MessageFontWeight),
		};
	}

	/// <summary><c>parseFontSize</c>: a number, or the number at the start of a text such as <c>16px</c>.</summary>
	private static double? Size(MermaidValue? value)
	{
		if (value is null)
		{
			return null;
		}

		if (value.Kind == MermaidValueKind.Number)
		{
			return value.Number;
		}

		if (value.Kind != MermaidValueKind.String)
		{
			return null;
		}

		var text = value.String.AsSpan();
		var end = 0;
		while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] == '.'))
		{
			end++;
		}

		return double.TryParse(text[..end], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var size) ? size : null;
	}

	/// <summary>A CSS <c>font-weight</c>: a number, <c>bold</c> or <c>bolder</c> as 700, and anything else as 400.</summary>
	private static int Weight(MermaidValue? value)
	{
		if (value is null)
		{
			return 400;
		}

		if (value.Kind == MermaidValueKind.Number)
		{
			return (int)Math.Clamp(value.Number, 1, 1000);
		}

		if (value.Kind == MermaidValueKind.String)
		{
			return value.String switch
			{
				"bold" or "bolder" => 700,
				_ when int.TryParse(value.String, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var weight) => Math.Clamp(weight, 1, 1000),
				_ => 400,
			};
		}

		return 400;
	}
}
