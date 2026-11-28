// Upstream: packages/mermaid/src/diagrams/sequence/sequenceRenderer.ts
// Upstream: packages/mermaid/src/diagrams/sequence/svgDraw.js
// Upstream: packages/mermaid/src/diagrams/common/svgDrawCommon.ts
using System.Globalization;
using SplashTheMusical.Diagrams.Flowchart;
using SplashTheMusical.Rendering;
using SplashTheMusical.Text;

namespace SplashTheMusical.Diagrams.Sequence;

/// <summary>
/// Writes a <see cref="SequenceLayout"/> in the order <c>draw</c> leaves mermaid.js's DOM. The groups <c>drawBox</c>,
/// <c>drawBackgroundRect</c> and <c>drawActor</c> lower to the front come first, the last lowered first: the boxes,
/// the <c>rect</c> backgrounds, the bottom participants and the top participants with their lifelines. Then come the
/// markers, the notes, activations and blocks in the order of the messages that add them, the figures of the icon
/// participants at the top, the messages, the figures at the bottom and the title.
/// </summary>
/// <remarks>
/// The <c>computer</c>, <c>database</c> and <c>clock</c> symbols, which nothing uses, are left out, as are the
/// <c>links</c> menus, which are DOM popups. An inline style of mermaid.js is a presentation attribute here when no
/// rule of the stylesheet sets the property, and a rule of the layout otherwise. The <c>name</c> attributes, which
/// draw nothing, are left out.
/// </remarks>
internal static class SequenceRenderer
{
	private const string ActorFill = "#eaeaea";
	private const string NoteFill = "#EDF2AE";
	private const string NoteStroke = "#666";
	private const string ZeroWidthSpace = "​";

	public static void Render(SequenceLayout layout, SvgWriter svg, string svgId)
	{
		ArgumentNullException.ThrowIfNull(layout);
		ArgumentNullException.ThrowIfNull(svg);
		var neo = layout.Look == "neo";
		for (var i = layout.Boxes.Count - 1; i >= 0; i--)
		{
			WriteBox(svg, layout.Boxes[i], layout.Conf);
		}

		for (var i = layout.Backgrounds.Count - 1; i >= 0; i--)
		{
			var background = layout.Backgrounds[i];
			WriteRect(svg, background.Rect, background.Fill ?? layout.RectFill, null, "rect", null);
		}

		var last = layout.Actors.Count - 1;
		for (var i = last; i >= 0; i--)
		{
			if (layout.Actors[i].Bottom is { } bottom)
			{
				WriteActorGroup(svg, layout, layout.Actors[i], bottom, i, last, footer: true, svgId);
			}
		}

		for (var i = last; i >= 0; i--)
		{
			WriteActorGroup(svg, layout, layout.Actors[i], layout.Actors[i].Top, i, last, footer: false, svgId);
		}

		WriteMarkers(svg, svgId, neo, layout.Theme);
		WriteMessageItems(svg, layout, neo);
		for (var i = 0; i <= last; i++)
		{
			WriteFigure(svg, layout, layout.Actors[i], layout.Actors[i].Top, i, footer: false, svgId);
		}

		foreach (var message in layout.Messages)
		{
			WriteMessage(svg, message, layout.Conf, svgId);
		}

		for (var i = 0; i <= last; i++)
		{
			if (layout.Actors[i].Bottom is { } bottom)
			{
				WriteFigure(svg, layout, layout.Actors[i], bottom, last, footer: true, svgId);
			}
		}

		if (layout.Title is { } title)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", title.X);
			svg.WriteAttribute("y", -25);
			svg.WriteText(title.Text);
			svg.EndElement();
		}
	}

	/// <summary>
	/// The notes, activations and blocks, each where the message loop of <c>draw</c> appends it: a note and a block at
	/// the message that draws them, and an activation's group at the message that starts it.
	/// </summary>
	private static void WriteMessageItems(SvgWriter svg, SequenceLayout layout, bool neo)
	{
		var items = new List<(int Id, object Item)>(layout.Notes.Count + layout.Activations.Count + layout.Blocks.Count);
		items.AddRange(layout.Notes.Select(n => (n.Id, (object)n)));
		items.AddRange(layout.Activations.Select(a => (a.StartId, (object)a)));
		items.AddRange(layout.Blocks.Select(b => (b.Id, (object)b)));
		foreach (var (_, item) in items.OrderBy(i => i.Id))
		{
			switch (item)
			{
				case SequenceNotePlace note:
					WriteNote(svg, note, layout.Conf, neo);
					break;
				case SequenceActivationPlace activation:
					svg.StartElement("g");
					WriteRect(svg, activation.Rect, NoteFill, NoteStroke, Classes("activation" + activation.Level.ToString(CultureInfo.InvariantCulture), activation.PaletteClass), null, neo);
					svg.EndElement();
					break;
				case SequenceBlockPlace block:
					WriteBlock(svg, block, layout.Conf);
					break;
			}
		}
	}

	/// <summary><c>drawBox</c>: the box's rectangle and its title.</summary>
	private static void WriteBox(SvgWriter svg, SequenceBoxPlace box, SequenceConf conf)
	{
		svg.StartElement("g");
		WriteRect(svg, box.Rect, box.Fill, "rgb(0,0,0, 0.5)", "rect", null);
		WriteTspanLabel(svg, box.Label, conf.ActorFont, "text", 0);
		svg.EndElement();
	}

	/// <summary>
	/// The group <c>drawActor</c> lowers to the front: the lifeline of a top participant, and the rectangle, stack or
	/// cylinder of the types that draw in it, with the label.
	/// </summary>
	private static void WriteActorGroup(SvgWriter svg, SequenceLayout layout, SequenceActorPlace actor, SequenceActorDrawing drawing, int index, int last, bool footer, string svgId)
	{
		var conf = layout.Conf;
		var neo = conf.Look == "neo";
		var boxType = actor.Type is "participant" or "collections" or "queue" or "database";
		if (!IsKnownType(actor.Type) || (footer && !boxType))
		{
			return;
		}

		var positionClass = footer ? "actor actor-bottom" : "actor actor-top";
		svg.StartElement("g");
		if (footer && actor.Type == "queue")
		{
			svg.WriteAttribute("class", positionClass);
		}

		if (!footer)
		{
			var center = actor.X + (actor.Width / 2);
			svg.StartElement("line");
			svg.WriteAttribute("id", "actor" + index.ToString(CultureInfo.InvariantCulture));
			svg.WriteAttribute("x1", center);
			svg.WriteAttribute("y1", actor.LifelineStartY);
			svg.WriteAttribute("x2", center);
			svg.WriteAttribute("y2", actor.LifelineEndY);
			svg.WriteAttribute("class", "actor-line 200");
			svg.WriteAttribute("stroke-width", "0.5px");
			svg.WriteAttribute("stroke", "#999");
			svg.WriteAttribute("data-et", "life-line");
			svg.WriteAttribute("data-id", actor.Name);
			svg.EndElement();
		}

		if (!boxType)
		{
			svg.EndElement();
			return;
		}

		var shadow = neo ? "url(#" + svgId + "-drop-shadow)" : null;
		if (!footer)
		{
			svg.StartElement("g");
			if (actor.Type == "queue")
			{
				svg.WriteAttribute("class", positionClass);
			}

			if (neo)
			{
				svg.WriteAttribute("data-look", "neo");
			}

			if (actor.Type == "collections" && neo)
			{
				svg.WriteAttribute("filter", shadow!);
			}

			svg.WriteAttribute("data-et", "participant");
			svg.WriteAttribute("data-type", actor.Type);
			svg.WriteAttribute("data-id", actor.Name);
		}

		var shape = drawing.Shape;
		switch (actor.Type)
		{
			case "participant":
				WriteRect(svg, shape, ActorFill, NoteStroke, Classes(positionClass, actor.PaletteClass), neo ? 6 : 3, neo, shadow);
				WriteTspanLabel(svg, drawing.Label, conf.ActorFont, "actor actor-box", 0);
				break;
			case "collections":
				{
					const double Offset = 6;
					WriteRect(svg, shape, ActorFill, NoteStroke, Classes(positionClass, actor.PaletteClass), null, neo);
					WriteRect(svg, shape with { X = shape.X - Offset, Y = shape.Y + Offset }, ActorFill, NoteStroke, Classes("actor", actor.PaletteClass), null, neo);
					WriteTspanLabel(svg, drawing.Label, conf.ActorFont, "actor actor-box", 0);
					break;
				}

			case "queue":
				{
					var ry = shape.Height / 2;
					var rx = ry / (2.5 + (shape.Height / 50));
					var top = shape.Y + ry;
					svg.StartElement("g");
					svg.WriteAttribute("transform", Translate(rx, -(shape.Height / 2)));
					if (neo)
					{
						svg.WriteAttribute("filter", shadow!);
					}

					WriteClass(svg, actor.PaletteClass);
					WritePath(svg, $"M {N(shape.X)},{N(top)} a {N(rx)},{N(ry)} 0 0 0 0,{N(shape.Height)} h {N(shape.Width - (2 * rx))} a {N(rx)},{N(ry)} 0 0 0 0,-{N(shape.Height)} Z");
					svg.EndElement();
					svg.StartElement("g");
					svg.WriteAttribute("transform", Translate(shape.Width - rx, -shape.Height / 2));
					WriteClass(svg, actor.PaletteClass);
					WritePath(svg, $"M {N(shape.X)},{N(top)} a {N(rx)},{N(ry)} 0 0 0 0,{N(shape.Height)}");
					svg.EndElement();
					WriteTspanLabel(svg, drawing.Label, conf.ActorFont, "actor actor-box", 0);
					break;
				}

			case "database":
				{
					var w = shape.Width;
					var rx = w / 2;
					var ry = rx / (2.5 + (w / 50));
					var h = shape.Height;
					var cylinderY = shape.Y - ry;
					svg.StartElement("g");
					svg.WriteAttribute("class", Classes(positionClass, actor.PaletteClass));
					if (neo)
					{
						svg.WriteAttribute("filter", shadow!);
					}

					if (actor.PaletteClass is null)
					{
						Paint(svg, "stroke", layout.ActorBorder);
					}

					svg.WriteAttribute("transform", Translate(w, ry));
					WritePath(svg, $"M {N(actor.X)},{N(cylinderY + ry)} a {N(rx)},{N(ry)} 0 0 0 {N(w)},0 a {N(rx)},{N(ry)} 0 0 0 -{N(w)},0 l 0,{N(h - (2 * ry))} a {N(rx)},{N(ry)} 0 0 0 {N(w)},0 l 0,-{N(h - (2 * ry))}");
					svg.EndElement();
					WriteTspanLabel(svg, drawing.Label, conf.ActorFont, "actor actor-box", 0);
					break;
				}
		}

		if (!footer)
		{
			svg.EndElement();
		}

		svg.EndElement();
	}

	/// <summary>
	/// The group <c>drawActor</c> appends for <c>actor</c>, <c>boundary</c>, <c>control</c> and <c>entity</c>: the
	/// type's figure and its label, moved by the translate the classic look gives some of them.
	/// </summary>
	private static void WriteFigure(SvgWriter svg, SequenceLayout layout, SequenceActorPlace actor, SequenceActorDrawing drawing, int count, bool footer, string svgId)
	{
		if (actor.Type is not ("actor" or "boundary" or "control" or "entity"))
		{
			return;
		}

		var conf = layout.Conf;
		var neo = conf.Look == "neo";
		var shadow = "url(#" + svgId + "-drop-shadow)";
		var position = footer ? " actor-bottom" : " actor-top";
		var center = actor.X + (actor.Width / 2);
		var shape = drawing.Shape;
		const double Radius = 22;
		var shift = neo ? 0 : actor.Type switch
		{
			"boundary" => (Radius / 2) + 10,
			"entity" => footer ? Radius : (Radius / 2) - 5,
			_ => 0,
		};
		var cnt = count.ToString(CultureInfo.InvariantCulture);

		svg.StartElement("g");
		svg.WriteAttribute("class", Classes((actor.Type == "entity" ? "actor" : "actor-man") + position, actor.PaletteClass));
		if (!footer)
		{
			svg.WriteAttribute("data-et", "participant");
			svg.WriteAttribute("data-type", actor.Type);
			svg.WriteAttribute("data-id", actor.Name);
		}

		if (neo && actor.Type is "boundary" or "entity")
		{
			svg.WriteAttribute("filter", shadow);
		}

		if (actor.PaletteClass is null && actor.Type != "entity")
		{
			Paint(svg, "stroke", layout.ActorBorder);
			if (actor.Type == "control")
			{
				Paint(svg, "fill", layout.ActorBackground);
			}
		}

		if (shift != 0)
		{
			svg.WriteAttribute("transform", Translate(0, shift));
		}

		switch (actor.Type)
		{
			case "actor":
				{
					var scale = neo ? 44.0 / 65 : 1;
					double Gy(double offset) => shape.Bottom - ((60 - offset) * scale);
					double Gx(double offset) => center + (offset * scale);
					WriteLine(svg, "actor-man-torso" + cnt, center, Gy(25), center, Gy(45));
					WriteLine(svg, "actor-man-arms" + cnt, Gx(-18), Gy(33), Gx(18), Gy(33));
					WriteLine(svg, null, Gx(-18), Gy(60), center, Gy(45));
					WriteLine(svg, null, center, Gy(45), Gx(16), Gy(60));
					WriteCircle(svg, center, Gy(10), 15 * scale, actor.Width, actor.Height, null);
					break;
				}

			case "boundary":
				{
					var iconCenterY = shape.Y - shift + Radius;
					WriteLine(svg, "actor-man-torso" + cnt, center - (Radius * 2.5), iconCenterY, center - 15, iconCenterY);
					WriteLine(svg, "actor-man-arms" + cnt, center - (Radius * 2.5), iconCenterY - 10, center - (Radius * 2.5), iconCenterY + 10);
					WriteCircle(svg, center, iconCenterY, Radius, null, null, null);
					break;
				}

			case "control":
				{
					var cy = shape.Y + Radius;
					svg.StartElement("defs");
					svg.StartElement("marker");
					svg.WriteAttribute("id", svgId + "-filled-head-control");
					svg.WriteAttribute("refX", 11);
					svg.WriteAttribute("refY", 5.8);
					svg.WriteAttribute("markerWidth", 20);
					svg.WriteAttribute("markerHeight", 28);
					svg.WriteAttribute("orient", "172.5");
					svg.WriteAttribute("stroke-width", 1.2);
					WritePath(svg, "M 14.4 5.6 L 7.2 10.4 L 8.8 5.6 L 7.2 0.8 Z");
					svg.EndElement();
					svg.EndElement();
					WriteCircle(svg, center, cy, Radius, null, null, neo ? shadow : null);
					svg.StartElement("line");
					svg.WriteAttribute("marker-end", "url(#" + svgId + "-filled-head-control)");
					svg.WriteAttribute("transform", Translate(center, cy - Radius));
					svg.EndElement();
					break;
				}

			case "entity":
				{
					var cy = shape.Y - shift + Radius;
					WriteCircle(svg, center, cy, Radius, actor.Width, actor.Height, null);
					svg.StartElement("line");
					svg.WriteAttribute("x1", center - Radius);
					svg.WriteAttribute("x2", center + Radius);
					svg.WriteAttribute("y1", cy + Radius);
					svg.WriteAttribute("y2", cy + Radius);
					svg.WriteAttribute("stroke-width", 2);
					svg.EndElement();
					break;
				}
		}

		WriteTspanLabel(svg, drawing.Label, conf.ActorFont, "actor actor-man", shift);
		svg.EndElement();
	}

	/// <summary>
	/// <c>drawMessage</c>: the text, the line or self loop with its markers, the circles of a central connection, and the
	/// number's circle and text.
	/// </summary>
	private static void WriteMessage(SvgWriter svg, SequenceMessagePlace message, SequenceConf conf, string svgId)
	{
		var (anchor, middle) = Anchor(conf.MessageAlign);
		foreach (var line in message.Text)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", line.X);
			svg.WriteAttribute("y", line.Y);
			WriteAnchor(svg, anchor, middle);
			WriteFont(svg, conf.MessageFont);
			svg.WriteAttribute("class", "messageText");
			svg.WriteAttribute("dy", "1em");
			svg.WriteText(Shown(line.Text, ZeroWidthSpace));
			svg.EndElement();
		}

		var type = (SequenceArrow)message.Type;
		if (message.SelfPath is { } path)
		{
			svg.StartElement("path");
			svg.WriteAttribute("d", path);
		}
		else
		{
			svg.StartElement("line");
			svg.WriteAttribute("x1", message.X1);
			svg.WriteAttribute("y1", message.LineY);
			svg.WriteAttribute("x2", message.X2);
			svg.WriteAttribute("y2", message.LineY);
		}

		svg.WriteAttribute("class", IsDotted(type) ? "messageLine1" : "messageLine0");
		svg.WriteAttribute("data-et", "message");
		svg.WriteAttribute("data-id", "i" + message.Id.ToString(CultureInfo.InvariantCulture));
		svg.WriteAttribute("data-from", message.From);
		svg.WriteAttribute("data-to", message.To);
		svg.WriteAttribute("stroke-width", 2);
		svg.WriteAttribute("stroke", "none");
		svg.WriteAttribute("fill", "none");
		var (start, end) = Markers(type);
		if (end is not null)
		{
			svg.WriteAttribute("marker-end", "url(#" + svgId + "-" + end + ")");
		}

		if (start is not null)
		{
			svg.WriteAttribute("marker-start", "url(#" + svgId + "-" + start + ")");
		}

		svg.EndElement();
		if (message.CentralCircles.Count > 0)
		{
			svg.StartElement("g");
			foreach (var cx in message.CentralCircles)
			{
				WriteCircle(svg, cx, message.LineY, 5, 10, 10, null);
			}

			svg.EndElement();
		}

		if (message.Number is { } number)
		{
			var text = JsNumber.ToString(number);
			svg.StartElement("line");
			svg.WriteAttribute("x1", message.NumberX);
			svg.WriteAttribute("y1", message.LineY);
			svg.WriteAttribute("x2", message.NumberX);
			svg.WriteAttribute("y2", message.LineY);
			svg.WriteAttribute("stroke-width", 0);
			svg.WriteAttribute("marker-start", "url(#" + svgId + "-sequencenumber)");
			svg.EndElement();
			svg.StartElement("text");
			svg.WriteAttribute("x", message.NumberX);
			svg.WriteAttribute("y", message.LineY + 4);
			svg.WriteAttribute("font-family", "sans-serif");
			svg.WriteAttribute("font-size", text.Length > 5 ? "7px" : text.Length > 3 ? "9px" : "12px");
			svg.WriteAttribute("text-anchor", "middle");
			svg.WriteAttribute("class", "sequenceNumber");
			svg.WriteText(text);
			svg.EndElement();
		}
	}

	/// <summary><c>drawNote</c>: the note's rectangle and its text, a tspan per line.</summary>
	private static void WriteNote(SvgWriter svg, SequenceNotePlace note, SequenceConf conf, bool neo)
	{
		svg.StartElement("g");
		svg.WriteAttribute("data-et", "note");
		svg.WriteAttribute("data-id", "i" + note.Id.ToString(CultureInfo.InvariantCulture));
		WriteRect(svg, note.Rect, NoteFill, NoteStroke, "note", null, neo);
		var (anchor, middle) = Anchor(conf.NoteAlign);
		foreach (var line in note.Lines)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", line.X);
			svg.WriteAttribute("y", line.Y);
			WriteAnchor(svg, anchor, middle);
			WriteFont(svg, conf.NoteFont);
			svg.WriteAttribute("class", "noteText");
			svg.WriteAttribute("dy", "1em");
			svg.StartElement("tspan");
			svg.WriteAttribute("x", line.X);
			svg.WriteText(Shown(line.Text, ZeroWidthSpace));
			svg.EndElement();
			svg.EndElement();
		}

		svg.EndElement();
	}

	/// <summary>
	/// <c>drawLoop</c>: the four sides of the frame, a dashed line for each branch, the label box with its text in the
	/// corner, the title, and each branch's title.
	/// </summary>
	private static void WriteBlock(SvgWriter svg, SequenceBlockPlace block, SequenceConf conf)
	{
		var frame = block.Frame;
		svg.StartElement("g");
		svg.WriteAttribute("data-et", "control-structure");
		svg.WriteAttribute("data-id", "i" + block.Id.ToString(CultureInfo.InvariantCulture));
		void LoopLine(double x1, double y1, double x2, double y2)
		{
			svg.StartElement("line");
			svg.WriteAttribute("x1", x1);
			svg.WriteAttribute("y1", y1);
			svg.WriteAttribute("x2", x2);
			svg.WriteAttribute("y2", y2);
			svg.WriteAttribute("class", "loopLine");
			svg.EndElement();
		}

		LoopLine(frame.X, frame.Y, frame.Right, frame.Y);
		LoopLine(frame.Right, frame.Y, frame.Right, frame.Bottom);
		LoopLine(frame.X, frame.Bottom, frame.Right, frame.Bottom);
		LoopLine(frame.X, frame.Y, frame.X, frame.Bottom);
		foreach (var y in block.Sections)
		{
			LoopLine(frame.X, y, frame.Right, y);
		}

		var box = block.LabelBox;
		const double Cut = 7;
		svg.StartElement("polygon");
		svg.WriteAttribute("points", string.Create(CultureInfo.InvariantCulture, $"{N(box.X)},{N(box.Y)} {N(box.Right)},{N(box.Y)} {N(box.Right)},{N(box.Bottom - Cut)} {N(box.Right - (Cut * 1.2))},{N(box.Bottom)} {N(box.X)},{N(box.Bottom)}"));
		svg.WriteAttribute("class", "labelBox");
		svg.EndElement();
		var font = conf.MessageFont;
		WriteLines(svg, block.Label, "middle", true, font, "labelText", tspan: false);
		WriteLines(svg, block.Title, "middle", false, font, "loopText", tspan: true);
		WriteLines(svg, block.SectionTitles, "middle", false, font, "sectionTitle", tspan: false);
		svg.EndElement();
	}

	/// <summary>The texts of <c>drawText</c> without a <c>dy</c>, one per line.</summary>
	private static void WriteLines(SvgWriter svg, IReadOnlyList<SequenceTextLine> lines, string anchor, bool middle, FontSpec font, string cls, bool tspan)
	{
		foreach (var line in lines)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", line.X);
			svg.WriteAttribute("y", line.Y);
			WriteAnchor(svg, anchor, middle);
			WriteFont(svg, font);
			svg.WriteAttribute("class", cls);
			var text = Shown(line.Text, ZeroWidthSpace);
			if (tspan)
			{
				svg.StartElement("tspan");
				svg.WriteAttribute("x", line.X);
				svg.WriteText(text);
				svg.EndElement();
			}
			else
			{
				svg.WriteText(text);
			}

			svg.EndElement();
		}
	}

	/// <summary>
	/// <c>byTspan</c>: a text per line, centred with <c>dominant-baseline: central</c>, and a tspan moved by the line's
	/// <c>dy</c>. <paramref name="shift"/> is the translate of the group the label is in.
	/// </summary>
	private static void WriteTspanLabel(SvgWriter svg, IReadOnlyList<SequenceTextLine> lines, FontSpec font, string cls, double shift)
	{
		foreach (var line in lines)
		{
			svg.StartElement("text");
			svg.WriteAttribute("x", line.X);
			svg.WriteAttribute("y", line.Y - line.Dy - shift);
			svg.WriteAttribute("text-anchor", "middle");
			WriteFont(svg, font);
			svg.WriteAttribute("dominant-baseline", "central");
			svg.WriteAttribute("alignment-baseline", "central");
			svg.WriteAttribute("class", cls);
			svg.StartElement("tspan");
			svg.WriteAttribute("x", line.X);
			svg.WriteAttribute("dy", line.Dy);
			svg.WriteText(Shown(line.Text, ""));
			svg.EndElement();
			svg.EndElement();
		}
	}

	/// <summary>
	/// <c>drawRect</c> of <c>svgDraw.js</c>: <c>svgDrawCommon.drawRect</c>, with <c>data-look</c> under <c>neo</c>. A
	/// fill or stroke outside the writer's grammar is left out.
	/// </summary>
	private static void WriteRect(SvgWriter svg, SequenceRect rect, string? fill, string? stroke, string cls, double? radius, bool neo = false, string? filter = null)
	{
		svg.StartElement("rect");
		svg.WriteAttribute("x", rect.X);
		svg.WriteAttribute("y", rect.Y);
		Paint(svg, "fill", fill);
		Paint(svg, "stroke", stroke);
		svg.WriteAttribute("width", rect.Width);
		svg.WriteAttribute("height", rect.Height);
		if (radius is { } r)
		{
			svg.WriteAttribute("rx", r);
			svg.WriteAttribute("ry", r);
		}

		svg.WriteAttribute("class", cls);
		if (neo)
		{
			svg.WriteAttribute("data-look", "neo");
		}

		if (filter is not null)
		{
			svg.WriteAttribute("filter", filter);
		}

		svg.EndElement();
	}

	private static void WriteLine(SvgWriter svg, string? id, double x1, double y1, double x2, double y2)
	{
		svg.StartElement("line");
		if (id is not null)
		{
			svg.WriteAttribute("id", id);
		}

		svg.WriteAttribute("x1", x1);
		svg.WriteAttribute("y1", y1);
		svg.WriteAttribute("x2", x2);
		svg.WriteAttribute("y2", y2);
		svg.EndElement();
	}

	private static void WriteCircle(SvgWriter svg, double cx, double cy, double r, double? width, double? height, string? filter)
	{
		svg.StartElement("circle");
		svg.WriteAttribute("cx", cx);
		svg.WriteAttribute("cy", cy);
		svg.WriteAttribute("r", r);
		if (width is { } w && height is { } h)
		{
			svg.WriteAttribute("width", w);
			svg.WriteAttribute("height", h);
		}

		if (filter is not null)
		{
			svg.WriteAttribute("filter", filter);
		}

		svg.EndElement();
	}

	private static void WritePath(SvgWriter svg, string d)
	{
		svg.StartElement("path");
		svg.WriteAttribute("d", d);
		svg.EndElement();
	}

	/// <summary>
	/// The markers of <c>insertArrowHead</c>, <c>insertArrowCrossHead</c>, <c>insertArrowFilledHead</c>,
	/// <c>insertSequenceNumber</c> and the four half arrowheads, each in a <c>defs</c> of its own, and under <c>neo</c>
	/// the filter of <c>insertDropShadow</c>.
	/// </summary>
	private static void WriteMarkers(SvgWriter svg, string svgId, bool neo, string theme)
	{
		void Start(string suffix, params (string Name, string Value)[] attributes)
		{
			svg.StartElement("defs");
			svg.StartElement("marker");
			svg.WriteAttribute("id", svgId + suffix);
			foreach (var (name, value) in attributes)
			{
				svg.WriteAttribute(name, value);
			}
		}

		void Path(params (string Name, string Value)[] attributes)
		{
			svg.StartElement("path");
			foreach (var (name, value) in attributes)
			{
				svg.WriteAttribute(name, value);
			}

			svg.EndElement();
		}

		void End()
		{
			svg.EndElement();
			svg.EndElement();
		}

		(string, string)[] userSpace = [("markerUnits", "userSpaceOnUse"), ("markerWidth", "12"), ("markerHeight", "12"), ("orient", "auto-start-reverse")];
		Start("-arrowhead", [("refX", "7.9"), ("refY", "5"), .. userSpace]);
		Path(("d", "M -1 0 L 10 5 L 0 10 z"));
		End();
		Start("-crosshead", ("markerWidth", "15"), ("markerHeight", "8"), ("orient", "auto"), ("refX", "4"), ("refY", "4.5"));
		Path(("fill", "none"), ("stroke", "#000000"), ("stroke-dasharray", "0, 0"), ("stroke-width", "1pt"), ("d", "M 1,2 L 6,7 M 6,2 L 1,7"));
		End();
		Start("-filled-head", ("refX", "15.5"), ("refY", "7"), ("markerWidth", "20"), ("markerHeight", "28"), ("orient", "auto"));
		Path(("d", "M 18,7 L9,13 L14,7 L9,1 Z"));
		End();
		Start("-sequencenumber", ("refX", "15"), ("refY", "15"), ("markerWidth", "60"), ("markerHeight", "40"), ("orient", "auto"));
		WriteCircle(svg, 15, 15, 6, null, null, null);
		End();
		Start("-solidTopArrowHead", [("refX", "7.9"), ("refY", "7.25"), .. userSpace]);
		Path(("d", "M 0 0 L 10 8 L 0 8 z"));
		End();
		Start("-solidBottomArrowHead", [("refX", "7.9"), ("refY", "0.75"), .. userSpace]);
		Path(("d", "M 0 0 L 10 0 L 0 8 z"));
		End();
		Start("-stickTopArrowHead", [("refX", "7.5"), ("refY", "7"), .. userSpace]);
		Path(("d", "M 0 0 L 7 7"), ("stroke", "black"), ("stroke-width", "1.5"), ("fill", "none"));
		End();
		Start("-stickBottomArrowHead", [("refX", "7.5"), ("refY", "0"), .. userSpace]);
		Path(("d", "M 0 7 L 7 0"), ("stroke", "black"), ("stroke-width", "1.5"), ("fill", "none"));
		End();
		if (neo)
		{
			svg.StartElement("defs");
			svg.StartElement("filter");
			svg.WriteAttribute("id", svgId + "-drop-shadow");
			svg.WriteAttribute("height", "130%");
			svg.WriteAttribute("width", "130%");
			svg.StartElement("feDropShadow");
			svg.WriteAttribute("dx", "4");
			svg.WriteAttribute("dy", "4");
			svg.WriteAttribute("stdDeviation", 0);
			svg.WriteAttribute("flood-opacity", "0.06");
			svg.WriteAttribute("flood-color", theme is "redux" or "redux-color" ? "#000000" : "#FFFFFF");
			svg.EndElement();
			svg.EndElement();
			svg.EndElement();
		}
	}

	/// <summary>The marker at each end of a message's line: the <c>marker-start</c> and the <c>marker-end</c> of <c>drawMessage</c>.</summary>
	internal static (string? Start, string? End) Markers(SequenceArrow type) => type switch
	{
		SequenceArrow.Solid or SequenceArrow.Dotted => (null, "arrowhead"),
		SequenceArrow.BidirectionalSolid or SequenceArrow.BidirectionalDotted => ("arrowhead", "arrowhead"),
		SequenceArrow.SolidPoint or SequenceArrow.DottedPoint => (null, "filled-head"),
		SequenceArrow.SolidCross or SequenceArrow.DottedCross => (null, "crosshead"),
		SequenceArrow.SolidTop or SequenceArrow.SolidTopDotted => (null, "solidTopArrowHead"),
		SequenceArrow.SolidBottom or SequenceArrow.SolidBottomDotted => (null, "solidBottomArrowHead"),
		SequenceArrow.StickTop or SequenceArrow.StickTopDotted => (null, "stickTopArrowHead"),
		SequenceArrow.StickBottom or SequenceArrow.StickBottomDotted => (null, "stickBottomArrowHead"),
		SequenceArrow.SolidArrowTopReverse or SequenceArrow.SolidArrowTopReverseDotted => ("solidBottomArrowHead", null),
		SequenceArrow.SolidArrowBottomReverse or SequenceArrow.SolidArrowBottomReverseDotted => ("solidTopArrowHead", null),
		SequenceArrow.StickArrowTopReverse or SequenceArrow.StickArrowTopReverseDotted => ("stickBottomArrowHead", null),
		SequenceArrow.StickArrowBottomReverse or SequenceArrow.StickArrowBottomReverseDotted => ("stickTopArrowHead", null),
		_ => (null, null),
	};

	/// <summary>The line types <c>drawMessage</c> dashes.</summary>
	internal static bool IsDotted(SequenceArrow type) => type is SequenceArrow.Dotted or SequenceArrow.DottedCross or SequenceArrow.DottedPoint
		or SequenceArrow.DottedOpen or SequenceArrow.BidirectionalDotted or SequenceArrow.SolidTopDotted or SequenceArrow.SolidBottomDotted
		or SequenceArrow.StickTopDotted or SequenceArrow.StickBottomDotted or SequenceArrow.SolidArrowTopReverseDotted
		or SequenceArrow.SolidArrowBottomReverseDotted or SequenceArrow.StickArrowTopReverseDotted or SequenceArrow.StickArrowBottomReverseDotted;

	private static bool IsKnownType(string type) => type is "participant" or "collections" or "queue" or "database" or "actor" or "boundary" or "control" or "entity";

	/// <summary>
	/// <c>drawText</c>'s anchor for an alignment: <c>start</c>, <c>middle</c> or <c>end</c>, centred on the line with
	/// <c>dominant-baseline: middle</c>, or the alignment itself without a baseline when it is none of those.
	/// </summary>
	private static (string Anchor, bool Middle) Anchor(string align) => align switch
	{
		"left" or "start" => ("start", true),
		"middle" or "center" => ("middle", true),
		"right" or "end" => ("end", true),
		_ => (align, false),
	};

	private static void WriteAnchor(SvgWriter svg, string anchor, bool middle)
	{
		svg.WriteAttribute("text-anchor", anchor);
		if (middle)
		{
			svg.WriteAttribute("dominant-baseline", "middle");
			svg.WriteAttribute("alignment-baseline", "middle");
		}
	}

	private static void WriteFont(SvgWriter svg, FontSpec font)
	{
		if (font.Family.Length > 0)
		{
			svg.WriteAttribute("font-family", font.Family);
		}

		svg.WriteAttribute("font-size", N(font.Size) + "px");
		svg.WriteAttribute("font-weight", font.Weight.ToString(CultureInfo.InvariantCulture));
	}

	private static void WriteClass(SvgWriter svg, string? cls)
	{
		if (cls is not null)
		{
			svg.WriteAttribute("class", cls);
		}
	}

	private static string Classes(string classes, string? palette) => palette is null ? classes : classes + " " + palette;

	/// <summary>The text the SVG shows for a line: its entity placeholders restored and read as HTML.</summary>
	private static string Shown(string line, string empty) =>
		line.Length == 0 ? empty : FlowchartLabels.HtmlTextOf(SequenceDb.Restore(line));

	private static void Paint(SvgWriter svg, string name, string? value)
	{
		if (value is not null && SvgValues.IsAllowedPaint(value))
		{
			svg.WriteAttribute(name, value);
		}
	}

	private static string Translate(double x, double y) => "translate(" + N(x) + ", " + N(y) + ")";

	private static string N(double value) => JsNumber.ToString(value);
}
