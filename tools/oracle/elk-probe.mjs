// Runs the pinned elkjs on a graph and prints the positioned graph in the .elk.txt format. tools/oracle/README.md
// describes the command, and docs/layered-layout/clean-room.md why it runs elkjs only as a black box: it imports the
// entry mermaid's render.ts imports, calls layout and reads the graph it returns. It opens no file of elkjs.
import fs from 'node:fs'
import path from 'node:path'
import readline from 'node:readline'
import { fileURLToPath } from 'node:url'
import ELK from 'elkjs/lib/elk.bundled.js'
import { recordElkLayouts } from './dumps.mjs'
import { checkPinned } from './pinned.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))

// The root options mermaid 12.3.0 gives ELK for a flowchart at default config, in its key order, as every flowchart
// recording of the corpus has them. Only elk.direction changes with the flowchart's direction.
export const flowchartOptions = [
	['elk.hierarchyHandling', 'INCLUDE_CHILDREN'],
	['elk.algorithm', 'elk.layered'],
	['elk.layered.nodePlacement.strategy', 'BRANDES_KOEPF'],
	['elk.layered.nodePlacement.bk.fixedAlignment', 'BALANCED'],
	['elk.layered.mergeEdges', 'false'],
	['elk.direction', 'DOWN'],
	['spacing.baseValue', '40'],
	['elk.layered.crossingMinimization.forceNodeModelOrder', 'false'],
	['elk.layered.considerModelOrder.strategy', 'NODES_AND_EDGES'],
	['elk.layered.unnecessaryBendpoints', 'true'],
	['elk.layered.cycleBreaking.strategy', 'DEPTH_FIRST'],
	['elk.layered.layering.strategy', 'NETWORK_SIMPLEX'],
	['elk.layered.layering.coffmanGraham.layerBound', '4'],
	['elk.layered.wrapping.multiEdge.improveCuts', 'true'],
	['elk.layered.wrapping.multiEdge.improveWrappedEdges', 'true'],
	['elk.layered.edgeRouting.selfLoopDistribution', 'EQUALLY'],
	['elk.layered.mergeHierarchyEdges', 'true'],
	['elk.spacing.portsSurrounding', '[top=12,left=12,bottom=12,right=12]'],
]

// The options of the label mermaid gives every flowchart edge. An edge without text gets an empty label of size 0.
export const edgeLabelOptions = [
	['edgeLabels.inline', 'true'],
	['edgeLabels.placement', 'CENTER'],
]

// The flags that pin a stage, as the root options they set. ELK's INTERACTIVE strategies read the x and y the input
// gives each node: layering keeps the layers those coordinates put the nodes in, and crossing minimization keeps the
// order they give within each layer. --place-from-result gives a recorded graph ELK's own result as those coordinates.
export const stageFlags = {
	'--keep-cycles': ['elk.layered.cycleBreaking.strategy', 'INTERACTIVE'],
	'--keep-layers': ['elk.layered.layering.strategy', 'INTERACTIVE'],
	'--keep-order': ['elk.layered.crossingMinimization.strategy', 'INTERACTIVE'],
}

export const help = `Usage: node tools/oracle/elk-probe.mjs [options] <graph file>
       node tools/oracle/elk-probe.mjs --serve [options]

Runs the pinned elkjs on each layout of the graph file and prints the graph it was
given and the graph it returned in the .elk.txt format of tools/oracle/README.md,
with the layer and the position in the layer of every leaf node, read from ELK's
coordinates, as "# layer" lines after each result.

The graph file holds the input lines of an .elk.txt file: node, port, edge, label,
option and property. A recorded .elk.txt file works as it is; its result lines are
what --check compares with. A file without a layout line is one layout, and one
without a root node gets the node "root". When the root sets no elk.algorithm,
the probe gives the root, the edges and the edge labels the options mermaid.js
gives a flowchart at default config.

Stage pins, which hold a stage of ELK still so the others can be probed:
  --keep-layers           elk.layered.layering.strategy=INTERACTIVE: the layers
                          the input's node coordinates give are kept
  --keep-order            elk.layered.crossingMinimization.strategy=INTERACTIVE:
                          the order within each layer the input's node
                          coordinates give is kept
  --keep-cycles           elk.layered.cycleBreaking.strategy=INTERACTIVE: edges
                          that point against the input's coordinates are reversed
  --place-from-result     give each node of a recorded layout the x and y of its
                          recorded result, so the pins keep ELK's own layers and order

Other options:
  --option <key>=<value>  set a root layout option, after the file's and the pins'
  --layout <n>            run only layout n of the file
  --check                 fail unless each result equals the file's recorded result
  --serve                 read one graph file per line on stdin, as base64 of its
                          UTF-8, and answer each with the output lines and a line
                          holding a period. It prints "ready" first
  --help                  print this text`

// The recorder's escapes of an id, and their inverse.
const escape = id => id.replace(/\\/g, '\\\\').replace(/\t/g, '\\t').replace(/\n/g, '\\n').replace(/\r/g, '\\r')
const unescape = field => field.replace(/\\(.)/g, (_, c) => ({ t: '\t', n: '\n', r: '\r', s: ' ' })[c] ?? c)
const text = field => field === undefined || field === '' ? undefined : unescape(field)
const number = field => field === undefined || field === '' ? undefined : Number(field)
const idList = field => field === undefined || field === '' ? [] : field.split(' ').map(unescape)

// The layouts of a graph file: each one's input lines and recorded result lines.
export function readLayouts(source) {
	const layouts = []
	let current
	for (const line of source.split(/\r?\n/)) {
		if (!line || line.startsWith('#')) continue
		const kind = line.slice(0, line.indexOf('\t') >>> 0)
		if (kind === 'layout') {
			current = { number: Number(line.split('\t')[1]), input: [], recorded: undefined }
			layouts.push(current)
		} else if (kind === 'result') {
			if (!current) throw new Error('a result line comes before any input')
			current.recorded = []
		} else {
			if (!current) {
				current = { number: 1, input: [], recorded: undefined }
				layouts.push(current)
			}
			;(current.recorded ?? current.input).push(line)
		}
	}
	return layouts
}

const setOption = (element, field, key, value) => {
	element[field] ??= {}
	element[field][key] = value
}

// ELK's JSON graph of a layout's input lines.
export function buildGraph(lines) {
	const nodes = new Map()
	const ports = new Map()
	const edges = new Map()
	let root
	const labelOf = (kind, owner, index) => {
		const element = { node: nodes, port: ports, edge: edges }[kind]?.get(owner)
		if (!element) throw new Error(`a label of ${kind} '${owner}', which comes before it or does not exist`)
		const label = element.labels?.[index]
		if (!label) throw new Error(`label ${index} of ${kind} '${owner}' has no label line`)
		return label
	}
	for (const line of lines) {
		const f = line.split('\t')
		switch (f[0]) {
			case 'node': {
				const id = unescape(f[1])
				const parent = text(f[2])
				const node = { id }
				for (const [key, i] of [['width', 3], ['height', 4], ['x', 5], ['y', 6]])
					if (number(f[i]) !== undefined) node[key] = number(f[i])
				if (nodes.has(id)) throw new Error(`node '${id}' is given twice`)
				nodes.set(id, node)
				if (parent === undefined) {
					if (root) throw new Error(`nodes '${root.id}' and '${id}' are both roots`)
					root = node
				} else {
					const owner = nodes.get(parent)
					if (!owner) throw new Error(`node '${id}' comes before its parent '${parent}' or its parent does not exist`)
					;(owner.children ??= []).push(node)
				}
				break
			}
			case 'port': {
				const id = unescape(f[1])
				const owner = nodes.get(unescape(f[2]))
				if (!owner) throw new Error(`port '${id}' comes before its node '${unescape(f[2])}'`)
				const port = { id }
				for (const [key, i] of [['width', 3], ['height', 4], ['x', 5], ['y', 6]])
					if (number(f[i]) !== undefined) port[key] = number(f[i])
				ports.set(id, port)
				;(owner.ports ??= []).push(port)
				break
			}
			case 'edge': {
				const id = unescape(f[1])
				const owner = nodes.get(unescape(f[2]))
				if (!owner) throw new Error(`edge '${id}' comes before its container '${unescape(f[2])}'`)
				const edge = { id, sources: idList(f[3]), targets: idList(f[4]) }
				edges.set(id, edge)
				;(owner.edges ??= []).push(edge)
				break
			}
			case 'label': {
				const kind = f[1]
				const owner = { node: nodes, port: ports, edge: edges }[kind]?.get(unescape(f[2]))
				if (!owner) throw new Error(`label of ${kind} '${unescape(f[2])}' comes before its owner`)
				const label = { text: text(f[4]) ?? '' }
				for (const [key, i] of [['width', 5], ['height', 6], ['x', 7], ['y', 8]])
					if (number(f[i]) !== undefined) label[key] = number(f[i])
				;(owner.labels ??= [])[Number(f[3])] = label
				break
			}
			case 'option':
			case 'property': {
				const field = f[0] === 'option' ? 'layoutOptions' : 'properties'
				const [kind, owner, index, key, value] = [f[1], unescape(f[2]), f[3], unescape(f[4]), unescape(f[5] ?? '')]
				const element = kind.endsWith('-label')
					? labelOf(kind.slice(0, -'-label'.length), owner, Number(index))
					: { node: nodes, port: ports, edge: edges }[kind]?.get(owner)
				if (!element) throw new Error(`an option of ${kind} '${owner}', which comes before it or does not exist`)
				setOption(element, field, key, value)
				break
			}
			default:
				throw new Error(`unknown input line '${f[0]}'`)
		}
	}
	if (!root) throw new Error('the graph has no root node, a node line whose parent is empty')
	return { root, nodes, edges }
}

// The input lines of a file without a root node, with the node "root" before them.
const withRoot = lines => lines.some(l => /^node\t[^\t]*(\t\t|\t?$)/.test(l)) ? lines : ['node\troot', ...lines]

// Gives a graph whose root sets no elk.algorithm the root, edge and edge label options mermaid gives a flowchart. A root
// option the file sets replaces mermaid's. Every recording sets elk.algorithm, so a recorded graph is run as it is.
function applyFlowchartDefaults(graph) {
	const own = graph.root.layoutOptions ?? {}
	if ('elk.algorithm' in own) return false
	graph.root.layoutOptions = Object.fromEntries(flowchartOptions)
	Object.assign(graph.root.layoutOptions, own)
	for (const edge of graph.edges.values()) {
		edge.labels ??= [{ text: '', width: 0, height: 0 }]
		for (const label of edge.labels)
			if (!label.layoutOptions) for (const [key, value] of edgeLabelOptions) setOption(label, 'layoutOptions', key, value)
	}
	return true
}

// Each node's x and y from a recorded result's place lines, which are relative to the parent as the input's are.
function placeFromResult(graph, recorded) {
	if (!recorded) throw new Error('--place-from-result needs a recorded result')
	for (const line of recorded) {
		const f = line.split('\t')
		if (f[0] !== 'place') continue
		const node = graph.nodes.get(unescape(f[1]))
		if (node && node !== graph.root) {
			node.x = Number(f[3])
			node.y = Number(f[4])
		}
	}
}

// The leaves of each layer in order, read from the coordinates of ELK's result as ElkLayoutComparer.Layers in
// tests/SplashTheMusical.Conformance reads them: along the layout direction a layer is a maximal run of leaves whose
// extents overlap, and its order is the leaves' order across the direction by their centres.
export function layersOf(input, result) {
	const epsilon = 0.01
	const direction = input.root.layoutOptions?.['elk.direction'] ?? 'DOWN'
	const boxes = new Map()
	const walk = (node, ox, oy, isRoot) => {
		const x = ox + (node.x ?? 0)
		const y = oy + (node.y ?? 0)
		if (!isRoot) boxes.set(node.id, { x, y, width: node.width ?? 0, height: node.height ?? 0 })
		for (const child of node.children ?? []) walk(child, x, y, false)
	}
	walk(result, 0, 0, true)
	const along = b => ({
		UP: [-(b.y + b.height), -b.y],
		RIGHT: [b.x, b.x + b.width],
		LEFT: [-(b.x + b.width), -b.x],
	})[direction] ?? [b.y, b.y + b.height]
	const across = b => direction === 'RIGHT' || direction === 'LEFT' ? b.y + b.height / 2 : b.x + b.width / 2
	const leaves = [...input.nodes.values()]
		.filter(n => n !== input.root && !(n.children?.length) && boxes.has(n.id))
		.map(n => ({ id: n.id, along: along(boxes.get(n.id)) }))
		.sort((a, b) => a.along[0] - b.along[0] || a.along[1] - b.along[1])
	const layers = []
	let start = -Infinity
	let end = -Infinity
	for (const leaf of leaves) {
		if (!layers.length || (leaf.along[0] >= end - epsilon && Math.abs(leaf.along[0] - start) > epsilon)) {
			layers.push([])
			start = leaf.along[0]
			end = leaf.along[1]
		}
		layers.at(-1).push(leaf.id)
		end = Math.max(end, leaf.along[1])
	}
	return layers.map(layer => layer.sort((a, b) => across(boxes.get(a)) - across(boxes.get(b))))
}

// Reads the flags. The graph file is the one argument that is not a flag.
export function parseArguments(args) {
	const options = { overrides: [], pins: [], placeFromResult: false, layout: undefined, check: false, serve: false, help: false, file: undefined }
	for (let i = 0; i < args.length; i++) {
		const arg = args[i]
		if (arg in stageFlags) options.pins.push(stageFlags[arg])
		else if (arg === '--place-from-result') options.placeFromResult = true
		else if (arg === '--check') options.check = true
		else if (arg === '--serve') options.serve = true
		else if (arg === '--help' || arg === '-h') options.help = true
		else if (arg === '--layout') options.layout = Number(args[++i])
		else if (arg === '--option') {
			const value = args[++i] ?? ''
			const eq = value.indexOf('=')
			if (eq <= 0) throw new Error(`--option takes <key>=<value>, not '${value}'`)
			options.overrides.push([value.slice(0, eq), value.slice(eq + 1)])
		} else if (arg.startsWith('--')) throw new Error(`unknown option ${arg}`)
		else if (options.file === undefined) options.file = arg
		else throw new Error(`a second graph file, ${arg}`)
	}
	return options
}

const elkVersion = () => {
	const lock = JSON.parse(fs.readFileSync(path.join(here, 'package-lock.json'), 'utf8'))
	return lock.packages?.['node_modules/elkjs']?.version ?? 'unknown'
}

// The first line of an error's message, at most 300 characters. Node prints the line of code that threw an uncaught
// exception, so every exception of elkjs is caught and only its message is printed.
export const message = e => String(e?.message ?? e).split('\n')[0].slice(0, 300)

// ELK's result. elkjs can throw outside the promise layout returns, which would be an uncaught exception. The probe
// runs one layout at a time, so an uncaught exception while one runs is that layout's.
let running
function layoutSafely(elk, graph) {
	return new Promise((resolve, reject) => {
		running = e => {
			running = undefined
			reject(new Error(`elkjs threw: ${message(e)}`))
		}
		elk.layout(graph).then(result => {
			running = undefined
			resolve(result)
		}, running)
	})
}

for (const event of ['uncaughtException', 'unhandledRejection'])
	process.on(event, e => {
		if (running) return running(e)
		console.error(`elk-probe: ${message(e)}`)
		process.exit(2)
	})

// The first line where the probe's lines differ from the file's, or undefined when they are the same.
function firstDifference(part, actual, expected) {
	const at = actual.findIndex((line, i) => line !== expected[i])
	if (at < 0 && actual.length === expected.length) return undefined
	const i = at < 0 ? Math.min(actual.length, expected.length) : at
	return `line ${i + 1} of the ${part} is '${actual[i] ?? ''}', the file's is '${expected[i] ?? ''}'`
}

// The output lines of a graph file's source. `differs` counts the layouts whose result is not the recorded one, for
// --check.
export async function probe(source, options, name = 'stdin') {
	const elk = new ELK()
	const out = [
		'# ELK probe layouts. Refresh with: node tools/oracle/elk-probe.mjs',
		`# elkjs ${elkVersion()}`,
		`# graph ${name}`,
	]
	const flags = [
		...options.pins.map(([key, value]) => `${key}=${value}`),
		...options.overrides.map(([key, value]) => `${key}=${value}`),
		...(options.placeFromResult ? ['place-from-result'] : []),
	]
	if (flags.length) out.push(`# overrides ${flags.join(' ')}`)
	let differs = 0
	const layouts = readLayouts(source).filter(l => options.layout === undefined || l.number === options.layout)
	if (!layouts.length) throw new Error(options.layout === undefined ? 'the graph file has no layout' : `the graph file has no layout ${options.layout}`)
	for (const layout of layouts) {
		const lines = withRoot(layout.input)
		const graph = buildGraph(lines)
		const defaults = applyFlowchartDefaults(graph)
		if (options.placeFromResult) placeFromResult(graph, layout.recorded)
		// ELK refuses a child graph whose hierarchy-aware stage differs from the root's, so a pin is set on every node
		// with children.
		for (const node of graph.nodes.values())
			if (node === graph.root || node.children?.length)
				for (const [key, value] of options.pins) setOption(node, 'layoutOptions', key, value)
		for (const [key, value] of options.overrides) setOption(graph.root, 'layoutOptions', key, value)
		// The recorder numbers its calls from 1, so each layout gets one of its own that is renumbered to the file's.
		recordElkLayouts()
		globalThis.__oracleElk.before(graph.root)
		const input = buildGraph(lines)
		if (defaults) applyFlowchartDefaults(input)
		const result = await layoutSafely(elk, graph.root)
		globalThis.__oracleElk.after(result)
		const recorded = globalThis.__oracleElk.take()
		recorded[0] = `layout\t${layout.number}`
		if (defaults) out.push('# flowchart defaults')
		out.push(...recorded)
		const resultLines = recorded.slice(recorded.indexOf('result') + 1)
		layersOf(input, result).forEach((layer, l) => layer.forEach((id, p) => out.push(`# layer\t${escape(id)}\t${l}\t${p}`)))
		if (options.check) {
			if (!layout.recorded) throw new Error(`--check: layout ${layout.number} has no recorded result`)
			// The input is compared too when nothing changed it, which shows the graph ELK was given is the recorded one.
			const unchanged = !defaults && !options.placeFromResult && !options.pins.length && !options.overrides.length
			const difference = (unchanged ? firstDifference('input', recorded.slice(1, recorded.indexOf('result')), lines) : undefined)
				?? firstDifference('result', resultLines, layout.recorded)
			if (difference) {
				differs++
				out.push(`# check\tdiffers\t${difference}`)
			} else out.push('# check\tsame')
		}
	}
	return { lines: out, differs }
}

async function serve(options) {
	const write = text => new Promise(resolve => process.stdout.write(text, resolve))
	await write('ready\n')
	for await (const line of readline.createInterface({ input: process.stdin, crlfDelay: Infinity })) {
		if (!line) continue
		let answer
		try {
			answer = (await probe(Buffer.from(line, 'base64').toString('utf8'), options)).lines.join('\n') + '\n'
		} catch (e) {
			answer = `# error ${message(e)}\n`
		}
		await write(answer + '.\n')
	}
}

async function main() {
	let options
	try {
		options = parseArguments(process.argv.slice(2))
	} catch (e) {
		console.error(`elk-probe: ${e.message}\n\n${help}`)
		process.exit(2)
	}
	if (options.help) {
		console.log(help)
		return
	}
	const problems = checkPinned(here)
	if (problems.length) {
		console.error(`elk-probe: ${problems.join('; ')}. Run npm ci in tools/oracle.`)
		process.exit(2)
	}
	if (options.serve) return serve(options)
	if (options.file === undefined) {
		console.error(`elk-probe: no graph file\n\n${help}`)
		process.exit(2)
	}
	try {
		const { lines, differs } = await probe(fs.readFileSync(options.file, 'utf8'), options, options.file)
		process.stdout.write(lines.join('\n') + '\n')
		if (differs) process.exit(1)
	} catch (e) {
		console.error(`elk-probe: ${options.file}: ${message(e)}`)
		process.exit(2)
	}
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) await main()
