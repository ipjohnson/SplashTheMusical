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

	// Boxes are in the SVG's user units, the space of its viewBox. A box is the union of its elements' rendered client
	// rects mapped back through the SVG's screen transform, so nested transforms and nested SVGs count, and strokes do not.
	// An element with no size, such as an empty label, gets no box. A name used twice for one kind gets " #2" and on, in
	// the order the extractor meets them.
	const boxes = []
	const toUser = dom.getScreenCTM().inverse()
	const boxNames = new Map()
	function box(kind, name, ...els) {
		let x1 = Infinity, y1 = Infinity, x2 = -Infinity, y2 = -Infinity
		for (const el of els.flat()) {
			if (!el) continue
			const r = el.getBoundingClientRect()
			if (r.width === 0 && r.height === 0) continue
			for (const [x, y] of [[r.left, r.top], [r.right, r.bottom]]) {
				const p = new DOMPoint(x, y).matrixTransform(toUser)
				x1 = Math.min(x1, p.x); y1 = Math.min(y1, p.y); x2 = Math.max(x2, p.x); y2 = Math.max(y2, p.y)
			}
		}
		if (x1 === Infinity) return
		const key = kind + '\t' + norm(name)
		const n = (boxNames.get(key) ?? 0) + 1
		boxNames.set(key, n)
		boxes.push(tab('box', kind, n > 1 ? `${norm(name)} #${n}` : name, num(x1), num(y1), num(x2 - x1), num(y2 - y1)))
	}
	// The canvas is the viewBox. An SVG without one, such as info's, is drawn in CSS pixels from the origin.
	function canvas() {
		const vb = dom.viewBox.baseVal
		if (vb && (vb.width || vb.height)) return tab('canvas', num(vb.x), num(vb.y), num(vb.width), num(vb.height))
		const r = dom.getBoundingClientRect()
		return tab('canvas', '0', '0', num(r.width), num(r.height))
	}
	// Letters and digits, case-folded: how a model label is found in drawn text that is wrapped or upper-cased.
	const key = s => norm(s).toLowerCase().replace(/[^\p{L}\p{N}]/gu, '')
	// The first element of a selector whose drawn text is the label and that no earlier lookup took.
	const taken = new Set()
	function byText(selector, label, within = dom) {
		const want = key(label)
		if (!want) return null
		for (const el of within.querySelectorAll(selector)) {
			if (taken.has(el) || key(el.textContent) !== want) continue
			taken.add(el)
			return el
		}
		return null
	}
	const nth = (selector, i) => dom.querySelectorAll(selector)[i] ?? null
	// The cluster mermaid draws for a group: g.cluster with the group's data-id or with the domId-style id.
	const cluster = id => dom.querySelector(`.clusters [data-id="${esc(id)}"], .clusters [id="${esc(`${svgId}-${id}`)}"]`) ?? byDomId(id)
	const edgePath = id => dom.querySelector(`path[data-edge][data-id="${esc(id)}"]`)
	const edgeLabelEl = id => {
		const el = dom.querySelector(`.edgeLabel .label[data-id="${esc(id)}"]`) ?? dom.querySelector(`g.edgeLabel[id$="-${esc(id)}"]`)
		return text(el) ? el : null
	}
	// An edge and its label, named by their ends.
	function edgeBoxes(e, start = e.start, end = e.end, kind = 'edge') {
		box(kind, `${start} --> ${end}`, edgePath(e.id))
		box(kind + '-label', `${start} --> ${end}`, edgeLabelEl(e.id))
	}

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
	// The front matter title and the config of the front matter and the directives, from mermaid's preprocessDiagram
	// (hookPreprocess in dumps.mjs). Config is flattened into dotted paths with JSON values, as config.txt is.
	const preamble = globalThis.__oraclePreprocess(source)
	if (preamble.title !== undefined) facts.push(tab('preamble-title', preamble.title))
	const flatten = (prefix, v) => {
		if (v !== null && typeof v === 'object') {
			const keys = Object.keys(v)
			if (!keys.length && prefix) facts.push(tab('preamble-config', prefix, Array.isArray(v) ? '[]' : '{}'))
			for (const k of keys) flatten(prefix ? `${prefix}.${k}` : k, v[k])
		} else facts.push(tab('preamble-config', prefix, v === undefined ? 'undefined' : JSON.stringify(v)))
	}
	flatten('', preamble.config ?? {})
	boxes.push(canvas())
	return { facts, boxes, svg }

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

	// The title fact, and the box of the text that draws it.
	function title(shown, value = db.getDiagramTitle?.()) {
		if (!value) return
		facts.push(tab('title', decode(value)))
		shown.push(decode(value))
		box('title', decode(value), byText('text, foreignObject', decode(value)))
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
	// becomes an <i class="fa fa-name">). Boxes: nodes, groups, edges and edge labels.
	function flowchart() {
		const data = db.getData()
		const childrenOf = members(data)
		for (const n of data.nodes) {
			if (n.isGroup) {
				facts.push(tab('group', n.id, clusterLabel(n.id), childrenOf(n.id)))
				box('group', n.id, cluster(n.id))
				continue
			}
			// A collapsed subgraph is drawn under the subgraph's id, not a node domId.
			const el = byDomId(n.domId) ?? dom.querySelector(`[id="${svgId}-${n.id}"]`)
			box('node', n.id, el)
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
			edgeBoxes(e)
		}
	}

	// Model: ids, nesting, shapes, notes and transitions. DOM: state and transition labels. Boxes: states, composite
	// states, notes, transitions and their labels.
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
				box('note', target, byDomId(n.domId))
				continue
			}
			if (n.isGroup) {
				facts.push(tab('group', region(n.id), clusterLabel(n.id), [...(children.get(region(n.id)) ?? [])].sort().join(',')))
				box('group', region(n.id), dom.querySelector(`.clusters [data-id="${esc(n.id)}"]`) ?? byDomId(n.domId))
				continue
			}
			const id = name(n.id)
			box('node', id, byDomId(n.domId))
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
			edgeBoxes(e, name(e.start), name(e.end))
		}
	}

	// Model: classes, namespaces, notes, relation ends and cardinalities. DOM: class names, annotations, members and
	// relation labels. Boxes: classes, namespaces, notes by their text, relations and relation labels.
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
				box('namespace', n.id, cluster(n.id))
				continue
			}
			const el = byDomId(n.domId) ?? dom.querySelector(`.nodes [id="${esc(`${svgId}-${n.id}`)}"]`)
			if (n.shape === 'note') {
				box('note', text(el) || decode(n.label), el)
				continue
			}
			box('class', n.id, el)
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
			edgeBoxes(e, name(e.start), name(e.end), 'relation')
		}
	}

	// Model: participants and their types, boxes, messages, notes and blocks. The participant type is mermaid's own name
	// (participant, actor, boundary, control, entity, database, collections, queue). Boxes: each actor's top and bottom
	// box, boxes of actors, messages and their texts, notes and blocks, each by the db's message id in the DOM.
	function sequence() {
		for (const [id, a] of Object.entries(Object.fromEntries(db.getActors()))) {
			facts.push(tab('actor', id, decode(a.description), a.type ?? 'participant'))
			box('actor', id, dom.querySelector(`[data-et="participant"][data-id="${esc(id)}"]`))
			box('actor-bottom', id, dom.querySelector(`.actor-bottom[name="${esc(id)}"]`))
		}
		for (const b of db.getBoxes?.() ?? []) {
			facts.push(tab('box', decode(b.name), (b.actorKeys ?? []).slice().sort().join(',')))
			const label = byText('text.text', decode(b.name))
			box('box', decode(b.name), label?.parentElement.querySelector('rect.rect'))
		}
		// mermaid's sequence LINETYPE values.
		const message = {
			0: ['solid', 'filled'], 1: ['dashed', 'filled'], 3: ['solid', 'cross'], 4: ['dashed', 'cross'],
			5: ['solid', 'none'], 6: ['dashed', 'none'], 24: ['solid', 'open'], 25: ['dashed', 'open'],
			33: ['solid', 'filled-both'], 34: ['dashed', 'filled-both'],
		}
		const blockStart = { 10: 'loop', 12: 'alt', 15: 'opt', 19: 'par', 22: 'rect', 27: 'critical', 30: 'break', 32: 'par' }
		const divider = { 13: 'else', 20: 'and', 28: 'option' }
		const placement = { 0: 'left', 1: 'right', 2: 'over' }
		const blockEnd = new Set([11, 14, 16, 21, 23, 29, 31])
		const drawnAs = (et, id) => dom.querySelector(`[data-et="${et}"][data-id="i${esc(id)}"]`)
		// A block is drawn under the id of the message that ends it.
		const open = []
		for (const m of db.getMessages()) {
			if (blockEnd.has(m.type)) {
				const start = open.pop()
				if (start) box('block', `${blockStart[start.type]} ${start.type === 22 ? '' : decode(start.message)}`.trim(), drawnAs('control-structure', m.id))
			}
			if (m.type in message) {
				facts.push(tab('message', m.from, m.to, ...message[m.type], decode(m.message)))
				const line = drawnAs('message', m.id)
				box('message', `${m.from} --> ${m.to}`, line)
				const label = line?.previousElementSibling
				box('message-text', `${m.from} --> ${m.to}`, label?.matches('text.messageText') ? label : null)
			} else if (m.type === 2) {
				facts.push(tab('note', placement[m.placement] ?? m.placement, [m.from, m.to].filter((v, i, a) => v && a.indexOf(v) === i).join(','), decode(m.message)))
				box('note', decode(m.message), drawnAs('note', m.id))
			} else if (m.type in blockStart) {
				facts.push(tab('block', blockStart[m.type], m.type === 22 ? '' : decode(m.message)))
				open.push(m)
			} else if (m.type in divider) facts.push(tab('divider', decode(m.message)))
		}
	}

	// Model: entities with their display names, attributes and relationships. mermaid's relSpec names the cardinality at
	// entityB's end cardA and the one at entityA's end cardB; the fact lists them by the entity they sit at. Labels: entity
	// names, attribute types, names and comments, relationship labels. Boxes: entities, and relationships and their
	// labels in the order mermaid draws them, which is the db's order.
	function er() {
		const cardinality = { ONLY_ONE: 'one', ZERO_OR_ONE: 'zero-or-one', ONE_OR_MORE: 'one-or-more', ZERO_OR_MORE: 'zero-or-more', MD_PARENT: 'parent' }
		const byInternalId = new Map()
		const shown = []
		for (const [key, e] of db.getEntities()) {
			byInternalId.set(e.id, key)
			const display = decode(e.alias || e.label || key)
			box('entity', key, dom.querySelector(`.nodes [id="${esc(`${svgId}-${e.id}`)}"]`))
			facts.push(tab('entity', key, display))
			shown.push(display)
			for (const a of e.attributes ?? []) {
				facts.push(tab('attribute', key, decode(a.type), decode(a.name), (a.keys ?? []).slice().sort().join(','), decode(a.comment)))
				shown.push(decode(a.type), decode(a.name), decode(a.comment))
			}
		}
		const paths = dom.querySelectorAll('.edgePaths path[data-edge]')
		for (const [i, r] of db.getRelationships().entries()) {
			const a = byInternalId.get(r.entityA) ?? r.entityA, b = byInternalId.get(r.entityB) ?? r.entityB
			const kind = r.relSpec.relType === 'IDENTIFYING' ? 'identifying' : 'non-identifying'
			facts.push(tab('relation', a, b, cardinality[r.relSpec.cardB] ?? r.relSpec.cardB, cardinality[r.relSpec.cardA] ?? r.relSpec.cardA, kind, decode(r.roleA)))
			shown.push(decode(r.roleA))
			if (paths[i]) edgeBoxes({ id: paths[i].dataset.id }, a, b, 'relation')
		}
		labels(shown)
	}

	// Model: title, slices and values, showData. Labels: title and slice names. Boxes: the title, and each slice and its
	// legend entry. A legend entry draws the slice's label, and its swatch has the slice's fill.
	function pie() {
		const shown = []
		title(shown)
		if (db.getShowData()) facts.push(tab('showdata', 'yes'))
		const legends = [...dom.querySelectorAll('g.legend')]
		const slices = [...dom.querySelectorAll('path.pieCircle')]
		for (const [label, value] of Object.entries(Object.fromEntries(db.getSections()))) {
			facts.push(tab('slice', decode(label), num(value)))
			shown.push(decode(label))
			const legend = legends.find(g => !taken.has(g) && [decode(label), `${decode(label)} [${value}]`].includes(text(g)))
			if (!legend) continue
			taken.add(legend)
			box('legend', decode(label), legend)
			const fill = getComputedStyle(legend.querySelector('rect')).fill
			const slice = slices.find(p => !taken.has(p) && getComputedStyle(p).fill === fill)
			if (slice) taken.add(slice)
			box('slice', decode(label), slice)
		}
		labels(shown)
	}

	// Model: title, axis and quadrant texts, points. mermaid's db keeps only the laid-out chart, so a point's value is its
	// position within the four quadrants, from 0 to 1 on each axis. Labels: every text of the model. Boxes: the title,
	// the four quadrants in the db's order, axis labels and points.
	function quadrant() {
		const q = db.getQuadrantData()
		const shown = []
		const add = (...f) => { facts.push(tab(...f)); shown.push(f.at(f[0] === 'point' ? 1 : -1)) }
		if (q.title) {
			add('title', decode(q.title.text))
			box('title', decode(q.title.text), dom.querySelector('g.title text'))
		}
		for (const [i, quad] of q.quadrants.entries()) {
			if (quad?.text?.text) add('quadrant', String(i + 1), decode(quad.text.text))
			box('quadrant', String(i + 1), nth('g.quadrant', i))
		}
		const axes = ['x-left', 'x-right', 'y-bottom', 'y-top']
		for (const [i, a] of q.axisLabels.entries()) {
			if (!a.text) continue
			add('axis', axes[i] ?? String(i), decode(a.text))
			box('axis', axes[i] ?? String(i), byText('g.labels g.label text', decode(a.text)))
		}
		const left = Math.min(...q.quadrants.map(r => r.x)), top = Math.min(...q.quadrants.map(r => r.y))
		const width = Math.max(...q.quadrants.map(r => r.x + r.width)) - left, height = Math.max(...q.quadrants.map(r => r.y + r.height)) - top
		const round = v => String(Math.round(v * 100) / 100)
		for (const p of q.points) {
			add('point', decode(p.text.text), round((p.x - left) / width), round(1 - (p.y - top) / height))
			box('point', decode(p.text.text), byText('g.data-point', decode(p.text.text)))
		}
		labels(shown)
	}

	// Model: title, sections, periods and their events. Labels: all of them. Boxes: the title, and the node that draws
	// each section, period and event.
	function timeline() {
		const shown = []
		title(shown, db.getCommonDb?.().getDiagramTitle?.())
		for (const s of db.getSections()) {
			facts.push(tab('section', decode(s)))
			shown.push(decode(s))
			box('section', decode(s), byText('g.timeline-node', decode(s)))
		}
		for (const t of db.getTasks()) {
			facts.push(tab('period', decode(t.section), decode(t.task)))
			shown.push(decode(t.task))
			box('period', decode(t.task), byText('g.timeline-node', decode(t.task)))
			for (const e of t.events ?? []) {
				facts.push(tab('event', decode(t.task), decode(e)))
				shown.push(decode(e))
				box('event', `${decode(t.task)} / ${decode(e)}`, byText('g.timeline-node', decode(e)))
			}
		}
		labels(shown)
	}

	// Model: branches, and each commit in order with its branch, type, tags, merge source and id. An id mermaid generated
	// ("<seq>-<hash>") is left out, because it is random. Labels: branch names, tags and given commit ids. Boxes: each
	// branch's line and label, by its index in mermaid's classes, and each commit's bullet, label and tags, by its index.
	function gitGraph() {
		const shown = []
		for (const [i, b] of db.getBranchesAsObjArray().entries()) {
			facts.push(tab('branch', b.name))
			shown.push(b.name)
			box('branch', b.name, dom.querySelector(`line.branch.branch${i}`))
			box('branch-label', b.name, dom.querySelector(`rect.branchLabelBkg.label${i}`), dom.querySelector(`g.branch-label${i}`))
		}
		const commits = db.getCommitsArray()
		const byId = new Map(commits.map(c => [c.id, c]))
		const generated = id => /^\d+-[0-9a-f]{7}$/.test(id)
		const kind = { 0: 'normal', 1: 'reverse', 2: 'highlight', 3: 'merge', 4: 'cherry-pick' }
		// The db re-read after render() generates other random ids than the render did, so a commit's DOM id is the
		// class its bullet elements share, in the order mermaid draws the commits.
		const drawnIds = []
		for (const el of dom.querySelectorAll('.commit-bullets .commit')) {
			const id = [...el.classList].find(c => c !== 'commit' && !/^commit[-\d]/.test(c))
			if (id && !drawnIds.includes(id)) drawnIds.push(id)
		}
		for (const [i, c] of commits.entries()) {
			const from = c.type === 3 ? byId.get(c.parents[1])?.branch ?? '' : ''
			const id = generated(c.id) ? '' : c.id
			facts.push(tab('commit', String(i), c.branch, kind[c.type] ?? String(c.type), (c.tags ?? []).join(','), from, id))
			shown.push(id, ...(c.tags ?? []))
			const drawnId = drawnIds[i]
			box('commit', String(i), drawnId ? [...dom.querySelectorAll('.commit-bullets .commit')].filter(el => el.classList.contains(drawnId)) : [])
			box('commit-label', String(i), drawnId && byText('.commit-labels text.commit-label', drawnId)?.parentElement)
			for (const t of c.tags ?? []) {
				const label = byText('.commit-labels text.tag-label', t)
				box('tag', `${i} / ${t}`, label, label?.previousElementSibling?.previousElementSibling)
			}
		}
		labels(shown)
	}

	// Model: title, axes, curves and their values. Labels: title, axis and curve names. Boxes: the title, axis labels,
	// and each curve and its legend entry, by the curve's index in mermaid's classes.
	function radar() {
		const shown = []
		title(shown)
		for (const a of db.getAxes()) {
			facts.push(tab('axis', decode(a.label)))
			shown.push(decode(a.label))
			box('axis', decode(a.label), byText('text.radarAxisLabel', decode(a.label)))
		}
		for (const [i, c] of db.getCurves().entries()) {
			facts.push(tab('curve', decode(c.label), c.entries.map(num).join(',')))
			shown.push(decode(c.label))
			box('curve', decode(c.label), dom.querySelector(`path.radarCurve-${i}`))
			box('legend', decode(c.label), dom.querySelector(`rect.radarLegendBox-${i}`)?.parentElement)
		}
		labels(shown)
	}

	// Model: title and each rule with its expression: "terminal", <nonterminal>, ?special?, (a b) for a sequence, (a | b)
	// for a choice, [a] for an optional and {a}min..max for a repetition, with max left out when unbounded. Labels: rule
	// names, terminal values, nonterminal names and special texts. Boxes: the title, each rule in the db's order, and
	// each terminal, nonterminal and special inside its rule, named "<rule> / <text>".
	function railroad() {
		const shown = []
		title([])
		let rule = null, ruleName = ''
		const item = (kind, label) => box(kind, `${ruleName} / ${label}`, byText(`g.railroad-${kind}`, label, rule ?? dom))
		const expr = n => {
			switch (n.type) {
				case 'terminal': shown.push(decode(n.value)); item('terminal', decode(n.value)); return '"' + decode(n.value) + '"'
				case 'nonterminal': shown.push(decode(n.name)); item('nonterminal', decode(n.name)); return '<' + decode(n.name) + '>'
				case 'special': shown.push(decode(n.text)); item('special', decode(n.text)); return '?' + decode(n.text) + '?'
				case 'sequence': return '(' + n.elements.map(expr).join(' ') + ')'
				case 'choice': return '(' + n.alternatives.map(expr).join(' | ') + ')'
				case 'optional': return '[' + expr(n.element) + ']'
				case 'repetition': return '{' + expr(n.element) + '}' + n.min + '..' + (n.max === Infinity ? '' : n.max)
				default: return '!' + n.type
			}
		}
		for (const [i, r] of db.getRules().entries()) {
			rule = nth('g.railroad-rule', i)
			ruleName = decode(r.name)
			box('rule', ruleName, rule)
			facts.push(tab('rule', decode(r.name), expr(r.definition)))
			shown.push(decode(r.name))
		}
		labels(shown)
	}

	// Model: title, size, evolution stages and their boundaries, nodes with kind, position in percent, label offset,
	// inertia, sourcing and pipeline parent flag, links with dashes, flow and label, trends, pipelines and their members,
	// notes, annotations, the annotations position, accelerators and deaccelerators. Labels: title, stages, node and link
	// labels, notes, annotation texts and force names. Boxes: the title, stage labels, nodes, and links in the db's order.
	function wardley() {
		const d = db.getWardleyData()
		const shown = []
		const yes = v => v ? 'yes' : ''
		title(shown)
		if (d.size) facts.push(tab('size', num(d.size.width), num(d.size.height)))
		for (const [i, s] of (d.axes.stages ?? []).entries()) {
			facts.push(tab('stage', String(i), s))
			shown.push(s)
			box('stage', String(i), byText('text.wardley-stage-label', s))
		}
		if (d.axes.stageBoundaries?.length) facts.push(tab('boundaries', d.axes.stageBoundaries.map(num).join(',')))
		for (const n of d.nodes) {
			facts.push(tab('node', n.id, n.label, n.className, num(n.x), num(n.y), num(n.labelOffsetX), num(n.labelOffsetY), yes(n.inertia), n.sourceStrategy ?? '', yes(n.isPipelineParent)))
			shown.push(n.label)
			box('node', n.id, byText('g.wardley-node', n.label))
		}
		for (const [i, l] of d.links.entries()) {
			facts.push(tab('link', l.source, l.target, yes(l.dashed), l.flow ?? 'none', l.label ?? ''))
			shown.push(l.label)
			box('link', `${l.source} --> ${l.target}`, nth('.wardley-links .wardley-link', i))
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
	// label; comments and dividers. Labels: all of those texts. Boxes: the title, participants, groups, messages named by
	// their ends, and fragments named by kind and condition.
	function zenuml() {
		const shown = []
		const n = (el, a) => Number(el.getAttribute(a))
		const heading = text(dom.querySelector('.frame-title'))
		if (heading) {
			facts.push(tab('title', heading))
			shown.push(heading)
			box('title', heading, dom.querySelector('.frame-title'))
		}
		const parts = [...dom.querySelectorAll('g.participant')].map(g => {
			const r = g.querySelector('rect.participant-box')
			return { g, id: g.dataset.participant, label: text(g.querySelector('.participant-label')), icon: !!g.querySelector('.participant-icon'), x: n(r, 'x') + n(r, 'width') / 2, top: n(r, 'y'), bottom: n(r, 'y') + n(r, 'height') }
		}).sort((a, b) => a.x - b.x)
		parts.forEach((p, i) => {
			facts.push(tab('participant', String(i), p.id, p.label))
			if (p.icon) facts.push(tab('icon', p.id))
			shown.push(p.label)
			box('participant', p.id, p.g)
		})
		const near = x => parts.reduce((best, p) => Math.abs(p.x - x) < Math.abs(best.x - x) ? p : best).id
		for (const g of dom.querySelectorAll('g.participant-group')) {
			const r = g.querySelector('rect.group-outline')
			const name = text(g.querySelector('.group-title-text'))
			const members = parts.filter(p => p.x > n(r, 'x') && p.x < n(r, 'x') + n(r, 'width')).map(p => p.id)
			facts.push(tab('group', name, members.join(',')))
			shown.push(name)
			box('group', name, g)
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
			box('message', `${from} --> ${to}`, g)
		}
		const unbracket = s => s.replace(/^\[(.*)\]$/, '$1')
		for (const g of dom.querySelectorAll('g.fragment')) {
			const raw = [...g.classList].find(c => c.startsWith('fragment-')).slice(9)
			const kind = { tcf: 'try' }[raw] ?? raw
			const condition = unbracket([...g.querySelectorAll('.fragment-condition')].filter(el => el.closest('g.fragment') === g).map(text).join(''))
			facts.push(tab('fragment', kind, condition))
			shown.push(condition)
			box('fragment', `${kind} ${condition}`.trim(), g)
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

	// Model: each node by its path of names, with the value a leaf is given. Labels: node names. Boxes: each section and
	// leaf by its path.
	function treemap() {
		const shown = []
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.name)]
				facts.push(tab('node', p.join(' / '), c.children?.length ? '' : num(c.value)))
				shown.push(decode(c.name))
				const label = c.children?.length ? byText('g.treemapSection > text.treemapSectionLabel', decode(c.name)) : byText('g.treemapLeafGroup > text.treemapLabel', decode(c.name))
				box('node', p.join(' / '), label?.parentElement.querySelector('rect.treemapSection, rect.treemapLeaf'))
				walk(c, p)
			}
		}
		walk(db.getRoot(), [])
		labels(shown)
	}

	// Model: sets and their unions, with labels and sizes; a size the source leaves out is mermaid's default. Labels: set
	// and union labels. Boxes: each set and union's area, by its sets.
	function venn() {
		const shown = []
		const areas = [...dom.querySelectorAll('g.venn-area[data-venn-sets]')]
		for (const s of db.getSubsetData()) {
			const sets = s.sets.slice().sort().join(',')
			facts.push(tab(s.sets.length === 1 ? 'set' : 'union', sets, decode(s.label ?? ''), num(s.size)))
			shown.push(decode(s.label ?? ''))
			box(s.sets.length === 1 ? 'set' : 'union', sets, areas.find(g => g.dataset.vennSets.split('_').sort().join(',') === sets)?.querySelector('path'))
		}
		labels(shown)
	}

	// Model: each node by its path of labels, with its shape and its icon. A plain node is mermaid's default shape, a
	// rectangle with only its bottom edge drawn. Labels: node labels. Boxes: nodes by their path, and the edge to each
	// node from its parent.
	function mindmap() {
		const shapeOf = { 0: 'default', 1: 'rounded', 2: 'rect', 3: 'circle', 4: 'cloud', 5: 'bang', 6: 'hex' }
		const shown = []
		const walk = (n, trail, parent) => {
			const p = [...trail, decode(n.descr)]
			facts.push(tab('node', p.join(' / '), shapeOf[n.type] ?? String(n.type)))
			shown.push(decode(n.descr))
			box('node', p.join(' / '), dom.querySelector(`g.node[id="${esc(`${svgId}-node_${n.id}`)}"]`))
			if (parent) box('edge', p.join(' / '), edgePath(`edge_${parent.id}_${n.id}`))
			if (n.icon) facts.push(tab('icon', p.join(' / '), iconName(n.icon)))
			for (const c of n.children ?? []) walk(c, p, n)
		}
		const root = db.getMindmap()
		if (root) walk(root, [])
		labels(shown)
	}

	// Model: sections, and each task with its section, explicit id, start, end and tags. A task's dependency (`after`) is
	// resolved into its start. Times are wall-clock times in the page's time zone, which facts.mjs sets to UTC. Labels:
	// title, sections and task names. Boxes: the title, section titles, and each task's bar and text, named
	// "<section> / <task>".
	function gantt() {
		const shown = []
		title(shown)
		for (const s of db.getSections()) {
			facts.push(tab('section', decode(s)))
			shown.push(decode(s))
			box('section', decode(s), byText('text.sectionTitle', decode(s)))
		}
		const pad = n => String(n).padStart(2, '0')
		const time = d => { d = new Date(d); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}` }
		for (const t of db.getTasks()) {
			const tags = ['done', 'active', 'crit', 'milestone'].filter(k => t[k]).join(',')
			facts.push(tab('task', decode(t.section), decode(t.task), /^task\d+$/.test(t.id) ? '' : t.id, time(t.startTime), time(t.endTime), tags))
			shown.push(decode(t.task))
			box('task', `${decode(t.section)} / ${decode(t.task)}`, dom.querySelector(`rect[id="${esc(`${svgId}-${t.id}`)}"]`))
			box('task-text', `${decode(t.section)} / ${decode(t.task)}`, dom.querySelector(`text[id="${esc(`${svgId}-${t.id}-text`)}"]`))
		}
		labels(shown)
	}

	// Model: title, sections and tasks with their scores and people. Labels: title, sections, tasks and people. Boxes: the
	// title, each section's header, each task's card with its score face, named "<section> / <task>", and each person's
	// legend entry.
	function journey() {
		const shown = []
		title(shown)
		const headers = [...dom.querySelectorAll('rect.journey-section')].map(r => r.parentElement)
		for (const s of db.getSections()) {
			facts.push(tab('section', decode(s)))
			shown.push(decode(s))
			const header = headers.find(g => !taken.has(g) && key(g.querySelector('text')?.textContent) === key(decode(s)))
			if (header) taken.add(header)
			box('section', decode(s), header?.querySelector('rect.journey-section'))
		}
		const people = new Set()
		for (const [i, t] of db.getTasks().entries()) {
			facts.push(tab('task', decode(t.section), decode(t.task), num(t.score), (t.people ?? []).map(decode).sort().join(',')))
			shown.push(decode(t.task))
			for (const p of t.people ?? []) people.add(decode(p))
			const card = dom.querySelector(`line[id="${esc(`${svgId}-task${i}`)}"]`)?.parentElement
			box('task', `${decode(t.section)} / ${decode(t.task)}`, card?.querySelector('rect.task'))
			box('face', `${decode(t.section)} / ${decode(t.task)}`, card?.querySelector('circle.face'))
		}
		for (const p of [...people].sort()) box('person', p, byText('text.legend', p))
		labels([...shown, ...people])
	}

	// Model: elements with type, external flag, label, technology, description and boundary; boundaries; relations.
	// mermaid's type names (system_db, external_container, ...) are split into the type and the external flag. Labels:
	// title, and the label, technology and description of every element, boundary and relation. Boxes: the title,
	// elements, each boundary's frame, found by its label, and each relation's label, named by its ends.
	function c4() {
		const shown = []
		title(shown, db.getTitle?.())
		for (const s of db.getC4ShapeArray()) {
			const t = s.typeC4Shape.text
			const external = t.startsWith('external_')
			facts.push(tab('element', s.alias, external ? t.slice('external_'.length) : t, external ? 'external' : 'internal', decode(s.label?.text), decode(s.techn?.text), decode(s.descr?.text), s.parentBoundary === 'global' ? '' : s.parentBoundary))
			shown.push(decode(s.label?.text), decode(s.techn?.text), decode(s.descr?.text))
			box('element', s.alias, dom.querySelector(`[id="${esc(`${svgId}-${s.alias}`)}"]`))
		}
		for (const b of db.getBoundaries()) {
			if (b.alias === 'global') continue
			facts.push(tab('boundary', b.alias, b.type.text.toLowerCase(), decode(b.label?.text), b.parentBoundary === 'global' ? '' : b.parentBoundary))
			shown.push(decode(b.label?.text))
			box('boundary', b.alias, byText('svg > g > text', decode(b.label?.text))?.parentElement.querySelector(':scope > rect'))
		}
		for (const r of db.getRels()) {
			facts.push(tab('rel', r.from, r.to, r.type === 'birel' ? 'both' : 'one', decode(r.label?.text), decode(r.techn?.text)))
			shown.push(decode(r.label?.text), decode(r.techn?.text))
			box('rel-label', `${r.from} --> ${r.to}`, byText('svg > g > text', decode(r.label?.text)))
		}
		labels(shown)
	}

	// Model: flows with their values. Labels: node names. Boxes: nodes and flows, in the db's order, which is the order
	// mermaid draws them.
	function sankey() {
		const nodes = new Set()
		const graph = db.getGraph()
		for (const [i, l] of graph.links.entries()) {
			facts.push(tab('flow', decode(l.source), decode(l.target), num(l.value)))
			nodes.add(decode(l.source)).add(decode(l.target))
			box('flow', `${decode(l.source)} --> ${decode(l.target)}`, nth('g.links g.link path', i))
		}
		for (const [i, n] of graph.nodes.entries()) box('node', decode(n.id), nth('g.nodes g.node', i))
		labels([...nodes])
	}

	// Model: title, orientation, axis titles and categories, and each series' values. Labels: title, axis titles and
	// categories. Boxes: the title, the plot area, each series by its index, each axis and its title, and each category
	// label.
	function xychart() {
		const d = db.getXYChartData()
		const value = d.title || db.getDiagramTitle?.()
		if (value) {
			facts.push(tab('title', decode(value)))
			box('title', decode(value), dom.querySelector('.chart-title text'))
		}
		const horizontal = db.getChartConfig?.()?.chartOrientation === 'horizontal'
		if (horizontal) facts.push(tab('orientation', 'horizontal'))
		facts.push(tab('x-axis', decode(d.xAxis.title), (d.xAxis.categories ?? []).map(decode).join(',')))
		facts.push(tab('y-axis', decode(d.yAxis.title)))
		box('plot', 'plot', dom.querySelector('g.plot'))
		for (const [i, p] of d.plots.entries()) {
			facts.push(tab('series', p.type, p.data.map(([, v]) => num(v)).join(',')))
			box('series', String(i), dom.querySelector(`g.plot > g.${p.type}-plot-${i}`))
		}
		const [xSide, ySide] = horizontal ? ['left', 'bottom'] : ['bottom', 'left']
		box('axis', 'x', dom.querySelector(`g.${xSide}-axis`))
		box('axis', 'y', dom.querySelector(`g.${ySide}-axis`))
		box('axis-title', 'x', dom.querySelector(`g.${xSide}-axis g.title text`))
		box('axis-title', 'y', dom.querySelector(`g.${ySide}-axis g.title text`))
		for (const c of d.xAxis.categories ?? []) box('category', decode(c), byText(`g.${xSide}-axis g.label text`, decode(c)))
		labels([decode(value), decode(d.xAxis.title), decode(d.yAxis.title), ...(d.xAxis.categories ?? []).map(decode)])
	}

	// Model: requirements with kind, id, text, risk and verification; elements; relations. Labels: names, ids, texts,
	// element types and document references, relation types. Boxes: requirements, elements, and relations and their
	// labels. A relation's data-id is "<source>-<target>-<n>".
	function requirement() {
		const lower = s => norm(s).toLowerCase().replace(/\s+/g, '-')
		const shown = []
		const node = name => dom.querySelector(`.nodes [id="${esc(`${svgId}-${name}`)}"]`)
		for (const r of Object.values(Object.fromEntries(db.getRequirements()))) {
			facts.push(tab('requirement', r.name, lower(r.type), decode(r.requirementId), decode(r.text), lower(r.risk), lower(r.verifyMethod)))
			shown.push(r.name, decode(r.requirementId), decode(r.text))
			box('requirement', r.name, node(r.name))
		}
		for (const e of Object.values(Object.fromEntries(db.getElements()))) {
			facts.push(tab('element', e.name, decode(e.type), decode(e.docRef)))
			shown.push(e.name, decode(e.type), decode(e.docRef))
			box('element', e.name, node(e.name))
		}
		for (const r of db.getRelationships()) {
			facts.push(tab('relation', r.src, r.dst, r.type))
			shown.push(r.type)
			const stem = `${r.src}-${r.dst}-`
			const path = [...dom.querySelectorAll('path[data-edge][data-id]')].find(p => !taken.has(p) && p.dataset.id.startsWith(stem) && /^\d+$/.test(p.dataset.id.slice(stem.length)))
			if (path) taken.add(path)
			if (path) edgeBoxes({ id: path.dataset.id }, r.src, r.dst, 'relation')
		}
		labels(shown)
	}

	// Model: title, and fields by their bit ranges. mermaid cuts a field at the end of each row; the pieces are joined
	// back. Labels: title and field labels. Boxes: the title, and each piece of a field on a row, named by the piece's
	// first and last bit.
	function packet() {
		const shown = []
		title(shown)
		const fields = []
		for (const [i, b] of db.getPacket().flat().entries()) {
			box('field', `${b.start}-${b.end}`, nth('rect.packetBlock', i))
			const last = fields.at(-1)
			if (last && last.label === b.label && last.end + 1 === b.start) last.end = b.end
			else fields.push({ start: b.start, end: b.end, label: b.label })
		}
		for (const f of fields) { facts.push(tab('field', String(f.start), String(f.end), decode(f.label))); shown.push(decode(f.label)) }
		labels(shown)
	}

	// Model: columns, and cards with their column and metadata. Labels: column and card labels, assignees and tickets;
	// priority is a colour in mermaid.js, not a text. Boxes: columns and cards.
	function kanban() {
		const shown = []
		for (const n of db.getData().nodes) {
			if (n.isGroup) facts.push(tab('column', n.id, decode(n.label)))
			else facts.push(tab('card', n.parentId ?? '', n.id, decode(n.label), decode(n.assigned), decode(n.ticket), decode(n.priority)))
			shown.push(decode(n.label), decode(n.assigned), decode(n.ticket))
			box(n.isGroup ? 'column' : 'card', n.id, dom.querySelector(`[id="${esc(`${svgId}-${n.id}`)}"]`))
		}
		labels(shown)
	}

	// Model: groups, services and junctions with their parents, icons, and edges with their sides and arrowheads. Labels:
	// group and service titles. Boxes: groups, services, junctions, and edges named by their ends.
	function architecture() {
		const shown = []
		const byId = id => dom.querySelector(`[id="${esc(`${svgId}-${id}`)}"]`)
		for (const g of db.getGroups()) {
			facts.push(tab('group', g.id, decode(g.title), g.in ?? ''))
			shown.push(decode(g.title))
			if (g.icon) facts.push(tab('icon', g.id, g.icon))
			box('group', g.id, byId(`group-${g.id}`))
		}
		for (const s of db.getServices()) {
			facts.push(tab('service', s.id, decode(s.title), s.in ?? ''))
			shown.push(decode(s.title))
			if (s.icon) facts.push(tab('icon', s.id, s.icon))
			if (s.iconText) facts.push(tab('icon-text', s.id, decode(s.iconText)))
			box('service', s.id, byId(`service-${s.id}`))
		}
		for (const j of db.getJunctions()) {
			facts.push(tab('junction', j.id, j.in ?? ''))
			box('junction', j.id, byId(`node-${j.id}`))
		}
		for (const e of db.getEdges()) {
			facts.push(tab('edge', e.lhsId, e.lhsDir, e.rhsId, e.rhsDir, e.lhsInto ? 'arrow' : 'none', e.rhsInto ? 'arrow' : 'none', e.lhsGroup || e.rhsGroup ? 'group' : '', decode(e.title)))
			const stem = `${svgId}-L_${e.lhsId}_${e.rhsId}_`
			const path = [...dom.querySelectorAll('.architecture-edges path[id]')].find(p => !taken.has(p) && p.id.startsWith(stem) && /^\d+$/.test(p.id.slice(stem.length)))
			if (path) taken.add(path)
			box('edge', `${e.lhsId} --> ${e.rhsId}`, path)
		}
		labels(shown)
	}

	// Model: blocks with label, shape and column span, nested composites, spaces and edges. Labels: block and edge labels.
	// Boxes: blocks, and edges and their labels. An edge's data-id ends in "-<start>-<end>".
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
					box('block', id(c.id), dom.querySelector(`g.node[id="${esc(`${svgId}-${c.id}`)}"]`))
				}
				walk(c, id(c.id))
			}
		}
		walk(db.getBlocksFlat().find(b => b.id === 'root') ?? { children: db.getBlocks() }, '')
		for (const e of db.getEdges()) {
			facts.push(tab('edge', e.start, e.end, end(e.arrowTypeStart), end(e.arrowTypeEnd), decode(e.label)))
			shown.push(decode(e.label))
			const tail = `-${e.start}-${e.end}`
			const path = [...dom.querySelectorAll('path[data-edge][data-id]')].find(p => !taken.has(p) && p.dataset.id.endsWith(tail))
			const label = [...dom.querySelectorAll('.edgeLabel .label[data-id]')].find(l => !taken.has(l) && l.dataset.id.endsWith(tail) && text(l))
			if (path) taken.add(path)
			if (label) taken.add(label)
			box('edge', `${e.start} --> ${e.end}`, path)
			box('edge-label', `${e.start} --> ${e.end}`, label)
		}
		labels(shown)
	}

	// Model: direction, actors with type, icon, business flag, stereotype and boundary; use cases with shape, business
	// flag, stereotype and boundary; boundaries with type and members; relationships with type, ends, association label and
	// minimum length; notes; JSON tables and their rows. A Markdown label's markers are dropped, as both engines draw them
	// as formatting. Labels: every label, stereotype, JSON table id, key and value. Boxes: actors, use cases, boundaries,
	// notes and JSON tables by their ids, and relations and their labels, named by their ends.
	function usecase() {
		const shown = []
		const plain = (label, type) => { const d = decode(label); return type === 'markdown' ? d.replace(/\*\*|\*|~~/g, '') : d }
		const yes = v => v ? 'yes' : ''
		const node = id => dom.querySelector(`.nodes [data-usecase-id="${esc(id)}"]`) ?? cluster(id) ?? dom.querySelector(`.nodes [id="${esc(`${svgId}-usecase-${id}`)}"]`)
		const dir = db.getDirection()
		facts.push(tab('direction', dir === 'TB' ? 'TD' : dir))
		for (const a of db.getActors().values()) {
			facts.push(tab('actor', a.id, plain(a.label, a.labelType), a.type, yes(a.business), decode(a.stereotype), a.parentId ?? ''))
			if (a.icon) facts.push(tab('icon', a.id, a.icon))
			shown.push(plain(a.label, a.labelType), decode(a.stereotype))
			box('actor', a.id, node(a.id))
		}
		for (const u of db.getUseCases().values()) {
			facts.push(tab('usecase', u.id, plain(u.label, u.labelType), u.shape, yes(u.business), decode(u.stereotype), u.parentId ?? ''))
			shown.push(plain(u.label, u.labelType), decode(u.stereotype))
			box('usecase', u.id, node(u.id))
		}
		for (const b of db.getSystemBoundaries().values()) {
			facts.push(tab('boundary', b.id, plain(b.label, b.labelType), b.type, b.members.slice().sort().join(',')))
			shown.push(plain(b.label, b.labelType))
			box('boundary', b.id, node(b.id))
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
			edgeBoxes(r, r.source, r.target, 'relation')
		}
		for (const n of db.getNotes().values()) {
			facts.push(tab('note', n.target, plain(n.label, n.labelType)))
			shown.push(plain(n.label, n.labelType))
			box('note', n.target, node(n.id))
		}
		for (const n of db.getData().nodes) {
			if (n.shape !== 'usecaseJsonTable') continue
			facts.push(tab('json', n.id))
			shown.push(n.id)
			box('json', n.id, node(n.id))
			for (const [i, row] of (n.jsonRows ?? []).entries()) {
				facts.push(tab('row', n.id, String(i), decode(row.key), decode(row.value)))
				shown.push(decode(row.key), decode(row.value))
			}
		}
		labels(shown)
	}

	// Model: each node by its path of names, whether it is a directory, its description and its explicit icon. Labels:
	// names and descriptions. Boxes: each node's label by its path, found in the order mermaid draws them.
	function treeView() {
		const shown = []
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.name)]
				facts.push(tab('node', p.join(' / '), c.nodeType === 'directory' ? 'dir' : 'file', decode(c.description)))
				shown.push(decode(c.name), decode(c.description))
				box('node', p.join(' / '), byText('g.tree-view > g', decode(c.name) + (c.description ? ' ' + decode(c.description) : '')) ?? byText('g.tree-view > g', decode(c.name)))
				if (c.icon) facts.push(tab('icon', p.join(' / '), c.icon))
				walk(c, p)
			}
		}
		walk(db.getRoot(), [])
		labels(shown)
	}

	// Model, from the db's semantic model: direction, nodes with label, shape and kind (task, tool, action, input, refdoc,
	// decision), flows with their title and members, connectors, and edges with their meaning (sequence, reference,
	// failure) and label. Labels: node, flow, connector and edge labels. Boxes: nodes and flows, and edges and their
	// labels, from the db's render data, whose edge ids are L_<start>_<end>_<n>.
	function agentflow() {
		const data = db.getData()
		const m = db.getSemanticModel()
		const shown = []
		title(shown)
		const rendered = new Map(data.nodes.map(n => [n.id, n]))
		const el = id => byDomId(rendered.get(id)?.domId) ?? cluster(id)
		if (m.direction) facts.push(tab('direction', m.direction === 'TB' ? 'TD' : m.direction))
		for (const v of m.vertices) { facts.push(tab('node', v.id, decode(v.label), shape(v.shape), v.vertexKind)); shown.push(decode(v.label)); box('node', v.id, el(v.id)) }
		for (const f of m.subGraphs) { facts.push(tab('flow', f.id, decode(f.title), f.nodes.slice().sort().join(','))); shown.push(decode(f.title)); box('flow', f.id, el(f.id)) }
		for (const c of m.connectors) { facts.push(tab('connector', c.id, decode(c.title))); shown.push(decode(c.title)) }
		for (const e of m.edges) { facts.push(tab('edge', e.start, e.end, e.edgeSemantic ?? '', decode(e.label))); shown.push(decode(e.label)) }
		for (const e of data.edges) edgeBoxes(e)
		labels(shown)
	}

	// Model: title, the items of each domain in order, and transitions between domains with their labels. Labels: title,
	// items and transition labels. Boxes: the title, domain labels, items named "<domain> / <index>", and transitions
	// and their labels in the db's order, named by their ends.
	function cynefin() {
		const shown = []
		title(shown)
		for (const [domain, d] of db.getDomains()) {
			box('domain-label', domain, byText('text.cynefinDomainLabel', domain))
			for (const [i, item] of d.items.entries()) {
				facts.push(tab('item', domain, String(i), decode(item.label)))
				shown.push(decode(item.label))
				box('item', `${domain} / ${i}`, byText('.cynefin-items > g', decode(item.label)))
			}
		}
		for (const [i, t] of db.getTransitions().entries()) {
			facts.push(tab('transition', t.from, t.to, decode(t.label)))
			shown.push(decode(t.label))
			box('transition', `${t.from} --> ${t.to}`, nth('path.cynefinArrowLine', i))
			box('transition-label', `${t.from} --> ${t.to}`, byText('text.cynefinArrowLabel', decode(t.label)))
		}
		labels(shown)
	}

	// Model, from the db's state: title, swimlanes with their index and label, each time frame in order with its name,
	// entity type, entity name, swimlane and inline data, and the relations between frames. Labels: lane labels and entity
	// names. Boxes: the title, lanes, frames and relations, each in the db's order, which is the order mermaid draws them.
	function eventmodeling() {
		const s = db.getState()
		const shown = []
		title(shown)
		for (const [i, l] of s.sortedSwimlanesArray.entries()) {
			facts.push(tab('lane', String(l.index), decode(l.label), l.namespace ?? ''))
			shown.push(decode(l.label))
			box('lane', String(l.index), nth('g.em-swimlane', i))
		}
		for (const [i, b] of s.boxes.entries()) {
			const f = b.frame
			facts.push(tab('frame', String(b.index), f.name, f.modelEntityType, f.entityIdentifier, String(b.swimlane.index), f.dataInlineValue ?? ''))
			shown.push(f.entityIdentifier)
			box('frame', String(b.index), nth('g.em-box', i))
		}
		for (const [i, r] of s.relations.entries()) {
			facts.push(tab('relation', r.sourceBox.frame.name, r.targetBox.frame.name))
			box('relation', `${r.sourceBox.frame.name} --> ${r.targetBox.frame.name}`, nth('path.em-relation', i))
		}
		labels(shown)
	}

	// Model: the version mermaid.js reports. The renderer draws it and nothing else. Labels: the version. Boxes: the
	// version.
	function info() {
		labels([db.getVersion()])
		box('version', db.getVersion(), dom.querySelector('text.version'))
	}

	// Model: the head, which is the diagram's title, and each cause by its path of texts below the head. Labels: the head
	// and every cause. Boxes: the head, and each cause's label by its path; a top-level cause's label includes its box.
	function ishikawa() {
		const root = db.getRoot()
		const shown = []
		if (!root) return
		facts.push(tab('head', decode(root.text)))
		shown.push(decode(root.text))
		box('head', decode(root.text), dom.querySelector('g.ishikawa-head-group'))
		const walk = (n, trail) => {
			for (const c of n.children ?? []) {
				const p = [...trail, decode(c.text)]
				facts.push(tab('cause', p.join(' / ')))
				shown.push(decode(c.text))
				const label = byText('text.ishikawa-label', decode(c.text))
				box('cause', p.join(' / '), label?.parentElement.matches('g.ishikawa-label-group') ? label.parentElement : label)
				walk(c, p)
			}
		}
		walk(root, [])
		labels(shown)
	}
}
