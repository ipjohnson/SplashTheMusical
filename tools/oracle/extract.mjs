// The in-page half of the facts runner. facts.mjs hands extractFacts to page.evaluate, so the function and everything it
// calls must live inside it: puppeteer sends the function's source to the page, not its module scope.
//
// Structure comes from the diagram's db. Flowchart, swimlane, state and class labels come from the rendered DOM, because
// the db keeps entities as placeholders and markdown unrendered, and the DOM finds each element by its id. The other
// types draw charts and trees whose DOM has no ids to find an element by, so their facts come from the db, and `label`
// facts say which of the db's labels the DOM shows. Every accepted case gets `type` from the detector and `drawn` from
// the DOM.
export async function extractFacts(source, name, shapes) {
	const norm = s => String(s ?? '').replace(/\s+/g, ' ').trim()
	// mermaid's own entity placeholders (see encodeEntities in mermaid's utils), then HTML entities and <br>.
	const decode = s => {
		const html = String(s ?? '').replace(/ﬂ°°/g, '&#').replace(/ﬂ°/g, '&').replace(/¶ß/g, ';').replace(/<br\s*\/?>/gi, ' ')
		const el = document.createElement('div')
		el.innerHTML = html
		return norm(el.textContent)
	}
	const tab = (...parts) => parts.map(p => norm(p).replace(/\t/g, ' ')).join('\t').replace(/\t+$/, '')
	// Numbers rounded to 3 decimals, in their shortest form.
	const num = v => v === undefined || v === null || v === '' ? '' : String(Math.round(Number(v) * 1000) / 1000)
	const shape = s => shapes[s] ?? s

	let diagram
	try {
		diagram = await mermaid.mermaidAPI.getDiagramFromText(source)
	} catch (e) {
		return { error: String(e?.message ?? e) }
	}
	const svgId = 'mjs' + name.replace(/[^A-Za-z0-9]/g, '')
	let svg
	try {
		svg = (await mermaid.render(svgId, source)).svg
	} catch (e) {
		return { error: String(e?.message ?? e) }
	}
	// render() parses the source again into a fresh db; re-read it so the db matches the DOM.
	diagram = await mermaid.mermaidAPI.getDiagramFromText(source)
	const host = document.createElement('div')
	host.innerHTML = svg
	document.body.replaceChildren(host)
	const dom = host.querySelector('svg')
	// textContent runs the lines of a <br> together; read each <br> as a space.
	const text = el => {
		if (!el) return ''
		const copy = el.cloneNode(true)
		for (const br of copy.querySelectorAll('br')) br.replaceWith(' ')
		return norm(copy.textContent)
	}
	// Ids from the source can hold quotes and newlines, so they are escaped before they go into a selector.
	const esc = s => CSS.escape(String(s))
	// A domId ends in a counter that moves between parses, so fall back to the same id with any counter.
	const byDomId = domId => {
		const exact = dom.querySelector(`[id="${esc(`${svgId}-${domId}`)}"]`)
		if (exact || !domId) return exact
		const stem = `${svgId}-${domId.replace(/-\d+$/, '')}-`
		return [...dom.querySelectorAll('[id]')].find(el => el.id.startsWith(stem) && /^\d+$/.test(el.id.slice(stem.length))) ?? null
	}
	// The swimlane layout draws an edge label as g.edgeLabel#edge-label-<start>-<end>-<edge id>, without a data-id.
	const edgeLabel = id => text(dom.querySelector(`.edgeLabel .label[data-id="${esc(id)}"]`) ?? dom.querySelector(`g.edgeLabel[id$="-${esc(id)}"]`))
	const clusterLabel = id => text(dom.querySelector(`.clusters [data-id="${esc(id)}"] .cluster-label`) ?? dom.querySelector(`.clusters [id="${esc(`${svgId}-${id}`)}"] .cluster-label`))
	// An icon class list such as "fa fa-book" or "fab fa-twitter", written as mermaid's source writes it: "fa:fa-book".
	const iconName = s => norm(s).split(' ').filter(c => c && c !== 'fa-fw').slice(0, 2).join(':')

	const db = diagram.db
	const facts = []
	const type = diagram.type
	const extractors = {
		'flowchart-v2': flowchart, 'flowchart-elk': flowchart, flowchart, swimlane: flowchart, stateDiagram: state,
		classDiagram, sequence, er, pie, quadrantChart: quadrant, timeline, gitGraph, radar, treemap, venn, mindmap, gantt,
		journey, c4, sankey, xychart, requirement, packet, kanban, architecture, block, treeView, usecase, wardley, zenuml,
		railroad, railroadEbnf: railroad, railroadAbnf: railroad, railroadPeg: railroad, agentflow, cynefin, eventmodeling,
		info, ishikawa,
	}
	const extract = extractors[type]
	if (!extract) return { unknownType: type }
	extract()
	facts.push(tab('type', type))
	facts.push(tab('drawn', drawn() ? 'yes' : 'no'))
	return { facts, svg }

	// Whether the DOM shows anything: a shape or a non-empty text with a size, outside defs and markers.
	function drawn() {
		for (const el of dom.querySelectorAll('rect, path, circle, ellipse, polygon, polyline, line, image, text, foreignObject')) {
			if (el.closest('defs, marker, clipPath, mask, pattern, symbol')) continue
			if ((el.localName === 'text' || el.localName === 'foreignObject') && !text(el)) continue
			const style = getComputedStyle(el)
			if (style.display === 'none' || style.visibility === 'hidden') continue
			const r = el.getBoundingClientRect()
			if (r.width > 0 || r.height > 0) return true
		}
		return false
	}

	// Writes a `label` fact for each model label the DOM shows. Two renderers style the same label differently: upper
	// case, wrapped at another width or mid-word, joined with its value ("Chrome [65.3]"), so a label counts as shown when
	// its letters and digits, case-folded, occur in all the drawn text joined the same way. Longer labels are matched first
	// and each match is used up, so two equal labels need two drawn copies.
	function labels(list) {
		const key = s => norm(s).toLowerCase().replace(/[^\p{L}\p{N}]/gu, '')
		let drawnText = ''
		for (const el of dom.querySelectorAll('text, foreignObject')) {
			if (el.closest('defs, marker, clipPath, mask, pattern, symbol') || el.parentElement?.closest('text')) continue
			const style = getComputedStyle(el)
			if (style.display === 'none' || style.visibility === 'hidden') continue
			drawnText += key(el.textContent)
		}
		for (const label of list.map(norm).filter(l => key(l)).sort((a, b) => key(b).length - key(a).length || (a < b ? -1 : a > b ? 1 : 0))) {
			const at = drawnText.indexOf(key(label))
			if (at < 0) continue
			drawnText = drawnText.slice(0, at) + '\u0000' + drawnText.slice(at + key(label).length)
			facts.push(tab('label', label))
		}
	}

	function title(shown, value = db.getDiagramTitle?.()) {
		if (!value) return
		facts.push(tab('title', decode(value)))
		shown.push(decode(value))
	}

	function end(t) {
		switch (t) {
			case 'arrow_point': return 'arrow'
			case 'arrow_circle': return 'circle'
			case 'arrow_cross': return 'cross'
			case 'arrow_barb': return 'arrow'
			case 'extension': return 'triangle'
			case 'composition': return 'diamond-filled'
			case 'aggregation': return 'diamond'
			case 'dependency': return 'arrow'
			case 'lollipop': return 'lollipop'
			case undefined: case null: case 'none': case 'arrow_open': return 'none'
			default: return t
		}
	}

	function members(data) {
		const children = new Map()
		for (const n of data.nodes) {
			if (!n.parentId) continue
			if (!children.has(n.parentId)) children.set(n.parentId, [])
			children.get(n.parentId).push(n.id)
		}
		return id => (children.get(id) ?? []).slice().sort().join(',')
	}

	// Flowchart, flowchart-elk and swimlane, which share flowchart's db. Model: ids, group members, shapes, icon and image
	// names, edge ends and line styles. DOM: labels, and the FontAwesome icons a label draws (`fa:fa-name` in the source
	// becomes an <i class="fa fa-name">).
	function flowchart() {
		const data = db.getData()
		const childrenOf = members(data)
		for (const n of data.nodes) {
			if (n.isGroup) {
				facts.push(tab('group', n.id, clusterLabel(n.id), childrenOf(n.id)))
				continue
			}
			// A collapsed subgraph is drawn under the subgraph's id, not a node domId.
			const el = byDomId(n.domId) ?? dom.querySelector(`[id="${svgId}-${n.id}"]`)
			// An icon or image node's shape is its frame; the icon or image is its own fact.
			const s = n.icon || n.img ? 'rect' : shape(n.shape)
			facts.push(tab('node', n.id, text(el?.querySelector('.label')), s))
			if (n.icon) facts.push(tab('icon', n.id, n.icon))
			if (n.img) facts.push(tab('image', n.id, n.img))
			for (const i of el?.querySelectorAll('.label i[class*="fa"]') ?? []) facts.push(tab('icon', n.id, iconName(i.getAttribute('class'))))
		}
		const strokes = new Map(db.getEdges().map(e => [e.id, e.stroke]))
		for (const e of data.edges) {
			const stroke = strokes.get(e.id) ?? e.thickness
			const line = stroke === 'invisible' ? 'invisible' : stroke === 'thick' ? 'thick' : stroke === 'dotted' || e.pattern === 'dotted' ? 'dotted' : 'solid'
			facts.push(tab('edge', e.start, e.end, end(e.arrowTypeStart), end(e.arrowTypeEnd), line, edgeLabel(e.id)))
		}
	}

	// Model: ids, nesting, shapes, notes and transitions. DOM: state and transition labels.
	function state() {
		const data = db.getData()
		const nodes = new Map(data.nodes.map(n => [n.id, n]))
		// A note sits with its state in a wrapper group "<state>----parent"; lift the wrapper's children to its parent.
		const parentOf = id => {
			let p = nodes.get(id)?.parentId ?? null
			while (p && p.endsWith('----parent')) p = nodes.get(p)?.parentId ?? null
			return p && region(p)
		}
		// The last concurrent region of a state gets a random id, id-<random>-<n>; the others are divider-id-<n>.
		const region = id => id.replace(/^id-[a-z0-9]+-(\d+)$/, 'divider-id-$1')
		const isNote = n => n.shape === 'note' || n.id.includes('----note')
		const name = id => {
			const n = nodes.get(id)
			if (n?.shape === 'stateStart') return `[*]start@${parentOf(id) ?? 'root'}`
			if (n?.shape === 'stateEnd') return `[*]end@${parentOf(id) ?? 'root'}`
			return region(id)
		}
		const children = new Map()
		for (const n of data.nodes) {
			if (n.id.endsWith('----parent') || isNote(n)) continue
			const p = parentOf(n.id)
			if (!p) continue
			if (!children.has(p)) children.set(p, new Set())
			children.get(p).add(name(n.id))
		}
		const seen = new Set()
		for (const n of data.nodes) {
			if (n.id.endsWith('----parent')) continue
			if (isNote(n)) {
				const target = n.id.split('----')[0]
				facts.push(tab('note', target, text(byDomId(n.domId)) || decode(n.label)))
				continue
			}
			if (n.isGroup) {
				facts.push(tab('group', region(n.id), clusterLabel(n.id), [...(children.get(region(n.id)) ?? [])].sort().join(',')))
				continue
			}
			const id = name(n.id)
			if (seen.has(id)) continue
			seen.add(id)
			// A plain state is drawn as a rounded rectangle; choice is a diamond; fork and join are one filled bar.
			const s = n.shape === 'choice' ? 'diam' : n.shape === 'fork' || n.shape === 'join' ? 'fork' : n.shape === 'rect' ? 'rounded' : shape(n.shape)
			const label = s === 'rounded' ? text(byDomId(n.domId)?.querySelector('.label')) : ''
			facts.push(tab('node', id, label, s))
		}
		for (const e of data.edges) {
			if (isNote(nodes.get(e.start) ?? {}) || isNote(nodes.get(e.end) ?? {})) continue
			facts.push(tab('edge', name(e.start), name(e.end), end(e.arrowTypeStart), end(e.arrowTypeEnd), 'solid', edgeLabel(e.id)))
		}
	}

	// Model: classes, namespaces, notes, relation ends and cardinalities. DOM: class names, annotations, members and
	// relation labels.
	function classDiagram() {
		const data = db.getData()
		const nodes = new Map(data.nodes.map(n => [n.id, n]))
		// A lollipop end is a synthetic interface node; name it by its label.
		const interfaces = new Map((db.interfaces ?? []).map(i => [i.id, '()' + decode(i.label)]))
		const name = id => interfaces.get(id) ?? id
		for (const n of data.nodes) {
			if (interfaces.has(n.id)) continue
			if (n.isGroup) {
				const kids = data.nodes.filter(c => c.parentId === n.id).map(c => c.id).sort().join(',')
				facts.push(tab('namespace', n.id, kids))
				continue
			}
			if (n.shape === 'note') continue
			const el = byDomId(n.domId)
			facts.push(tab('class', n.id, text(el?.querySelector('.label-group'))))
			for (const a of el?.querySelectorAll('.annotation-group .label') ?? []) facts.push(tab('annotation', n.id, text(a)))
			for (const m of el?.querySelectorAll('.members-group .label') ?? []) facts.push(tab('attribute', n.id, text(m)))
			for (const m of el?.querySelectorAll('.methods-group .label') ?? []) facts.push(tab('method', n.id, text(m)))
		}
		const notes = db.getNotes?.() ?? []
		for (const note of notes instanceof Map ? notes.values() : Object.values(notes)) facts.push(tab('note', note.class ?? '', decode(note.text)))
		for (const e of data.edges) {
			if (nodes.get(e.start)?.shape === 'note' || nodes.get(e.end)?.shape === 'note') continue
			const line = e.pattern === 'dashed' || e.pattern === 'dotted' ? 'dashed' : 'solid'
			const card = v => { const d = decode(v); return d === 'none' ? '' : d }
			facts.push(tab('relation', name(e.start), name(e.end), end(e.arrowTypeStart), end(e.arrowTypeEnd), line, edgeLabel(e.id), card(e.startLabelRight), card(e.endLabelLeft)))
		}
	}

	// Model: participants and their types, boxes, messages, notes and blocks. The participant type is mermaid's own name
	// (participant, actor, boundary, control, entity, database, collections, queue).
	function sequence() {
		for (const [id, a] of Object.entries(Object.fromEntries(db.getActors()))) facts.push(tab('actor', id, decode(a.description), a.type ?? 'participant'))
		for (const b of db.getBoxes?.() ?? []) facts.push(tab('box', decode(b.name), (b.actorKeys ?? []).slice().sort().join(',')))
		// mermaid's sequence LINETYPE values.
		const message = {
			0: ['solid', 'filled'], 1: ['dashed', 'filled'], 3: ['solid', 'cross'], 4: ['dashed', 'cross'],
			5: ['solid', 'none'], 6: ['dashed', 'none'], 24: ['solid', 'open'], 25: ['dashed', 'open'],
			33: ['solid', 'filled-both'], 34: ['dashed', 'filled-both'],
		}
		const blockStart = { 10: 'loop', 12: 'alt', 15: 'opt', 19: 'par', 22: 'rect', 27: 'critical', 30: 'break', 32: 'par' }
		const divider = { 13: 'else', 20: 'and', 28: 'option' }
		const placement = { 0: 'left', 1: 'right', 2: 'over' }
		for (const m of db.getMessages()) {
			if (m.type in message) facts.push(tab('message', m.from, m.to, ...message[m.type], decode(m.message)))
			else if (m.type === 2) facts.push(tab('note', placement[m.placement] ?? m.placement, [m.from, m.to].filter((v, i, a) => v && a.indexOf(v) === i).join(','), decode(m.message)))
			else if (m.type in blockStart) facts.push(tab('block', blockStart[m.type], m.type === 22 ? '' : decode(m.message)))
			else if (m.type in divider) facts.push(tab('divider', decode(m.message)))
		}
	}

	// Model: entities with their display names, attributes and relationships. mermaid's relSpec names the cardinality at
	// entityB's end cardA and the one at entityA's end cardB; the fact lists them by the entity they sit at. Labels: entity
	// names, attribute types, names and comments, relationship labels.
	function er() {
		const cardinality = { ONLY_ONE: 'one', ZERO_OR_ONE: 'zero-or-one', ONE_OR_MORE: 'one-or-more', ZERO_OR_MORE: 'zero-or-more', MD_PARENT: 'parent' }
		const byInternalId = new Map()
		const shown = []
		for (const [key, e] of db.getEntities()) {
			byInternalId.set(e.id, key)
			const display = decode(e.alias || e.label || key)
			facts.push(tab('entity', key, display))
			shown.push(display)
			for (const a of e.attributes ?? []) {
				facts.push(tab('attribute', key, decode(a.type), decode(a.name), (a.keys ?? []).slice().sort().join(','), decode(a.comment)))
				shown.push(decode(a.type), decode(a.name), decode(a.comment))
			}
		}
		for (const r of db.getRelationships()) {
			const a = byInternalId.get(r.entityA) ?? r.entityA, b = byInternalId.get(r.entityB) ?? r.entityB
			const kind = r.relSpec.relType === 'IDENTIFYING' ? 'identifying' : 'non-identifying'
			facts.push(tab('relation', a, b, cardinality[r.relSpec.cardB] ?? r.relSpec.cardB, cardinality[r.relSpec.cardA] ?? r.relSpec.cardA, kind, decode(r.roleA)))
			shown.push(decode(r.roleA))
		}
		labels(shown)
	}

	// Model: title, slices and values, showData. Labels: title and slice names.
	function pie() {
		const shown = []
		title(shown)
		if (db.getShowData()) facts.push(tab('showdata', 'yes'))
		for (const [label, value] of Object.entries(Object.fromEntries(db.getSections()))) { facts.push(tab('slice', decode(label), num(value))); shown.push(decode(label)) }
		labels(shown)
	}

	// Model: title, axis and quadrant texts, points. mermaid's db keeps only the laid-out chart, so a point's value is its
	// position within the four quadrants, from 0 to 1 on each axis. Labels: every text of the model.
	function quadrant() {
		const q = db.getQuadrantData()
		const shown = []
		const add = (...f) => { facts.push(tab(...f)); shown.push(f.at(f[0] === 'point' ? 1 : -1)) }
		if (q.title) add('title', decode(q.title.text))
		for (const [i, quad] of q.quadrants.entries()) if (quad?.text?.text) add('quadrant', String(i + 1), decode(quad.text.text))
		const axes = ['x-left', 'x-right', 'y-bottom', 'y-top']
		for (const [i, a] of q.axisLabels.entries()) if (a.text) add('axis', axes[i] ?? String(i), decode(a.text))
		const left = Math.min(...q.quadrants.map(r => r.x)), top = Math.min(...q.quadrants.map(r => r.y))
		const width = Math.max(...q.quadrants.map(r => r.x + r.width)) - left, height = Math.max(...q.quadrants.map(r => r.y + r.height)) - top
		const round = v => String(Math.round(v * 100) / 100)
		for (const p of q.points) add('point', decode(p.text.text), round((p.x - left) / width), round(1 - (p.y - top) / height))
		labels(shown)
	}

	// Model: title, sections, periods and their events. Labels: all of them.
	function timeline() {
		const shown = []
		title(shown, db.getCommonDb?.().getDiagramTitle?.())
		for (const s of db.getSections()) { facts.push(tab('section', decode(s))); shown.push(decode(s)) }
		for (const t of db.getTasks()) {
			facts.push(tab('period', decode(t.section), decode(t.task)))
			shown.push(decode(t.task))
			for (const e of t.events ?? []) { facts.push(tab('event', decode(t.task), decode(e))); shown.push(decode(e)) }
		}
		labels(shown)
	}

	// Model: branches, and each commit in order with its branch, type, tags, merge source and id. An id mermaid generated
	// ("<seq>-<hash>") is left out, because it is random. Labels: branch names, tags and given commit ids.
	function gitGraph() {
		const shown = []
		for (const b of db.getBranchesAsObjArray()) { facts.push(tab('branch', b.name)); shown.push(b.name) }
		const commits = db.getCommitsArray()
		const byId = new Map(commits.map(c => [c.id, c]))
		const generated = id => /^\d+-[0-9a-f]{7}$/.test(id)
		const kind = { 0: 'normal', 1: 'reverse', 2: 'highlight', 3: 'merge', 4: 'cherry-pick' }
		for (const [i, c] of commits.entries()) {
			const from = c.type === 3 ? byId.get(c.parents[1])?.branch ?? '' : ''
			const id = generated(c.id) ? '' : c.id
			facts.push(tab('commit', String(i), c.branch, kind[c.type] ?? String(c.type), (c.tags ?? []).join(','), from, id))
			shown.push(id, ...(c.tags ?? []))
		}
		labels(shown)
	}

	// Model: title, axes, curves and their values. Labels: title, axis and curve names.
	function radar() {
		const shown = []
		title(shown)
		for (const a of db.getAxes()) { facts.push(tab('axis', decode(a.label))); shown.push(decode(a.label)) }
		for (const c of db.getCurves()) { facts.push(tab('curve', decode(c.label), c.entries.map(num).join(','))); shown.push(decode(c.label)) }
		labels(shown)
	}

	// Model: title and each rule with its expression: "terminal", <nonterminal>, ?special?, (a b) for a sequence, (a | b)
	// for a choice, [a] for an optional and {a}min..max for a repetition, with max left out when unbounded. Labels: rule
	// names, terminal values, nonterminal names and special texts.
	function railroad() {
		const shown = []
		const value = db.getDiagramTitle()
		if (value) facts.push(tab('title', decode(value)))
		const expr = n => {
			switch (n.type) {
				case 'terminal': shown.push(decode(n.value)); return '"' + decode(n.value) + '"'
				case 'nonterminal': shown.push(decode(n.name)); return '<' + decode(n.name) + '>'
				case 'special': shown.push(decode(n.text)); return '?' + decode(n.text) + '?'
				case 'sequence': return '(' + n.elements.map(expr).join(' ') + ')'
				case 'choice': return '(' + n.alternatives.map(expr).join(' | ') + ')'
				case 'optional': return '[' + expr(n.element) + ']'
				case 'repetition': return '{' + expr(n.element) + '}' + n.min + '..' + (n.max === Infinity ? '' : n.max)
				default: return '!' + n.type
			}
		}
		for (const r of db.getRules()) {
			facts.push(tab('rule', decode(r.name), expr(r.definition)))
			shown.push(decode(r.name))
		}
		labels(shown)
	}

	// Model: title, size, evolution stages and their boundaries, nodes with kind, position in percent, label offset,
	// inertia, sourcing and pipeline parent flag, links with dashes, flow and label, trends, pipelines and their members,
	// notes, annotations, the annotations position, accelerators and deaccelerators. Labels: title, stages, node and link
	// labels, notes, annotation texts and force names.
	function wardley() {
		const d = db.getWardleyData()
		const shown = []
		const yes = v => v ? 'yes' : ''
		title(shown)
		if (d.size) facts.push(tab('size', num(d.size.width), num(d.size.height)))
		for (const [i, s] of (d.axes.stages ?? []).entries()) { facts.push(tab('stage', String(i), s)); shown.push(s) }
		if (d.axes.stageBoundaries?.length) facts.push(tab('boundaries', d.axes.stageBoundaries.map(num).join(',')))
		for (const n of d.nodes) {
			facts.push(tab('node', n.id, n.label, n.className, num(n.x), num(n.y), num(n.labelOffsetX), num(n.labelOffsetY), yes(n.inertia), n.sourceStrategy ?? '', yes(n.isPipelineParent)))
			shown.push(n.label)
		}
		for (const l of d.links) {
			facts.push(tab('link', l.source, l.target, yes(l.dashed), l.flow ?? 'none', l.label ?? ''))
			shown.push(l.label)
		}
		for (const t of d.trends) facts.push(tab('trend', t.nodeId, num(t.targetX), num(t.targetY)))
		for (const p of d.pipelines) facts.push(tab('pipeline', p.nodeId, p.componentIds.join(',')))
		for (const n of d.notes) { facts.push(tab('note', n.text, num(n.x), num(n.y))); shown.push(n.text) }
		for (const a of d.annotations) {
			for (const c of a.coordinates) facts.push(tab('annotation', String(a.number), num(c.x), num(c.y), a.text ?? ''))
			shown.push(a.text)
		}
		if (d.annotationsBox) facts.push(tab('annotations', num(d.annotationsBox.x), num(d.annotationsBox.y)))
		for (const a of d.accelerators) { facts.push(tab('accelerator', a.name, num(a.x), num(a.y))); shown.push(a.name) }
		for (const a of d.deaccelerators) { facts.push(tab('deaccelerator', a.name, num(a.x), num(a.y))); shown.push(a.name) }
		labels(shown)
	}

	// DOM only: the plugin's db is empty, because @zenuml/core parses and draws in one call. Title; participants left to
	// right with their label and whether an icon is drawn; groups with their members; messages by kind (a closed arrow head
	// is a sync call) with the participants nearest their ends; fragments with their condition and each later section's
	// label; comments and dividers. Labels: all of those texts.
	function zenuml() {
		const shown = []
		const n = (el, a) => Number(el.getAttribute(a))
		const heading = text(dom.querySelector('.frame-title'))
		if (heading) { facts.push(tab('title', heading)); shown.push(heading) }
		const parts = [...dom.querySelectorAll('g.participant')].map(g => {
			const r = g.querySelector('rect.participant-box')
			return { id: g.dataset.participant, label: text(g.querySelector('.participant-label')), icon: !!g.querySelector('.participant-icon'), x: n(r, 'x') + n(r, 'width') / 2, top: n(r, 'y'), bottom: n(r, 'y') + n(r, 'height') }
		}).sort((a, b) => a.x - b.x)
		parts.forEach((p, i) => {
			facts.push(tab('participant', String(i), p.id, p.label))
			if (p.icon) facts.push(tab('icon', p.id))
			shown.push(p.label)
		})
		const near = x => parts.reduce((best, p) => Math.abs(p.x - x) < Math.abs(best.x - x) ? p : best).id
		for (const g of dom.querySelectorAll('g.participant-group')) {
			const r = g.querySelector('rect.group-outline')
			const name = text(g.querySelector('.group-title-text'))
			const members = parts.filter(p => p.x > n(r, 'x') && p.x < n(r, 'x') + n(r, 'width')).map(p => p.id)
			facts.push(tab('group', name, members.join(',')))
			shown.push(name)
		}
		for (const g of dom.querySelectorAll('g.message, g.creation, g.return')) {
			const head = g.querySelector('.arrow-head path, :scope svg svg path')?.getAttribute('d') ?? ''
			const kind = g.classList.contains('creation') ? 'create' : g.classList.contains('return') ? 'return' : /Z\s*$/.test(head) ? 'sync' : 'async'
			const labelEl = g.querySelector('.message-label, .return-label')
			const line = g.querySelector(':scope > line')
			// A creation ends at the edge of the new participant's box, drawn level with it.
			const created = kind === 'create' && parts.find(p => p.top <= n(line, 'y1') && n(line, 'y1') <= p.bottom)
			const [from, to] = line ? [near(n(line, 'x1')), created ? created.id : near(n(line, 'x2'))] : [near(n(labelEl, 'x')), near(n(labelEl, 'x'))]
			facts.push(tab('message', kind, from, to, text(labelEl)))
			shown.push(text(labelEl))
		}
		const unbracket = s => s.replace(/^\[(.*)\]$/, '$1')
		for (const g of dom.querySelectorAll('g.fragment')) {
			const raw = [...g.classList].find(c => c.startsWith('fragment-')).slice(9)
			const kind = { tcf: 'try' }[raw] ?? raw
			const condition = unbracket([...g.querySelectorAll('.fragment-condition')].filter(el => el.closest('g.fragment') === g).map(text).join(''))
			facts.push(tab('fragment', kind, condition))
			shown.push(condition)
			const sections = []
			for (const el of g.children) {
				if (el.matches('line.fragment-separator')) sections.push([])
				else if (sections.length) sections.at(-1).push(...[...(el.matches('.fragment-section-label') ? [el] : el.querySelectorAll('.fragment-section-label'))].map(text).filter(Boolean))
			}
			for (const parts of sections) {
				const label = unbracket(parts[0] === 'catch' && parts.length > 1 ? `catch(${parts.slice(1).join(' ')})` : parts.join(''))
				facts.push(tab('section', kind, label))
				shown.push(label)
			}
		}
		for (const el of dom.querySelectorAll('.comment-text')) { facts.push(tab('comment', text(el))); shown.push(text(el)) }
		for (const el of dom.querySelectorAll('.divider-label')) { facts.push(tab('divider', text(el))); shown.push(text(el)) }
		labels(shown)
	}

	// Model: each node by its path of names, with the value a leaf is given. Labels: node names.
	function treemap() {
		const shown = []
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.name)]
				facts.push(tab('node', p.join(' / '), c.children?.length ? '' : num(c.value)))
				shown.push(decode(c.name))
				walk(c, p)
			}
		}
		walk(db.getRoot(), [])
		labels(shown)
	}

	// Model: sets and their unions, with labels and sizes; a size the source leaves out is mermaid's default. Labels: set
	// and union labels.
	function venn() {
		const shown = []
		for (const s of db.getSubsetData()) {
			const sets = s.sets.slice().sort().join(',')
			facts.push(tab(s.sets.length === 1 ? 'set' : 'union', sets, decode(s.label ?? ''), num(s.size)))
			shown.push(decode(s.label ?? ''))
		}
		labels(shown)
	}

	// Model: each node by its path of labels, with its shape and its icon. A plain node is mermaid's default shape, a
	// rectangle with only its bottom edge drawn. Labels: node labels.
	function mindmap() {
		const shapeOf = { 0: 'default', 1: 'rounded', 2: 'rect', 3: 'circle', 4: 'cloud', 5: 'bang', 6: 'hex' }
		const shown = []
		const walk = (n, trail) => {
			const p = [...trail, decode(n.descr)]
			facts.push(tab('node', p.join(' / '), shapeOf[n.type] ?? String(n.type)))
			shown.push(decode(n.descr))
			if (n.icon) facts.push(tab('icon', p.join(' / '), iconName(n.icon)))
			for (const c of n.children ?? []) walk(c, p)
		}
		const root = db.getMindmap()
		if (root) walk(root, [])
		labels(shown)
	}

	// Model: sections, and each task with its section, explicit id, start, end and tags. A task's dependency (`after`) is
	// resolved into its start. Times are wall-clock times in the page's time zone, which facts.mjs sets to UTC. Labels:
	// title, sections and task names.
	function gantt() {
		const shown = []
		title(shown)
		for (const s of db.getSections()) { facts.push(tab('section', decode(s))); shown.push(decode(s)) }
		const pad = n => String(n).padStart(2, '0')
		const time = d => { d = new Date(d); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}` }
		for (const t of db.getTasks()) {
			const tags = ['done', 'active', 'crit', 'milestone'].filter(k => t[k]).join(',')
			facts.push(tab('task', decode(t.section), decode(t.task), /^task\d+$/.test(t.id) ? '' : t.id, time(t.startTime), time(t.endTime), tags))
			shown.push(decode(t.task))
		}
		labels(shown)
	}

	// Model: title, sections and tasks with their scores and people. Labels: title, sections, tasks and people.
	function journey() {
		const shown = []
		title(shown)
		for (const s of db.getSections()) { facts.push(tab('section', decode(s))); shown.push(decode(s)) }
		const people = new Set()
		for (const t of db.getTasks()) {
			facts.push(tab('task', decode(t.section), decode(t.task), num(t.score), (t.people ?? []).map(decode).sort().join(',')))
			shown.push(decode(t.task))
			for (const p of t.people ?? []) people.add(decode(p))
		}
		labels([...shown, ...people])
	}

	// Model: elements with type, external flag, label, technology, description and boundary; boundaries; relations.
	// mermaid's type names (system_db, external_container, ...) are split into the type and the external flag. Labels:
	// title, and the label, technology and description of every element, boundary and relation.
	function c4() {
		const shown = []
		title(shown, db.getTitle?.())
		for (const s of db.getC4ShapeArray()) {
			const t = s.typeC4Shape.text
			const external = t.startsWith('external_')
			facts.push(tab('element', s.alias, external ? t.slice('external_'.length) : t, external ? 'external' : 'internal', decode(s.label?.text), decode(s.techn?.text), decode(s.descr?.text), s.parentBoundary === 'global' ? '' : s.parentBoundary))
			shown.push(decode(s.label?.text), decode(s.techn?.text), decode(s.descr?.text))
		}
		for (const b of db.getBoundaries()) {
			if (b.alias === 'global') continue
			facts.push(tab('boundary', b.alias, b.type.text.toLowerCase(), decode(b.label?.text), b.parentBoundary === 'global' ? '' : b.parentBoundary))
			shown.push(decode(b.label?.text))
		}
		for (const r of db.getRels()) {
			facts.push(tab('rel', r.from, r.to, r.type === 'birel' ? 'both' : 'one', decode(r.label?.text), decode(r.techn?.text)))
			shown.push(decode(r.label?.text), decode(r.techn?.text))
		}
		labels(shown)
	}

	// Model: flows with their values. Labels: node names.
	function sankey() {
		const nodes = new Set()
		for (const l of db.getGraph().links) {
			facts.push(tab('flow', decode(l.source), decode(l.target), num(l.value)))
			nodes.add(decode(l.source)).add(decode(l.target))
		}
		labels([...nodes])
	}

	// Model: title, orientation, axis titles and categories, and each series' values. Labels: title, axis titles and
	// categories.
	function xychart() {
		const d = db.getXYChartData()
		const value = d.title || db.getDiagramTitle?.()
		if (value) facts.push(tab('title', decode(value)))
		if (db.getChartConfig?.()?.chartOrientation === 'horizontal') facts.push(tab('orientation', 'horizontal'))
		facts.push(tab('x-axis', decode(d.xAxis.title), (d.xAxis.categories ?? []).map(decode).join(',')))
		facts.push(tab('y-axis', decode(d.yAxis.title)))
		for (const p of d.plots) facts.push(tab('series', p.type, p.data.map(([, v]) => num(v)).join(',')))
		labels([decode(value), decode(d.xAxis.title), decode(d.yAxis.title), ...(d.xAxis.categories ?? []).map(decode)])
	}

	// Model: requirements with kind, id, text, risk and verification; elements; relations. Labels: names, ids, texts,
	// element types and document references, relation types.
	function requirement() {
		const lower = s => norm(s).toLowerCase().replace(/\s+/g, '-')
		const shown = []
		for (const r of Object.values(Object.fromEntries(db.getRequirements()))) {
			facts.push(tab('requirement', r.name, lower(r.type), decode(r.requirementId), decode(r.text), lower(r.risk), lower(r.verifyMethod)))
			shown.push(r.name, decode(r.requirementId), decode(r.text))
		}
		for (const e of Object.values(Object.fromEntries(db.getElements()))) {
			facts.push(tab('element', e.name, decode(e.type), decode(e.docRef)))
			shown.push(e.name, decode(e.type), decode(e.docRef))
		}
		for (const r of db.getRelationships()) { facts.push(tab('relation', r.src, r.dst, r.type)); shown.push(r.type) }
		labels(shown)
	}

	// Model: title, and fields by their bit ranges. mermaid cuts a field at the end of each row; the pieces are joined
	// back. Labels: title and field labels.
	function packet() {
		const shown = []
		title(shown)
		const fields = []
		for (const b of db.getPacket().flat()) {
			const last = fields.at(-1)
			if (last && last.label === b.label && last.end + 1 === b.start) last.end = b.end
			else fields.push({ start: b.start, end: b.end, label: b.label })
		}
		for (const f of fields) { facts.push(tab('field', String(f.start), String(f.end), decode(f.label))); shown.push(decode(f.label)) }
		labels(shown)
	}

	// Model: columns, and cards with their column and metadata. Labels: column and card labels, assignees and tickets;
	// priority is a colour in mermaid.js, not a text.
	function kanban() {
		const shown = []
		for (const n of db.getData().nodes) {
			if (n.isGroup) facts.push(tab('column', n.id, decode(n.label)))
			else facts.push(tab('card', n.parentId ?? '', n.id, decode(n.label), decode(n.assigned), decode(n.ticket), decode(n.priority)))
			shown.push(decode(n.label), decode(n.assigned), decode(n.ticket))
		}
		labels(shown)
	}

	// Model: groups, services and junctions with their parents, icons, and edges with their sides and arrowheads. Labels:
	// group and service titles.
	function architecture() {
		const shown = []
		for (const g of db.getGroups()) {
			facts.push(tab('group', g.id, decode(g.title), g.in ?? ''))
			shown.push(decode(g.title))
			if (g.icon) facts.push(tab('icon', g.id, g.icon))
		}
		for (const s of db.getServices()) {
			facts.push(tab('service', s.id, decode(s.title), s.in ?? ''))
			shown.push(decode(s.title))
			if (s.icon) facts.push(tab('icon', s.id, s.icon))
			if (s.iconText) facts.push(tab('icon-text', s.id, decode(s.iconText)))
		}
		for (const j of db.getJunctions()) facts.push(tab('junction', j.id, j.in ?? ''))
		for (const e of db.getEdges()) facts.push(tab('edge', e.lhsId, e.lhsDir, e.rhsId, e.rhsDir, e.lhsInto ? 'arrow' : 'none', e.rhsInto ? 'arrow' : 'none', e.lhsGroup || e.rhsGroup ? 'group' : '', decode(e.title)))
		labels(shown)
	}

	// Model: blocks with label, shape and column span, nested composites, spaces and edges. Labels: block and edge labels.
	function block() {
		const blockShape = { square: 'rect', na: 'rect', round: 'rounded', composite: 'composite' }
		const shown = []
		// A block without an id gets id-<random>-<counter>, the counter running across diagrams; name it by its order here.
		const anonymous = new Map()
		const id = raw => {
			if (!/^id-[a-z0-9]+-\d+$/.test(raw)) return raw
			if (!anonymous.has(raw)) anonymous.set(raw, `anonymous-${anonymous.size + 1}`)
			return anonymous.get(raw)
		}
		const walk = (n, parent) => {
			for (const c of n.children ?? []) {
				if (c.type === 'space') facts.push(tab('space', parent, num(c.width ?? c.widthInColumns ?? 1)))
				else {
					facts.push(tab('block', id(c.id), c.type === 'composite' ? '' : decode(c.label), blockShape[c.type] ?? shape(c.type), num(c.widthInColumns ?? 1), parent))
					if (c.type !== 'composite') shown.push(decode(c.label))
				}
				walk(c, id(c.id))
			}
		}
		walk(db.getBlocksFlat().find(b => b.id === 'root') ?? { children: db.getBlocks() }, '')
		for (const e of db.getEdges()) {
			facts.push(tab('edge', e.start, e.end, end(e.arrowTypeStart), end(e.arrowTypeEnd), decode(e.label)))
			shown.push(decode(e.label))
		}
		labels(shown)
	}

	// Model: direction, actors with type, icon, business flag, stereotype and boundary; use cases with shape, business
	// flag, stereotype and boundary; boundaries with type and members; relationships with type, ends, association label and
	// minimum length; notes; JSON tables and their rows. A Markdown label's markers are dropped, as both engines draw them
	// as formatting. Labels: every label, stereotype, JSON table id, key and value.
	function usecase() {
		const shown = []
		const plain = (label, type) => { const d = decode(label); return type === 'markdown' ? d.replace(/\*\*|\*|~~/g, '') : d }
		const yes = v => v ? 'yes' : ''
		const dir = db.getDirection()
		facts.push(tab('direction', dir === 'TB' ? 'TD' : dir))
		for (const a of db.getActors().values()) {
			facts.push(tab('actor', a.id, plain(a.label, a.labelType), a.type, yes(a.business), decode(a.stereotype), a.parentId ?? ''))
			if (a.icon) facts.push(tab('icon', a.id, a.icon))
			shown.push(plain(a.label, a.labelType), decode(a.stereotype))
		}
		for (const u of db.getUseCases().values()) {
			facts.push(tab('usecase', u.id, plain(u.label, u.labelType), u.shape, yes(u.business), decode(u.stereotype), u.parentId ?? ''))
			shown.push(plain(u.label, u.labelType), decode(u.stereotype))
		}
		for (const b of db.getSystemBoundaries().values()) {
			facts.push(tab('boundary', b.id, plain(b.label, b.labelType), b.type, b.members.slice().sort().join(',')))
			shown.push(plain(b.label, b.labelType))
		}
		const ends = {
			0: ['none', 'arrow'], 1: ['arrow', 'none'], 2: ['none', 'none'], 3: ['none', 'circle'], 4: ['none', 'cross'],
			5: ['circle', 'none'], 6: ['cross', 'none'],
		}
		for (const r of db.getRelationships()) {
			const [start, finish] = r.type === 'association' ? ends[r.arrowType] : r.type === 'generalization' ? ['none', 'triangle'] : ['none', 'arrow']
			const label = r.type === 'association' ? plain(r.label, r.labelType) : ''
			facts.push(tab('relation', r.source, r.target, r.type, start, finish, label, String(r.minlen)))
			shown.push(label)
		}
		for (const n of db.getNotes().values()) {
			facts.push(tab('note', n.target, plain(n.label, n.labelType)))
			shown.push(plain(n.label, n.labelType))
		}
		for (const n of db.getData().nodes) {
			if (n.shape !== 'usecaseJsonTable') continue
			facts.push(tab('json', n.id))
			shown.push(n.id)
			for (const [i, row] of (n.jsonRows ?? []).entries()) {
				facts.push(tab('row', n.id, String(i), decode(row.key), decode(row.value)))
				shown.push(decode(row.key), decode(row.value))
			}
		}
		labels(shown)
	}

	// Model: each node by its path of names, whether it is a directory, its description and its explicit icon. Labels:
	// names and descriptions.
	function treeView() {
		const shown = []
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.name)]
				facts.push(tab('node', p.join(' / '), c.nodeType === 'directory' ? 'dir' : 'file', decode(c.description)))
				shown.push(decode(c.name), decode(c.description))
				if (c.icon) facts.push(tab('icon', p.join(' / '), c.icon))
				walk(c, p)
			}
		}
		walk(db.getRoot(), [])
		labels(shown)
	}

	// Model, from the db's semantic model: direction, nodes with label, shape and kind (task, tool, action, input, refdoc,
	// decision), flows with their title and members, connectors, and edges with their meaning (sequence, reference,
	// failure) and label. Labels: node, flow, connector and edge labels.
	function agentflow() {
		db.getData()
		const m = db.getSemanticModel()
		const shown = []
		title(shown)
		if (m.direction) facts.push(tab('direction', m.direction === 'TB' ? 'TD' : m.direction))
		for (const v of m.vertices) { facts.push(tab('node', v.id, decode(v.label), shape(v.shape), v.vertexKind)); shown.push(decode(v.label)) }
		for (const f of m.subGraphs) { facts.push(tab('flow', f.id, decode(f.title), f.nodes.slice().sort().join(','))); shown.push(decode(f.title)) }
		for (const c of m.connectors) { facts.push(tab('connector', c.id, decode(c.title))); shown.push(decode(c.title)) }
		for (const e of m.edges) { facts.push(tab('edge', e.start, e.end, e.edgeSemantic ?? '', decode(e.label))); shown.push(decode(e.label)) }
		labels(shown)
	}

	// Model: title, the items of each domain in order, and transitions between domains with their labels. Labels: title,
	// items and transition labels.
	function cynefin() {
		const shown = []
		title(shown)
		for (const [domain, d] of db.getDomains()) {
			for (const [i, item] of d.items.entries()) { facts.push(tab('item', domain, String(i), decode(item.label))); shown.push(decode(item.label)) }
		}
		for (const t of db.getTransitions()) { facts.push(tab('transition', t.from, t.to, decode(t.label))); shown.push(decode(t.label)) }
		labels(shown)
	}

	// Model, from the db's state: title, swimlanes with their index and label, each time frame in order with its name,
	// entity type, entity name, swimlane and inline data, and the relations between frames. Labels: lane labels and entity
	// names.
	function eventmodeling() {
		const s = db.getState()
		const shown = []
		title(shown)
		for (const l of s.sortedSwimlanesArray) { facts.push(tab('lane', String(l.index), decode(l.label), l.namespace ?? '')); shown.push(decode(l.label)) }
		for (const b of s.boxes) {
			const f = b.frame
			facts.push(tab('frame', String(b.index), f.name, f.modelEntityType, f.entityIdentifier, String(b.swimlane.index), f.dataInlineValue ?? ''))
			shown.push(f.entityIdentifier)
		}
		for (const r of s.relations) facts.push(tab('relation', r.sourceBox.frame.name, r.targetBox.frame.name))
		labels(shown)
	}

	// Model: the version mermaid.js reports. The renderer draws it and nothing else. Labels: the version.
	function info() {
		labels([db.getVersion()])
	}

	// Model: the head, which is the diagram's title, and each cause by its path of texts below the head. Labels: the head
	// and every cause.
	function ishikawa() {
		const root = db.getRoot()
		const shown = []
		if (!root) return
		facts.push(tab('head', decode(root.text)))
		shown.push(decode(root.text))
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.text)]
				facts.push(tab('cause', p.join(' / ')))
				shown.push(decode(c.text))
				walk(c, p)
			}
		}
		walk(root, [])
		labels(shown)
	}
}
