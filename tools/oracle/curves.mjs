// Runs d3-shape's line() and mermaid's edge code on fixed point lists, and writes the path strings and the edge markers
// they give. tools/oracle/README.md describes the two files.
//
//   node curves.mjs <curves-file> <markers-file>
//
// The d3 curves come from d3-shape. The rounded curve, the marker offsets, the marker ids and the marker definitions come
// from mermaid's exported insertEdge and insertMarkers, called with a selection that records what they write. The run
// fails when node_modules does not match package-lock.json.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'
import * as d3 from 'd3-shape'
import { checkPinned } from './pinned.mjs'

const [, , curvesFile, markersFile] = process.argv
if (!curvesFile || !markersFile) {
	console.error('usage: node curves.mjs <curves-file> <markers-file>')
	process.exit(2)
}

const root = path.dirname(fileURLToPath(import.meta.url))
const problems = checkPinned(root)
const lock = JSON.parse(fs.readFileSync(path.join(root, 'package-lock.json'), 'utf8'))
const installed = name => JSON.parse(fs.readFileSync(path.join(root, 'node_modules', name, 'package.json'), 'utf8')).version
for (const name of ['d3-shape', 'd3-path']) {
	const pinned = lock.packages[`node_modules/${name}`]?.version
	if (installed(name) !== pinned) problems.push(`node_modules has ${name} ${installed(name)}, package-lock.json pins ${pinned}`)
}
if (problems.length) {
	for (const p of problems) console.error(p)
	console.error('run npm ci in tools/oracle with the Node mise.toml pins')
	process.exit(1)
}
const mermaidDir = path.join(root, 'node_modules', 'mermaid')
const version = installed('mermaid')

// The mermaid.core chunk whose source holds `marker`. These chunks import d3 by name, so Node can load them.
const chunks = path.join(mermaidDir, 'dist', 'chunks', 'mermaid.core')
function chunk(marker) {
	const found = fs.readdirSync(chunks).filter(f => f.endsWith('.mjs') && fs.readFileSync(path.join(chunks, f), 'utf8').includes(marker))
	if (found.length !== 1) throw new Error(`${found.length} mermaid.core chunks hold ${marker}; the bundle changed`)
	return path.join(chunks, found[0])
}
const edgesFile = chunk('// src/rendering-util/rendering-elements/markers.js')
const edges = await import(pathToFileURL(edgesFile))
const config = await import(pathToFileURL(chunk('var setSiteConfig =')))
const edgesCode = fs.readFileSync(edgesFile, 'utf8')

// The keys of the object literal `var <name> = {` in the edge chunk.
function keys(name) {
	const start = edgesCode.indexOf(`var ${name} = {`)
	if (start < 0) throw new Error(`${name} not found in the edge chunk`)
	const body = edgesCode.slice(start, edgesCode.indexOf('\n};', start))
	return [...body.matchAll(/^ {2}(\w+)\b/gm)].map(m => m[1])
}

// The curve values of the config schema, from the config types mermaid generates from it.
const types = fs.readFileSync(path.join(mermaidDir, 'dist', 'config.type.d.ts'), 'utf8')
const curveTypes = [...types.matchAll(/^\s*curve\?: ((?:'\w+'(?: \| )?)+);$/gm)]
if (curveTypes.length !== 1) throw new Error(`config.type.d.ts has ${curveTypes.length} curve types; expected 1`)
const curveNames = [...curveTypes[0][1].matchAll(/'(\w+)'/g)].map(m => m[1])

// A selection that records the elements, attributes and styles written through it.
function element(tag) {
	const node = { tag, attrs: [], styles: [], children: [] }
	const all = n => [n, ...n.children.flatMap(all)]
	const selection = {
		node: () => ({}),
		append: t => {
			const child = element(t)
			node.children.push(child.node)
			return child.selection
		},
		attr: (name, value) => {
			if (value === undefined) return node.attrs.findLast(a => a[0] === name)?.[1]
			node.attrs.push([name, String(value)])
			return selection
		},
		style: (name, value) => {
			node.styles.push([name, String(value)])
			return selection
		},
		selectAll: () => ({
			attr: (name, value) => {
				for (const n of node.children.flatMap(all)) n.attrs.push([name, String(value)])
			},
		}),
	}
	return { node, selection }
}

const escape = text => text.replace(/\\/g, '\\\\').replace(/\t/g, '\\t').replace(/\n/g, '\\n')
const pointsText = points => points.map(p => `${p[0]},${p[1]}`).join(' ')
// String(), except that negative zero keeps its sign.
const signedText = value => Object.is(value, -0) ? '-0' : String(value)

// mulberry32, so the random points are the same on every run.
let seed = 0xc0ffee
const random = () => {
	seed = (seed + 0x6d2b79f5) | 0
	let t = Math.imul(seed ^ (seed >>> 15), 1 | seed)
	t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t
	return ((t ^ (t >>> 14)) >>> 0) / 4294967296
}
const coordinate = () => Math.round((random() * 1000 - 500) * 1e6) / 1e6
const randomPoints = count => Array.from({ length: count }, () => [coordinate(), coordinate()])

const curveLists = [
	[],
	[[5, 5]],
	[[0, 0], [100, 0]],
	[[0, 0], [0, 100]],
	[[0, 0], [50, 80], [100, 0]],
	[[10.5, 20.25], [40.125, -30.0625], [90.3333333, 60.6666667], [120, 10]],
	[[0, 0], [0, 50], [100, 50], [100, 100]],
	[[0, 0], [0, 0], [10, 10], [10, 10], [20, 0]],
	[[100, 0], [50, 10], [0, 20], [-50, 30]],
	[[0, 0], [10, 0], [10, 0], [10, 20], [30, 20], [30, -5], [60, -5]],
	[[0.0004, -0.0004], [0.0005, -0.0005], [1.0015, 2.0025], [3.4995, -1.5], [-2.5005, 0.4995]],
	[[1e21, 1e-7], [2e21, 3], [5e20, -1e21]],
	[[1e15, 0], [123456789012.345, 1], [-9007199254740993, 0.1]],
	...[2, 3, 4, 5, 6, 7, 9, 12].map(randomPoints),
]

const d3Curves = {
	basis: d3.curveBasis,
	bumpX: d3.curveBumpX,
	bumpY: d3.curveBumpY,
	cardinal: d3.curveCardinal,
	catmullRom: d3.curveCatmullRom,
	linear: d3.curveLinear,
	monotoneX: d3.curveMonotoneX,
	monotoneY: d3.curveMonotoneY,
	natural: d3.curveNatural,
	step: d3.curveStep,
	stepAfter: d3.curveStepAfter,
	stepBefore: d3.curveStepBefore,
}

// The path insertEdge draws for an edge through `points` with no node intersection or cluster cut.
function edgePath(points, curve, arrowTypeStart, arrowTypeEnd, look) {
	const { node, selection } = element('g')
	const edge = { id: 'e', points: points.map(([x, y]) => ({ x, y })), curve, arrowTypeStart, arrowTypeEnd, look }
	edges.insertEdge(selection, edge, new Map(), 'TYPE', {}, {}, 'ID', true)
	const attrs = Object.fromEntries(node.children[0].attrs)
	return { d: attrs.d ?? '', start: attrs['marker-start'] ?? '', end: attrs['marker-end'] ?? '' }
}

const curveLines = []
for (const name of curveNames) curveLines.push(`curve-name\t${name}`)
for (const name of curveNames) {
	for (const points of curveLists) {
		let d
		if (name in d3Curves) d = d3.line().curve(d3Curves[name])(points) ?? ''
		else if (name === 'rounded') d = points.length < 2 ? '' : edgePath(points, name).d
		else throw new Error(`the config schema names a curve curves.mjs does not know: ${name}`)
		curveLines.push(`curve\t${name}\t${pointsText(points)}\t${d}`)
	}
}

const edgeLists = [
	[[50, 30], [50, 80], [50, 130]],
	[[50, 30], [50, 80], [150, 80], [150, 130]],
	[[0, 0], [3, 0.5], [100, 2]],
	[[0, 0], [97, 98], [100, 100]],
	[[200, 100], [150, 100], [100, 40], [100, 0]],
	[[10, 10], [10, 14], [12, 60], [80, 61], [83, 61]],
	[[0, 0], [100, 0]],
	...[3, 4, 6].map(randomPoints),
]
const arrowPairs = [
	['none', 'none'],
	['none', 'arrow_point'],
	['arrow_point', 'arrow_point'],
	['aggregation', 'extension'],
	['composition', 'dependency'],
	['lollipop', 'none'],
	['none', 'arrow_barb'],
	['none', 'arrow_barb_neo'],
	['only_one', 'zero_or_more'],
	['arrow_circle', 'arrow_cross'],
]
for (const curve of curveNames) {
	for (const points of edgeLists) {
		for (const [start, end] of arrowPairs) {
			curveLines.push(`edge\t${curve}\t${start}\t${end}\t${pointsText(points)}\t${edgePath(points, curve, start, end).d}`)
		}
	}
}
// V8's results for the Math functions the rounded curve and the marker offsets print in full. The sin and cos inputs
// stay below 2^19 times pi/2, the reach of the port's argument reduction.
const signed = () => (random() < 0.5 ? -1 : 1) * random() * 10 ** (random() * 24 - 12)
const angles = [0, -0, Math.PI / 4, Math.PI / 2, Math.PI, 3 * Math.PI / 4, -Math.PI, 2 * Math.PI, 1e-9, 1e5, 8e5]
	.concat(Array.from({ length: 1500 }, () => (random() * 2 - 1) * 4 * Math.PI))
	.concat(Array.from({ length: 300 }, () => Math.round(random() * 400 - 200) * Math.PI / 2 + (random() - 0.5) * 1e-6))
	.concat(Array.from({ length: 200 }, () => (random() * 2 - 1) * 8e5))
const mathCalls = {
	acos: [[1], [-1], [0], [-0], [0.5], [-0.5], [1e-20], [0.9999999999], [-0.9999999999], [1.5], ...Array.from({ length: 2000 }, () => [random() * 2 - 1])],
	atan: [[0], [-0], [1], [-1], [1e-30], [1e30], [Infinity], [-Infinity], [0.4375], [1.1875], [2.4375], ...Array.from({ length: 2000 }, () => [signed()])],
	atan2: [[0, 0], [-0, 0], [0, -0], [-0, -0], [1, 0], [-1, 0], [0, -1], [-0, -1], [1, 1], [Infinity, Infinity], [-Infinity, -Infinity],
		[Infinity, 1], [1, -Infinity], [1e300, 1e-300], [1e-300, -1e300], ...Array.from({ length: 2000 }, () => [signed(), signed()])],
	sin: angles.map(a => [a]),
	cos: angles.map(a => [a]),
	hypot: [[0, 0], [3, 4], [-3, -4], [1e300, 1e300], [1e-300, 1e-300], [Infinity, 1], ...Array.from({ length: 1000 }, () => [signed(), signed()])],
}
for (const [name, calls] of Object.entries(mathCalls)) {
	for (const args of calls) curveLines.push(`math\t${name}\t${args.map(signedText).join(',')}\t${signedText(Math[name](...args))}`)
}
for (const [type, offset] of Object.entries(edges.markerOffsets)) curveLines.push(`marker-offset\t${type}\t${offset}`)
for (const [type, offset] of Object.entries(edges.markerOffsets2)) curveLines.push(`dash-offset\t${type}\t${offset}`)

fs.writeFileSync(curvesFile, [
	'# d3-shape curves and mermaid\'s edge paths on fixed points. Refresh with: mise run curves',
	`# mermaid ${version}`,
	`# d3-shape ${installed('d3-shape')}`,
	`# d3-path ${installed('d3-path')}`,
	...curveLines,
].join('\n') + '\n')

// The markers, with the theme variables the neo markers read set to their names in braces.
config.setSiteConfig({ themeVariables: { strokeWidth: '{strokeWidth}', mainBkg: '{mainBkg}', transitionColor: '{transitionColor}' } })
const markerLines = []
for (const set of keys('markers')) {
	const { node, selection } = element('root')
	edges.markers_default(selection, [set], 'TYPE', 'ID')
	const walk = (n, depth) => {
		const fields = [...n.attrs.map(([k, v]) => `${k}=${escape(v)}`), ...n.styles.map(([k, v]) => `style:${k}=${escape(v)}`)]
		markerLines.push(['element', set, depth, n.tag, ...fields].join('\t'))
		for (const child of n.children) walk(child, depth + 1)
	}
	for (const child of node.children) walk(child, 0)
}
for (const type of [...keys('arrowTypesMap'), 'none', 'arrow_open', 'md_parent', 'unknown']) {
	for (const look of ['classic', 'neo']) {
		const { start, end } = edgePath([[0, 0], [0, 100]], 'linear', type, type, look)
		markerLines.push(`arrow\t${type}\t${look}\t${start}\t${end}`)
	}
}

fs.writeFileSync(markersFile, [
	'# mermaid\'s edge markers for the diagram id ID and type TYPE. Refresh with: mise run curves',
	`# mermaid ${version}`,
	...markerLines,
].join('\n') + '\n')
console.log(`${curveLines.length} curve lines, ${markerLines.length} marker lines`)
